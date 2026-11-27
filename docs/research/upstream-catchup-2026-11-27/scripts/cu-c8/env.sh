# 2026-11-27 (CU-C8). usage: source env.sh [checkout]   (defaults to the CU-C8 worktree)
export W=${1:-/workspaces/wt/CU-C8} L=/workspaces/logs/CU-C8
export CARGO_TARGET_DIR=${T:-/workspaces/target-c8} CI=true NPM_TOKEN="" CARGO_BUILD_JOBS=${CARGO_BUILD_JOBS:-4} CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
export PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium
export LD_LIBRARY_PATH=$W/packages/engine/runtime/build/libpdfium/linux-arm64/lib
export PATH="/workspaces/bin:/home/node/.cargo/bin:$PATH"
cd $W
