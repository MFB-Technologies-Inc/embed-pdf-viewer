import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdir, mkdtemp, realpath, rm, symlink, writeFile } from 'node:fs/promises';
import { devNull, tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { after, before, test } from 'node:test';

// Before any check module: see `src/library-guard.ts`.
import '../src/as-library';

import {
  checkoutState,
  childEnv,
  compareReceipts,
  readReceipts,
  replayPackages,
  runProblems,
  staleProblems,
  unappliedObservations,
  unlistedAllowances,
  unusedAllowances,
  type CargoMetadata,
  type Receipt,
} from '../src/checks/replay-check';

function receipt(file: string, ran: string[], only: string | null = null): Receipt {
  return {
    runId: 'run-1',
    head: 'abc',
    dirty: false,
    vectorSha256: `sha-${file}`,
    file,
    only,
    ran,
    failures: 0,
    memberOrder: true,
    allowListOverridden: false,
    exceptions: [],
    memberOrderAllowed: [],
  };
}

const expected = new Map([['core/x.json', ['a', 'b', 'c']]]);

test('two receipts for one file whose ran lists cover every case pass', () => {
  assert.deepEqual(
    compareReceipts(expected, [receipt('core/x.json', ['a', 'b']), receipt('core/x.json', ['c'])]),
    [],
  );
});

test('a case no receipt ran is a problem', () => {
  assert.deepEqual(compareReceipts(expected, [receipt('core/x.json', ['a', 'b'])]), [
    'core/x.json: case "c" never ran',
  ]);
});

test('a vector file with no receipt is a problem', () => {
  assert.deepEqual(compareReceipts(expected, []), ['core/x.json: no Rust test replayed it']);
});

test('a filtered receipt is a problem even when every case ran', () => {
  const problems = compareReceipts(expected, [receipt('core/x.json', ['a', 'b', 'c'], 'b')]);
  assert.equal(problems.length, 1);
  assert.match(problems[0], /^core\/x\.json: .*PLUGIN_CONFORMANCE_ONLY="b"/);
});

test('a receipt for a file the generator does not write is a problem', () => {
  assert.deepEqual(
    compareReceipts(expected, [
      receipt('core/x.json', ['a', 'b', 'c']),
      receipt('core/gone.json', ['z']),
    ]),
    ['core/gone.json: unknown vector file (no suite writes it)'],
  );
});

test('a ran case the generator does not write is a problem', () => {
  assert.deepEqual(compareReceipts(expected, [receipt('core/x.json', ['a', 'b', 'c', 'old'])]), [
    'core/x.json: case "old" ran but no suite writes it (stale vector file)',
  ]);
});

test('readReceipts reads every *.json and skips partial files', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'replay-check-test-'));
  try {
    await writeFile(
      join(dir, 'core__x.json.1.0.json'),
      JSON.stringify(receipt('core/x.json', ['a'])),
    );
    await writeFile(
      join(dir, 'core__x.json.2.0.json'),
      JSON.stringify(receipt('core/x.json', ['b'])),
    );
    await writeFile(join(dir, 'core__x.json.3.0.json.partial'), '{');
    const read = await readReceipts(dir);
    assert.deepEqual(read.problems, []);
    assert.deepEqual(read.receipts.map((r) => r.ran).sort(), [['a'], ['b']]);
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

test('readReceipts reports a malformed receipt by its file name instead of throwing', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'replay-check-test-'));
  try {
    await writeFile(join(dir, 'good.json'), JSON.stringify(receipt('core/x.json', ['a'])));
    await writeFile(join(dir, 'no-ran.json'), JSON.stringify({ file: 'core/x.json', only: null }));
    await writeFile(
      join(dir, 'bad-only.json'),
      JSON.stringify({ ...receipt('core/x.json', ['a']), only: 3 }),
    );
    await writeFile(join(dir, 'not-json.json'), '{');
    await writeFile(
      join(dir, 'case-list.json'),
      JSON.stringify({ ...receipt('core/x.json', ['a']), memberOrderAllowed: ['a'] }),
    );
    const { failures: _, ...noFailures } = receipt('core/x.json', ['a']);
    await writeFile(join(dir, 'no-failures.json'), JSON.stringify(noFailures));
    await writeFile(
      join(dir, 'bad-dirty.json'),
      JSON.stringify({ ...receipt('core/x.json', ['a']), dirty: '0' }),
    );
    const read = await readReceipts(dir);
    assert.deepEqual(
      read.receipts.map((r) => r.file),
      ['core/x.json'],
    );
    assert.equal(read.problems.length, 6);
    for (const name of [
      'bad-dirty.json',
      'bad-only.json',
      'case-list.json',
      'no-failures.json',
      'no-ran.json',
      'not-json.json',
    ]) {
      assert.ok(
        read.problems.some((line) => line.startsWith(`${name}: malformed receipt`)),
        `${name} in ${JSON.stringify(read.problems)}`,
      );
    }
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

test('replayPackages selects plugin-conformance and the workspace members that depend on it', () => {
  const dep = (name: string, kind: string | null) => ({ name, kind });
  const metadata: CargoMetadata = {
    workspace_members: ['pc-id', 'normal-id', 'dev-id', 'build-id', 'none-id'],
    packages: [
      { id: 'pc-id', name: 'plugin-conformance', dependencies: [dep('serde', null)] },
      { id: 'normal-id', name: 'uses-normal', dependencies: [dep('plugin-conformance', null)] },
      { id: 'dev-id', name: 'uses-dev', dependencies: [dep('plugin-conformance', 'dev')] },
      { id: 'build-id', name: 'uses-build', dependencies: [dep('plugin-conformance', 'build')] },
      { id: 'none-id', name: 'unrelated', dependencies: [dep('serde', null)] },
      { id: 'outside-id', name: 'not-a-member', dependencies: [dep('plugin-conformance', 'dev')] },
    ],
  };
  assert.deepEqual(replayPackages(metadata), ['plugin-conformance', 'uses-dev', 'uses-normal']);
});

test('childEnv removes every variable that would weaken the proof and sets receipts', () => {
  const env = childEnv(
    {
      PATH: '/bin',
      PLUGIN_CONFORMANCE_ONLY: 'open-all',
      PLUGIN_CONFORMANCE_MEMBER_ORDER: '0',
      PLUGIN_CONFORMANCE_MEMBER_ORDER_ALLOW: '/tmp/allow.json',
      PLUGIN_CONFORMANCE_RECEIPTS: '/tmp/old',
      PLUGIN_CONFORMANCE_RUN_ID: 'old-run',
      PLUGIN_CONFORMANCE_HEAD: 'old-head',
    },
    '/tmp/receipts',
    'run-2',
    { head: 'head-2', dirty: false, changes: [] },
  );
  assert.deepEqual(env, {
    PATH: '/bin',
    PLUGIN_CONFORMANCE_RECEIPTS: '/tmp/receipts',
    PLUGIN_CONFORMANCE_RUN_ID: 'run-2',
    PLUGIN_CONFORMANCE_HEAD: 'head-2',
    PLUGIN_CONFORMANCE_DIRTY: '0',
  });
  const dirty = childEnv({ PLUGIN_CONFORMANCE_DIRTY: '0' }, '/tmp/r', 'run-3', {
    head: 'head-3',
    dirty: true,
    changes: ['a.rs'],
  });
  assert.equal(dirty.PLUGIN_CONFORMANCE_DIRTY, '1');
});

test('checkoutState reads the commit and uncommitted changes, and fails with git', () => {
  const answers: Record<string, { status: number; stdout: string; stderr: string }> = {
    'rev-parse HEAD': { status: 0, stdout: 'abc\n', stderr: '' },
    'status --porcelain -z --untracked-files=normal': { status: 0, stdout: '', stderr: '' },
  };
  const git = (args: readonly string[]) => answers[args.join(' ')];
  assert.deepEqual(checkoutState({ run: git }), { head: 'abc', dirty: false, changes: [] });
  // `-z` prints raw paths, NUL-ended, and a rename's source as a field of its own.
  answers['status --porcelain -z --untracked-files=normal'] = {
    status: 0,
    stdout: ' M a.rs\0R  new name.rs\0old.rs\0?? é.rs\0',
    stderr: '',
  };
  assert.deepEqual(checkoutState({ run: git }), {
    head: 'abc',
    dirty: true,
    changes: ['a.rs', 'old.rs -> new name.rs', 'é.rs'],
  });
  answers['status --porcelain -z --untracked-files=normal'] = {
    status: 0,
    stdout: 'M\0',
    stderr: '',
  };
  assert.throws(() => checkoutState({ run: git }), /git status printed an entry this cannot read/);
  // The container case: git refuses a repository another user owns.
  answers['rev-parse HEAD'] = {
    status: 128,
    stdout: '',
    stderr: "fatal: detected dubious ownership in repository at '/repo'\n",
  };
  assert.throws(
    () => checkoutState({ run: git }),
    /git rev-parse HEAD failed, so no receipt can be tied to a commit: fatal: detected dubious ownership/,
  );
  answers['rev-parse HEAD'] = { status: 0, stdout: 'abc\n', stderr: '' };
  answers['status --porcelain -z --untracked-files=normal'] = { status: 1, stdout: '', stderr: '' };
  assert.throws(
    () => checkoutState({ run: git }),
    /git status --porcelain -z --untracked-files=normal failed.*: exit 1/,
  );
});

// The real-git tests below read no global or system git configuration, in
// this process (which `checkoutState` runs git from) or any git it starts, so
// a user's `commit.gpgsign` or `status.showUntrackedFiles` cannot change what
// they see. Settings a test needs go in its scratch repository's own
// configuration.
const userGitConfig = {
  GIT_CONFIG_GLOBAL: process.env.GIT_CONFIG_GLOBAL,
  GIT_CONFIG_NOSYSTEM: process.env.GIT_CONFIG_NOSYSTEM,
};
before(() => {
  process.env.GIT_CONFIG_GLOBAL = devNull;
  process.env.GIT_CONFIG_NOSYSTEM = '1';
});
after(() => {
  for (const [name, value] of Object.entries(userGitConfig)) {
    if (value === undefined) delete process.env[name];
    else process.env[name] = value;
  }
});

/** A scratch repository with one commit, git run in it, and a file writer. */
async function scratchRepository() {
  const root = await realpath(await mkdtemp(join(tmpdir(), 'replay-check-git-')));
  const git = (...args: string[]) => {
    const result = spawnSync('git', args, { cwd: root, encoding: 'utf8' });
    assert.equal(result.status, 0, `git ${args.join(' ')}: ${result.stderr}`);
  };
  const put = async (path: string, text = 'x') => {
    await mkdir(dirname(join(root, path)), { recursive: true });
    await writeFile(join(root, path), text);
  };
  git('init', '-q');
  git('config', 'user.name', 'test');
  git('config', 'user.email', 'test@example.invalid');
  await put('crates/a/src/lib.rs', 'fn a() {}');
  git('add', 'crates/a/src/lib.rs');
  git('commit', '-q', '-m', 'one');
  return { root, git, put };
}

test('checkoutState exempts only an untracked receipts directory, in a real repository', async () => {
  const { root, git, put } = await scratchRepository();
  const outside = await realpath(await mkdtemp(join(tmpdir(), 'replay-check-link-')));
  const state = (receipts?: string) =>
    checkoutState({ root, receipts: receipts === undefined ? undefined : join(root, receipts) });
  try {
    assert.equal(state().dirty, false);

    // Untracked receipts inside the repository: exempt when named, a change otherwise.
    await put('saved/r.json');
    assert.deepEqual(state().changes, ['saved/']);
    assert.deepEqual(state('saved').changes, []);

    // Through a symlink from outside the repository, the same directory is still exempt.
    await symlink(join(root, 'saved'), join(outside, 'saved'));
    assert.deepEqual(checkoutState({ root, receipts: join(outside, 'saved') }).changes, []);

    // A directory with tracked files gets no exemption: its edits and new files count.
    await put('crates/a/src/lib.rs', 'fn b() {}');
    await put('crates/a/src/r.json');
    assert.deepEqual(state('crates/a/src').changes, [
      'crates/a/src/lib.rs',
      'crates/a/src/r.json',
      'saved/',
    ]);
    git('checkout', '-q', '--', 'crates/a/src/lib.rs');
    await rm(join(root, 'crates/a/src/r.json'));

    // The pathspec is literal: a directory named `r*` does not hide `rx/`.
    await put('r*/r.json');
    await put('rx/new.rs');
    assert.deepEqual(state('r*').changes, ['rx/', 'saved/']);
    await rm(join(root, 'r*'), { recursive: true });
    await rm(join(root, 'rx'), { recursive: true });

    // Only the `*.json` files directly in it are receipts: any other
    // untracked file there, at any depth, still counts.
    await put('saved/notes.txt');
    await put('saved/sub/r.json');
    await put('saved/r.json.partial');
    assert.deepEqual(state('saved').changes, [
      'saved/notes.txt',
      'saved/r.json.partial',
      'saved/sub/r.json',
    ]);
    await rm(join(root, 'saved'), { recursive: true });
    await put('saved/r.json');

    // Untracked source named as the receipts directory is not exempt.
    await put('crates/a/tests/t.rs');
    assert.deepEqual(state('crates/a/tests').changes, ['crates/a/tests/t.rs', 'saved/']);
    await rm(join(root, 'crates/a/tests'), { recursive: true });

    // A receipts directory inside an untracked parent: git prints only the
    // parent, which is not left out.
    await put('parent/saved/r.json');
    assert.deepEqual(state('parent/saved').changes, ['parent/', 'saved/']);
    await rm(join(root, 'parent'), { recursive: true });

    // Paths with a space and non-ASCII are matched as git stores them.
    await put('my receipts é/r.json');
    assert.deepEqual(state('my receipts é').changes, ['saved/']);
    assert.deepEqual(state().changes, ['my receipts é/', 'saved/']);
    await rm(join(root, 'my receipts é'), { recursive: true });

    // A staged deletion under the directory counts, though git then tracks
    // nothing under it.
    await put('kept/r.json');
    git('add', 'kept/r.json');
    git('commit', '-q', '-m', 'two');
    git('rm', '-r', '-q', '--cached', 'kept');
    assert.deepEqual(state('kept').changes, ['kept/r.json', 'saved/']);

    // The repository root is never exempt.
    assert.deepEqual(state('.').changes, ['kept/r.json', 'kept/', 'saved/']);
  } finally {
    await rm(root, { recursive: true, force: true });
    await rm(outside, { recursive: true, force: true });
  }
});

test('checkoutState lists untracked files the same whatever status.showUntrackedFiles says', async () => {
  const { root, git, put } = await scratchRepository();
  try {
    await put('crates/a/src/new.rs');
    await put('saved/r.json');
    for (const setting of ['no', 'normal', 'all']) {
      git('config', 'status.showUntrackedFiles', setting);
      assert.deepEqual(
        checkoutState({ root }).changes,
        ['crates/a/src/new.rs', 'saved/'],
        `status.showUntrackedFiles=${setting}`,
      );
      assert.deepEqual(
        checkoutState({ root, receipts: join(root, 'saved') }).changes,
        ['crates/a/src/new.rs'],
        `status.showUntrackedFiles=${setting}, receipts in saved/`,
      );
    }
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test('a ledger entry with observedBy that no receipt applied is a problem', () => {
  const ledger = [
    { id: 'A1', observedBy: [{}] },
    { id: 'B2', observedBy: [{}] },
    { id: 'P3' }, // pinnedBy only: nothing to apply
    { id: 'E4', observedBy: [] },
  ];
  const applied = { ...receipt('core/x.json', ['a']), exceptions: ['A1'] };
  assert.deepEqual(unappliedObservations(ledger, [applied, receipt('core/x.json', ['b'])]), [
    'divergences.json: B2: no replay applied any of its observedBy entries',
  ]);
  const both = { ...receipt('core/y.json', ['c']), exceptions: ['B2', 'A1'] };
  assert.deepEqual(unappliedObservations(ledger, [applied, both]), []);
});

test('unapplied observations are not checked when a receipt is filtered', () => {
  assert.deepEqual(
    unappliedObservations([{ id: 'A1', observedBy: [{}] }], [receipt('core/x.json', ['a'], 'a')]),
    [],
  );
});

test('receipts of one passing full-strength run have no run problems', () => {
  const both = [receipt('core/x.json', ['a']), receipt('core/y.json', ['b'])];
  assert.deepEqual(runProblems(both), []);
  assert.deepEqual(runProblems(both, 'run-1'), []);
});

test('a receipt that counts failed cases is a problem', () => {
  assert.deepEqual(runProblems([{ ...receipt('core/x.json', ['a', 'b']), failures: 2 }]), [
    'core/x.json: 2 cases failed in the replay',
  ]);
});

test('a receipt written with the member-order comparison off is a problem', () => {
  assert.deepEqual(runProblems([{ ...receipt('core/x.json', ['a']), memberOrder: false }]), [
    'core/x.json: replayed with the member-order comparison off (PLUGIN_CONFORMANCE_MEMBER_ORDER=0)',
  ]);
});

test('receipts from two runs, or with no run id, are a problem', () => {
  const stale = { ...receipt('core/y.json', ['b']), runId: 'run-0' };
  assert.deepEqual(runProblems([receipt('core/x.json', ['a']), stale]), [
    'receipts come from 2 runs (run-0, run-1), not one',
  ]);
  assert.deepEqual(runProblems([receipt('core/x.json', ['a']), stale], 'run-1'), [
    'core/y.json: receipt is from run run-0, not this run (run-1)',
  ]);
  assert.deepEqual(runProblems([{ ...receipt('core/x.json', ['a']), runId: null }]), [
    'core/x.json: receipt has no run id',
  ]);
});

test('an allow-list path no receipt used is a problem', () => {
  const allow = {
    'core/x.json': {
      a: [{ path: '$.returned' }, { path: '$.returned.never' }],
      gone: [{ path: '$.returned' }],
    },
  };
  const used = {
    ...receipt('core/x.json', ['a']),
    memberOrderAllowed: [{ case: 'a', path: '$.returned' }],
  };
  assert.deepEqual(unusedAllowances(allow, [used]), [
    'member-order-allow.json: core/x.json / a / $.returned.never: no replay needed it (the member orders agree, or the case never ran)',
    'member-order-allow.json: core/x.json / gone / $.returned: no replay needed it (the member orders agree, or the case never ran)',
  ]);
  // The same case and path in another file's receipt does not count.
  const elsewhere = { ...used, file: 'core/y.json' };
  assert.equal(
    unusedAllowances({ 'core/x.json': { a: [{ path: '$.returned' }] } }, [elsewhere]).length,
    1,
  );
  assert.deepEqual(
    unusedAllowances({ 'core/x.json': { a: [{ path: '$.returned' }] } }, [used]),
    [],
  );
});

test('unused allowances are not checked when a receipt is filtered or ran with the order off', () => {
  const allow = { 'core/x.json': { a: [{ path: '$.returned' }] } };
  assert.deepEqual(unusedAllowances(allow, [receipt('core/x.json', ['a'], 'a')]), []);
  assert.deepEqual(
    unusedAllowances(allow, [{ ...receipt('core/x.json', ['a']), memberOrder: false }]),
    [],
  );
});

test('a receipt written with the allowlist replaced is a problem', () => {
  assert.deepEqual(runProblems([{ ...receipt('core/x.json', ['a']), allowListOverridden: true }]), [
    'core/x.json: replayed with the allowlist replaced (PLUGIN_CONFORMANCE_MEMBER_ORDER_ALLOW)',
  ]);
});

test('an allowance a receipt used that the allowlist does not hold is a problem', () => {
  const allow = { 'core/x.json': { a: [{ path: '$.returned' }] } };
  const used = (uses: { case: string; path: string }[]) => ({
    ...receipt('core/x.json', ['a', 'b']),
    memberOrderAllowed: uses,
  });
  assert.deepEqual(unlistedAllowances(allow, [used([{ case: 'a', path: '$.returned' }])]), []);
  assert.deepEqual(
    unlistedAllowances(allow, [
      used([
        { case: 'a', path: '$.returned.more' },
        { case: 'b', path: '$.returned' },
      ]),
    ]),
    [
      'core/x.json: case "a" skipped the member order at $.returned.more, which member-order-allow.json does not allow',
      'core/x.json: case "b" skipped the member order at $.returned, which member-order-allow.json does not allow',
    ],
  );
});

test('receipts from another commit or of other vector text than the checkout are a problem', () => {
  const checkout = {
    head: 'abc',
    dirty: false,
    changes: [],
    saved: false,
    vectorSha256: (file: string) => `sha-${file}`,
  };
  assert.deepEqual(staleProblems([receipt('core/x.json', ['a'])], checkout), []);
  assert.deepEqual(
    staleProblems(
      [
        { ...receipt('core/x.json', ['a']), head: 'old' },
        { ...receipt('core/y.json', ['b']), vectorSha256: 'sha-old' },
        { ...receipt('core/z.json', ['c']), vectorSha256: null },
        // A unit test's receipt, or a run where git failed: no commit.
        {
          ...receipt('core/w.json', ['d']),
          head: null,
          dirty: null,
          vectorSha256: 'sha-core/w.json',
        },
      ],
      checkout,
    ),
    [
      'core/x.json: receipt is from commit old, the checkout is at abc',
      'core/y.json: receipt replayed other vector text than the checkout holds',
      'core/z.json: receipt has no vector hash',
      'core/w.json: receipt names no commit (PLUGIN_CONFORMANCE_HEAD was not set)',
      '1 receipts do not say whether the tree had uncommitted changes (PLUGIN_CONFORMANCE_DIRTY was not set): core/w.json',
    ],
  );
  // A vector file the checkout no longer has.
  assert.deepEqual(
    staleProblems([receipt('core/x.json', ['a'])], { ...checkout, vectorSha256: () => null }),
    ['core/x.json: receipt replayed other vector text than the checkout holds'],
  );
});

test('a receipt from a tree with uncommitted changes passes only for this run', () => {
  const vectorSha256 = (file: string) => `sha-${file}`;
  const files = ['core/a.json', 'core/b.json', 'core/c.json', 'core/d.json'];
  const clean = files.map((file) => receipt(file, ['a']));
  const dirty = clean.map((r) => ({ ...r, dirty: true }));
  const changes = ['a.rs', 'b.rs', 'c.rs', 'd.rs', 'e.rs'];
  const at = (tree: boolean, saved: boolean) => ({
    head: 'abc',
    dirty: tree,
    changes: tree ? changes : [],
    saved,
    vectorSha256,
  });
  // This run's own receipts describe the tree it ran on, changes and all.
  assert.deepEqual(staleProblems(dirty, at(true, false)), []);
  assert.deepEqual(staleProblems(clean, at(false, true)), []);
  // One line for every receipt, not one per receipt.
  assert.deepEqual(staleProblems(dirty, at(false, false)), [
    '4 receipts are from a tree with uncommitted changes, and the checkout has none: core/a.json, core/b.json, core/c.json and 1 more',
  ]);
  assert.deepEqual(staleProblems(clean, at(true, true)), [
    'the checkout has uncommitted changes (a.rs, b.rs, c.rs and 2 more), so 4 receipts from a clean tree are not its proof',
  ]);
  // Saved receipts cannot show their changes are the checkout's.
  assert.deepEqual(staleProblems(dirty, at(true, true)), [
    'the checkout has uncommitted changes (a.rs, b.rs, c.rs and 2 more), and nothing ties them to the 4 receipts in the --receipts directory, which were written with uncommitted changes too',
  ]);
});
