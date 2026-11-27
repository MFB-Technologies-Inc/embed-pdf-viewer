#!/usr/bin/env bash
# CU-C2 (2026-11-27b) driver: prep-extra, then suite.sh at the merge (target /workspaces/target)
# and at the baseline 0afd87825 (target /workspaces/target-base), one after the other.
L=/workspaces/logs/CU-C2
export PATH="/workspaces/bin:$PATH"
CARGO_TARGET_DIR=/workspaces/target bash /workspaces/scripts/prep-extra.sh /workspaces/wt/CU-C2 > $L/prep-extra-head.log 2>&1
CARGO_TARGET_DIR=/workspaces/target-base bash /workspaces/scripts/prep-extra.sh /workspaces/wt/base > $L/prep-extra-base.log 2>&1
CARGO_TARGET_DIR=/workspaces/target bash /workspaces/scripts/suite.sh /workspaces/wt/CU-C2 $L/head > $L/suite-head.log 2>&1
CARGO_TARGET_DIR=/workspaces/target-base bash /workspaces/scripts/suite.sh /workspaces/wt/base $L/base > $L/suite-base.log 2>&1
echo ALL SUITES DONE
