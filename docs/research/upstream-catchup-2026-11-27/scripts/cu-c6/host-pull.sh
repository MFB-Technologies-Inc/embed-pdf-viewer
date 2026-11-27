#!/usr/bin/env bash
# CU-C6 (2026-11-27), host side (git and tar only, no node or cargo): see decisions/CU-C6.md D1.
# usage: pull.sh : copy every file that differs from 053b0a901 in the container CU-C6 worktree
# (committed or not) back to the host edit worktree.
cd /Users/andrew/repos/embed-pdf-viewer/.claude/worktrees/c6-edit || exit 2
files=$(docker exec embed-pdf-catchup1127 bash -c 'cd /workspaces/wt/CU-C6 && { git diff --name-only 053b0a901 --diff-filter=d; git ls-files --others --exclude-standard; } | sort -u')
[ -n "$files" ] && docker exec embed-pdf-catchup1127 bash -c "cd /workspaces/wt/CU-C6 && tar -cf - $(echo $files)" | tar -xf -
echo "$files" | wc -l
