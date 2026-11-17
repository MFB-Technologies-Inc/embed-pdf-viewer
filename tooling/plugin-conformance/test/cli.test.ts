import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtemp, realpath, rm, symlink } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

/** Run `entry` under tsx, as the package scripts do. */
function run(entry: string, args: string[]) {
  return spawnSync(process.execPath, ['--import=tsx', '--conditions=development', entry, ...args], {
    cwd: packageRoot,
    encoding: 'utf8',
  });
}

test('a check entry runs however it is named', async () => {
  const dir = await realpath(await mkdtemp(join(tmpdir(), 'plugin-conformance-cli-')));
  try {
    await symlink(join(packageRoot, 'src'), join(dir, 'src'));
    for (const entry of [
      'src/cli/differential.ts',
      'src/cli/differential.js',
      'src/cli/differential',
      join(dir, 'src', 'cli', 'differential.ts'),
    ]) {
      const result = run(entry, ['--bogus']);
      assert.equal(result.status, 2, `${entry}: ${result.stdout}${result.stderr}`);
      assert.match(result.stderr, /unknown argument --bogus/, entry);
    }
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

test('replay:check through a .js spelling fails on a missing receipts directory', () => {
  const result = run('src/cli/replay-check.js', ['--receipts', '/nonexistent-receipts']);
  assert.notEqual(result.status, 0, `${result.stdout}${result.stderr}`);
  assert.match(result.stderr, /nonexistent-receipts/);
});
