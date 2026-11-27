#!/usr/bin/env bash
# CU-C4 (2026-11-27): the job's new engine tests and vectors against code that predates them.
# usage: red.sh <code-commit> <tests-commit> <label>
#   A detached worktree at <code-commit> takes <tests-commit>'s test files and change vectors,
#   then runs those tests. Logs in /workspaces/logs/CU-C4/<label>/.
set -uo pipefail
CODE=$1; TESTS=$2; L=/workspaces/logs/CU-C4/$3; W=/workspaces/wt/CU-C4-red
mkdir -p $L
git -C /workspaces/wt/CU-C2 worktree add --detach $W $CODE > $L/worktree.log 2>&1 || exit 2
cd $W
git checkout $TESTS -- \
  crates/epdf-engine/tests/change_ops.rs crates/epdf-engine/tests/signature_mark.rs \
  crates/epdf-engine/tests/font_fallback_named.rs crates/epdf-engine/tests/wire_numbers.rs \
  crates/epdf-engine/tests/host_sessions.rs tooling/engine-conformance/vectors/changes
export CARGO_TARGET_DIR=/workspaces/target CARGO_BUILD_JOBS=6 CARGO_INCREMENTAL=0 \
  CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0 PDF_RUNTIME_LIB_DIR=/workspaces/pdfium/lib \
  LD_LIBRARY_PATH=/workspaces/pdfium/lib/linux-arm64/lib
cargo test -p epdf-engine --no-fail-fast --test change_ops --test signature_mark \
  --test font_fallback_named --test wire_numbers --test host_sessions > $L/cargo.log 2>&1
echo "exit=$?" > $L/cargo.exit
grep -E "^test .* (ok|FAILED)$|cases differ|test result" $L/cargo.log > $L/summary.txt
cd / && git -C /workspaces/wt/CU-C2 worktree remove --force $W
# Cargo hashes a path package by its path inside the workspace, so this worktree's build wrote
# the same artifacts as the job's own; touch the job's sources so its next build rebuilds them.
find /workspaces/wt/CU-C2/crates -name '*.rs' -exec touch {} +
echo RED DONE
