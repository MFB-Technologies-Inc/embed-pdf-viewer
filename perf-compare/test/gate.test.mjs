import { test } from 'node:test';
import assert from 'node:assert/strict';
import { sameAnswer } from '../lib/gate.mjs';

// From the gate of results/2026-11-16T20-06-31-783Z (main against plugins-next).
const OLD = 'Pay through the nose for a commercial\r\nPDF SDK that locks you into their\r\necosystem';
const NEW = 'Pay through the nose for a commercial \nPDF SDK that locks you into their \necosystem';

test('a selection matches across the two line-break encodings', () => {
  assert.ok(sameAnswer('selection', NEW, OLD));
});

test('a selection with different words still fails', () => {
  assert.ok(!sameAnswer('selection', NEW.replace('their', 'your'), OLD));
  assert.ok(!sameAnswer('selection', 'Pay through the nose for a commercial PDF', OLD));
});

test('other answers are compared exactly', () => {
  assert.ok(sameAnswer('searchHits', 12, 12));
  assert.ok(!sameAnswer('searchHits', 12, 13));
  assert.ok(!sameAnswer('largePageCount', '1 \n', '1\r\n'));
});
