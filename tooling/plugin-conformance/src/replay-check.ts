/**
 * Proves the Rust replay ran every case the generator writes.
 *
 *   pnpm --filter @embedpdf/tooling-plugin-conformance run replay:check
 *   pnpm --filter @embedpdf/tooling-plugin-conformance run replay:check -- --receipts <dir>
 *
 * Runs `cargo test --tests --no-fail-fast` over `plugin-conformance` and every
 * workspace crate with a normal or dev dependency on it (read from
 * `cargo metadata` at run time), with `PLUGIN_CONFORMANCE_RECEIPTS` pointing at a fresh temporary directory (and
 * `PLUGIN_CONFORMANCE_ONLY` removed), so every `assert_suite` call writes a
 * receipt naming the cases it ran. Then it compares the union of the receipts
 * with the case names of every suite in `src/suites/`. A receipt shows a case
 * ran, not that it passed, so cargo's exit status counts too: the check fails
 * if cargo failed or any receipt problem exists, and reports both.
 *
 * `--receipts <dir>` skips cargo and reads `<dir>` as it is. Point it only at
 * a directory one full run filled: receipts from older runs would cover cases
 * the current tests no longer run.
 *
 * `vectors:check` keeps its source scan (a `.rs` file must name each vector
 * file as a string literal) as a fast offline lint; this is the proof.
 */
import { spawnSync } from 'node:child_process';
import { mkdtemp, readdir, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

import type { Suite } from './vectors';

/** What one Rust `assert_suite` call replayed (`crates/plugin-conformance/src/receipt.rs`). */
export interface Receipt {
  readonly file: string;
  readonly only: string | null;
  readonly ran: readonly string[];
  readonly exceptions: readonly string[];
  readonly memberOrderAllowed: readonly string[];
}

/**
 * One problem line per failure; empty means every expected case of every
 * expected file ran, unfiltered, and nothing ran that the generator does not
 * write. `expected` maps a vector file to its case names.
 */
export function compareReceipts(expected: Map<string, string[]>, receipts: Receipt[]): string[] {
  const problems: string[] = [];
  const byFile = new Map<string, Receipt[]>();
  for (const receipt of receipts) {
    const list = byFile.get(receipt.file) ?? [];
    list.push(receipt);
    byFile.set(receipt.file, list);
  }
  for (const file of [...new Set([...expected.keys(), ...byFile.keys()])].sort()) {
    const names = expected.get(file);
    const got = byFile.get(file) ?? [];
    if (!names) {
      problems.push(`${file}: unknown vector file (no suite writes it)`);
      continue;
    }
    if (!got.length) {
      problems.push(`${file}: no Rust test replayed it`);
      continue;
    }
    for (const only of [...new Set(got.flatMap((r) => (r.only === null ? [] : [r.only])))].sort()) {
      problems.push(`${file}: a replay ran filtered by PLUGIN_CONFORMANCE_ONLY="${only}"`);
    }
    const ran = new Set(got.flatMap((r) => r.ran));
    const want = new Set(names);
    for (const name of names)
      if (!ran.has(name)) problems.push(`${file}: case "${name}" never ran`);
    for (const name of [...ran].sort()) {
      if (!want.has(name))
        problems.push(`${file}: case "${name}" ran but no suite writes it (stale vector file)`);
    }
  }
  return problems;
}

const isStrings = (value: unknown): boolean =>
  Array.isArray(value) && value.every((item) => typeof item === 'string');

/** Why `value` is not a receipt, or null when it is one. */
function malformed(value: unknown): string | null {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return 'not an object';
  const r = value as Record<string, unknown>;
  if (typeof r.file !== 'string') return '"file" is not a string';
  if (r.only !== null && typeof r.only !== 'string') return '"only" is not a string or null';
  if (!isStrings(r.ran)) return '"ran" is not an array of strings';
  if (!Array.isArray(r.exceptions)) return '"exceptions" is not an array';
  if (!Array.isArray(r.memberOrderAllowed)) return '"memberOrderAllowed" is not an array';
  return null;
}

/** Every complete receipt in `dir`, and one problem line per file that is not
 *  a receipt. The Rust side writes `*.partial` first and renames, so only
 *  `*.json` names are finished. */
export async function readReceipts(
  dir: string,
): Promise<{ receipts: Receipt[]; problems: string[] }> {
  const receipts: Receipt[] = [];
  const problems: string[] = [];
  for (const name of (await readdir(dir)).filter((n) => n.endsWith('.json')).sort()) {
    let value: unknown;
    try {
      value = JSON.parse(await readFile(join(dir, name), 'utf8'));
    } catch (error) {
      problems.push(`${name}: malformed receipt (${(error as Error).message})`);
      continue;
    }
    const why = malformed(value);
    if (why) problems.push(`${name}: malformed receipt (${why})`);
    else receipts.push(value as Receipt);
  }
  return { receipts, problems };
}

/** The part of `cargo metadata --format-version 1 --no-deps` this reads. */
export interface CargoMetadata {
  readonly workspace_members: readonly string[];
  readonly packages: readonly {
    readonly id: string;
    readonly name: string;
    readonly dependencies: readonly { readonly name: string; readonly kind: string | null }[];
  }[];
}

const CONFORMANCE_CRATE = 'plugin-conformance';

/** `plugin-conformance` and every workspace member with a normal or dev
 *  dependency on it, sorted: the crates whose tests can call `assert_suite`. */
export function replayPackages(metadata: CargoMetadata): string[] {
  const members = new Set(metadata.workspace_members);
  return metadata.packages
    .filter((p) => members.has(p.id))
    .filter(
      (p) =>
        p.name === CONFORMANCE_CRATE ||
        p.dependencies.some(
          (d) => d.name === CONFORMANCE_CRATE && (d.kind === null || d.kind === 'dev'),
        ),
    )
    .map((p) => p.name)
    .sort();
}

const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repoRoot = resolve(packageRoot, '..', '..');

/** The case names of every suite `src/generate.ts` discovers, by vector file.
 *  Read from the suite definitions, which is what the generator writes the
 *  names from; no case is run. */
async function expectedCases(): Promise<Map<string, string[]>> {
  const suitesDir = join(packageRoot, 'src', 'suites');
  const expected = new Map<string, string[]>();
  for (const file of (await readdir(suitesDir)).filter((f) => f.endsWith('.ts')).sort()) {
    const module = (await import(pathToFileURL(join(suitesDir, file)).href)) as {
      suites?: Suite[];
    };
    if (!module.suites) throw new Error(`src/suites/${file} exports no suites`);
    for (const suite of module.suites) {
      if (expected.has(suite.file)) throw new Error(`two suites write ${suite.file}`);
      expected.set(
        suite.file,
        suite.cases.map((c) => c.name),
      );
    }
  }
  return expected;
}

/** Read the workspace's packages from cargo. */
function cargoMetadata(): CargoMetadata {
  const run = spawnSync('cargo', ['metadata', '--format-version', '1', '--no-deps'], {
    cwd: repoRoot,
    encoding: 'utf8',
    maxBuffer: 256 * 1024 * 1024,
    stdio: ['ignore', 'pipe', 'inherit'],
  });
  if (run.error) throw run.error;
  if (run.status !== 0) throw new Error(`cargo metadata exited ${run.status}`);
  return JSON.parse(run.stdout) as CargoMetadata;
}

/** Run the tests of `packages` with receipts on; true when cargo passed. */
function runCargo(packages: readonly string[], receipts: string): boolean {
  const env: NodeJS.ProcessEnv = { ...process.env, PLUGIN_CONFORMANCE_RECEIPTS: receipts };
  delete env.PLUGIN_CONFORMANCE_ONLY;
  const args = ['test', ...packages.flatMap((p) => ['-p', p]), '--tests', '--no-fail-fast'];
  console.log(
    `running cargo test over ${packages.length} crates (${packages.join(', ')}) with receipts in ${receipts}`,
  );
  const run = spawnSync('cargo', args, { cwd: repoRoot, env, stdio: 'inherit' });
  if (run.error) throw run.error;
  return run.status === 0;
}

async function main(): Promise<void> {
  const argv = process.argv.slice(2);
  const at = argv.indexOf('--receipts');
  const given = at >= 0 ? argv[at + 1] : undefined;
  if (at >= 0 && !given) throw new Error('--receipts needs a directory');

  const expected = await expectedCases();
  const dir = given
    ? resolve(given)
    : await mkdtemp(join(tmpdir(), 'plugin-conformance-receipts-'));
  let cargoPassed: boolean | null = null;
  let read: { receipts: Receipt[]; problems: string[] };
  try {
    if (!given) cargoPassed = runCargo(replayPackages(cargoMetadata()), dir);
    read = await readReceipts(dir);
  } finally {
    if (!given) await rm(dir, { recursive: true, force: true });
  }

  const { receipts } = read;
  const problems = [...read.problems, ...compareReceipts(expected, receipts)];
  const cases = [...expected.values()].reduce((n, names) => n + names.length, 0);
  console.log('');
  if (cargoPassed === null) console.log(`cargo: not run (read receipts from ${dir})`);
  else console.log(`cargo: ${cargoPassed ? 'passed' : 'FAILED (see the test output above)'}`);
  console.log(`receipts: ${receipts.length} read`);
  if (problems.length) {
    console.error(`${problems.length} replay problems:`);
    for (const line of problems) console.error(`  ${line}`);
  } else {
    console.log(`${expected.size} vector files, ${cases} cases, all replayed`);
  }
  if (problems.length || cargoPassed === false) process.exitCode = 1;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  await main();
}
