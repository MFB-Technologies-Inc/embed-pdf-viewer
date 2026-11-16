import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
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
  assert.match(out, /latency median 1\.100x, main-thread median 0\.900x/);
  assert.match(out, /plugins-base vs plugins-next: 1 slower, 1 no detectable difference\n/);
  assert.match(out, /open\(small\) \/ loadMs: 100\.0 \[108\.9\] -> 110\.0 \[119\.8\], 1\.100 \[1\.045, 1\.158\] \(slower\)/);
  assert.doesNotMatch(out, /paintMs/);
});

test('summary reads the verdict and ratio columns of a 6-column report with threshold', () => {
  const out = summarize(NEW);
  assert.match(out, /main vs plugins-next: 1 slower, 1 faster, 1 no detectable difference\n/);
  assert.match(out, /latency median 1\.100x, main-thread median 0\.900x/);
  assert.match(out, /plugins-base vs plugins-next: 1 slower, 1 no detectable difference, 1 unreliable \(A\/A failed\)\n/);
  assert.match(out, /open\(small\) \/ loadMs: 100\.0 \[110\.0\] -> 110\.0 \[121\.0\], 1\.100 \[1\.045, 1\.158\] \(slower\)/);
  assert.match(out, /open\(small\) \/ textMs: .* \(unreliable \(A\/A failed\)\)/);
  assert.doesNotMatch(out, /paintMs/);
  assert.doesNotMatch(out, /±/);
});
