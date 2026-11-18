// CU-C9: builds the four Apple fork slices for the worktree's runtime-src pin with
// perf-compare's buildFork, the function publish-engine-payload.mjs calls, without
// publishing anything. Run from the worktree root with PERF_WORK_DIR set:
//   PERF_WORK_DIR=<main checkout>/perf-compare/.work node docs/research/upstream-catchup-2026-11-18/scripts/cu-c9/slices.mjs
// The fork tree <PERF_WORK_DIR>/fork/<sha>/ was seeded beforehand (see reports/CU-C9.md).
import { buildFork } from '../../../../../perf-compare/scripts/build-arm.mjs';

const dir = process.cwd();
const out = {};
for (const target of ['ios-arm64', 'ios-sim-arm64', 'darwin-static-arm64', 'darwin-static-x64']) {
  const started = Date.now();
  const stamp = buildFork(dir, target);
  out[target] = { ...stamp, seconds: Math.round((Date.now() - started) / 1000) };
  console.log(`${target} ${stamp.sha} ${stamp.libSha256} ${out[target].seconds}s`);
}
if (new Set(Object.values(out).map((s) => s.sha)).size !== 1) throw new Error('slices disagree on the fork commit');
console.log(JSON.stringify(out, null, 2));
