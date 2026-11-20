#!/usr/bin/env bash
# Renders the existing-app guide (platforms/dotnet/EmbedPDF-CSharp-Existing-App.md.in)
# for one .NET engine release. The build facts come from guide-facts.json,
# which release-dotnet.yml writes next to the package; the repository, tag and
# commit come from the release that publishes it.
#
# Usage: render-guide.sh <template> <facts.json> <repo> <tag> <commit> <out>
#
# facts.json: {"nupkgSha256": "<64 hex>", "nupkgBytes": <int>,
#              "runtimeTag": "runtime-<sha>", "glibc": "2.41",
#              "renderSize": "918x1188"}
#
# Every value is checked against a strict pattern before substitution, and the
# render fails if any @NAME@ placeholder is left.
set -euo pipefail

if [[ $# -ne 6 ]]; then
  echo "usage: render-guide.sh <template> <facts.json> <repo> <tag> <commit> <out>" >&2
  exit 2
fi
template=$1 facts=$2 repo=$3 tag=$4 commit=$5 out=$6

fail() { echo "render-guide.sh: $*" >&2; exit 1; }

fact() {
  jq -er --arg key "$1" '.[$key] | select(. != null) | tostring' "$facts" \
    || fail "$facts has no $1"
}

check() {
  [[ $2 =~ $3 ]] || fail "$1 '$2' does not match $3"
}

# 9384244 -> 9,384,244
group_thousands() {
  local digits=$1 grouped=""
  while (( ${#digits} > 3 )); do
    grouped=",${digits: -3}$grouped"
    digits=${digits:0:${#digits}-3}
  done
  printf '%s%s' "$digits" "$grouped"
}

sha=$(fact nupkgSha256)
bytes=$(fact nupkgBytes)
runtime_tag=$(fact runtimeTag)
glibc=$(fact glibc)
render_size=$(fact renderSize)

check repo "$repo" '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$'
check tag "$tag" '^[A-Za-z0-9_.-]+$'
check commit "$commit" '^[0-9a-f]{40}$'
check nupkgSha256 "$sha" '^[0-9a-f]{64}$'
check nupkgBytes "$bytes" '^[1-9][0-9]*$'
check runtimeTag "$runtime_tag" '^runtime-[0-9a-f]+$'
check glibc "$glibc" '^[0-9]+\.[0-9]+$'
check renderSize "$render_size" '^[1-9][0-9]*x[1-9][0-9]*$'

text=$(<"$template")
text=${text//@REPO@/$repo}
text=${text//@TAG@/$tag}
text=${text//@COMMIT@/$commit}
text=${text//@NUPKG_SHA256@/$sha}
text=${text//@NUPKG_SIZE@/$(group_thousands "$bytes")}
text=${text//@RUNTIME_TAG@/$runtime_tag}
text=${text//@GLIBC@/$glibc}
text=${text//@RENDER_SIZE@/$render_size}

if left=$(grep -o '@[A-Z0-9_]*@' <<<"$text" | sort -u | tr '\n' ' ') && [[ -n "$left" ]]; then
  fail "unreplaced placeholders in $template: $left"
fi

mkdir -p "$(dirname "$out")"
printf '%s\n' "$text" > "$out"
