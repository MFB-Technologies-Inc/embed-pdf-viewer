#!/usr/bin/env bash
# Fails when the Android build pins a different prebuilt engine than the tree
# would build, so an engine change can't merge with a stale pin.
#
# Usage: check-android-engine.sh [--properties <path>]
#   --properties  the gradle.properties to read the pin from
#                 (default: platforms/android/embedpdf/gradle.properties)
#
# EMBEDPDF_RUNTIME_REPO names the owner/repo whose releases it asks about
# (default: the viewer fork, as RUNTIME_REPO below spells it).
#
# Exit 0: the pin is current. Prints `current android-engine-<hash>`.
# Exit 1: the pin is stale and the tree's release isn't published yet.
# Exit 2: the pin is stale but the tree's release exists; only the pin needs
#         committing.
#
# Any other code is a hard error, not a verdict:
#   64  bad arguments, or the file is missing or has no
#       embedpdf.engineRelease line
#   69  gh failed for a reason other than "release not found" (not installed,
#       not authenticated, network, rate limit); the gh error is printed
#   70  the tree's engine hash couldn't be computed
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RUNTIME_REPO="${EMBEDPDF_RUNTIME_REPO:-MFB-Technologies-Inc/embed-pdf-viewer}"
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
# Only gh's "release not found" means unpublished; any other failure must not
# look like it, or a publish would rebuild and upload over an existing tag.
if gh_err="$(gh release view "$current" --repo "$RUNTIME_REPO" 2>&1 >/dev/null)"; then
  echo "commit the bump: set embedpdf.engineRelease=$current in $PROPERTIES"
  exit 2
elif [[ "$gh_err" == *"release not found"* ]]; then
  echo "publish it: no Android publish workflow exists yet"
  exit 1
fi
echo "gh release view failed: $gh_err" >&2
exit 69
