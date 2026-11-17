import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readdirSync, readFileSync } from 'node:fs';
import { access, mkdtemp, readFile, realpath, rm, symlink } from 'node:fs/promises';
import { devNull, tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

/** Every module in `src/checks/`, read from disk so that a new check is
 *  covered by every test below without editing this file. */
const checks = readdirSync(join(packageRoot, 'src', 'checks'))
  .filter((file) => file.endsWith('.ts'))
  .map((file) => file.slice(0, -'.ts'.length))
  .sort();

const packageScripts = (
  JSON.parse(readFileSync(join(packageRoot, 'package.json'), 'utf8')) as {
    scripts: Record<string, string>;
  }
).scripts;

/** The package scripts that run `check`'s entry. */
const scriptsFor = (check: string): string[] =>
  Object.entries(packageScripts)
    .filter(([, command]) => command.split(/\s+/).includes(`src/cli/${check}.ts`))
    .map(([name]) => name);

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

test('every module in src/checks is a check with an entry and a script', async () => {
  assert.ok(checks.length > 0, 'src/checks/ holds no module');
  for (const check of checks) {
    const source = await readFile(join(packageRoot, 'src', 'checks', `${check}.ts`), 'utf8');
    assert.match(source, /^export (async )?function main\(/m, `${check} exports no main`);
    assert.match(source, /^refuseDirectRun\(/m, `${check} does not refuse a direct run`);
    await access(join(packageRoot, 'src', 'cli', `${check}.ts`));
    assert.ok(scriptsFor(check).length > 0, `no package script runs src/cli/${check}.ts`);
  }
});

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
          new RegExp(`src/checks/${check}\\.ts is a library .* run (${scriptsFor(check).join('|')})\``),
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
      // No user or system configuration to fail on first.
      GIT_CONFIG_GLOBAL: devNull,
      GIT_CONFIG_NOSYSTEM: '1',
      // The message below is git's English text.
      LC_ALL: 'C',
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
