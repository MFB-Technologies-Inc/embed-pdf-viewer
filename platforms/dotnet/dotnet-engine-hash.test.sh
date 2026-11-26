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
for rel in LICENSE packages/engine/runtime/engine-runtime-build.json .gitattributes \
           platforms/dotnet/packaging/package.py platforms/dotnet/EmbedPDF.Runtime/EmbedPDF.Runtime.csproj \
           platforms/dotnet/build-viewer-host.sh crates/epdf-viewer-host/src/lib.rs; do
  cp "$ROOT/$rel" "$TMP/backup"
  printf '\n' >> "$ROOT/$rel"
  edited="$(bash "$HERE/dotnet-engine-hash.sh")"
  cp "$TMP/backup" "$ROOT/$rel"
  [[ "$edited" != "$short" ]] && pass "hash moves with $rel" || fail "hash moves with $rel"
done
[[ "$(bash "$HERE/dotnet-engine-hash.sh")" == "$short" ]] && pass "inputs restored" || fail "inputs restored"

# Text files stage-runtime ships must reach every slice byte for byte, so
# no checkout may convert their line endings (a Windows checkout with
# core.autocrlf=true would).
for rel in LICENSE packages/engine/runtime/engine-runtime-build.json; do
  attr="$(git -C "$ROOT" check-attr text -- "$rel" | awk -F': ' '{print $3}')"
  [[ "$attr" == unset ]] && pass "$rel is -text" || fail "$rel is -text (text: $attr)"
done

props="$TMP/EmbedPDF.Runtime.props"
printf '<Project><PropertyGroup><EmbedPdfRuntimeVersion>0.0.0-engine.%s</EmbedPdfRuntimeVersion></PropertyGroup></Project>\n' "$short" > "$props"
out="$(bash "$HERE/check-dotnet-engine.sh" --props "$props" 2>&1)"
code=$?
[[ $code == 0 && "$out" == "current 0.0.0-engine.$short" ]] && pass "current pin exits 0" || fail "current pin exits 0 ($code: $out)"
# A stale pin asks the package feed whether the tree's runtime is out, through
# a stand-in gh: FAKE_GH=listed lists it, unlisted lists another version,
# missing answers the API's 404 for a package never pushed, broken fails.
fake="$TMP/fake"
mkdir -p "$fake"
cat >"$fake/gh" <<FAKE
#!/usr/bin/env bash
echo "gh \$*" >>"$TMP/gh-calls"
case "\$*" in
  "api users/"*) echo Organization; exit 0 ;;
esac
case "\$FAKE_GH" in
  listed) printf '0.0.0-engine.0000000000000000\n0.0.0-engine.$short\n' ;;
  unlisted) echo 0.0.0-engine.0000000000000000 ;;
  missing) echo '{"message":"Package not found.","status":"404"}'; echo "gh: Package not found. (HTTP 404)" >&2; exit 1 ;;
  *) echo "gh: Bad credentials (HTTP 401)" >&2; exit 1 ;;
esac
FAKE
chmod +x "$fake/gh"
stale() {
  printf '<Project><PropertyGroup><EmbedPdfRuntimeVersion>0.0.0-engine.ffffffffffffffff</EmbedPdfRuntimeVersion></PropertyGroup></Project>\n' > "$props"
  out="$(FAKE_GH=$1 EMBEDPDF_RUNTIME_REPO=owner/repo PATH="$fake:$PATH" bash "$HERE/check-dotnet-engine.sh" --props "$props" 2>&1)"
}
stale listed; code=$?
[[ $code == 2 && "$out" == *"commit the bump"* ]] && pass "stale pin with the runtime on the feed exits 2" || fail "stale pin with the runtime on the feed exits 2 ($code: $out)"
grep -q "api --paginate orgs/owner/packages/nuget/EmbedPDF.Runtime/versions" "$TMP/gh-calls" && pass "asks the owner's NuGet feed" || { fail "asks the owner's NuGet feed"; cat "$TMP/gh-calls"; }
stale unlisted; code=$?
[[ $code == 1 && "$out" == *"publish it"* ]] && pass "stale pin with another version on the feed exits 1" || fail "stale pin with another version on the feed exits 1 ($code: $out)"
stale missing; code=$?
[[ $code == 1 ]] && pass "stale pin with no package on the feed exits 1" || fail "stale pin with no package on the feed exits 1 ($code: $out)"
stale broken; code=$?
[[ $code == 69 ]] && pass "a gh failure exits 69" || fail "a gh failure exits 69 ($code: $out)"
echo "<Project/>" > "$props"
bash "$HERE/check-dotnet-engine.sh" --props "$props" >/dev/null 2>&1
[[ $? == 64 ]] && pass "props without a pin exits 64" || fail "props without a pin exits 64"
bash "$HERE/check-dotnet-engine.sh" --props "$TMP/missing.props" >/dev/null 2>&1
[[ $? == 64 ]] && pass "missing props exits 64" || fail "missing props exits 64"
bash "$HERE/check-dotnet-engine.sh" --bogus >/dev/null 2>&1
[[ $? == 64 ]] && pass "bad flag exits 64" || fail "bad flag exits 64"

echo "failures: $failures"
((failures == 0))
