#!/usr/bin/env bash
# Generates the C# bindings for epdf-viewer-host into the EmbedPDF facade
# project. The output is a build product: git-ignored, never edited by hand.
#
# Usage: generate-bindings.sh [--library <path>] [--out-dir <dir>]
#   --library  the built viewer host (default: target/release/ with the
#              platform's file name: libepdf_viewer_host.so, .dylib, or
#              epdf_viewer_host.dll)
#   --out-dir  where the .cs files go (default:
#              platforms/dotnet/EmbedPDF.Viewer/Generated); emptied first
#
# The generator reads the uniffi metadata baked into the library, so the
# library has to come from the same tree. Build it first with
# `cargo rustc -p epdf-viewer-host --release --locked --crate-type cdylib`.
# The metadata lives in the symbol table, which build-viewer-host.sh strips
# from the packaged library, so generate from this unstripped build. The
# bindings match the stripped library all the same: the checksums they call
# are exported functions.
#
# Exit 64: bad arguments. Exit 66: the library is missing.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONFIG="$ROOT/platforms/dotnet/EmbedPDF.Viewer/uniffi.toml"
OUT="$ROOT/platforms/dotnet/EmbedPDF.Viewer/Generated"
case "$(uname -s)" in
  Darwin) LIBRARY="$ROOT/target/release/libepdf_viewer_host.dylib" ;;
  MINGW* | MSYS* | CYGWIN*) LIBRARY="$ROOT/target/release/epdf_viewer_host.dll" ;;
  *) LIBRARY="$ROOT/target/release/libepdf_viewer_host.so" ;;
esac

while [[ $# -gt 0 ]]; do
  case "$1" in
    --library) LIBRARY="${2:?--library needs a path}"; shift 2 ;;
    --out-dir) OUT="${2:?--out-dir needs a path}"; shift 2 ;;
    *) echo "usage: $0 [--library <path>] [--out-dir <dir>]" >&2; exit 64 ;;
  esac
done

if [[ ! -f "$LIBRARY" ]]; then
  echo "no viewer host library at $LIBRARY; build it first" >&2
  exit 66
fi

cargo build --locked --quiet -p epdf-uniffi-bindgen-cs --manifest-path "$ROOT/Cargo.toml"
GENERATOR="${CARGO_TARGET_DIR:-$ROOT/target}/debug/uniffi-bindgen-cs"

rm -rf "$OUT"
mkdir -p "$OUT"
# --no-format skips CSharpier: the files are never read by people, and the
# formatter would be one more tool to pin.
"$GENERATOR" --no-format --library "$LIBRARY" --config "$CONFIG" --out-dir "$OUT"

count=$(find "$OUT" -maxdepth 1 -name '*.cs' | wc -l | tr -d ' ')
if ((count == 0)); then
  echo "the generator wrote no C# into $OUT; a stripped library has no uniffi metadata" >&2
  exit 1
fi
echo "generated $count files in $OUT"
