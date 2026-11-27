// CU-C3 (2026-11-27b): copied from 2026-11-27 scripts/cu-c3/restamp.mts.
// restamp.mts <suite-module> <history-dir>: re-stamp a historical current-inputs.json to the suite's
// current cases. Each stored row whose case input changed gets the case's canonical input and its
// sha256; every other file in <history-dir> has the old digest replaced by the new one (accounting
// mappings and additions). Prints each change. Run from tooling/plugin-conformance with
// `node --import=tsx --conditions=development`, then `pnpm exec prettier --write` the rewritten
// current-inputs.json (the archives are Prettier-formatted).
import { createHash } from 'node:crypto';
import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
const [mod, dir] = process.argv.slice(2);
const { canonical } = (await import(pathToFileURL(resolve('src/canonical.ts')).href)) as { canonical: (v: unknown) => unknown };
const { suites } = (await import(pathToFileURL(resolve(mod!)).href)) as { suites: { file: string; cases: { name: string }[] }[] };
const sha = (s: string) => createHash('sha256').update(s).digest('hex');
const path = join(dir!, 'current-inputs.json');
const text = readFileSync(path, 'utf8');
const rows = JSON.parse(text) as { file: string; name: string; input: unknown; inputSha256: string }[];
const swaps = new Map<string, string>();
for (const row of rows) {
  const suite = suites.find((s) => s.file === row.file);
  const c = suite?.cases.find((x) => x.name === row.name);
  if (!c) { console.log('NOT FOUND', row.file, row.name); continue; }
  const input = canonical(c);
  const digest = sha(JSON.stringify(input));
  if (digest === row.inputSha256) continue;
  console.log(`${row.file} ${row.name}: ${row.inputSha256.slice(0, 12)} -> ${digest.slice(0, 12)}`);
  swaps.set(row.inputSha256, digest);
  row.input = input;
  row.inputSha256 = digest;
}
if (!swaps.size) process.exit(0);
const indent = text.startsWith('[\n  ') ? 2 : 0;
writeFileSync(path, JSON.stringify(rows, null, indent) + (text.endsWith('\n') ? '\n' : ''));
for (const f of readdirSync(dir!)) {
  if (f === 'current-inputs.json' || !f.endsWith('.json')) continue;
  let t = readFileSync(join(dir!, f), 'utf8'); const before = t;
  for (const [o, n] of swaps) t = t.split(o).join(n);
  if (t !== before) { writeFileSync(join(dir!, f), t); console.log(`${f}: digests replaced`); }
}
