#!/usr/bin/env bash
# CU-C2 (2026-11-27b), CU-C2-D6.
# usage: freetype-src.sh <checkout>...   puts the FreeType source runtime-src/DEPS pins at
# runtime-src/third_party/freetype/src, so packages/core/fonts' standard-fonts test can read
# src/psnames/pstables.h. One shallow clone in /workspaces/freetype-src, copied into each checkout.
set -euo pipefail
SRC=/workspaces/freetype-src
for w in "$@"; do
  DEPS="$w/packages/engine/runtime/runtime-src/DEPS"
  REV=$(sed -nE "s/^ *'freetype_revision': '([0-9a-f]{40})',/\1/p" "$DEPS")
  [[ -n "$REV" ]] || { echo "no freetype_revision in $DEPS"; exit 1; }
  if [[ "$(git -C "$SRC" rev-parse HEAD 2>/dev/null)" != "$REV" ]]; then
    rm -rf "$SRC"; git init -q "$SRC"
    git -C "$SRC" fetch -q --depth 1 https://chromium.googlesource.com/chromium/src/third_party/freetype2.git "$REV"
    git -C "$SRC" checkout -q FETCH_HEAD
  fi
  DEST="$w/packages/engine/runtime/runtime-src/third_party/freetype/src"
  rm -rf "$DEST"; cp -a "$SRC" "$DEST"
  echo "$DEST at $REV"
done
