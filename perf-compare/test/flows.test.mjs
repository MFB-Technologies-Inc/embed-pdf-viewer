import { test } from 'node:test';
import assert from 'node:assert/strict';
import { armRuns, FLOWS } from '../lib/flows.mjs';

test('undo needs a history plugin', () => {
  assert.deepEqual(FLOWS.undo.needs, ['history']);
  assert.equal(armRuns('undo', []), false);
  assert.equal(armRuns('undo', ['history']), true);
});

test('a flow that needs nothing runs on every arm', () => {
  for (const flow of ['scrollMid', 'hover', 'dragAnnot', 'drawInk', 'typeField']) {
    assert.equal(armRuns(flow, []), true, flow);
  }
});
