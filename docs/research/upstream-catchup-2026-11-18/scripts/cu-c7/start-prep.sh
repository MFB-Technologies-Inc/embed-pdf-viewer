#!/usr/bin/env bash
# usage: start-prep.sh <checkout> <wasm32-eh pin file>
# A fresh checkout's prep, as ../cu-c3/CU-C3-prep.sh did it at cf7fdbe7a, with the wasm32-eh PDFium
# that checkout pins: deps, the linux-arm64 PDFium, the wasm32 payload without the Rust
# core (wasm.sh replaces it), and every workspace package built.
set -uo pipefail
W=$1; PIN=$2
cd $W || exit 2
export NPM_TOKEN="" CI=true PATH="/workspaces/bin:$PATH"
export CARGO_TARGET_DIR=/workspaces/target CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
step() { echo "=== $(date +%T) $*"; "$@" || { echo "FAILED: $*"; exit 1; }; }
step pnpm install --frozen-lockfile
step bash packages/engine/runtime/scripts/fetch-libpdfium.sh linux-arm64
PDF_RUNTIME_BUILD_FILE=$PIN step bash packages/engine/runtime/scripts/fetch-libpdfium.sh wasm32-eh
EMBEDPDF_RUST_CORE=0 step pnpm --filter @embedpdf/engine-runtime build:target wasm32
step pnpm exec turbo run build --filter='./packages/**' --filter='./tooling/**' --filter='!@mfb/embedpdf-rust-viewer' --concurrency=6
step cargo fetch --locked
echo "PREP DONE"
