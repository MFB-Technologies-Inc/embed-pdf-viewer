// fixrow.mjs <case>: re-pin one task-12b2b1b-ii accounting row (annotation-tools) to the case's
// current canonical input digest (given as argv[3]) and covers text. Text edit, formatting kept.
import { readFileSync, writeFileSync } from 'node:fs';
import { zstdDecompressSync } from 'node:zlib';
const [name, digest] = process.argv.slice(2);
const root = '/workspaces/wt/CU-C3/tooling/plugin-conformance';
const p = `${root}/historical/task-12b2b1b-ii/accounting.json`;
const s = readFileSync(p, 'utf8');
const suite = JSON.parse(zstdDecompressSync(readFileSync(`${root}/vectors/plugins/annotation-tools.json.zst`)).toString('utf8'));
const c = suite.cases.find((x) => x.name === name);
const start = s.indexOf(`"before": "${name}"`); const end = s.indexOf('}', start);
let row = s.slice(start, end);
const nrow = row
  .replace(/"currentCanonicalInputSha256": "[0-9a-f]{64}"/, `"currentCanonicalInputSha256": "${digest}"`)
  .replace(/"afterCovers": "(?:[^"\\]|\\.)*"/, `"afterCovers": ${JSON.stringify(c.covers)}`);
if (nrow === row) throw new Error('nothing changed');
writeFileSync(p, s.slice(0, start) + nrow + s.slice(end));
console.log('ok');
