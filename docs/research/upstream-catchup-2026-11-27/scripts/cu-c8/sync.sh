#!/usr/bin/env bash
# 2026-11-27 (CU-C8). Run on the Mac host; text only, no build tool.
# Copies the host edit worktree's changed and untracked files into the container worktree, with
# fresh mtimes (tar -m), as the container user. Deleted files are removed there too.
set -euo pipefail
H=/Users/andrew/repos/embed-pdf-viewer/.claude/worktrees/c8-edit
cd "$H"
files=$( { git diff --name-only HEAD; git ls-files --others --exclude-standard; } | sort -u)
present=(); gone=()
for f in $files; do if [[ -e "$f" ]]; then present+=("$f"); else gone+=("$f"); fi; done
if ((${#present[@]})); then COPYFILE_DISABLE=1 tar --no-xattrs -cf - "${present[@]}" | docker exec -i -u node embed-pdf-catchup1127 tar -xmf - -C /workspaces/wt/CU-C8; fi
for f in "${gone[@]:-}"; do [[ -n "$f" ]] && docker exec -u node embed-pdf-catchup1127 rm -f "/workspaces/wt/CU-C8/$f"; done
echo "synced ${#present[@]} files, removed ${#gone[@]}"
