# usage: source env.sh <checkout>   (defaults to the CU-C7 worktree)
export W=${1:-/workspaces/wt/CU-C7} L=/workspaces/logs/CU-C7
export CARGO_TARGET_DIR=/workspaces/target CI=true NPM_TOKEN="" CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
export PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium
export LD_LIBRARY_PATH=$W/packages/engine/runtime/build/libpdfium/linux-arm64/lib
export PATH="/workspaces/bin:/home/node/.cargo/bin:$PATH"
cd $W
