#!/usr/bin/env bash
# usage: CU-C3-baseline.sh <checkout> <label>  prep, prep-extra, full suite, evidence
set -uo pipefail
W=$1; LB=$2; L=/workspaces/logs/CU-C3
export CARGO_TARGET_DIR=/workspaces/target
bash $L/CU-C3-prep.sh $W > $L/CU-C3-prep-$LB.log 2>&1; echo "prep exit=$?"
bash $L/CU-C3-prep-extra.sh $W > $L/CU-C3-prep-extra-$LB.log 2>&1; echo "prep-extra exit=$?"
bash $L/CU-C3-suite.sh $W $L/$LB cargo engconf plugconf ts > $L/CU-C3-suite-$LB.log 2>&1; echo "suite exit=$?"
bash $L/CU-C3-evrun.sh $W $LB > $L/CU-C3-ev-$LB.log 2>&1; echo "ev exit=$?"
