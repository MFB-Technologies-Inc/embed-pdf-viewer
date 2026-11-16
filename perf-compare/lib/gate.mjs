// The fairness gate: what "the same answer" means. Every arm must give the
// baseline arm's answers before any of its timings count.
import { createHash } from 'node:crypto';
import { armRuns, FLOWS, runFlow } from './flows.mjs';

const sha256 = (buf) => createHash('sha256').update(buf).digest('hex');
// Arms on different engine wasm may differ by this share of pixels: the
// Rust-core payload links the same PDFium fork, but a relink can move
// anti-aliasing by a pixel.
const PIXEL_TOLERANCE = 0.01;

// The upstream catch-up (main ed064e0c8, plugins 2aaa8f841) changed how
// selected text joins its lines: "commercial \nPDF" where main 2516e2786 gives
// "commercial\r\nPDF". The words are the same, so a selection is compared with
// each line break, and the spaces before it, read as one "\n". gate.json keeps
// every arm's text as it came.
const NORMALIZE = {
  selection: (text) => (typeof text === 'string' ? text.replace(/[ \t]*\r?\n/g, '\n') : text),
};

export function sameAnswer(key, got, reference) {
  const normalize = NORMALIZE[key] ?? ((value) => value);
  return JSON.stringify(normalize(got)) === JSON.stringify(normalize(reference));
}

// Page index 0's rendered bitmap at zoom 1, search cleared: the engine's own
// output, read back from the full-page image through a canvas. A compositor
// screenshot isn't usable here: two identical builds differed in 0.01% of
// its pixels.
async function screenshot(page) {
  await page.evaluate(() => window.__perf.clearSearch());
  await page.evaluate(() => window.__perf.goToPageIndex(0));
  await page.evaluate(() => window.__probe.settledAt(performance.now(), { requireChange: false }));
  const dataUrl = await page.evaluate(() => {
    const box = window.__perf.pageBox(0);
    const imgs = [...document.querySelector('embedpdf-viewer').shadowRoot.querySelectorAll('img[draggable="false"]')];
    const near = (a, b) => Math.abs(a - b) <= 1;
    const img = imgs.find((i) => {
      const r = i.getBoundingClientRect();
      return near(r.x, box.x) && near(r.y, box.y) && near(r.width, box.width) && near(r.height, box.height);
    });
    if (!img) throw new Error('no full-page image for page 1');
    const canvas = document.createElement('canvas');
    canvas.width = img.naturalWidth;
    canvas.height = img.naturalHeight;
    canvas.getContext('2d').drawImage(img, 0, 0);
    return canvas.toDataURL('image/png');
  });
  return Buffer.from(dataUrl.slice(dataUrl.indexOf(',') + 1), 'base64');
}

async function differingShare(browser, a, b) {
  const page = await browser.newPage();
  try {
    return await page.evaluate(
      async ([x, y]) => {
        const load = async (b64) => createImageBitmap(await (await fetch(`data:image/png;base64,${b64}`)).blob());
        const [ia, ib] = await Promise.all([load(x), load(y)]);
        if (ia.width !== ib.width || ia.height !== ib.height) return 1;
        const pixels = (img) => {
          const c = new OffscreenCanvas(img.width, img.height);
          const ctx = c.getContext('2d');
          ctx.drawImage(img, 0, 0);
          return ctx.getImageData(0, 0, img.width, img.height).data;
        };
        const [pa, pb] = [pixels(ia), pixels(ib)];
        let differ = 0;
        for (let i = 0; i < pa.length; i += 4) {
          if (pa[i] !== pb[i] || pa[i + 1] !== pb[i + 1] || pa[i + 2] !== pb[i + 2] || pa[i + 3] !== pb[i + 3]) differ++;
        }
        return differ / (pa.length / 4);
      },
      [a.toString('base64'), b.toString('base64')],
    );
  } finally {
    await page.close();
  }
}

// The flows on annots.pdf and forms.pdf, run once each. Each answer is kept
// under its flow's name, so an arm that doesn't run a flow (lib/flows.mjs
// `needs`) has no answer for it.
const FIXTURE_FLOWS = ['hover', 'dragAnnot', 'drawInk', 'typeField', 'undo'];

// Every annotation and field is read once the plugin says it's ready.
const fixtureCounts = (read) => async (page) => {
  await page.waitForFunction((s) => window.__perf[s]() === 'ready', read.status, { timeout: 30_000 });
  return page.evaluate((c) => window.__perf[c](), read.count);
};

async function answersFor(run, arm, origin, env) {
  const opts = { origin, rate: 1, ...env };
  const large = await runFlow(run, {
    ...opts,
    flow: 'open',
    fixture: 'large',
    inspect: (page) => page.evaluate(() => window.__perf.pageCount()),
  });
  const search = await runFlow(run, {
    ...opts,
    flow: 'search',
    fixture: 'ebook',
    inspect: async (page) => ({
      pageCount: await page.evaluate(() => window.__perf.pageCount()),
      arm: await page.evaluate(() => window.__perf.arm),
      kernelCounts: await page.evaluate(() => window.__perf.kernelCounts()),
      capabilities: await page.evaluate(() => window.__perf.capabilities),
      png: await screenshot(page),
    }),
  });
  const select = await runFlow(run, { ...opts, flow: 'select', fixture: 'ebook' });
  const annotate = await runFlow(run, { ...opts, flow: 'annotate', fixture: 'ebook' });
  const annots = await runFlow(run, {
    ...opts,
    flow: 'open',
    fixture: 'annots',
    inspect: fixtureCounts({ status: 'annotationStatus', count: 'annotationTotal' }),
  });
  const forms = await runFlow(run, {
    ...opts,
    flow: 'open',
    fixture: 'forms',
    inspect: fixtureCounts({ status: 'formStatus', count: 'fieldCount' }),
  });
  const { capabilities } = search.inspected;
  const flowAnswers = {};
  const skipped = [];
  for (const flow of FIXTURE_FLOWS) {
    if (!armRuns(flow, capabilities)) {
      skipped.push(flow);
      continue;
    }
    flowAnswers[flow] = (await runFlow(run, { ...opts, flow, fixture: FLOWS[flow].fixture })).answers;
  }
  return {
    identity: { arm: search.inspected.arm, kernelMounted: search.inspected.kernelCounts !== null },
    capabilities,
    skipped,
    answers: {
      largePageCount: large.inspected,
      ebookPageCount: search.inspected.pageCount,
      searchHits: search.answers.hits,
      selection: select.answers.selection,
      annotationsBefore: annotate.answers.countBefore,
      annotationsAfter: annotate.answers.countAfter,
      annotsAnnotations: annots.inspected,
      formsFields: forms.inspected,
      ...flowAnswers,
    },
    png: search.inspected.png,
  };
}

// Each answer is compared with the reference arm's: the baseline's when the
// baseline has that answer, else the first arm in `order` that has it. An arm
// may lack an answer only for a flow it skipped (`skipped`, flow names, which
// are also the keys of their answers). `collected` is { [armId]: { answers,
// skipped } }; returns the reasons `id` fails, empty when it passes.
export function answerReasons(id, collected, order) {
  const reasons = [];
  const keys = [...new Set(order.flatMap((a) => Object.keys(collected[a]?.answers ?? {})))];
  const got = collected[id];
  for (const key of keys) {
    const refId = order.find((a) => collected[a] && key in collected[a].answers);
    if (!(key in got.answers)) {
      if (!got.skipped?.includes(key)) reasons.push(`${key}: no answer where ${refId} has one`);
      continue;
    }
    const value = collected[refId].answers[key];
    if (!sameAnswer(key, got.answers[key], value)) {
      reasons.push(`${key}: ${JSON.stringify(got.answers[key])} where ${refId} has ${JSON.stringify(value)}`);
    }
  }
  return reasons;
}

// Returns { [armId]: { pass: true, answers, capabilities, skipped, screenshotSha256 } | { pass: false, reason } }.
export async function runGate(run, { config, arms, urls, stamps, env }) {
  const results = {};
  const collected = {};
  for (const id of arms) {
    try {
      collected[id] = await answersFor(run, id, urls[id], env);
    } catch (error) {
      results[id] = { pass: false, reason: `harness: ${String(error.message ?? error).split('\n')[0]}` };
    }
  }
  const reference = collected[config.baseline] ?? Object.values(collected)[0];
  const referenceId = Object.keys(collected).find((id) => collected[id] === reference);
  for (const [id, got] of Object.entries(collected)) {
    const arm = { id, ...config.arms[id] };
    const reasons = [];
    const expectedArm = { id, kernel: arm.kernel, rustReads: arm.rustReads, dedupeCompat: arm.dedupeCompat === true };
    if (JSON.stringify(got.identity.arm) !== JSON.stringify(expectedArm)) {
      reasons.push(`the page reports ${JSON.stringify(got.identity.arm)}, arms.json says ${JSON.stringify(expectedArm)}`);
    }
    if (arm.kernel === 'rust' && !got.identity.kernelMounted) reasons.push('the Rust kernel did not mount');
    // The viewer can't ask the worker which reader served a page, so the
    // stamp's verify-rust-core result stands in for impl-rust.
    if (arm.rustReads && !stamps[arm.worktree].rustCore) reasons.push('the payload has no verified Rust core');
    reasons.push(...answerReasons(id, collected, [referenceId, ...arms.filter((a) => a !== referenceId)]));
    const sameWasm = stamps[arm.worktree].engineWasmSha256 === stamps[config.arms[referenceId].worktree].engineWasmSha256;
    let pixelShare = 0;
    if (sha256(got.png) !== sha256(reference.png)) {
      pixelShare = await differingShare(run.browser, got.png, reference.png);
      if (sameWasm) reasons.push(`page 1 differs from ${referenceId} on the same engine wasm (${(pixelShare * 100).toFixed(2)}% of pixels)`);
      else if (pixelShare > PIXEL_TOLERANCE) {
        reasons.push(`page 1 differs from ${referenceId} in ${(pixelShare * 100).toFixed(2)}% of pixels`);
      }
    }
    results[id] = reasons.length
      ? { pass: false, reason: reasons.join('; ') }
      : {
          pass: true,
          answers: got.answers,
          capabilities: got.capabilities,
          skipped: got.skipped,
          screenshotSha256: sha256(got.png), pixelShareVsReference: pixelShare };
  }
  return Object.fromEntries(arms.map((id) => [id, results[id]]));
}
