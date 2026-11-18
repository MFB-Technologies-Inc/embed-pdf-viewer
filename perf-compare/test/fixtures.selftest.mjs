// Needs Chromium and a built main arm: main's TypeScript engine reads
// annots.pdf and forms.pdf as fixtures/generate.mjs wrote them.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { launchBrowser } from '../lib/cdp.mjs';
import { serveArms } from '../lib/serve.mjs';

test('main lists 1,200 annotations in annots.pdf and 1,000 fields in forms.pdf', async () => {
  const dist = fileURLToPath(new URL('../.work/dist/main', import.meta.url));
  const { urls, close } = await serveArms({ main: dist }, { basePort: 5395 });
  const run = await launchBrowser({ port: 9336 });
  const open = async (doc) => {
    const page = await run.browser.newPage();
    await page.goto(`${urls.main}/?doc=${doc}`);
    await page.waitForFunction(() => window.__perf, null, { timeout: 60_000 });
    await page.evaluate(() => window.__perf.opened);
    return page;
  };
  try {
    const annots = await open('annots');
    await annots.waitForFunction(() => window.__perf.annotationStatus() === 'ready');
    const kinds = await annots.evaluate(() => {
      const count = {};
      for (let i = 0; i < window.__perf.pageCount(); i++) {
        for (const a of window.__perf.annotations(i)) count[a.subtype] = (count[a.subtype] ?? 0) + 1;
      }
      return { total: window.__perf.annotationTotal(), count, hoverPage: window.__perf.annotations(200).length };
    });
    assert.deepEqual(kinds, { total: 1200, count: { highlight: 1000, ink: 150, stamp: 50 }, hoverPage: 20 });

    const forms = await open('forms');
    await forms.waitForFunction(() => window.__perf.formStatus() === 'ready');
    assert.equal(await forms.evaluate(() => window.__perf.fieldCount()), 1000);
    assert.equal(await forms.evaluate(() => window.__perf.pageCount()), 100);
  } finally {
    await run.close();
    await close();
  }
});
