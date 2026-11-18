#!/usr/bin/env bash
# CU-C4a after: engine regen x2 (determinism), the check set, replay:check.
set -uo pipefail
W=/workspaces/wt/CU-C4a; L=/workspaces/logs/CU-C4a
export CARGO_TARGET_DIR=/workspaces/target PATH="/workspaces/bin:$PATH" CI=true NPM_TOKEN=""
cd $W
for run in 1 2; do
  bash /workspaces/regen-engine.sh $W > $L/engregen-run$run.log 2>&1
  (cd tooling/engine-conformance && find vectors -type f -name "*.json" | sort | xargs sha256sum) > $L/engregen-run$run.sha
done
cmp -s $L/engregen-run1.sha $L/engregen-run2.sha && echo deterministic=yes > $L/engregen-determinism.log || echo deterministic=no > $L/engregen-determinism.log
git status --short > $L/engregen-status.log
bash $L/suite.sh $W $L/after cargo engconf plugconf ts > $L/suite-after.log 2>&1; echo "suite exit=$?"
cd $W/tooling/plugin-conformance && CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0 \
  PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium LD_LIBRARY_PATH=$W/packages/engine/runtime/build/libpdfium/linux-arm64/lib \
  timeout 3000 pnpm -s run replay:check > $L/after/plugconf.replay:check.log 2>&1; echo "replay:check exit=$?" | tee -a $L/after/plugconf.all
echo AFTER DONE
