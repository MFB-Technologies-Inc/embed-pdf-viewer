import { test } from 'node:test';
import assert from 'node:assert/strict';
import { answerReasons, gateRecord, referencesFor, sameAnswer, warnOrderReferences } from '../lib/gate.mjs';

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

// The default arms as arms.json pins them: plugins-base (9d6c2eca1) builds
// as viewer API v1 with no capabilities, so it skips undo as main does, and
// plugins-next is the only arm that answers it.
const defaults = ['main', 'main-aa', 'plugins-base', 'plugins-next'];
const defaultRun = {
  main: collected.main,
  'main-aa': collected.main,
  'plugins-base': collected.main,
  'plugins-next': collected['plugins-next'],
};

test('without main-now, the default arms leave undo unchecked, record it, and say no comparison happened', () => {
  const refs = referencesFor(config, 'main', defaults);
  for (const id of defaults) assert.deepEqual(answerReasons(id, defaultRun, refs), [], id);
  assert.deepEqual(gateRecord('plugins-next', defaultRun, refs), {
    references: { searchHits: 'main', dragAnnot: 'main', undo: 'unchecked' },
    unchecked: ['undo'],
  });
  assert.deepEqual(gateRecord('plugins-base', defaultRun, refs), {
    references: { searchHits: 'main', dragAnnot: 'main' },
    unchecked: [],
  });
  const warnings = [];
  warnOrderReferences(defaultRun, config, 'main', refs, (w) => warnings.push(w));
  assert.deepEqual(warnings, [
    'gate: only plugins-next answered undo, so no comparison happened; undo is unchecked for plugins-next, and the report leaves out its undo rows',
  ]);
});

test('with main-now in the default arms, undo is checked against main-now', () => {
  const order = [...defaults, 'main-now'];
  const run = { ...defaultRun, 'main-now': collected['main-now'] };
  const refs = referencesFor(config, 'main', order);
  assert.deepEqual(gateRecord('plugins-next', run, refs).references.undo, 'main-now');
  assert.deepEqual(gateRecord('plugins-next', run, refs).unchecked, []);
  assert.deepEqual(gateRecord('main-now', run, refs).unchecked, []);
  const off = withUndo('plugins-next', { countAfter: 3 });
  assert.deepEqual(answerReasons('plugins-next', { ...run, 'plugins-next': off['plugins-next'] }, refs), [
    'undo: {"countAfter":3} where main-now has {"countAfter":2}',
  ]);
  const warnings = [];
  warnOrderReferences(run, config, 'main', refs, (w) => warnings.push(w));
  assert.deepEqual(warnings, []);
});

test('two history arms without main-now compare undo with the first in run order, and warn', () => {
  const order = ['main', 'main-aa', 'plugins-next', 'plugins-next-lto'];
  const run = {
    main: collected.main,
    'main-aa': collected.main,
    'plugins-next': collected['plugins-next'],
    'plugins-next-lto': collected['plugins-next'],
  };
  const refs = referencesFor(config, 'main', order);
  for (const id of order) assert.deepEqual(answerReasons(id, run, refs), [], id);
  assert.deepEqual(gateRecord('plugins-next-lto', run, refs).references.undo, 'plugins-next');
  assert.deepEqual(gateRecord('plugins-next', run, refs).unchecked, []);
  const warnings = [];
  warnOrderReferences(run, config, 'main', refs, (w) => warnings.push(w));
  assert.deepEqual(warnings, [
    'gate: no main or main-now answer for undo; compared with plugins-next, the first arm in run order that gave one',
  ]);
  const off = { ...run, 'plugins-next-lto': { ...run['plugins-next-lto'], answers: { ...run['plugins-next-lto'].answers, undo: { countAfter: 3 } } } };
  assert.deepEqual(answerReasons('plugins-next-lto', off, refs), ['undo: {"countAfter":3} where plugins-next has {"countAfter":2}']);
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
