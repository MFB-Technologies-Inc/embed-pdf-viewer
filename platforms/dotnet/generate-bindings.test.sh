#!/usr/bin/env bash
# Tests for generate-bindings.sh and build-viewer-host.sh. Run from anywhere:
#   bash platforms/dotnet/generate-bindings.test.sh
# The generation case needs a built host (target/release/libepdf_viewer_host.so)
# and is skipped without one.
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
failures=0
pass() { echo "PASS: $1"; }
fail() { echo "FAIL: $1"; failures=$((failures + 1)); }

expect_exit() {
  local name=$1 want=$2; shift 2
  "$@" >"$TMP/out" 2>&1
  local got=$?
  if [[ $got == "$want" ]]; then pass "$name"; else fail "$name (exit $got, want $want)"; cat "$TMP/out"; fi
}

expect_exit "generate: unknown flag exits 64" 64 bash "$HERE/generate-bindings.sh" --bogus
expect_exit "generate: missing library exits 66" 66 bash "$HERE/generate-bindings.sh" --library /nonexistent/libx.so --out-dir "$TMP/none"
grep -q "/nonexistent/libx.so" "$TMP/out" && pass "generate: missing library is named" || fail "generate: missing library is named"

expect_exit "build: unknown RID exits 64" 64 bash "$HERE/build-viewer-host.sh" bogus
for rid in linux-x64 linux-arm64 osx-arm64 osx-x64 win-x64; do
  grep -q "$rid" "$TMP/out" && pass "build: usage lists $rid" || fail "build: usage lists $rid"
done
expect_exit "build: no RID exits 64" 64 bash "$HERE/build-viewer-host.sh"

library="$ROOT/target/release/libepdf_viewer_host.so"
if [[ -f "$library" ]]; then
  out="$TMP/generated"
  mkdir -p "$out"
  touch "$out/stale.cs"
  expect_exit "generate: runs" 0 bash "$HERE/generate-bindings.sh" --library "$library" --out-dir "$out"
  count=$(find "$out" -name '*.cs' | wc -l)
  [[ $count == 25 ]] && pass "generate: 25 files" || fail "generate: 25 files (got $count)"
  [[ ! -e "$out/stale.cs" ]] && pass "generate: stale output removed" || fail "generate: stale output removed"
  if grep -q "public class ViewerHost\b\|public partial class ViewerHost\b" "$out/epdf_viewer_host.cs"; then
    fail "generate: ViewerHost is internal"
  else
    grep -q "internal.*class ViewerHost\b" "$out/epdf_viewer_host.cs" && pass "generate: ViewerHost is internal" || fail "generate: ViewerHost is internal"
  fi
  grep -q "DestroyViewer" "$out/epdf_viewer_host.cs" && pass "generate: renames applied" || fail "generate: renames applied"
else
  echo "SKIP: generation case, no $library"
fi

echo "failures: $failures"
((failures == 0))
