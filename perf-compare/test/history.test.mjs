import { test } from 'node:test';
import assert from 'node:assert/strict';
import { armFor, eligible, intervalTargets, selectIntervalPoints, seriesData } from '../lib/history.mjs';

const A = 'a'.repeat(40);
const B = 'b'.repeat(40);
const C = 'c'.repeat(40);

test('intervalTargets steps from the start date and stops at the end', () => {
  assert.deepEqual(intervalTargets('2026-09-29', '2026-10-14', 7), ['2026-09-29', '2026-10-06', '2026-10-13']);
  assert.deepEqual(intervalTargets('2026-09-29', '2026-09-28', 7), []);
});

test('a target takes the latest commit on or before its day, in UTC', () => {
  const commits = [
    { sha: A, date: '2026-10-05T23:00:00Z' },
    { sha: B, date: '2026-10-06T22:00:00-05:00' }, // 2026-10-07 in UTC: after the target day
    { sha: C, date: '2026-10-06T10:00:00+02:00' },
  ];
  const got = selectIntervalPoints({ commits, existing: [], from: '2026-10-06', to: '2026-10-06', days: 7 });
  assert.deepEqual(got, [{ sha: C, date: '2026-10-06', target: '2026-10-06' }]);
});

test('a target with a point within a day of it adds nothing', () => {
  const commits = [{ sha: A, date: '2026-10-06T12:00:00Z' }];
  const args = { commits, from: '2026-10-06', to: '2026-10-06', days: 7 };
  assert.deepEqual(selectIntervalPoints({ ...args, existing: ['2026-10-05'] }), []);
  assert.deepEqual(selectIntervalPoints({ ...args, existing: ['2026-10-07'] }), []);
  assert.equal(selectIntervalPoints({ ...args, existing: ['2026-10-04'] }).length, 1);
});

test('a target with no commit before it adds nothing', () => {
  const commits = [{ sha: A, date: '2026-10-08T12:00:00Z' }];
  assert.deepEqual(selectIntervalPoints({ commits, existing: [], from: '2026-10-06', to: '2026-10-06', days: 7 }), []);
});

test('a commit is chosen once, and not when it sits next to an existing point', () => {
  const commits = [{ sha: A, date: '2026-10-01T12:00:00Z' }];
  const quiet = selectIntervalPoints({ commits, existing: [], from: '2026-10-06', to: '2026-10-20', days: 7 });
  assert.deepEqual(quiet.map((p) => p.target), ['2026-10-06']);
  const beside = selectIntervalPoints({ commits, existing: ['2026-09-30'], from: '2026-10-06', to: '2026-10-06', days: 7 });
  assert.deepEqual(beside, []);
});

const config = {
  worktrees: {
    main: { sha: A, recipe: 'typescript' },
    lto: { sha: B, recipe: 'rust-kernel-lto' },
    plain: { sha: B, recipe: 'rust-kernel' },
  },
  arms: {
    main: { worktree: 'main', kernel: 'typescript', rustReads: false },
    'b-lto': { worktree: 'lto', kernel: 'rust', rustReads: false },
    'b-reads': { worktree: 'plain', kernel: 'rust', rustReads: true },
    b: { worktree: 'plain', kernel: 'rust', rustReads: false },
  },
};

test('armFor reuses an arm with the same commit, recipe and kernel', () => {
  assert.equal(armFor(config, { sha: B, recipe: 'rust-kernel', kernel: 'rust' }), 'b');
  assert.equal(armFor(config, { sha: A, recipe: 'typescript', kernel: 'typescript' }), 'main');
  assert.equal(armFor(config, { sha: C, recipe: 'rust-kernel', kernel: 'rust' }), null);
});

const want = { machine: { host: 'mini', cpuModel: 'M1' }, reference: 'main', noise: 'main-aa' };
const goodMeta = {
  host: 'mini',
  cpuModel: 'M1',
  profile: 'desktop',
  quick: false,
  rounds: 10,
  harnessDirty: false,
  armsRun: ['main', 'main-aa', 'x'],
  arms: { main: { sha: A }, 'main-aa': { sha: A }, x: { sha: B } },
  sizes: { main: { jsBytes: 100 }, 'main-aa': { jsBytes: 100 }, x: { jsBytes: 250 } },
};
const goodGate = { main: { pass: true }, 'main-aa': { pass: true }, x: { pass: true } };

test('eligible passes a full run on the history machine', () => {
  assert.deepEqual(eligible(goodMeta, goodGate, want), []);
});

test('eligible gives one reason per failed condition', () => {
  const bad = { ...goodMeta, host: 'laptop', cpuModel: 'M4', profile: 'slow', quick: true, rounds: 3, harnessDirty: true, armsRun: ['main', 'x'] };
  const reasons = eligible(bad, { main: { pass: false, reason: 'pages' } }, want);
  assert.equal(reasons.length, 8, reasons.join('\n'));
  assert.equal(eligible({ ...goodMeta, rounds: 9 }, goodGate, want).length, 1);
});

const sample = (arm, round, wallMs) => ({ arm, flow: 'scroll', fixture: 'large', round, metrics: { wallMs } });
const samplesFor = (xScale) =>
  [100, 101, 99, 100, 102, 100, 101, 100, 99, 101].flatMap((v, i) => [
    sample('main', i, v),
    sample('main-aa', i, v + 0.5),
    sample('x', i, v * xScale),
  ]);
const run = (runId, xScale, over = {}) => ({ meta: { ...goodMeta, runId, ...over }, gate: goodGate, samples: samplesFor(xScale) });
const points = [{ series: 's', sha: B, date: '2026-10-01', why: 'milestone', note: 'n', arm: 'x' }];
const base = { points, reference: 'main', noise: 'main-aa', series: { s: { label: 'S' } } };

test('seriesData compares a point with the reference of its own run', () => {
  const data = seriesData({ ...base, runs: [run('r1', 2)] });
  assert.equal(data.points.length, 1);
  assert.equal(data.points[0].runId, 'r1');
  const row = data.rows.find((r) => r.key === 'scroll|large|wallMs');
  assert.equal(row.kind, 'ratio');
  assert.equal(row.unit, 'ms');
  assert.equal(row.better, 'lower');
  const [v] = row.values;
  assert.equal(v.point, 0);
  assert.equal(v.n, 10);
  assert.equal(v.rel, 2);
  assert.equal(v.verdict, 'slower');
  assert.ok(v.lo < 2 && v.hi > 2 && v.noise < 0.05, JSON.stringify(v));
});

test('seriesData takes the latest run that measured a point', () => {
  const data = seriesData({ ...base, runs: [run('r1', 2), run('r2', 3)] });
  assert.equal(data.points[0].runId, 'r2');
  assert.equal(data.rows[0].values[0].rel, 3);
});

test('seriesData ignores a run whose arm was built from another commit, or failed the gate', () => {
  const other = run('r2', 3, { arms: { ...goodMeta.arms, x: { sha: C } } });
  assert.equal(seriesData({ ...base, runs: [run('r1', 2), other] }).points[0].runId, 'r1');
  const failed = { ...run('r3', 3), gate: { ...goodGate, x: { pass: false } } };
  assert.equal(seriesData({ ...base, runs: [run('r1', 2), failed] }).points[0].runId, 'r1');
  assert.deepEqual(seriesData({ ...base, runs: [other] }).points, []);
});

test('a row the reference arm did not run keeps its median and has no ratio', () => {
  const r = run('r1', 2);
  r.samples.push({ arm: 'x', flow: 'undo', fixture: 'annots', round: 0, metrics: { wallMs: 40 } });
  const row = seriesData({ ...base, runs: [r] }).rows.find((x) => x.key === 'undo|annots|wallMs');
  assert.equal(row.values[0].median, 40);
  assert.equal(row.values[0].rel, null);
});

test('sizes become rows without an interval', () => {
  const row = seriesData({ ...base, runs: [run('r1', 2)] }).rows.find((x) => x.key === 'size||jsBytes');
  assert.equal(row.flow, 'size');
  assert.deepEqual(row.values, [{ point: 0, n: 1, median: 250, ref: 100, rel: 2.5, lo: null, hi: null, noise: null, verdict: null }]);
});

test('points come out in date order and skipped points are left out', () => {
  const two = [
    { series: 's', sha: B, date: '2026-10-08', why: 'interval', arm: 'x' },
    { series: 's', sha: A, date: '2026-10-01', why: 'milestone', arm: 'main' },
    { series: 's', sha: A, date: '2026-09-01', why: 'milestone', arm: 'main-aa', skip: 'does not build' },
  ];
  const data = seriesData({ ...base, points: two, runs: [run('r1', 2)] });
  assert.deepEqual(data.points.map((p) => p.date), ['2026-10-01', '2026-10-08']);
  assert.equal(data.rows[0].values.find((v) => v.point === 0).rel, 1);
});
