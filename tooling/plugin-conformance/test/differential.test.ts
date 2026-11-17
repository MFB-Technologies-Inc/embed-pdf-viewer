import assert from 'node:assert/strict';
import { test } from 'node:test';

import fc from 'fast-check';

// Before any check module: see `src/library-guard.ts`.
import '../src/as-library';

import { diff } from '../src/canonical';
import {
  crates,
  readLedger,
  runDifferential,
  type CrateTable,
  type FunctionServer,
  type LedgerEntry,
} from '../src/checks/differential';
import { table as geometry } from '../src/differential/geometry';
import { adjacent, fromBits, platformMath, toBits } from '../src/differential/platform-math';
import { callFunctionCase, decodePlain } from '../src/vectors';

type MathName = 'cos' | 'sin' | 'atan2';
const math = Math as unknown as Record<MathName, (...args: number[]) => number>;

/** A server that answers with the TypeScript's own outcome, passed through
 *  `edit` first, and answers a math probe with `probe` (by default the
 *  JavaScript runtime's own answer). */
function stubServer(
  edit: (fn: string, args: unknown[], outcome: unknown) => unknown,
  probe: (name: string, args: readonly number[]) => number = (name, args) =>
    math[name as MathName](...args),
): FunctionServer & { probed: string[] } {
  const functions = Object.assign({}, ...geometry.suites.map((suite) => suite.functions));
  const probed: string[] = [];
  return {
    probed,
    async call(_table, fn, args) {
      const { outcome } = callFunctionCase(functions[fn], args, `[stub] ${fn}`);
      return edit(fn, (args as unknown[]).map(decodePlain), outcome);
    },
    async math(name, args) {
      probed.push(`${name}(${args.join(', ')})`);
      return probe(name, args);
    },
    async close() {},
  };
}

/** Rust's `rectFromCorners` got wrong in one place: a NaN first-corner x
 *  comes back as 0 where the TypeScript keeps NaN. */
const nanXBecomesZero = stubServer((fn, args, outcome) => {
  const first = args[0] as { x: number };
  if (fn !== 'rectFromCorners' || !Number.isNaN(first.x)) return outcome;
  const { returned } = outcome as { returned: Record<string, unknown> };
  return { returned: { ...returned, x: 0 } };
});

/** Every number in a canonical value, `0` turned to `-0`. */
function negateZeros(value: unknown): unknown {
  if (value === 0) return -0;
  if (Array.isArray(value)) return value.map(negateZeros);
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, negateZeros(v)]));
  }
  return value;
}

const SEED = 7;

/** The geometry table without its ledger skips, for runs with no ledger. */
const bare = { ...geometry, skips: {} };

test('differential reports a planted mismatch', async () => {
  const report = await runDifferential({
    table: bare,
    server: nanXBecomesZero,
    runs: 1000,
    seed: SEED,
    ledger: [],
    only: ['rectFromCorners'],
  });
  assert.equal(report.mismatches.length, 1);
  const [mismatch] = report.mismatches;
  assert.equal(mismatch.fn, 'rectFromCorners');
  assert.equal(mismatch.file, 'core/geometry-basics.json');
  // Shrunk: everything but the NaN that matters is gone to 0.
  assert.deepEqual(mismatch.args, [{ x: NaN, y: 0 }, { x: 0, y: 0 }]);
  assert.equal(
    mismatch.printed,
    `{ name: 'diff-rectFromCorners-${SEED}', fn: 'rectFromCorners', args: [{ x: NaN, y: 0 }, { x: 0, y: 0 }] },`,
  );
  assert.match(mismatch.difference, /^\$\.returned\.x: expected \{"\$number":"NaN"\}, got 0$/);
});

// The vectors print -0 as 0 (canonical.ts) and replay
// compares 0 and -0 equal (canonical.rs numbers_equal), so a Rust -0.0
// where the TypeScript has 0 is no mismatch here either.
test('differential compares -0 and 0 as the vectors do', async () => {
  assert.equal(diff({ returned: [0, 1] }, { returned: [-0, 1] }), null);
  const report = await runDifferential({
    table: bare,
    server: stubServer((_fn, _args, outcome) => negateZeros(outcome)),
    runs: 50,
    seed: SEED,
    ledger: [],
  });
  assert.deepEqual(report.mismatches, []);
  assert.deepEqual(report.untested, []);
});

test('differential compares numbers, arrays and member order as replay does', () => {
  assert.equal(diff(0.1 + 0.2, 0.3), null, 'within 1e-12');
  assert.match(diff(1, 2) ?? '', /expected 1, got 2/);
  assert.match(diff(2 ** 52, 2 ** 52 + 1) ?? '', /expected/, 'safe integers compare exactly');
  assert.equal(diff({ $number: 'NaN' }, { $number: 'NaN' }), null);
  assert.match(diff([1], [1, 2]) ?? '', /expected 1 items, got 2/);
  assert.match(diff({ a: 1, b: 2 }, { b: 2, a: 1 }) ?? '', /member order/);
  assert.equal(diff({ a: 1, b: 2 }, { b: 2, a: 1 }, false), null);
  assert.match(diff({ a: 1 }, { a: 1, b: 2 }) ?? '', /\$\.b: unexpected 2/);
});

const SKIP_LEDGER: LedgerEntry[] = [
  {
    id: 'X-NAN-CORNER',
    differentialSkip: [{ fn: 'rectFromCorners', when: 'the first corner x is NaN' }],
  },
];

test('a skip from the ledger suppresses a known divergence', async () => {
  const report = await runDifferential({
    table: {
      ...geometry,
      skips: { 'X-NAN-CORNER': ({ args }) => Number.isNaN((args[0] as { x: number }).x) },
    },
    server: nanXBecomesZero,
    runs: 1000,
    seed: SEED,
    ledger: SKIP_LEDGER,
    only: ['rectFromCorners'],
  });
  assert.deepEqual(report.mismatches, []);
  assert.ok((report.skipped.rectFromCorners ?? 0) > 0, 'the skip passed at least one input');
});

test('a ledger skip needs its predicate, and a predicate its ledger skip', async () => {
  const run = (skips: typeof geometry.skips, ledger: LedgerEntry[]) =>
    runDifferential({
      table: { ...geometry, skips },
      server: nanXBecomesZero,
      runs: 1,
      seed: SEED,
      ledger,
      only: ['rectFromCorners'],
    });
  await assert.rejects(run({}, SKIP_LEDGER), /X-NAN-CORNER: differentialSkip for rectFromCorners has no predicate/);
  await assert.rejects(run({ 'X-OTHER': () => true }, []), /skip for X-OTHER/);
});

test('a --fn name no suite calls fails instead of running nothing', async () => {
  await assert.rejects(
    runDifferential({
      table: bare,
      server: stubServer((_fn, _args, outcome) => outcome),
      runs: 1,
      seed: SEED,
      ledger: [],
      only: ['rectFromCorners', 'noSuchFunction'],
    }),
    /--fn noSuchFunction: no suite of geometry calls it/,
  );
});

test('a --fn name with no arbitrary fails instead of running nothing', async () => {
  const { rectFromCorners: _, ...arbitraries } = geometry.arbitraries;
  await assert.rejects(
    runDifferential({
      table: { ...bare, arbitraries },
      server: stubServer((_fn, _args, outcome) => outcome),
      runs: 1,
      seed: SEED,
      ledger: [],
      only: ['rectFromCorners'],
    }),
    /--fn rectFromCorners: no arbitrary in src\/differential\/geometry\.ts/,
  );
});

// The platform math entries: Rust's cos, sin and atan2 may answer one ulp
// off the JavaScript runtime's, and only as the platform library does.

/** `x` moved `steps` doubles. */
function moved(x: number, steps: number): number {
  for (let i = 0; i < Math.abs(steps); i++) x = adjacent(x, steps > 0 ? 1 : -1);
  return x;
}

/** `run` with each `Math` function in `moves` answering that many doubles
 *  off the runtime. */
function withMoved<T>(moves: Partial<Record<MathName, number>>, run: () => T): T {
  const names = Object.keys(moves) as MathName[];
  const saved = names.map((name) => math[name]);
  names.forEach((name, i) => {
    math[name] = (...a: number[]) => moved(saved[i](...a), moves[name]!);
  });
  try {
    return run();
  } finally {
    names.forEach((name, i) => {
      math[name] = saved[i];
    });
  }
}

/** A platform whose library answers `moves` off the runtime: the functions
 *  in `fns` return the TypeScript's outcome with `Math` moved so, and a
 *  probe answers moved by `probes` (by default the same `moves`, as a real
 *  server does). */
function platformServer(
  fns: readonly string[],
  moves: Partial<Record<MathName, number>>,
  probes: Partial<Record<MathName, number>> = moves,
) {
  const functions = Object.assign({}, ...geometry.suites.map((suite) => suite.functions));
  return stubServer(
    (fn, args, outcome) => {
      if (!fns.includes(fn)) return outcome;
      return withMoved(moves, () => callFunctionCase(functions[fn], args, `[stub] ${fn}`).outcome);
    },
    (name, args) => moved(math[name as MathName](...args), probes[name as MathName] ?? 0),
  );
}

/** `rotateAbout` about a pivot far enough out that its translation is a
 *  safe integer, so one ulp of cos shows. */
const farPivot = {
  ...geometry,
  arbitraries: {
    ...geometry.arbitraries,
    rotateAbout: fc.constant<unknown[]>([{ x: 0, y: -4915517297019373 }, -6]),
  },
};

test('the ledger allows a one-ulp cos in rotateAbout that the platform library gives, and counts it', async () => {
  const server = platformServer(['rotateAbout'], { cos: -1 });
  const report = await runDifferential({
    table: farPivot,
    server,
    runs: 5,
    seed: SEED,
    ledger: readLedger(),
    only: ['rotateAbout'],
  });
  assert.deepEqual(report.mismatches, []);
  assert.deepEqual(report.skipped, { rotateAbout: 5 });
  assert.deepEqual(report.skippedBy, { 'PLATFORM-MATH-COS-SIN': 5 });
  // One probe per distinct call per input, from the server under comparison.
  assert.deepEqual([...new Set(server.probed)], ['cos(-6)', 'sin(-6)']);
  assert.equal(math.cos, Object.getOwnPropertyDescriptor(Math, 'cos')!.value);
  assert.equal(Math.cos(0), 1, "Math.cos is the runtime's again");
});

test('a one-ulp cos the platform library does not give is a mismatch', async () => {
  // Rust's matrix is the TypeScript's with cos one ulp lower, which some
  // one-ulp answer reaches, but the library answers one ulp higher.
  const report = await runDifferential({
    table: farPivot,
    server: platformServer(['rotateAbout'], { cos: -1 }, { cos: 1 }),
    runs: 5,
    seed: SEED,
    ledger: readLedger(),
    only: ['rotateAbout'],
  });
  assert.equal(report.mismatches.length, 1);
  assert.deepEqual(report.skippedBy, {});
  assert.match(
    report.mismatches[0].refusals.join('\n'),
    /^PLATFORM-MATH-COS-SIN: Rust's outcome is not the TypeScript's with the platform library's answers: \$\.returned\[5\]/,
  );
});

test('a platform answer two ulps from the runtime is a mismatch, and says which call', async () => {
  const report = await runDifferential({
    table: farPivot,
    server: platformServer(['rotateAbout'], { cos: -2 }),
    runs: 5,
    seed: SEED,
    ledger: readLedger(),
    only: ['rotateAbout'],
  });
  assert.equal(report.mismatches.length, 1);
  assert.deepEqual(report.mismatches[0].refusals, [
    `PLATFORM-MATH-COS-SIN: cos(-6): the platform library answers ${moved(Math.cos(-6), -2)}, ` +
      `the runtime ${Math.cos(-6)}, more than one ulp apart`,
  ]);
});

// Two ulps print the same matrix as one here, so three.
test('a three-ulp cos in rotateAbout is still a mismatch', async () => {
  const report = await runDifferential({
    table: farPivot,
    server: platformServer(['rotateAbout'], { cos: -3 }),
    runs: 5,
    seed: SEED,
    ledger: readLedger(),
    only: ['rotateAbout'],
  });
  assert.equal(report.mismatches.length, 1);
  assert.match(
    report.mismatches[0].difference,
    /^\$\.returned\[5\]: expected -195783644905449, got -195783644905451/,
  );
});

test('a server that cannot answer a probe allows nothing', async () => {
  const failing = platformServer(['rotateAbout'], { cos: -1 });
  failing.math = async () => {
    throw new Error('math cos: no math function "cos"');
  };
  const report = await runDifferential({
    table: farPivot,
    server: failing,
    runs: 5,
    seed: SEED,
    ledger: readLedger(),
    only: ['rotateAbout'],
  });
  assert.equal(report.mismatches.length, 1);
  assert.deepEqual(report.mismatches[0].refusals, [
    'PLATFORM-MATH-COS-SIN: cos(-6): function-server: math cos: no math function "cos"',
  ]);
});

test('a call that appears only with the platform answers is asked for too', async () => {
  // Takes the second branch only when cos answers below the runtime, so the
  // sin call shows only in the rerun with the platform's cos. The flag is an
  // integer, so the comparison sees which side of the runtime sin landed.
  const X = 2;
  const runtimeCos = Math.cos(X);
  const runtimeSin = Math.sin(X);
  const branchy = (x: number) => {
    const c = Math.cos(x);
    return c < runtimeCos ? [c, Math.sin(x) > runtimeSin ? 1 : 0] : [c];
  };
  const table: CrateTable = {
    table: 'test',
    suites: [
      { kind: 'function', file: 'test.json', describes: 'test', functions: { branchy }, cases: [] },
    ],
    arbitraries: { branchy: fc.constant<unknown[]>([X]) },
    skips: { 'T-MATH': (d) => platformMath(['cos', 'sin'], d) },
  };
  const ledger: LedgerEntry[] = [
    { id: 'T-MATH', differentialSkip: [{ fn: 'branchy', when: 'test' }] },
  ];
  const rustOutcome = { returned: [adjacent(runtimeCos, -1), 1] };
  const server = (
    probe: (name: string, x: number) => number,
  ): FunctionServer & { probed: string[] } => ({
    ...stubServer(
      () => rustOutcome,
      (name, args) => probe(name, args[0]),
    ),
    async call() {
      return rustOutcome;
    },
  });
  const platform = server((name, x) =>
    adjacent(math[name as MathName](x), name === 'cos' ? -1 : 1),
  );
  const allowed = await runDifferential({ table, server: platform, runs: 3, seed: SEED, ledger });
  assert.deepEqual(allowed.mismatches, []);
  assert.deepEqual(allowed.skippedBy, { 'T-MATH': 3 });
  assert.deepEqual(platform.probed.slice(0, 2), ['cos(2)', 'sin(2)']);

  // The same Rust outcome where the library's sin is the runtime's: the
  // rerun gives the runtime's sin, so it is refused.
  const refused = await runDifferential({
    table,
    server: server((name, x) => (name === 'cos' ? adjacent(Math.cos(x), -1) : Math.sin(x))),
    runs: 3,
    seed: SEED,
    ledger,
  });
  assert.equal(refused.mismatches.length, 1);
  assert.match(
    refused.mismatches[0].refusals[0],
    /^T-MATH: Rust's outcome is not the TypeScript's/,
  );
});

test('a one-ulp cos is a mismatch in a function the ledger entry does not name', async () => {
  const ledger = readLedger().map((entry) =>
    entry.id === 'PLATFORM-MATH-COS-SIN'
      ? {
          ...entry,
          differentialSkip: entry.differentialSkip?.filter((s) => s.fn !== 'rotateAbout'),
        }
      : entry,
  );
  const report = await runDifferential({
    table: farPivot,
    server: platformServer(['rotateAbout'], { cos: -1 }),
    runs: 5,
    seed: SEED,
    ledger,
    only: ['rotateAbout'],
  });
  assert.equal(report.mismatches.length, 1);
  assert.deepEqual(report.skippedBy, {});
});

test('a one-ulp move in a function that calls no Math is a mismatch even where the ledger names it', async () => {
  // translate(tx, ty) returns its arguments: no Math call to move.
  const ledger = readLedger().map((entry) =>
    entry.id === 'PLATFORM-MATH-COS-SIN'
      ? {
          ...entry,
          differentialSkip: [...(entry.differentialSkip ?? []), { fn: 'translate', when: 'test' }],
        }
      : entry,
  );
  const report = await runDifferential({
    table: {
      ...geometry,
      arbitraries: { ...geometry.arbitraries, translate: fc.constant<unknown[]>([1, 2 ** 52 + 1]) },
    },
    server: stubServer((fn, _args, outcome) => {
      if (fn !== 'translate') return outcome;
      const { returned } = outcome as { returned: number[] };
      return { returned: returned.map((v, i) => (i === 5 ? adjacent(v, 1) : v)) };
    }),
    runs: 5,
    seed: SEED,
    ledger,
    only: ['translate'],
  });
  assert.equal(report.mismatches.length, 1);
});

test('the ledger allows a degenerate quad labelled as the platform atan2 gives, and no other labelling', async () => {
  const degenerate = fc.constant<unknown[]>([
    [
      { x: 0, y: -1 },
      { x: -1.4456042752816933e-19, y: 0 },
      { x: 0, y: 0 },
      { x: 0, y: 5e-324 },
    ],
  ]);
  const table = {
    ...geometry,
    arbitraries: { ...geometry.arbitraries, normalizePdfQuad: degenerate },
  };
  // What the dev container's atan2 gives: the second corner's angle one ulp
  // lower, tied with the corners on the y axis.
  const tiedLabelling = (fn: string, _args: unknown[], outcome: unknown) => {
    if (fn !== 'normalizePdfQuad') return outcome;
    const { returned: q } = outcome as { returned: Record<string, unknown> };
    return { returned: { ...q, lowerLeft: q.lowerRight, lowerRight: q.lowerLeft } };
  };
  const SECOND = 1.5707963267948968;
  const containerAtan2 = (name: string, args: readonly number[]) => {
    const runtime = math[name as MathName](...args);
    return runtime === SECOND ? adjacent(runtime, -1) : runtime;
  };
  const allowed = await runDifferential({
    table,
    server: stubServer(tiedLabelling, containerAtan2),
    runs: 5,
    seed: SEED,
    ledger: readLedger(),
    only: ['normalizePdfQuad'],
  });
  assert.deepEqual(allowed.mismatches, []);
  assert.deepEqual(allowed.skippedBy, { 'PLATFORM-MATH-ATAN2': 5 });

  // The same labelling from a library that answers as the runtime does: a
  // one-ulp atan2 would reach it, but not this library's, so it is a real
  // difference in the labelling (the shape of a reordered sort).
  const runtimeLibrary = await runDifferential({
    table,
    server: stubServer(tiedLabelling),
    runs: 5,
    seed: SEED,
    ledger: readLedger(),
    only: ['normalizePdfQuad'],
  });
  assert.equal(runtimeLibrary.mismatches.length, 1);
  assert.match(
    runtimeLibrary.mismatches[0].refusals[0],
    /^PLATFORM-MATH-ATAN2: Rust's outcome is not/,
  );

  // The left corners swapped: the same four points, a labelling no one-ulp
  // atan2 reaches. (The upper corners, (0, 0) and (0, 5e-324), compare
  // equal within the tolerance, so swapping those would show nothing.)
  const swapped = await runDifferential({
    table,
    server: stubServer((fn, _args, outcome) => {
      if (fn !== 'normalizePdfQuad') return outcome;
      const { returned: q } = outcome as { returned: Record<string, unknown> };
      return { returned: { ...q, upperLeft: q.lowerLeft, lowerLeft: q.upperLeft } };
    }, containerAtan2),
    runs: 5,
    seed: SEED,
    ledger: readLedger(),
    only: ['normalizePdfQuad'],
  });
  assert.equal(swapped.mismatches.length, 1);
});

test('a ledger skip for an id divergences.json does not hold fails the run', async () => {
  const ledger = readLedger().filter((entry) => entry.id !== 'PLATFORM-MATH-ATAN2');
  await assert.rejects(
    runDifferential({
      table: geometry,
      server: stubServer((_fn, _args, o) => o),
      runs: 1,
      seed: SEED,
      ledger,
    }),
    /has a skip for PLATFORM-MATH-ATAN2, which no divergences\.json differentialSkip on this table names/,
  );
});

test('a ledger skip naming a function no crate holds fails the run', async () => {
  const run = (fn: string) =>
    runDifferential({
      table: bare,
      server: stubServer((_fn, _args, o) => o),
      runs: 1,
      seed: SEED,
      ledger: [{ id: 'X-NOWHERE', differentialSkip: [{ fn, when: 'test' }] }],
      only: ['identity'],
    });
  await assert.rejects(
    run('noSuchFunction'),
    /X-NOWHERE: differentialSkip names noSuchFunction, which no suite of geometry holds/,
  );
  // The math probes are no table's functions, so no skip can name them.
  await assert.rejects(run('cos'), /X-NOWHERE: differentialSkip names cos/);
});

test("a ledger skip for a function of another crate is not this run's, and no error", async () => {
  const other: CrateTable = {
    table: 'other',
    suites: [
      {
        kind: 'function',
        file: 'other.json',
        describes: 'test',
        functions: { otherFn: () => 0 },
        cases: [],
      },
    ],
    arbitraries: {},
    skips: {},
  };
  crates.other = async () => other;
  try {
    const report = await runDifferential({
      table: bare,
      server: stubServer((_fn, _args, o) => o),
      runs: 1,
      seed: SEED,
      ledger: [{ id: 'X-OTHER-CRATE', differentialSkip: [{ fn: 'otherFn', when: 'test' }] }],
      only: ['identity'],
    });
    assert.deepEqual(report.mismatches, []);
  } finally {
    delete crates.other;
  }
});

test('a math probe crosses as the bits of the double', () => {
  // The same strings crates/conformance-function-server/tests/server.rs
  // expects from Rust.
  assert.equal(toBits(0.960170286650366), '3feeb9b7097822f5');
  assert.equal(toBits(1.5707963267948966), '3ff921fb54442d18');
  assert.equal(toBits(-0), '8000000000000000');
  for (const x of [
    0.960170286650366,
    1.5707963267948966,
    -0,
    0,
    5e-324,
    -1e-310,
    NaN,
    Infinity,
    -Infinity,
  ]) {
    assert.ok(Object.is(fromBits(toBits(x)), x), String(x));
  }
  assert.throws(() => fromBits('3ff'), /not the bits of a double/);
});
