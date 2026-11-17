/**
 * Proves the Rust replay ran every case the generator writes.
 *
 *   pnpm --filter @embedpdf/tooling-plugin-conformance run replay:check
 *   pnpm --filter @embedpdf/tooling-plugin-conformance run replay:check -- --receipts <dir>
 *
 * Runs `cargo test --tests --no-fail-fast` over `plugin-conformance` and every
 * workspace crate with a normal or dev dependency on it (read from
 * `cargo metadata` at run time), with `PLUGIN_CONFORMANCE_RECEIPTS` pointing at a fresh temporary directory (and
 * `PLUGIN_CONFORMANCE_ONLY`, `PLUGIN_CONFORMANCE_MEMBER_ORDER` and
 * `PLUGIN_CONFORMANCE_MEMBER_ORDER_ALLOW` removed), so every `assert_suite` call writes a
 * receipt naming the cases it ran, and `PLUGIN_CONFORMANCE_RUN_ID` set to a
 * fresh id every receipt of the run records. Then it compares the union of
 * the receipts with the case names of every suite in `src/suites/`. Cargo's
 * exit status counts too: the check fails if cargo failed or any receipt
 * problem exists, and reports both. A receipt problem is also a receipt
 * that counts failed cases, one written with the member-order comparison
 * off or the allowlist replaced, one from another run, one that names no
 * commit or another commit than the checkout's `git rev-parse HEAD`
 * (`PLUGIN_CONFORMANCE_HEAD`, which this sets), one whose record of
 * uncommitted changes (`PLUGIN_CONFORMANCE_DIRTY`, from `git status
 * --porcelain`, which this also sets) differs from the checkout's, or one
 * whose vector text hash is not the hash of the vector file in the
 * checkout. A failing git command fails the check. It also fails when a
 * `crates/plugin-conformance/divergences.json` entry with `observedBy` is in
 * no receipt's `exceptions` (an observation that never applied), or when a
 * `crates/plugin-conformance/member-order-allow.json` path is in no
 * receipt's `memberOrderAllowed` (an allowance that never suppressed a
 * difference), or a receipt's `memberOrderAllowed` names a path the
 * allowlist does not hold.
 *
 * `--receipts <dir>` skips cargo and reads `<dir>` as it is. Every receipt
 * in it must carry the same run id and the checkout's commit, and come from
 * a clean tree while the checkout is clean, so fill it with one cargo run
 * on a clean tree that sets `PLUGIN_CONFORMANCE_RUN_ID`,
 * `PLUGIN_CONFORMANCE_HEAD` and `PLUGIN_CONFORMANCE_DIRTY=0`.
 *
 * `vectors:check` keeps its source scan (a `.rs` file must name each vector
 * file as a string literal) as a fast offline lint; this is the proof.
 */
import { spawnSync } from 'node:child_process';
import { createHash, randomUUID } from 'node:crypto';
import { mkdtemp, readdir, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

import { rejectUnknownArgs } from '../args';
import { refuseDirectRun } from '../library-guard';
import { readVector, vectorsRoot, type Suite } from '../vectors';

refuseDirectRun('src/checks/replay-check.ts', 'replay:check');

/** An allow-list path that suppressed a member-order difference in a case. */
export interface AllowanceUse {
  readonly case: string;
  readonly path: string;
}

/** What one Rust `assert_suite` call replayed (`crates/plugin-conformance/src/receipt.rs`). */
export interface Receipt {
  readonly runId: string | null;
  readonly head: string | null;
  /** Whether the tree had uncommitted changes when the run started. */
  readonly dirty: boolean | null;
  readonly vectorSha256: string | null;
  readonly file: string;
  readonly only: string | null;
  readonly ran: readonly string[];
  readonly failures: number;
  readonly memberOrder: boolean;
  readonly allowListOverridden: boolean;
  readonly exceptions: readonly string[];
  readonly memberOrderAllowed: readonly AllowanceUse[];
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

/** The part of a `divergences.json` entry this reads. */
export interface LedgerEntry {
  readonly id: string;
  readonly observedBy?: readonly unknown[];
}

/**
 * One problem line per ledger entry that has `observedBy` but that no
 * receipt lists in `exceptions`, which is where the Rust replay records each
 * observation it applied. Filtered receipts already fail
 * [`compareReceipts`], so with any of them this reports nothing.
 */
export function unappliedObservations(ledger: LedgerEntry[], receipts: Receipt[]): string[] {
  if (receipts.some((r) => r.only !== null)) return [];
  const applied = new Set(receipts.flatMap((r) => r.exceptions));
  return ledger
    .filter((entry) => (entry.observedBy?.length ?? 0) > 0 && !applied.has(entry.id))
    .map(
      (entry) => `divergences.json: ${entry.id}: no replay applied any of its observedBy entries`,
    );
}

/**
 * One problem line per receipt that does not vouch for a passing, full
 * strength run: failed cases, the member-order comparison off, no run id, or
 * a run id other than `runId` (when given) or than the other receipts'.
 */
export function runProblems(receipts: Receipt[], runId?: string): string[] {
  const problems: string[] = [];
  for (const r of receipts) {
    if (r.failures > 0) problems.push(`${r.file}: ${r.failures} cases failed in the replay`);
    if (!r.memberOrder) {
      problems.push(
        `${r.file}: replayed with the member-order comparison off (PLUGIN_CONFORMANCE_MEMBER_ORDER=0)`,
      );
    }
    if (r.allowListOverridden) {
      problems.push(
        `${r.file}: replayed with the allowlist replaced (PLUGIN_CONFORMANCE_MEMBER_ORDER_ALLOW)`,
      );
    }
    if (r.runId === null) problems.push(`${r.file}: receipt has no run id`);
    else if (runId !== undefined && r.runId !== runId) {
      problems.push(`${r.file}: receipt is from run ${r.runId}, not this run (${runId})`);
    }
  }
  const ids = [...new Set(receipts.flatMap((r) => (r.runId === null ? [] : [r.runId])))].sort();
  if (runId === undefined && ids.length > 1) {
    problems.push(`receipts come from ${ids.length} runs (${ids.join(', ')}), not one`);
  }
  return problems;
}

/** What the checkout holds now: its commit, whether it has uncommitted
 *  changes, the SHA-256 of each vector file's text (null when the file is
 *  gone), and whether the receipts come from a `--receipts` directory
 *  rather than this run. */
export interface Checkout {
  readonly head: string;
  readonly dirty: boolean;
  readonly vectorSha256: (file: string) => string | null;
  readonly saved: boolean;
}

/**
 * One problem line per receipt that does not match `checkout`: no commit or
 * a commit other than its head, no record of uncommitted changes or a record
 * that differs from the checkout's, uncommitted changes in a saved receipt,
 * no vector hash, or a vector hash other than the file's in the checkout. A
 * receipt like that replayed other vectors or other code than the checkout
 * holds. A saved receipt from a tree with uncommitted changes fails even
 * when the checkout has uncommitted changes too: nothing says they are the
 * same changes.
 */
export function staleProblems(receipts: Receipt[], checkout: Checkout): string[] {
  const problems: string[] = [];
  for (const r of receipts) {
    if (r.head === null) {
      problems.push(`${r.file}: receipt names no commit (${HEAD_VAR} was not set)`);
    } else if (r.head !== checkout.head) {
      problems.push(
        `${r.file}: receipt is from commit ${r.head}, the checkout is at ${checkout.head}`,
      );
    }
    if (r.dirty === null) {
      problems.push(
        `${r.file}: receipt does not say whether the tree had uncommitted changes (${DIRTY_VAR} was not set)`,
      );
    } else if (r.dirty !== checkout.dirty) {
      problems.push(
        r.dirty
          ? `${r.file}: receipt is from a tree with uncommitted changes, the checkout has none`
          : `${r.file}: receipt is from a clean tree, the checkout has uncommitted changes`,
      );
    } else if (r.dirty && checkout.saved) {
      problems.push(
        `${r.file}: receipt is from a tree with uncommitted changes, which a --receipts directory cannot tie to the checkout`,
      );
    }
    const current = checkout.vectorSha256(r.file);
    if (r.vectorSha256 === null) problems.push(`${r.file}: receipt has no vector hash`);
    else if (r.vectorSha256 !== current) {
      problems.push(`${r.file}: receipt replayed other vector text than the checkout holds`);
    }
  }
  return problems;
}

/** `member-order-allow.json`: `{ file: { case: [{ path, divergence }] } }`. */
export type AllowList = Record<string, Record<string, readonly { readonly path: string }[]>>;

/**
 * One problem line per allow-list path that no receipt of its file lists in
 * `memberOrderAllowed` for its case: an allowance that suppressed no
 * difference, so has no effect. Filtered receipts and receipts with the
 * comparison off already fail, so with any of them this reports nothing.
 */
export function unusedAllowances(allow: AllowList, receipts: Receipt[]): string[] {
  if (receipts.some((r) => r.only !== null || !r.memberOrder)) return [];
  const used = new Set(
    receipts.flatMap((r) =>
      r.memberOrderAllowed.map((u) => JSON.stringify([r.file, u.case, u.path])),
    ),
  );
  const problems: string[] = [];
  for (const [file, cases] of Object.entries(allow)) {
    for (const [name, entries] of Object.entries(cases)) {
      for (const { path } of entries) {
        if (!used.has(JSON.stringify([file, name, path]))) {
          problems.push(
            `member-order-allow.json: ${file} / ${name} / ${path}: no replay needed it (the member orders agree, or the case never ran)`,
          );
        }
      }
    }
  }
  return problems;
}

/**
 * One problem line per allowance a receipt used that `allow` does not hold
 * for its file and case: a run under a replaced or wider allowlist.
 */
export function unlistedAllowances(allow: AllowList, receipts: Receipt[]): string[] {
  const problems: string[] = [];
  for (const r of receipts) {
    for (const use of r.memberOrderAllowed) {
      if (!(allow[r.file]?.[use.case] ?? []).some((entry) => entry.path === use.path)) {
        problems.push(
          `${r.file}: case "${use.case}" skipped the member order at ${use.path}, which member-order-allow.json does not allow`,
        );
      }
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
  for (const key of ['runId', 'head', 'vectorSha256']) {
    if (r[key] !== null && typeof r[key] !== 'string') return `"${key}" is not a string or null`;
  }
  if (r.dirty !== null && typeof r.dirty !== 'boolean') return '"dirty" is not a boolean or null';
  if (typeof r.file !== 'string') return '"file" is not a string';
  if (r.only !== null && typeof r.only !== 'string') return '"only" is not a string or null';
  if (!isStrings(r.ran)) return '"ran" is not an array of strings';
  if (!Number.isInteger(r.failures) || (r.failures as number) < 0) {
    return '"failures" is not a count';
  }
  if (typeof r.memberOrder !== 'boolean') return '"memberOrder" is not a boolean';
  if (typeof r.allowListOverridden !== 'boolean') return '"allowListOverridden" is not a boolean';
  if (!isStrings(r.exceptions)) return '"exceptions" is not an array of strings';
  const isUse = (u: unknown): boolean =>
    u !== null &&
    typeof u === 'object' &&
    typeof (u as AllowanceUse).case === 'string' &&
    typeof (u as AllowanceUse).path === 'string';
  if (!Array.isArray(r.memberOrderAllowed) || !r.memberOrderAllowed.every(isUse)) {
    return '"memberOrderAllowed" is not an array of {case, path}';
  }
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
    readonly dependencies: readonly {
      readonly name: string;
      readonly kind: string | null;
      readonly optional?: boolean;
    }[];
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

const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const repoRoot = resolve(packageRoot, '..', '..');

/** The case names of every suite `src/cli/generate.ts` discovers, by vector file.
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
export function cargoMetadata(): CargoMetadata {
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

/** The environment cargo runs under: `base` with receipts written to
 *  `receipts` under `runId` and every variable that would weaken the proof
 *  removed. */
export function childEnv(
  base: NodeJS.ProcessEnv,
  receipts: string,
  runId: string,
  checkout: CheckoutState,
): NodeJS.ProcessEnv {
  const env: NodeJS.ProcessEnv = {
    ...base,
    PLUGIN_CONFORMANCE_RECEIPTS: receipts,
    PLUGIN_CONFORMANCE_RUN_ID: runId,
    [HEAD_VAR]: checkout.head,
    [DIRTY_VAR]: checkout.dirty ? '1' : '0',
  };
  delete env.PLUGIN_CONFORMANCE_ONLY;
  // `0` turns the member-order comparison off.
  delete env.PLUGIN_CONFORMANCE_MEMBER_ORDER;
  // `member-order:allow`'s replacement allowlist: never part of the proof.
  delete env.PLUGIN_CONFORMANCE_MEMBER_ORDER_ALLOW;
  return env;
}

/** Run the tests of `packages` with receipts on; true when cargo passed. */
function runCargo(
  packages: readonly string[],
  receipts: string,
  runId: string,
  checkout: CheckoutState,
): boolean {
  const env = childEnv(process.env, receipts, runId, checkout);
  const args = ['test', ...packages.flatMap((p) => ['-p', p]), '--tests', '--no-fail-fast'];
  console.log(
    `running cargo test over ${packages.length} crates (${packages.join(', ')}) with receipts in ${receipts}, run ${runId}`,
  );
  const run = spawnSync('cargo', args, { cwd: repoRoot, env, stdio: 'inherit' });
  if (run.error) throw run.error;
  return run.status === 0;
}

/** The receipt variables naming the commit and whether the tree had
 *  uncommitted changes (`crates/plugin-conformance/src/receipt.rs`). */
export const HEAD_VAR = 'PLUGIN_CONFORMANCE_HEAD';
export const DIRTY_VAR = 'PLUGIN_CONFORMANCE_DIRTY';

/** The checkout's commit and whether it has uncommitted changes, untracked
 *  files included. */
export interface CheckoutState {
  readonly head: string;
  readonly dirty: boolean;
}

/** What one git command printed. */
export type Git = (args: readonly string[]) => {
  readonly status: number | null;
  readonly stdout: string;
  readonly stderr: string;
  readonly error?: Error;
};

const git: Git = (args) => spawnSync('git', args, { cwd: repoRoot, encoding: 'utf8' });

/** `git rev-parse HEAD` and `git status --porcelain`. Throws, with git's
 *  message, when either fails: without the commit the receipts cannot be
 *  tied to the checkout, so the check cannot pass. */
export function checkoutState(run: Git = git): CheckoutState {
  const output = (args: readonly string[]): string => {
    const result = run(args);
    if (result.error || result.status !== 0) {
      const why = result.error?.message ?? (result.stderr.trim() || `exit ${result.status}`);
      throw new Error(`git ${args.join(' ')} failed, so no receipt can be tied to a commit: ${why}`);
    }
    return result.stdout;
  };
  const head = output(['rev-parse', 'HEAD']).trim();
  const dirty = output(['status', '--porcelain']).trim() !== '';
  return { head, dirty };
}

/** The SHA-256 of each vector file's text, as `readVector` gives it. */
async function vectorHashes(files: Iterable<string>): Promise<Map<string, string | null>> {
  const out = new Map<string, string | null>();
  for (const file of files) {
    const text = await readVector(join(vectorsRoot, file));
    out.set(file, text === null ? null : createHash('sha256').update(text, 'utf8').digest('hex'));
  }
  return out;
}

/** The command line: `src/cli/replay-check.ts` calls it. */
export async function main(): Promise<void> {
  const argv = process.argv.slice(2);
  rejectUnknownArgs(argv, [], ['--receipts']);
  const at = argv.indexOf('--receipts');
  const given = at >= 0 ? argv[at + 1] : undefined;
  if (at >= 0 && !given) throw new Error('--receipts needs a directory');

  const expected = await expectedCases();
  const dir = given
    ? resolve(given)
    : await mkdtemp(join(tmpdir(), 'plugin-conformance-receipts-'));
  const runId = given ? undefined : randomUUID();
  const checkout = checkoutState();
  let cargoPassed: boolean | null = null;
  let read: { receipts: Receipt[]; problems: string[] };
  try {
    if (runId) cargoPassed = runCargo(replayPackages(cargoMetadata()), dir, runId, checkout);
    read = await readReceipts(dir);
  } finally {
    if (!given) await rm(dir, { recursive: true, force: true });
  }

  const { receipts } = read;
  const ledger = JSON.parse(
    await readFile(join(repoRoot, 'crates', 'plugin-conformance', 'divergences.json'), 'utf8'),
  ) as LedgerEntry[];
  const allow = JSON.parse(
    await readFile(
      join(repoRoot, 'crates', 'plugin-conformance', 'member-order-allow.json'),
      'utf8',
    ),
  ) as AllowList;
  const hashes = await vectorHashes(new Set(receipts.map((r) => r.file)));
  const problems = [
    ...read.problems,
    ...runProblems(receipts, runId),
    ...staleProblems(receipts, {
      ...checkout,
      vectorSha256: (file) => hashes.get(file) ?? null,
      saved: given !== undefined,
    }),
    ...unlistedAllowances(allow, receipts),
    ...compareReceipts(expected, receipts),
    ...unappliedObservations(ledger, receipts),
    ...unusedAllowances(allow, receipts),
  ];
  const cases = [...expected.values()].reduce((n, names) => n + names.length, 0);
  console.log('');
  if (cargoPassed === null) console.log(`cargo: not run (read receipts from ${dir})`);
  else console.log(`cargo: ${cargoPassed ? 'passed' : 'FAILED (see the test output above)'}`);
  console.log(
    `checkout: ${checkout.head}${checkout.dirty ? ' with uncommitted changes' : ', clean'}`,
  );
  console.log(`receipts: ${receipts.length} read`);
  if (problems.length) {
    console.error(`${problems.length} replay problems:`);
    for (const line of problems) console.error(`  ${line}`);
  } else {
    console.log(`${expected.size} vector files, ${cases} cases, all replayed`);
  }
  if (problems.length || cargoPassed === false) process.exitCode = 1;
}
