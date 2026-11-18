#!/usr/bin/env bash
# CU-C3-merge1.sh: prep the merged checkout, typecheck both generators, build the workspace,
# regenerate the engine vectors twice and hash them after each run.
set -uo pipefail
W=/workspaces/wt/CU-C3; L=/workspaces/logs/CU-C3
export CARGO_TARGET_DIR=/workspaces/target
bash $L/CU-C3-prep.sh $W > $L/CU-C3-prep-merge.log 2>&1; echo "prep exit=$?"
bash $L/CU-C3-prep-extra.sh $W > $L/CU-C3-prep-extra-merge.log 2>&1; echo "prep-extra exit=$?"
(cd $W/tooling/engine-conformance && pnpm -s run typecheck) > $L/CU-C3-engconf-typecheck-merge.log 2>&1; echo "engconf typecheck exit=$?"
(cd $W/tooling/plugin-conformance && pnpm -s run typecheck) > $L/CU-C3-plugconf-typecheck-merge2.log 2>&1; echo "plugconf typecheck exit=$?"
touch $W/crates/pdfium-sys/build.rs
(cd $W && CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0 \
  PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium cargo build --workspace --tests) > $L/CU-C3-cargo-build-merge.log 2>&1; echo "cargo build exit=$?"
for r in 1 2; do
  bash /workspaces/regen-engine.sh $W > $L/CU-C3-engregen-run$r.log 2>&1
  for f in /tmp/regen.*.log; do cp $f $L/engregen-run$r-$(basename $f); done
  (cd $W/tooling/engine-conformance && find vectors -type f | LC_ALL=C sort | xargs sha256sum) > $L/CU-C3-engregen-run$r.sha
  git -C $W status --short > $L/CU-C3-engregen-status-run$r.txt
done
{ cmp $L/CU-C3-engregen-run1.log $L/CU-C3-engregen-run2.log && echo "logs identical"
  cmp $L/CU-C3-engregen-run1.sha $L/CU-C3-engregen-run2.sha && echo "deterministic=yes" || echo "deterministic=no"
  echo "files: $(wc -l < $L/CU-C3-engregen-run2.sha)"; cat $L/CU-C3-engregen-status-run2.txt; } > $L/CU-C3-engregen-determinism.log
cat $L/CU-C3-engregen-determinism.log
echo MERGE1 DONE
