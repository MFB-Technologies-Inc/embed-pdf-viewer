import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { access, mkdtemp, readFile, realpath, rm, symlink } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

/** Run `entry` under tsx, as the package scripts do. */
function run(entry: string, args: string[], env: NodeJS.ProcessEnv = process.env) {
  return spawnSync(process.execPath, ['--import=tsx', '--conditions=development', entry, ...args], {
    cwd: packageRoot,
    encoding: 'utf8',
    env,
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

/** The five checks a `pnpm run` script starts through `src/cli/`. */
const checks = ['coverage', 'differential', 'member-order-allow', 'replay-check', 'rust-coverage'];

test('every check entry runs its check', () => {
  // An unknown argument stops each check before it does any work, so exit 2
  // and the message prove `main` ran.
  for (const check of checks) {
    const result = run(`src/cli/${check}.ts`, ['--bogus']);
    assert.equal(result.status, 2, `${check}: ${result.stdout}${result.stderr}`);
    assert.match(result.stderr, /unknown argument --bogus/, check);
  }
});

test('the old module paths no longer resolve', () => {
  for (const check of checks) {
    const result = run(`src/${check}.ts`, ['--receipts', '/nonexistent-receipts']);
    assert.notEqual(result.status, 0, `${check}: ${result.stdout}${result.stderr}`);
    assert.match(result.stderr, /ERR_MODULE_NOT_FOUND|Cannot find module/, check);
  }
});

/** Each check's `pnpm run` script, which the refusal names. */
const scripts: Record<string, string> = {
  coverage: 'coverage:check',
  differential: 'differential',
  'member-order-allow': 'member-order:allow',
  'replay-check': 'replay:check',
  'rust-coverage': 'rust-coverage:check',
};

test('a check module run directly fails and names its script, however it is named', async () => {
  const dir = await realpath(await mkdtemp(join(tmpdir(), 'plugin-conformance-direct-')));
  try {
    await symlink(join(packageRoot, 'src'), join(dir, 'src'));
    for (const check of checks) {
      for (const entry of [
        `src/checks/${check}.ts`,
        `src/checks/${check}.js`,
        `src/checks/${check}`,
        `./src/checks/../checks/${check}.ts`,
        join(packageRoot, 'src', 'checks', `${check}.ts`),
        join(dir, 'src', 'checks', `${check}.ts`),
      ]) {
        const result = run(entry, ['--receipts', '/nonexistent-receipts']);
        assert.equal(result.status, 2, `${entry}: ${result.stdout}${result.stderr}`);
        assert.match(
          result.stderr,
          new RegExp(`src/checks/${check}\\.ts is a library .* run ${scripts[check]}\``),
          entry,
        );
      }
    }
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

test('vectors rejects an unknown argument instead of writing', () => {
  const result = run('src/cli/generate.ts', ['--chekc']);
  assert.equal(result.status, 2, `${result.stdout}${result.stderr}`);
  assert.match(result.stderr, /unknown argument --chekc/);
});

test('replay:check fails with git\'s message when git fails', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'plugin-conformance-git-'));
  try {
    const result = run('src/cli/replay-check.ts', ['--receipts', dir], {
      ...process.env,
      GIT_DIR: join(dir, 'not-a-repository'),
    });
    assert.notEqual(result.status, 0, `${result.stdout}${result.stderr}`);
    assert.match(
      result.stderr,
      /git rev-parse HEAD failed, so no receipt can be tied to a commit: fatal: not a git repository/,
    );
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

test('every package script that runs a source file runs a src/cli/ entry', async () => {
  const { scripts } = JSON.parse(await readFile(join(packageRoot, 'package.json'), 'utf8')) as {
    scripts: Record<string, string>;
  };
  const entries = new Set<string>();
  for (const [name, command] of Object.entries(scripts)) {
    for (const path of command.match(/\bsrc\/\S+/g) ?? []) {
      assert.match(path, /^src\/cli\/[^/]+\.ts$/, `script ${name} runs ${path}`);
      await access(join(packageRoot, path));
      entries.add(path);
    }
  }
  for (const check of checks) assert.ok(entries.has(`src/cli/${check}.ts`), `no script runs ${check}`);
});

test('replay:check through a .js spelling fails on a missing receipts directory', () => {
  const result = run('src/cli/replay-check.js', ['--receipts', '/nonexistent-receipts']);
  assert.notEqual(result.status, 0, `${result.stdout}${result.stderr}`);
  assert.match(result.stderr, /nonexistent-receipts/);
});
