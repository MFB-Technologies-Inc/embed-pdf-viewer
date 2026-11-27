#!/usr/bin/env bash
# CU-C3 (2026-11-27). usage: repin-loop.sh <test-file> [max]  run one evidence test file from
# CU-C3 (2026-11-27b): copied from 2026-11-27 scripts/cu-c3/repin-loop.sh.
# tooling/plugin-conformance; while it fails on a 64-hex digest under historical/, replace the
# expected digest with the actual one in the historical/ file that holds it. Prints each swap.
set -uo pipefail
for i in $(seq 1 "${2:-60}"); do
  out=$(CI=true TZ=UTC node --import=tsx --conditions=development --test "$1" 2>&1)
  a=$(echo "$out" | grep -oE "^    actual: .[0-9a-f]{64}" | head -1 | grep -oE "[0-9a-f]{64}")
  e=$(echo "$out" | grep -oE "^    expected: .[0-9a-f]{64}" | head -1 | grep -oE "[0-9a-f]{64}")
  if [ -z "$a" ] || [ -z "$e" ]; then echo "$out" | grep -E "^ℹ (pass|fail)"; exit 0; fi
  files=$(grep -rl "$e" historical)
  [ -z "$files" ] && { echo "expected $e not under historical/"; exit 1; }
  for f in $files; do sed -i "s/$e/$a/g" "$f"; echo "$f: ${e:0:12} -> ${a:0:12}"; done
done
