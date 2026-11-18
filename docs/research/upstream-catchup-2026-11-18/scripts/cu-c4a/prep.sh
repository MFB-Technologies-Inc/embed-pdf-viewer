#!/usr/bin/env bash
# CU-C4a prep: fresh payload (CU-C3-D8), deps, TS builds, native addon
set -uo pipefail
W=/workspaces/wt/CU-C4a
cd $W || exit 2
rm -rf packages/engine/runtime/npm/wasm32/lib packages/engine/runtime/build/libpdfium/wasm32
export NPM_TOKEN="" CI=true CARGO_TARGET_DIR=/workspaces/target CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
export PATH="/workspaces/bin:$PATH"
step() { echo "=== $(date +%T) $*"; "$@" || { echo "FAILED: $*"; exit 1; }; }
step pnpm install --frozen-lockfile
step bash packages/engine/runtime/scripts/fetch-libpdfium.sh linux-$(node -p process.arch)
PDF_RUNTIME_BUILD_FILE=/workspaces/wasm32-eh-pin.json bash packages/engine/runtime/scripts/fetch-libpdfium.sh wasm32-eh || echo "skipped wasm32-eh"
EMBEDPDF_RUST_CORE=0 step pnpm --filter @embedpdf/engine-runtime build:target wasm32
step pnpm exec turbo run build --filter="./packages/**" --filter="./tooling/**" --filter="!@mfb/embedpdf-rust-viewer" --concurrency=6
pnpm --filter @embedpdf/example-rust-kernel-eval bootstrap || echo "kernel-eval bootstrap failed (non-fatal)"
pnpm --filter @embedpdf/engine-runtime build:target linux-$(node -p process.arch) || echo "FAILED native addon"
step cargo fetch --locked
echo "PREP DONE"
