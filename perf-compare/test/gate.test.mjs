import { test } from 'node:test';
import assert from 'node:assert/strict';
import { answerReasons, referencesFor, sameAnswer, warnOrderReferences } from '../lib/gate.mjs';

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
// run it. A fixture config, with plugins-next before main-now in run order as
// the tracked arms.json has them.
const config = { baseline: 'main', fallbackReference: 'main-now' };
const order = ['main', 'main-aa', 'plugins-next', 'main-now'];
const references = referencesFor(config, 'main', order);
const collected = {
  main: { answers: { searchHits: 12, dragAnnot: [270, 222] }, skipped: ['undo'] },
  'plugins-next': { answers: { searchHits: 12, dragAnnot: [270, 222], undo: { countAfter: 2 } }, skipped: [] },
  'main-now': { answers: { searchHits: 12, dragAnnot: [270, 222], undo: { countAfter: 2 } }, skipped: [] },
};
const withUndo = (arm, undo) => ({ ...collected, [arm]: { ...collected[arm], answers: { ...collected[arm].answers, undo } } });

test('with main-now in the run, it is the reference even when plugins-next runs first', () => {
  assert.deepEqual(references.slice(0, 2), ['main', 'main-now']);
  const wrong = withUndo('plugins-next', { countAfter: 3 });
  assert.deepEqual(answerReasons('main-now', wrong, references), []);
  assert.deepEqual(answerReasons('plugins-next', wrong, references), ['undo: {"countAfter":3} where main-now has {"countAfter":2}']);
  const warnings = [];
  warnOrderReferences(collected, config, 'main', references, (w) => warnings.push(w));
  assert.deepEqual(warnings, []);
});

test('without main-now, the default arms compare undo with the first arm in run order, and warn', () => {
  const defaults = ['main', 'main-aa', 'plugins-base', 'plugins-next'];
  const run = {
    main: collected.main,
    'main-aa': collected.main,
    'plugins-base': collected['plugins-next'],
    'plugins-next': collected['plugins-next'],
  };
  const refs = referencesFor(config, 'main', defaults);
  for (const id of defaults) assert.deepEqual(answerReasons(id, run, refs), [], id);
  const warnings = [];
  warnOrderReferences(run, config, 'main', refs, (w) => warnings.push(w));
  assert.deepEqual(warnings, [
    'gate: no main or main-now answer for undo; compared with plugins-base, the first arm in run order that gave one',
  ]);
  const off = { ...run, 'plugins-next': { ...run['plugins-next'], answers: { ...run['plugins-next'].answers, undo: { countAfter: 3 } } } };
  assert.deepEqual(answerReasons('plugins-next', off, refs), ['undo: {"countAfter":3} where plugins-base has {"countAfter":2}']);
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
