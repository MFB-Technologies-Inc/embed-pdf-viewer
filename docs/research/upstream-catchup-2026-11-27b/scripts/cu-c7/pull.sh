#!/usr/bin/env bash
# 2026-11-27b (CU-C7), from ../../../upstream-catchup-2026-11-27/scripts/cu-c8/pull.sh.
# Run on the Mac host; text only. Copies the container worktree's changed and untracked files back
# into the host worktree (after cargo fmt or a generator ran there).
set -euo pipefail
H=/Users/andrew/repos/embed-pdf-viewer/.claude/worktrees/catchup-1127b-platforms C=embed-pdf-catchup1127b D=${D:-/workspaces/wt/CU-C7}
files=$(docker exec -u node $C bash -c "cd $D && { git diff --name-only HEAD; git ls-files --others --exclude-standard; } | sort -u")
[[ -z "$files" ]] && { echo "nothing to pull"; exit 0; }
docker exec -u node $C bash -c "cd $D && tar -cf - $(echo $files)" | tar -xf - -C "$H"
echo "pulled $(echo "$files" | wc -l | tr -d ' ') files"
