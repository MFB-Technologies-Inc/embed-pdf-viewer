#!/usr/bin/env bash
# CU-C8 (2026-11-27b): the picture-button exports in each slice of the local
# EmbedPDFBindings.xcframework, read with the Rust toolchain's llvm-nm (Apple
# nm cannot read Rust bitcode): uniffi's set_button_image function and checksum,
# and the compiled Swift's FormWidgetLayout and FormFieldUpdateResult metadata.
# Exit 1 if any is missing.
#
#   ffi-exports.sh <llvm-nm> <EmbedPDFBindings.xcframework>
set -uo pipefail
NM="$1"; XCF="$2"
WANT=(uniffi_epdf_plugin_form_fn_method_formhandle_set_button_image
  uniffi_epdf_plugin_form_checksum_method_formhandle_set_button_image)
status=0
while IFS= read -r bin; do
  archs="$(lipo -archs "$bin")"
  for arch in $archs; do
    syms="$("$NM" -gUj --arch="$arch" "$bin" 2>/dev/null | sed 's/^_//' | sort -u)"
    for s in "${WANT[@]}"; do grep -qx "$s" <<<"$syms" || { echo "MISSING $s in $bin ($arch)"; status=1; }; done
    layout=$(grep -c 'FormWidgetLayout' <<<"$syms"); result=$(grep -c 'FormFieldUpdateResult' <<<"$syms")
    [[ $layout -gt 0 && $result -gt 0 ]] || { echo "MISSING Swift types in $bin ($arch)"; status=1; }
    echo "${bin#"$XCF"/} $arch: set_button_image fn+checksum present, FormWidgetLayout $layout symbols, FormFieldUpdateResult $result symbols"
  done
done < <(find "$XCF" -type f -name EmbedPDFBindings | sort)
exit $status
