// node scripts/summary.mjs <results/run-id> [arm ...] [--drift <results/other-run> <drift-arm>]:
// the numbers to quote from a run's report.md (written by report.mjs, run
// here if missing).
//
// For each pair in the report, the verdict counts, the median ratio over the
// latency rows and over the main-thread rows (`mainJsMs`, `mainTaskMs`), and,
// unless the pair's first arm is the baseline (`main`), every row that isn't
// "no detectable difference". Rows reported as a difference in ms (settle
// times) have no ratio and count only toward the verdicts. Arms after the run
// id narrow the output to pairs naming one of them.
//
// --drift compares one arm's medians between this run and another, for a
// build that is in both: how far the same build moved from run to run. It
// prints each row's two medians, then every moved row of this run's pairs
// whose change is no larger than that arm's drift on the same row, as
// unconfirmed.
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadConfig } from '../lib/config.mjs';

const args = process.argv.slice(2);
const driftAt = args.indexOf('--drift');
const drift = driftAt === -1 ? null : { run: args[driftAt + 1], arm: args[driftAt + 2] };
if (drift && (!drift.run || !drift.arm)) throw new Error('usage: --drift <results/other-run> <arm>');
const [runArg, ...only] = driftAt === -1 ? args : [...args.slice(0, driftAt), ...args.slice(driftAt + 3)];
if (!runArg) throw new Error('usage: node scripts/summary.mjs <results/run-id> [arm ...] [--drift <results/other-run> <arm>]');
const here = fileURLToPath(new URL('..', import.meta.url));
const baseline = loadConfig().baseline;

function readRun(arg) {
  const dir = path.resolve(here, arg);
  const reportFile = path.join(dir, 'report.md');
  if (!existsSync(reportFile)) execFileSync('node', ['report.mjs', dir], { cwd: here, stdio: 'ignore' });
  const metaFile = path.join(dir, 'meta.json');
  return {
    report: readFileSync(reportFile, 'utf8'),
    meta: existsSync(metaFile) ? JSON.parse(readFileSync(metaFile, 'utf8')) : {},
  };
}

const median = (xs) => {
  if (xs.length === 0) return NaN;
  const s = [...xs].sort((a, b) => a - b);
  const m = s.length >> 1;
  return s.length % 2 ? s[m] : (s[m - 1] + s[m]) / 2;
};
const cells = (line) => line.split('|').slice(1, -1).map((c) => c.trim());
// A ratio cell starts with a digit, a difference cell with its sign.
const isRatio = (cell) => /^\d/.test(cell) && !Number.isNaN(Number.parseFloat(cell));
const isMainThread = (row) => /mainJs|mainTask/.test(row);
const times = (x) => (Number.isNaN(x) ? 'n/a' : `${x.toFixed(3)}x`);

// Every table with a `Row` header and a `ratio or diff` column: the A/A table
// and each pair's. Columns by name: reports from before the threshold column
// have five, later ones six, and an older run's cached report.md keeps its
// layout.
function tables(report) {
  const out = [];
  for (const section of report.split(/^## /m).slice(1)) {
    const head = section.slice(0, section.indexOf('\n'));
    const lines = section.split('\n');
    const header = lines.find((l) => l.startsWith('| Row'));
    if (!header) continue;
    const columns = cells(header);
    const I = columns.indexOf('ratio or diff [95% CI]');
    if (I === -1) continue;
    const rows = lines
      .filter((l) => /^\| [a-zA-Z]/.test(l) && !l.startsWith('| Row'))
      .map(cells)
      .filter((c) => c.length === columns.length);
    out.push({ head, columns, I, rows });
  }
  return out;
}

// One arm's median per row, from the first table that has a column for it.
function armMedians(report, arm) {
  for (const t of tables(report)) {
    const c = t.columns.findIndex((name) => name.startsWith(`${arm} median`));
    if (c === -1) continue;
    return new Map(t.rows.map((r) => [r[0], { median: Number.parseFloat(r[c]), ratioRow: isRatio(r[t.I]) }]));
  }
  throw new Error(`no table has a column for ${arm}`);
}

const current = readRun(runArg);
const pairs = [];
for (const t of tables(current.report)) {
  const pair = t.head.match(/^`([^`]+)` vs `([^`]+)`$/);
  if (!pair) continue;
  const [, a, b] = pair;
  if (only.length && !only.includes(a) && !only.includes(b)) continue;
  const V = t.columns.indexOf('Verdict');
  if (V === -1) throw new Error(`${a} vs ${b}: no Verdict column in ${t.columns.join(' | ')}`);
  if (t.rows.length === 0) continue;
  pairs.push({ a, b, ...t, V });
  const verdicts = {};
  for (const r of t.rows) verdicts[r[V]] = (verdicts[r[V]] ?? 0) + 1;
  console.log(`\n${a} vs ${b}: ${Object.entries(verdicts).map(([v, n]) => `${n} ${v}`).join(', ')}`);
  const lat = [];
  const cpu = [];
  for (const r of t.rows) {
    if (!isRatio(r[t.I])) continue;
    (isMainThread(r[0]) ? cpu : lat).push(Number.parseFloat(r[t.I]));
  }
  console.log(`  latency median ${times(median(lat))}, main-thread median ${times(median(cpu))}`);
  if (a === baseline) continue;
  for (const r of t.rows.filter((r) => r[V] !== 'no detectable difference')) {
    console.log(`  ${r[0]}: ${r[1]} -> ${r[2]}, ${r[t.I]} (${r[V]})`);
  }
}

if (drift) {
  const other = readRun(drift.run);
  const was = armMedians(other.report, drift.arm);
  const now = armMedians(current.report, drift.arm);
  const build = (meta) => {
    const arm = meta.arms?.[drift.arm] ?? {};
    return { sha: arm.sha ?? null, engine: arm.engineWasmSha256 ?? null, probe: meta.probeSha256 ?? null };
  };
  const [p, q] = [build(other.meta), build(current.meta)];
  console.log(`\ndrift of ${drift.arm}: ${path.basename(path.resolve(here, drift.run))} -> ${path.basename(path.resolve(here, runArg))}`);
  console.log(`  commit ${p.sha === q.sha ? 'same' : 'differs'}, engine wasm ${p.engine === q.engine ? 'same' : 'differs'}, probe ${p.probe === q.probe ? 'same' : `differs (${p.probe?.slice(0, 12) ?? 'none recorded'} -> ${q.probe?.slice(0, 12) ?? 'none recorded'})`}`);
  const moved = new Map(); // row -> { ratio, diff }
  const lat = [];
  const cpu = [];
  for (const [row, { median: before, ratioRow }] of was) {
    const after = now.get(row)?.median;
    if (after === undefined || Number.isNaN(before) || Number.isNaN(after)) continue;
    const diff = after - before;
    const ratio = before > 0 ? after / before : NaN;
    moved.set(row, { diff, ratio, ratioRow });
    if (ratioRow && !Number.isNaN(ratio)) (isMainThread(row) ? cpu : lat).push(ratio);
    const change = ratioRow ? (Number.isNaN(ratio) ? 'n/a' : `${ratio.toFixed(3)}x`) : `${diff >= 0 ? '+' : ''}${diff.toFixed(1)}`;
    console.log(`  ${row}: ${before} -> ${after}, ${change}`);
  }
  console.log(`  latency median ${times(median(lat))}, main-thread median ${times(median(cpu))}`);
  // A moved row whose change is no larger than the drift arm's own change on
  // the same row can't be told from run-to-run movement.
  for (const pr of pairs) {
    const inside = [];
    for (const r of pr.rows) {
      if (r[pr.V] === 'no detectable difference' || !moved.has(r[0])) continue;
      const d = moved.get(r[0]);
      const value = Number.parseFloat(r[pr.I]);
      const within = isRatio(r[pr.I])
        ? !Number.isNaN(d.ratio) && Math.abs(value - 1) <= Math.abs(d.ratio - 1)
        : Math.abs(value) <= Math.abs(d.diff);
      if (within) inside.push(`${r[0]} ${r[pr.I].split(' ')[0]}`);
    }
    console.log(`  ${pr.a} vs ${pr.b}, moved rows within ${drift.arm}'s drift: ${inside.length ? inside.join('; ') : 'none'}`);
  }
}
