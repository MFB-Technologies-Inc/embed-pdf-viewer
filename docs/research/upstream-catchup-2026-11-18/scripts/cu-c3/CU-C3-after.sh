#!/usr/bin/env bash
# CU-C3-after.sh: the check set on the job branch tip, plus replay:check.
set -uo pipefail
W=/workspaces/wt/CU-C3; L=/workspaces/logs/CU-C3
export CARGO_TARGET_DIR=/workspaces/target
bash $L/CU-C3-suite.sh $W $L/after cargo engconf plugconf ts > $L/CU-C3-suite-after.log 2>&1; echo "suite exit=$?"
cd $W/tooling/plugin-conformance && CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0 CI=true \
  PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium LD_LIBRARY_PATH=$W/packages/engine/runtime/build/libpdfium/linux-arm64/lib \
  timeout 3000 pnpm -s run replay:check > $L/after/plugconf.replay:check.log 2>&1; echo "replay:check exit=$?" | tee -a $L/after/plugconf.all
echo AFTER DONE
