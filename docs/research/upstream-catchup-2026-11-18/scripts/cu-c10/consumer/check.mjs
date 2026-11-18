// Opens the built consumer in headless Chromium with a URL src and a bytes src.
import { chromium } from 'playwright';
const out = {};
const browser = await chromium.launch();
for (const mode of ['url', 'bytes']) {
  const page = await browser.newPage();
  const errors = [], failed = [], http = [];
  page.on('console', (m) => m.type() === 'error' && errors.push(m.text()));
  page.on('pageerror', (e) => errors.push(String(e)));
  page.on('requestfailed', (r) => failed.push(r.url()));
  page.on('response', (r) => r.status() >= 400 && http.push(`${r.status()} ${r.url()}`));
  await page.goto(`http://127.0.0.1:4173/?src=${mode}`);
  await page.waitForFunction(() => window.__viewer && window.__viewer.documents.list().length > 0, null, { timeout: 60000 });
  const r = await page.evaluate(async () => {
    const v = window.__viewer;
    const wait = (ms) => new Promise((r) => setTimeout(r, ms));
    let docs, scoped, docInfo;
    for (let i = 0; i < 120; i++) {
      docs = v.documents.list();
      const d = docs[0]; const id = typeof d === 'string' ? d : (d.id ?? d.documentId);
      scoped = v.forDocument(String(id));
      if (!String(id).startsWith('pending:') && scoped.tryGet('annotation')) { docInfo = JSON.stringify(d).slice(0, 200); break; }
      await wait(250);
    }
    // a painted page: any canvas or img in the viewer with non-white pixels
    let painted = false;
    for (let i = 0; i < 60 && !painted; i++) {
      await wait(500);
      const root = document.querySelector('embedpdf-viewer');
      const scope = root?.shadowRoot ?? document;
      for (const img of scope.querySelectorAll('img, canvas')) {
        const w = img.naturalWidth ?? img.width, h = img.naturalHeight ?? img.height;
        if (!w || !h) continue;
        const c = document.createElement('canvas'); c.width = Math.min(w, 200); c.height = Math.min(h, 200);
        const ctx = c.getContext('2d'); try { ctx.drawImage(img, 0, 0, c.width, c.height); } catch { continue; }
        const d = ctx.getImageData(0, 0, c.width, c.height).data;
        for (let k = 0; k < d.length; k += 4) if (d[k] < 200 || d[k + 1] < 200 || d[k + 2] < 200) { painted = true; break; }
        if (painted) break;
      }
    }
    const annotation = scoped.get('annotation');
    const { annotation: rec } = await annotation.create(0, { subtype: 'square', rect: { x: 10, y: 10, width: 50, height: 50 } });
    const readBack = annotation.get(rec.ref);
    const search = scoped.get('search');
    const result = await search.search({ text: 'e' });
    return { docInfo, documents: docs.length, painted, created: rec.subtype, readBack: readBack?.subtype ?? null,
      hits: search.getHitCount(), resultKeys: Object.keys(result ?? {}), buildInfo: window.__buildInfo };
  });
  await page.waitForTimeout(1000);
  out[mode] = { ...r, errors, failed, http };
  await page.close();
}
await browser.close();
console.log(JSON.stringify(out, null, 2));
