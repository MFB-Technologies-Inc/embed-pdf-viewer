#!/usr/bin/env bash
# CU-C9 (2026-11-27b): the re-verification of the merged tree, one row after another.
#   run.sh <checkout>
# table.sh's suite (cargo test, engine :check, the four plugin checks), replay:check and the
# evidence tests, then rust-coverage:check in its own target dir.
W=$1; D=$(dirname "$0")
bash "$D/table.sh" "$W" head suite replay evidence
T=/workspaces/target-cov bash "$D/rustcov.sh" "$W" rustcov check
echo RUN DONE
