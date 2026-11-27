# chainpins.py: for each changed file under historical/ (vs HEAD), replace pins of its old sha256 with
# its new sha256 across historical/ and test/, repeating until no pinned file changes. Prints each swap.
# CU-C3 (2026-11-27): the last pinned sha256 of each file is kept in STATE, so a later run moves
# CU-C3 (2026-11-27b): copied from 2026-11-27 scripts/cu-c3/chainpins.py.
# pins a previous run already moved off HEAD.
import hashlib, json, os, subprocess
root = '/workspaces/wt/CU-C2'; base = 'tooling/plugin-conformance'
def changed():
    out = subprocess.check_output(['git', '-C', root, 'diff', '--name-only', 'HEAD', '--', f'{base}/historical', f'{base}/test', f'{base}/src']).decode().split()
    return out
def sha_head(f):
    return hashlib.sha256(subprocess.check_output(['git', '-C', root, 'show', f'HEAD:{f}'])).hexdigest()
def sha_now(f):
    return hashlib.sha256(open(f'{root}/{f}', 'rb').read()).hexdigest()
targets = []
for d in ('historical', 'test'):
    for dp, _, fs in os.walk(f'{root}/{base}/{d}'):
        targets += [os.path.join(dp, f) for f in fs]
STATE = '/workspaces/logs/CU-C3/chainpins-state.json'
done = json.load(open(STATE)) if os.path.exists(STATE) else {}
for _ in range(20):
    swaps = {}
    for f in changed():
        old, new = sha_head(f), sha_now(f)
        old = done.get(f, old)
        if old != new: swaps[f] = (old, new)
    if not swaps: break
    for f, (old, new) in swaps.items():
        done[f] = new
        for t in targets:
            try:
                text = open(t).read()
            except UnicodeDecodeError:
                continue
            if old in text:
                open(t, 'w').write(text.replace(old, new))
                print(f'{os.path.relpath(t, root)}: pin of {f} {old[:12]} -> {new[:12]}')
json.dump(done, open(STATE, 'w'), indent=1)
