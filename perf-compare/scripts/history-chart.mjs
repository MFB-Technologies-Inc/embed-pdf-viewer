// Builds the history page from the recorded runs.
//
//   node scripts/history-chart.mjs
//
// Reads history/points.json and every run under history/runs/, and writes
// history/data.json (lib/history.mjs `seriesData`) and history/index.html:
// lib/history-page.html with that data in place of its DATA marker. Both
// are committed, so the page opens from a checkout. The output depends only
// on its inputs: the bootstrap is seeded and nothing stamps the time.
import { existsSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { seriesData } from '../lib/history.mjs';

const here = fileURLToPath(new URL('..', import.meta.url));
const dir = path.join(here, 'history');
const history = JSON.parse(readFileSync(path.join(dir, 'points.json'), 'utf8'));
const runsDir = path.join(dir, 'runs');
const runs = (existsSync(runsDir) ? readdirSync(runsDir) : [])
  .filter((id) => existsSync(path.join(runsDir, id, 'meta.json')))
  .sort()
  .map((id) => {
    const read = (f) => readFileSync(path.join(runsDir, id, f), 'utf8');
    return {
      meta: JSON.parse(read('meta.json')),
      gate: JSON.parse(read('gate.json')),
      samples: read('samples.jsonl').split('\n').filter(Boolean).map((l) => JSON.parse(l)),
    };
  });

const data = seriesData({
  runs,
  points: history.points,
  reference: history.reference,
  noise: history.noise,
  series: Object.fromEntries(Object.entries(history.series).map(([id, s]) => [id, { label: s.label }])),
});
data.planned = history.points.filter((p) => !p.skip).length;
// The reference arm is one fixed commit; the page names it by that commit,
// so it can't be read as the upstream series, which is also `main`.
const pin = history.points.find((p) => p.arm === history.reference);
if (pin) data.baseline = { sha: pin.sha, date: pin.date };
writeFileSync(path.join(dir, 'data.json'), JSON.stringify(data, null, 1) + '\n');

const MARKER = '/*DATA*/null';
const page = readFileSync(path.join(here, 'lib/history-page.html'), 'utf8');
if (!page.includes(MARKER)) throw new Error(`lib/history-page.html has no ${MARKER} marker`);
// `<` is escaped so no value can close the script element.
const json = JSON.stringify(data).replace(/</g, '\\u003c');
const head = '<!doctype html>\n<html lang="en">\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width, initial-scale=1">\n';
writeFileSync(path.join(dir, 'index.html'), head + page.replace(MARKER, () => json));
console.log(`${data.points.length} of ${data.planned} points, ${data.rows.length} rows, from ${runs.length} run${runs.length === 1 ? '' : 's'}`);
