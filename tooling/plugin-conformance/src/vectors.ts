/**
 * Suites, and the files they write.
 *
 * A suite is either scenarios (`scenario.ts`) or function cases: a pure
 * function called with canonical arguments, and its canonical outcome. Both
 * kinds land in `vectors/<suite>.json` under one envelope the Rust runner
 * asserts the version of. On disk a suite is that file or, zstd-compressed,
 * `vectors/<suite>.json.zst`; everything that names a suite still uses the
 * `.json` name.
 */
import { mkdir, readdir, readFile, rm, writeFile } from 'node:fs/promises';
import { dirname, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import zlib from 'node:zlib';

import { canonical, canonicalError, isThenable, snapshotHooks } from './canonical';
import { caseDiff, describeCaseDiff, type RenderedSuite } from './case-diff';
import { runScenario, type ScenarioCase } from './scenario';

/** Bump when the envelope or the trace changes shape. */
export const SCHEMA_VERSION = 2;

const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
export const vectorsRoot = join(packageRoot, 'vectors');
const cratesRoot = join(packageRoot, '..', '..', 'crates');

/** The ICU the vectors were written with. */
const ENVIRONMENT_FILE = 'environment.json';

/** Written by `coverage.ts`, `rust-coverage.ts` or by `emit` itself, not by
 *  a suite, and replayed by nothing. */
const NOT_A_SUITE = new Set(['coverage.json', 'rust-coverage.json', ENVIRONMENT_FILE]);

/**
 * Node's ICU decides what some cases print: `Intl.PluralRules` (i18n
 * plurals) and `localeCompare` (render tiles, sorting). A Node with another
 * ICU can write other vectors from the same TypeScript, so a write records
 * the versions and a check compares against them.
 */
interface Environment {
  readonly icu: string | null;
  readonly unicode: string | null;
}

function currentEnvironment(): Environment {
  return { icu: process.versions.icu ?? null, unicode: process.versions.unicode ?? null };
}

async function recordedEnvironment(): Promise<Environment | null> {
  const text = await readFile(join(vectorsRoot, ENVIRONMENT_FILE), 'utf8').catch(() => null);
  return text === null ? null : (JSON.parse(text) as Environment);
}

function describeEnvironment(environment: Environment): string {
  return `ICU ${environment.icu ?? 'none'} (Unicode ${environment.unicode ?? 'none'})`;
}

/** The difference between the recorded and the running ICU, or null. */
async function environmentDrift(): Promise<string | null> {
  const recorded = await recordedEnvironment();
  const current = currentEnvironment();
  if (!recorded) return `vectors/${ENVIRONMENT_FILE} is missing, so the ICU the vectors were written with is unknown`;
  if (recorded.icu === current.icu && recorded.unicode === current.unicode) return null;
  return `the vectors were written with ${describeEnvironment(recorded)}, and this Node (${process.version}) has ${describeEnvironment(current)}`;
}

async function writeEnvironment(): Promise<void> {
  const recorded = await recordedEnvironment();
  const current = currentEnvironment();
  if (recorded && (recorded.icu !== current.icu || recorded.unicode !== current.unicode)) {
    console.log(`ICU changed: ${describeEnvironment(recorded)} to ${describeEnvironment(current)}; review i18n and sorting cases in the diff`);
  }
  await writeFile(join(vectorsRoot, ENVIRONMENT_FILE), `${JSON.stringify(current, null, 2)}\n`);
}

/** Whether a write compresses a suite that has no `.json.zst` yet. A suite
 *  already compressed stays compressed either way. */
export const COMPRESS_NEW_VECTORS = true;

/** The zstd level a write compresses at. Check mode never compares
 *  compressed bytes, so another level or another zstd build is not a
 *  difference. */
const ZSTD_LEVEL = 3;

const ZSTD = '.zst';

/** Node's zstd, which arrived in Node 22.15. */
function zstd(): { compress: typeof zlib.zstdCompressSync; decompress: typeof zlib.zstdDecompressSync } {
  if (typeof zlib.zstdCompressSync !== 'function' || typeof zlib.zstdDecompressSync !== 'function') {
    throw new Error(`vector compression needs Node 22.15 or later, running ${process.version}`);
  }
  return { compress: zlib.zstdCompressSync, decompress: zlib.zstdDecompressSync };
}

const missing = (error: NodeJS.ErrnoException): null => {
  if (error.code === 'ENOENT') return null;
  throw error;
};

/** The text of the suite at `path` (its `.json` name): `<path>.zst`
 *  decompressed if it exists, else `<path>`, else null. */
export async function readVector(path: string): Promise<string | null> {
  const compressed = await readFile(`${path}${ZSTD}`).catch(missing);
  if (compressed) return zstd().decompress(compressed).toString('utf8');
  return readFile(path, 'utf8').catch(missing);
}

/** Write `text` as the suite at `path` (its `.json` name), compressed when
 *  `compress` is set or a `.json.zst` already exists, and leave one form
 *  on disk: a plain file beside a current `.json.zst` is removed. False when
 *  the file already held `text` in that form and nothing else was there. */
export async function writeVector(
  path: string,
  text: string,
  { compress = COMPRESS_NEW_VECTORS }: { compress?: boolean } = {},
): Promise<boolean> {
  const compressed = await readFile(`${path}${ZSTD}`).catch(missing);
  if (compressed || compress) {
    const { compress: deflate, decompress } = zstd();
    if (compressed && decompress(compressed).toString('utf8') === text) {
      // The plain file a reader would never see; removing it heals a suite
      // `vectors:check` reports as both plain and compressed.
      return (await rm(path).then(() => true).catch(missing)) ?? false;
    }
    await mkdir(dirname(path), { recursive: true });
    const level = { [zlib.constants.ZSTD_c_compressionLevel]: ZSTD_LEVEL };
    await writeFile(`${path}${ZSTD}`, deflate(text, { params: level }));
    await rm(path, { force: true });
    return true;
  }
  if ((await readFile(path, 'utf8').catch(missing)) === text) return false;
  await mkdir(dirname(path), { recursive: true });
  await writeFile(path, text);
  return true;
}

/** Compare (`check`) or write (`write`) one suite's rendered text against
 *  its file, by decompressed text only. */
export async function syncVector(path: string, text: string, mode: 'write' | 'check'): Promise<'current' | 'stale' | 'wrote'> {
  if (mode === 'check') return (await readVector(path)) === text ? 'current' : 'stale';
  return (await writeVector(path, text)) ? 'wrote' : 'current';
}

async function filesUnder(root: string, keep: (name: string) => boolean): Promise<string[]> {
  const out: string[] = [];
  for (const entry of await readdir(root, { withFileTypes: true })) {
    const path = join(root, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === 'target' || entry.name === 'node_modules') continue;
      out.push(...(await filesUnder(path, keep)));
    } else if (keep(entry.name)) {
      out.push(path);
    }
  }
  return out;
}

/**
 * Vector files no suite writes (left behind by a deleted or renamed suite),
 * and vector files no Rust source names (a suite with no `assert_suite`).
 * Either one stays green under a content check alone.
 *
 * "Replayed" is read from the source: some `.rs` file under `crates/` must
 * hold the path as a string literal, `"plugins/actions.json"`, which is how
 * every `assert_suite` call and replay helper names its file. A literal in a
 * test that never runs would pass; a missing one cannot.
 */
async function unaccounted(
  written: ReadonlySet<string>,
  only?: string,
): Promise<{ orphaned: string[]; unreplayed: string[]; doubled: string[] }> {
  // A `.json.zst` counts under its `.json` name.
  const files = (await filesUnder(vectorsRoot, (name) => name.endsWith('.json') || name.endsWith(`.json${ZSTD}`)))
    .map((path) => relative(vectorsRoot, path).split(sep).join('/').replace(/\.zst$/, ''))
    .filter((file) => !NOT_A_SUITE.has(file) && (!only || file.startsWith(only)))
    .sort();
  // Both forms of one suite: the reader takes the `.zst`, and the plain file
  // goes stale unseen.
  const doubled = files.filter((file, index) => files[index + 1] === file);
  const onDisk = [...new Set(files)];
  const orphaned = onDisk.filter((file) => !written.has(file));
  const rust = (
    await Promise.all((await filesUnder(cratesRoot, (name) => name.endsWith('.rs'))).map((path) => readFile(path, 'utf8')))
  ).join('\n');
  const unreplayed = onDisk.filter((file) => written.has(file) && !rust.includes(`"${file}"`));
  return { orphaned, unreplayed, doubled };
}

export interface FunctionCase {
  readonly name: string;
  /** A function name from the suite's `functions` table. */
  readonly fn: string;
  readonly args: readonly unknown[];
  readonly covers?: string;
}

export interface ScenarioSuite {
  readonly kind: 'scenario';
  /** `vectors/<file>`, e.g. `kernel/lifecycle.json`. */
  readonly file: string;
  readonly describes: string;
  readonly cases: readonly ScenarioCase[];
}

export interface FunctionSuite {
  readonly kind: 'function';
  readonly file: string;
  readonly describes: string;
  /** The functions under test, by the name cases use. Arguments arrive
   *  decoded (`$bytes`, `$map`, `$undefined`, ...). */
  readonly functions: Readonly<Record<string, (...args: never[]) => unknown>>;
  readonly cases: readonly FunctionCase[];
}

export type Suite = ScenarioSuite | FunctionSuite;

function decodePlain(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(decodePlain);
  if (value === null || typeof value !== 'object') return value;
  const object = value as Record<string, unknown>;
  if ('$undefined' in object) return undefined;
  if ('$number' in object) return Number(object.$number);
  if ('$bytes' in object) return new Uint8Array(Buffer.from(object.$bytes as string, 'base64'));
  if ('$map' in object) {
    return new Map((object.$map as [unknown, unknown][]).map(([k, v]) => [decodePlain(k), decodePlain(v)]));
  }
  if ('$set' in object) return new Set((object.$set as unknown[]).map(decodePlain));
  const out: Record<string, unknown> = {};
  for (const [key, member] of Object.entries(object)) out[key] = decodePlain(member);
  return out;
}

async function renderSuite(suite: Suite): Promise<unknown> {
  if (suite.kind === 'function') {
    const cases = [];
    for (const entry of suite.cases) {
      const fn = suite.functions[entry.fn];
      if (!fn) throw new Error(`[${suite.file}] no function "${entry.fn}"`);
      // Encode the arguments first and call with their decoding, so the
      // function sees exactly what the file records: a raw `NaN` would
      // otherwise reach the TypeScript and print as `null` for the Rust side.
      const args = canonical(entry.args) as unknown[];
      let outcome: unknown;
      try {
        const result = (fn as (...args: unknown[]) => unknown)(...args.map(decodePlain));
        if (isThenable(result)) throw new Error(`[${suite.file}] ${entry.name}: function cases must be synchronous`);
        outcome = { returned: canonical(result, snapshotHooks) };
      } catch (error) {
        outcome = { threw: canonicalError(error, snapshotHooks) };
      }
      cases.push({ ...entry, args, outcome });
    }
    return { schemaVersion: SCHEMA_VERSION, kind: 'function', describes: suite.describes, cases };
  }
  const cases = [];
  for (const scenario of suite.cases) {
    // The type asks for this too; a case built through a cast still lands here.
    if ((scenario.snapshot === 'none') !== (typeof scenario.reason === 'string' && scenario.reason.trim() !== '')) {
      throw new Error(`[${suite.file}] ${scenario.name}: snapshot 'none' needs a reason, and only it takes one`);
    }
    const { trace, unmatched } = await runScenario(scenario);
    if (unmatched.length && !scenario.allowUnmatched) {
      throw new Error(`[${suite.file}] ${scenario.name}: unanswered engine calls:\n  ${unmatched.join('\n  ')}`);
    }
    cases.push({ ...scenario, trace });
  }
  return { schemaVersion: SCHEMA_VERSION, kind: 'scenario', describes: suite.describes, cases };
}

/** The line write mode prints for a file it changed: its case counts, or
 *  `wrote vectors/<file>` when no case changed (its envelope did, or only
 *  its form on disk). */
function writeSummary(file: string, before: string | null, after: RenderedSuite): string {
  let old: RenderedSuite | null = null;
  try {
    old = before === null ? null : (JSON.parse(before) as RenderedSuite);
  } catch {
    // An unreadable file before: every case reads as added.
  }
  return describeCaseDiff(file, caseDiff(old, after)) ?? `wrote vectors/${file}`;
}

/** Write each suite, or with `check` fail on any file whose content would
 *  change, any vector file no suite writes, or any no Rust test replays. `render` runs every case and keeps nothing: the coverage gate's
 *  second pass, over a filtered set of cases no file holds. */
export async function emit(suites: readonly Suite[], mode: 'write' | 'check' | 'render', only?: string): Promise<void> {
  const check = mode === 'check';
  const stale: string[] = [];
  const names = new Set<string>();
  for (const suite of suites) {
    if (names.has(suite.file)) throw new Error(`two suites write ${suite.file}`);
    names.add(suite.file);
    if (only && !suite.file.startsWith(only)) continue;
    const rendered = await renderSuite(suite);
    if (mode === 'render') continue;
    const text = `${JSON.stringify(rendered, null, 2)}\n`;
    const path = join(vectorsRoot, suite.file);
    const before = mode === 'write' ? await readVector(path) : null;
    const status = await syncVector(path, text, mode);
    if (status === 'stale') stale.push(suite.file);
    if (status === 'wrote') console.log(writeSummary(suite.file, before, rendered as RenderedSuite));
  }
  const drift = check ? await environmentDrift() : null;
  if (stale.length) {
    console.error(`stale vectors (run \`pnpm --filter @embedpdf/tooling-plugin-conformance run vectors\`):`);
    for (const file of stale) console.error(`  vectors/${file}`);
    if (drift) {
      console.error(
        `and ${drift}. An ICU difference changes plural and collation output, so these files may be stale ` +
          'only because of it. Check with the recorded ICU before regenerating.',
      );
    }
    process.exitCode = 1;
  } else if (drift) {
    // stderr directly: suites replace `console.warn` to capture plugin output.
    process.stderr.write(`warning: ${drift}. Every file still matches, but i18n plural and localeCompare cases may not on the next change.\n`);
  }
  if (mode === 'write' && !only) await writeEnvironment();
  if (check) {
    const { orphaned, unreplayed, doubled } = await unaccounted(names, only);
    if (doubled.length) {
      console.error('vector files on disk both plain and compressed (delete the plain `.json`; the `.json.zst` is the one read):');
      for (const file of doubled) console.error(`  vectors/${file}`);
      process.exitCode = 1;
    }
    if (orphaned.length) {
      console.error('vector files no suite writes (delete them, or restore the suite in src/suites/):');
      for (const file of orphaned) console.error(`  vectors/${file}`);
      process.exitCode = 1;
    }
    if (unreplayed.length) {
      console.error('vector files no Rust test replays (no `.rs` file under crates/ names them; add an `assert_suite`):');
      for (const file of unreplayed) console.error(`  vectors/${file}`);
      process.exitCode = 1;
    }
    if (!stale.length && !orphaned.length && !unreplayed.length && !doubled.length) {
      console.log(`${names.size} vector files are current, and each is replayed`);
    }
  } else if (mode === 'render') {
    console.log(`rendered ${names.size} suites`);
  }
}
