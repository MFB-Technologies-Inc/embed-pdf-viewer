import { test } from 'node:test';
import assert from 'node:assert/strict';
import { collectEnv, compatible, verifyFixtures } from '../lib/env.mjs';

const config = {
  armOrder: ['a', 'b'],
  arms: {
    a: { worktree: 'w', kernel: 'typescript', rustReads: false },
    b: { worktree: 'w', kernel: 'typescript', rustReads: true },
  },
  worktrees: { w: { ref: 'main', sha: 'f'.repeat(40), recipe: 'typescript' } },
};
const stamps = { w: { source: 'fetched', engineWasmSha256: 'e'.repeat(64) } };

const meta = () =>
  collectEnv({
    config,
    stamps,
    dists: { a: '/nonexistent', b: '/nonexistent' },
    fixtures: verifyFixtures(),
    browserVersion: '140.0.0.0',
    profile: 'desktop',
    flows: ['open', 'scroll'],
  });

test('every environment field is present and non-empty', () => {
  const m = meta();
  for (const key of [
    'harnessCommit',
    'arms',
    'fixtures',
    'browserVersion',
    'playwrightVersion',
    'nodeVersion',
    'os',
    'cpuModel',
    'cpuCores',
    'totalMemoryBytes',
    'powerSource',
    'viewport',
    'deviceScaleFactor',
    'profile',
    'throttleRate',
    'flows',
  ]) {
    assert.ok(m[key] !== undefined && m[key] !== null && m[key] !== '', `${key} is empty`);
  }
  assert.equal(m.arms.b.sha, 'f'.repeat(40));
  assert.equal(m.arms.b.engineWasmSha256, 'e'.repeat(64));
  assert.equal(m.throttleRate, 1);
});

test('the committed fixtures match fixtures.json', () => {
  assert.deepEqual(Object.keys(verifyFixtures()).sort(), ['annots', 'ebook', 'forms', 'large']);
});

test('compatible lists exactly the differing keys', () => {
  const a = meta();
  assert.deepEqual(compatible(a, meta()), []);
  const b = { ...meta(), profile: 'slow', flows: ['open'], nodeVersion: 'v0' };
  assert.deepEqual(compatible(a, b), ['profile', 'flows']);
});

test('runs with different probe code cannot be compared as the same measurement', () => {
  const current = meta();
  assert.match(current.probeSha256 ?? '', /^[a-f0-9]{64}$/);
  const legacy = { ...current };
  delete legacy.probeSha256;
  assert.deepEqual(compatible(current, legacy), ['probeSha256']);
  assert.deepEqual(compatible(current, { ...current, probeSha256: '0'.repeat(64) }), [
    'probeSha256',
  ]);
});
