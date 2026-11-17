// node scripts/summary.mjs <results/run-id> [arm ...] [--drift <results/other-run> <drift-arm>]:
// the numbers to quote from a run's report.md (written by report.mjs, run
// here if missing).
//
// First the run's frame interval (lib/env.mjs `frameInterval`), from its
// samples.jsonl. Then, for each pair in the report, the verdict counts; the median ratio over
// three groups of ratio rows, each labelled with its row count: timing rows
// (open, search and wall times), frame-interval rows (`frameP50Ms`,
// `frameP95Ms`, which sit near the run's frame interval whatever the arm does) and
// main-thread rows (`mainJsMs`, `mainTaskMs`); the median difference over the
// settle rows, which have no ratio; and, unless the pair's first arm is the
// baseline (`main`), every row that isn't "no detectable difference". Arms
// after the run id narrow the output to pairs naming one of them.
//
// --drift compares one arm's medians between this run and another, for a
// build that is in both: how far the same build moved from run to run. It
// prints each row's two medians, then every moved row of this run's pairs
// whose change is no larger than that arm's drift on the same row, as
// unconfirmed. It refuses (exit 2) two runs that don't compare: a probe hash
// that differs or is missing (viewer-perf-compare-plan.md, Task 6 Step 6:
// "reject comparisons with a different or missing probe hash"), a different
// browser, CPU, profile, viewport or scale factor, a fixture both runs have
// with different bytes (lib/env.mjs `compatible`), frame intervals more than
// FRAME_INTERVAL_TOLERANCE_MS apart or not determined in either run, or a
// commit or engine wasm for the drift arm that differs or wasn't recorded.
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadConfig } from '../lib/config.mjs';
import { compatible, FRAME_INTERVAL_TOLERANCE_MS, frameInterval, frameIntervalText } from '../lib/env.mjs';

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
  const samplesFile = path.join(dir, 'samples.jsonl');
  const samples = existsSync(samplesFile)
    ? readFileSync(samplesFile, 'utf8').split('\n').filter(Boolean).map((l) => JSON.parse(l))
    : [];
  return {
    report: readFileSync(reportFile, 'utf8'),
    meta: existsSync(metaFile) ? JSON.parse(readFileSync(metaFile, 'utf8')) : {},
    frameInterval: frameInterval(samples, baseline),
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
const isFrame = (row) => /\/ frameP\d+Ms$/.test(row);
const isSettle = (row) => /[sS]ettleMs$/.test(row);
const times = (x) => (Number.isNaN(x) ? 'n/a' : `${x.toFixed(3)}x`);
const ms = (x) => `${x >= 0 ? '+' : ''}${x.toFixed(1)} ms`;
// A verdict can carry a note, "slower (no A/A band)".
const isSame = (verdict) => verdict.startsWith('no detectable difference');

// The ratio medians by group, each with its row count, and the settle rows'
// median difference, from [row, value, isRatio] triples.
function medians(entries) {
  const groups = { timing: [], frame: [], main: [], settle: [] };
  for (const [row, value, ratio] of entries) {
    if (Number.isNaN(value)) continue;
    if (ratio) groups[isMainThread(row) ? 'main' : isFrame(row) ? 'frame' : 'timing'].push(value);
    else if (isSettle(row)) groups.settle.push(value);
  }
  const ratioText = (name, xs) => `${name} median ${times(median(xs))} (${xs.length} rows)`;
  const settle = groups.settle;
  return [
    `  ratio rows: ${ratioText('timing', groups.timing)}, ${ratioText('frame-interval', groups.frame)}, ${ratioText('main-thread', groups.main)}`,
    `  settle rows: median ${settle.length ? ms(median(settle)) : 'n/a'} (${settle.length} rows${settle.length ? `, ${ms(Math.min(...settle))} to ${ms(Math.max(...settle))}` : ''})`,
  ];
}

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
console.log(`frame interval: ${frameIntervalText(current.frameInterval, baseline)}`);
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
  for (const line of medians(t.rows.map((r) => [r[0], Number.parseFloat(r[t.I]), isRatio(r[t.I])]))) console.log(line);
  if (a === baseline) continue;
  for (const r of t.rows.filter((r) => !isSame(r[V]))) {
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
  const reasons = driftRefusals(other, current, p, q);
  if (reasons.length) {
    console.error(
      `--drift refused: ${path.basename(path.resolve(here, drift.run))} and ${path.basename(path.resolve(here, runArg))} don't compare for ${drift.arm}: ${reasons.join('; ')}. ` +
        'The plan rejects comparisons across a different or missing probe hash; time both builds in one run instead.',
    );
    process.exit(2);
  }
  console.log(`\ndrift of ${drift.arm}: ${path.basename(path.resolve(here, drift.run))} -> ${path.basename(path.resolve(here, runArg))}`);
  console.log(
    `  commit same, engine wasm same, probe same (${q.probe.slice(0, 12)}), frame interval ${other.frameInterval.ms.toFixed(1)} -> ${current.frameInterval.ms.toFixed(1)} ms`,
  );
  const moved = new Map(); // row -> { ratio, diff }
  const changes = [];
  for (const [row, { median: before, ratioRow }] of was) {
    const after = now.get(row)?.median;
    if (after === undefined || Number.isNaN(before) || Number.isNaN(after)) continue;
    const diff = after - before;
    const ratio = before > 0 ? after / before : NaN;
    moved.set(row, { diff, ratio, ratioRow });
    changes.push([row, ratioRow ? ratio : diff, ratioRow]);
    const change = ratioRow ? (Number.isNaN(ratio) ? 'n/a' : `${ratio.toFixed(3)}x`) : `${diff >= 0 ? '+' : ''}${diff.toFixed(1)}`;
    console.log(`  ${row}: ${before} -> ${after}, ${change}`);
  }
  for (const line of medians(changes)) console.log(line);
  // A moved row whose change is no larger than the drift arm's own change on
  // the same row can't be told from run-to-run movement.
  for (const pr of pairs) {
    const inside = [];
    for (const r of pr.rows) {
      if (isSame(r[pr.V]) || !moved.has(r[0])) continue;
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

// Why the two runs' medians for the drift arm don't compare; empty when they do.
function driftRefusals(wasRun, nowRun, p, q) {
  const [was, now] = [wasRun.meta, nowRun.meta];
  const reasons = [];
  const hash = (h) => h?.slice(0, 12) ?? 'none recorded';
  if (!p.probe || !q.probe || p.probe !== q.probe) reasons.push(`probe hash ${hash(p.probe)} -> ${hash(q.probe)}`);
  // The flow and fixture lists may grow from run to run; a fixture both runs
  // have must be the same file.
  for (const key of compatible(was, now)) {
    if (!['probeSha256', 'flows', 'fixtures'].includes(key)) reasons.push(`${key} differs`);
  }
  for (const [f, sha] of Object.entries(was.fixtures ?? {})) {
    if (now.fixtures?.[f] !== undefined && now.fixtures[f] !== sha) reasons.push(`fixture ${f} differs`);
  }
  // Paced flows take whole frames, so a different interval moves their rows
  // on every arm (FINDINGS.md, "The fix branch under the corrected harness").
  const [fa, fb] = [wasRun.frameInterval, nowRun.frameInterval];
  if (!fa || !fb) {
    reasons.push(`frame interval ${fa ? `${fa.ms.toFixed(1)} ms` : 'not determined'} -> ${fb ? `${fb.ms.toFixed(1)} ms` : 'not determined'}`);
  } else if (Math.abs(fa.ms - fb.ms) > FRAME_INTERVAL_TOLERANCE_MS) {
    reasons.push(`frame interval ${fa.ms.toFixed(1)} -> ${fb.ms.toFixed(1)} ms, more than ${FRAME_INTERVAL_TOLERANCE_MS} ms apart`);
  }
  // A build not recorded on either side can't be shown to be the same build.
  if (!p.sha || !q.sha) reasons.push('commit not recorded');
  else if (p.sha !== q.sha) reasons.push('commit differs');
  if (!p.engine || !q.engine) reasons.push('engine wasm not recorded');
  else if (p.engine !== q.engine) reasons.push('engine wasm differs');
  return reasons;
}
