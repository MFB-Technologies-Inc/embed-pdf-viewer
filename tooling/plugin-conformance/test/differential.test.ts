import assert from 'node:assert/strict';
import { test } from 'node:test';

import fc from 'fast-check';

// Before any check module: see `src/library-guard.ts`.
import '../src/as-library';

import { diff } from '../src/canonical';
import {
  readLedger,
  runDifferential,
  type FunctionServer,
  type LedgerEntry,
} from '../src/checks/differential';
import { table as geometry } from '../src/differential/geometry';
import { adjacent } from '../src/differential/platform-math';
import { callFunctionCase, decodePlain } from '../src/vectors';

/** A server that answers with the TypeScript's own outcome, passed through
 *  `edit` first. */
function stubServer(edit: (fn: string, args: unknown[], outcome: unknown) => unknown): FunctionServer {
  const functions = Object.assign({}, ...geometry.suites.map((suite) => suite.functions));
  return {
    async call(_table, fn, args) {
      const { outcome } = callFunctionCase(functions[fn], args, `[stub] ${fn}`);
      return edit(fn, (args as unknown[]).map(decodePlain), outcome);
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
// off the JavaScript runtime's, and only that.

type MathName = 'cos' | 'sin' | 'atan2';
const math = Math as unknown as Record<MathName, (...args: number[]) => number>;

/** A server that answers with the TypeScript's outcome for the functions in
 *  `fns`, run with `name` moved `steps` doubles from the runtime's answer. */
function mathOffServer(fns: readonly string[], name: MathName, steps: number): FunctionServer {
  return stubServer((fn, args, outcome) => {
    if (!fns.includes(fn)) return outcome;
    const functions = Object.assign({}, ...geometry.suites.map((suite) => suite.functions));
    const original = math[name];
    math[name] = (...a: number[]) => {
      let value = original(...a);
      for (let i = 0; i < Math.abs(steps); i++) value = adjacent(value, steps > 0 ? 1 : -1);
      return value;
    };
    try {
      return callFunctionCase(functions[fn], args, `[stub] ${fn}`).outcome;
    } finally {
      math[name] = original;
    }
  });
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

test('the ledger allows a one-ulp cos in rotateAbout and counts it', async () => {
  const report = await runDifferential({
    table: farPivot,
    server: mathOffServer(['rotateAbout'], 'cos', -1),
    runs: 5,
    seed: SEED,
    ledger: readLedger(),
    only: ['rotateAbout'],
  });
  assert.deepEqual(report.mismatches, []);
  assert.deepEqual(report.skipped, { rotateAbout: 5 });
  assert.deepEqual(report.skippedBy, { 'PLATFORM-MATH-COS-SIN': 5 });
});

// Two ulps print the same matrix as one here, so three.
test('a three-ulp cos in rotateAbout is still a mismatch', async () => {
  const report = await runDifferential({
    table: farPivot,
    server: mathOffServer(['rotateAbout'], 'cos', -3),
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
    server: mathOffServer(['rotateAbout'], 'cos', -1),
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

test('the ledger allows a degenerate quad labelled as one ulp of atan2 gives, and no other labelling', async () => {
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
  const allowed = await runDifferential({
    table,
    server: stubServer((fn, _args, outcome) => {
      if (fn !== 'normalizePdfQuad') return outcome;
      const { returned: q } = outcome as { returned: Record<string, unknown> };
      return { returned: { ...q, lowerLeft: q.lowerRight, lowerRight: q.lowerLeft } };
    }),
    runs: 5,
    seed: SEED,
    ledger: readLedger(),
    only: ['normalizePdfQuad'],
  });
  assert.deepEqual(allowed.mismatches, []);
  assert.deepEqual(allowed.skippedBy, { 'PLATFORM-MATH-ATAN2': 5 });

  // The left corners swapped: the same four points, a labelling no one-ulp
  // atan2 reaches. (The upper corners, (0, 0) and (0, 5e-324), compare
  // equal within the tolerance, so swapping those would show nothing.)
  const swapped = await runDifferential({
    table,
    server: stubServer((fn, _args, outcome) => {
      if (fn !== 'normalizePdfQuad') return outcome;
      const { returned: q } = outcome as { returned: Record<string, unknown> };
      return { returned: { ...q, upperLeft: q.lowerLeft, lowerLeft: q.upperLeft } };
    }),
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
