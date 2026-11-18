// Which viewer API generation a worktree has, read from its stage contract.
// The upstream catch-up (plugins 2aaa8f841, main ed064e0c8) replaced the
// stage's `goToPageIndex` and `getPageFrame` with `goToPage(index)` and
// `getPageBox`, along with the other renames entry/api-v2.ts lists.
// build-entry.mjs builds each arm with entry/api-<generation>.ts, so every arm
// is driven through the calls its own packages have.
import { readFileSync } from 'node:fs';
import path from 'node:path';

export const STAGE_CONTRACT = 'packages/plugin/stage/src/contract.ts';

export function apiGenerationOf(stageContract) {
  const has = (re) => re.test(stageContract);
  const v1 = has(/\bgetPageFrame\(/) && has(/\bgoToPageIndex\(/);
  const v2 = has(/\bgetPageBox\(/) && has(/\bgoToPage\(/);
  if (v1 && !v2) return 'v1';
  if (v2 && !v1) return 'v2';
  throw new Error(`the stage contract matches ${v1 ? 'both' : 'neither'} of the viewer API generations`);
}

export function apiGeneration(worktree) {
  const file = path.join(worktree, STAGE_CONTRACT);
  try {
    return apiGenerationOf(readFileSync(file, 'utf8'));
  } catch (error) {
    throw new Error(`${file}: ${error.message}`);
  }
}
