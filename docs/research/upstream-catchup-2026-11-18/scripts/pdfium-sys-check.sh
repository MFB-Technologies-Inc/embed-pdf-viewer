#!/usr/bin/env bash
# usage: pdfium-sys-check.sh <checkout> <probe-crate>
# pdfium-sys's own tests, then the probe crate's load test, against the
# linux-arm64 libembedpdf in /workspaces/pdfium/lib (PDF_RUNTIME_LIB_DIR).
set -uo pipefail
C="$1"; P="$2"
export PATH="/workspaces/bin:$PATH" CARGO_TARGET_DIR="$C/target" CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
export PDF_RUNTIME_LIB_DIR=/workspaces/pdfium/lib
export LD_LIBRARY_PATH=/workspaces/pdfium/lib/linux-arm64/lib
echo "=== pdfium-sys tests"; (cd "$C" && cargo test -p pdfium-sys --locked); a=$?
echo "pdfium-sys rc=$a"
echo "=== probe load test"; cp "$C/Cargo.lock" "$P/Cargo.lock"; (cd "$P" && cargo test -- --nocapture); b=$?
echo "probe rc=$b"
for t in "$C"/target/debug/deps/pdfium_sys-* "$C"/target/debug/deps/load-*; do [[ -x $t && -f $t ]] && { echo "--- ldd $t"; ldd "$t" | grep -i embedpdf; }; done
exit $(( a || b ))
