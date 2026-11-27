#!/usr/bin/env bash
# CU-C7 (2026-11-27), from 2026-11-18 cu-c7/prep.sh.
# usage: prep.sh <checkout> <payload-source-checkout>
# Copies the prepared PDFium libraries and the wasm32 payload without the Rust core from another
# checkout, installs deps, builds every workspace package, then the linux-arm64 addon
# (../prep-extra.sh; Playwright's browsers are already in the container).
set -uo pipefail
W=$1; P=$2/packages/engine/runtime
cd "$W" || exit 2
export NPM_TOKEN="" CI=true CARGO_TARGET_DIR=${CARGO_TARGET_DIR:-/workspaces/target-c5} CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
export PATH="/workspaces/bin:$PATH"
step() { echo "=== $(date +%T) $*"; "$@" || { echo "FAILED: $*"; exit 1; }; }
R=packages/engine/runtime
rm -rf $R/build $R/npm/wasm32/lib
step cp -a $P/build $R/build
step cp -a $P/npm/wasm32/lib $R/npm/wasm32/lib
step pnpm install --frozen-lockfile
step pnpm exec turbo run build --filter="./packages/**" --filter="./tooling/**" --filter="!@mfb/embedpdf-rust-viewer" --concurrency=6
pnpm --filter @embedpdf/example-rust-kernel-eval bootstrap || echo "kernel-eval bootstrap failed (non-fatal)"
step bash /workspaces/scripts/prep-extra.sh "$W"
step cargo fetch --locked
echo "PREP DONE"
