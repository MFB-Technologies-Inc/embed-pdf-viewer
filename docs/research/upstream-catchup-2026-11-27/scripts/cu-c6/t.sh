#!/usr/bin/env bash
# CU-C6 (2026-11-27): run a command in the CU-C6 worktree with its own target dir.
# usage: t.sh <log-label> -- <cmd...>   (log in /workspaces/logs/CU-C6/<label>.log)
set -uo pipefail
L=/workspaces/logs/CU-C6/$1.log; shift; [ "$1" = "--" ] && shift
source /workspaces/scripts/env.sh /workspaces/wt/CU-C6 /workspaces/target-c5
export PATH="/workspaces/bin:$PATH"
"$@" > $L 2>&1; e=$?
echo "exit=$e" >> $L
grep -E "^test .* FAILED$|test result|cases differ|^error|panicked" $L | head -40
echo "exit=$e log=$L"
