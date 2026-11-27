#!/usr/bin/env bash
# CU-C4 (2026-11-27b), from this run's cu-c3/after.sh: logs under /workspaces/logs/CU-C4.
# CU-C3 (2026-11-27b): copied from 2026-11-27 scripts/cu-c3/after.sh.
# usage: after.sh <checkout> <label>  cargo, engine :check, plugin checks, then replay:check, and the
# failures by crate (cargo-by-crate.py) and by replayed case (replay-cases.py).
set -uo pipefail
W=$1; L=/workspaces/logs/CU-C4/$2; S=$(dirname "$0")/../cu-c3; mkdir -p $L
export CARGO_TARGET_DIR=/workspaces/target PATH="/workspaces/bin:$PATH"
bash $S/../suite.sh $W $L cargo engconf plugconf > $L/suite.log 2>&1; echo "suite exit=$?"
cd $W && CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0 \
  PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium \
  cargo test --workspace --no-run --message-format=json 2>/dev/null \
  | node -e 'let s="";process.stdin.on("data",d=>s+=d).on("end",()=>{for(const l of s.split("\n")){if(!l.startsWith("{"))continue;const m=JSON.parse(l);if(m.reason==="compiler-artifact"&&m.executable){const id=m.package_id,h=id.split("#")[1]||"",n=h.includes("@")?h.split("@")[0]:id.split("#")[0].split("/").pop();console.log(require("path").basename(m.executable),n,m.target.name)}}})' > $L/test-binaries.txt
python3 $S/../cargo-by-crate.py $L/cargo.log $L/cargo-by-crate.tsv $L/test-binaries.txt > $L/cargo-by-crate.summary
python3 $S/replay-cases.py $L/cargo.log > $L/replay-cases.tsv
cd $W/tooling/plugin-conformance && CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0 CI=true \
  PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium LD_LIBRARY_PATH=$W/packages/engine/runtime/build/libpdfium/linux-arm64/lib \
  timeout 3000 pnpm -s run replay:check > $L/plugconf.replay:check.log 2>&1; echo "replay:check exit=$?" | tee -a $L/plugconf.all
echo AFTER DONE
