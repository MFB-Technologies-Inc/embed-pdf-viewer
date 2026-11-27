#!/usr/bin/env bash
# CU-C9 (2026-11-27): copies the Swift test fixtures into a checkout whose
# engine is downloaded (no build-xcframework.sh run): the FIXTURES list and
# test targets read from that checkout's own build-xcframework.sh.
#
#   fixtures.sh <checkout>
set -euo pipefail
ROOT="$(cd "$1" && pwd)"
SCRIPT="$ROOT/platforms/apple/build-xcframework.sh"
OUT="$ROOT/platforms/apple/EmbedPDF"
mapfile_fixtures() { sed -n '/^FIXTURES=(/,/^)/p' "$SCRIPT" | sed '1d;$d' | sed 's/^ *//'; }
targets="$(sed -n 's/^TEST_TARGETS=(\(.*\))$/\1/p' "$SCRIPT")"
for tests in $targets; do
  mkdir -p "$OUT/Tests/$tests/Fixtures"
  while read -r fixture; do
    [[ -n "$fixture" ]] || continue
    # shellcheck disable=SC2086
    cp "$ROOT"/$fixture "$OUT/Tests/$tests/Fixtures/"
  done < <(mapfile_fixtures)
  echo "$tests: $(ls "$OUT/Tests/$tests/Fixtures" | wc -l | tr -d ' ') fixtures"
done
