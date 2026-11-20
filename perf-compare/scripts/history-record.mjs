// Adds a finished run to the history.
//
//   node scripts/history-record.mjs <results/run-id>
//
// Copies the run's samples.jsonl, gate.json and meta.json to
// history/runs/<run-id>/ once lib/history.mjs `eligible` passes it. A run
// that doesn't qualify prints each reason and exits 1 with nothing copied.
// This is the only way a run gets into history/runs/, so every run there
// compares with the others. Then run scripts/history-chart.mjs.
import { copyFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { eligible } from '../lib/history.mjs';

const here = fileURLToPath(new URL('..', import.meta.url));
const FILES = ['samples.jsonl', 'gate.json', 'meta.json'];
const arg = process.argv[2];
if (!arg) throw new Error('usage: node scripts/history-record.mjs <results/run-id>');
const dir = path.resolve(arg);
for (const f of FILES) {
  if (!existsSync(path.join(dir, f))) throw new Error(`${dir} has no ${f}: the run didn't finish`);
}
const read = (f) => JSON.parse(readFileSync(path.join(dir, f), 'utf8'));
const history = JSON.parse(readFileSync(path.join(here, 'history/points.json'), 'utf8'));
const meta = read('meta.json');
const reasons = eligible(meta, read('gate.json'), history);
if (reasons.length) {
  console.error(`${meta.runId} can't join the history:`);
  for (const r of reasons) console.error(`- ${r}`);
  process.exit(1);
}
const out = path.join(here, 'history/runs', meta.runId);
if (existsSync(out)) {
  console.log(`${meta.runId} is already recorded`);
} else {
  mkdirSync(out, { recursive: true });
  for (const f of FILES) copyFileSync(path.join(dir, f), path.join(out, f));
  const measured = history.points.filter((p) => !p.skip && meta.armsRun.includes(p.arm) && meta.arms[p.arm]?.sha === p.sha);
  console.log(`recorded ${meta.runId}: ${measured.length} points (${measured.map((p) => p.arm).join(', ') || 'none'})`);
}
