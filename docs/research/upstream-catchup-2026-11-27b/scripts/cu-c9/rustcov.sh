#!/usr/bin/env bash
# CU-C9 (2026-11-27b), from cu-c6/rustcov.sh: logs under CU-C9.
# CU-C6 (2026-11-27b), from 2026-11-27 cu-c7/rustcov.sh: logs under CU-C6.
# CU-C7 (2026-11-27), from 2026-11-18 cu-c7/rustcov.sh and CU-C7-D9's follow-up.
# usage: rustcov.sh <checkout> <label> <mode>   mode: check | write
# rust-coverage of tooling/plugin-conformance in its own target dir (T, default /workspaces/target-cov).
set -uo pipefail
W=$1; LB=$2; MODE=$3; OUT=/workspaces/logs/CU-C9/$LB; mkdir -p "$OUT"
cd "$W" || exit 2
export CARGO_TARGET_DIR=${T:-/workspaces/target-cov} CI=true CARGO_BUILD_JOBS=6 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
export PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium
export LD_LIBRARY_PATH=$W/packages/engine/runtime/build/libpdfium/linux-arm64/lib
export PATH="/workspaces/bin:/home/node/.cargo/bin:$PATH"
{ git rev-parse HEAD; cargo llvm-cov --version; } > "$OUT/rust-coverage-$MODE.log"
s=rust-coverage; [[ $MODE == check ]] && s=rust-coverage:check
(cd tooling/plugin-conformance && timeout 7200 pnpm -s run $s) >> "$OUT/rust-coverage-$MODE.log" 2>&1
echo "$s exit=$?" | tee -a "$OUT/rust-coverage-$MODE.log"
tail -5 "$OUT/rust-coverage-$MODE.log"
git diff --stat -- tooling/plugin-conformance/vectors/rust-coverage.json
