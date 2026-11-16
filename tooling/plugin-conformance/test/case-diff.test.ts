import assert from 'node:assert/strict';
import { test } from 'node:test';

import { caseDiff, describeCaseDiff, type RenderedSuite } from '../src/case-diff';

function suite(...cases: { name: string; [member: string]: unknown }[]): RenderedSuite {
  return { cases };
}

const a = { name: 'a', fn: 'f', args: [1], outcome: { returned: 1 } };
const b = { name: 'b', fn: 'f', args: [2], outcome: { returned: 2 } };

test('a suite with no file before adds every case', () => {
  assert.deepEqual(caseDiff(null, suite(a, b)), { added: ['a', 'b'], removed: [], changed: [] });
});

test('cases are added and removed by name', () => {
  const c = { ...b, name: 'c' };
  assert.deepEqual(caseDiff(suite(a, b), suite(a, c)), {
    added: ['c'],
    removed: ['b'],
    changed: [],
  });
});

test('a case whose trace differs is changed', () => {
  const before = { name: 's', steps: [], trace: [{ kind: 'state', value: 1 }] };
  const after = { name: 's', steps: [], trace: [{ kind: 'state', value: 2 }] };
  assert.deepEqual(caseDiff(suite(before, a), suite(after, a)), {
    added: [],
    removed: [],
    changed: ['s'],
  });
});

// `canonicalKey` sorts object keys before it stringifies, so a case whose
// members only moved is the same case. Array order still counts.
test('member reordering alone is not a change, because canonicalKey sorts object keys', () => {
  const reordered = { outcome: { returned: 1 }, args: [1], fn: 'f', name: 'a' };
  assert.deepEqual(caseDiff(suite(a), suite(reordered)), { added: [], removed: [], changed: [] });
  const swapped = { ...a, args: [[2, 1]] };
  assert.deepEqual(caseDiff(suite({ ...a, args: [[1, 2]] }), suite(swapped)), {
    added: [],
    removed: [],
    changed: ['a'],
  });
});

test('the summary line counts each list and names changed cases', () => {
  assert.equal(
    describeCaseDiff('core/x.json', {
      added: ['n1', 'n2'],
      removed: [],
      changed: ['a', 'b', 'c', 'd', 'e'],
    }),
    'core/x.json: +2 -0 ~5 (changed: a, b, c, d, e)',
  );
});

test('the summary names at most ten changed cases', () => {
  const changed = Array.from({ length: 13 }, (_, i) => `c${i}`);
  assert.equal(
    describeCaseDiff('core/x.json', { added: [], removed: ['r'], changed }),
    'core/x.json: +0 -1 ~13 (changed: c0, c1, c2, c3, c4, c5, c6, c7, c8, c9, and 3 more)',
  );
});

test('a file with no entry has no summary line', () => {
  assert.equal(describeCaseDiff('core/x.json', { added: [], removed: [], changed: [] }), null);
  assert.equal(
    describeCaseDiff('core/x.json', { added: ['a'], removed: [], changed: [] }),
    'core/x.json: +1 -0 ~0',
  );
});
