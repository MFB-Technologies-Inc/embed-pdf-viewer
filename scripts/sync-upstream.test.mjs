import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const script = resolve('scripts/sync-upstream.sh');
const frozenBranch = 'research/multi-platform-rust-core-impl';
const pluginsBranch = `${frozenBranch}-plugins`;
const runtimeBranch = 'research/multi-platform-rust-core';
const sub = 'packages/engine/runtime/runtime-src';

function fixture() {
  const root = mkdtempSync(join(tmpdir(), 'sync-upstream-'));
  const env = { ...process.env, GIT_CONFIG_GLOBAL: join(root, 'gitconfig'), GIT_CONFIG_NOSYSTEM: '1',
    GIT_AUTHOR_NAME: 'Test', GIT_AUTHOR_EMAIL: 'test@example.com', GIT_COMMITTER_NAME: 'Test',
    GIT_COMMITTER_EMAIL: 'test@example.com', GIT_TERMINAL_PROMPT: '0' };
  const git = (cwd, ...args) => execFileSync('git', args, { cwd, env, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  git(root, 'config', '--global', 'protocol.file.allow', 'always');
  git(root, 'config', '--global', 'init.defaultBranch', 'main');
  const runtime = join(root, 'runtime'); mkdirSync(runtime); git(runtime, 'init');
  const commit = (cwd, path, value) => { writeFileSync(join(cwd, path), value); git(cwd, 'add', path); git(cwd, 'commit', '-m', path); return git(cwd, 'rev-parse', 'HEAD'); };
  const base = commit(runtime, 'base', 'base');
  git(runtime, 'checkout', '-b', runtimeBranch);
  const forkBase = commit(runtime, 'fork', 'fork');
  const forkRuntime = join(root, 'runtime-fork.git'); git(root, 'clone', '--bare', runtime, forkRuntime);
  git(runtime, 'checkout', 'main');
  const upstreamRuntime = commit(runtime, 'upstream', 'upstream');
  const override = commit(runtime, 'annotation', 'annotation');
  const viewer = join(root, 'viewer'); mkdirSync(viewer); git(viewer, 'init');
  git(viewer, 'submodule', 'add', forkRuntime, sub);
  git(join(viewer, sub), 'checkout', forkBase);
  mkdirSync(join(viewer, 'packages/engine/runtime/scripts'), { recursive: true });
  writeFileSync(join(viewer, 'packages/engine/runtime/scripts/runtime-payload-hash.sh'), 'echo fixture\n');
  git(viewer, 'add', '.'); git(viewer, 'commit', '-m', 'viewer base');
  git(viewer, 'branch', pluginsBranch);
  git(join(viewer, sub), 'fetch', runtime, 'main'); git(join(viewer, sub), 'checkout', upstreamRuntime);
  git(viewer, 'add', sub); commit(viewer, 'main.txt', 'upstream');
  const main = git(viewer, 'rev-parse', 'HEAD');
  const plugins = join(root, 'plugins'); git(viewer, 'worktree', 'add', plugins, pluginsBranch); commit(plugins, 'plugins.txt', 'plugins');
  git(plugins, 'submodule', 'update', '--init');
  for (const [url, path] of [
    ['https://github.com/embedpdf/embed-pdf-viewer.git', viewer],
    ['https://github.com/embedpdf/runtime.git', runtime],
    ['https://github.com/MFB-Technologies-Inc/embed-pdf-runtime.git', forkRuntime],
  ]) git(root, 'config', '--global', `url.file://${path}.insteadOf`, url);
  const pushes = join(root, 'pushes'); writeFileSync(pushes, '');
  const hook = (bare, name) => writeFileSync(join(bare, 'hooks/pre-receive'), `#!/bin/sh\necho ${name} >> '${pushes}'\ncat >/dev/null\n`, { mode: 0o755 });
  hook(forkRuntime, 'runtime');
  for (const name of ['fork', 'mirror']) {
    const bare = join(root, `${name}.git`); git(root, 'init', '--bare', bare); git(viewer, 'remote', 'add', name, bare); hook(bare, name);
  }
  const run = (...args) => spawnSync('bash', [script, ...args], { cwd: plugins, env, encoding: 'utf8' });
  const head = path => git(path, 'rev-parse', 'HEAD');
  const ancestor = (path, before, after = 'HEAD') => git(path, 'merge-base', '--is-ancestor', before, after);
  return { root, env, git, commit, runtime, forkRuntime, plugins, viewer, main, base, upstreamRuntime, override, pushes,
    run, head, ancestor, cleanup: () => rmSync(root, { recursive: true, force: true }) };
}
function withFixture(name, body) {
  test(name, () => { const f = fixture(); try { body(f); } finally { f.cleanup(); } });
}
function success(result) { assert.equal(result.status, 0, result.stdout + result.stderr); }

withFixture('merges main into plugins with a fork runtime override', f => {
  success(f.run('--runtime-commit', f.override));
  f.ancestor(f.plugins, f.main);
  const pin = f.git(f.plugins, 'rev-parse', `HEAD:${sub}`);
  f.ancestor(join(f.plugins, sub), f.override, pin);
  assert.equal(readFileSync(join(f.plugins, 'main.txt'), 'utf8'), 'upstream');
  assert.equal(readFileSync(f.pushes, 'utf8'), '');
  const before = f.head(f.plugins); success(f.run('--runtime-commit', f.override));
  assert.equal(f.head(f.plugins), before);
});
withFixture('pushes runtime, then plugins to the fork remote', f => {
  success(f.run('--push'));
  assert.deepEqual(readFileSync(f.pushes, 'utf8').trim().split('\n'), ['runtime', 'fork']);
  assert.equal(f.git(f.viewer, 'ls-remote', 'fork', `refs/heads/${pluginsBranch}`).split('\t')[0], f.head(f.plugins));
});
withFixture('refuses the frozen impl branch before changing the runtime', f => {
  f.git(f.plugins, 'checkout', '-b', frozenBranch);
  const before = [f.head(f.plugins), f.head(join(f.plugins, sub))];
  const result = f.run(); assert.notEqual(result.status, 0); assert.match(result.stderr, /is frozen/);
  assert.deepEqual([f.head(f.plugins), f.head(join(f.plugins, sub))], before);
});
withFixture('refuses a dirty branch', f => {
  writeFileSync(join(f.plugins, 'plugins.txt'), 'dirty'); const before = f.head(f.plugins);
  const result = f.run(); assert.notEqual(result.status, 0); assert.match(result.stderr, /commit or discard/); assert.equal(f.head(f.plugins), before);
});
withFixture('stops on viewer conflicts without pushing', f => {
  f.commit(f.plugins, 'main.txt', 'plugins conflict');
  const result = f.run('--push'); assert.notEqual(result.status, 0); assert.match(result.stderr, /merge conflicts/);
  assert.equal(readFileSync(f.pushes, 'utf8'), '');
  assert.equal(f.git(f.plugins, 'diff', '--name-only', '--diff-filter=U'), 'main.txt');
});
withFixture('single-branch local source mode remains available', f => {
  success(f.run('--from', 'main')); f.ancestor(f.plugins, f.main);
});
withFixture('refuses a runtime override that omits the source runtime', f => {
  const before = f.head(f.plugins);
  const result = f.run('--runtime-commit', f.base); assert.notEqual(result.status, 0);
  assert.match(result.stderr, /does not contain.*source runtime/);
  assert.equal(f.head(f.plugins), before);
});
withFixture('does not push the viewer when the runtime push is rejected', f => {
  writeFileSync(join(f.forkRuntime, 'hooks/pre-receive'), '#!/bin/sh\nexit 1\n', { mode: 0o755 });
  const result = f.run('--push'); assert.notEqual(result.status, 0);
  assert.equal(readFileSync(f.pushes, 'utf8'), '');
  assert.equal(f.git(f.viewer, 'ls-remote', 'fork', `refs/heads/${pluginsBranch}`), '');
});
withFixture('keeps unpushed local runtime commits', f => {
  const runtime = join(f.plugins, sub); f.git(runtime, 'checkout', runtimeBranch);
  const local = f.commit(runtime, 'plugins-runtime', 'keep');
  success(f.run()); f.ancestor(runtime, local, f.git(f.plugins, 'rev-parse', `HEAD:${sub}`));
});
withFixture('refuses a detached HEAD', f => {
  f.git(f.plugins, 'checkout', '--detach'); const before = f.head(f.plugins);
  const result = f.run(); assert.notEqual(result.status, 0); assert.match(result.stderr, /detached HEAD/); assert.equal(f.head(f.plugins), before);
});
withFixture('refuses a dirty runtime', f => {
  writeFileSync(join(f.plugins, sub, 'untracked'), 'keep'); const before = f.head(f.plugins);
  const result = f.run(); assert.notEqual(result.status, 0); assert.match(result.stderr, /uncommitted changes/); assert.equal(f.head(f.plugins), before);
});
withFixture('refuses an existing merge', f => {
  f.git(f.plugins, 'merge', '--no-commit', '--no-ff', 'main');
  const result = f.run(); assert.notEqual(result.status, 0); assert.match(result.stderr, /merge is in progress/);
});
withFixture('resumes after a viewer conflict is committed', f => {
  f.commit(f.plugins, 'main.txt', 'plugins conflict'); assert.notEqual(f.run().status, 0);
  f.git(f.plugins, 'checkout', '--theirs', 'main.txt'); f.git(f.plugins, 'add', 'main.txt'); f.git(f.plugins, 'commit', '--no-edit');
  success(f.run()); f.ancestor(f.plugins, f.main);
});
withFixture('explicit viewer remotes replace the fork default', f => {
  success(f.run('--push', '--viewer-remote', 'mirror'));
  assert.deepEqual(readFileSync(f.pushes, 'utf8').trim().split('\n'), ['runtime', 'mirror']);
});

withFixture('runtime conflicts stop before the viewer changes', f => {
  const target = f.commit(f.runtime, 'fork', 'upstream conflict');
  const before = f.head(f.plugins); const result = f.run('--runtime-commit', target, '--push');
  assert.notEqual(result.status, 0); assert.match(result.stderr, /runtime merge conflicts/);
  assert.equal(f.head(f.plugins), before);
  assert.equal(f.git(join(f.plugins, sub), 'diff', '--name-only', '--diff-filter=U'), 'fork');
  assert.equal(readFileSync(f.pushes, 'utf8'), '');
});
withFixture('an unavailable runtime override stops before the viewer merge or pushes', f => {
  const before = f.head(f.plugins); const result = f.run('--runtime-commit', 'missing-ref', '--push');
  assert.notEqual(result.status, 0); assert.equal(f.head(f.plugins), before);
  assert.equal(readFileSync(f.pushes, 'utf8'), '');
});
withFixture('single-branch URL source mode still merges upstream', f => {
  success(f.run('--from', 'https://github.com/embedpdf/embed-pdf-viewer.git', 'main'));
  f.ancestor(f.plugins, f.main);
});
withFixture('both merges disable the commit graph', f => {
  const trace = join(f.root, 'git-trace'); f.env.GIT_TRACE2_EVENT = trace; success(f.run());
  const merges = readFileSync(trace, 'utf8').trim().split('\n').map(line => JSON.parse(line))
    .filter(event => event.event === 'start' && event.argv.includes('merge') && event.argv.includes('-q'));
  assert.equal(merges.length, 2);
  for (const { argv } of merges) assert.deepEqual(argv.slice(1, 4), ['-c', 'core.commitGraph=false', 'merge']);
});
