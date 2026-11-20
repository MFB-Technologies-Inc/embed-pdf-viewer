#!/usr/bin/env bash
# Builds the epdf-viewer-host cdylib for one .NET RID and prints the library's
# absolute path on the last line of stdout.
#
# Usage: build-viewer-host.sh <rid>
#   rid  linux-x64, linux-arm64, osx-arm64, osx-x64 or win-x64
#
# The build links the PDFium that build-support resolves for the target
# (PDF_RUNTIME_LIB_DIR, default packages/engine/runtime/build/libpdfium).
# Fetch it first with packages/engine/runtime/scripts/fetch-libpdfium.sh
# <fork target>. The library this writes still points into the build tree;
# packaging/package.py stage-runtime makes it relocatable.
#
# Exit 64: bad arguments.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
usage() { echo "usage: $0 <linux-x64|linux-arm64|osx-arm64|osx-x64|win-x64>" >&2; exit 64; }
[[ $# == 1 ]] || usage
case "$1" in
  linux-x64) triple=x86_64-unknown-linux-gnu; file=libepdf_viewer_host.so ;;
  linux-arm64) triple=aarch64-unknown-linux-gnu; file=libepdf_viewer_host.so ;;
  osx-arm64) triple=aarch64-apple-darwin; file=libepdf_viewer_host.dylib ;;
  osx-x64) triple=x86_64-apple-darwin; file=libepdf_viewer_host.dylib ;;
  win-x64) triple=x86_64-pc-windows-msvc; file=epdf_viewer_host.dll ;;
  *) usage ;;
esac

# strip=symbols applies to this crate's link only, so it relinks without
# rebuilding the dependencies. The exported uniffi symbols stay.
cargo rustc --manifest-path "$ROOT/Cargo.toml" -p epdf-viewer-host --release --locked \
  --target "$triple" --crate-type cdylib -- -C strip=symbols >&2
library="${CARGO_TARGET_DIR:-$ROOT/target}/$triple/release/$file"
[[ -f "$library" ]] || { echo "cargo finished but $library is missing" >&2; exit 1; }
echo "$library"
