// Needs Chromium and a built main arm.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { launchBrowser } from '../lib/cdp.mjs';
import { FLOWS } from '../lib/flows.mjs';
import { serveArms } from '../lib/serve.mjs';
const probePath = fileURLToPath(new URL('../lib/probe.js', import.meta.url));

test('start, warmup, start puts every flow back in its starting state', async () => {
  const dist = fileURLToPath(new URL('../.work/dist/main', import.meta.url));
  const { urls, close } = await serveArms({ main: dist }, { basePort: 5390 });
  const run = await launchBrowser({ port: 9335 });
  const starting = { scroll: 0, jump: 0, zoom: 0, select: 1, search: 0, annotate: 1, soak: 0 };
  try {
    for (const [name, pageIndex] of Object.entries(starting)) {
      const flow = FLOWS[name];
      const context = await run.browser.newContext({
        viewport: { width: 1280, height: 800 },
        deviceScaleFactor: 2,
      });
      await context.addInitScript({ path: probePath });
      const page = await context.newPage();
      await page.goto(`${urls.main}/?doc=${flow.fixture}`);
      await page.waitForFunction(() => window.__perf);
      await page.evaluate(() => window.__perf.opened);
      await flow.start(page);
      if (flow.warmup) await flow.warmup(page, null);
      await flow.start(page);
      const state = await page.evaluate(() => ({
        page: window.__perf.currentPageIndex(),
        zoom: window.__perf.zoomLevel(),
      }));
      assert.deepEqual(state, { page: pageIndex, zoom: 1 }, name);
      await context.close();
    }
  } finally {
    await run.close();
    await close();
  }
});
