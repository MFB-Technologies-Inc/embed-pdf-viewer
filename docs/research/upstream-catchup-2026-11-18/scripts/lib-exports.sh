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
syms "$1" > /tmp/old.syms; syms "$2" > /tmp/new.syms
echo "old $1: EPDF=$(grep -c ^EPDF /tmp/old.syms) FPDF=$(grep -c ^FPDF /tmp/old.syms)"
echo "new $2: EPDF=$(grep -c ^EPDF /tmp/new.syms) FPDF=$(grep -c ^FPDF /tmp/new.syms)"
echo "added:"; comm -13 /tmp/old.syms /tmp/new.syms
echo "removed:"; comm -23 /tmp/old.syms /tmp/new.syms
echo "end"
