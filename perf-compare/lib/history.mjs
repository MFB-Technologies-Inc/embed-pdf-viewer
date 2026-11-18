// The perf history: which commits get measured, which runs may join, and
// what the page plots. Pure functions; the scripts/history-*.mjs files do
// the git and file work. viewer-perf-history-plan.md has the design.
import { better, isDifference, rows, unit } from './rows.mjs';
import { bootstrapDifference, bootstrapRatio, calibrate, comparePair, summarize } from './stats.mjs';

const DAY = 86_400_000;
const utcDate = (ms) => new Date(ms).toISOString().slice(0, 10);
const dayStart = (isoDate) => Date.parse(`${isoDate.slice(0, 10)}T00:00:00Z`);

// ISO dates from `from`, every `days` days, none after `to`.
export function intervalTargets(from, to, days) {
  const out = [];
  for (let t = dayStart(from); t <= dayStart(to); t += days * DAY) out.push(utcDate(t));
  return out;
}

// One commit for each target day that has no point near it: the latest of
// `commits` (a first-parent line, `[{ sha, date }]`) dated on or before that
// day in UTC. `existing` holds the dates of the points the series already
// has. A quiet stretch yields its last commit once, not once per target.
export function selectIntervalPoints({ commits, existing, from, to, days, nearDays = 1 }) {
  const taken = existing.map(dayStart);
  const near = (t) => taken.some((e) => Math.abs(e - t) <= nearDays * DAY);
  const timed = commits.map((c) => ({ sha: c.sha, at: Date.parse(c.date) }));
  const used = new Set();
  const out = [];
  for (const target of intervalTargets(from, to, days)) {
    const start = dayStart(target);
    if (near(start)) continue;
    let best = null;
    for (const c of timed) if (c.at < start + DAY && (!best || c.at > best.at)) best = c;
    if (!best || used.has(best.sha)) continue;
    const date = utcDate(best.at);
    if (near(dayStart(date))) continue;
    used.add(best.sha);
    taken.push(dayStart(date));
    out.push({ sha: best.sha, date, target });
  }
  return out;
}

// The arm arms.json already has for this commit built this way, or null.
export function armFor(config, { sha, recipe, kernel }) {
  for (const [id, arm] of Object.entries(config.arms)) {
    const w = config.worktrees[arm.worktree];
    if (w.sha === sha && w.recipe === recipe && arm.kernel === kernel && !arm.rustReads) return id;
  }
  return null;
}

// Why a run can't join the history; empty when it can. Points from
// different runs share a line only because each is compared with the
// reference arm of its own run, on one machine, at full length.
export function eligible(meta, gate, { machine, reference, noise }) {
  const reasons = [];
  if (meta.host !== machine.host) reasons.push(`host is ${meta.host}, not ${machine.host}`);
  if (meta.cpuModel !== machine.cpuModel) reasons.push(`CPU is ${meta.cpuModel}, not ${machine.cpuModel}`);
  if (meta.profile !== 'desktop') reasons.push(`profile is ${meta.profile}, not desktop`);
  if (meta.quick) reasons.push('a --quick run');
  if (!(meta.rounds >= 10)) reasons.push(`${meta.rounds} rounds, fewer than 10`);
  if (meta.harnessDirty !== false) reasons.push('the harness had uncommitted changes, or the run did not record that it was clean');
  for (const arm of [reference, noise]) {
    if (!meta.armsRun?.includes(arm)) reasons.push(`${arm} was not in the run`);
    else if (!gate[arm]?.pass) reasons.push(`${arm} did not pass the gate`);
  }
  return reasons;
}

const round = (v) => (Number.isFinite(v) ? Number(v.toPrecision(6)) : null);

function compare(row, arm, reference, noise) {
  const values = row.byArm[arm];
  const out = { n: values.length, median: round(summarize(values).median), ref: null, rel: null, lo: null, hi: null, noise: null, verdict: null };
  const ref = row.byArm[reference];
  if (!ref?.length) return out;
  const interval = isDifference(row.metric) ? bootstrapDifference : bootstrapRatio;
  out.ref = round(summarize(ref).median);
  try {
    const i = interval(ref, values);
    Object.assign(out, { rel: round(i.kind === 'difference' ? i.diff : i.ratio), lo: round(i.lo), hi: round(i.hi) });
    const aa = row.byArm[noise];
    if (aa?.length) {
      const calibration = calibrate(interval(ref, aa));
      out.noise = round(calibration.noise);
      out.verdict = comparePair(i, calibration);
    }
  } catch {
    // A ratio of a zero sample has no value; the median still plots.
  }
  return out;
}

// What history/data.json holds. `runs` is `[{ meta, gate, samples }]`,
// oldest first. A run measures a point when the point's arm ran, passed the
// gate and was built from the point's commit; the latest such run wins.
export function seriesData({ runs, points, reference, noise, series }) {
  const measuredBy = new Map();
  for (const run of runs) {
    for (const p of points) {
      if (p.skip) continue;
      if (run.meta.armsRun?.includes(p.arm) && run.gate[p.arm]?.pass && run.meta.arms?.[p.arm]?.sha === p.sha) measuredBy.set(p, run);
    }
  }
  const measured = [...measuredBy.keys()].sort((a, b) => a.date.localeCompare(b.date) || a.series.localeCompare(b.series));
  const tables = new Map();
  const table = (run) => {
    // As report.mjs does: a flow the gate left unchecked for an arm has no rows for it.
    if (!tables.has(run)) tables.set(run, rows(run.samples.filter((s) => !run.gate[s.arm]?.unchecked?.includes(s.flow))));
    return tables.get(run);
  };
  const out = new Map();
  const rowOut = (key, fields) => {
    if (!out.has(key)) out.set(key, { key, ...fields, values: [] });
    return out.get(key).values;
  };
  measured.forEach((p, index) => {
    for (const [key, row] of table(measuredBy.get(p))) {
      if (!row.byArm[p.arm]?.length) continue;
      const fields = { label: row.label, flow: row.flow, metric: row.metric, kind: isDifference(row.metric) ? 'difference' : 'ratio', unit: unit(row.metric), better: better(row.metric) };
      rowOut(key, fields).push({ point: index, ...compare(row, p.arm, reference, noise) });
    }
  });
  measured.forEach((p, index) => {
    const sizes = measuredBy.get(p).meta.sizes ?? {};
    for (const [metric, bytes] of Object.entries(sizes[p.arm] ?? {})) {
      const ref = sizes[reference]?.[metric] ?? null;
      const fields = { label: `size / ${metric}`, flow: 'size', metric, kind: 'ratio', unit: unit(metric), better: better(metric) };
      rowOut(`size||${metric}`, fields).push({ point: index, n: 1, median: bytes, ref, rel: ref > 0 ? round(bytes / ref) : null, lo: null, hi: null, noise: null, verdict: null });
    }
  });
  return {
    reference,
    series,
    points: measured.map((p) => ({ sha: p.sha, series: p.series, date: p.date, why: p.why, note: p.note ?? null, arm: p.arm, runId: measuredBy.get(p).meta.runId })),
    rows: [...out.values()],
  };
}
