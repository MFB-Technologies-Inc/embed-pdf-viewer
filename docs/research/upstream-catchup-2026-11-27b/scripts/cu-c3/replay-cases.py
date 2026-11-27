# CU-C3 (2026-11-27b): copied from 2026-11-27 scripts/cu-c3/replay-cases.py.
# replay-cases.py <cargo.log>: every vector case a Rust replay reported as differing, as
# "file<TAB>case<TAB>first difference" (from the "N of M cases differ" blocks).
import re, sys
lines = open(sys.argv[1], errors='replace').read().splitlines()
cur = None
for l in lines:
    m = re.match(r'^(\S+\.json): (\d+) of (\d+) cases differ from the TypeScript:$', l)
    if m: cur = m.group(1); continue
    if cur:
        m = re.match(r'^  (\S+): (.*)$', l)
        if m: print(f'{cur}\t{m.group(1)}\t{m.group(2)[:200]}'); continue
        cur = None
