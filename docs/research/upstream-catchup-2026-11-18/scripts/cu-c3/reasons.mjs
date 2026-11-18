// reasons.mjs <rev> <file>: for each case whose settled values differ, the old and new "reason"
// strings and whether the trust path changed length.
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { zstdDecompressSync } from 'node:zlib';
const [rev, f] = process.argv.slice(2);
const parse = (b) => JSON.parse((f.endsWith('.zst') ? zstdDecompressSync(b) : b).toString('utf8'));
const o = new Map(parse(execFileSync('git', ['show', `${rev}:${f}`], { maxBuffer: 1 << 30 })).cases.map((c) => [c.name, c]));
const grab = (x, k, acc = []) => { if (x && typeof x === 'object') for (const [kk, v] of Object.entries(x)) { if (kk === k) acc.push(v); grab(v, k, acc); } return acc; };
const tally = new Map();
for (const c of parse(readFileSync(f)).cases) {
  const a = o.get(c.name); if (!a || JSON.stringify(a.trace) === JSON.stringify(c.trace)) continue;
  const ra = JSON.stringify(grab(a.trace, 'reason')), rb = JSON.stringify(grab(c.trace, 'reason'));
  const pa = grab(a.trace, 'path').map((p) => Array.isArray(p) ? p.length : -1).join(','), pb = grab(c.trace, 'path').map((p) => Array.isArray(p) ? p.length : -1).join(',');
  const key = `${ra === rb ? 'same reasons' : `reasons ${ra} -> ${rb}`}; path lengths ${pa} -> ${pb}`;
  tally.set(key, [...(tally.get(key) ?? []), c.name]);
}
for (const [k, v] of tally) console.log(`${v.length}: ${k.slice(0, 400)}\n    ${v.join(' ')}`);
