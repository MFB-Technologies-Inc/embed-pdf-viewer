// CU-C3 (2026-11-27b): copied from 2026-11-27 scripts/cu-c3/zcasediff.mjs.
// zcasediff.mjs <rev> <file...>: per vector file (plain or .zst), the cases added, removed or
// changed between <rev> and the worktree, with each changed case's covers text and top-level keys
// that differ. Run from the repo root.
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { zstdDecompressSync } from 'node:zlib';
const [rev, ...files] = process.argv.slice(2);
const parse = (buf, f) => JSON.parse((f.endsWith('.zst') ? zstdDecompressSync(buf) : buf).toString('utf8'));
for (const f of files) {
  let old;
  try { old = parse(execFileSync('git', ['show', `${rev}:${f}`], { maxBuffer: 1 << 30 }), f); } catch { console.log(`== ${f}: new`); continue; }
  const neu = parse(readFileSync(f), f);
  const o = new Map((old.cases ?? []).map((c) => [c.name, c]));
  const n = new Map((neu.cases ?? []).map((c) => [c.name, c]));
  const lines = [];
  for (const k of o.keys()) if (!n.has(k)) lines.push(`  REMOVED ${k}`);
  for (const [k, c] of n) {
    if (!o.has(k)) { lines.push(`  ADDED ${k}`); continue; }
    const a = o.get(k);
    if (JSON.stringify(a) === JSON.stringify(c)) continue;
    const keys = [...new Set([...Object.keys(a), ...Object.keys(c)])].filter((x) => JSON.stringify(a[x]) !== JSON.stringify(c[x]));
    lines.push(`  CHANGED ${k} [${keys.join(',')}]`);
  }
  const top = Object.keys(neu).filter((x) => x !== 'cases' && JSON.stringify(old[x]) !== JSON.stringify(neu[x]));
  console.log(`== ${f}: ${lines.length} cases differ of ${n.size}${top.length ? `; top-level ${top.join(',')}` : ''}`);
  for (const l of lines) console.log(l);
}
