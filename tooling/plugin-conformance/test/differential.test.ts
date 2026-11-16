import assert from 'node:assert/strict';
import { test } from 'node:test';

import { diff } from '../src/canonical';
import {
  runDifferential,
  type FunctionServer,
  type LedgerEntry,
} from '../src/differential';
import { table as geometry } from '../src/differential/geometry';
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

test('differential reports a planted mismatch', async () => {
  const report = await runDifferential({
    table: geometry,
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

// Review Focus 5: the vectors print -0 as 0 (canonical.ts) and replay
// compares 0 and -0 equal (canonical.rs numbers_equal), so a Rust -0.0
// where the TypeScript has 0 is no mismatch here either.
test('differential compares -0 and 0 as the vectors do', async () => {
  assert.equal(diff({ returned: [0, 1] }, { returned: [-0, 1] }), null);
  const report = await runDifferential({
    table: geometry,
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
      skips: { 'X-NAN-CORNER': (_fn, args) => Number.isNaN((args[0] as { x: number }).x) },
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
