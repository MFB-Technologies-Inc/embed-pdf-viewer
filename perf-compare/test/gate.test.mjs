import { test } from 'node:test';
import assert from 'node:assert/strict';
import { answerReasons, referencesFor, sameAnswer } from '../lib/gate.mjs';
import { loadConfig } from '../lib/config.mjs';

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

// main has no history plugin, so it skips `undo`; main-now and plugins-next
// run it. `references` is what runGate passes: the baseline, then arms.json's
// fallbackReference.
const references = ['main', 'main-now'];
const collected = {
  main: { answers: { searchHits: 12, dragAnnot: [270, 222] }, skipped: ['undo'] },
  'main-now': { answers: { searchHits: 12, dragAnnot: [270, 222], undo: { countAfter: 2 } }, skipped: [] },
  'plugins-next': { answers: { searchHits: 12, dragAnnot: [270, 222], undo: { countAfter: 2 } }, skipped: [] },
};
const withUndo = (arm, undo) => ({ ...collected, [arm]: { ...collected[arm], answers: { ...collected[arm].answers, undo } } });

test('an answer the baseline skipped is compared with the fallback reference', () => {
  assert.deepEqual(answerReasons('plugins-next', collected, references), []);
  assert.deepEqual(answerReasons('plugins-next', withUndo('plugins-next', { countAfter: 3 }), references), [
    'undo: {"countAfter":3} where main-now has {"countAfter":2}',
  ]);
});

test('the fallback reference does not depend on the order the arms ran in', () => {
  // arms.json lists plugins-next before main-now, so a run collects it first.
  // Here plugins-next is the one that's wrong; main-now must still pass.
  const config = loadConfig();
  assert.ok(config.armOrder.indexOf('plugins-next') < config.armOrder.indexOf('main-now'));
  const refs = referencesFor(config, 'main');
  assert.deepEqual(refs, ['main', 'main-now']);
  const ranFirst = { main: collected.main, 'plugins-next': withUndo('plugins-next', { countAfter: 3 })['plugins-next'], 'main-now': collected['main-now'] };
  assert.deepEqual(answerReasons('main-now', ranFirst, refs), []);
  assert.deepEqual(answerReasons('plugins-next', ranFirst, refs), ['undo: {"countAfter":3} where main-now has {"countAfter":2}']);
});

test('an answer no reference gives fails the arms that give it', () => {
  const { 'main-now': _, ...noFallback } = collected;
  assert.deepEqual(answerReasons('plugins-next', noFallback, references), ['undo: no reference arm answered it (main, main-now)']);
  assert.deepEqual(answerReasons('main', noFallback, references), []);
});

test('an arm may lack only the answers of the flows it skipped', () => {
  assert.deepEqual(answerReasons('main', collected, references), []);
  const missing = { ...collected, 'plugins-next': { answers: { searchHits: 12, dragAnnot: [270, 222] }, skipped: [] } };
  assert.deepEqual(answerReasons('plugins-next', missing, references), ['undo: no answer where main-now has one']);
});

test('the baseline stays the reference for the answers it has', () => {
  const moved = { ...collected, 'main-now': { ...collected['main-now'], answers: { ...collected['main-now'].answers, dragAnnot: [271, 222] } } };
  assert.deepEqual(answerReasons('main-now', moved, references), ['dragAnnot: [271,222] where main has [270,222]']);
});
