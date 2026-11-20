#!/usr/bin/env bash
# Fails when the EmbedPDF facade pins a different EmbedPDF.Runtime than the
# tree would build, so an engine change can't merge with a stale pin.
#
# Usage: check-dotnet-engine.sh [--props <path>]
#   --props  the file holding EmbedPdfRuntimeVersion
#            (default: platforms/dotnet/EmbedPDF.Runtime/EmbedPDF.Runtime.props)
#
# EMBEDPDF_RUNTIME_REPO names the owner/repo whose releases it asks about
# (default: MFB-Technologies-Inc/embed-pdf-viewer). A runtime is published as
# the release dotnet-engine-<hash>.
#
# Exit 0: the pin is current. Prints `current 0.0.0-engine.<hash>`.
# Exit 1: the pin is stale and the tree's release isn't published yet.
# Exit 2: the pin is stale but the tree's release exists; only the pin needs
#         committing.
#
# Any other code is a hard error, not a verdict:
#   64  bad arguments, or the props file is missing or has no pin
#   69  gh failed for a reason other than "release not found"
#   70  the tree's engine hash couldn't be computed
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RUNTIME_REPO="${EMBEDPDF_RUNTIME_REPO:-MFB-Technologies-Inc/embed-pdf-viewer}"
PROPS="$REPO_ROOT/platforms/dotnet/EmbedPDF.Runtime/EmbedPDF.Runtime.props"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --props)
      PROPS="${2:?--props needs a path}"
      shift 2
      ;;
    *)
      echo "unknown argument: $1" >&2
      exit 64
      ;;
  esac
done

if [[ ! -f "$PROPS" ]]; then
  echo "no props file at $PROPS" >&2
  exit 64
fi

hash="$(bash "$REPO_ROOT/platforms/dotnet/dotnet-engine-hash.sh")" || {
  echo "couldn't compute the engine hash" >&2
  exit 70
}
current="0.0.0-engine.$hash"
pinned="$(sed -n 's|.*<EmbedPdfRuntimeVersion>\(.*\)</EmbedPdfRuntimeVersion>.*|\1|p' "$PROPS")"
if [[ -z "$pinned" ]]; then
  echo "no EmbedPdfRuntimeVersion in $PROPS" >&2
  exit 64
fi

if [[ "$pinned" == "$current" ]]; then
  echo "current $current"
  exit 0
fi

echo "pinned:  $pinned"
echo "current: $current"
release="dotnet-engine-$hash"
# Only gh's "release not found" means unpublished; any other failure must not
# look like it.
if gh_err="$(gh release view "$release" --repo "$RUNTIME_REPO" 2>&1 >/dev/null)"; then
  echo "commit the bump: set EmbedPdfRuntimeVersion to $current in $PROPS"
  exit 2
elif [[ "$gh_err" == *"release not found"* ]]; then
  echo "publish it: build and pack EmbedPDF.Runtime $current, release $release"
  exit 1
fi
echo "gh release view failed: $gh_err" >&2
exit 69
