import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const guard = fileURLToPath(new URL('../scripts/remote-guard.sh', import.meta.url));

// A machine made of files: `listener` and `worker` exist while that process
// is alive, `busy` holds how many more looks find a job running, and `calls`
// is every launchctl call and the command, in order.
function machine({ service = true, listener = false, busy = 0 } = {}) {
  const root = mkdtempSync(path.join(tmpdir(), 'guard-'));
  const bin = path.join(root, 'bin');
  const runner = path.join(root, 'runner');
  mkdirSync(bin);
  mkdirSync(runner);
  const plist = path.join(root, 'the.runner.plist');
  if (service) writeFileSync(path.join(runner, '.service'), `${plist}\n`);
  if (listener) writeFileSync(path.join(root, 'listener'), '');
  writeFileSync(path.join(root, 'busy'), String(busy));
  writeFileSync(path.join(root, 'calls'), '');
  const script = (name, body) => {
    writeFileSync(path.join(bin, name), `#!/bin/sh\nroot='${root}'\n${body}\n`);
    chmodSync(path.join(bin, name), 0o755);
  };
  script(
    'pgrep',
    `case "$2" in
  *Listener*) [ -f "$root/listener" ] || [ "$(cat "$root/busy")" -gt 0 ] ;;
  *) n=$(cat "$root/busy"); [ "$n" -gt 0 ] || exit 1; echo $((n - 1)) > "$root/busy" ;;
esac`,
  );
  script(
    'launchctl',
    `echo "$*" >> "$root/calls"
case "$1" in
  print) [ -f "$root/listener" ] ;;
  bootout) [ ! -f "$root/stuck" ] || exit 5; rm -f "$root/listener" ;;
  bootstrap) : > "$root/listener" ;;
esac`,
  );
  const run = (command, env = {}) => {
    const r = spawnSync('sh', [guard, path.join(root, 'exit'), command], {
      encoding: 'utf8',
      env: {
        ...process.env,
        PATH: `${bin}:${process.env.PATH}`,
        PERF_RUNNER_DIR: runner,
        PERF_RUNNER_POLL: '0',
        ROOT: root,
        ...env,
      },
    });
    const calls = readFileSync(path.join(root, 'calls'), 'utf8').split('\n').filter(Boolean);
    const exit = existsSync(path.join(root, 'exit')) ? readFileSync(path.join(root, 'exit'), 'utf8').trim() : null;
    return { ...r, calls: calls.map((c) => c.split(' ')[0]), exit, listening: existsSync(path.join(root, 'listener')) };
  };
  return { root, run };
}
// What the command saw: whether the listener was alive while it ran.
const probe = 'if [ -f "$ROOT/listener" ]; then echo ran-with-runner >> "$ROOT/calls"; else echo ran >> "$ROOT/calls"; fi';

test('a machine with no runner just runs the command', () => {
  const r = machine({ service: false }).run(probe);
  assert.equal(r.status, 0);
  assert.deepEqual(r.calls, ['ran']);
  assert.equal(r.exit, '0');
});

test('an idle runner is stopped for the command and started after it', () => {
  const r = machine({ listener: true }).run(probe);
  assert.equal(r.status, 0);
  assert.deepEqual(r.calls, ['print', 'bootout', 'ran', 'bootstrap']);
  assert.equal(r.listening, true);
});

test('the runner comes back when the command fails, and the status is kept', () => {
  const r = machine({ listener: true }).run(`${probe}; exit 3`);
  assert.equal(r.status, 3);
  assert.equal(r.exit, '3');
  assert.deepEqual(r.calls, ['print', 'bootout', 'ran', 'bootstrap']);
});

test('a running job is waited for, not cut short', () => {
  const r = machine({ listener: true, busy: 2 }).run(probe, { PERF_RUNNER_POLL: '1' });
  assert.equal(r.status, 0);
  assert.match(r.stdout, /waiting for the running job/);
  assert.deepEqual(r.calls, ['print', 'bootout', 'ran', 'bootstrap']);
});

test('a job that outlasts the wait leaves the runner alone and runs nothing', () => {
  const r = machine({ listener: true, busy: 50 }).run(probe, { PERF_RUNNER_WAIT: '0' });
  assert.equal(r.status, 75);
  assert.equal(r.exit, '75');
  assert.deepEqual(r.calls, ['print']);
  assert.equal(r.listening, true);
});

test('a service that was already stopped stays stopped', () => {
  const r = machine({ listener: false }).run(probe);
  assert.equal(r.status, 0);
  assert.deepEqual(r.calls, ['print', 'ran']);
  assert.equal(r.listening, false);
});

test('a runner that is not a service refuses the run', () => {
  const r = machine({ service: false, listener: true }).run(probe);
  assert.equal(r.status, 70);
  assert.equal(r.exit, '70');
  assert.deepEqual(r.calls, []);
});

test('a service that will not stop refuses the run', () => {
  const m = machine({ listener: true });
  writeFileSync(path.join(m.root, 'stuck'), '');
  const r = m.run(probe);
  assert.equal(r.status, 70);
  assert.deepEqual(r.calls, ['print', 'bootout']);
});
