import { test } from 'node:test';
import assert from 'node:assert/strict';
import { better, isDifference, rowLabel, rows, unit } from '../lib/rows.mjs';

test('settle times and counts are differences, other times are ratios', () => {
  for (const m of ['settleMs', 'zoom2SettleMs', 'jankFrames', 'longTaskMs', 'heapSoakSlopeBytes']) assert.equal(isDifference(m), true, m);
  for (const m of ['mainJsMs', 'settledMs', 'wallMs', 'heapCycle5Bytes']) assert.equal(isDifference(m), false, m);
});

test('only open rows name their fixture', () => {
  assert.equal(rowLabel('open', 'ebook', 'readyMs'), 'open(ebook) / readyMs');
  assert.equal(rowLabel('scroll', 'large', 'wallMs'), 'scroll / wallMs');
});

test('rows groups values by arm and skips failed samples', () => {
  const out = rows([
    { arm: 'a', flow: 'scroll', fixture: 'large', round: 0, metrics: { wallMs: 1 } },
    { arm: 'a', flow: 'scroll', fixture: 'large', round: 1, metrics: { wallMs: 2 } },
    { arm: 'b', flow: 'scroll', fixture: 'large', round: 0, metrics: { wallMs: 3 } },
    { arm: 'b', flow: 'scroll', fixture: 'large', round: 1, error: 'boom' },
  ]);
  assert.deepEqual([...out.keys()], ['scroll|large|wallMs']);
  assert.deepEqual(out.get('scroll|large|wallMs').byArm, { a: [1, 2], b: [3] });
});

test('unit and better read the metric name', () => {
  assert.equal(unit('heapCycle5Bytes'), 'bytes');
  assert.equal(unit('mainJsMs'), 'ms');
  assert.equal(unit('jankFrames'), 'count');
  assert.equal(better('mainJsMs'), 'lower');
});
