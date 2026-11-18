#!/usr/bin/env bash
# usage: prep.sh <checkout> <payload-source-checkout>
# Copies a prepared runtime payload (PDFium libraries, the wasm32 payload without the Rust core,
# the linux-arm64 addon) from another checkout, then installs deps and builds the TS packages.
set -uo pipefail
W=$1; P=$2/packages/engine/runtime
cd $W || exit 2
export NPM_TOKEN="" CI=true CARGO_TARGET_DIR=/workspaces/target CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
export PATH="/workspaces/bin:$PATH"
step() { echo "=== $(date +%T) $*"; "$@" || { echo "FAILED: $*"; exit 1; }; }
R=packages/engine/runtime
rm -rf $R/build $R/npm/wasm32/lib $R/npm/linux-arm64/lib
step cp -a $P/build $R/build
step cp -a $P/npm/wasm32/lib $R/npm/wasm32/lib
step cp -a $P/npm/linux-arm64/lib $R/npm/linux-arm64/lib
step pnpm install --frozen-lockfile
step pnpm exec turbo run build --filter="./packages/**" --filter="./tooling/**" --filter="!@mfb/embedpdf-rust-viewer" --concurrency=6
pnpm --filter @embedpdf/example-rust-kernel-eval bootstrap || echo "kernel-eval bootstrap failed (non-fatal)"
step cargo fetch --locked
echo "PREP DONE"
