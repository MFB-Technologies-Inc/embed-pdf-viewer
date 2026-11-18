#!/usr/bin/env bash
# usage: wasm.sh <checkout> <label> <wasm32-eh pin file>
# The browser table, as ../cu-c7/wasm.sh without its addon rows: the Rust-core wasm32 payload
# (emsdk 4.0.0), epdf-web for core-wasm (build:wasm), core-wasm vitest, test:gc and typecheck,
# and the epdf-web wasm tests. <checkout>/target is a symlink to the shared /workspaces/target:
# build-target.sh and the epdf-web build script read <repo>/target.
set -uo pipefail
W=$1; LB=$2; PIN=$3; OUT=/workspaces/logs/CU-C8b/$LB
mkdir -p $OUT; : > $OUT/wasm.all
cd $W || exit 2
# T: the cargo target dir (default the shared one). Cargo fingerprints workspace crates by path
# relative to the workspace and by mtime, so a build from another checkout of the same tree that is
# newer than this checkout's files counts as fresh here. TOUCH=1 makes this checkout's sources newer
# than any such build; the start checkout uses its own T instead (CU-C7-D3).
T=${T:-/workspaces/target}
ln -sfn $T $W/target
[[ ${TOUCH:-0} == 1 ]] && find $W/crates -type f \( -name '*.rs' -o -name Cargo.toml -o -name '*.h' \) -exec touch {} +
export PATH="/workspaces/bin:/home/node/.cargo/bin:$PATH" CI=true NPM_TOKEN=""
# Named, not left to pdfium-sys's default: with one target dir for two checkouts, a changed
# PDF_RUNTIME_LIB_DIR is what makes pdfium-sys regenerate its bindings for the other checkout.
export PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium
export CARGO_TARGET_DIR=$T CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
row() { local name=$1; shift; "$@" > $OUT/$name.log 2>&1; local e=$?; echo "$name exit=$e" | tee -a $OUT/wasm.all; return $e; }
payload() (
  unset EM_BINARYEN_ROOT EM_CONFIG
  source /workspaces/emsdk4/emsdk_env.sh > /dev/null 2>&1
  embuilder build sysroot > /dev/null || { echo "FAILED embuilder build sysroot"; exit 1; }
  export PDF_RUNTIME_BUILD_FILE=$PIN
  echo "emcc: $(emcc --version | head -1)"
  echo "EM_BINARYEN_ROOT=${EM_BINARYEN_ROOT:-unset} EM_CONFIG=${EM_CONFIG:-unset}"
  bash packages/engine/runtime/scripts/build-target.sh wasm32-eh
)
row payload payload
row build-wasm pnpm --filter @embedpdf/core-wasm run build:wasm
(cd packages/core-wasm && row core-wasm-vitest npx vitest run)
(cd packages/core-wasm && row core-wasm-gc npx vitest run -c vitest.gc.config.ts)
(cd packages/core-wasm && row core-wasm-typecheck pnpm run typecheck)
row epdf-web env CARGO_TARGET_WASM32_UNKNOWN_UNKNOWN_RUNNER=wasm-bindgen-test-runner cargo test -p epdf-web --target wasm32-unknown-unknown --test '*'
echo WASM DONE
