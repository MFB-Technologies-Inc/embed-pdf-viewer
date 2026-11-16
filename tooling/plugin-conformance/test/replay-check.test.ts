import assert from 'node:assert/strict';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';

import {
  compareReceipts,
  readReceipts,
  replayPackages,
  type CargoMetadata,
  type Receipt,
} from '../src/replay-check';

function receipt(file: string, ran: string[], only: string | null = null): Receipt {
  return { file, only, ran, exceptions: [], memberOrderAllowed: [] };
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
    const read = await readReceipts(dir);
    assert.deepEqual(
      read.receipts.map((r) => r.file),
      ['core/x.json'],
    );
    assert.equal(read.problems.length, 3);
    for (const name of ['bad-only.json', 'no-ran.json', 'not-json.json']) {
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
