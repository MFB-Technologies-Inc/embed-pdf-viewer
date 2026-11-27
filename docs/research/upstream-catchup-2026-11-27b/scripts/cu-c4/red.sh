#!/usr/bin/env bash
# CU-C4 (2026-11-27b), from 2026-11-27 scripts/cu-c4/red.sh: the job's new engine tests and vectors
# against code that predates them.
# usage: red.sh <code-commit> <tests-commit> <label>
#   A detached worktree at <code-commit> takes <tests-commit>'s test files and change vectors, then
#   runs those tests. Logs in /workspaces/logs/CU-C4/<label>/.
set -uo pipefail
CODE=$1; TESTS=$2; L=/workspaces/logs/CU-C4/$3; W=/workspaces/wt/CU-C4-red
mkdir -p $L
git -C /workspaces/wt/CU-C2 worktree add --detach $W $CODE > $L/worktree.log 2>&1 || exit 2
cd $W
TESTS_FILES="signature_compat_v4 signature_mark form_attribution form_transfer anyone_fields"
git checkout $TESTS -- tooling/engine-conformance/vectors/changes \
  $(for t in $TESTS_FILES; do echo crates/epdf-engine/tests/$t.rs; done)
export CARGO_TARGET_DIR=/workspaces/target CARGO_BUILD_JOBS=8 CARGO_INCREMENTAL=0 \
  CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0 \
  PDF_RUNTIME_LIB_DIR=/workspaces/wt/CU-C2/packages/engine/runtime/build/libpdfium \
  LD_LIBRARY_PATH=/workspaces/wt/CU-C2/packages/engine/runtime/build/libpdfium/linux-arm64/lib
cargo test -p epdf-engine --no-fail-fast $(for t in $TESTS_FILES; do echo --test $t; done) \
  > $L/cargo.log 2>&1
echo "exit=$?" > $L/cargo.exit
grep -E "^test .* (ok|FAILED)$|test result" $L/cargo.log > $L/summary.txt
cd / && git -C /workspaces/wt/CU-C2 worktree remove --force $W
# Cargo hashes a path package by its path inside the workspace, so this worktree's build wrote
# the same artifacts as the job's own; touch the job's sources so its next build rebuilds them.
find /workspaces/wt/CU-C2/crates -name '*.rs' -exec touch {} +
echo RED DONE
