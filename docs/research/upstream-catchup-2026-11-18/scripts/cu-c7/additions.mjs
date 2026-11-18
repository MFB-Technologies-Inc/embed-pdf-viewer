// additions.mjs <start> <end> <out>: every scenario or table case the 2026-11-18 catch-up added to
// tooling/plugin-conformance/vectors, read from git at <start> and <end>: file, name, and the
// case's covers text. The evidence tests count these as later additions, as the 2026-11-12
// catch-up's lists did.
import { execFileSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { zstdDecompressSync } from 'node:zlib';
const [start, end, out] = process.argv.slice(2);
const base = 'tooling/plugin-conformance/vectors/';
const git = (...args) => execFileSync('git', args, { maxBuffer: 1 << 30, stdio: ['ignore', 'pipe', 'ignore'] });
const read = (rev, path) => {
  try {
    const buf = git('show', `${rev}:${path}`);
    return JSON.parse((path.endsWith('.zst') ? zstdDecompressSync(buf) : buf).toString('utf8'));
  } catch {
    return null;
  }
};
const files = git('ls-tree', '-r', '--name-only', end, base).toString().trim().split('\n')
  .filter((p) => /\.json(\.zst)?$/.test(p) && !/(^|\/)(rust-)?coverage\.json$/.test(p));
const additions = [];
for (const path of files) {
  const now = read(end, path);
  if (!now || !Array.isArray(now.cases)) continue;
  const before = read(start, path);
  const had = new Set((before?.cases ?? []).map((c) => c.name));
  for (const c of now.cases)
    if (!had.has(c.name))
      additions.push({
        file: path.slice(base.length).replace(/\.zst$/, ''),
        name: c.name,
        newFile: before === null,
        covers: c.covers ?? null,
      });
}
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, JSON.stringify({ start, end, additions }, null, 2) + '\n');
console.log(`${additions.length} cases in ${new Set(additions.map((a) => a.file)).size} files`);
