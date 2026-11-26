#!/usr/bin/env bash
# Fails when the EmbedPDF facade pins a different EmbedPDF.Runtime than the
# tree would build, so an engine change can't merge with a stale pin.
#
# Usage: check-dotnet-engine.sh [--props <path>]
#   --props  the file holding EmbedPdfRuntimeVersion
#            (default: platforms/dotnet/EmbedPDF.Runtime/EmbedPDF.Runtime.props)
#
# EMBEDPDF_RUNTIME_REPO names the owner/repo whose release workflow pushes the
# runtime. A runtime is published when EmbedPDF.Runtime 0.0.0-engine.<hash> is
# on that owner's GitHub Packages NuGet feed, which the release train's
# dotnet-viewer-release job pushes to; the check reads the owner's package
# versions with `gh api`, so its token needs read:packages.
#
# Exit 0: the pin is current. Prints `current 0.0.0-engine.<hash>`.
# Exit 1: the pin is stale and the tree's runtime isn't on the feed yet.
# Exit 2: the pin is stale but the tree's runtime is on the feed; only the pin needs
#         committing.
#
# Any other code is a hard error, not a verdict:
#   64  bad arguments, or the props file is missing or has no pin
#   69  gh failed for a reason other than "package not found"
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
# The owner's package versions; an organization and a user answer on
# different paths. Only the API's "Package not found" means unpublished; any
# other failure must not look like it.
owner="${RUNTIME_REPO%%/*}"
if ! owner_type="$(gh api "users/$owner" --jq .type 2>&1)"; then
  echo "gh api users/$owner failed: $owner_type" >&2
  exit 69
fi
case "$owner_type" in
  Organization) base="orgs/$owner" ;;
  *) base="users/$owner" ;;
esac
err="$(mktemp)"
trap 'rm -f "$err"' EXIT
if versions="$(gh api --paginate "$base/packages/nuget/EmbedPDF.Runtime/versions" --jq '.[].name' 2>"$err")"; then
  if grep -qxF "$current" <<<"$versions"; then
    echo "commit the bump: set EmbedPdfRuntimeVersion to $current in $PROPS"
    exit 2
  fi
elif ! grep -qi "package not found" "$err" <<<"$versions"; then
  echo "gh api $base/packages/nuget/EmbedPDF.Runtime/versions failed: $(cat "$err")" >&2
  exit 69
fi
echo "publish it: build and pack EmbedPDF.Runtime $current and push it to $owner's NuGet feed (the release train's dotnet-viewer-release job)"
exit 1
