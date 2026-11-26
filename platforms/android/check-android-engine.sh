#!/usr/bin/env bash
# Fails when the Android build pins a different prebuilt engine than the tree
# would build, so an engine change can't merge with a stale pin.
#
# Usage: check-android-engine.sh [--properties <path>]
#   --properties  the gradle.properties to read the pin from
#                 (default: platforms/android/embedpdf/gradle.properties)
#
# An engine is published when GitHub Packages has the POM of
# com.embedpdf:embedpdf-bindings:0.0.0-engine.<hash>, the coordinate
# android-release.yml publishes it under. EMBEDPDF_PACKAGES_REPOSITORY names
# the owner/repo whose registry it asks (default: the workflow's
# packages_repository default, as PACKAGES_REPOSITORY below spells it). The
# registry wants a token even for a public package: GH_TOKEN, else
# GITHUB_TOKEN, else `gh auth token`; it needs read:packages there.
#
# Exit 0: the pin is current. Prints `current android-engine-<hash>`.
# Exit 1: the pin is stale and the tree's engine isn't published yet.
# Exit 2: the pin is stale but the tree's engine is published; only the pin
#         needs committing.
#
# Any other code is a hard error, not a verdict:
#   64  bad arguments, or the file is missing or has no
#       embedpdf.engineRelease line
#   69  the registry couldn't be asked: no token, curl failed, or an answer
#       other than 200 or 404 (a token that can't read the registry); the
#       cause is printed
#   70  the tree's engine hash couldn't be computed
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PACKAGES_REPOSITORY="${EMBEDPDF_PACKAGES_REPOSITORY:-MFB-Technologies-Inc/embed-pdf-runtime}"
PROPERTIES="$REPO_ROOT/platforms/android/embedpdf/gradle.properties"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --properties)
      PROPERTIES="${2:?--properties needs a path}"
      shift 2
      ;;
    *)
      echo "unknown argument: $1" >&2
      exit 64
      ;;
  esac
done

if [[ ! -f "$PROPERTIES" ]]; then
  echo "no properties file at $PROPERTIES" >&2
  exit 64
fi

hash="$(bash "$REPO_ROOT/platforms/android/android-engine-hash.sh")" || {
  echo "couldn't compute the engine hash" >&2
  exit 70
}
current="android-engine-$hash"
pinned="$(sed -n 's/^embedpdf\.engineRelease=\(.*\)$/\1/p' "$PROPERTIES")" || {
  echo "couldn't read $PROPERTIES" >&2
  exit 64
}
if [[ -z "$pinned" ]]; then
  echo "no embedpdf.engineRelease line in $PROPERTIES" >&2
  exit 64
fi

if [[ "$pinned" == "$current" ]]; then
  echo "current $current"
  exit 0
fi

echo "pinned:  $pinned"
echo "current: $current"
# Only a 404 means unpublished; any other failure must not look like it, or a
# publish would rebuild and upload over an existing engine version.
token="${GH_TOKEN:-${GITHUB_TOKEN:-}}"
if [[ -z "$token" ]] && command -v gh >/dev/null 2>&1; then
  token="$(gh auth token 2>/dev/null || true)"
fi
if [[ -z "$token" ]]; then
  echo "no token to ask $PACKAGES_REPOSITORY's registry: set GH_TOKEN or GITHUB_TOKEN, or log in with gh" >&2
  exit 69
fi
version="0.0.0-engine.${current#android-engine-}"
pom="https://maven.pkg.github.com/$PACKAGES_REPOSITORY/com/embedpdf/embedpdf-bindings/$version/embedpdf-bindings-$version.pom"
if ! status="$(curl -sS -o /dev/null -w '%{http_code}' -u "x-access-token:$token" "$pom" 2>&1)"; then
  echo "asking $PACKAGES_REPOSITORY for embedpdf-bindings $version failed: $status" >&2
  exit 69
fi
case "$status" in
  200)
    echo "commit the bump: set embedpdf.engineRelease=$current in $PROPERTIES"
    exit 2
    ;;
  404)
    echo "set embedpdf.engineRelease=$current in $PROPERTIES, commit it, then run .github/workflows/android-release.yml"
    exit 1
    ;;
esac
echo "asking $PACKAGES_REPOSITORY for embedpdf-bindings $version returned HTTP $status" >&2
exit 69
