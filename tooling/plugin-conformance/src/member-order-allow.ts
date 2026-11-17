/**
 * Finds the paths where the Rust replay fails the member-order comparison
 * and `crates/plugin-conformance/member-order-allow.json` does not exempt
 * them (see the README, "Comparing"). It never edits the allowlist.
 *
 *   pnpm run member-order:allow          the failing paths the allowlist misses
 *   pnpm run member-order:allow --fresh  every failing path, from no allowance
 *
 * It runs `cargo test --tests` over `plugin-conformance` and every crate that
 * replays vectors (`replayPackages`, as `replay:check` does) with the order
 * check on and `PLUGIN_CONFORMANCE_MEMBER_ORDER_ALLOW` naming a temporary
 * file of allowed paths that replaces the checked-in list for the run. It
 * starts from the allowlist's paths (none with `--fresh`), adds every
 * member-order failure (vector file, case, path with indexes as `[]`), and
 * runs again, since the comparer reports one difference per case, until a
 * run adds nothing. Any other failure stops it.
 *
 * The failing paths the allowlist does not hold are written to
 * `member-order-candidates.json` (gitignored) and printed. With `--fresh` it
 * also prints the allowlist entries no case needs; delete those freely.
 * Moving a candidate into the allowlist is a hand edit that fills in its
 * `divergence`, the `divergences.json` id that says why. Only two causes
 * qualify: the TypeScript passes a caller's object through
 * (one `MO-` ledger entry per place it does), or one type's order depends
 * on which function built it. Anything else is a Rust order to fix.
 */
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { isMainModule } from './main-module';
import { cargoMetadata, replayPackages } from './replay-check';

/** Paths by vector file and case. */
export type Paths = Record<string, Record<string, string[]>>;
/** `member-order-allow.json`: each path cites a `divergences.json` id. */
export type AllowList = Record<string, Record<string, { path: string; divergence: string }[]>>;

export interface Run {
  readonly output: string;
  readonly status: number | null;
}

export interface FindOptions {
  readonly allowFile: string;
  readonly candidatesFile: string;
  /** Start from no allowance instead of the allowlist's paths. */
  readonly fresh: boolean;
  /** One replay with `overrideFile` as the allowed paths. */
  readonly run: (overrideFile: string) => Run;
  readonly log?: (line: string) => void;
  readonly maxRuns?: number;
}

export interface Found {
  /** Failing paths the allowlist does not hold. */
  readonly candidates: Paths;
  /** Allowlist paths no case failed on; only known with `fresh`. */
  readonly unneeded: Paths;
}

const sorted = <T>(
  paths: Record<string, Record<string, T[]>>,
): Record<string, Record<string, T[]>> => {
  const out: Record<string, Record<string, T[]>> = {};
  for (const file of Object.keys(paths).sort()) {
    const cases = Object.keys(paths[file]).filter((name) => paths[file][name].length > 0);
    if (!cases.length) continue;
    out[file] = {};
    for (const name of cases.sort()) out[file][name] = paths[file][name];
  }
  return out;
};

/** `a` without the paths `b` holds. */
const minus = (a: Paths, b: Paths): Paths => {
  const out: Paths = {};
  for (const [file, cases] of Object.entries(a))
    for (const [name, paths] of Object.entries(cases)) {
      const left = paths.filter((p) => !(b[file]?.[name] ?? []).includes(p));
      if (left.length) (out[file] ??= {})[name] = [...left].sort();
    }
  return sorted(out);
};

const count = (paths: Paths) =>
  Object.values(paths)
    .flatMap((c) => Object.values(c))
    .flat().length;

/** The member-order failures in one run's output, and every other failure line. */
export function parseFailures(output: string): {
  order: [string, string, string][];
  others: string[];
} {
  const order: [string, string, string][] = [];
  const others: string[] = [];
  let file: string | undefined;
  for (const line of output.split('\n')) {
    const header = line.match(/^(\S+\.json): \d+ of \d+ cases differ/);
    if (header) {
      file = header[1];
      continue;
    }
    if (!file || !line.startsWith('  ')) continue;
    const at = line.match(/^\s+(.+?): (?:step \d+: )?(\$\S*): member order: /);
    if (at) order.push([file, at[1], at[2].replace(/\[\d+\]/g, '[]')]);
    else if (/^\s+\S.*?: /.test(line)) others.push(`${file}: ${line.trim()}`);
  }
  return { order, others };
}

/** See the header comment. Throws when a run fails on anything but member
 *  order, or never stops adding paths. */
export function findCandidates(options: FindOptions): Found {
  const log = options.log ?? console.log;
  const maxRuns = options.maxRuns ?? 60;
  const allow = JSON.parse(readFileSync(options.allowFile, 'utf8')) as AllowList;
  const listed: Paths = {};
  for (const [file, cases] of Object.entries(allow))
    for (const [name, entries] of Object.entries(cases))
      (listed[file] ??= {})[name] = entries.map((e) => e.path);

  const failing: Paths = {};
  const allowed: Paths = options.fresh ? {} : structuredClone(listed);
  const dir = mkdtempSync(path.join(tmpdir(), 'member-order-allow-'));
  const overrideFile = path.join(dir, 'allowed.json');
  try {
    for (let run = 1; run <= maxRuns; run++) {
      writeFileSync(overrideFile, JSON.stringify(allowed));
      const result = options.run(overrideFile);
      const { order, others } = parseFailures(result.output);
      if (others.length > 0)
        throw new Error(
          `failures that aren't member order, fix these first:\n  ${others.join('\n  ')}`,
        );
      let added = 0;
      for (const [file, name, pattern] of order) {
        const paths = ((allowed[file] ??= {})[name] ??= []);
        if (paths.includes(pattern)) continue;
        paths.push(pattern);
        ((failing[file] ??= {})[name] ??= []).push(pattern);
        added++;
      }
      log(`run ${run}: ${added} failing paths found`);
      if (added > 0) continue;
      if (result.status !== 0)
        throw new Error('cargo test failed without a conformance difference; see its output');
      const candidates = minus(failing, listed);
      const unneeded = options.fresh ? minus(listed, failing) : {};
      const file: AllowList = {};
      for (const [f, cases] of Object.entries(candidates))
        for (const [name, paths] of Object.entries(cases))
          (file[f] ??= {})[name] = paths.map((p) => ({ path: p, divergence: '' }));
      writeFileSync(options.candidatesFile, JSON.stringify(file, null, 2) + '\n');
      return { candidates, unneeded };
    }
    throw new Error(`still finding paths after ${maxRuns} runs`);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
}

/** The environment each cargo run gets: member order on, `overrideFile`
 *  as the allowlist, and no filter or receipts, so every case runs and
 *  `--fresh` never mistakes a skipped case for an unneeded entry. */
export function childEnv(base: NodeJS.ProcessEnv, overrideFile: string): NodeJS.ProcessEnv {
  const env: NodeJS.ProcessEnv = {
    ...base,
    PLUGIN_CONFORMANCE_MEMBER_ORDER: '1',
    PLUGIN_CONFORMANCE_MEMBER_ORDER_ALLOW: overrideFile,
  };
  delete env.PLUGIN_CONFORMANCE_ONLY;
  delete env.PLUGIN_CONFORMANCE_RECEIPTS;
  return env;
}

const root = fileURLToPath(new URL('../../..', import.meta.url));

function main(): void {
  const packages = replayPackages(cargoMetadata());
  const candidatesFile = path.join(root, 'tooling/plugin-conformance/member-order-candidates.json');
  const found = findCandidates({
    allowFile: path.join(root, 'crates/plugin-conformance/member-order-allow.json'),
    candidatesFile,
    fresh: process.argv.includes('--fresh'),
    run: (overrideFile) => {
      const result = spawnSync(
        'cargo',
        ['test', ...packages.flatMap((p) => ['-p', p]), '--tests', '-q', '--no-fail-fast'],
        {
          cwd: root,
          encoding: 'utf8',
          env: childEnv(process.env, overrideFile),
          maxBuffer: 256 * 1024 * 1024,
        },
      );
      if (result.error) throw result.error;
      return { output: `${result.stdout}\n${result.stderr}`, status: result.status };
    },
  });
  const print = (title: string, paths: Paths) => {
    console.log(`${title}: ${count(paths)}`);
    for (const [file, cases] of Object.entries(paths))
      for (const [name, list] of Object.entries(cases))
        for (const p of list) console.log(`  ${file} / ${name} / ${p}`);
  };
  print(`candidates, written to ${path.relative(root, candidatesFile)}`, found.candidates);
  if (process.argv.includes('--fresh')) print('allowlist paths no case needs', found.unneeded);
}

if (isMainModule(import.meta.url)) {
  main();
}
