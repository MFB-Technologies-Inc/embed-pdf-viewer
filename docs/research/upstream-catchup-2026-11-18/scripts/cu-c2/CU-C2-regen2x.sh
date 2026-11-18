#!/usr/bin/env bash
# usage: CU-C2-regen2x.sh <checkout>  regenerate twice, hash vectors after each run
set -uo pipefail
W=$1; L=/workspaces/logs/CU-C2
for r in 1 2; do
  RLOG=$L/regen-tmp-run$r bash $L/CU-C2-regen-engine.sh $W > $L/CU-C2-regen-run$r.log 2>&1
  (cd $W/tooling/engine-conformance && find vectors -type f | LC_ALL=C sort | xargs sha256sum) > $L/CU-C2-regen-sha-run$r.txt
  git -C $W status --short > $L/CU-C2-regen-status-run$r.txt
done
{ cmp $L/CU-C2-regen-run1.log $L/CU-C2-regen-run2.log && echo "logs identical"
  cmp $L/CU-C2-regen-sha-run1.txt $L/CU-C2-regen-sha-run2.txt && echo "deterministic=yes" || echo "deterministic=no"
  echo "files: $(wc -l < $L/CU-C2-regen-sha-run2.txt)"
  echo "git status after run 2:"; cat $L/CU-C2-regen-status-run2.txt; } > $L/CU-C2-regen-determinism.log
