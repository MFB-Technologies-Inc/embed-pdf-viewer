#!/usr/bin/env bash
# Tests for dotnet-engine-hash.sh and check-dotnet-engine.sh:
#   bash platforms/dotnet/dotnet-engine-hash.test.sh
# Needs cargo (offline) for the crate-list check.
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
failures=0
pass() { echo "PASS: $1"; }
fail() { echo "FAIL: $1"; failures=$((failures + 1)); }

short="$(bash "$HERE/dotnet-engine-hash.sh")"
[[ "$short" =~ ^[0-9a-f]{16}$ ]] && pass "short hash is 16 hex" || fail "short hash is 16 hex (got '$short')"
full="$(bash "$HERE/dotnet-engine-hash.sh" --full)"
[[ "$full" =~ ^[0-9a-f]{64}$ ]] && pass "full hash is 64 hex" || fail "full hash is 64 hex"
[[ "$full" == "$short"* ]] && pass "short hash prefixes full" || fail "short hash prefixes full"
[[ "$(bash "$HERE/dotnet-engine-hash.sh")" == "$short" ]] && pass "hash is stable" || fail "hash is stable"
bash "$HERE/dotnet-engine-hash.sh" --bogus >/dev/null 2>&1
[[ $? == 2 ]] && pass "bad flag exits 2" || fail "bad flag exits 2"

# Every workspace crate the host links, normal and build edges, any target.
linked="$(cd "$ROOT" && cargo tree --locked --offline -p epdf-viewer-host -e normal,build --target all \
  --prefix none -f '{p}' 2>/dev/null | grep -o "($ROOT/crates/[^)]*)" | sed "s|($ROOT/crates/||; s|)||" | LC_ALL=C sort -u)"
listed="$(sed -n 's/^DOTNET_CRATES=(\(.*\))$/\1/p' "$HERE/dotnet-engine-hash.sh" | tr ' ' '\n' | LC_ALL=C sort -u)"
if [[ -n "$linked" && "$linked" == "$listed" ]]; then
  pass "DOTNET_CRATES matches cargo tree"
else
  fail "DOTNET_CRATES matches cargo tree"
  diff <(echo "$linked") <(echo "$listed")
fi

# Every file stage-runtime and pack-runtime put into the package must move the
# hash. Each input is edited in place, rehashed and restored from a copy.
for rel in LICENSE packages/engine/runtime/engine-runtime-build.json \
           platforms/dotnet/packaging/package.py platforms/dotnet/EmbedPDF.Runtime/EmbedPDF.Runtime.csproj \
           platforms/dotnet/build-viewer-host.sh crates/epdf-viewer-host/src/lib.rs; do
  cp "$ROOT/$rel" "$TMP/backup"
  printf '\n' >> "$ROOT/$rel"
  edited="$(bash "$HERE/dotnet-engine-hash.sh")"
  cp "$TMP/backup" "$ROOT/$rel"
  [[ "$edited" != "$short" ]] && pass "hash moves with $rel" || fail "hash moves with $rel"
done
[[ "$(bash "$HERE/dotnet-engine-hash.sh")" == "$short" ]] && pass "inputs restored" || fail "inputs restored"

props="$TMP/EmbedPDF.Runtime.props"
printf '<Project><PropertyGroup><EmbedPdfRuntimeVersion>0.0.0-engine.%s</EmbedPdfRuntimeVersion></PropertyGroup></Project>\n' "$short" > "$props"
out="$(bash "$HERE/check-dotnet-engine.sh" --props "$props" 2>&1)"
code=$?
[[ $code == 0 && "$out" == "current 0.0.0-engine.$short" ]] && pass "current pin exits 0" || fail "current pin exits 0 ($code: $out)"
echo "<Project/>" > "$props"
bash "$HERE/check-dotnet-engine.sh" --props "$props" >/dev/null 2>&1
[[ $? == 64 ]] && pass "props without a pin exits 64" || fail "props without a pin exits 64"
bash "$HERE/check-dotnet-engine.sh" --props "$TMP/missing.props" >/dev/null 2>&1
[[ $? == 64 ]] && pass "missing props exits 64" || fail "missing props exits 64"
bash "$HERE/check-dotnet-engine.sh" --bogus >/dev/null 2>&1
[[ $? == 64 ]] && pass "bad flag exits 64" || fail "bad flag exits 64"

echo "failures: $failures"
((failures == 0))
