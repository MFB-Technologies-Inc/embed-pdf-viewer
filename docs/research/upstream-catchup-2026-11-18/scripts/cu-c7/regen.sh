#!/usr/bin/env bash
# usage: regen.sh <label>
# Plugin vectors twice (determinism), coverage.json, then the plugin checks and replay:check.
set -uo pipefail
source /workspaces/wt/CU-C7/docs/research/upstream-catchup-2026-11-18/scripts/cu-c7/env.sh /workspaces/wt/CU-C7
OUT=$L/$1; mkdir -p $OUT; : > $OUT/regen.all
cd $W/tooling/plugin-conformance
for run in 1 2; do
  TZ=UTC timeout 3600 pnpm -s run vectors > $OUT/plugvec-run$run.log 2>&1; echo "vectors run$run exit=$?" >> $OUT/regen.all
  (cd vectors && find . -type f | sort | xargs sha256sum) > $OUT/plugvec-run$run.sha
done
cmp -s $OUT/plugvec-run1.sha $OUT/plugvec-run2.sha && echo deterministic=yes >> $OUT/regen.all || echo deterministic=no >> $OUT/regen.all
TZ=UTC timeout 3600 pnpm -s run coverage > $OUT/coverage.log 2>&1; echo "coverage exit=$?" >> $OUT/regen.all
git -C $W status --short > $OUT/status.log
for s in vectors:check coverage:check test:harness typecheck replay:check; do
  timeout 3600 pnpm -s run $s > "$OUT/plugconf.$s.log" 2>&1; echo "$s exit=$?" >> $OUT/regen.all
done
cat $OUT/regen.all
