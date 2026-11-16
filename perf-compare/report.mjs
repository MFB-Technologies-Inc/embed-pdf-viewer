// results/<run-id>/ -> report.md (and stdout).
//
//   node report.mjs <results/run-id | --latest> [--publish]
//
// Every comparison is between arms of one run. Two runs don't share a noise
// band, so `--against` is gone and exits 2.
//
// Deterministic: the bootstrap is seeded, so the same raw data prints the
// same report.
import { existsSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { fileURLToPath } from 'node:url';
import { loadConfig } from './lib/config.mjs';
import { distSizes } from './lib/env.mjs';
import { bootstrapDifference, bootstrapRatio, calibrate, comparePair, summarize, threshold } from './lib/stats.mjs';
import { workDir } from './scripts/materialize.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const { values, positionals } = parseArgs({
  allowPositionals: true,
  options: {
    latest: { type: 'boolean', default: false },
    against: { type: 'string' },
    publish: { type: 'boolean', default: false },
  },
});

function latestRun() {
  const root = path.join(here, 'results');
  const runs = existsSync(root)
    ? readdirSync(root)
        .filter((d) => existsSync(path.join(root, d, 'meta.json')))
        .sort()
    : [];
  if (!runs.length) throw new Error('no completed run under results/ (a run without meta.json never finished)');
  return path.join(root, runs[runs.length - 1]);
}

function loadRun(dir) {
  const read = (f) => JSON.parse(readFileSync(path.join(dir, f), 'utf8'));
  if (!existsSync(path.join(dir, 'meta.json'))) throw new Error(`${dir} has no meta.json: the run didn't finish`);
  const samples = readFileSync(path.join(dir, 'samples.jsonl'), 'utf8').split('\n').filter(Boolean).map((l) => JSON.parse(l));
  return { dir, meta: read('meta.json'), gate: read('gate.json'), samples };
}

// Fixed per metric, never chosen from the data, so a metric's A/A
// calibration and its other pairs always use the same kind.
const isDifference = (metric) =>
  ['jankFrames', 'longTasks', 'longTaskMs', 'heapSoakSlopeBytes'].includes(metric) || /[sS]ettleMs$/.test(metric);

// Memory metrics get their own section; sizes aren't sampled.
const MEMORY = ['heapAfterOpenBytes', 'workerHeapAfterOpenBytes', 'heapSoakSlopeBytes', 'heapCycle5Bytes', 'heapCycle30Bytes'];

const rowLabel = (flow, fixture, metric) => `${flow === 'open' ? `open(${fixture})` : flow} / ${metric}`;

// { rowKey: { label, metric, byArm: { arm: number[] } } } in first-seen order.
function rows(samples) {
  const out = new Map();
  for (const s of samples) {
    if (s.error) continue;
    for (const [metric, value] of Object.entries(s.metrics)) {
      const key = `${s.flow}|${s.fixture}|${metric}`;
      if (!out.has(key)) out.set(key, { label: rowLabel(s.flow, s.fixture, metric), metric, byArm: {} });
      (out.get(key).byArm[s.arm] ??= []).push(value);
    }
  }
  return out;
}

const fmt = (metric, v) => {
  if (!Number.isFinite(v)) return String(v);
  if (/Bytes$/.test(metric)) return `${(v / 2 ** 20).toFixed(2)} MiB`;
  if (/Ms$/.test(metric)) return v.toFixed(1);
  return Number.isInteger(v) ? String(v) : v.toFixed(2);
};
const fmtDiff = (metric, v) => (v >= 0 ? '+' : '') + fmt(metric, v);
const cell = (metric, values) => {
  const s = summarize(values);
  return `${fmt(metric, s.median)} [${fmt(metric, s.max)}]`;
};
// The change a row must clear to get a verdict other than "no detectable
// difference": a ratio's as a percentage, a difference's in the metric's unit.
const thresholdText = (metric, iv, noise) => {
  const t = threshold(iv, noise);
  return iv.kind === 'difference' ? `±${fmt(metric, t)}` : `±${(t * 100).toFixed(1)}%`;
};

function interval(metric, reference, candidate) {
  return isDifference(metric) ? bootstrapDifference(reference, candidate) : bootstrapRatio(reference, candidate);
}

const intervalText = (metric, iv) =>
  iv.kind === 'difference'
    ? `${fmtDiff(metric, iv.diff)} [${fmtDiff(metric, iv.lo)}, ${fmtDiff(metric, iv.hi)}]`
    : `${iv.ratio.toFixed(3)} [${iv.lo.toFixed(3)}, ${iv.hi.toFixed(3)}]`;

const table = (head, body) =>
  [`| ${head.join(' | ')} |`, `| ${head.map(() => '---').join(' | ')} |`, ...body.map((r) => `| ${r.join(' | ')} |`)].join('\n');

function calibrations(allRows, config) {
  const out = new Map();
  for (const [key, row] of allRows) {
    const a = row.byArm[config.baseline];
    const b = row.byArm[config.noiseArm];
    if (!a || !b) continue;
    try {
      const iv = interval(row.metric, a, b);
      out.set(key, { ...calibrate(iv), iv });
    } catch (error) {
      out.set(key, { ok: false, noise: 0, invalid: error.message });
    }
  }
  return out;
}

function pairTable(allRows, calib, ref, cand) {
  const body = [];
  for (const [key, row] of allRows) {
    if (MEMORY.includes(row.metric)) continue;
    const r = row.byArm[ref];
    const c = row.byArm[cand];
    if (!r || !c) continue;
    let ivText = '';
    let thresholdCell = '';
    let verdict;
    try {
      const iv = interval(row.metric, r, c);
      ivText = intervalText(row.metric, iv);
      const cal = calib.get(key);
      if (cal?.ok) thresholdCell = thresholdText(row.metric, iv, cal.noise);
      verdict = !cal ? 'no A/A data' : cal.ok ? comparePair(iv, cal) : 'unreliable (A/A failed)';
      if (verdict === 'same') verdict = 'no detectable difference';
    } catch (error) {
      verdict = `invalid: ${error.message}`;
    }
    body.push([row.label, cell(row.metric, r), cell(row.metric, c), ivText, thresholdCell, verdict]);
  }
  return body;
}

function report(run, config) {
  const { meta, gate, samples } = run;
  const lines = [];
  const push = (...l) => lines.push(...l);
  const allRows = rows(samples);
  const calib = calibrations(allRows, config);

  push(
    `# Viewer perf comparison: ${meta.runId}`,
    '',
    `${meta.rounds} rounds, profile \`${meta.profile}\` (CPU throttle ${meta.throttleRate}x)${meta.quick ? ', **quick, not for publication**' : ''}.`,
  );
  if (meta.profile !== 'desktop') {
    push(
      '',
      meta.workerThrottled === false
        ? 'The throttle reached the main thread only: Chromium refuses `Emulation.setCPUThrottlingRate` on the engine worker, so worker-side work ran at full speed.'
        : 'The throttle reached the page and the engine worker.',
    );
  }
  if (meta.memorySource === 'cdp') push('', 'Memory is JS heap only, wasm linear memory not included.');
  push('', '`workerTaskMs` is n/a: Chromium has no Performance domain on dedicated workers.');

  push('', '## Environment', '');
  push(
    table(
      ['Field', 'Value'],
      [
        ['Harness commit', `${meta.harnessCommit}${meta.harnessDirty ? ' (uncommitted changes)' : ''}`],
        ['Chromium / Playwright', `${meta.browserVersion} / ${meta.playwrightVersion}`],
        ['Node', meta.nodeVersion],
        ['OS', meta.os],
        ['CPU', `${meta.cpuModel}, ${meta.cpuCores} cores`],
        ['Memory', `${(meta.totalMemoryBytes / 2 ** 30).toFixed(0)} GiB`],
        ['Power', meta.powerSource],
        ['Viewport', `${meta.viewport.width}x${meta.viewport.height} @${meta.deviceScaleFactor}x`],
        ['Fixtures', Object.entries(meta.fixtures).map(([k, v]) => `${k} ${v.slice(0, 12)}`).join(', ')],
      ],
    ),
  );
  push('', table(
    ['Arm', 'Ref', 'SHA', 'Payload', 'Engine wasm sha256', 'Kernel wasm sha256'],
    Object.entries(meta.arms).map(([id, a]) => [
      id,
      a.ref,
      a.sha.slice(0, 9),
      a.fork ? `built (fork ${a.fork.sha.slice(0, 9)}, ${a.fork.target})` : a.payload,
      a.engineWasmSha256.slice(0, 12),
      a.kernelWasmSha256?.slice(0, 12) ?? '',
    ]),
  ));

  push('', '## Gate', '');
  for (const [id, g] of Object.entries(gate)) push(`- \`${id}\`: ${g.pass ? 'pass' : `excluded: ${g.reason}`}`);
  const errors = samples.filter((s) => s.error);
  if (errors.length) {
    push('', `${errors.length} samples failed and are left out:`, '');
    const byKind = {};
    for (const e of errors) (byKind[`${e.arm} ${e.flow}(${e.fixture}): ${e.error}`] ??= 0), byKind[`${e.arm} ${e.flow}(${e.fixture}): ${e.error}`]++;
    for (const [k, n] of Object.entries(byKind)) push(`- ${k} (x${n})`);
  }

  const sizes = meta.sizes ?? Object.fromEntries(meta.armsRun.map((id) => [id, distSizes(path.join(workDir(), 'dist', id))]));
  push('', '## Sizes', '');
  if (!meta.sizes) push('Measured from the current `.work/dist`, not recorded at run time.', '');
  const sizeKeys = ['engineWasmBytes', 'engineWasmBrotliBytes', 'kernelWasmBytes', 'kernelWasmBrotliBytes', 'jsBytes', 'jsBrotliBytes'];
  const sized = Object.keys(sizes);
  push(table(['Metric', ...sized], sizeKeys.map((k) => [k, ...sized.map((id) => fmt(k, sizes[id][k]))])));

  const [noiseRef, noiseCand] = [config.baseline, config.noiseArm];
  push('', `## A/A: \`${noiseRef}\` vs \`${noiseCand}\``, '');
  push('A metric passes when its interval includes the null value or its point estimate is within 3% of it. Its interval then sets the noise band every other pair must clear.', '');
  const aaBody = [];
  for (const [key, row] of allRows) {
    if (MEMORY.includes(row.metric)) continue;
    const c = calib.get(key);
    if (!c) continue;
    aaBody.push([
      row.label,
      cell(row.metric, row.byArm[noiseRef]),
      cell(row.metric, row.byArm[noiseCand]),
      c.iv ? intervalText(row.metric, c.iv) : '',
      c.invalid ? `invalid: ${c.invalid}` : c.ok ? 'pass' : 'A/A failed',
    ]);
  }
  const passed = aaBody.filter((r) => r[4] === 'pass').length;
  push(`${passed} of ${aaBody.length} rows pass.`, '');
  push(table(['Row', `${noiseRef} median [max]`, `${noiseCand} median [max]`, 'ratio or diff [95% CI]', 'A/A'], aaBody));

  for (const [ref, cand] of config.pairs) {
    if (ref === noiseRef && cand === noiseCand) continue;
    push('', `## \`${ref}\` vs \`${cand}\``, '');
    const out = [ref, cand].filter((id) => gate[id] && !gate[id].pass);
    const missing = [ref, cand].filter((id) => !gate[id]);
    if (missing.length) {
      push(`Not run: ${missing.map((id) => `\`${id}\``).join(', ')}.`);
      continue;
    }
    if (out.length) {
      push(out.map((id) => `\`${id}\` excluded: ${gate[id].reason}`).join('\n\n'));
      continue;
    }
    push(table(['Row', `${ref} median [max]`, `${cand} median [max]`, 'ratio or diff [95% CI]', 'threshold', 'Verdict'], pairTable(allRows, calib, ref, cand)));
  }

  push('', '## Memory', '');
  if (meta.memorySource === 'cdp') push('JS heap only, wasm linear memory not included.', '');
  const memRows = [...allRows.values()].filter((r) => MEMORY.includes(r.metric));
  const memArms = meta.armsRun.filter((id) => gate[id]?.pass);
  push(table(['Row', ...memArms.map((id) => `${id} median [max]`)], memRows.map((r) => [r.label, ...memArms.map((id) => (r.byArm[id] ? cell(r.metric, r.byArm[id]) : ''))])));

  const diag = samples.filter((s) => s.diagnostics);
  if (diag.length) {
    push('', '## Diagnostics', '', 'Kernel table counts from the Rust kernel at soak cycles 5 and 30, median over rounds. Not compared: no other arm has them.', '');
    for (const arm of [...new Set(diag.map((s) => s.arm))]) {
      const mine = diag.filter((s) => s.arm === arm);
      const keys = [...new Set(mine.flatMap((s) => Object.keys(s.diagnostics.cycle5 ?? {})))].sort();
      const med = (cycle, k) => summarize(mine.map((s) => s.diagnostics[cycle]?.[k]).filter(Number.isFinite)).median;
      push(`\`${arm}\`:`, '', table(['Table', 'Cycle 5', 'Cycle 30'], keys.map((k) => [k, fmt(k, med('cycle5', k)), fmt(k, med('cycle30', k))])), '');
    }
  }

  return lines.join('\n') + '\n';
}

if (values.against !== undefined) {
  console.error(
    'cross-run comparison removed: two runs do not share an A/A noise band, so a verdict across them is not one this harness can back. Put both builds in one run as arms and compare the pair.',
  );
  process.exit(2);
}
const dir = values.latest ? latestRun() : positionals[0] && path.resolve(positionals[0]);
if (!dir) throw new Error('usage: node report.mjs <results/run-id | --latest> [--publish]');
const run = loadRun(dir);
const config = loadConfig();
const text = report(run, config);
writeFileSync(path.join(dir, 'report.md'), text);
process.stdout.write(text);
if (values.publish) {
  if (run.meta.quick) {
    console.error('refusing to publish a quick run');
    process.exit(1);
  }
  const summary = '<!-- Hand-written: which pairs moved, by how much, and which rows are `no detectable difference`. -->\n\n';
  writeFileSync(path.join(here, 'RESULTS.md'), summary + text);
  console.error(`wrote ${path.relative(process.cwd(), path.join(here, 'RESULTS.md'))}`);
}
