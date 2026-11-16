import { test } from 'node:test';
import assert from 'node:assert/strict';
import { answerReasons, sameAnswer } from '../lib/gate.mjs';

// From the gate of results/2026-11-16T20-06-31-783Z (main against plugins-next).
const OLD = 'Pay through the nose for a commercial\r\nPDF SDK that locks you into their\r\necosystem';
const NEW = 'Pay through the nose for a commercial \nPDF SDK that locks you into their \necosystem';

test('a selection matches across the two line-break encodings', () => {
  assert.ok(sameAnswer('selection', NEW, OLD));
});

test('a selection with different words still fails', () => {
  assert.ok(!sameAnswer('selection', NEW.replace('their', 'your'), OLD));
  assert.ok(!sameAnswer('selection', 'Pay through the nose for a commercial PDF', OLD));
});

test('other answers are compared exactly', () => {
  assert.ok(sameAnswer('searchHits', 12, 12));
  assert.ok(!sameAnswer('searchHits', 12, 13));
  assert.ok(!sameAnswer('largePageCount', '1 \n', '1\r\n'));
});

// main has no history plugin, so it skips `undo`; main-now and plugins-next run it.
const collected = {
  main: { answers: { searchHits: 12, dragAnnot: [270, 222] }, skipped: ['undo'] },
  'main-now': { answers: { searchHits: 12, dragAnnot: [270, 222], undo: { countAfter: 2 } }, skipped: [] },
  'plugins-next': { answers: { searchHits: 12, dragAnnot: [270, 222], undo: { countAfter: 2 } }, skipped: [] },
};
const order = ['main', 'main-now', 'plugins-next'];

test('an answer the baseline skipped is compared with the first arm that has it', () => {
  assert.deepEqual(answerReasons('plugins-next', collected, order), []);
  const wrong = { ...collected, 'plugins-next': { ...collected['plugins-next'], answers: { ...collected['plugins-next'].answers, undo: { countAfter: 3 } } } };
  assert.deepEqual(answerReasons('plugins-next', wrong, order), ['undo: {"countAfter":3} where main-now has {"countAfter":2}']);
});

test('an arm may lack only the answers of the flows it skipped', () => {
  assert.deepEqual(answerReasons('main', collected, order), []);
  const missing = { ...collected, 'plugins-next': { answers: { searchHits: 12, dragAnnot: [270, 222] }, skipped: [] } };
  assert.deepEqual(answerReasons('plugins-next', missing, order), ['undo: no answer where main-now has one']);
});

test('the baseline stays the reference for the answers it has', () => {
  const moved = { ...collected, 'main-now': { ...collected['main-now'], answers: { ...collected['main-now'].answers, dragAnnot: [271, 222] } } };
  assert.deepEqual(answerReasons('main-now', moved, order), ['dragAnnot: [271,222] where main has [270,222]']);
});
