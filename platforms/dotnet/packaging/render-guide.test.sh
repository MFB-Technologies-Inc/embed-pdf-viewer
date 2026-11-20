#!/usr/bin/env bash
# Tests render-guide.sh against the checked-in guide template. Needs bash and jq.
#
# Usage: bash platforms/dotnet/packaging/render-guide.test.sh
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
render=$here/render-guide.sh
template=$here/../EmbedPDF-CSharp-Existing-App.md.in
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

repo=example-org/embed-pdf-viewer
tag=dotnet-engine-0.0.0-local-0123456789ab
commit=0123456789abcdef0123456789abcdef01234567
sha=08a575929ca3d3821b9c8ed9f2949218be6b427e5112686c849f5221bb2447d9

write_facts() { # <path> <bytes>
  cat > "$1" <<JSON
{"nupkgSha256": "$sha", "nupkgBytes": $2,
 "runtimeTag": "runtime-f2ea7597287f26bdcb450602de6616ff6939d506",
 "glibc": "2.41", "renderSize": "918x1188"}
JSON
}

failures=0
pass() { echo "ok   $1"; }
flunk() { echo "FAIL $1"; failures=$((failures + 1)); }
expect_line() { # <file> <fixed string> <name>
  if grep -qF -- "$2" "$1"; then pass "$3"; else flunk "$3"; fi
}
expect_failure() { # <name> <args...>
  local name=$1; shift
  if bash "$render" "$@" > "$work/err.log" 2>&1; then
    flunk "$name (rendered)"
  elif grep -q 'render-guide.sh:\|usage:' "$work/err.log"; then
    pass "$name"
  else
    flunk "$name (unexpected error: $(cat "$work/err.log"))"
  fi
}

write_facts "$work/facts.json" 9384244
bash "$render" "$template" "$work/facts.json" "$repo" "$tag" "$commit" "$work/guide.md"
out=$work/guide.md

if grep -q '@[A-Z0-9_]*@' "$out"; then flunk "no placeholder left"; else pass "no placeholder left"; fi
expect_line "$out" "Expected package SHA-256: \`$sha\` (9,384,244 bytes)." "checksum and grouped size"
expect_line "$out" "https://github.com/$repo/releases/download/$tag/EmbedPDF.Engine.0.0.0-local.nupkg.sha256" "download URL"
expect_line "$out" "(https://github.com/$repo/releases/tag/$tag)" "release link"
expect_line "$out" "[viewer commit $commit](https://github.com/$repo/tree/$commit)" "source link"
expect_line "$out" "upstream's runtime release \`runtime-f2ea7597287f26bdcb450602de6616ff6939d506\`" "runtime tag"
expect_line "$out" "Validation ran with glibc 2.41;" "glibc"
expect_line "$out" "to a 918x1188 image" "render size"
if [[ $(tail -c 1 "$out" | od -An -c | tr -d ' ') == '\n' ]]; then pass "ends with a newline"; else flunk "ends with a newline"; fi

for case in 999:999 1000:1,000 123456:123,456 1234567:1,234,567; do
  write_facts "$work/size.json" "${case%%:*}"
  bash "$render" "$template" "$work/size.json" "$repo" "$tag" "$commit" "$work/size.md"
  expect_line "$work/size.md" "(${case#*:} bytes)" "size ${case%%:*} -> ${case#*:}"
done

jq 'del(.glibc)' "$work/facts.json" > "$work/missing.json"
expect_failure "missing fact" "$template" "$work/missing.json" "$repo" "$tag" "$commit" "$work/x.md"
jq '.nupkgSha256 = "abc"' "$work/facts.json" > "$work/badsha.json"
expect_failure "malformed sha256" "$template" "$work/badsha.json" "$repo" "$tag" "$commit" "$work/x.md"
expect_failure "short commit" "$template" "$work/facts.json" "$repo" "$tag" 0123456 "$work/x.md"
expect_failure "repo with a path" "$template" "$work/facts.json" "a/b/c" "$tag" "$commit" "$work/x.md"
expect_failure "wrong argument count" "$template" "$work/facts.json" "$repo"
{ cat "$template"; echo "@UNKNOWN@"; } > "$work/extra.md.in"
expect_failure "unknown placeholder" "$work/extra.md.in" "$work/facts.json" "$repo" "$tag" "$commit" "$work/x.md"

if (( failures )); then echo "$failures failed"; exit 1; fi
echo "all passed"
