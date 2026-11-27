#!/usr/bin/env bash
# CU-C5 (2026-11-27): the job's tests and vectors against code that predates them.
# usage: red.sh <code-commit> <tests-commit> <label> <cargo test args...>
#   A detached worktree at <code-commit> takes <tests-commit>'s form, signature and stamp
#   test files and the plugin vectors, then runs `cargo test <args>`.
#   Logs in /workspaces/logs/CU-C5/red-<label>/. The target dir is the job's own
#   (/workspaces/target-c5); the job's sources are touched afterwards (CU-C4-D10).
set -uo pipefail
CODE=$1; TESTS=$2; L=/workspaces/logs/CU-C5/red-$3; shift 3; W=/workspaces/wt/CU-C5-red
mkdir -p "$L"
git -C /workspaces/wt/CU-C5 worktree add --detach "$W" "$CODE" > "$L/worktree.log" 2>&1 || exit 2
cd "$W"
git checkout "$TESTS" -- crates/plugins/form/tests crates/plugins/signature/tests \
  crates/plugins/stamp/tests tooling/plugin-conformance/vectors/plugins ${EXTRA:-} >> "$L/worktree.log" 2>&1
export CARGO_TARGET_DIR=/workspaces/target-c5 CARGO_BUILD_JOBS=6 CARGO_INCREMENTAL=0 \
  CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0 PDF_RUNTIME_LIB_DIR=/workspaces/pdfium/lib \
  LD_LIBRARY_PATH=/workspaces/pdfium/lib/linux-arm64/lib
cargo test "$@" > "$L/cargo.log" 2>&1
echo "exit=$?" > "$L/cargo.exit"
grep -E "^test .* (ok|FAILED)$|cases differ|^  [a-z0-9-]+(/[a-z0-9-]+)*: |test result|^error" "$L/cargo.log" > "$L/summary.txt"
cd / && git -C /workspaces/wt/CU-C5 worktree remove --force "$W"
find /workspaces/wt/CU-C5/crates -name '*.rs' -exec touch {} +
echo RED DONE
