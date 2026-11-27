#!/usr/bin/env bash
# CU-C6 (2026-11-27), host side (git and tar only, no node or cargo): see decisions/CU-C6.md D1.
# usage: sync.sh <file...> : copy host edit-worktree files into the container CU-C6 worktree
# (as the container user, with fresh mtimes so cargo rebuilds); a missing file is removed there.
cd /Users/andrew/repos/embed-pdf-viewer/.claude/worktrees/c6-edit || exit 2
present=(); for f in "$@"; do if [ -e "$f" ]; then present+=("$f"); else docker exec embed-pdf-catchup1127 rm -f "/workspaces/wt/CU-C6/$f"; echo "removed $f"; fi; done
[ ${#present[@]} -gt 0 ] && COPYFILE_DISABLE=1 tar --no-xattrs --no-mac-metadata -cf - "${present[@]}" | docker exec -i embed-pdf-catchup1127 tar -C /workspaces/wt/CU-C6 -xmf - && printf 'synced %s\n' "${present[@]}"
true
