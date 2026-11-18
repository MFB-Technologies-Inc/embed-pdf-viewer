#!/usr/bin/env bash
# CU-C3-engregen-fresh.sh: engine vectors twice more on the rebuilt wasm32 payload, then every engine :check.
set -uo pipefail
W=/workspaces/wt/CU-C3; L=/workspaces/logs/CU-C3
for r in 3 4; do
  bash /workspaces/regen-engine.sh $W > $L/CU-C3-engregen-run$r.log 2>&1
  (cd $W/tooling/engine-conformance && find vectors -type f | LC_ALL=C sort | xargs sha256sum) > $L/CU-C3-engregen-run$r.sha
  git -C $W status --short > $L/CU-C3-engregen-status-run$r.txt
done
{ cmp $L/CU-C3-engregen-run3.log $L/CU-C3-engregen-run4.log && echo "logs identical"
  cmp $L/CU-C3-engregen-run3.sha $L/CU-C3-engregen-run4.sha && echo "deterministic=yes" || echo "deterministic=no"
  echo "files: $(wc -l < $L/CU-C3-engregen-run4.sha)"; echo "status after run 4:"; cat $L/CU-C3-engregen-status-run4.txt; } > $L/CU-C3-engregen-fresh-determinism.log
cat $L/CU-C3-engregen-fresh-determinism.log
bash $L/CU-C3-suite.sh $W $L/after-eng2 engconf > $L/CU-C3-suite-after-eng2.log 2>&1
cat $L/after-eng2/engconf.failed; grep -c " ok" $L/after-eng2/engconf.all
