(() => {
  const state = { firstPaintAt: null, frames: [], longTasks: [], lastChange: null };
  const decodes = new WeakMap();
  const waiters = new Set();
  let visible = new Map();
  let lastFrame = null;
  let paintCandidate = null;
  const mark = () => {
    state.lastChange = performance.now();
  };
  const root = () => document.querySelector('embedpdf-viewer')?.shadowRoot ?? null;
  const sourceOf = (img) => `${img.getAttribute('src') ?? ''}\n${img.currentSrc}`;
  const parentOf = (el) => el.parentElement ?? el.getRootNode()?.host ?? null;
  const clips = (value) => /^(hidden|clip|scroll|auto)$/.test(value);

  // Rectangles use viewport CSS pixels, including ancestor scrolling and
  // transforms. Clip each axis separately; crossing a shadow root includes
  // the viewer host's clipping ancestors too.
  function visibleRect(img) {
    const rect = img.getBoundingClientRect();
    let left = Math.max(0, rect.left);
    let top = Math.max(0, rect.top);
    let right = Math.min(innerWidth, rect.right);
    let bottom = Math.min(innerHeight, rect.bottom);
    for (let el = img; el; el = parentOf(el)) {
      const css = getComputedStyle(el);
      if (css.display === 'none' || css.visibility === 'hidden' || css.visibility === 'collapse')
        return null;
      if (el !== img && (clips(css.overflowX) || clips(css.overflowY))) {
        const box = el.getBoundingClientRect();
        const sx = el.offsetWidth ? box.width / el.offsetWidth : 1;
        const sy = el.offsetHeight ? box.height / el.offsetHeight : 1;
        const innerLeft = box.left + el.clientLeft * sx;
        const innerTop = box.top + el.clientTop * sy;
        if (clips(css.overflowX)) {
          left = Math.max(left, innerLeft);
          right = Math.min(right, innerLeft + el.clientWidth * sx);
        }
        if (clips(css.overflowY)) {
          top = Math.max(top, innerTop);
          bottom = Math.min(bottom, innerTop + el.clientHeight * sy);
        }
      }
    }
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
    const next = new Map();
    for (const img of root()?.querySelectorAll('img[draggable="false"]') ?? []) {
      const rect = visibleRect(img);
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
          sample();
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
          if ((!requireChange || changed) && now - settledAt >= 250 && allDecoded())
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
