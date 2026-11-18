#!/usr/bin/env bash
# Every "## CU-Cn-Dn" (or "## CU-final-Dn") heading in decisions/*.md must appear in decisions.md.
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 2
ids=$(grep -hoE "^## CU-(C[0-9]+[a-z]?|final)-D[0-9]+" decisions/*.md | sed "s/^## //" | sort -u)
n=0; miss=0
for id in $ids; do n=$((n+1)); grep -qE "(^|[^0-9A-Za-z-])$id([^0-9]|$)" decisions.md || { echo "MISSING $id"; miss=$((miss+1)); }; done
echo "headings=$n missing=$miss"
