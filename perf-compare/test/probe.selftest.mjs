// Synthetic viewer checks: needs Chromium, but no built viewer arm.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { crc32, deflateSync } from 'node:zlib';
import { fileURLToPath } from 'node:url';
import { launchBrowser } from '../lib/cdp.mjs';

const probePath = fileURLToPath(new URL('../lib/probe.js', import.meta.url));

// A solid-colour RGBA PNG, big enough that decoding takes a moment.
function png(width, height) {
  const chunk = (type, data) => {
    const len = Buffer.alloc(4);
    len.writeUInt32BE(data.length);
    const body = Buffer.concat([Buffer.from(type), data]);
    const crc = Buffer.alloc(4);
    crc.writeUInt32BE(crc32(body));
    return Buffer.concat([len, body, crc]);
  };
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr.set([8, 6, 0, 0, 0], 8);
  const row = Buffer.alloc(1 + width * 4);
  for (let x = 0; x < width; x++) row.set([x % 256, 80, 160, 255], 1 + x * 4);
  const raw = Buffer.concat(Array.from({ length: height }, () => row));
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', deflateSync(raw)),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

const FAKE = `<!doctype html><body style="margin:0">
<embedpdf-viewer></embedpdf-viewer>
<script>
  const root = document.querySelector('embedpdf-viewer').attachShadow({ mode: 'open' });
  const img = document.createElement('img');
  img.setAttribute('draggable', 'false');
  img.style.cssText = 'display:block;width:400px;height:300px';
  img.src = 'data:image/png;base64,${png(4, 4).toString('base64')}';
  root.append(img);
  window.__img = img;
</script>`;

async function fakeServer() {
  const small = png(4, 4);
  const big = png(3000, 3000);
  const server = createServer((req, res) => {
    if (req.url === '/') return res.writeHead(200, { 'Content-Type': 'text/html' }).end(FAKE);
    if (req.url === '/small.png' || req.url.startsWith('/swap')) {
      return res.writeHead(200, { 'Content-Type': 'image/png' }).end(small);
    }
    if (req.url.startsWith('/slow')) {
      setTimeout(() => res.writeHead(200, { 'Content-Type': 'image/png' }).end(big), 300);
      return;
    }
    res.writeHead(404).end();
  });
  await new Promise((r) => server.listen(5389, '127.0.0.1', r));
  return { url: 'http://127.0.0.1:5389', close: () => new Promise((r) => server.close(r)) };
}

test('the settle rule waits for the change and the decode, and refuses no change', async () => {
  const server = await fakeServer();
  const run = await launchBrowser({ port: 9334 });
  try {
    const context = await run.browser.newContext();
    await context.addInitScript({ path: probePath });
    const page = await context.newPage();
    await page.goto(server.url);
    await page.waitForFunction(() => window.__img.complete && window.__probe.firstPaintAt !== null);

    // 1. A src swap 200 ms after sinceMs settles no earlier than the swap.
    const one = await page.evaluate(async () => {
      const since = performance.now();
      let swappedAt = 0;
      setTimeout(() => {
        swappedAt = performance.now();
        window.__img.src = '/swap.png?1';
      }, 200);
      const settled = await window.__probe.settledAt(since);
      return { since, swappedAt, settled };
    });
    assert.ok(one.settled >= one.swappedAt, JSON.stringify(one));
    assert.ok(one.settled - one.since >= 190, JSON.stringify(one));

    // 2. An image whose bytes arrive 300 ms after the swap settles no earlier
    //    than its decode.
    const two = await page.evaluate(async () => {
      const since = performance.now();
      let decodedAt = 0;
      window.__img.addEventListener(
        'load',
        () => window.__img.decode().then(() => (decodedAt = performance.now())),
        {
          once: true,
        },
      );
      window.__img.src = '/slow.png?2';
      const settled = await window.__probe.settledAt(since);
      return { since, decodedAt, settled };
    });
    assert.ok(two.decodedAt > 0 && two.settled >= two.decodedAt - 1, JSON.stringify(two));
    assert.ok(two.settled - two.since >= 290, JSON.stringify(two));

    // Cached images entering a clipped viewport are changes even without
    // src/load events; offscreen decodes must not extend that window.
    const visibility = await page.evaluate(async () => {
      const root = document.querySelector('embedpdf-viewer').shadowRoot;
      const clip = document.createElement('div');
      clip.style.cssText =
        'position:absolute;top:310px;left:0;width:200px;height:100px;overflow:hidden;border:20px solid black';
      const img = document.createElement('img');
      img.draggable = false;
      img.style.cssText = 'display:block;width:100px;height:100px;margin-top:200px';
      img.src = '/small.png';
      const borderImage = document.createElement('img');
      borderImage.draggable = false;
      borderImage.src = '/small.png';
      borderImage.style.cssText = 'position:absolute;left:-15px;top:0;width:10px;height:10px';
      clip.append(img, borderImage);
      root.append(clip);
      await img.decode();
      await new Promise((r) => requestAnimationFrame(r));
      window.__probe.reset();
      const since = performance.now();
      let resolved = false;
      const pending = window.__probe.settledAt(since).then((t) => {
        resolved = true;
        return t;
      });
      img.src = '/swap.png?offscreen';
      borderImage.src = '/swap.png?under-border';
      await img.decode();
      await new Promise((r) => setTimeout(r, 350));
      const premature = resolved;
      const enteredAt = performance.now();
      clip.scrollTop = 200;
      const entered = await pending;
      const removalAt = performance.now();
      clip.remove();
      const removed = await window.__probe.settledAt(removalAt);
      const remaining = [...root.querySelectorAll('img[draggable="false"]')];
      const ready =
        remaining.length > 0 && remaining.every((i) => i.complete && i.naturalWidth > 0);
      await Promise.all(remaining.map((i) => i.decode()));
      return {
        premature,
        enteredAt,
        entered,
        removalAt,
        removed,
        ready,
        observedAt: performance.now(),
      };
    });
    assert.equal(visibility.premature, false, JSON.stringify(visibility));
    assert.equal(visibility.ready, true, JSON.stringify(visibility));
    assert.ok(visibility.entered >= visibility.enteredAt, JSON.stringify(visibility));
    assert.ok(visibility.removed >= visibility.removalAt, JSON.stringify(visibility));
    assert.ok(visibility.observedAt - visibility.removed >= 250, JSON.stringify(visibility));

    // 3. requireChange with no change at all rejects (after the 30 s deadline).
    const three = await page.evaluate(() =>
      window.__probe.settledAt(performance.now()).then(
        () => 'resolved',
        (e) => e.message,
      ),
    );
    assert.equal(three, 'action produced no page-image change');
    await context.close();
  } finally {
    await run.close();
    await server.close();
  }
});
