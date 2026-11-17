import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtemp, realpath, rm, symlink, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath, pathToFileURL } from 'node:url';

import { isMainModule } from '../src/main-module';

const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

/** A fresh temporary directory as a real path (the system temp dir can sit
 *  under a symlink, as `/var` does on macOS). */
const tempDir = async () => realpath(await mkdtemp(join(tmpdir(), 'plugin-conformance-main-')));

test('a script started through a symlink is the main module', async () => {
  const dir = await tempDir();
  try {
    const real = join(dir, 'real.ts');
    const link = join(dir, 'link.ts');
    await writeFile(real, '');
    await symlink(real, link);
    const url = pathToFileURL(real).href;
    assert.equal(isMainModule(url, real), true);
    assert.equal(isMainModule(url, link), true);
    assert.equal(isMainModule(url, join(dir, 'real')), true, 'without its extension');
    assert.equal(isMainModule(url, join(dir, 'link')), true, 'a symlink without its extension');
    assert.equal(isMainModule(url, join(dir, 'other.ts')), false);
    assert.equal(isMainModule(url, join(dir, 'other')), false);
    assert.equal(isMainModule(url, undefined), false);
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

test('a check run through a symlinked directory still runs and fails on a bad argument', async () => {
  const dir = await tempDir();
  try {
    await symlink(join(packageRoot, 'src'), join(dir, 'src'));
    const run = spawnSync(
      process.execPath,
      ['--import=tsx', '--conditions=development', join(dir, 'src', 'differential.ts'), '--bogus'],
      { cwd: packageRoot, encoding: 'utf8' },
    );
    assert.equal(run.status, 2, run.stderr);
    assert.match(run.stderr, /unknown argument --bogus/);
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

test('a check started without its extension still runs', () => {
  for (const entry of ['src/replay-check', 'src/replay-check.ts']) {
    const run = spawnSync(
      process.execPath,
      ['--import=tsx', '--conditions=development', entry, '--receipts', '/nonexistent-receipts'],
      { cwd: packageRoot, encoding: 'utf8' },
    );
    assert.notEqual(run.status, 0, `${entry}: ${run.stdout}${run.stderr}`);
    assert.match(run.stderr, /nonexistent-receipts/, entry);
  }
});
