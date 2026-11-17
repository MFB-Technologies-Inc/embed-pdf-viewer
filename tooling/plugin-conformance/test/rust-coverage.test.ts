import assert from 'node:assert/strict';
import { test } from 'node:test';

import {
  childEnv,
  lineCounts,
  reachedPackages,
  testModuleLines,
  unitOnly,
  type LlvmCovExport,
  type Segment,
} from '../src/checks/rust-coverage';

const root = '/work/repo';

/** A segment as `llvm-cov export` writes it: line, column, count, has
 *  count, region entry, gap region. */
const seg = (line: number, col: number, count: number, entry = true, gap = false): Segment => [
  line,
  col,
  count,
  true,
  entry,
  gap,
];
/** The end of a region: no count, not an entry. */
const end = (line: number, col: number): Segment => [line, col, 0, false, false, false];

function cov(files: Record<string, Segment[]>): LlvmCovExport {
  return {
    data: [
      {
        files: Object.entries(files).map(([path, segments]) => ({
          filename: `${root}/${path}`,
          segments,
        })),
      },
    ],
  };
}

test('lineCounts gives each line the count llvm-cov does', () => {
  // A function on lines 1-6 whose branch from 3:5 to 5:2 never ran. Line 3
  // keeps the count wrapped into it; line 5, where only the branch's end
  // falls, takes the branch's count, as `llvm-cov export -format=lcov` does.
  const counts = lineCounts([seg(1, 1, 5), seg(3, 5, 0), seg(5, 2, 5, false), end(6, 2)]);
  assert.deepEqual(
    [...counts.entries()],
    [
      [1, 5],
      [2, 5],
      [3, 5],
      [4, 0],
      [5, 0],
      [6, 5],
    ],
  );
});

test('lineCounts leaves a skipped region and a gap-only line unmapped', () => {
  const skipped: Segment = [2, 1, 0, false, true, false];
  const counts = lineCounts([
    seg(1, 1, 1),
    end(1, 9),
    skipped,
    end(3, 1),
    seg(5, 1, 0, true, true),
    end(5, 4),
  ]);
  assert.deepEqual([...counts.keys()], [1]);
});

test('unitOnly lists the lines all tests hit and replay does not, as ranges', () => {
  const replay = cov({
    'crates/plugins/a/src/lib.rs': [seg(1, 1, 1), end(2, 9), seg(3, 1, 0), seg(6, 1, 1), end(9, 1)],
  });
  const all = cov({
    'crates/plugins/a/src/lib.rs': [seg(1, 1, 2), end(2, 9), seg(3, 1, 4), seg(6, 1, 2), end(9, 1)],
  });
  assert.deepEqual(unitOnly(replay, all, { root, exclude: [] }), {
    total: { replayLines: '6/9', unitOnlyLines: 3, files: 1, excludedFiles: 0 },
    files: {
      'crates/plugins/a/src/lib.rs': { replayLines: '6/9', unitOnlyLines: ['3-5'] },
    },
  });
});

test('unitOnly counts a file replay never loaded as reached nowhere', () => {
  const all = cov({
    'crates/plugins/b/src/lib.rs': [
      seg(1, 1, 1),
      end(1, 9),
      seg(2, 1, 0),
      end(2, 9),
      seg(3, 1, 1),
      end(3, 9),
    ],
  });
  assert.deepEqual(unitOnly(cov({}), all, { root, exclude: [] }).files, {
    'crates/plugins/b/src/lib.rs': { replayLines: '0/3', unitOnlyLines: ['1', '3'] },
  });
});

test('unitOnly drops excluded files before the totals and sorts the rest', () => {
  const file = [seg(1, 1, 1), end(2, 1)];
  const all = cov({
    'crates/plugins/z/src/lib.rs': file,
    'crates/plugins/a/src/harness.rs': file,
    'crates/plugins/a/src/ffi/mod.rs': file,
    'crates/plugins/a/src/lib.rs': file,
  });
  const result = unitOnly(cov({}), all, {
    root,
    exclude: [
      { glob: 'crates/plugins/*/src/harness.rs', reason: 'replay adapter' },
      { glob: 'crates/plugins/*/src/ffi/**', reason: 'C ABI glue' },
    ],
  });
  assert.deepEqual(Object.keys(result.files), [
    'crates/plugins/a/src/lib.rs',
    'crates/plugins/z/src/lib.rs',
  ]);
  assert.deepEqual(result.total, {
    replayLines: '0/4',
    unitOnlyLines: 4,
    files: 2,
    excludedFiles: 2,
  });
});

test('testModuleLines spans each cfg(test) module, braces in strings, chars and comments aside', () => {
  const source = [
    'fn real() {}', // 1
    '#[cfg(test)]', // 2
    'mod tests {', // 3
    '    // a } in a comment', // 4
    '    const S: &str = "}{";', // 5
    '    const R: &str = r#"', // 6
    '}', // 7
    '"#;', // 8
    "    fn f<'a>(_: &'a str) -> char { '}' }", // 9
    '}', // 10
    'fn after() {}', // 11
    '#[cfg(test)]', // 12
    'pub(crate) mod more { /* { */ }', // 13
  ].join('\n');
  assert.deepEqual([...testModuleLines(source)], [2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 13]);
});

test('testModuleLines throws on a module it cannot close', () => {
  assert.throws(() => testModuleLines('#[cfg(test)]\nmod tests {\n'), /never closes/);
});

test('unitOnly leaves the lines of test modules out of every count', () => {
  const replay = cov({
    'crates/plugins/c/src/lib.rs': [seg(1, 1, 1), end(1, 9), seg(2, 1, 0), end(4, 9)],
  });
  const all = cov({
    'crates/plugins/c/src/lib.rs': [seg(1, 1, 1), end(1, 9), seg(2, 1, 1), end(4, 9)],
  });
  const result = unitOnly(replay, all, { root, exclude: [], testLines: () => new Set([3, 4]) });
  assert.deepEqual(result.files, {
    'crates/plugins/c/src/lib.rs': { replayLines: '1/2', unitOnlyLines: ['2'] },
  });
});

test('childEnv removes the filter, order, allowlist and receipt variables', () => {
  assert.deepEqual(
    childEnv({
      PATH: '/bin',
      PLUGIN_CONFORMANCE_ONLY: 'open-all',
      PLUGIN_CONFORMANCE_MEMBER_ORDER: '0',
      PLUGIN_CONFORMANCE_MEMBER_ORDER_ALLOW: '/tmp/allow.json',
      PLUGIN_CONFORMANCE_RECEIPTS: '/tmp/old',
    }),
    { PATH: '/bin' },
  );
});

test('reachedPackages follows normal, non-optional dependencies between workspace members', () => {
  const pkg = (name: string, deps: [string, string | null, boolean?][] = []) => ({
    id: `${name}-id`,
    name,
    dependencies: deps.map(([dep, kind, optional = false]) => ({ name: dep, kind, optional })),
  });
  const metadata = {
    workspace_members: ['plugin-id', 'kernel-id', 'geometry-id', 'helper-id', 'tests-id', 'ffi-id'],
    packages: [
      pkg('plugin', [
        ['kernel', null],
        ['serde', null],
        ['tests', 'dev'],
        ['helper', 'build'],
        ['ffi', null, true],
      ]),
      pkg('kernel', [['geometry', null]]),
      pkg('geometry'),
      pkg('helper'),
      pkg('tests'),
      pkg('serde'),
      pkg('ffi'),
    ],
  };
  assert.deepEqual(reachedPackages(metadata, ['plugin']), ['geometry', 'kernel', 'plugin']);
});
