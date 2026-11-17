/**
 * Differential runner for pure function tables: generated inputs, called on
 * the shipping TypeScript and on the Rust port, compared the way replay
 * compares a vector.
 *
 *   pnpm run differential --crate geometry --runs 1000 --seed 42 [--fn name]...
 *
 * For each function of the crate's suites that `src/differential/<crate>.ts`
 * gives an arbitrary, fast-check generates arguments. The TypeScript runs
 * through `callFunctionCase`, the generator's own path, so arguments are
 * encoded canonically first and the outcome is `{returned}` or `{threw}`
 * exactly as a vector records it. The same canonical arguments go to
 * `function-server` (`crates/conformance-function-server`), which answers
 * through `plugin_conformance::call_function`, the path replay takes. The two
 * outcomes compare with `diff` from `canonical.ts`, the port of
 * `canonical.rs`, so `-0` and `0` are one value and NaN equals NaN, as in
 * the frozen cases.
 *
 * On a mismatch fast-check shrinks the input, and the tool prints a function
 * case, `{ name: 'diff-<fn>-<seed>', fn, args }`, to paste into the suite;
 * the generator then freezes it as a vector. It exits 1 when any function
 * mismatched.
 *
 * A kept divergence is allowed by a `differentialSkip` on its
 * `divergences.json` entry, `{ "fn", "when" }`, with the predicate in
 * `src/differential/<crate>.ts` under the entry's id. The predicate sees the
 * input and both outcomes, so it can accept the difference the entry
 * describes and refuse any other; it may ask `function-server` what the
 * platform math library answers (`Difference.math`). A skip without a
 * predicate, a predicate without a skip, or a skip naming a function no
 * crate's suites hold fails the run.
 */
import { spawn, spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';

import fc from 'fast-check';

import { diff } from '../canonical';
import { refuseDirectRun } from '../library-guard';
import { fromBits, toBits } from '../differential/platform-math';
import { callFunctionCase, decodePlain, type FunctionSuite } from '../vectors';

refuseDirectRun('src/checks/differential.ts', 'differential');

const repoRoot = fileURLToPath(new URL('../../../..', import.meta.url));
const ledgerFile = resolve(repoRoot, 'crates/plugin-conformance/divergences.json');

/** A process that answers `{table, fn, args}` with an outcome. */
export interface FunctionServer {
  call(table: string, fn: string, args: readonly unknown[]): Promise<unknown>;
  /** What the platform math library answers to `cos`, `sin` or `atan2` on
   *  `args`, to the bit; rejects when the server cannot say. */
  math(name: string, args: readonly number[]): Promise<number>;
  close(): Promise<void>;
}

/** An input on which the two sides differed. */
export interface Difference {
  readonly fn: string;
  /** The decoded arguments both sides received. */
  readonly args: readonly unknown[];
  /** Both outcomes, as a vector records them. */
  readonly typescript: unknown;
  readonly rust: unknown;
  /** Calls the TypeScript again on the same input and returns its outcome. */
  readonly rerun: () => unknown;
  /** `FunctionServer.math` on the server that gave `rust`. */
  readonly math: (name: string, args: readonly number[]) => Promise<number>;
}

/** `true` when the difference is the one a ledger entry describes; a
 *  refusal may say why it is not, for the mismatch report. */
export type SkipVerdict = boolean | { readonly refused: string };

/** Whether a difference is the one a ledger entry describes. */
export type SkipPredicate = (difference: Difference) => SkipVerdict | Promise<SkipVerdict>;

/** What `src/differential/<crate>.ts` exports as `table`. */
export interface CrateTable {
  /** The table's name in `function-server`. */
  readonly table: string;
  readonly suites: readonly FunctionSuite[];
  /** Arguments per function, by the name the suites call it. */
  readonly arbitraries: Readonly<Record<string, fc.Arbitrary<unknown[]>>>;
  /** Predicates by `divergences.json` id. */
  readonly skips: Readonly<Record<string, SkipPredicate>>;
}

/** The part of a `divergences.json` entry this tool reads. */
export interface LedgerEntry {
  readonly id: string;
  readonly differentialSkip?: readonly { readonly fn: string; readonly when: string }[];
}

export const crates: Record<string, () => Promise<CrateTable>> = {
  geometry: async () => (await import('../differential/geometry')).table,
};

export interface Mismatch {
  readonly fn: string;
  /** The vector file whose suite holds `fn`. */
  readonly file: string;
  /** The shrunk arguments, as both sides received them. */
  readonly args: unknown[];
  readonly difference: string;
  /** Why each ledger skip on `fn` refused the shrunk input, by entry id. */
  readonly refusals: string[];
  readonly typescript: unknown;
  readonly rust: unknown;
  /** The function case to paste into the suite. */
  readonly printed: string;
}

export interface Report {
  /** Inputs run per function, by fast-check's count. */
  readonly runs: Record<string, number>;
  /** Inputs a ledger skip passed, per function. */
  readonly skipped: Record<string, number>;
  /** Inputs a ledger skip passed, per `divergences.json` id. */
  readonly skippedBy: Record<string, number>;
  readonly mismatches: Mismatch[];
  /** Functions of the suites with no arbitrary, so never run. */
  readonly untested: string[];
}

export interface Options {
  readonly table: CrateTable;
  readonly server: FunctionServer;
  readonly runs: number;
  readonly seed: number;
  readonly ledger: readonly LedgerEntry[];
  /** Only these functions; a name the table's suites do not hold, or one
   *  with no arbitrary, throws. */
  readonly only?: readonly string[];
}

export function readLedger(file = ledgerFile): LedgerEntry[] {
  return JSON.parse(readFileSync(file, 'utf8')) as LedgerEntry[];
}

/** Every function of the table's suites, first suite first, once each. */
function functionsOf(table: CrateTable): Map<string, { fn: (...args: never[]) => unknown; file: string }> {
  const out = new Map<string, { fn: (...args: never[]) => unknown; file: string }>();
  for (const suite of table.suites) {
    for (const [name, fn] of Object.entries(suite.functions)) {
      if (!out.has(name)) out.set(name, { fn, file: suite.file });
    }
  }
  return out;
}

/** Every function the suites of every crate in `crates` hold, and `table`'s. */
async function everyFunction(table: CrateTable): Promise<Set<string>> {
  const names = new Set(functionsOf(table).keys());
  for (const load of Object.values(crates)) {
    for (const name of functionsOf(await load()).keys()) names.add(name);
  }
  return names;
}

/**
 * The ledger's skips for this table's functions, each with its predicate. A
 * skip for a function of another crate's table is that run's business; one
 * naming a function no table in `crates` holds (`known`) can never apply,
 * so it fails instead of passing unseen.
 */
export function resolveSkips(
  table: CrateTable,
  ledger: readonly LedgerEntry[],
  known: ReadonlySet<string>,
): Map<string, { id: string; predicate: SkipPredicate }[]> {
  const names = functionsOf(table);
  const out = new Map<string, { id: string; predicate: SkipPredicate }[]>();
  for (const entry of ledger) {
    for (const skip of entry.differentialSkip ?? []) {
      if (!known.has(skip.fn)) {
        throw new Error(
          `${entry.id}: differentialSkip names ${skip.fn}, which no suite of ${Object.keys(crates).join(', ')} holds`,
        );
      }
      if (!names.has(skip.fn)) continue;
      const predicate = table.skips[entry.id];
      if (!predicate) {
        throw new Error(
          `${entry.id}: differentialSkip for ${skip.fn} has no predicate in src/differential/${table.table}.ts`,
        );
      }
      out.set(skip.fn, [...(out.get(skip.fn) ?? []), { id: entry.id, predicate }]);
    }
  }
  for (const id of Object.keys(table.skips)) {
    const entry = ledger.find((e) => e.id === id);
    if (!entry?.differentialSkip?.some((skip) => names.has(skip.fn))) {
      throw new Error(
        `src/differential/${table.table}.ts has a skip for ${id}, which no divergences.json differentialSkip on this table names`,
      );
    }
  }
  return out;
}

/** JavaScript source for a decoded argument value. */
export function literal(value: unknown): string {
  if (value === undefined) return 'undefined';
  if (value === null) return 'null';
  switch (typeof value) {
    case 'number':
      if (Object.is(value, -0)) return '-0';
      return String(value);
    case 'boolean':
      return String(value);
    case 'string':
      return `'${value.replace(/\\/g, '\\\\').replace(/'/g, "\\'").replace(/\n/g, '\\n')}'`;
  }
  if (Array.isArray(value)) return `[${value.map(literal).join(', ')}]`;
  if (Object.getPrototypeOf(value) === Object.prototype) {
    const members = Object.entries(value as Record<string, unknown>).map(
      ([key, member]) =>
        `${/^[A-Za-z_$][\w$]*$/.test(key) ? key : literal(key)}: ${literal(member)}`,
    );
    return members.length ? `{ ${members.join(', ')} }` : '{}';
  }
  throw new Error(`[differential] no literal for ${Object.prototype.toString.call(value)}`);
}

export async function runDifferential(options: Options): Promise<Report> {
  const { table, server, runs, seed } = options;
  const skips = resolveSkips(table, options.ledger, await everyFunction(table));
  const functions = functionsOf(table);
  const unknown = (options.only ?? []).filter((name) => !functions.has(name));
  if (unknown.length) {
    throw new Error(
      `--fn ${unknown.join(', ')}: no suite of ${table.table} calls it; the functions are ${[...functions.keys()].join(', ')}`,
    );
  }
  const untestable = (options.only ?? []).filter((name) => !table.arbitraries[name]);
  if (untestable.length) {
    throw new Error(
      `--fn ${untestable.join(', ')}: no arbitrary in src/differential/${table.table}.ts, so nothing would run`,
    );
  }
  const report: Report = { runs: {}, skipped: {}, skippedBy: {}, mismatches: [], untested: [] };
  for (const [name, { fn, file }] of functions) {
    if (options.only && !options.only.includes(name)) continue;
    const arbitrary = table.arbitraries[name];
    if (!arbitrary) {
      report.untested.push(name);
      continue;
    }
    const call = (raw: unknown[]) => callFunctionCase(fn, raw, `[differential] ${name}`);
    const compare = async (raw: unknown[]) => {
      const { args, outcome } = call(raw);
      const reply = await server.call(table.table, name, args);
      const failed =
        reply !== null && typeof reply === 'object' && 'error' in reply
          ? `function-server: ${String((reply as { error: unknown }).error)}`
          : diff(outcome, reply);
      return { args, outcome, reply, failed };
    };
    /** The first ledger skip that allows the difference, or each one's refusal. */
    const judge = async (raw: unknown[], args: unknown[], outcome: unknown, reply: unknown) => {
      const difference: Difference = {
        fn: name,
        args: args.map(decodePlain),
        typescript: outcome,
        rust: reply,
        rerun: () => call(raw).outcome,
        math: (mathName, mathArgs) => server.math(mathName, mathArgs),
      };
      const refusals: string[] = [];
      for (const skip of skips.get(name) ?? []) {
        const verdict = await skip.predicate(difference);
        if (verdict === true) return { allowedBy: skip.id, refusals };
        if (typeof verdict === 'object') refusals.push(`${skip.id}: ${verdict.refused}`);
      }
      return { allowedBy: null, refusals };
    };
    let shrinking = false;
    let skipped = 0;
    const skippedBy: Record<string, number> = {};
    const property = fc.asyncProperty(arbitrary, async (raw) => {
      const { args, outcome, reply, failed } = await compare(raw);
      if (failed === null) return true;
      const { allowedBy } = await judge(raw, args, outcome, reply);
      if (allowedBy !== null) {
        if (!shrinking) {
          skipped++;
          skippedBy[allowedBy] = (skippedBy[allowedBy] ?? 0) + 1;
        }
        return true;
      }
      shrinking = true;
      return false;
    });
    const result = await fc.check(property, { numRuns: runs, seed });
    report.runs[name] = result.numRuns;
    if (skipped) report.skipped[name] = skipped;
    for (const [id, count] of Object.entries(skippedBy)) {
      report.skippedBy[id] = (report.skippedBy[id] ?? 0) + count;
    }
    if (!result.failed || !result.counterexample) continue;
    // Called again on the shrunk input; a call that threw (the server died)
    // throws here.
    const [raw] = result.counterexample;
    const { args, outcome, reply, failed } = await compare(raw);
    const { refusals } =
      failed === null ? { refusals: [] } : await judge(raw, args, outcome, reply);
    const decoded = args.map(decodePlain);
    const printed = `{ name: ${literal(`diff-${name}-${seed}`)}, fn: ${literal(name)}, args: ${literal(decoded)} },`;
    report.mismatches.push({
      fn: name,
      file,
      args: decoded,
      difference: failed ?? 'differs only before shrinking',
      refusals,
      typescript: outcome,
      rust: reply,
      printed,
    });
  }
  return report;
}

/** `function-server` at `binary`, one request line per call. */
export function spawnFunctionServer(binary: string): FunctionServer {
  const child = spawn(binary, [], { stdio: ['pipe', 'pipe', 'inherit'] });
  const pending: { resolve: (value: unknown) => void; reject: (error: Error) => void }[] = [];
  let exited: Error | null = null;
  const lines = createInterface({ input: child.stdout });
  lines.on('line', (line) => {
    const next = pending.shift();
    if (!next) return;
    try {
      next.resolve(JSON.parse(line));
    } catch (error) {
      next.reject(error as Error);
    }
  });
  const fail = (error: Error) => {
    exited = error;
    for (const next of pending.splice(0)) next.reject(error);
  };
  child.on('error', fail);
  child.on('exit', (code, signal) => fail(new Error(`function-server exited (${signal ?? code})`)));
  const done = new Promise<void>((resolveDone) => child.on('close', () => resolveDone()));
  const request = (line: unknown): Promise<unknown> => {
    if (exited) return Promise.reject(exited);
    return new Promise((resolveCall, reject) => {
      pending.push({ resolve: resolveCall, reject });
      child.stdin.write(`${JSON.stringify(line)}\n`);
    });
  };
  return {
    call: (tableName, fn, args) => request({ table: tableName, fn, args }),
    async math(name, args) {
      const reply = (await request({ math: name, args: args.map(toBits) })) as {
        bits?: unknown;
        error?: unknown;
      };
      if (typeof reply?.bits === 'string') return fromBits(reply.bits);
      throw new Error(
        `math ${name}: ${reply?.error === undefined ? JSON.stringify(reply) : String(reply.error)}`,
      );
    },
    async close() {
      child.stdin.end();
      await done;
    },
  };
}

/** Build `function-server` and return its path. */
export function buildFunctionServer(): string {
  const run = spawnSync(
    'cargo',
    [
      'build',
      '-p',
      'conformance-function-server',
      '--bin',
      'function-server',
      '--message-format=json-render-diagnostics',
    ],
    { cwd: repoRoot, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024, stdio: ['ignore', 'pipe', 'inherit'] },
  );
  if (run.error) throw run.error;
  if (run.status !== 0) throw new Error(`cargo build exited ${run.status}`);
  for (const line of run.stdout.split('\n')) {
    if (!line.startsWith('{')) continue;
    const message = JSON.parse(line) as { reason?: string; target?: { name?: string }; executable?: string | null };
    if (message.reason === 'compiler-artifact' && message.target?.name === 'function-server' && message.executable) {
      return message.executable;
    }
  }
  throw new Error('cargo build named no function-server executable');
}

function parseArgs(argv: readonly string[]) {
  let crate = '';
  let runs = 1000;
  let seed: number | null = null;
  let server: string | null = null;
  const only: string[] = [];
  for (let i = 0; i < argv.length; i++) {
    const flag = argv[i];
    const value = argv[i + 1];
    const need = () => {
      if (value === undefined) throw new Error(`${flag} needs a value`);
      i++;
      return value;
    };
    // `pnpm run differential -- --crate ...` passes the `--` through.
    if (flag === '--') continue;
    if (flag === '--crate') crate = need();
    else if (flag === '--runs') runs = Number(need());
    else if (flag === '--seed') seed = Number(need());
    else if (flag === '--fn') only.push(...need().split(','));
    else if (flag === '--server') server = need();
    else throw new Error(`unknown argument ${flag}`);
  }
  if (!crates[crate]) throw new Error(`--crate is one of: ${Object.keys(crates).join(', ')}`);
  if (!Number.isInteger(runs) || runs < 1) throw new Error('--runs is a positive integer');
  if (seed !== null && !Number.isInteger(seed)) throw new Error('--seed is an integer');
  return { crate, runs, seed: seed ?? (Date.now() ^ (Math.random() * 0x100000000)) | 0, server, only };
}

/** The command line: `src/cli/differential.ts` calls it. */
export async function main() {
  const { crate, runs, seed, server: serverPath, only } = parseArgs(process.argv.slice(2));
  // Before the run, so a run that dies still names the seed that repeats it.
  console.log(`differential --crate ${crate} --runs ${runs} --seed ${seed}`);
  const table = await crates[crate]();
  const server = spawnFunctionServer(serverPath ?? buildFunctionServer());
  let report: Report;
  try {
    report = await runDifferential({
      table,
      server,
      runs,
      seed,
      ledger: readLedger(),
      only: only.length ? only : undefined,
    });
  } finally {
    await server.close();
  }
  for (const [name, count] of Object.entries(report.runs)) {
    const skipped = report.skipped[name] ? `, ${report.skipped[name]} allowed by the ledger` : '';
    const failed = report.mismatches.some((m) => m.fn === name) ? ' MISMATCH' : '';
    console.log(`  ${name}: ${count} inputs${skipped}${failed}`);
  }
  for (const name of report.untested) console.log(`  ${name}: no arbitrary, not run`);
  for (const mismatch of report.mismatches) {
    console.log(`\n${mismatch.fn} (${mismatch.file}): ${mismatch.difference}`);
    for (const refusal of mismatch.refusals) console.log(`  ledger refused: ${refusal}`);
    console.log(`  TypeScript: ${JSON.stringify(mismatch.typescript)}`);
    console.log(`  Rust:       ${JSON.stringify(mismatch.rust)}`);
    console.log(`  case for the suite:\n  ${mismatch.printed}`);
  }
  const total = Object.values(report.runs).reduce((a, b) => a + b, 0);
  const allowed = Object.values(report.skippedBy).reduce((a, b) => a + b, 0);
  const byId = Object.entries(report.skippedBy)
    .map(([id, count]) => `${id} ${count}`)
    .join(', ');
  console.log(
    `\n${Object.keys(report.runs).length} functions, ${total} inputs, ${report.mismatches.length} mismatched, ` +
      `${allowed} allowed by the ledger${byId ? ` (${byId})` : ''}`,
  );
  if (report.mismatches.length) process.exitCode = 1;
}
