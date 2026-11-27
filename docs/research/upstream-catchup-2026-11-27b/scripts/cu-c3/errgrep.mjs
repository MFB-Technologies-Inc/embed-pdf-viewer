// CU-C3 (2026-11-27b): copied from 2026-11-27 scripts/cu-c3/errgrep.mjs.
// errgrep.mjs <rev> <file...>: counts of suspicious strings (a generator out of step with the code
// it drives) in each vector file at <rev> and in the worktree.
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { zstdDecompressSync } from 'node:zlib';
const [rev, ...files] = process.argv.slice(2);
const text = (buf, f) => (f.endsWith('.zst') ? zstdDecompressSync(buf) : buf).toString('utf8');
const pats = ['is not a function', 'Cannot read properties', 'undefined is not', 'TypeError', 'unanswered'];
for (const f of files) {
  let o = '';
  try { o = text(execFileSync('git', ['show', `${rev}:${f}`], { maxBuffer: 1 << 30 }), f); } catch {}
  const n = text(readFileSync(f), f);
  const row = pats.map((p) => `${p}: ${o.split(p).length - 1}->${n.split(p).length - 1}`).filter((s) => !/: (\d+)->\1$/.test(s));
  console.log(f, row.length ? row.join('; ') : 'no change in suspicious strings');
}
