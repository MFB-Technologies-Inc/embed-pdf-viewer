#!/usr/bin/env bash
# For each release archive: its sha256, the BUILD-METADATA.json sha/target/emsdk,
# and whether the library defines the three EPDFDss_* functions and
# EPDFDoc_GetLoadedObjectValue (read with the Rust toolchain's llvm-nm).
set -uo pipefail
NM=${NM:-$HOME/.rustup/toolchains/1.98.0-aarch64-apple-darwin/lib/rustlib/aarch64-apple-darwin/bin/llvm-nm}
for a in "$@"; do
  d=$(mktemp -d); tar -xzf "$a" -C "$d"
  meta=$(python3 -c 'import json,sys;m=json.load(open(sys.argv[1]));print(m["target"],m["sha"],m.get("emsdkVersion","-"))' "$d/BUILD-METADATA.json")
  lib=$(ls "$d"/lib/libembedpdf.* | head -1)
  syms=$("$NM" -g --defined-only "$lib" 2>/dev/null | grep -o -E '\b_?(EPDFDss_(Add|GetCount|GetData)|EPDFDoc_GetLoadedObjectValue)$' | sed 's/^_//' | sort -u | tr '\n' ' ')
  echo "$(basename "$a") sha256=$(shasum -a 256 "$a" | cut -d' ' -f1) bytes=$(stat -f %z "$a") meta=[$meta] lib=$(basename "$lib") exports=[$syms]"
  rm -rf "$d"
done
