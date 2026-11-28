#!/usr/bin/env python3
# CU-C6 (2026-11-27b): copied from 2026-11-27 cu-c7/ts-count.py.
# CU-C7 (2026-11-27): sum the vitest "Tests" lines and node --test "ℹ pass/fail/skip" lines of a
# turbo test log. usage: ts-count.py <ts.log>
import re, sys
t = {"passed": 0, "failed": 0, "skipped": 0}
ansi = re.compile(r"\x1b\[[0-9;]*m")
for line in open(sys.argv[1], encoding="utf-8", errors="replace"):
    line = ansi.sub("", line)
    m = re.search(r":test:\s+Tests\s+(.*)\(\d+\)", line)
    if m:
        for n, k in re.findall(r"(\d+) (passed|failed|skipped)", m.group(1)):
            t[k] += int(n)
        continue
    m = re.search(r":test: ℹ (pass|fail|skipped) (\d+)", line)
    if m:
        t[{"pass": "passed", "fail": "failed", "skipped": "skipped"}[m.group(1)]] += int(m.group(2))
print(" ".join(f"{k}={v}" for k, v in t.items()))
