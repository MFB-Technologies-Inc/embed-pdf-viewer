#!/usr/bin/env bash
# usage: table.sh <checkout> <label>
# The Rust and Node rows that need no wasm payload: cargo, engine :check, the four plugin checks,
# TS package tests (suite.sh), then replay:check and the evidence tests.
set -uo pipefail
W=$1; LB=$2; S=/workspaces/wt/CU-C7/docs/research/upstream-catchup-2026-11-18/scripts; OUT=/workspaces/logs/CU-C7/$LB
source $S/cu-c7/env.sh $W
mkdir -p $OUT
bash $S/cu-c4b/suite.sh $W $OUT cargo engconf plugconf ts > $OUT/suite.log 2>&1; echo "suite exit=$?"
(cd tooling/plugin-conformance && timeout 3000 pnpm -s run replay:check) > $OUT/plugconf.replay:check.log 2>&1; echo "replay:check exit=$?" | tee -a $OUT/plugconf.all
bash $S/cu-c7/evrun.sh $W $OUT/evidence > $OUT/evrun.log 2>&1; echo "evidence exit=$?"
echo TABLE DONE
