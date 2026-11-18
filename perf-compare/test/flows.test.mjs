import { test } from 'node:test';
import assert from 'node:assert/strict';
import { armRuns, FLOWS, sampleFields, warmupFailureOn } from '../lib/flows.mjs';
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

// Page time that moves 10 ms per pointer move, so a test can tell time
// counted from the first move from time counted from the last.
function pacedClock() {
  const real = globalThis.performance;
  let clock = 0;
  globalThis.performance = { now: () => clock };
  return { tick: () => (clock += 10), restore: () => (globalThis.performance = real) };
}

test('hover and drawInk count settleMs from the last pointer move and wallMs from the first, as scroll does', async () => {
  const clock = pacedClock();
  const hover = fakeRun({ capabilities: [], pageBox: box, annotations: () => highlights, onInput: (n) => n === 'mouse.move' && clock.tick() });
  try {
    const r = await FLOWS.hover.measure(hover.page, hover.cdp);
    // Three highlights, one move each; the stub settles at doneAt.
    assert.equal(r.metrics.settleMs, 0);
    assert.equal(r.metrics.wallMs, 30);
  } finally {
    hover.restore();
  }
  let ups = 0;
  let polls = 0;
  const ink = fakeRun({
    capabilities: [],
    pageBox: box,
    execute: () => {},
    annotationCount: () => (ups === 3 ? 1 : 0),
    watch: () => 0,
    // The ink lands on the third poll after the last stroke, 20 ms later.
    read: () => (ups === 3 && ++polls >= 3 ? 1 : (ups === 3 && clock.tick(), 0)),
    onInput: (name) => (name === 'mouse.move' && clock.tick(), name === 'mouse.up' && ups++),
  });
  try {
    const r = await FLOWS.drawInk.measure(ink.page, ink.cdp);
    // Three strokes of 60 moves.
    assert.equal(r.metrics.settleMs, 20);
    assert.equal(r.metrics.wallMs, 1820);
  } finally {
    ink.restore();
    clock.restore();
  }
});

test('a failed annotate or select warm-up is logged and kept for the sample line, not swallowed', async () => {
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
    // runFlow puts the page's first failure on the sample as `warmupFailed`.
    assert.equal(warmupFailureOn(run.page), 'timed out waiting for the warm-up highlight');
    assert.equal(warmupFailureOn({}), undefined, 'a page whose warm-up never failed');
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

test('undo finds its page once and polls only its count while the ten undos land', async () => {
  let count = 2;
  let undos = 0;
  const run = fakeRun({
    capabilities: ['history'],
    pageBox: box,
    execute: (command) => {
      if (command === 'history:undo' && ++undos % 10 === 0) count = 2;
    },
    annotationCount: () => count,
    watch: (kind, arg) => (assert.deepEqual([kind, arg], ['annotationCount', 208]), 0),
    read: () => count,
    onInput: (name) => name === 'mouse.up' && count++,
  });
  try {
    const r = await FLOWS.undo.measure(run.page, run.cdp);
    assert.ok(run.log.lastIndexOf('watch') < run.log.indexOf('cpuStart'));
    assert.deepEqual([...new Set(inWindow(run.log))], ['execute', 'read']);
    assert.deepEqual(r.answers, { countBefore: 2, countEdited: 12, countAfter: 2 });
  } finally {
    run.restore();
  }
});

for (const flow of ['annotate', 'annotateLarge']) {
  test(`${flow} finds its page and drag ends before the CPU window and polls only its count`, async () => {
    let count = 0;
    const run = fakeRun({
      capabilities: [],
      pageBox: box,
      execute: () => {},
      annotationCount: () => count,
      watch: (kind) => (assert.equal(kind, 'annotationCount'), 0),
      read: () => count,
      onInput: (name) => name === 'mouse.up' && count++,
    });
    try {
      const r = await FLOWS[flow].measure(run.page, run.cdp);
      assert.ok(run.log.indexOf('watch') < run.log.indexOf('cpuStart'));
      assert.deepEqual([...new Set(inWindow(run.log))], ['read']);
      assert.deepEqual(r.answers, { countBefore: 0, countAfter: 1 });
    } finally {
      run.restore();
    }
  });
}

test('a sample line carries warmupFailed only when the warm-up failed', () => {
  const metrics = { settleMs: 1 };
  // A clean sample serializes exactly as before the field existed.
  assert.equal(JSON.stringify(sampleFields({ metrics, inspected: undefined, workerThrottled: null })), '{"metrics":{"settleMs":1}}');
  assert.deepEqual(Object.keys(sampleFields({ metrics, diagnostics: { cycle5: {} } })), ['metrics', 'diagnostics']);
  assert.deepEqual(sampleFields({ metrics, warmupFailed: 'the drag selected no text' }), {
    metrics,
    warmupFailed: 'the drag selected no text',
  });
});
