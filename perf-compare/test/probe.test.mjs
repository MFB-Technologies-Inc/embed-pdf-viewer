import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../lib/probe.js', import.meta.url), 'utf8');

// Control browser scheduling and decode completion, while executing the actual
// init script. Browser geometry/event behavior is covered by probe.selftest.
function harness() {
  let now = 0;
  let observer;
  let frames = [];
  const timers = [];
  const images = [];
  const listeners = {};
  const styleReads = new Map();
  class Element {
    parentElement = null;
    style = { overflowX: 'visible', overflowY: 'visible', display: 'block', visibility: 'visible' };
    rect = { left: 0, top: 0, right: 100, bottom: 100, width: 100, height: 100 };
    clientLeft = 0;
    clientTop = 0;
    get clientWidth() {
      return this.rect.width - 2 * this.clientLeft;
    }
    get clientHeight() {
      return this.rect.height - 2 * this.clientTop;
    }
    get offsetWidth() {
      return this.rect.width;
    }
    get offsetHeight() {
      return this.rect.height;
    }
    getBoundingClientRect() {
      return this.rect;
    }
    querySelectorAll() {
      return [];
    }
    querySelector() {
      return null;
    }
    getRootNode() {
      return { host: null };
    }
  }
  class Img extends Element {
    complete = true;
    naturalWidth = 100;
    isConnected = true;
    constructor(src = 'a') {
      super();
      this.swap(src);
    }
    swap(src) {
      this.src = this.currentSrc = src;
      this.deferred = Promise.withResolvers();
    }
    getAttribute(name) {
      return name === 'draggable' ? 'false' : this.src;
    }
    decodeCalls = 0;
    decode() {
      this.decodeCalls++;
      return this.deferred.promise;
    }
  }
  const root = {
    querySelectorAll: () => images.filter((i) => i.isConnected),
    addEventListener: (type, fn) => {
      listeners[type] = fn;
    },
  };
  const ctx = {
    window: {},
    performance: { now: () => now },
    innerWidth: 1280,
    innerHeight: 800,
    document: { querySelector: () => ({ shadowRoot: root }) },
    Element,
    HTMLImageElement: Img,
    getComputedStyle: (el) => {
      styleReads.set(el, (styleReads.get(el) ?? 0) + 1);
      return el.style;
    },
    requestAnimationFrame: (fn) => frames.push(fn),
    setTimeout: (fn, ms) => timers.push({ fn, at: now + ms }),
    PerformanceObserver: class {
      observe() {}
    },
    MutationObserver: class {
      constructor(fn) {
        observer = fn;
      }
      observe() {}
    },
  };
  vm.runInNewContext(source, ctx);
  const flush = async () => {
    for (let i = 0; i < 8; i++) await Promise.resolve();
  };
  return {
    probe: ctx.window.__probe,
    styleReads,
    Element,
    add(src = 'a', top = 0) {
      const img = new Img(src);
      img.rect = { ...img.rect, top, bottom: top + 100 };
      images.push(img);
      observer?.([{ type: 'childList', addedNodes: [img], removedNodes: [] }]);
      return img;
    },
    clip(img) {
      const parent = new Element();
      parent.style.overflowY = 'hidden';
      img.parentElement = parent;
      return parent;
    },
    swap(img, src) {
      img.swap(src);
      observer?.([{ type: 'attributes', target: img }]);
    },
    remove(img) {
      img.isConnected = false;
      observer?.([{ type: 'childList', addedNodes: [], removedNodes: [img] }]);
    },
    async decode(img) {
      img.deferred.resolve();
      listeners.load?.({ target: img });
      await flush();
    },
    async advance(t) {
      now = t;
      const dueFrames = frames;
      frames = [];
      dueFrames.forEach((fn) => fn(t));
      const due = timers.filter((timer) => timer.at <= now);
      for (const timer of due) {
        timers.splice(timers.indexOf(timer), 1);
        timer.fn();
      }
      await flush();
    },
  };
}
function result(promise) {
  const value = { status: 'pending' };
  promise.then(
    (time) => Object.assign(value, { status: 'resolved', time }),
    (error) => Object.assign(value, { status: 'rejected', error }),
  );
  return value;
}
async function ready() {
  const h = harness();
  const img = h.add();
  await h.advance(10);
  await h.decode(img);
  await h.advance(300);
  h.probe.reset();
  return { h, img };
}

test('settle and first paint wait for the actual decode promise', async () => {
  const h = harness();
  const img = h.add();
  await h.advance(10);
  const r = result(h.probe.settledAt(0));
  await h.advance(300);
  assert.equal(r.status, 'pending');
  assert.equal(h.probe.firstPaintAt, null);
  await h.advance(400);
  await h.decode(img);
  await h.advance(401);
  assert.equal(h.probe.firstPaintAt, 401);
  await h.advance(649);
  assert.equal(r.status, 'pending');
  await h.advance(665);
  assert.equal(r.time, 400);
});

test('offscreen changes cannot satisfy requireChange, but cached viewport entry can', async () => {
  const { h } = await ready();
  const r = result(h.probe.settledAt(301));
  await h.advance(310);
  const off = h.add('off', 2000);
  await h.advance(320);
  await h.decode(off);
  await h.advance(600);
  assert.equal(r.status, 'pending');
  assert.equal(off.decodeCalls, 0);
  off.rect = { ...off.rect, top: 200, bottom: 300 };
  await h.advance(700);
  await h.advance(701);
  await h.advance(970);
  assert.equal(r.status, 'resolved');
  assert.equal(r.time, 700);
  assert.equal(off.decodeCalls, 1);
});

test('clipping ancestors exclude images inside the browser viewport', async () => {
  const { h } = await ready();
  const off = h.add('clipped', 200);
  h.clip(off);
  await h.advance(310);
  await h.decode(off);
  const r = result(h.probe.settledAt(301));
  await h.advance(600);
  assert.equal(r.status, 'pending');
  await h.advance(31000);
  assert.equal(r.status, 'rejected');
});

test('removal restarts quiet time and an empty visible set never settles', async () => {
  const { h, img } = await ready();
  const other = h.add('b');
  await h.advance(310);
  await h.decode(other);
  const r = result(h.probe.settledAt(301));
  await h.advance(540);
  h.remove(other);
  await h.advance(540);
  await h.advance(600);
  assert.equal(r.status, 'pending');
  await h.advance(810);
  assert.equal(r.time, 540);
  h.remove(img);
  await h.advance(820);
  const empty = result(h.probe.settledAt(810));
  await h.advance(1200);
  assert.equal(empty.status, 'pending');
});

test('movement without mutations restarts the visible quiet window', async () => {
  const { h, img } = await ready();
  const r = result(h.probe.settledAt(301));
  img.rect = { ...img.rect, left: 20, right: 120 };
  await h.advance(400);
  await h.advance(670);
  assert.equal(r.time, 400);
});

test('a stale decode cannot complete or fail the replacement source', async () => {
  for (const reject of [false, true]) {
    const { h, img } = await ready();
    h.swap(img, 'A');
    await h.advance(310);
    const old = img.deferred;
    h.swap(img, 'B');
    await h.advance(320);
    const r = result(h.probe.settledAt(301));
    if (reject) old.reject(new Error('obsolete'));
    else old.resolve();
    await h.advance(600);
    assert.equal(r.status, 'pending');
    await h.advance(700);
    await h.decode(img);
    await h.advance(970);
    assert.equal(r.status, 'resolved');
    assert.equal(r.time, 700);
  }
});

test('current visible decode failures reject instead of appearing settled', async () => {
  const { h, img } = await ready();
  h.swap(img, 'bad');
  await h.advance(310);
  const r = result(h.probe.settledAt(301));
  img.deferred.reject(new Error('bad image'));
  await h.advance(330);
  await h.advance(350);
  assert.equal(r.status, 'rejected');
  assert.match(r.error.message, /decode/i);
});

test('reset preserves pending decode and honors the action completion boundary', async () => {
  const { h, img } = await ready();
  const unchanged = result(h.probe.settledAt(301));
  await h.advance(600);
  assert.equal(unchanged.status, 'pending');
  h.swap(img, 'B');
  await h.advance(610);
  h.probe.reset();
  const r = result(h.probe.settledAt(610, { doneAt: 700, requireChange: false }));
  await h.advance(800);
  await h.decode(img);
  await h.advance(1000);
  assert.equal(r.status, 'pending');
  await h.advance(1070);
  assert.equal(r.time, 800);
});

test('an image with no source waits without a decode failure', async () => {
  const h = harness();
  const img = h.add('');
  img.naturalWidth = 0;
  await h.advance(10);
  const r = result(h.probe.settledAt(0));
  await h.advance(300);
  assert.equal(r.status, 'pending');
  h.swap(img, 'real');
  img.naturalWidth = 100;
  await h.advance(400);
  await h.decode(img);
  await h.advance(670);
  assert.equal(r.time, 400);
});

test('an image decoded while detached can be reinserted without staying pending', async () => {
  const { h } = await ready();
  const img = h.add('reinsert');
  await h.advance(310);
  h.remove(img);
  await h.advance(320);
  await h.decode(img);
  img.isConnected = true;
  await h.advance(400);
  const r = result(h.probe.settledAt(390));
  await h.advance(670);
  assert.equal(r.status, 'resolved');
  assert.equal(r.time, 400);
});

test('doneAt extends the quiet window beyond the last image change', async () => {
  const { h } = await ready();
  const r = result(h.probe.settledAt(300, { doneAt: 500, requireChange: false }));
  await h.advance(749);
  assert.equal(r.status, 'pending');
  await h.advance(765);
  assert.equal(r.status, 'resolved');
  assert.equal(r.time, 500);
});

test('a visible decode failure rejects an active settle before removal can hide it', async () => {
  const { h } = await ready();
  const bad = h.add('bad');
  await h.advance(310);
  const r = result(h.probe.settledAt(301));
  bad.deferred.reject(new Error('broken'));
  for (let i = 0; i < 8; i++) await Promise.resolve();
  h.remove(bad);
  await h.advance(320);
  await h.advance(600);
  assert.equal(r.status, 'rejected');
  assert.match(r.error.message, /decode/);
});

test('overflow clips to the inner scrollport rather than its border box', async () => {
  const { h } = await ready();
  const img = h.add('under-border');
  img.rect = { left: 5, right: 15, top: 30, bottom: 40, width: 10, height: 10 };
  const clip = h.clip(img);
  clip.style.overflowX = 'hidden';
  clip.clientLeft = 20;
  clip.clientTop = 20;
  await h.advance(310);
  assert.equal(img.decodeCalls, 0);
  const r = result(h.probe.settledAt(301));
  await h.advance(600);
  assert.equal(r.status, 'pending');
});

test('an image outside the browser viewport costs no style reads', async () => {
  const { h } = await ready();
  const off = h.add('off', 2000);
  const parent = h.clip(off);
  h.styleReads.clear();
  await h.advance(310);
  assert.equal(h.styleReads.get(off), undefined);
  assert.equal(h.styleReads.get(parent), undefined);
  assert.equal(off.decodeCalls, 0);
});

test('one sample reads a shared ancestor once, however many images it holds', async () => {
  const { h, img } = await ready();
  const viewer = new h.Element();
  const page = new h.Element();
  page.parentElement = viewer;
  img.parentElement = page;
  const second = h.add('b', 120);
  second.parentElement = page;
  h.styleReads.clear();
  await h.advance(310);
  // One frame's sample: each image once, the page and viewer once.
  assert.equal(h.styleReads.get(img), 1);
  assert.equal(h.styleReads.get(second), 1);
  assert.equal(h.styleReads.get(page), 1);
  assert.equal(h.styleReads.get(viewer), 1);
});

test('a hidden ancestor still hides every image under it', async () => {
  const { h, img } = await ready();
  const page = new h.Element();
  page.style.visibility = 'hidden';
  img.parentElement = page;
  const r = result(h.probe.settledAt(301));
  await h.advance(310);
  await h.advance(31000);
  assert.equal(r.status, 'rejected');
  assert.match(r.error.message, /never settled|no page-image change/);
});
