#!/usr/bin/env bash
# CU-C1 runtime verification: ancestry, fork delta, Android branch, export diff.
set -euo pipefail
cd /workspaces/runtime
T=$(mktemp -d); trap 'rm -rf "$T"' EXIT
M=$(git rev-parse HEAD); F=17d8073d49bab5c29d956568345652bf893b459c; U=f2ea7597287f26bdcb450602de6616ff6939d506; B=aa59ce554aab55879b728dbb86714e13e5cb327f
echo "merge $M parents $(git log -1 --format=%P $M)"
git merge-base --is-ancestor $F $M && echo "fork tip is ancestor"
git merge-base --is-ancestor $U $M && echo "upstream is ancestor"
echo "upstream commits $B..$U: $(git rev-list --count $B..$U) ($(git rev-list --count --no-merges $B..$U) non-merge)"
echo "--- fork delta before ($B..$F) and after ($U..$M)"
git diff --stat $B $F | tail -1
git diff --stat $U $M | tail -1
norm() { grep -vE '^(index |@@)'; }
diff <(git diff $B $F | norm) <(git diff $U $M | norm) > "$T/delta.diff" && echo "fork delta identical (index/@@ lines dropped)" || { echo "fork delta DIFFERS"; head -50 "$T/delta.diff"; }
echo "--- files in fork delta"; git diff --name-only $U $M
echo "--- epdf_secure_random.cpp platform branches"
for r in $B $U $M; do echo "$r:"; git show $r:core/fxcrt/epdf_secure_random.cpp | grep -nE '^#(if|elif|else|error)' ; done
echo "--- upstream diff of epdf_secure_random.cpp $B..$U"; git diff --stat $B $U -- core/fxcrt/epdf_secure_random.cpp
echo "--- export diff (EPDF*/FPDF* declared in public/*.h)"
ex() { git grep -hoE '\b(EPDF|FPDF)[A-Za-z0-9_]*[[:space:]]*\(' $1 -- 'public/*.h' | sed -E 's/[[:space:]]*\($//' | sort -u; }
for r in $B $F $U $M; do ex $r > "$T/ex.$r"; done
for r in $B $F $U $M; do echo "$r EPDF=$(grep -c '^EPDF' "$T/ex.$r") FPDF=$(grep -c '^FPDF' "$T/ex.$r")"; done
echo "added $B->$U:"; comm -13 "$T/ex.$B" "$T/ex.$U"
echo "removed $B->$U:"; comm -23 "$T/ex.$B" "$T/ex.$U"
echo "added $F->$M:"; comm -13 "$T/ex.$F" "$T/ex.$M"
echo "removed $F->$M:"; comm -23 "$T/ex.$F" "$T/ex.$M"
for s in $(comm -13 "$T/ex.$B" "$T/ex.$U"); do echo "$s: $(git grep -l "$s" $U -- 'public/*.h' | sed "s/^$U://")"; done
