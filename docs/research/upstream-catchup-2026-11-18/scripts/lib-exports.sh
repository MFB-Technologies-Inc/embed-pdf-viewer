#!/usr/bin/env bash
# usage: lib-exports.sh <old-lib> <new-lib>
# Exported (defined, global) EPDF*/FPDF* symbols of two libembedpdf builds and
# their difference. .so: dynamic symbol table; .a: llvm-nm of every member.
set -euo pipefail
NM=/workspaces/emsdk4/upstream/bin/llvm-nm
syms() {
  case "$1" in
    *.so) $NM -D --defined-only --extern-only "$1" ;;
    *.a)  $NM --defined-only --extern-only "$1" 2>/dev/null ;;
  esac | awk 'NF>=2 {print $NF}' | grep -E '^_?(EPDF|FPDF)' | sed 's/^_//' | sort -u
}
T=$(mktemp -d); trap 'rm -rf "$T"' EXIT
syms "$1" > "$T/old.syms"; syms "$2" > "$T/new.syms"
echo "old $1: EPDF=$(grep -c ^EPDF "$T/old.syms") FPDF=$(grep -c ^FPDF "$T/old.syms")"
echo "new $2: EPDF=$(grep -c ^EPDF "$T/new.syms") FPDF=$(grep -c ^FPDF "$T/new.syms")"
echo "added:"; comm -13 "$T/old.syms" "$T/new.syms"
echo "removed:"; comm -23 "$T/old.syms" "$T/new.syms"
echo "end"
