// diffpaths.mjs <rev> <file...>: per vector file (plain or .zst), the JSON paths that differ between
// <rev> and the worktree across all cases, array indices folded to [], counted, most frequent first,
// each with one example of old and new value. Run from the repo root.
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { zstdDecompressSync } from 'node:zlib';
const [rev, ...files] = process.argv.slice(2);
const parse = (buf, f) => JSON.parse((f.endsWith('.zst') ? zstdDecompressSync(buf) : buf).toString('utf8'));
const short = (v) => { const s = JSON.stringify(v); return s === undefined ? 'undefined' : s.length > 90 ? s.slice(0, 87) + '...' : s; };
function walk(a, b, path, out) {
  if (JSON.stringify(a) === JSON.stringify(b)) return;
  const obj = (x) => x && typeof x === 'object';
  if (obj(a) && obj(b) && Array.isArray(a) === Array.isArray(b)) {
    if (Array.isArray(a) && a.length !== b.length) { out.push([path + '.length', a.length, b.length]); }
    const keys = Array.isArray(a) ? [...Array(Math.min(a.length, b.length)).keys()] : [...new Set([...Object.keys(a), ...Object.keys(b)])];
    for (const k of keys) walk(a[k], b[k], Array.isArray(a) ? `${path}[]` : `${path}.${k}`, out);
    return;
  }
  out.push([path, a, b]);
}
for (const f of files) {
  let old;
  try { old = parse(execFileSync('git', ['show', `${rev}:${f}`], { maxBuffer: 1 << 30 }), f); } catch { console.log(`== ${f}: new`); continue; }
  const neu = parse(readFileSync(f), f);
  const o = new Map((old.cases ?? []).map((c) => [c.name, c]));
  const counts = new Map();
  for (const c of neu.cases ?? []) {
    const a = o.get(c.name); if (!a) continue;
    const out = []; walk(a, c, '', out);
    const seen = new Set();
    for (const [p, x, y] of out) { if (seen.has(p)) continue; seen.add(p); const e = counts.get(p) ?? { n: 0, x, y, c: c.name }; e.n++; counts.set(p, e); }
  }
  console.log(`== ${f}`);
  for (const [p, e] of [...counts].sort((l, r) => r[1].n - l[1].n).slice(0, 25)) console.log(`  ${e.n}x ${p}: ${short(e.x)} -> ${short(e.y)} (e.g. ${e.c})`);
}
