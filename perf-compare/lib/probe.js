(() => {
  const state = { firstPaintAt: null, frames: [], longTasks: [], lastChange: null };
  const decodes = new WeakMap();
  const waiters = new Set();
  let visible = new Map();
  let lastFrame = null;
  let paintCandidate = null;
  // When the latest sample and the latest frame ran. While frames run, a
  // settle check reuses the frame's sample, so the probe samples once a
  // frame, not once a frame and again every 16 ms.
  let sampledAt = null;
  let tickedAt = null;
  const mark = () => {
    state.lastChange = performance.now();
  };
  const root = () => document.querySelector('embedpdf-viewer')?.shadowRoot ?? null;
  const sourceOf = (img) => `${img.getAttribute('src') ?? ''}\n${img.currentSrc}`;
  const parentOf = (el) => el.parentElement ?? el.getRootNode()?.host ?? null;
  const clips = (value) => /^(hidden|clip|scroll|auto)$/.test(value);

  const hidden = (css) => css.display === 'none' || css.visibility === 'hidden' || css.visibility === 'collapse';
  const VIEWPORT = () => ({ left: 0, top: 0, right: innerWidth, bottom: innerHeight });

  // The viewport area `el` and its ancestors let through, or null when one of
  // them is hidden. Every page image shares the viewer's ancestors, so one
  // sample memoizes each element's answer (`seen`) and walks a chain once.
  function clipOf(el, seen) {
    if (!el) return VIEWPORT();
    if (seen.has(el)) return seen.get(el);
    const css = getComputedStyle(el);
    let clip = null;
    if (!hidden(css)) {
      const up = clipOf(parentOf(el), seen);
      if (up && (clips(css.overflowX) || clips(css.overflowY))) {
        const box = el.getBoundingClientRect();
        const sx = el.offsetWidth ? box.width / el.offsetWidth : 1;
        const sy = el.offsetHeight ? box.height / el.offsetHeight : 1;
        const innerLeft = box.left + el.clientLeft * sx;
        const innerTop = box.top + el.clientTop * sy;
        clip = { ...up };
        if (clips(css.overflowX)) {
          clip.left = Math.max(clip.left, innerLeft);
          clip.right = Math.min(clip.right, innerLeft + el.clientWidth * sx);
        }
        if (clips(css.overflowY)) {
          clip.top = Math.max(clip.top, innerTop);
          clip.bottom = Math.min(clip.bottom, innerTop + el.clientHeight * sy);
        }
      } else clip = up;
    }
    seen.set(el, clip);
    return clip;
  }

  // Rectangles use viewport CSS pixels, including ancestor scrolling and
  // transforms. Clip each axis separately; crossing a shadow root includes
  // the viewer host's clipping ancestors too. An image outside the browser
  // viewport is offscreen whatever its ancestors are, so it costs one
  // getBoundingClientRect and no style reads.
  function visibleRect(img, seen) {
    const rect = img.getBoundingClientRect();
    let left = Math.max(0, rect.left);
    let top = Math.max(0, rect.top);
    let right = Math.min(innerWidth, rect.right);
    let bottom = Math.min(innerHeight, rect.bottom);
    if (!(right > left && bottom > top)) return null;
    if (hidden(getComputedStyle(img))) return null;
    const clip = clipOf(parentOf(img), seen);
    if (!clip) return null;
    left = Math.max(left, clip.left);
    top = Math.max(top, clip.top);
    right = Math.min(right, clip.right);
    bottom = Math.min(bottom, clip.bottom);
    return right > left && bottom > top ? `${left},${top},${right},${bottom}` : null;
  }

  function track(img, source) {
    let record = decodes.get(img);
    if (record?.source === source) return record;
    record = { source, status: 'pending' };
    decodes.set(img, record);
    // RenderLayer mounts its img before the engine supplies a source.
    if (!img.getAttribute('src') && !img.currentSrc) return record;
    const finish = (status) => {
      // A late completion from A must not satisfy (or fail) B, even if A's
      // callback runs before the next frame has noticed B's src mutation.
      if (sourceOf(img) !== source || decodes.get(img) !== record) return;
      if (!img.isConnected) {
        // Re-insertion must recheck decode rather than reuse an ignored
        // completion's permanently pending record.
        decodes.delete(img);
        return;
      }
      sample();
      record.status = status;
      if (visible.has(img)) {
        mark();
        if (status === 'error') {
          for (const fail of [...waiters]) fail(new Error('visible page image decode failed'));
        }
        if (state.firstPaintAt === null && allDecoded()) paintCandidate = visible;
      }
    };
    try {
      img.decode().then(
        () => finish('decoded'),
        () => finish('error'),
      );
    } catch {
      finish('error');
    }
    return record;
  }

  function sample() {
    sampledAt = performance.now();
    const next = new Map();
    const seen = new Map();
    for (const img of root()?.querySelectorAll('img[draggable="false"]') ?? []) {
      const rect = visibleRect(img, seen);
      if (rect === null) continue;
      const source = sourceOf(img);
      next.set(img, { source, rect, decode: track(img, source) });
    }
    if (
      next.size !== visible.size ||
      [...next].some(([img, v]) => {
        const previous = visible.get(img);
        return !previous || previous.source !== v.source || previous.rect !== v.rect;
      })
    )
      mark();
    visible = next;
  }

  // Longer than any frame interval while frames run, so a settle check
  // samples only when frames have stopped.
  const THROTTLED_MS = 100;

  const allDecoded = () =>
    visible.size > 0 &&
    [...visible].every(
      ([img, v]) => v.decode.status === 'decoded' && img.complete && img.naturalWidth > 0,
    );
  const sameSources = (a, b) =>
    a?.size === b.size && [...b].every(([img, v]) => a.get(img)?.source === v.source);
  const tick = (t) => {
    if (lastFrame !== null) state.frames.push(t - lastFrame);
    lastFrame = t;
    tickedAt = performance.now();
    sample();
    if (state.firstPaintAt === null) {
      if (allDecoded()) {
        if (sameSources(paintCandidate, visible)) state.firstPaintAt = t;
        else paintCandidate = visible;
      } else paintCandidate = null;
    }
    requestAnimationFrame(tick);
  };
  requestAnimationFrame(tick);
  new PerformanceObserver((list) => {
    for (const e of list.getEntries())
      state.longTasks.push({ start: e.startTime, duration: e.duration });
  }).observe({ type: 'longtask', buffered: true });

  window.__probe = {
    get firstPaintAt() {
      return state.firstPaintAt;
    },
    settledAt: (sinceMs, { doneAt = sinceMs, requireChange = true } = {}) =>
      new Promise((resolve, reject) => {
        const deadline = performance.now() + 30_000;
        let finished = false;
        const finish = (callback, value) => {
          if (finished) return;
          finished = true;
          waiters.delete(fail);
          callback(value);
        };
        const fail = (error) => finish(reject, error);
        waiters.add(fail);
        const check = () => {
          if (finished) return;
          // Reuse the frame's sample while requestAnimationFrame runs; sample
          // here, every check, once no frame has run for THROTTLED_MS, as
          // when the browser throttles frames.
          if (tickedAt === null || performance.now() - tickedAt >= THROTTLED_MS) sample();
          if (finished) return;
          const now = performance.now();
          const changed = state.lastChange !== null && state.lastChange >= sinceMs;
          if ([...visible.values()].some((v) => v.decode.status === 'error'))
            return fail(new Error('visible page image decode failed'));
          if (now > deadline)
            return fail(
              new Error(
                requireChange && !changed
                  ? 'action produced no page-image change'
                  : 'page never settled',
              ),
            );
          const settledAt = Math.max(changed ? state.lastChange : sinceMs, doneAt);
          // Quiet time counts to the sample that saw it, not to now: a change
          // since that sample is unseen until the next one.
          if ((!requireChange || changed) && sampledAt - settledAt >= 250 && allDecoded())
            return finish(resolve, settledAt);
          setTimeout(check, 16);
        };
        check();
      }),
    frames: () => state.frames.slice(),
    longTasks: () => state.longTasks.slice(),
    reset: () => {
      sample();
      state.frames = [];
      state.longTasks = [];
      state.lastChange = null;
      lastFrame = null;
    },
  };
})();
