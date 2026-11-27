#!/usr/bin/env python3
"""CU-C9 (2026-11-27): scripts/lib/linked-crates.mjs's APPLE_CRATES check in
Python. Reads `cargo metadata --format-version 1` JSON on stdin, walks
epdf-viewer-host's normal and build dependencies, and compares the local
crates (as paths under crates/) with APPLE_CRATES in apple-engine-hash.sh.
JavaScript's default sort orders by UTF-16 code units; for these ASCII names
Python's sorted() gives the same order.

    cargo metadata --format-version 1 | apple-crates.py <apple-engine-hash.sh>
"""
import json
import os
import re
import sys

metadata = json.load(sys.stdin)
nodes = {n['id']: n for n in metadata['resolve']['nodes']}
packages = {p['id']: p for p in metadata['packages']}
stack = [next(p['id'] for p in metadata['packages'] if p['name'] == 'epdf-viewer-host')]
linked = set()
while stack:
    pid = stack.pop()
    if pid in linked:
        continue
    linked.add(pid)
    for dep in nodes[pid]['deps']:
        if any(k['kind'] in (None, 'build') for k in dep['dep_kinds']):
            stack.append(dep['pkg'])
crates_root = os.path.join(metadata['workspace_root'], 'crates')
local = sorted(
    os.path.relpath(os.path.dirname(packages[i]['manifest_path']), crates_root).replace(os.sep, '/')
    for i in linked if not packages[i].get('source'))
script = open(sys.argv[1]).read()
listed = sorted(re.search(r'^APPLE_CRATES=\((.*)\)$', script, re.M).group(1).split())
print('linked:', ' '.join(local))
print('listed:', ' '.join(listed))
print('missing from APPLE_CRATES:', sorted(set(local) - set(listed)) or 'none')
print('extra in APPLE_CRATES:', sorted(set(listed) - set(local)) or 'none')
sys.exit(0 if local == listed else 1)
