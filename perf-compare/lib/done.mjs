// Flows whose done condition needs a read that some viewers lack
// (window.__perf.capabilities, entry/api-v*.ts). With it, the flow waits for
// the viewer to show it handled the input; without it, the flow ends at its
// last input event, so its rows time input pacing, not the arm's handling,
// and report.mjs marks them.
export const DONE_NEEDS = { hover: 'hovered' };

// Whether an arm with `capabilities` has `flow`'s done condition.
export const hasDone = (flow, capabilities = []) => !DONE_NEEDS[flow] || capabilities.includes(DONE_NEEDS[flow]);
