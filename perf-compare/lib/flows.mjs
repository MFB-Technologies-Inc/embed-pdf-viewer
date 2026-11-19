// The nineteen flows. For the first eight, the table in
// viewer-perf-compare-plan.md (Task 7, Step 2) is the contract: starting state,
// warm-up, measured action, done condition. Added since: `coldScroll`, which
// is `scroll` without its warm-up, and `searchLarge`, `selectLarge`,
// `annotateLarge` and `zoomLarge`, which are their flows on large.pdf. Then
// `scrollMid`, and five flows on annots.pdf and forms.pdf (fixtures/generate.mjs)
// that see what annotations and form fields cost: `hover`, `dragAnnot`,
// `drawInk`, `typeField` and `undo`.
//
// Every time is page clock minus page clock (performance.now() in the page).
// The runner calls start, warmup, start, then measure; only measure is timed.
import { openCdp } from './cdp.mjs';
import { DONE_NEEDS } from './done.mjs';
import { slope, summarize } from './stats.mjs';

/** @typedef {{ fixture: string, needs?: string[], start(page): Promise<void>, warmup?(page, cdp): Promise<void>, measure(page, cdp): Promise<{ metrics: Record<string, number>, diagnostics?: unknown, answers?: unknown }> }} Flow */

// Drag regions, as fractions of the page box. Picked on ebook.pdf page index 1
// at zoom 1: four lines of the "1 Pay through the nose" paragraph, and the
// single line "You need PDF functionality in your app."
const SELECT_DRAG = { from: [0.2, 0.384], to: [0.78, 0.475] };
const HIGHLIGHT_DRAG = { from: [0.11, 0.252], to: [0.69, 0.252] };
// large.pdf's 40 lines of Courier start at y = 730 of 792 with 13 pt leading,
// from x = 54 to about x = 384 of 612: lines 5 to 9, and line 3.
const LARGE_SELECT_DRAG = { from: [0.12, 0.139], to: [0.55, 0.205] };
const LARGE_HIGHLIGHT_DRAG = { from: [0.1, 0.106], to: [0.55, 0.106] };
// #viewer fills the window, so "outside" is the tab bar's empty left corner:
// over no page, so no hover can ask for text geometry.
const PARKED = { x: 4, y: 4 };

// Every fixture page is US Letter, in pt.
const LETTER = { width: 612, height: 792 };

const now = (page) => page.evaluate(() => performance.now());
const settle = (page, since, opts = {}) =>
  page.evaluate(([s, o]) => window.__probe.settledAt(s, o), [since, opts]);

async function waitFor(page, fn, arg, what, timeout = 30_000) {
  try {
    await page.waitForFunction(fn, arg, { timeout, polling: 16 });
  } catch {
    throw new Error(`timed out waiting for ${what}`);
  }
  return now(page);
}

// Main-thread task time, and JavaScript time from a trace of the same window
// (`mainJsMs`). It replaces `ScriptDuration`, which leaves microtasks out: a
// Preact render is one, so a change that moved work out of a render read as
// more script when the JavaScript run was the same.
const cpu = (cdp, before) => cdp.cpuEnd(before);

async function goTo(page, index) {
  await page.evaluate((i) => window.__perf.goToPageIndex(i), index);
  return waitFor(page, (i) => window.__perf.currentPageIndex() === i, index, `page index ${index}`);
}

async function zoomTo(page, zoom) {
  await page.evaluate((z) => window.__perf.zoomTo(z), zoom);
  return waitFor(page, (z) => Math.abs(window.__perf.zoomLevel() - z) < 1e-6, zoom, `zoom ${zoom}`);
}

// The shared starting state: no query, no selection, pointer tool, the given
// page at zoom 1, settled.
function startAt(pageIndex, { park = false } = {}) {
  return async (page) => {
    await page.evaluate(() => {
      window.__perf.clearSearch();
      window.__perf.clearSelection();
      window.__perf.execute('pointer:toggle');
    });
    if (park) await page.mouse.move(PARKED.x, PARKED.y);
    await zoomTo(page, 1);
    await goTo(page, pageIndex);
    await settle(page, await now(page), { requireChange: false });
  };
}

// A drag's two ends in window px. `pageBox` walks the viewer's shadow DOM, so
// a timed drag works these out before its window (`dragAlong`).
async function dragEnds(page, pageIndex, { from, to }) {
  const box = await page.evaluate((i) => window.__perf.pageBox(i), pageIndex);
  const at = ([fx, fy]) => [box.x + box.width * fx, box.y + box.height * fy];
  return [at(from), at(to)];
}

async function drag(page, pageIndex, region) {
  await dragAlong(page, await dragEnds(page, pageIndex, region));
}

async function dragAlong(page, [[x0, y0], [x1, y1]]) {
  await page.mouse.move(x0, y0, { steps: 4 });
  await page.mouse.down();
  await page.mouse.move(x1, y1, { steps: 12 });
  await page.mouse.up();
}

async function readSelection(page, timeout = 10_000) {
  const deadline = Date.now() + timeout;
  for (;;) {
    const text = await page.evaluate(() => window.__perf.readSelection());
    if (text) return { text, at: await now(page) };
    if (Date.now() > deadline) throw new Error('the drag selected no text');
    await page.waitForTimeout(16);
  }
}

async function wheel(page, count) {
  for (let i = 0; i < count; i++) {
    await page.mouse.wheel(0, 240);
    await page.waitForTimeout(16);
  }
}

async function centre(page) {
  const box = await page.locator('#viewer').boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
}

// `scroll`'s 60 wheels from page index `pageIndex`, warmed up at `warmPage`.
/** @type {(pageIndex: number, warmPage: number) => Flow} */
const scrollFlow = (pageIndex, warmPage) => ({
  fixture: 'large',
  async start(page) {
    await startAt(pageIndex)(page);
    await centre(page);
  },
  async warmup(page) {
    await goTo(page, warmPage);
    await centre(page);
    await wheel(page, 20);
    await settle(page, await now(page), { requireChange: false });
  },
  async measure(page, cdp) {
    await centre(page);
    const before = await cdp.cpuStart();
    // Frames and long tasks from here on only.
    await page.evaluate(() => window.__probe.reset());
    const t0 = await now(page);
    await wheel(page, 60);
    const tLast = await now(page);
    // requireChange counts from t0, not tLast: the scroll's renders may all
    // land during the wheel burst, and that's a settled page, not a failure.
    const settled = await settle(page, t0, { doneAt: tLast });
    const load = await cpu(cdp, before);
    const { frames, longTasks } = await page.evaluate(() => ({
      frames: window.__probe.frames(),
      longTasks: window.__probe.longTasks(),
    }));
    const s = summarize(frames);
    const inFlow = longTasks.filter((l) => l.start >= t0);
    return {
      metrics: {
        settleMs: settled - tLast,
        wallMs: settled - t0,
        frameP50Ms: s.median,
        frameP95Ms: s.p95,
        jankFrames: frames.filter((f) => f > 50).length,
        longTasks: inFlow.length,
        longTaskMs: inFlow.reduce((a, l) => a + l.duration, 0),
        ...load,
      },
    };
  },
});

// A warm-up that fails leaves the measured window cold, not wrong: the
// measure still has its own done condition. So the run goes on, but says so,
// on the console and, through runFlow's `warmupFailed`, on the sample's line.
const warmupFailures = new WeakMap(); // page -> first failure's message
const warmupFailed = (page, flow) => (error) => {
  const message = String(error?.message ?? error).split('\n')[0];
  console.warn(`[${flow}] warm-up failed: ${message}`);
  if (!warmupFailures.has(page)) warmupFailures.set(page, message);
};

// The first warm-up failure on `page`, or undefined.
export const warmupFailureOn = (page) => warmupFailures.get(page);

// A page-space point (pt, y down, as window.__perf.annotations gives boxes)
// on page `pageIndex`, in window px.
async function screenPoint(page, pageIndex, [x, y]) {
  const box = await page.evaluate((i) => window.__perf.pageBox(i), pageIndex);
  return [box.x + (box.width * x) / LETTER.width, box.y + (box.height * y) / LETTER.height];
}
const centreOf = ([x, y, w, h]) => [x + w / 2, y + h / 2];
const annotationsOn = (page, pageIndex) => page.evaluate((i) => window.__perf.annotations(i), pageIndex);

// A done condition polls one page's count, one annotation or one field
// (entry/main.ts `watch`), found once before the timed window, not a scan of
// a 1,000-entry list. The read still differs by arm: the count watch lists
// the page's annotations (`annotationCountOn` in entry/api-v*.ts), which a
// Rust arm serializes across the wire whenever its store has changed, and
// every read on a Rust arm first checks the kernel's state revision
// (packages/core-wasm/src/proxy.ts `sync`).
const watch = (page, kind, arg) => page.evaluate(([k, a]) => window.__perf.watch(k, a), [kind, arg]);

// annots.pdf (fixtures/generate.mjs): page index 200 has 20 highlights, one
// per line on lines 1 to 20; a stamp is on every page index 8k + 5; page
// indexes 202 and 208 have no ink and no stamp.
const HOVER_PAGE = 200;
const STAMP_PAGE = 205;
const INK_PAGE = 202;
const UNDO_PAGE = 208;

// The page's highlight boxes, top to bottom.
const highlightBoxes = async (page, pageIndex) =>
  (await annotationsOn(page, pageIndex))
    .filter((a) => a.subtype === 'highlight')
    .map((a) => a.box)
    .sort((a, b) => a[1] - b[1]);

// The centres of the page's highlights, top to bottom, in window px.
async function highlightCentres(page, pageIndex) {
  const boxes = await highlightBoxes(page, pageIndex);
  return Promise.all(boxes.map((b) => screenPoint(page, pageIndex, centreOf(b))));
}

async function hoverAcross(page, points) {
  for (const [x, y] of points) await page.mouse.move(x, y, { steps: 4 });
}

// The arm handled the hover once the viewer reports the last highlight as
// the annotation under the pointer. Only viewers with a hover read
// (`hovered`, lib/done.mjs) can say so; on the others the flow ends at its
// last pointer move, and the report marks those rows. As in `scroll`,
// `settleMs` counts from the last pointer move and `wallMs` from the first:
// the 80 moves are frame-paced, so a settle from t0 is mostly pacing.
/** @type {Flow} */
const hoverFlow = {
  fixture: 'annots',
  start: startAt(HOVER_PAGE, { park: true }),
  async warmup(page) {
    await goTo(page, HOVER_PAGE + 1);
    await settle(page, await now(page), { requireChange: false });
    await hoverAcross(page, await highlightCentres(page, HOVER_PAGE + 1));
    await page.mouse.move(PARKED.x, PARKED.y);
  },
  async measure(page, cdp) {
    const points = await highlightCentres(page, HOVER_PAGE);
    const capabilities = await page.evaluate(() => window.__perf.capabilities);
    const reads = capabilities.includes(DONE_NEEDS.hover);
    const last = reads ? (await highlightBoxes(page, HOVER_PAGE)).at(-1) : null;
    const hovered = reads ? await watch(page, 'hoveredBox') : null;
    const before = await cdp.cpuStart();
    const t0 = await now(page);
    // The pointer enters the page on the first highlight and moves down
    // across all 20, four steps from one centre to the next.
    await hoverAcross(page, points);
    const tLast = await now(page);
    const done = reads
      ? await waitFor(
          page,
          ([id, box]) => JSON.stringify(window.__perf.read(id)) === box,
          [hovered, JSON.stringify(last)],
          'the last highlight to be hovered',
          10_000,
        )
      : tLast;
    const settled = await settle(page, t0, { doneAt: done, requireChange: false });
    return {
      metrics: { settleMs: settled - tLast, wallMs: settled - t0, ...(await cpu(cdp, before)) },
      answers: { highlights: points.length },
    };
  },
};

const stampBox = async (page, pageIndex) => (await annotationsOn(page, pageIndex)).find((a) => a.subtype === 'stamp')?.box;

// The stamp on page index `pageIndex`: its box, where to press, and a watch
// on its box. Untimed.
async function findStamp(page, pageIndex) {
  const from = await stampBox(page, pageIndex);
  if (!from) throw new Error(`page index ${pageIndex} has no stamp`);
  const at = await screenPoint(page, pageIndex, centreOf(from));
  return { from, at, box: await watch(page, 'annotationBox', [pageIndex, 'stamp']) };
}

// Presses on the stamp's centre with the pointer tool and drags it `dx` px
// left in 20 moves; resolves once the stamp's box has moved.
async function dragStamp(page, { from, at: [x, y], box }, dx) {
  await page.mouse.move(x, y, { steps: 4 });
  await page.mouse.down();
  await page.mouse.move(x - dx, y, { steps: 20 });
  await page.mouse.up();
  return waitFor(page, ([id, x0]) => window.__perf.read(id)?.[0] !== x0, [box, from[0]], 'the stamp to move', 10_000);
}

// Named for what Phase D watches. It drags a stamp, not a highlight: neither
// viewer API generation moves a highlight (selectable, not movable,
// packages/plugin/annotation/src/read/chrome.ts on main and plugins).
/** @type {Flow} */
const dragAnnotFlow = {
  fixture: 'annots',
  start: startAt(STAMP_PAGE, { park: true }),
  async warmup(page) {
    await goTo(page, STAMP_PAGE + 8);
    await settle(page, await now(page), { requireChange: false });
    await findStamp(page, STAMP_PAGE + 8)
      .then((stamp) => dragStamp(page, stamp, 200))
      .catch(warmupFailed(page, 'dragAnnot'));
    await page.mouse.move(PARKED.x, PARKED.y);
  },
  async measure(page, cdp) {
    const stamp = await findStamp(page, STAMP_PAGE);
    const { from } = stamp;
    const before = await cdp.cpuStart();
    const t0 = await now(page);
    const done = await dragStamp(page, stamp, 200);
    const settled = await settle(page, t0, { doneAt: done, requireChange: false });
    const load = await cpu(cdp, before);
    return {
      metrics: { settleMs: settled - t0, ...load },
      answers: { stampBefore: from, stampAfter: await stampBox(page, STAMP_PAGE) },
    };
  },
};

// Three strokes of 60 samples each, right of the text on page index 202:
// zig-zags 170 pt wide at y = 150, 250 and 350 pt from the page top. In
// window px, worked out before the timed window: each point reads the page's
// box.
const strokesOn = (page, pageIndex) =>
  Promise.all(
    [150, 250, 350].map((y) =>
      Promise.all(
        Array.from({ length: 60 }, (_, i) => screenPoint(page, pageIndex, [410 + (170 * i) / 59, y + (i % 2 ? 20 : -20)])),
      ),
    ),
  );

async function drawStrokes(page, strokes) {
  for (const points of strokes) {
    await page.mouse.move(...points[0]);
    await page.mouse.down();
    for (const p of points.slice(1)) await page.mouse.move(...p);
    await page.mouse.up();
  }
}

// The ink tool groups strokes drawn within 800 ms into one annotation
// (`groupStrokesMs` in packages/plugin/annotation/src/tools/definitions.ts on
// both generations), so the three strokes make one ink annotation, added once
// the grouping window closes. As in `scroll`, `settleMs` counts from the last
// pointer move (so it holds the grouping window) and `wallMs` from the first:
// the 180 moves are frame-paced.
/** @type {Flow} */
const drawInkFlow = {
  fixture: 'annots',
  start: startAt(INK_PAGE, { park: true }),
  async warmup(page) {
    const warm = INK_PAGE + 2;
    await goTo(page, warm);
    await settle(page, await now(page), { requireChange: false });
    const count = await page.evaluate((i) => window.__perf.annotationCount(i), warm);
    await page.evaluate(() => window.__perf.execute('annotation:add-ink'));
    await drawStrokes(page, await strokesOn(page, warm));
    await waitFor(page, ([i, n]) => window.__perf.annotationCount(i) > n, [warm, count], 'the warm-up ink', 10_000).catch(
      warmupFailed(page, 'drawInk'),
    );
    await page.mouse.move(PARKED.x, PARKED.y);
  },
  async measure(page, cdp) {
    const countBefore = await page.evaluate((i) => window.__perf.annotationCount(i), INK_PAGE);
    await page.evaluate(() => window.__perf.execute('annotation:add-ink'));
    const count = await watch(page, 'annotationCount', INK_PAGE);
    const strokes = await strokesOn(page, INK_PAGE);
    const before = await cdp.cpuStart();
    const t0 = await now(page);
    await drawStrokes(page, strokes);
    const tLast = await now(page);
    const done = await waitFor(
      page,
      ([id, n]) => window.__perf.read(id) === n + 1,
      [count, countBefore],
      'the ink to be added',
      10_000,
    );
    const settled = await settle(page, t0, { doneAt: done, requireChange: false });
    const load = await cpu(cdp, before);
    const countAfter = await page.evaluate((i) => window.__perf.annotationCount(i), INK_PAGE);
    return { metrics: { settleMs: settled - tLast, wallMs: settled - t0, ...load }, answers: { countBefore, countAfter } };
  },
};

// forms.pdf: field t<p>-1 is the first text field on page index p - 1, its
// widget 240 x 20 pt at x = 170 pt, y = 72 pt from the page top.
const FIELD_PAGE = 50;
const TYPED = 'The quick brown fox jumps over a lazy do'; // 40 characters
const fieldName = (pageIndex) => `t${pageIndex + 1}-1`;

// The field's click point and a watch on its value. Untimed.
async function findField(page, pageIndex) {
  return {
    at: await screenPoint(page, pageIndex, [290, 82]),
    value: await watch(page, 'fieldText', fieldName(pageIndex)),
  };
}

// Clicks into the field, types `text` and presses Tab to leave it, which
// commits the value; resolves once the form reports it.
async function typeInto(page, { at: [x, y], value }, text) {
  await page.mouse.click(x, y);
  await page.keyboard.type(text);
  await page.keyboard.press('Tab');
  return waitFor(page, ([id, t]) => window.__perf.read(id) === t, [value, text], 'the typed value', 10_000);
}

/** @type {Flow} */
const typeFieldFlow = {
  fixture: 'forms',
  start: startAt(FIELD_PAGE, { park: true }),
  async warmup(page) {
    await goTo(page, FIELD_PAGE + 1);
    await settle(page, await now(page), { requireChange: false });
    await findField(page, FIELD_PAGE + 1)
      .then((field) => typeInto(page, field, TYPED.slice(0, 10)))
      .catch(warmupFailed(page, 'typeField'));
    await page.mouse.move(PARKED.x, PARKED.y);
  },
  async measure(page, cdp) {
    const field = await findField(page, FIELD_PAGE);
    const before = await cdp.cpuStart();
    const t0 = await now(page);
    const done = await typeInto(page, field, TYPED);
    const settled = await settle(page, t0, { doneAt: done, requireChange: false });
    const load = await cpu(cdp, before);
    return {
      metrics: { settleMs: settled - t0, ...load },
      answers: { value: await page.evaluate((n) => window.__perf.fieldText(n), fieldName(FIELD_PAGE)) },
    };
  },
};

// Ten highlights, one per line on lines 11 to 20 of the page, each drawn with
// the highlight tool and waited for; the edits `undo` takes back.
async function highlightLines(page, pageIndex) {
  const start = await page.evaluate((i) => window.__perf.annotationCount(i), pageIndex);
  const count = await watch(page, 'annotationCount', pageIndex);
  await page.evaluate(() => window.__perf.execute('annotation:add-highlight'));
  for (let n = 11; n <= 20; n++) {
    // Line n's baseline is 730 - 13 (n - 1) pt up from the bottom.
    const y = (LETTER.height - (730 - 13 * (n - 1) + 3)) / LETTER.height;
    await drag(page, pageIndex, { from: [0.1, y], to: [0.55, y] });
    await waitFor(
      page,
      ([id, k]) => window.__perf.read(id) === k,
      [count, start + n - 10],
      `highlight ${n - 10} of 10`,
      10_000,
    );
  }
  await page.evaluate(() => window.__perf.execute('pointer:toggle'));
  await page.mouse.move(PARKED.x, PARKED.y);
}

// `watched` is a watch on the page's count (`watch`), made before the timed window.
async function undoAll(page, watched, count) {
  for (let i = 0; i < 10; i++) await page.evaluate(() => window.__perf.execute('history:undo'));
  return waitFor(page, ([id, n]) => window.__perf.read(id) === n, [watched, count], 'ten undos', 10_000);
}

// Needs a history plugin: main 2516e2786 has none (its `history:undo` is a
// disabled no-op), so the runner leaves `undo` out on arms without one.
/** @type {Flow} */
const undoFlow = {
  fixture: 'annots',
  needs: ['history'],
  start: startAt(UNDO_PAGE, { park: true }),
  async warmup(page) {
    const warm = UNDO_PAGE + 1;
    await goTo(page, warm);
    await settle(page, await now(page), { requireChange: false });
    const count = await page.evaluate((i) => window.__perf.annotationCount(i), warm);
    await highlightLines(page, warm);
    await watch(page, 'annotationCount', warm)
      .then((watched) => undoAll(page, watched, count))
      .catch(warmupFailed(page, 'undo'));
  },
  async measure(page, cdp) {
    const countBefore = await page.evaluate((i) => window.__perf.annotationCount(i), UNDO_PAGE);
    // Not timed: the ten edits.
    await highlightLines(page, UNDO_PAGE);
    await settle(page, await now(page), { requireChange: false });
    const countEdited = await page.evaluate((i) => window.__perf.annotationCount(i), UNDO_PAGE);
    const watched = await watch(page, 'annotationCount', UNDO_PAGE);
    const before = await cdp.cpuStart();
    const t0 = await now(page);
    const done = await undoAll(page, watched, countBefore);
    const settled = await settle(page, t0, { doneAt: done, requireChange: false });
    const load = await cpu(cdp, before);
    const countAfter = await page.evaluate((i) => window.__perf.annotationCount(i), UNDO_PAGE);
    return { metrics: { settleMs: settled - t0, ...load }, answers: { countBefore, countEdited, countAfter } };
  },
};

/** @type {Record<string, Flow>} */
const zoomFlow = (fixture, pageIndex) => ({
  fixture,
  start: startAt(pageIndex),
  async warmup(page) {
    for (const z of [1.5, 3]) {
      const t = await now(page);
      await zoomTo(page, z);
      await settle(page, t, { requireChange: false });
    }
  },
  async measure(page, cdp) {
    const before = await cdp.cpuStart();
    const steps = {};
    for (const [z, key, requireChange] of [
      [2, 'zoom2SettleMs', true],
      [4, 'zoom4SettleMs', true],
      // Can be served from the starting state's tiles, so it's labeled
      // apart and never read as a cold render.
      [1, 'backTo1SettleMs', false],
    ]) {
      const t = await now(page);
      const done = await zoomTo(page, z);
      steps[key] = (await settle(page, t, { doneAt: done, requireChange })) - t;
    }
    return {
      metrics: { settleMs: steps.zoom2SettleMs + steps.zoom4SettleMs, ...steps, ...(await cpu(cdp, before)) },
    };
  },
});

const selectFlow = (name, fixture, pageIndex, region) => ({
  fixture,
  start: startAt(pageIndex, { park: true }),
  async warmup(page) {
    await goTo(page, pageIndex + 1);
    await settle(page, await now(page), { requireChange: false });
    await drag(page, pageIndex + 1, region);
    await page
      .evaluate(() => window.__perf.readSelection())
      .then((text) => {
        if (!text) throw new Error('the drag selected no text');
      })
      .catch(warmupFailed(page, name));
    await page.mouse.move(PARKED.x, PARKED.y);
  },
  async measure(page, cdp) {
    const ends = await dragEnds(page, pageIndex, region);
    const before = await cdp.cpuStart();
    // Before the pointer first enters the page: on main the selection
    // handler's onHover already fetches text geometry.
    const t0 = await now(page);
    await dragAlong(page, ends);
    const { text, at } = await readSelection(page);
    const settled = await settle(page, t0, { doneAt: at, requireChange: false });
    return { metrics: { settleMs: settled - t0, ...(await cpu(cdp, before)) }, answers: { selection: text } };
  },
});

const annotateFlow = (name, fixture, pageIndex, region) => ({
  fixture,
  start: startAt(pageIndex, { park: true }),
  async warmup(page) {
    const warm = pageIndex + 1;
    await goTo(page, warm);
    await settle(page, await now(page), { requireChange: false });
    const count = await page.evaluate((i) => window.__perf.annotationCount(i), warm);
    await page.evaluate(() => window.__perf.execute('annotation:add-highlight'));
    await drag(page, warm, region);
    await waitFor(
      page,
      ([i, n]) => window.__perf.annotationCount(i) > n,
      [warm, count],
      'the warm-up highlight',
      10_000,
    ).catch(warmupFailed(page, name));
    await page.mouse.move(PARKED.x, PARKED.y);
  },
  async measure(page, cdp) {
    const countBefore = await page.evaluate((i) => window.__perf.annotationCount(i), pageIndex);
    await page.evaluate(() => window.__perf.execute('annotation:add-highlight'));
    const count = await watch(page, 'annotationCount', pageIndex);
    const ends = await dragEnds(page, pageIndex, region);
    const before = await cdp.cpuStart();
    // After the tool is on, before the pointer first enters the page.
    const t0 = await now(page);
    await dragAlong(page, ends);
    const done = await waitFor(
      page,
      ([id, n]) => window.__perf.read(id) === n + 1,
      [count, countBefore],
      'the highlight to be added',
      10_000,
    );
    const settled = await settle(page, t0, { doneAt: done, requireChange: false });
    const load = await cpu(cdp, before);
    const countAfter = await page.evaluate((i) => window.__perf.annotationCount(i), pageIndex);
    return {
      metrics: { settleMs: settled - t0, ...load },
      answers: { countBefore, countAfter },
    };
  },
});

export const FLOWS = {
  // Measured by the runner's open path (openFlow below), not start/measure.
  open: { fixtures: ['ebook', 'large'] },

  scroll: scrollFlow(0, 200),
  // The same scroll from page index 300, warmed up at page index 100: the
  // middle of the document.
  scrollMid: scrollFlow(300, 100),

  // The same 60 wheels on a document just opened, with no warm-up, so every
  // page the scroll reaches is reached for the first time: its scale and its
  // tiles load during the measured window. A first pass through a document
  // does that; `scroll`, after its warm-up, doesn't time it.
  coldScroll: {
    fixture: 'large',
    start: (page) => FLOWS.scroll.start(page),
    measure: (page, cdp) => FLOWS.scroll.measure(page, cdp),
  },

  jump: {
    fixture: 'large',
    start: startAt(0),
    async warmup(page) {
      await goTo(page, 200);
      await settle(page, await now(page), { requireChange: false });
    },
    async measure(page, cdp) {
      const before = await cdp.cpuStart();
      const t0 = await now(page);
      const done = await goTo(page, 300);
      const settled = await settle(page, t0, { doneAt: done });
      return { metrics: { settleMs: settled - t0, ...(await cpu(cdp, before)) } };
    },
  },

  zoom: zoomFlow('ebook', 0),
  // The same zoom steps in the middle of large.pdf.
  zoomLarge: zoomFlow('large', 200),


  select: selectFlow('select', 'ebook', 1, SELECT_DRAG),
  // Four lines on page index 200 of large.pdf, a 400-page document.
  selectLarge: selectFlow('selectLarge', 'large', 200, LARGE_SELECT_DRAG),


  search: {
    fixture: 'ebook',
    start: startAt(0),
    // No warm-up: a warm-up search fills every page's text cache.
    async measure(page, cdp) {
      const before = await cdp.cpuStart();
      const { ms, hits } = await page.evaluate(async () => {
        const t0 = performance.now();
        const hits = await window.__perf.search('the');
        return { ms: performance.now() - t0, hits };
      });
      return { metrics: { searchMs: ms, ...(await cpu(cdp, before)) }, answers: { hits } };
    },
  },
  // The same cold search on large.pdf: 400 pages of glyph geometry read in
  // the worker, where ebook.pdf's 4 pages barely reach the reader.
  searchLarge: {
    fixture: 'large',
    start: (page) => FLOWS.search.start(page),
    measure: (page, cdp) => FLOWS.search.measure(page, cdp),
  },


  annotate: annotateFlow('annotate', 'ebook', 1, HIGHLIGHT_DRAG),
  // One line on page index 200 of large.pdf.
  annotateLarge: annotateFlow('annotateLarge', 'large', 200, LARGE_HIGHLIGHT_DRAG),

  hover: hoverFlow,
  dragAnnot: dragAnnotFlow,
  drawInk: drawInkFlow,
  typeField: typeFieldFlow,
  undo: undoFlow,


  soak: {
    fixture: 'ebook',
    start: startAt(0),
    // No warm-up; cycles 1 to 4 are its warm-up and aren't reported.
    async measure(page, cdp) {
      const last = (await page.evaluate(() => window.__perf.pageCount())) - 1;
      const totals = [];
      const diagnostics = {};
      // Cached tiles can make a step change nothing, so no step requires one.
      const step = async (action) => {
        const t = await now(page);
        const done = await action();
        await settle(page, t, { doneAt: done, requireChange: false });
      };
      for (let cycle = 1; cycle <= 30; cycle++) {
        await step(() => goTo(page, last));
        await step(() => zoomTo(page, 3));
        await step(() => goTo(page, 0));
        await step(() => zoomTo(page, 1));
        if (cycle >= 5) totals.push((await cdp.memory()).total);
        if (cycle === 5 || cycle === 30) {
          diagnostics[`cycle${cycle}`] = await page.evaluate(() => window.__perf.kernelCounts());
        }
      }
      return {
        metrics: {
          heapCycle5Bytes: totals[0],
          heapCycle30Bytes: totals[totals.length - 1],
          heapSoakSlopeBytes: slope(totals),
        },
        diagnostics: diagnostics.cycle5 ? diagnostics : undefined,
      };
    },
  },
};

export const FLOW_ORDER = Object.keys(FLOWS);

// Whether an arm whose viewer has `capabilities` (window.__perf.capabilities,
// from entry/api-v*.ts) runs `flow`: it has everything the flow `needs`.
export const armRuns = (flow, capabilities) =>
  (FLOWS[flow]?.needs ?? []).every((need) => capabilities.includes(need));

// Cold open on a page the runner prepared: parked on /__blank with CDP
// attached and the throttle on.
export async function openFlow(page, cdp, url) {
  const before = await cdp.cpuStart();
  await page.goto(url);
  await page.waitForFunction(() => window.__perf, null, { timeout: 60_000 });
  await page.evaluate(() => window.__perf.opened);
  await page.waitForFunction(() => window.__probe.firstPaintAt !== null, null, { timeout: 30_000 });
  // performance.now() is relative to navigation start, so 0 is the start.
  const settled = await settle(page, 0);
  const marks = await page.evaluate(() => ({
    readyMs: performance.getEntriesByName('perf:ready')[0].startTime,
    openedMs: performance.getEntriesByName('perf:opened')[0].startTime,
    firstPaintMs: window.__probe.firstPaintAt,
  }));
  const load = await cpu(cdp, before);
  const memory = await cdp.memory();
  return {
    metrics: {
      ...marks,
      settledMs: settled,
      ...load,
      heapAfterOpenBytes: memory.total,
      workerHeapAfterOpenBytes: memory.worker,
    },
    memorySource: memory.source,
  };
}

const probePath = new URL('./probe.js', import.meta.url).pathname;

// What a sample's line in samples.jsonl records of runFlow's result.
// `warmupFailed` is there only when the warm-up failed, so a run with none
// writes the same lines as before it existed.
export const sampleFields = (r) => ({
  metrics: r.metrics,
  ...(r.diagnostics ? { diagnostics: r.diagnostics } : {}),
  ...(r.warmupFailed ? { warmupFailed: r.warmupFailed } : {}),
});

// One flow on one arm, in a fresh context: park on /__blank, attach CDP and
// throttle there (same origin, so the viewer's navigation keeps the process
// and the throttle), then open and run. `inspect(page)`, when given, runs
// after the measurement and its answer comes back as `inspected`.
// Rejects when `work` has not settled after `ms`. The caller still has to stop
// the work itself; runFlow does that by closing the browser context.
export function withDeadline(work, ms, what) {
  let timer;
  const late = new Promise((_, reject) => {
    timer = setTimeout(() => reject(Object.assign(new Error(`${what} did not finish in ${Math.round(ms / 1000)} s`), { deadline: true })), ms);
  });
  return Promise.race([work, late]).finally(() => clearTimeout(timer));
}

// A viewer that never settles would otherwise hold the whole run: a page
// evaluate has no timeout of its own. PERF_FLOW_TIMEOUT is in seconds.
const flowTimeoutMs = () => Number(process.env.PERF_FLOW_TIMEOUT ?? 300) * 1000;

export async function runFlow(run, options) {
  const context = await run.browser.newContext({ viewport: options.viewport, deviceScaleFactor: options.deviceScaleFactor });
  let stuck = false;
  try {
    const work = flowIn(context, run, options);
    // Closing the context below rejects whatever the flow still awaits.
    work.catch(() => {});
    return await withDeadline(work, flowTimeoutMs(), `${options.flow}/${options.fixture}`);
  } catch (error) {
    stuck = error.deadline === true;
    throw error;
  } finally {
    // A renderer stuck in a loop can hold the close as well, and outlives it:
    // the next flow gets a new browser.
    await withDeadline(context.close(), 30_000, 'closing the page').catch(() => {});
    if (stuck) await run.restart();
  }
}

async function flowIn(context, run, { origin, flow, fixture, rate, inspect }) {
  await context.addInitScript({ path: probePath });
  const page = await context.newPage();
  if (process.env.PERF_DEBUG) console.error('[flow]', flow, fixture, origin);
  await page.goto(`${origin}/__blank`);
  const cdp = await openCdp(page, run.cdp);
  try {
    await cdp.throttle(rate);
    if (process.env.PERF_DEBUG) console.error('[flow] throttled');
    const url = `${origin}/?doc=${fixture}`;
    let result;
    if (flow === 'open') {
      result = await openFlow(page, cdp, url);
    } else {
      const f = FLOWS[flow];
      await page.goto(url);
      await page.waitForFunction(() => window.__perf, null, { timeout: 60_000 });
      await page.evaluate(() => window.__perf.opened);
      await settle(page, 0);
      await f.start(page);
      if (f.warmup) {
        await f.warmup(page, cdp);
        await f.start(page);
      }
      await page.evaluate(() => window.__probe.reset());
      result = await f.measure(page, cdp);
    }
    const warmup = warmupFailureOn(page);
    const inspected = inspect ? await inspect(page) : undefined;
    return {
      ...result,
      ...(warmup ? { warmupFailed: warmup } : {}),
      inspected,
      workerThrottled: rate === 1 ? null : cdp.workerThrottled,
    };
  } finally {
    await cdp.close().catch(() => {});
  }
}
