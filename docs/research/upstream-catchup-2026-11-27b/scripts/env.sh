# usage: source env.sh <checkout> <target-dir>
export W=$1 CARGO_TARGET_DIR=$2 CI=true NPM_TOKEN="" CARGO_BUILD_JOBS=6 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
export PDF_RUNTIME_LIB_DIR=/workspaces/pdfium/lib
export LD_LIBRARY_PATH=/workspaces/pdfium/lib/linux-arm64/lib
cd $W
