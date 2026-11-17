import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { bootstrapDifference, bootstrapRatio, noiseBand } from '../lib/stats.mjs';

const REPORT = fileURLToPath(new URL('../report.mjs', import.meta.url));
const FLOOR = 0.03;

// Three rows: a noisy ratio row whose A/A band is wider than the 3% floor, a
// quiet ratio row where the floor wins, and a difference row (settleMs).
const base = {
  loadMs: [100, 110, 90, 105, 95, 100, 108, 92, 103, 97, 100, 100],
  paintMs: [100, 100.2, 99.8, 100.1, 99.9, 100, 100.3, 99.7, 100, 100.1, 99.9, 100],
  openSettleMs: [50, 51, 49, 50.5, 49.5, 50, 51.5, 48.5, 50, 50.2, 49.8, 50],
};
const shuffle = (xs) => [...xs.slice(6), ...xs.slice(0, 6)];
const arms = {
  main: base,
  'main-aa': Object.fromEntries(Object.entries(base).map(([m, xs]) => [m, shuffle(xs)])),
  'plugins-next': Object.fromEntries(Object.entries(base).map(([m, xs]) => [m, xs.map((x) => x * 1.1)])),
};

function fixtureRun({ extra = () => {} } = {}) {
  const dir = mkdtempSync(path.join(tmpdir(), 'report-test-'));
  const armMeta = { ref: 'r', sha: '0'.repeat(40), payload: 'p', engineWasmSha256: 'e'.repeat(64) };
  const meta = {
    runId: 'fixture',
    rounds: 12,
    profile: 'desktop',
    throttleRate: 1,
    quick: false,
    memorySource: 'cdp',
    harnessCommit: 'abc',
    harnessDirty: false,
    browserVersion: 'x',
    playwrightVersion: 'x',
    nodeVersion: 'x',
    os: 'x',
    cpuModel: 'x',
    cpuCores: 1,
    totalMemoryBytes: 2 ** 30,
    powerSource: 'ac',
    viewport: { width: 1, height: 1 },
    deviceScaleFactor: 1,
    fixtures: { small: 'f'.repeat(64) },
    arms: Object.fromEntries(Object.keys(arms).map((id) => [id, armMeta])),
    armsRun: Object.keys(arms),
    sizes: {},
  };
  const gate = Object.fromEntries(Object.keys(arms).map((id) => [id, { pass: true }]));
  const samples = [];
  for (const [arm, metrics] of Object.entries(arms)) {
    for (let i = 0; i < 12; i++) {
      samples.push({ arm, flow: 'open', fixture: 'small', metrics: Object.fromEntries(Object.entries(metrics).map(([m, xs]) => [m, xs[i]])) });
    }
  }
  extra({ meta, gate, samples });
  writeFileSync(path.join(dir, 'meta.json'), JSON.stringify(meta));
  writeFileSync(path.join(dir, 'gate.json'), JSON.stringify(gate));
  writeFileSync(path.join(dir, 'samples.jsonl'), samples.map((s) => JSON.stringify(s)).join('\n') + '\n');
  return dir;
}

function runReport(...args) {
  return spawnSync(process.execPath, [REPORT, ...args], { encoding: 'utf8' });
}

// The table under `heading`, as { head, rows }.
function section(text, heading) {
  const lines = text.split('\n');
  const start = lines.indexOf(heading);
  assert.notEqual(start, -1, `no section ${heading}`);
  const tableLines = [];
  for (const line of lines.slice(start + 1)) {
    if (line.startsWith('## ')) break;
    if (line.startsWith('|')) tableLines.push(line);
  }
  const split = (l) => l.slice(1, -1).split('|').map((c) => c.trim());
  return { head: split(tableLines[0]), rows: tableLines.slice(2).map(split) };
}

test('threshold column shows max(FLOOR, noise) per row', () => {
  const dir = fixtureRun();
  try {
    const out = runReport(dir);
    assert.equal(out.status, 0, out.stderr);
    const { head, rows } = section(out.stdout, '## `main` vs `plugins-next`');
    const col = head.indexOf('threshold');
    assert.notEqual(col, -1, `no threshold column in ${head.join(' | ')}`);
    const printed = Object.fromEntries(rows.map((r) => [r[0], r[col]]));

    const ratioRow = (metric) => {
      const noise = noiseBand(bootstrapRatio(base[metric], arms['main-aa'][metric]));
      return { noise, expected: `±${(Math.max(FLOOR, noise) * 100).toFixed(1)}%` };
    };
    const noisy = ratioRow('loadMs');
    const quiet = ratioRow('paintMs');
    // The fixture exercises both sides of the max.
    assert.ok(noisy.noise > FLOOR, `loadMs noise ${noisy.noise}`);
    assert.ok(quiet.noise < FLOOR, `paintMs noise ${quiet.noise}`);
    assert.equal(printed['open(small) / loadMs'], noisy.expected);
    assert.equal(printed['open(small) / paintMs'], quiet.expected);

    // A difference row's floor is 3% of the reference median, in the metric's unit.
    const m = 'openSettleMs';
    const noise = noiseBand(bootstrapDifference(base[m], arms['main-aa'][m]));
    const scale = Math.abs(bootstrapDifference(base[m], arms['plugins-next'][m]).scale);
    assert.equal(printed[`open(small) / ${m}`], `±${Math.max(FLOOR * scale, noise).toFixed(1)}`);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('--against exits non-zero with "cross-run comparison removed"', () => {
  const dir = fixtureRun();
  try {
    const out = runReport(dir, '--against', dir);
    assert.equal(out.status, 2);
    assert.match(out.stderr, /cross-run comparison removed/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('max column header is "max", not "p95"', () => {
  const dir = fixtureRun();
  try {
    const out = runReport(dir);
    assert.equal(out.status, 0, out.stderr);
    assert.doesNotMatch(out.stdout, /p95/);
    const { head, rows } = section(out.stdout, '## `main` vs `plugins-next`');
    assert.equal(head[1], 'main median [max]');
    assert.equal(head[2], 'plugins-next median [max]');
    // The bracket holds the largest sample: 110 * 1.1 for plugins-next loadMs.
    const load = rows.find((r) => r[0] === 'open(small) / loadMs');
    assert.equal(load[2], '110.0 [121.0]');
    const aa = section(out.stdout, '## A/A: `main` vs `main-aa`');
    assert.equal(aa.head[1], 'main median [max]');
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

// `undo` on main-now and plugins-next only, as when main has no history plugin.
const undoOnly = (skipped) => ({ meta, gate, samples }) => {
  meta.armsRun.push('main-now');
  meta.arms['main-now'] = meta.arms.main;
  if (skipped) meta.skipped = { main: ['undo'], 'main-aa': ['undo'], 'main-now': [], 'plugins-next': [] };
  gate['main-now'] = { pass: true };
  for (const [arm, scale] of [['main-now', 1], ['plugins-next', 1.2]]) {
    for (let i = 0; i < 12; i++) samples.push({ arm, flow: 'undo', fixture: 'annots', metrics: { mainJsMs: base.paintMs[i] * scale } });
  }
};

test('a flow the baseline skipped is judged against the floor alone', () => {
  const dir = fixtureRun({ extra: undoOnly(true) });
  try {
    const out = runReport(dir);
    assert.equal(out.status, 0, out.stderr);
    const { head, rows } = section(out.stdout, '## `main-now` vs `plugins-next`');
    const undo = rows.find((r) => r[0] === 'undo / mainJsMs');
    assert.equal(undo[head.indexOf('threshold')], '±3.0%');
    assert.equal(undo[head.indexOf('Verdict')], 'slower');
    assert.ok(!section(out.stdout, '## A/A: `main` vs `main-aa`').rows.some((r) => r[0].startsWith('undo')));
    assert.match(out.stdout, /- `main`: `undo`/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('a row without A/A data that no arm skipped keeps "no A/A data"', () => {
  const dir = fixtureRun({ extra: undoOnly(false) });
  try {
    const out = runReport(dir);
    assert.equal(out.status, 0, out.stderr);
    const { head, rows } = section(out.stdout, '## `main-now` vs `plugins-next`');
    assert.equal(rows.find((r) => r[0] === 'undo / mainJsMs')[head.indexOf('Verdict')], 'no A/A data');
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('an arm whose flow the gate left unchecked has its rows for that flow left out', () => {
  // The rows still exist for main-now here, so leaving plugins-next's out
  // shows in the pair table; in a real run the gate marks a flow unchecked
  // only when no other arm answered it.
  const unchecked = ({ meta, gate, samples }) => {
    undoOnly(true)({ meta, gate, samples });
    gate['plugins-next'] = { pass: true, unchecked: ['undo'] };
  };
  const dir = fixtureRun({ extra: unchecked });
  try {
    const out = runReport(dir);
    assert.equal(out.status, 0, out.stderr);
    const { rows } = section(out.stdout, '## `main-now` vs `plugins-next`');
    assert.ok(!rows.some((r) => r[0].startsWith('undo')), rows.map((r) => r[0]).join(', '));
    assert.ok(section(out.stdout, '## `main` vs `plugins-next`').rows.some((r) => r[0] === 'open(small) / loadMs'));
    assert.match(out.stdout, /Unchecked, because no other arm in the run gave the answer/);
    assert.match(out.stdout, /- `plugins-next`: `undo`\n/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});
