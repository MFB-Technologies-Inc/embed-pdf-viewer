import assert from 'node:assert/strict';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';

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
    { head: 'head-2', dirty: false },
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
  });
  assert.equal(dirty.PLUGIN_CONFORMANCE_DIRTY, '1');
});

test('checkoutState reads the commit and uncommitted changes, and fails with git', () => {
  const answers: Record<string, { status: number; stdout: string; stderr: string }> = {
    'rev-parse HEAD': { status: 0, stdout: 'abc\n', stderr: '' },
    'status --porcelain': { status: 0, stdout: '', stderr: '' },
  };
  const git = (args: readonly string[]) => answers[args.join(' ')];
  assert.deepEqual(checkoutState(git), { head: 'abc', dirty: false });
  answers['status --porcelain'] = { status: 0, stdout: '?? new.rs\n', stderr: '' };
  assert.deepEqual(checkoutState(git), { head: 'abc', dirty: true });
  // The container case: git refuses a repository another user owns.
  answers['rev-parse HEAD'] = {
    status: 128,
    stdout: '',
    stderr: "fatal: detected dubious ownership in repository at '/repo'\n",
  };
  assert.throws(
    () => checkoutState(git),
    /git rev-parse HEAD failed, so no receipt can be tied to a commit: fatal: detected dubious ownership/,
  );
  answers['rev-parse HEAD'] = { status: 0, stdout: 'abc\n', stderr: '' };
  answers['status --porcelain'] = { status: 1, stdout: '', stderr: '' };
  assert.throws(() => checkoutState(git), /git status --porcelain failed.*: exit 1/);
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
        { ...receipt('core/w.json', ['d']), head: null, dirty: null, vectorSha256: 'sha-core/w.json' },
      ],
      checkout,
    ),
    [
      'core/x.json: receipt is from commit old, the checkout is at abc',
      'core/y.json: receipt replayed other vector text than the checkout holds',
      'core/z.json: receipt has no vector hash',
      'core/w.json: receipt names no commit (PLUGIN_CONFORMANCE_HEAD was not set)',
      'core/w.json: receipt does not say whether the tree had uncommitted changes (PLUGIN_CONFORMANCE_DIRTY was not set)',
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
  const clean = receipt('core/x.json', ['a']);
  const dirty = { ...clean, dirty: true };
  const at = (tree: boolean, saved: boolean) => ({ head: 'abc', dirty: tree, saved, vectorSha256 });
  // This run's own receipts describe the tree it ran on, changes and all.
  assert.deepEqual(staleProblems([dirty], at(true, false)), []);
  assert.deepEqual(staleProblems([clean], at(false, true)), []);
  assert.deepEqual(staleProblems([dirty], at(false, false)), [
    'core/x.json: receipt is from a tree with uncommitted changes, the checkout has none',
  ]);
  assert.deepEqual(staleProblems([clean], at(true, true)), [
    'core/x.json: receipt is from a clean tree, the checkout has uncommitted changes',
  ]);
  // Saved receipts cannot show their changes are the checkout's.
  assert.deepEqual(staleProblems([dirty], at(true, true)), [
    'core/x.json: receipt is from a tree with uncommitted changes, which a --receipts directory cannot tie to the checkout',
  ]);
});
