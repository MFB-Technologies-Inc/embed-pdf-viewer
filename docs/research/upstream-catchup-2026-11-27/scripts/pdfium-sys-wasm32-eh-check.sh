#!/usr/bin/env bash
# usage: pdfium-sys-wasm32-eh-check.sh <checkout> <probe-crate>
# 1. pdfium-sys for wasm32-unknown-emscripten: bindgen over the wasm32-eh
#    headers in /workspaces/pdfium/lib (the image's emsdk 4.0.0 at /opt/emsdk-eh).
# 2. The probe's load test linked against the wasm32-eh libembedpdf.a with
#    emcc 4.0.0 and run under node.
set -uo pipefail
C="$1"; P="$2"
unset EM_BINARYEN_ROOT EM_CONFIG
source /opt/emsdk-eh/emsdk_env.sh > /dev/null 2>&1
embuilder build sysroot > /dev/null || { echo "FAILED embuilder build sysroot"; exit 1; }
echo "emcc: $(emcc --version | head -1)"
export CARGO_TARGET_DIR="${CARGO_TARGET_DIR:-$C/target}" CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
export PDF_RUNTIME_LIB_DIR=/workspaces/pdfium/lib
echo "=== pdfium-sys build, wasm32-unknown-emscripten"
(cd "$C" && cargo build -p pdfium-sys --target wasm32-unknown-emscripten --locked); a=$?
echo "pdfium-sys wasm32 rc=$a"
find "$CARGO_TARGET_DIR" -path "*pdfium-sys*/out/bindings.rs" | while read -r f; do
  echo "$f: pub fn=$(grep -c 'pub fn ' "$f") new=$(grep -cE 'pub fn (EPDFAnnot_GetDrawing|EPDFAnnot_SetDrawing|EPDFDoc_GetDrawings|EPDFAnnot_GetMKIcon|EPDFAnnot_SetMKIcon|EPDFAnnot_HasMKIcon|EPDFAnnot_ClearMKIcon|EPDFText_GetPageGeometry)\(' "$f")"
done
echo "=== probe load test, wasm32-eh under node"
cp "$C/Cargo.lock" "$P/Cargo.lock"
export CARGO_TARGET_WASM32_UNKNOWN_EMSCRIPTEN_RUNNER=node
export CARGO_TARGET_WASM32_UNKNOWN_EMSCRIPTEN_RUSTFLAGS="-C link-arg=/workspaces/pdfium/lib/wasm32-eh/lib/libembedpdf.a -C link-arg=-fwasm-exceptions -C link-arg=-sALLOW_MEMORY_GROWTH=1"
(cd "$P" && cargo test --target wasm32-unknown-emscripten --test load -- --nocapture); b=$?
echo "probe wasm32-eh rc=$b"
exit $(( a || b ))
