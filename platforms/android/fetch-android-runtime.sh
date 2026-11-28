#!/usr/bin/env bash
# Fetches the Android PDFium runtime slices the AAR links into
# packages/engine/runtime/build/libpdfium/android-{arm64,x64}.
#
# The slices cannot be built on macOS (the fork's Android config asserts a
# Linux host), so they come from the runtime release for the commit
# packages/engine/runtime/runtime-src pins. Each archive is checked against
# the SHA-256 in that release's pdf-runtime-build.generated.json before it is
# unpacked; a mismatch fails and leaves nothing behind.
#
#   bash platforms/android/fetch-android-runtime.sh            # both slices
#   bash platforms/android/fetch-android-runtime.sh android-x64
#
# RUNTIME_RELEASE_REPO selects the repository holding the release. Runtime
# releases are published to the viewer repo by .github/workflows/release-runtime.yml.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
REPO="${RUNTIME_RELEASE_REPO:-MFB-Technologies-Inc/embed-pdf-viewer}"
OUT_DIR="${PDF_RUNTIME_LIB_DIR:-$ROOT/packages/engine/runtime/build/libpdfium}"
CACHE="$ROOT/packages/engine/runtime/build/cache"

if (($# == 0)); then
  set -- android-arm64 android-x64
fi

SHA="$(git -C "$ROOT" ls-files -s packages/engine/runtime/runtime-src | awk '{print $2}')"
[[ -n "$SHA" ]] || { echo "packages/engine/runtime/runtime-src is not a pinned submodule" >&2; exit 1; }
BASE="https://github.com/$REPO/releases/download/runtime-$SHA"

mkdir -p "$CACHE"
MANIFEST="$CACHE/pdf-runtime-build-$SHA.json"
if [[ ! -s "$MANIFEST" ]]; then
  curl -fsSL --retry 3 "$BASE/pdf-runtime-build.generated.json" -o "$MANIFEST.part"
  mv "$MANIFEST.part" "$MANIFEST"
fi

for target in "$@"; do
  # The manifest's URLs name the repository that first published the release,
  # so only its file name and checksum are used; the archive comes from $BASE.
  read -r name expected < <(python3 - "$MANIFEST" "$target" "$SHA" <<'PYTHON'
import json, sys
manifest, target, sha = sys.argv[1:]
data = json.load(open(manifest))
if data.get("sha") != sha:
    sys.exit(f"manifest is for runtime {data.get('sha')}, expected {sha}")
artifact = data.get("artifacts", {}).get(target)
if not artifact:
    sys.exit(f"the runtime-{sha} release has no {target} slice")
print(artifact["url"].rsplit("/", 1)[1], artifact["sha256"])
PYTHON
)
  archive="$CACHE/$name"
  if [[ ! -f "$archive" ]] || [[ "$(sha256sum "$archive" | awk '{print $1}')" != "$expected" ]]; then
    echo "downloading $name"
    curl -fsSL --retry 3 "$BASE/$name" -o "$archive.part"
    mv "$archive.part" "$archive"
  fi
  actual="$(sha256sum "$archive" | awk '{print $1}')"
  if [[ "$actual" != "$expected" ]]; then
    rm -f "$archive"
    echo "sha256 mismatch for $name: expected $expected, got $actual" >&2
    exit 1
  fi
  dest="$OUT_DIR/$target"
  rm -rf "$dest"
  mkdir -p "$dest"
  tar -xzf "$archive" -C "$dest"
  echo "$dest"
done
