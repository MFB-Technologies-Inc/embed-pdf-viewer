#!/usr/bin/env bash
# CU-C9 (2026-11-27): builds the four Apple fork slices for the checkout's
# runtime-src pin without Node. It runs the steps of
# perf-compare/scripts/build-arm.mjs `buildFork` (FORK_RECIPE 4) for the native
# targets, and writes the same <target>.json stamps, so a later buildFork call
# (publish-engine-payload.mjs) finds them and skips the build.
#
#   slices.sh <checkout> [seed fork root]
#
# PERF_WORK_DIR defaults to <main checkout>/perf-compare/.work. When the fork
# tree for the pin does not exist yet and a seed root is given (an earlier
# buildFork tree, for example fork/af480d0ce.../), the seed is cloned with
# `cp -cR` (APFS clones) and the pin checked out in it. The first target then
# runs with PDF_RUNTIME_SYNC=auto, which resyncs gclient deps because the DEPS
# hash differs from the seed's stamp; later targets find the new stamp.
set -euo pipefail
CHECKOUT="$(cd "$1" && pwd)"
SEED="${2:-}"
RUNTIME_SRC=packages/engine/runtime/runtime-src
WORK="${PERF_WORK_DIR:-/Users/andrew/repos/embed-pdf-viewer/perf-compare/.work}"
SHA="$(git -C "$CHECKOUT" ls-tree HEAD "$RUNTIME_SRC" | awk '{print $3}')"
ROOT="$WORK/fork/$SHA"
SRC="$ROOT/runtime-src"
DEPOT_TOOLS="$WORK/depot_tools"
TARGET_OS_LIST="emscripten ios mac"

if [[ ! -d "$SRC/.git" ]]; then
  [[ -n "$SEED" ]] || { echo "no fork tree at $ROOT and no seed given" >&2; exit 1; }
  mkdir -p "$ROOT"
  for entry in .cipd .gclient .gclient_entries .gclient_previous_custom_vars \
    .gclient_previous_sync_commits .gcs_entries runtime-src; do
    [[ -e "$SEED/$entry" ]] && cp -cR "$SEED/$entry" "$ROOT/$entry"
  done
  rm -rf "$SRC/out"
  git -C "$SRC" fetch --quiet "$CHECKOUT/$RUNTIME_SRC" "$SHA"
  git -C "$SRC" checkout --quiet --detach "$SHA"
fi
[[ "$(git -C "$SRC" rev-parse HEAD)" == "$SHA" ]] || { echo "fork tree is not at $SHA" >&2; exit 1; }
[[ -f "$DEPOT_TOOLS/python3_bin_reldir.txt" ]] || { echo "depot_tools at $DEPOT_TOOLS is not bootstrapped" >&2; exit 1; }

for target in ios-arm64 ios-sim-arm64 darwin-static-arm64 darwin-static-x64; do
  started=$(date +%s)
  echo "== building fork ${SHA:0:9} for $target"
  rm -rf "$SRC/out/embedpdf-runtime/$target"
  (
    cd "$SRC"
    PATH="$DEPOT_TOOLS:$PATH" DEPOT_TOOLS_UPDATE=0 PDF_RUNTIME_TARGET_OS_LIST="$TARGET_OS_LIST" \
      PDF_RUNTIME_SYNC=auto PDF_RUNTIME_ARTIFACT_DIR="$ROOT/artifacts" \
      bash -c "scripts/embedpdf-runtime/build-target.sh $target && scripts/embedpdf-runtime/package-target.sh $target"
  )
  staging="$SRC/out/embedpdf-runtime-staging/$target"
  lib_sha="$(shasum -a 256 "$staging/lib/libembedpdf.a" | awk '{print $1}')"
  meta_sha="$(sed -n 's/.*"sha": *"\([0-9a-f]*\)".*/\1/p' "$staging/BUILD-METADATA.json" | head -1)"
  [[ "$meta_sha" == "$SHA" ]] || { echo "$target BUILD-METADATA names $meta_sha" >&2; exit 1; }
  cat > "$ROOT/$target.json" <<JSON
{
  "recipe": 4,
  "sha": "$SHA",
  "target": "$target",
  "seeded": false,
  "staging": "$staging",
  "libSha256": "$lib_sha"
}
JSON
  printf '%s %s %s %ss\n' "$target" "$SHA" "$lib_sha" "$(( $(date +%s) - started ))" | tee -a "$ROOT/cu-c9-slices.txt"
done
