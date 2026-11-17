import { test } from 'node:test';
import assert from 'node:assert/strict';
import { armRuns, FLOWS } from '../lib/flows.mjs';
import { DONE_NEEDS, hasDone } from '../lib/done.mjs';

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

// A page whose evaluate and waitForFunction run here, against a fake
// window.__perf, and a CDP stub. `log` records the bridge calls and the CPU
// window's edges, so a test can see what a done condition polls inside it.
function fakeRun(perf, { warn } = {}) {
  const log = [];
  const record = (name, fn) => (...args) => {
    log.push(name);
    return fn(...args);
  };
  const bridge = Object.fromEntries(Object.entries(perf).map(([k, v]) => [k, typeof v === 'function' ? record(k, v) : v]));
  globalThis.window = { __perf: bridge, __probe: { settledAt: async (since, { doneAt = since } = {}) => doneAt } };
  const input = (name) => async () => {
    log.push(name);
    perf.onInput?.(name);
  };
  const page = {
    evaluate: async (fn, arg) => fn(arg),
    waitForFunction: async (fn, arg) => {
      for (let i = 0; i < 100; i++) {
        if (fn(arg)) return;
        await new Promise((r) => setImmediate(r));
      }
      throw new Error('timeout');
    },
    waitForTimeout: async () => {},
    mouse: { move: input('mouse.move'), down: input('mouse.down'), up: input('mouse.up'), click: input('mouse.click') },
    keyboard: { type: input('keyboard.type'), press: input('keyboard.press') },
  };
  const cdp = {
    cpuStart: async () => log.push('cpuStart'),
    cpuEnd: async () => (log.push('cpuEnd'), { mainJsMs: 1 }),
  };
  const warnings = [];
  const original = console.warn;
  console.warn = (w) => warnings.push(w);
  return { page, cdp, log, warnings, restore: () => (console.warn = original), window: () => window };
}

// The bridge calls made between cpuStart and cpuEnd.
const inWindow = (log) => log.slice(log.indexOf('cpuStart') + 1, log.indexOf('cpuEnd')).filter((c) => !/^(mouse|keyboard)\./.test(c));

const box = () => ({ x: 0, y: 0, width: 612, height: 792 });

test('typeField finds its field once, before the CPU window, and polls only its value', async () => {
  let typed = false;
  const run = fakeRun({
    capabilities: [],
    pageBox: box,
    fieldText: () => 'The quick brown fox jumps over a lazy do',
    watch: (kind, arg) => (assert.deepEqual([kind, arg], ['fieldText', 't51-1']), 0),
    read: () => (typed ? 'The quick brown fox jumps over a lazy do' : ''),
    onInput: (name) => (typed ||= name === 'keyboard.press'),
  });
  try {
    const r = await FLOWS.typeField.measure(run.page, run.cdp);
    assert.ok(run.log.indexOf('watch') < run.log.indexOf('cpuStart'));
    assert.deepEqual([...new Set(inWindow(run.log))], ['read']);
    assert.equal(r.answers.value, 'The quick brown fox jumps over a lazy do');
  } finally {
    run.restore();
  }
});

test('dragAnnot finds its stamp once and polls only that stamp until its box moves', async () => {
  let moved = false;
  const run = fakeRun({
    capabilities: [],
    pageBox: box,
    annotations: () => [{ subtype: 'highlight', box: [1, 2, 3, 4] }, { subtype: 'stamp', box: moved ? [70, 100, 50, 50] : [270, 100, 50, 50] }],
    watch: (kind, arg) => (assert.deepEqual([kind, arg], ['annotationBox', [205, 'stamp']]), 0),
    read: () => (moved ? [70, 100, 50, 50] : [270, 100, 50, 50]),
    onInput: (name) => (moved ||= name === 'mouse.up'),
  });
  try {
    const r = await FLOWS.dragAnnot.measure(run.page, run.cdp);
    assert.ok(run.log.indexOf('watch') < run.log.indexOf('cpuStart'));
    assert.deepEqual([...new Set(inWindow(run.log))], ['read']);
    assert.deepEqual(r.answers, { stampBefore: [270, 100, 50, 50], stampAfter: [70, 100, 50, 50] });
  } finally {
    run.restore();
  }
});

test('drawInk polls its page count through one watch until the ink is added', async () => {
  let ups = 0;
  const run = fakeRun({
    capabilities: [],
    pageBox: box,
    execute: () => {},
    annotationCount: () => (ups === 3 ? 1 : 0),
    watch: (kind, arg) => (assert.deepEqual([kind, arg], ['annotationCount', 202]), 0),
    read: () => (ups === 3 ? 1 : 0),
    onInput: (name) => name === 'mouse.up' && ups++,
  });
  try {
    const r = await FLOWS.drawInk.measure(run.page, run.cdp);
    assert.ok(run.log.indexOf('watch') < run.log.indexOf('cpuStart'));
    assert.deepEqual([...new Set(inWindow(run.log))], ['read']);
    assert.deepEqual(r.answers, { countBefore: 0, countAfter: 1 });
  } finally {
    run.restore();
  }
});

const highlights = Array.from({ length: 3 }, (_, i) => ({ subtype: 'highlight', box: [50, 60 + 13 * i, 300, 13] }));

test('hover, on a viewer with a hover read, is done when the last highlight is hovered', async () => {
  let moves = 0;
  let polls = 0;
  const run = fakeRun({
    capabilities: ['history', 'hovered'],
    pageBox: box,
    annotations: () => highlights,
    watch: (kind) => (assert.equal(kind, 'hoveredBox'), 0),
    // The last highlight shows as hovered only on the third poll after the
    // last move: the flow has to wait for it.
    read: () => {
      if (moves < 3) return moves ? highlights[moves - 1].box : null;
      return ++polls >= 3 ? highlights[2].box : highlights[1].box;
    },
    onInput: (name) => name === 'mouse.move' && moves++,
  });
  try {
    await FLOWS.hover.measure(run.page, run.cdp);
    assert.ok(run.log.indexOf('watch') < run.log.indexOf('cpuStart'));
    assert.deepEqual([...new Set(inWindow(run.log))], ['read']);
    assert.equal(polls, 3);
  } finally {
    run.restore();
  }
});

test('hover, on a viewer without one, ends at its last pointer move and watches nothing', async () => {
  const run = fakeRun({ capabilities: [], pageBox: box, annotations: () => highlights, watch: () => assert.fail('no hover read to watch') });
  try {
    const r = await FLOWS.hover.measure(run.page, run.cdp);
    assert.deepEqual(inWindow(run.log), []);
    assert.deepEqual(r.answers, { highlights: 3 });
  } finally {
    run.restore();
  }
});

test('a failed annotate or select warm-up is logged, not swallowed', async () => {
  const run = fakeRun({
    capabilities: [],
    pageBox: box,
    execute: () => {},
    goToPageIndex: () => {},
    currentPageIndex: () => 2,
    annotationCount: () => 0,
    readSelection: async () => '',
  });
  try {
    await FLOWS.annotate.warmup(run.page, run.cdp);
    await FLOWS.select.warmup(run.page, run.cdp);
    assert.deepEqual(run.warnings, [
      '[annotate] warm-up failed: timed out waiting for the warm-up highlight',
      '[select] warm-up failed: the drag selected no text',
    ]);
  } finally {
    run.restore();
  }
});

test('hover is the flow whose done condition needs a hover read', () => {
  assert.deepEqual(DONE_NEEDS, { hover: 'hovered' });
  assert.equal(hasDone('hover', ['history']), false);
  assert.equal(hasDone('hover', ['history', 'hovered']), true);
  assert.equal(hasDone('undo', []), true);
});
