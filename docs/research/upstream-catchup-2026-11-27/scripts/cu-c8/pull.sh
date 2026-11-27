#!/usr/bin/env bash
# 2026-11-27 (CU-C8). Run on the Mac host; text only, no build tool.
# Copies the container worktree's changed and untracked files back into the host edit worktree
# (after cargo fmt or a generator ran there).
set -euo pipefail
H=/Users/andrew/repos/embed-pdf-viewer/.claude/worktrees/c8-edit
files=$(docker exec -u node embed-pdf-catchup1127 bash -c 'cd /workspaces/wt/CU-C8 && { git diff --name-only HEAD; git ls-files --others --exclude-standard; } | sort -u')
[[ -z "$files" ]] && { echo "nothing to pull"; exit 0; }
docker exec -u node embed-pdf-catchup1127 bash -c "cd /workspaces/wt/CU-C8 && tar -cf - $(echo $files)" | tar -xf - -C "$H"
echo "pulled $(echo "$files" | wc -l | tr -d ' ') files"
