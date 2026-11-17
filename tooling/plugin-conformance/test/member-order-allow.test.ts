import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';

// Before any check module: see `src/library-guard.ts`.
import '../src/as-library';

import { childEnv, findCandidates, type Paths } from '../src/checks/member-order-allow';

/** Every member-order difference the fake replay has, by file and case. */
const FAILING: Paths = {
  'f.json': { c: ['$.a', '$.b[1]'] },
  'g.json': { d: ['$.x'] },
};

/** A fake cargo run: like the Rust comparer, it reports the first
 *  difference of each case that the override file does not allow. */
function fakeRun(overrideFile: string, extra = '') {
  const allowed = JSON.parse(readFileSync(overrideFile, 'utf8')) as Paths;
  const lines: string[] = [];
  for (const [file, cases] of Object.entries(FAILING)) {
    const failing: string[] = [];
    for (const [name, paths] of Object.entries(cases)) {
      const first = paths.find(
        (p) => !(allowed[file]?.[name] ?? []).includes(p.replace(/\[\d+\]/g, '[]')),
      );
      if (first)
        failing.push(`  ${name}: step 1: ${first}: member order: expected [p, q], got [q, p]`);
    }
    if (failing.length)
      lines.push(`${file}: ${failing.length} of 3 cases differ from the TypeScript:`, ...failing);
  }
  if (extra) lines.push('g.json: 1 of 3 cases differ from the TypeScript:', extra);
  return { output: lines.join('\n'), status: lines.length ? 101 : 0 };
}

async function fixture(allow: unknown) {
  const dir = await mkdtemp(join(tmpdir(), 'member-order-allow-test-'));
  const allowFile = join(dir, 'member-order-allow.json');
  const text = JSON.stringify(allow, null, 2) + '\n';
  await writeFile(allowFile, text);
  return { dir, allowFile, text, candidatesFile: join(dir, 'member-order-candidates.json') };
}

const ALLOW = { 'f.json': { c: [{ path: '$.a', divergence: 'MO-ui-group-options' }] } };

test('a run writes the paths outside the allowlist as candidates and leaves the allowlist bytes unchanged', async () => {
  const { dir, allowFile, text, candidatesFile } = await fixture(ALLOW);
  try {
    const found = findCandidates({
      allowFile,
      candidatesFile,
      fresh: false,
      run: (o) => fakeRun(o),
      log: () => {},
    });
    assert.deepEqual(found.candidates, { 'f.json': { c: ['$.b[]'] }, 'g.json': { d: ['$.x'] } });
    assert.equal(await readFile(allowFile, 'utf8'), text);
    assert.deepEqual(JSON.parse(await readFile(candidatesFile, 'utf8')), {
      'f.json': { c: [{ path: '$.b[]', divergence: '' }] },
      'g.json': { d: [{ path: '$.x', divergence: '' }] },
    });
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

test('--fresh starts from no allowance and names allowlist entries nothing needs', async () => {
  const { dir, allowFile, text, candidatesFile } = await fixture({
    ...ALLOW,
    'h.json': { e: [{ path: '$.gone', divergence: 'MO-ui-group-options' }] },
  });
  try {
    const found = findCandidates({
      allowFile,
      candidatesFile,
      fresh: true,
      run: (o) => fakeRun(o),
      log: () => {},
    });
    assert.deepEqual(found.candidates, { 'f.json': { c: ['$.b[]'] }, 'g.json': { d: ['$.x'] } });
    assert.deepEqual(found.unneeded, { 'h.json': { e: ['$.gone'] } });
    assert.equal(await readFile(allowFile, 'utf8'), text);
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

test('no candidates writes an empty candidates file', async () => {
  const { dir, allowFile, candidatesFile } = await fixture({
    'f.json': { c: ['$.a', '$.b[]'].map((path) => ({ path, divergence: 'MO-ui-group-options' })) },
    'g.json': { d: [{ path: '$.x', divergence: 'MO-ui-group-options' }] },
  });
  try {
    const found = findCandidates({
      allowFile,
      candidatesFile,
      fresh: true,
      run: (o) => fakeRun(o),
      log: () => {},
    });
    assert.deepEqual(found, { candidates: {}, unneeded: {} });
    assert.deepEqual(JSON.parse(await readFile(candidatesFile, 'utf8')), {});
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

test('a failure that is not member order stops the run and leaves the allowlist alone', async () => {
  const { dir, allowFile, text, candidatesFile } = await fixture(ALLOW);
  try {
    assert.throws(
      () =>
        findCandidates({
          allowFile,
          candidatesFile,
          fresh: false,
          run: (o) => fakeRun(o, '  d: step 2: $.y: expected 1, got 2'),
          log: () => {},
        }),
      /aren't member order[\s\S]*g\.json: d: step 2: \$\.y: expected 1, got 2/,
    );
    assert.equal(await readFile(allowFile, 'utf8'), text);
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
});

test('childEnv turns order on, sets the override and removes the filter and receipts', () => {
  assert.deepEqual(
    childEnv(
      {
        PATH: '/bin',
        PLUGIN_CONFORMANCE_ONLY: 'open-all',
        PLUGIN_CONFORMANCE_MEMBER_ORDER: '0',
        PLUGIN_CONFORMANCE_MEMBER_ORDER_ALLOW: '/tmp/allow.json',
        PLUGIN_CONFORMANCE_RECEIPTS: '/tmp/old',
      },
      '/tmp/override.json',
    ),
    {
      PATH: '/bin',
      PLUGIN_CONFORMANCE_MEMBER_ORDER: '1',
      PLUGIN_CONFORMANCE_MEMBER_ORDER_ALLOW: '/tmp/override.json',
    },
  );
});
