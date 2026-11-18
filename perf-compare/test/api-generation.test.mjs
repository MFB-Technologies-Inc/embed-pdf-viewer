import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { apiGeneration, apiGenerationOf, STAGE_CONTRACT } from '../lib/api-generation.mjs';

// Trimmed from packages/plugin/stage/src/contract.ts at main 2516e2786 and at
// plugins 2aaa8f841.
const V1 = `
  goToPage(page: PageRef, options?: GoToOptions): void;
  goToPageIndex(index: number, options?: GoToOptions): void;
  getPageFrame(page: PageRef): VisiblePage | null;
`;
const V2 = `
  goToPage(page: PageRef | number, options?: GoToOptions): void;
  getPageBox(page: PageRef | number): VisiblePage | null;
`;

test('the pre-catch-up stage contract is v1', () => {
  assert.equal(apiGenerationOf(V1), 'v1');
});

test('the post-catch-up stage contract is v2', () => {
  assert.equal(apiGenerationOf(V2), 'v2');
});

test('a contract with both or neither generation throws', () => {
  assert.throws(() => apiGenerationOf(V1 + V2), /both/);
  assert.throws(() => apiGenerationOf('zoomTo(zoom: number): void;'), /neither/);
});

// The built worktrees, when this checkout has them.
const worktrees = fileURLToPath(new URL('../.work/worktrees', import.meta.url));
const expected = { main: 'v1', 'plugins-base': 'v1', 'main-now': 'v2', 'plugins-next': 'v2' };
for (const [id, generation] of Object.entries(expected)) {
  const dir = path.join(worktrees, id);
  test(`worktree ${id} is ${generation}`, { skip: !existsSync(path.join(dir, STAGE_CONTRACT)) && 'not materialized' }, () => {
    assert.equal(apiGeneration(dir), generation);
  });
}
