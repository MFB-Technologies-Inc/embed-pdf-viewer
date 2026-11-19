import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { FORK_RECIPE, forkStampMatches } from '../../perf-compare/scripts/build-arm.mjs';

const cli = fileURLToPath(new URL('./stamp-fork-slice.mjs', import.meta.url));
const sha = 'a'.repeat(40);
const target = 'wasm32-eh';

// A repo whose runtime-src gitlink is `sha`, a staging dir whose
// BUILD-METADATA.json says `metadata`, and a PERF_WORK_DIR to stamp into.
function fixture(t, metadata) {
  const root = mkdtempSync(path.join(tmpdir(), 'stamp-fork-slice-'));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  const repo = path.join(root, 'repo');
  mkdirSync(repo);
  const env = {
    ...process.env,
    GIT_CONFIG_GLOBAL: path.join(root, 'gitconfig'),
    GIT_CONFIG_NOSYSTEM: '1',
    GIT_AUTHOR_NAME: 'Test',
    GIT_AUTHOR_EMAIL: 'test@example.com',
    GIT_COMMITTER_NAME: 'Test',
    GIT_COMMITTER_EMAIL: 'test@example.com',
  };
  const git = (...args) => execFileSync('git', args, { cwd: repo, env, stdio: 'pipe' });
  git('init', '--quiet');
  git('update-index', '--add', '--cacheinfo', `160000,${sha},packages/engine/runtime/runtime-src`);
  git('commit', '--quiet', '-m', 'gitlink');
  const staging = path.join(root, 'staging');
  mkdirSync(path.join(staging, 'lib'), { recursive: true });
  writeFileSync(path.join(staging, 'lib/libembedpdf.a'), 'fixture archive');
  writeFileSync(path.join(staging, 'BUILD-METADATA.json'), JSON.stringify(metadata));
  const work = path.join(root, 'work');
  const stampFile = path.join(work, 'fork', sha, `${target}.json`);
  const stamp = () =>
    spawnSync('node', [cli, target, staging], {
      cwd: repo,
      env: { ...env, PERF_WORK_DIR: work },
      encoding: 'utf8',
    });
  return { staging, stampFile, stamp };
}

test('writes a stamp buildFork accepts', (t) => {
  const { staging, stampFile, stamp } = fixture(t, { sha, target });
  const result = stamp();
  assert.equal(result.status, 0, result.stderr);
  const written = JSON.parse(readFileSync(stampFile, 'utf8'));
  assert.equal(written.staging, staging);
  assert.equal(written.seeded, false);
  assert.equal(forkStampMatches(written, { recipe: FORK_RECIPE, sha, target }), true);
});

test('rejects a staging dir from another commit', (t) => {
  const { stampFile, stamp } = fixture(t, { sha: 'b'.repeat(40), target });
  const result = stamp();
  assert.equal(result.status, 65, result.stderr);
  assert.equal(existsSync(stampFile), false);
});

test('rejects a staging dir for another target', (t) => {
  const { stampFile, stamp } = fixture(t, { sha, target: 'ios-arm64' });
  const result = stamp();
  assert.equal(result.status, 65, result.stderr);
  assert.equal(existsSync(stampFile), false);
});
