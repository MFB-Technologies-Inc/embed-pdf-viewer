# cargo-by-crate.py <cargo.log> <out.tsv> <test-binaries.txt>: every failing test of a
# `cargo test --workspace --no-fail-fast` log as "crate<TAB>target<TAB>test<TAB>first panic line".
# test-binaries.txt maps each test executable to its package: lines "<file name> <package> <target>"
# from `cargo test --workspace --no-run --message-format=json`.
import re, sys, collections
log, out_path, bins_path = sys.argv[1:4]
bins = {l.split()[0]: l.split()[1] for l in open(bins_path) if l.strip()}
lines = open(log, errors='replace').read().splitlines()
cur = None; fails = []; panics = {}
for i, l in enumerate(lines):
    m = re.match(r'^\s+Running (?:unittests )?(\S+) \((?:\S*/)?deps/(([A-Za-z0-9_]+)-[0-9a-f]+)\)', l)
    if m:
        cur = (bins.get(m.group(2), '?'), m.group(3)); continue
    m = re.match(r'^test (.*) \.\.\. FAILED$', l)
    if m and cur: fails.append((cur[0], cur[1], m.group(1)))
    m = re.match(r'^---- (.*) stdout ----$', l)
    if m and cur:
        msg = ''
        for k in range(i + 1, min(i + 40, len(lines))):
            t = lines[k]
            if t.startswith('---- ') or t.startswith('failures:'): break
            if re.search(r'panicked at [^:]+:\d+:\d+:$', t) and k + 1 < len(lines): msg = lines[k + 1].strip(); break
            mm = re.search(r'panicked at (.*)$', t)
            if mm: msg = mm.group(1).strip(); break
        panics[(cur[0], cur[1], m.group(1))] = msg[:300]
with open(out_path, 'w') as out:
    for f in sorted(fails): print('\t'.join(f + (panics.get(f, ''),)), file=out)
c = collections.Counter(f[0] for f in fails)
print(f'# {len(fails)} failing tests in {len(c)} crates')
for k, v in sorted(c.items()): print(f'#   {k}: {v}')
