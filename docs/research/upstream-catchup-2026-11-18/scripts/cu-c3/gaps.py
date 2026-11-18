# gaps.py [root]: uncovered statement lines (vectors/coverage.json) that upstream added in
# ed064e0c8..c4e744345, grouped by package, with the upstream commit that last touched each line
# (git blame at c4e744345). Also lists upstream-touched source files coverage.json reports unloaded.
import json, re, subprocess, collections, sys
root = sys.argv[1] if len(sys.argv) > 1 else '/workspaces/wt/CU-C3'
BASE, TIP = 'ed064e0c8', 'c4e744345'
cov = json.load(open(f'{root}/tooling/plugin-conformance/vectors/coverage.json'))
def ranges(v):
    out = set()
    for x in v:
        if isinstance(x, int): out.add(x)
        else:
            m = re.match(r'(\d+)-(\d+)', str(x))
            if m: out.update(range(int(m.group(1)), int(m.group(2)) + 1))
            elif str(x).isdigit(): out.add(int(x))
    return out
diff = subprocess.check_output(['git', '-C', root, 'diff', '-U0', BASE, TIP, '--', 'packages']).decode(errors='replace')
added = collections.defaultdict(set); cur = None
for line in diff.splitlines():
    if line.startswith('+++ '): cur = line[6:] if line.startswith('+++ b/') else None
    elif line.startswith('@@') and cur:
        m = re.search(r'\+(\d+)(?:,(\d+))?', line); a = int(m.group(1)); n = int(m.group(2) if m.group(2) is not None else 1)
        added[cur].update(range(a, a + n))
res = collections.defaultdict(list); total = 0
for path, info in sorted(cov['files'].items()):
    if path not in added: continue
    hit = sorted(ranges(info.get('uncoveredLines', [])) & added[path])
    if not hit: continue
    total += len(hit)
    blame = subprocess.check_output(['git', '-C', root, 'blame', '-s', '-l', TIP, '--', path]).decode(errors='replace').splitlines()
    commits = sorted({blame[l - 1][:9] for l in hit if l - 1 < len(blame)})
    pkg = '/'.join(path.split('/')[1:3])
    spans = []
    for l in hit:
        if spans and l == spans[-1][1] + 1: spans[-1][1] = l
        else: spans.append([l, l])
    res[pkg].append((path, ','.join(f'{a}' if a == b else f'{a}-{b}' for a, b in spans), len(hit), commits))
unloaded = [u for u in cov.get('unloaded', []) if u in added]
print(f'# {total} uncovered lines that {BASE}..{TIP} added, in {sum(len(v) for v in res.values())} files')
for pkg in sorted(res):
    print(f'## {pkg} ({sum(r[2] for r in res[pkg])})')
    for path, sp, n, commits in res[pkg]: print(f'- {path}: lines {sp} ({n}; {" ".join(commits)})')
print(f'## unloaded files upstream touched ({len(unloaded)})')
for u in unloaded: print('-', u)
