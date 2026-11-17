import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const SUMMARY = fileURLToPath(new URL('../scripts/summary.mjs', import.meta.url));

// report.md before the threshold column (cached in older runs), and after.
const OLD = `# Viewer perf comparison: fixture

## \`main\` vs \`plugins-next\`

| Row | main median [p95] | plugins-next median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(small) / loadMs | 100.0 [108.9] | 110.0 [119.8] | 1.100 [1.045, 1.158] | slower |
| scroll / mainJsMs | 100.0 [100.2] | 90.0 [90.3] | 0.900 [0.898, 0.902] | faster |
| open(small) / openSettleMs | 50.0 [51.2] | 50.0 [51.3] | +0.0 [-0.5, +0.5] | no detectable difference |

## \`plugins-base\` vs \`plugins-next\`

| Row | plugins-base median [p95] | plugins-next median [p95] | ratio or diff [95% CI] | Verdict |
| --- | --- | --- | --- | --- |
| open(small) / loadMs | 100.0 [108.9] | 110.0 [119.8] | 1.100 [1.045, 1.158] | slower |
| open(small) / paintMs | 100.0 [100.2] | 100.0 [100.3] | 1.000 [0.998, 1.002] | no detectable difference |
`;

const NEW = `# Viewer perf comparison: fixture

## \`main\` vs \`plugins-next\`

| Row | main median [max] | plugins-next median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(small) / loadMs | 100.0 [110.0] | 110.0 [121.0] | 1.100 [1.045, 1.158] | ±5.3% | slower |
| scroll / mainJsMs | 100.0 [100.3] | 90.0 [90.3] | 0.900 [0.898, 0.902] | ±3.0% | faster |
| open(small) / openSettleMs | 50.0 [51.5] | 50.0 [51.6] | +0.0 [-0.5, +0.5] | ±1.5 | no detectable difference |

## \`plugins-base\` vs \`plugins-next\`

| Row | plugins-base median [max] | plugins-next median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(small) / loadMs | 100.0 [110.0] | 110.0 [121.0] | 1.100 [1.045, 1.158] | ±5.3% | slower |
| open(small) / paintMs | 100.0 [100.3] | 100.0 [100.3] | 1.000 [0.998, 1.002] | ±3.0% | no detectable difference |
| open(small) / textMs | 100.0 [100.3] | 100.0 [100.3] | 1.000 [0.998, 1.002] |  | unreliable (A/A failed) |
`;

function summarize(report) {
  const dir = mkdtempSync(path.join(tmpdir(), 'summary-test-'));
  try {
    writeFileSync(path.join(dir, 'report.md'), report);
    const out = spawnSync(process.execPath, [SUMMARY, dir], { encoding: 'utf8' });
    assert.equal(out.status, 0, out.stderr);
    return out.stdout;
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
}

test('summary reads the verdict and ratio columns of a 5-column report', () => {
  const out = summarize(OLD);
  assert.match(out, /main vs plugins-next: 1 slower, 1 faster, 1 no detectable difference\n/);
  assert.match(out, /ratio rows: timing median 1\.100x \(1 rows\), frame-interval median n\/a \(0 rows\), main-thread median 0\.900x \(1 rows\)\n/);
  assert.match(out, /settle rows: median \+0\.0 ms \(1 rows, \+0\.0 ms to \+0\.0 ms\)\n/);
  assert.match(out, /plugins-base vs plugins-next: 1 slower, 1 no detectable difference\n/);
  assert.match(out, /open\(small\) \/ loadMs: 100\.0 \[108\.9\] -> 110\.0 \[119\.8\], 1\.100 \[1\.045, 1\.158\] \(slower\)/);
  assert.doesNotMatch(out, /paintMs/);
});

test('summary reads the verdict and ratio columns of a 6-column report with threshold', () => {
  const out = summarize(NEW);
  assert.match(out, /main vs plugins-next: 1 slower, 1 faster, 1 no detectable difference\n/);
  assert.match(out, /ratio rows: timing median 1\.100x \(1 rows\), frame-interval median n\/a \(0 rows\), main-thread median 0\.900x \(1 rows\)\n/);
  assert.match(out, /plugins-base vs plugins-next: 1 slower, 1 no detectable difference, 1 unreliable \(A\/A failed\)\n/);
  assert.match(out, /open\(small\) \/ loadMs: 100\.0 \[110\.0\] -> 110\.0 \[121\.0\], 1\.100 \[1\.045, 1\.158\] \(slower\)/);
  assert.match(out, /open\(small\) \/ textMs: .* \(unreliable \(A\/A failed\)\)/);
  assert.doesNotMatch(out, /paintMs/);
  assert.doesNotMatch(out, /±/);
});

test('summary prints the median ratios for a pair whose first arm is not the baseline', () => {
  const out = summarize(NEW);
  const tail = out.slice(out.indexOf('plugins-base vs plugins-next'));
  // loadMs 1.100 and paintMs 1.000 are latency rows, textMs 1.000 too: median 1.000.
  assert.match(tail, /^plugins-base vs plugins-next: .*\n  ratio rows: timing median 1\.000x \(3 rows\), frame-interval median n\/a \(0 rows\), main-thread median n\/a \(0 rows\)\n  settle rows: median n\/a \(0 rows\)\n/);
});

// An A/A table for the drift arm in each of two runs.
const aa = (rows) => `# Viewer perf comparison: fixture

## A/A: \`main\` vs \`main-aa\`

| Row | main median [max] | main-aa median [max] | ratio or diff [95% CI] | A/A |
| --- | --- | --- | --- | --- |
${rows}
`;
const BEFORE = aa(`| open(small) / loadMs | 100.0 [101.0] | 100.0 [101.0] | 1.000 [0.990, 1.010] | pass |
| zoom / settleMs | 200.0 [210.0] | 200.0 [210.0] | +0.0 [-1.0, +1.0] | pass |
| scroll / mainJsMs | 100.0 [101.0] | 100.0 [101.0] | 1.000 [0.990, 1.010] | pass |`);
const AFTER = `${aa(`| open(small) / loadMs | 104.0 [105.0] | 104.0 [105.0] | 1.000 [0.990, 1.010] | pass |
| zoom / settleMs | 230.0 [240.0] | 230.0 [240.0] | +0.0 [-1.0, +1.0] | pass |
| scroll / mainJsMs | 100.0 [101.0] | 100.0 [101.0] | 1.000 [0.990, 1.010] | pass |`)}
## \`plugins-base\` vs \`plugins-next\`

| Row | plugins-base median [max] | plugins-next median [max] | ratio or diff [95% CI] | threshold | Verdict |
| --- | --- | --- | --- | --- | --- |
| open(small) / loadMs | 100.0 [110.0] | 110.0 [121.0] | 1.100 [1.045, 1.158] | ±3.0% | slower |
| zoom / settleMs | 200.0 [210.0] | 225.0 [230.0] | +25.0 [+20.0, +30.0] | ±5.0 | slower |
| scroll / mainJsMs | 100.0 [100.3] | 102.0 [103.0] | 1.020 [1.010, 1.030] | ±1.0% | slower |
`;

function driftRuns(probes, armShas = ['abc', 'abc']) {
  const root = mkdtempSync(path.join(tmpdir(), 'summary-drift-'));
  const write = (name, report, probe, sha) => {
    const dir = path.join(root, name);
    mkdirSync(dir);
    writeFileSync(path.join(dir, 'report.md'), report);
    writeFileSync(path.join(dir, 'meta.json'), JSON.stringify({ probeSha256: probe, fixtures: { large: 'l1' }, arms: { main: { sha, engineWasmSha256: 'e1' } } }));
    return dir;
  };
  return {
    before: write('before', BEFORE, probes[0], armShas[0]),
    after: write('after', AFTER, probes[1], armShas[1]),
    done: () => rmSync(root, { recursive: true, force: true }),
  };
}
const drifted = (runs) => spawnSync(process.execPath, [SUMMARY, runs.after, '--drift', runs.before, 'main'], { encoding: 'utf8' });

test('--drift prints the arm across two runs and the moved rows inside its drift', () => {
  const runs = driftRuns(['f'.repeat(64), 'f'.repeat(64)]);
  try {
    const out = drifted(runs);
    assert.equal(out.status, 0, out.stderr);
    assert.match(out.stdout, /drift of main: before -> after\n  commit same, engine wasm same, probe same \(ffffffffffff\)\n/);
    assert.match(out.stdout, /open\(small\) \/ loadMs: 100 -> 104, 1\.040x\n/);
    assert.match(out.stdout, /zoom \/ settleMs: 200 -> 230, \+30\.0\n/);
    assert.match(out.stdout, /settle rows: median \+30\.0 ms \(1 rows, \+30\.0 ms to \+30\.0 ms\)\n/);
    // settleMs moved 25 against main's 30, mainJsMs 2% against main's 0%, loadMs 10% against 4%.
    assert.match(out.stdout, /plugins-base vs plugins-next, moved rows within main's drift: zoom \/ settleMs \+25\.0\n/);
  } finally {
    runs.done();
  }
});

test('--drift refuses a run with no probe hash recorded, as the plan says', () => {
  const runs = driftRuns([undefined, 'f'.repeat(64)]);
  try {
    const out = drifted(runs);
    assert.equal(out.status, 2);
    assert.match(out.stderr, /--drift refused: .*probe hash none recorded -> ffffffffffff/);
    assert.doesNotMatch(out.stdout, /moved rows within/);
  } finally {
    runs.done();
  }
});

test('--drift refuses a different probe, or a different build of the drift arm', () => {
  for (const [probes, shas, reason] of [
    [['a'.repeat(64), 'f'.repeat(64)], ['abc', 'abc'], /probe hash aaaaaaaaaaaa -> ffffffffffff/],
    [['f'.repeat(64), 'f'.repeat(64)], ['abc', 'def'], /commit differs/],
  ]) {
    const runs = driftRuns(probes, shas);
    try {
      const out = drifted(runs);
      assert.equal(out.status, 2);
      assert.match(out.stderr, reason);
    } finally {
      runs.done();
    }
  }
});

test('a verdict with a note still counts as no difference', () => {
  const noted = NEW.replace(
    '| 1.000 [0.998, 1.002] | ±3.0% | no detectable difference |',
    '| 1.000 [0.998, 1.002] | ±3.0% | no detectable difference (no A/A band) |',
  );
  assert.notEqual(noted, NEW);
  const out = summarize(noted);
  assert.match(out, /plugins-base vs plugins-next: 1 slower, 1 no detectable difference \(no A\/A band\), 1 unreliable/);
  assert.doesNotMatch(out, /paintMs/);
});
