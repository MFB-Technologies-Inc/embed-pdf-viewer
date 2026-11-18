// How samples become report rows. report.mjs and the history
// (lib/history.mjs) both read rows through this file, so a row means the
// same thing in a run's report and on the history page.

// Fixed per metric, never chosen from the data, so a metric's A/A
// calibration and its other pairs always use the same kind.
export const isDifference = (metric) =>
  ['jankFrames', 'longTasks', 'longTaskMs', 'heapSoakSlopeBytes'].includes(metric) || /[sS]ettleMs$/.test(metric);

// Memory metrics get their own section; sizes aren't sampled.
export const MEMORY = ['heapAfterOpenBytes', 'workerHeapAfterOpenBytes', 'heapSoakSlopeBytes', 'heapCycle5Bytes', 'heapCycle30Bytes'];

export const rowLabel = (flow, fixture, metric) => `${flow === 'open' ? `open(${fixture})` : flow} / ${metric}`;

// { rowKey: { label, metric, byArm: { arm: number[] } } } in first-seen order.
export function rows(samples) {
  const out = new Map();
  for (const s of samples) {
    if (s.error) continue;
    for (const [metric, value] of Object.entries(s.metrics)) {
      const key = `${s.flow}|${s.fixture}|${metric}`;
      if (!out.has(key)) out.set(key, { label: rowLabel(s.flow, s.fixture, metric), flow: s.flow, metric, byArm: {} });
      (out.get(key).byArm[s.arm] ??= []).push(value);
    }
  }
  return out;
}

// Which way is good. Every metric is lower-is-better today (lib/stats.mjs
// `verdict` assumes it too). A higher-is-better metric is declared here, and
// the history page charts it in its own group.
const HIGHER_IS_BETTER = [];
export const better = (metric) => (HIGHER_IS_BETTER.includes(metric) ? 'higher' : 'lower');

export const unit = (metric) => (/Bytes$/.test(metric) ? 'bytes' : /Ms$/.test(metric) ? 'ms' : 'count');
