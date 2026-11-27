#!/usr/bin/env bash
# CU-C3 (2026-11-27), from 2026-11-18 cu-c3/CU-C3-merge1.sh and CU-C3-regen2.sh.
# CU-C3 (2026-11-27b): copied from 2026-11-27 scripts/cu-c3/regen2x.sh.
# usage: regen2x.sh <checkout> <label>  engine vectors twice, then plugin vectors twice (TZ=UTC),
# hashing every vector file after each run and comparing the two runs of each.
set -uo pipefail
W=$1; L=/workspaces/logs/CU-C3/$2; S=$(dirname "$0"); mkdir -p $L
export CI=true TZ=UTC PATH="/workspaces/bin:$PATH"
for r in 1 2; do
  RLOG=$L/eng-run$r bash $S/regen-engine.sh $W > $L/engregen-run$r.log 2>&1
  (cd $W/tooling/engine-conformance && find vectors -type f | LC_ALL=C sort | xargs sha256sum) > $L/engregen-run$r.sha
  git -C $W status --short > $L/engregen-status-run$r.txt
done
{ cmp $L/engregen-run1.log $L/engregen-run2.log && echo "engine logs identical"
  cmp $L/engregen-run1.sha $L/engregen-run2.sha && echo "engine deterministic=yes" || echo "engine deterministic=no"
  echo "engine files: $(wc -l < $L/engregen-run2.sha)"; } > $L/determinism.log
for r in 1 2; do
  (cd $W/tooling/plugin-conformance && timeout 3600 pnpm -s run vectors) > $L/plugregen-run$r.log 2>&1; echo "plugin run$r exit=$?" >> $L/determinism.log
  (cd $W/tooling/plugin-conformance && find vectors -type f | LC_ALL=C sort | xargs sha256sum; sha256sum $W/crates/plugins/acrojs/prelude.js) > $L/plugregen-run$r.sha
done
{ cmp $L/plugregen-run1.sha $L/plugregen-run2.sha && echo "plugin deterministic=yes" || echo "plugin deterministic=no"
  echo "plugin files: $(wc -l < $L/plugregen-run2.sha)"; git -C $W status --short; } >> $L/determinism.log
cat $L/determinism.log
echo REGEN2X DONE
