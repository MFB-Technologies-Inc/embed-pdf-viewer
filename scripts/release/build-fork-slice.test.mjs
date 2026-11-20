import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

// Only the argument checks: a real slice is an hours-long PDFium build.
const cli = fileURLToPath(new URL('./build-fork-slice.mjs', import.meta.url));
const run = (...args) => spawnSync('node', [cli, ...args], { encoding: 'utf8' });

test('refuses an unknown target', () => {
  const result = run('win32-x64', '/nonexistent/out');
  assert.equal(result.status, 64, result.stderr);
  assert.match(result.stderr, /win32-x64/);
});

test('refuses a missing out dir', () => {
  const result = run('wasm32');
  assert.equal(result.status, 64, result.stderr);
});

// wasm32-eh is both a buildFork target and a fork-script target; it is one
// target to the caller.
test('lists every target once', () => {
  const result = run();
  assert.equal(result.status, 64, result.stderr);
  const names = result.stderr.match(/<([^>]*)>/)[1].split('|');
  assert.deepEqual(names, [...new Set(names)]);
  for (const name of ['wasm32', 'wasm32-eh', 'linux-arm64', 'ios-arm64', 'darwin-static-x64']) {
    assert.ok(names.includes(name), name);
  }
});
