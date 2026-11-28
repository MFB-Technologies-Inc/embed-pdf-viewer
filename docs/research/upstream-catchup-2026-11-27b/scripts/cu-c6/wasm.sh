#!/usr/bin/env bash
# CU-C6 (2026-11-27b), from 2026-11-27 cu-c7/wasm.sh: logs under CU-C6, T defaults to
# /workspaces/target.
# CU-C7 (2026-11-27), from 2026-11-18 cu-c7/wasm.sh and ../cu-c6/payload.sh.
# usage: wasm.sh <checkout> <label> <wasm32-eh pin file>
# The rows that need wasm or the Node addon: the plain wasm32 payload with the Rust core (expected
# to fail to link, CU-C6-D9), the Rust-core wasm32-eh payload (emsdk 4.0.0 at /opt/emsdk-eh) and
# verify-rust-core.mjs, epdf-web for core-wasm, core-wasm vitest, test:gc and typecheck, the
# epdf-web wasm tests, and the epdf-node addon test. <checkout>/target is a symlink to T:
# build-target.sh, the epdf-web build script and addon.mjs read <repo>/target. The wasm32 payload
# prep made is restored at the end, so the checkout's npm/wasm32/lib is what it was.
set -uo pipefail
W=$1; LB=$2; PIN=$3; OUT=/workspaces/logs/CU-C6/$LB
mkdir -p "$OUT"; : > "$OUT/wasm.all"
cd "$W" || exit 2
T=${T:-/workspaces/target}
ln -sfn "$T" "$W/target"
[[ ${TOUCH:-0} == 1 ]] && find "$W/crates" -type f \( -name '*.rs' -o -name Cargo.toml -o -name '*.h' \) -exec touch {} +
export PATH="/workspaces/bin:/home/node/.cargo/bin:$PATH" CI=true NPM_TOKEN=""
export PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium
export CARGO_TARGET_DIR=$T CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
row() { local name=$1; shift; "$@" > "$OUT/$name.log" 2>&1; local e=$?; echo "$name exit=$e" | tee -a "$OUT/wasm.all"; return $e; }
LIB=packages/engine/runtime/npm/wasm32/lib
rm -rf "$OUT/prep-lib"; cp -a "$LIB" "$OUT/prep-lib"
git rev-parse HEAD > "$OUT/head.txt"
row payload-wasm32 bash -c 'emcc --version | head -1; bash packages/engine/runtime/scripts/build-target.sh wasm32'
rm -rf "$LIB"; cp -a "$OUT/prep-lib" "$LIB"
payload() (
  unset EM_BINARYEN_ROOT EM_CONFIG
  source /opt/emsdk-eh/emsdk_env.sh > /dev/null 2>&1
  export PDF_RUNTIME_BUILD_FILE=$PIN
  echo "emcc: $(emcc --version | head -1)"
  bash packages/engine/runtime/scripts/build-target.sh wasm32-eh
)
row payload payload
grep -E "bound functions" "$OUT/payload.log" | tail -2
row verify-rust-core node packages/engine/runtime/scripts/verify-rust-core.mjs
row build-wasm pnpm --filter @embedpdf/core-wasm run build:wasm
(cd packages/core-wasm && row core-wasm-vitest npx vitest run)
(cd packages/core-wasm && row core-wasm-gc npx vitest run -c vitest.gc.config.ts)
(cd packages/core-wasm && row core-wasm-typecheck pnpm run typecheck)
row epdf-web env CARGO_TARGET_WASM32_UNKNOWN_UNKNOWN_RUNNER=wasm-bindgen-test-runner cargo test -p epdf-web --target wasm32-unknown-unknown --test '*'
row addon-build cargo build -p epdf-node --release
row addon env LD_LIBRARY_PATH=$W/packages/engine/runtime/build/libpdfium/linux-arm64/lib node crates/epdf-node/tests/addon.mjs
rm -rf "$OUT/rust-core-lib"; cp -a "$LIB" "$OUT/rust-core-lib"
rm -rf "$LIB"; cp -a "$OUT/prep-lib" "$LIB"
echo WASM DONE
