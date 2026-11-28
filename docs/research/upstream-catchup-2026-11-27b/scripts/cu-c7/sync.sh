#!/usr/bin/env bash
# 2026-11-27b (CU-C7), from ../../../upstream-catchup-2026-11-27/scripts/cu-c8/sync.sh.
# Run on the Mac host; text only, no build tool. Copies the host worktree's changed and untracked
# files (against HEAD) into the container worktree with fresh mtimes, as the container user.
set -euo pipefail
H=/Users/andrew/repos/embed-pdf-viewer/.claude/worktrees/catchup-1127b-platforms C=embed-pdf-catchup1127b D=${D:-/workspaces/wt/CU-C7}
cd "$H"
files=$( { git diff --name-only HEAD; git ls-files --others --exclude-standard; } | sort -u)
present=(); gone=()
for f in $files; do if [[ -e "$f" ]]; then present+=("$f"); else gone+=("$f"); fi; done
if ((${#present[@]})); then COPYFILE_DISABLE=1 tar --no-xattrs -cf - "${present[@]}" | docker exec -i -u node $C tar -xmf - -C $D; fi
for f in "${gone[@]:-}"; do [[ -n "$f" ]] && docker exec -u node $C rm -f "$D/$f"; done
echo "synced ${#present[@]} files, removed ${#gone[@]}"
