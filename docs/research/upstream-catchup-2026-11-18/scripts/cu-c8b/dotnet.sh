#!/usr/bin/env bash
# usage: dotnet.sh <checkout> <label> <rid> <runtime-archive> [runtime-manifest]
# The .NET rows for one RID, as ../../../upstream-catchup-2026-11-12/scripts/platforms.sh ran them
# for linux-arm64: epdf-capi (debug) for the facade tests, dotnet test, the parity self-test, the
# packaging unit tests (HOST_TESTS=0 skips these four), epdf-capi release for the RID, then
# pack.sh and validate.sh with no LD_LIBRARY_PATH or DYLD_LIBRARY_PATH, so the relocation check
# sees only what the package carries. Each step's exit code goes to <out>/summary.txt. On Linux,
# validate.sh needs EPDF_IMAGE_ID (the container image) and EPDF_HOST_OS; a macOS host needs
# neither. LOGS sets the log root, CARGO_TARGET_DIR the target dir, LIBPDFIUM the extracted runtime.
set -uo pipefail
C="$1"; LABEL="$2"; RID="$3"; ARCHIVE="$4"; MANIFEST="${5:-$C/packages/engine/runtime/engine-runtime-build.json}"
OUT=${LOGS:-/workspaces/logs/CU-C8b}/dotnet-$LABEL-$RID; rm -rf "$OUT"; mkdir -p "$OUT"; R=$OUT/summary.txt; : > "$R"
case "$RID" in
  linux-arm64) TRIPLE=aarch64-unknown-linux-gnu LIBDIR=linux-arm64 ;;
  linux-x64) TRIPLE=x86_64-unknown-linux-gnu LIBDIR=linux-x64 ;;
  osx-arm64) TRIPLE=aarch64-apple-darwin LIBDIR=darwin-arm64 ;;
  osx-x64) TRIPLE=x86_64-apple-darwin LIBDIR=darwin-x64 ;;
  *) echo "unknown rid $RID"; exit 2 ;;
esac
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 CI=true
export CARGO_TARGET_DIR=${CARGO_TARGET_DIR:-/workspaces/target} CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
LIBPDFIUM=${LIBPDFIUM:-$C/packages/engine/runtime/build/libpdfium}
export PDF_RUNTIME_LIB_DIR=$LIBPDFIUM
case "$RID" in
  linux-*) export LD_LIBRARY_PATH=$LIBPDFIUM/$LIBDIR/lib ;;
  osx-*) export DYLD_LIBRARY_PATH=$LIBPDFIUM/$LIBDIR/lib ;;
esac
cd "$C" || exit 2
echo "checkout $(git rev-parse HEAD) rid $RID manifest $MANIFEST archive $ARCHIVE start $(date -u +%FT%TZ)" >> "$R"
echo "dotnet $(cd platforms/dotnet && dotnet --version) $(rustc --version)" >> "$R"
if [[ ${HOST_TESTS:-1} == 1 ]]; then
  cargo build --locked -p epdf-capi > "$OUT/capi-build.log" 2>&1; echo "capi build exit=$?" >> "$R"
  EPDF_NATIVE_DIR=$CARGO_TARGET_DIR/debug dotnet test platforms/dotnet/EmbedPDF.Tests/EmbedPDF.Tests.csproj > "$OUT/dotnet-test.log" 2>&1; echo "dotnet test exit=$?" >> "$R"
  grep -E "^(Passed|Failed)!|Total tests|Passed:|Failed:" "$OUT/dotnet-test.log" | tail -3 >> "$R"
  (cd platforms/dotnet && dotnet run --project EmbedPDF.Parity -- self-test) > "$OUT/parity.log" 2>&1
  echo "parity self-test exit=$? passed=$(grep -c '"status":"passed"' "$OUT/parity.log")" >> "$R"
  python3 -m unittest discover -s platforms/dotnet/packaging -v > "$OUT/packaging-unittest.log" 2>&1
  echo "packaging unittest exit=$? $(tail -1 "$OUT/packaging-unittest.log")" >> "$R"
fi
cargo build --locked -p epdf-capi --release --target $TRIPLE > "$OUT/capi-release.log" 2>&1; echo "capi release build exit=$?" >> "$R"
env -u LD_LIBRARY_PATH -u DYLD_LIBRARY_PATH bash platforms/dotnet/pack.sh --rid $RID --out "$OUT/pack" --runtime-manifest "$MANIFEST" \
  --runtime-archive "$ARCHIVE" > "$OUT/pack.log" 2>&1; echo "pack exit=$?" >> "$R"
env -u LD_LIBRARY_PATH -u DYLD_LIBRARY_PATH bash platforms/dotnet/validate.sh --rid $RID --runtime-manifest "$MANIFEST" --record "$OUT/validate-record.json" \
  --package "$OUT/pack/EmbedPDF.Engine.0.0.0-local.nupkg" --manifest "$OUT/pack/package-manifest.$RID.json" > "$OUT/validate.log" 2>&1
echo "validate exit=$? $(tail -1 "$OUT/validate.log")" >> "$R"
echo "end $(date -u +%FT%TZ)" >> "$R"; echo DOTNETDONE >> "$R"
