// zcasepatch.mjs <rev> <file> <case> [maxlines]: line diff of one case (plain or .zst), <rev> vs worktree.
import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync, mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { zstdDecompressSync } from 'node:zlib';
const [rev, f, name, max = '120'] = process.argv.slice(2);
const parse = (buf) => JSON.parse((f.endsWith('.zst') ? zstdDecompressSync(buf) : buf).toString('utf8'));
const pick = (d) => d.cases.find((c) => c.name === name);
const d = mkdtempSync(join(tmpdir(), 'zcp-'));
writeFileSync(join(d, 'a'), JSON.stringify(pick(parse(execFileSync('git', ['show', `${rev}:${f}`], { maxBuffer: 1 << 30 }))), null, 1) + '\n');
writeFileSync(join(d, 'b'), JSON.stringify(pick(parse(readFileSync(f))), null, 1) + '\n');
let out = '';
try { out = execFileSync('diff', ['-U3', join(d, 'a'), join(d, 'b')]).toString(); } catch (e) { out = e.stdout.toString(); }
console.log(out.split('\n').slice(0, Number(max)).map((l) => l.slice(0, 260)).join('\n'));
