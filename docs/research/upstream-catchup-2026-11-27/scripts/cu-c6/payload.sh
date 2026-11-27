#!/usr/bin/env bash
# CU-C6 (2026-11-27): the Rust-core wasm32 payload (#1125, #1143 scripts), from
# 2026-11-18 cu-c7/wasm.sh: build-target.sh wasm32 with the Rust core (the default), then
# verify-rust-core.mjs. verify-wasm-abi.mjs runs inside compile.esm.sh.
# usage: payload.sh <checkout> <label> [target]   Logs in /workspaces/logs/CU-C6/<label>/.
# <checkout>/target is a symlink to the job target dir: build-target.sh links
# <repo>/target/wasm32-unknown-emscripten/release/libepdf_wasm.a.
set -uo pipefail
W=$1; OUT=/workspaces/logs/CU-C6/$2; TGT=${3:-wasm32}; mkdir -p "$OUT"; : > "$OUT/payload.all"
cd "$W" || exit 2
T=/workspaces/target-c5
ln -sfn $T "$W/target"
export PATH="/workspaces/bin:/home/node/.cargo/bin:$PATH" CI=true NPM_TOKEN=""
export PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium
export CARGO_TARGET_DIR=$T CARGO_BUILD_JOBS=6 CARGO_INCREMENTAL=0
row() { local name=$1; shift; "$@" > "$OUT/$name.log" 2>&1; local e=$?; echo "$name exit=$e" | tee -a "$OUT/payload.all"; return $e; }
if [[ $TGT == wasm32-eh ]]; then
  export PDF_RUNTIME_BUILD_FILE=/workspaces/wasm32-eh-pin.json
  payload() ( unset EM_BINARYEN_ROOT EM_CONFIG; source /opt/emsdk-eh/emsdk_env.sh >/dev/null 2>&1; emcc --version | head -1; bash packages/engine/runtime/scripts/build-target.sh wasm32-eh )
else
  payload() ( emcc --version | head -1; bash packages/engine/runtime/scripts/build-target.sh "$TGT" )
fi
row payload payload
grep -E "bound functions|verify|epdf_wasm|Finished" "$OUT/payload.log" | tail -5
row verify-rust-core node packages/engine/runtime/scripts/verify-rust-core.mjs
cat "$OUT/verify-rust-core.log"
git -C "$W" status --short | head
echo PAYLOAD DONE
