/**
 * How `compare.mjs` reads two builds' answer digests (`answers.mjs`).
 *
 *   node --test tooling/engine-timer/answers.test.mjs
 */
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { compareAnswers } from './answers.mjs';

const byRow = (rows) =>
  Object.fromEntries(Object.entries(rows).map(([row, values]) => [row, new Set(values)]));

describe('compareAnswers', () => {
  it('passes builds that answer every row the same', () => {
    const result = compareAnswers(byRow({ 'large/open': ['q1'] }), byRow({ 'large/open': ['r1'] }));
    assert.deepEqual(result, { differing: [], belowQuantum: [] });
  });

  it('passes a raw-only difference and names its row', () => {
    const result = compareAnswers(
      byRow({ 'annots/annotations.list': ['q1'], 'large/open': ['q2'] }),
      byRow({ 'annots/annotations.list': ['r1', 'r2'], 'large/open': ['r3'] }),
    );
    assert.deepEqual(result, { differing: [], belowQuantum: ['annots/annotations.list'] });
  });

  it('stops on a difference at the quantum, and does not also call it a raw-only one', () => {
    const result = compareAnswers(
      byRow({ 'large/pages.geometry': ['q1', 'q2'] }),
      byRow({ 'large/pages.geometry': ['r1', 'r2'] }),
    );
    assert.deepEqual(result, { differing: ['large/pages.geometry'], belowQuantum: [] });
  });
});
