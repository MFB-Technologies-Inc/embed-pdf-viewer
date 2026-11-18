#!/usr/bin/env bash
# CU-C4b after: plugin vectors twice (determinism), coverage.json, the check set, replay:check.
set -uo pipefail
W=/workspaces/wt/CU-C4b; L=/workspaces/logs/CU-C4b
export CARGO_TARGET_DIR=/workspaces/target PATH="/workspaces/bin:$PATH" CI=true NPM_TOKEN=""
cd $W/tooling/plugin-conformance
for run in 1 2; do
  TZ=UTC timeout 3600 pnpm -s run vectors > $L/plugvec-run$run.log 2>&1; echo "vectors run$run exit=$?" >> $L/plugvec-runs.log
  (cd vectors && find . -type f | sort | xargs sha256sum) > $L/plugvec-run$run.sha
done
cmp -s $L/plugvec-run1.sha $L/plugvec-run2.sha && echo deterministic=yes > $L/plugvec-determinism.log || echo deterministic=no > $L/plugvec-determinism.log
TZ=UTC timeout 3600 pnpm -s run coverage > $L/coverage.log 2>&1; echo "coverage exit=$?" >> $L/plugvec-runs.log
cd $W
git status --short > $L/after-status.log
bash $W/docs/research/upstream-catchup-2026-11-18/scripts/cu-c4b/suite.sh $W $L/after cargo engconf plugconf ts > $L/suite-after.log 2>&1; echo "suite exit=$?"
cd $W/tooling/plugin-conformance && CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0 \
  PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium LD_LIBRARY_PATH=$W/packages/engine/runtime/build/libpdfium/linux-arm64/lib \
  timeout 3000 pnpm -s run replay:check > $L/after/plugconf.replay:check.log 2>&1; echo "replay:check exit=$?" | tee -a $L/after/plugconf.all
echo AFTER DONE
