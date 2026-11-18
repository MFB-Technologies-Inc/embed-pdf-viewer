#!/usr/bin/env bash
# usage: rustcov.sh <checkout> <label>
# rust-coverage (write mode) in a checkout: the summary line, then whether vectors/rust-coverage.json
# changed (what --check reports as stale), with a diff stat. The file is restored afterwards.
set -uo pipefail
W=$1; LB=$2; OUT=/workspaces/logs/CU-C7/$LB; mkdir -p $OUT
cd $W || exit 2
T=${T:-/workspaces/target}
[[ ${TOUCH:-0} == 1 ]] && find $W/crates -type f \( -name '*.rs' -o -name Cargo.toml -o -name '*.h' \) -exec touch {} +
export CARGO_TARGET_DIR=$T CI=true CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
export PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium
export LD_LIBRARY_PATH=$W/packages/engine/runtime/build/libpdfium/linux-arm64/lib
export PATH="/workspaces/bin:/home/node/.cargo/bin:$PATH"
git rev-parse HEAD > $OUT/rust-coverage.log
cargo llvm-cov --version >> $OUT/rust-coverage.log
(cd tooling/plugin-conformance && timeout 7200 pnpm -s run rust-coverage) >> $OUT/rust-coverage.log 2>&1
echo "rust-coverage exit=$?" | tee -a $OUT/rust-coverage.log
grep "wrote vectors/rust-coverage.json" $OUT/rust-coverage.log
git diff --stat -- tooling/plugin-conformance/vectors/rust-coverage.json | tee $OUT/rust-coverage.diffstat
git diff -- tooling/plugin-conformance/vectors/rust-coverage.json > $OUT/rust-coverage.diff
git show HEAD:tooling/plugin-conformance/vectors/rust-coverage.json | node -e 'let s="";process.stdin.on("data",d=>s+=d).on("end",()=>console.log("committed total: "+JSON.stringify(JSON.parse(s).total)))' | tee -a $OUT/rust-coverage.diffstat
node -e 'console.log("measured total: "+JSON.stringify(require(process.argv[1]).total))' $W/tooling/plugin-conformance/vectors/rust-coverage.json | tee -a $OUT/rust-coverage.diffstat
git checkout -- tooling/plugin-conformance/vectors/rust-coverage.json
