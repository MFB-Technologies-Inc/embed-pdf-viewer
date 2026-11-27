#!/usr/bin/env bash
# CU-C2 (2026-11-27b) checkout prep, unchanged from 2026-11-27 scripts/prep.sh.
# usage: prep.sh <checkout-dir>
# pnpm install, the linux-arm64 and wasm32 PDFium of the checkout's pin, the wasm32-eh archive
# from /workspaces/wasm32-eh-pin.json, a wasm32 payload (published, else built without the Rust
# core), every workspace package build so the TS suites and generators resolve, then the FreeType
# source (freetype-src.sh, CU-C2-D6) so packages/core/fonts' test finds pstables.h.
# CU-C3 added the freetype-src.sh step (CU-C2 review finding).
set -uo pipefail
S=$(cd "$(dirname "$0")" && pwd)
cd "$1" || exit 2
export NPM_TOKEN="" CI=true
export CARGO_TARGET_DIR="${CARGO_TARGET_DIR:-/workspaces/target}" CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
# clang shim (clang-shim.sh): generate-functions.mjs parses the headers for the AST against the
# emscripten sysroot, so uint64_t is "unsigned long long" (i64) as on macOS, not LP64 "unsigned long".
export PATH="/workspaces/bin:$PATH"
step() { echo "=== $(date +%T) $*"; "$@" || { echo "FAILED: $*"; exit 1; }; }
step pnpm install --frozen-lockfile
step bash packages/engine/runtime/scripts/fetch-libpdfium.sh linux-$(node -p process.arch)
PDF_RUNTIME_BUILD_FILE=/workspaces/wasm32-eh-pin.json bash packages/engine/runtime/scripts/fetch-libpdfium.sh wasm32-eh || echo "skipped wasm32-eh"
if ! bash packages/engine/runtime/scripts/fetch-payload.sh wasm32; then
  echo "no published payload; building wasm32 without the Rust core"
  EMBEDPDF_RUST_CORE=0 step pnpm --filter @embedpdf/engine-runtime build:target wasm32
fi
step pnpm exec turbo run build --filter='./packages/**' --filter='./tooling/**' --filter='!@mfb/embedpdf-rust-viewer' --concurrency=6
pnpm --filter @embedpdf/example-rust-kernel-eval bootstrap || echo "kernel-eval bootstrap failed (non-fatal)"
step cargo fetch --locked
step bash "$S/freetype-src.sh" "$PWD"
echo "PREP DONE"
