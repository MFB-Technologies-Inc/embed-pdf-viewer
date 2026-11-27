#!/usr/bin/env bash
# CU-C9 (2026-11-27): CU-C1's export diff in each Apple slice archive, read with
# the Rust toolchain's llvm-nm (Apple nm cannot read Rust bitcode). Prints, per
# archive, each renamed and new export's presence and each old name's absence,
# plus the archive's sha256. Exit 1 if any check fails.
#
#   slice-exports.sh <llvm-nm> <libembedpdf.a>...
set -uo pipefail
NM="$1"; shift
NEW=(EPDFAnnot_GetDrawing EPDFAnnot_SetDrawing EPDFDoc_GetDrawings
  EPDFAnnot_GetMKIcon EPDFAnnot_SetMKIcon EPDFAnnot_HasMKIcon EPDFAnnot_ClearMKIcon
  EPDFText_GetPageGeometry)
OLD=(EPDFAnnot_GetStampDrawing EPDFAnnot_SetStampDrawing EPDFDoc_GetStampDrawings)
status=0
for lib in "$@"; do
  syms="$("$NM" -gUj "$lib" 2>/dev/null | sed 's/^_//' | sort -u)"
  present=0; absent=0
  for s in "${NEW[@]}"; do grep -qx "$s" <<<"$syms" && present=$((present + 1)) || { echo "MISSING $s in $lib"; status=1; }; done
  for s in "${OLD[@]}"; do grep -qx "$s" <<<"$syms" && { echo "STILL THERE $s in $lib"; status=1; } || absent=$((absent + 1)); done
  echo "$(shasum -a 256 "$lib" | awk '{print $1}') $lib: ${present}/8 new present, ${absent}/3 old absent, $(grep -c '^EPDF' <<<"$syms") EPDF* exports"
done
exit $status
