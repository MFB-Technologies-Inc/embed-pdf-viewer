/**
 * Which Rust lines the vector replay reaches, and which only other tests do.
 *
 *   pnpm --filter @embedpdf/tooling-plugin-conformance run rust-coverage
 *   pnpm --filter @embedpdf/tooling-plugin-conformance run rust-coverage:check
 *
 * Needs `cargo-llvm-cov` and the `llvm-tools-preview` rustup component.
 * Measures the crates `replay:check` runs (`replayPackages`: plugin-conformance
 * and every workspace member that depends on it) twice under
 * `cargo llvm-cov`:
 *
 * - replay: the `conformance` integration test target of each plugin crate
 *   (`tests/conformance.rs`) and plugin-conformance's own `kernel` target,
 *   which replays the kernel vectors and the function-table suites. Every
 *   test in those targets runs, including the few that are not
 *   `assert_suite` calls.
 * - all tests: `--tests` over the same crates, so the library unit tests and
 *   every integration test target. Doc tests are not measured.
 *
 * `vectors/rust-coverage.json` records, per source file, `replayLines`
 * (lines replay hit / lines llvm-cov maps) and `unitOnlyLines`: the lines
 * some test hits and replay does not, as ranges. Line counts follow
 * `llvm-cov export -format=lcov`, computed from the JSON export's segments.
 * The lines of inline `#[cfg(test)] mod` blocks are test code, not the port,
 * so they are left out of every count; integration test files under
 * `tests/` are already outside cargo-llvm-cov's report.
 * Files matching a glob in `crates/plugin-conformance/rust-coverage-exclude.json`
 * are dropped before the totals; each glob carries its reason.
 *
 * This records, it does not gate on a threshold. `--check` fails only when
 * the file would change, like `coverage:check`: a unit-only line that
 * appears or disappears shows up in review.
 */
import { spawnSync } from 'node:child_process';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, isAbsolute, join, relative, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

import { ranges } from './coverage';
import { cargoMetadata, replayPackages } from './replay-check';

/** One `llvm-cov export` segment: line, column, count, has count, region
 *  entry, gap region. */
export type Segment = [number, number, number, boolean, boolean, boolean];

/** The part of `llvm-cov export -format=json` this reads. */
export interface LlvmCovExport {
  readonly data: readonly {
    readonly files: readonly { readonly filename: string; readonly segments: readonly Segment[] }[];
  }[];
}

export interface Exclusion {
  readonly glob: string;
  readonly reason: string;
}

export interface FileLines {
  readonly replayLines: string;
  readonly unitOnlyLines: string[];
}

export interface RustCoverage {
  readonly total: {
    readonly replayLines: string;
    readonly unitOnlyLines: number;
    readonly files: number;
    readonly excludedFiles: number;
  };
  readonly files: Record<string, FileLines>;
}

/**
 * The execution count of every line llvm-cov maps in one file, by line
 * number, ascending. The same rule as LLVM's `LineCoverageStats`: a line is
 * mapped when a counted region starts on it or a counted region wraps into
 * it, unless a skipped region starts it; its count is the largest of the
 * wrapped count and the counts of the regions that start on it.
 */
export function lineCounts(segments: readonly Segment[]): Map<number, number> {
  const counts = new Map<number, number>();
  const startsRegion = (s: Segment): boolean => s[3] && s[4] && !s[5];
  let wrapped: Segment | null = null;
  let next = 0;
  if (!segments.length) return counts;
  for (let line = segments[0][0]; next < segments.length; line += 1) {
    const onLine: Segment[] = [];
    while (next < segments.length && segments[next][0] === line) onLine.push(segments[next++]);
    const starts = onLine.filter(startsRegion);
    const skipped = onLine.length > 0 && !onLine[0][3] && onLine[0][4];
    if (!skipped && ((wrapped && wrapped[3]) || starts.length)) {
      let count = wrapped ? wrapped[2] : 0;
      for (const s of starts) count = Math.max(count, s[2]);
      counts.set(line, count);
    }
    if (onLine.length) wrapped = onLine[onLine.length - 1];
  }
  return counts;
}

const TEST_MODULE = /#\[cfg\(test\)\]\s*(?:pub(?:\([^)]*\))?\s+)?mod\s+\w+\s*\{/y;

/**
 * The lines of every inline `#[cfg(test)] mod name { ... }` in `source`, from
 * the attribute through the closing brace. Braces inside comments, string
 * and raw string literals and char literals do not count. Throws when a
 * module never closes, which means the scan misread the file.
 */
export function testModuleLines(source: string): Set<number> {
  const lines = new Set<number>();
  let line = 1;
  let depth = 0;
  /** The depth a test module's closing brace returns to, and its first line. */
  let open: { depth: number; line: number } | null = null;
  const advance = (to: number, from: number): void => {
    for (let k = from; k < to; k += 1) if (source[k] === '\n') line += 1;
  };
  let i = 0;
  while (i < source.length) {
    const c = source[i];
    let next = i + 1;
    if (source.startsWith('//', i)) {
      next = source.indexOf('\n', i);
      if (next < 0) next = source.length;
    } else if (source.startsWith('/*', i)) {
      let nest = 0;
      for (next = i; next < source.length; next += 1) {
        if (source.startsWith('/*', next)) {
          nest += 1;
          next += 1;
        } else if (source.startsWith('*/', next)) {
          nest -= 1;
          next += 1;
          if (nest === 0) break;
        }
      }
      next += 1;
    } else if (
      (c === 'r' || c === 'b' || c === 'c') &&
      !/\w/.test(source[i - 1] ?? '') &&
      /^[bc]?r#*"/.test(source.slice(i, i + 260))
    ) {
      const hashes = /^[bc]?r(#*)"/.exec(source.slice(i, i + 260))![1];
      const close = source.indexOf(`"${hashes}`, source.indexOf('"', i) + 1);
      next = close < 0 ? source.length : close + 1 + hashes.length;
    } else if (c === '"') {
      for (next = i + 1; next < source.length && source[next] !== '"'; next += 1) {
        if (source[next] === '\\') next += 1;
      }
      next += 1;
    } else if (c === "'") {
      // A char literal, or a lifetime or label (`'a`), which has no closing quote.
      const literal = /^'(?:\\(?:u\{[0-9a-fA-F]+\}|x[0-9a-fA-F]{2}|.)|[^\\'])'/su.exec(
        source.slice(i, i + 12),
      );
      if (literal) next = i + literal[0].length;
    } else if (c === '#') {
      TEST_MODULE.lastIndex = i;
      const m = TEST_MODULE.exec(source);
      if (m && !open) {
        open = { depth, line };
        next = i + m[0].length;
        depth += 1;
      }
    } else if (c === '{') depth += 1;
    else if (c === '}') {
      depth -= 1;
      if (open && depth === open.depth) {
        for (let l = open.line; l <= line; l += 1) lines.add(l);
        open = null;
      }
    }
    advance(next, i);
    i = next;
  }
  if (open) throw new Error(`the test module at line ${open.line} never closes`);
  return lines;
}

/** `glob` as a whole-path pattern: `**` crosses directories, `*` does not. */
function globRegExp(glob: string): RegExp {
  let out = '';
  for (let i = 0; i < glob.length; i += 1) {
    if (glob.startsWith('**/', i)) {
      out += '(?:.*/)?';
      i += 2;
    } else if (glob.startsWith('**', i)) {
      out += '.*';
      i += 1;
    } else if (glob[i] === '*') out += '[^/]*';
    else out += glob[i].replace(/[.+?^${}()|[\]\\]/g, '\\$&');
  }
  return new RegExp(`^${out}$`);
}

/** Line counts by repo-relative path; files outside `root` are dropped. */
function byPath(report: LlvmCovExport, root: string): Map<string, Map<number, number>> {
  const out = new Map<string, Map<number, number>>();
  for (const unit of report.data) {
    for (const file of unit.files) {
      const path = relative(root, file.filename);
      if (path.startsWith('..') || isAbsolute(path)) continue;
      out.set(path, lineCounts(file.segments));
    }
  }
  return out;
}

/**
 * Per file, how many of its lines `replay` hit and which lines `all` hit
 * that `replay` did not. Paths are relative to `root`; files matching an
 * exclusion are left out of the files and the totals, and the lines
 * `testLines` gives for a file are left out of its counts.
 */
export function unitOnly(
  replay: LlvmCovExport,
  all: LlvmCovExport,
  options: {
    readonly root: string;
    readonly exclude: readonly Exclusion[];
    /** Lines of a file that are test code, left out of every count. */
    readonly testLines?: (path: string) => Set<number>;
  },
): RustCoverage {
  const excluded = options.exclude.map((e) => globRegExp(e.glob));
  const replayed = byPath(replay, options.root);
  const files: Record<string, FileLines> = {};
  let mapped = 0;
  let hit = 0;
  let unitOnlyLines = 0;
  let excludedFiles = 0;
  for (const [path, counts] of [...byPath(all, options.root)].sort(([a], [b]) =>
    a.localeCompare(b),
  )) {
    if (excluded.some((re) => re.test(path))) {
      excludedFiles += 1;
      continue;
    }
    const replayCounts = replayed.get(path) ?? new Map<number, number>();
    const tests = options.testLines?.(path) ?? new Set<number>();
    const lines = [...counts.keys()].filter((line) => !tests.has(line));
    const replayHit = lines.filter((line) => (replayCounts.get(line) ?? 0) > 0);
    const only = lines.filter(
      (line) => counts.get(line)! > 0 && !((replayCounts.get(line) ?? 0) > 0),
    );
    mapped += lines.length;
    hit += replayHit.length;
    unitOnlyLines += only.length;
    files[path] = {
      replayLines: `${replayHit.length}/${lines.length}`,
      unitOnlyLines: ranges(only),
    };
  }
  return {
    total: {
      replayLines: `${hit}/${mapped}`,
      unitOnlyLines,
      files: Object.keys(files).length,
      excludedFiles,
    },
    files,
  };
}

const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repoRoot = resolve(packageRoot, '..', '..');
const target = join(packageRoot, 'vectors', 'rust-coverage.json');
const excludeFile = join(repoRoot, 'crates', 'plugin-conformance', 'rust-coverage-exclude.json');

/** The environment cargo runs under: `base` without the variables that
 *  filter the replay or weaken its comparison. */
export function childEnv(base: NodeJS.ProcessEnv): NodeJS.ProcessEnv {
  const env: NodeJS.ProcessEnv = { ...base };
  // A filtered or reordered replay would measure less than the vectors hold.
  delete env.PLUGIN_CONFORMANCE_ONLY;
  delete env.PLUGIN_CONFORMANCE_MEMBER_ORDER;
  delete env.PLUGIN_CONFORMANCE_MEMBER_ORDER_ALLOW;
  delete env.PLUGIN_CONFORMANCE_RECEIPTS;
  return env;
}

function cargo(args: readonly string[]): void {
  const env = childEnv(process.env);
  const run = spawnSync('cargo', args, {
    cwd: repoRoot,
    env,
    stdio: ['ignore', 'inherit', 'inherit'],
  });
  if (run.error) throw run.error;
  if (run.status !== 0) {
    console.error(
      `cargo ${args.slice(0, 3).join(' ')} ... exited ${run.status}; replay:check first`,
    );
    process.exit(1);
  }
}

/** Run `targets` of `packages` under coverage from fresh profiles and return
 *  the JSON export. */
async function measure(
  packages: readonly string[],
  targets: readonly string[],
  dir: string,
  name: string,
): Promise<LlvmCovExport> {
  const select = packages.flatMap((p) => ['-p', p]);
  cargo(['llvm-cov', 'clean', '--profraw-only']);
  cargo(['llvm-cov', '--no-report', '--no-fail-fast', ...select, ...targets]);
  const out = join(dir, `${name}.json`);
  cargo(['llvm-cov', 'report', '--json', ...select, '--output-path', out]);
  return JSON.parse(await readFile(out, 'utf8')) as LlvmCovExport;
}

async function main(): Promise<void> {
  const check = process.argv.includes('--check');
  const packages = replayPackages(cargoMetadata());
  const exclude = JSON.parse(await readFile(excludeFile, 'utf8')) as Exclusion[];
  const dir = await mkdtemp(join(tmpdir(), 'rust-coverage-'));
  let result: RustCoverage;
  try {
    console.log(`measuring ${packages.length} crates: ${packages.join(', ')}`);
    const replay = await measure(
      packages,
      ['--test', 'conformance', '--test', 'kernel'],
      dir,
      'replay',
    );
    const all = await measure(packages, ['--tests'], dir, 'all');
    const sources = new Map<string, string>();
    for (const unit of all.data) {
      for (const file of unit.files)
        sources.set(relative(repoRoot, file.filename), await readFile(file.filename, 'utf8'));
    }
    result = unitOnly(replay, all, {
      root: repoRoot,
      exclude,
      testLines: (path) => testModuleLines(sources.get(path) ?? ''),
    });
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
  const text = `${JSON.stringify(result, null, 2)}\n`;
  const { total } = result;
  const summary = `replay reaches ${total.replayLines} lines; ${total.unitOnlyLines} lines only other tests reach, in ${total.files} files (${total.excludedFiles} excluded)`;
  if (check) {
    const current = await readFile(target, 'utf8').catch(() => null);
    if (current !== text) {
      console.error(
        'vectors/rust-coverage.json is stale: run `pnpm --filter @embedpdf/tooling-plugin-conformance run rust-coverage` and review the diff',
      );
      process.exit(1);
    }
    console.log(`rust coverage is current: ${summary}`);
  } else {
    await writeFile(target, text);
    console.log(`wrote vectors/rust-coverage.json: ${summary}`);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  await main();
}
