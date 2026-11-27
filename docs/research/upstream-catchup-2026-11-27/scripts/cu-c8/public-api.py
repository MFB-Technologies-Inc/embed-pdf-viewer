#!/usr/bin/env python3
"""2026-11-27 (CU-C8). usage: public-api.py <build log> <PublicAPI.Unshipped.txt>
Applies the build's RS0016 and RS0017 diagnostics to the unshipped public API list: each symbol
RS0016 names (public, not declared) is added, each one RS0017 names (declared, not found) is
removed. Prints what changed. The list stays sorted as the analyzer's code fix keeps it."""
import re, sys
log, path = sys.argv[1], sys.argv[2]
text = open(log).read()
add = set(re.findall(r"RS0016: Symbol '(.+?)' is not part of the declared public API", text))
drop = set(re.findall(r"RS0017: Symbol '(.+?)' is part of the declared API", text))
lines = open(path).read().splitlines()
header = [l for l in lines if l.startswith('#')]
body = [l for l in lines if l and not l.startswith('#')]
kept = [l for l in body if l not in drop]
out = sorted(set(kept) | add)
open(path, 'w').write('\n'.join(header + out) + '\n')
print(f"added {len(add - set(body))}, removed {len(set(body) & drop)}, not found to remove {len(drop - set(body))}")
