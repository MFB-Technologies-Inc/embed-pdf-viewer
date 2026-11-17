#!/usr/bin/env node
// Times two engine-timer builds against each other. See README.md.
//
//   node compare.mjs --a <binary> --b <binary> --fixtures <dir> --corpus <root>
//                    [--rounds 10] [--scale 1] [--names a,b] [--out <file.json>]
//
// Each build's answers are checked against the conformance vectors first.
// The results file records the run's id, when it ran, the machine, and each
// arm's build (`engine-timer build`: its commit and LTO setting).
// Then every round runs A, an identical copy of A, and B, each in its own
// process, in an order that rotates by round. A row's sample for a round is
// the median of that process's calls. The A/A pair sets the noise band the
// A/B pair has to clear.

import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { createHash, randomUUID } from 'node:crypto';
import os from 'node:os';
import path from 'node:path';
import { parseArgs } from 'node:util';

import { bootstrapRatio, calibrate, comparePair, noiseBand, summarize, threshold } from './stats.mjs';

const { values: args } = parseArgs({
  options: {
    a: { type: 'string' },
    b: { type: 'string' },
    fixtures: { type: 'string' },
    corpus: { type: 'string' },
    rounds: { type: 'string', default: '10' },
    scale: { type: 'string', default: '1' },
    names: { type: 'string', default: 'A,B' },
    out: { type: 'string' },
  },
});
for (const key of ['a', 'b', 'fixtures', 'corpus']) {
  if (!args[key]) {
    console.error(`missing --${key}`);
    process.exit(2);
  }
}
const rounds = Number(args.rounds);
const [nameA, nameB] = args.names.split(',');

const sha256 = (file) => createHash('sha256').update(readFileSync(file)).digest('hex');
const runId = randomUUID();
process.stdout.write(`run ${runId}\n`);

// The A/A arm is a byte-identical copy beside A, so it loads the same library.
const a = path.resolve(args.a);
const aa = `${a}-aa`;
copyFileSync(a, aa);
const arms = [
  { name: nameA, bin: a },
  { name: `${nameA}-aa`, bin: aa },
  { name: nameB, bin: path.resolve(args.b) },
];

const run = (bin, argv) => execFileSync(bin, argv, { encoding: 'utf8', maxBuffer: 1 << 28 });

// What each binary was built from. A binary older than `build` answers
// nothing, and its build is recorded as unknown.
const builds = Object.fromEntries(
  arms
    .filter((arm) => arm.bin !== aa)
    .map((arm) => {
      try {
        return [arm.name, JSON.parse(run(arm.bin, ['build']))];
      } catch {
        return [arm.name, null];
      }
    }),
);
// The checkout this script runs from, when it runs from one.
let harness = null;
try {
  harness = execFileSync('git', ['rev-parse', 'HEAD'], {
    cwd: path.dirname(new URL(import.meta.url).pathname),
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'ignore'],
  }).trim();
} catch {
  // A copy of the script with no checkout around it.
}

for (const arm of arms.filter((arm) => arm.bin !== aa)) {
  try {
    process.stdout.write(`${arm.name}: ${run(arm.bin, ['check', args.corpus]).trim().split('\n').at(-1)}\n`);
  } catch (error) {
    process.stderr.write(`${arm.name}: the answer check failed\n${error.stdout ?? ''}${error.stderr ?? ''}`);
    process.exit(1);
  }
}

const median = (values) => summarize(values).median;
// samples[arm][row] = one value per round, in milliseconds.
const samples = Object.fromEntries(arms.map((arm) => [arm.name, {}]));
const digests = {};
const started = new Date().toISOString();
const loadBefore = os.loadavg();

for (let round = 0; round < rounds; round++) {
  const order = arms.map((_, i) => arms[(i + round) % arms.length]);
  for (const arm of order) {
    const argv = ['round', args.fixtures, '--scale', args.scale];
    for (const line of run(arm.bin, argv).trim().split('\n')) {
      const { row, digest, samplesNs } = JSON.parse(line);
      (samples[arm.name][row] ??= []).push(median(samplesNs) / 1e6);
      (digests[row] ??= new Set()).add(digest);
    }
  }
  process.stdout.write(`round ${round + 1}/${rounds}: ${order.map((arm) => arm.name).join(', ')}\n`);
}
rmSync(aa);

// A build that answers differently is not timed against the other: a wrong
// answer can be a fast one.
const differing = Object.entries(digests).filter(([, set]) => set.size > 1).map(([row]) => row);
if (differing.length) {
  console.error(`the builds answer differently on: ${differing.join(', ')}`);
  console.error('`engine-timer round <fixtures> --dump <dir>` with each build writes the answers to diff');
  process.exit(1);
}

const pct = (x) => `${(x * 100).toFixed(1)}%`;
const ms = (x) => (x >= 10 ? x.toFixed(1) : x >= 1 ? x.toFixed(2) : x.toFixed(3));
const cell = (values) => `${ms(median(values))} [${ms(summarize(values).max)}]`;

const rows = Object.keys(samples[nameA]).map((row) => {
  const [ref, aaValues, cand] = arms.map((arm) => samples[arm.name][row]);
  const aaInterval = bootstrapRatio(ref, aaValues);
  const calibration = calibrate(aaInterval);
  const interval = bootstrapRatio(ref, cand);
  return {
    row,
    medians: { [nameA]: median(ref), [`${nameA}-aa`]: median(aaValues), [nameB]: median(cand) },
    aa: { ...aaInterval, ok: calibration.ok, noise: noiseBand(aaInterval) },
    interval,
    threshold: calibration.ok ? threshold(interval, calibration.noise) : null,
    verdict: comparePair(interval, calibration),
    samples: { [nameA]: ref, [`${nameA}-aa`]: aaValues, [nameB]: cand },
  };
});

console.log(`\n${rounds} rounds, ms per call, median [max] over rounds; ratio is ${nameB} / ${nameA}\n`);
console.log(`| row | ${nameA} | ${nameA}-aa | ${nameB} | A/A ratio | ratio [95% CI] | threshold | verdict |`);
console.log('| --- | --- | --- | --- | --- | --- | --- | --- |');
for (const r of rows) {
  const [ref, aaValues, cand] = Object.values(r.samples);
  const aaText = `${r.aa.ratio.toFixed(3)}${r.aa.ok ? '' : ' (failed)'}`;
  const ci = `${r.interval.ratio.toFixed(3)} [${r.interval.lo.toFixed(3)}, ${r.interval.hi.toFixed(3)}]`;
  const t = r.threshold === null ? '' : pct(r.threshold);
  const verdict = { same: 'no detectable difference', slower: 'slower', faster: 'faster', unreliable: 'A/A failed' }[r.verdict];
  console.log(`| ${r.row} | ${cell(ref)} | ${cell(aaValues)} | ${cell(cand)} | ${aaText} | ${ci} | ${t} | ${verdict} |`);
}

if (args.out) {
  const meta = {
    runId,
    started,
    finished: new Date().toISOString(),
    host: os.hostname(),
    cpu: os.cpus()[0]?.model,
    cpus: os.cpus().length,
    memoryBytes: os.totalmem(),
    os: `${os.type()} ${os.release()} ${os.arch()}`,
    node: process.version,
    harness,
    builds,
    loadBefore,
    loadAfter: os.loadavg(),
    rounds,
    scale: Number(args.scale),
    arms: arms.map((arm) => ({ name: arm.name, bin: arm.bin })),
    sha256: { [nameA]: sha256(a), [nameB]: sha256(arms[2].bin) },
  };
  writeFileSync(args.out, `${JSON.stringify({ meta, rows }, null, 2)}\n`);
  console.log(`\nwrote ${args.out}`);
}
