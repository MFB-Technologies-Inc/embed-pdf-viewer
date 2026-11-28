#!/usr/bin/env bash
# 2026-11-27b (CU-C7), from ../../../upstream-catchup-2026-11-27/scripts/cu-c8/dotnet.sh; the log
# root and the default target dir are this job's.
# usage: dotnet.sh <checkout> <label> <rid> <runtime-archive> [runtime-manifest]
# The .NET rows for one RID: epdf-capi (debug) and the facade tests (EmbedPDF.Tests), the parity
# self-test, the packaging unit tests; then the viewer rows release-dotnet.yml runs: an unstripped
# epdf-viewer-host release build, generate-bindings.sh, the EmbedPDF.Viewer.Tests and
# EmbedPDF.Models.Tests suites, and the C# API coverage check (HOST_TESTS=0 skips all of these).
# Then check-dotnet-engine.sh, epdf-capi release for the RID, pack.sh and validate.sh with no
# LD_LIBRARY_PATH or DYLD_LIBRARY_PATH, so the relocation check sees only what the package carries.
# Each step's exit code goes to <out>/summary.txt. On Linux, validate.sh needs EPDF_IMAGE_ID (the
# container image) and EPDF_HOST_OS; a macOS host needs neither. LOGS sets the log root,
# CARGO_TARGET_DIR the target dir, LIBPDFIUM the extracted runtime.
set -uo pipefail
C="$1"; LABEL="$2"; RID="$3"; ARCHIVE="$4"; MANIFEST="${5:-$C/packages/engine/runtime/engine-runtime-build.json}"
OUT=${LOGS:-/workspaces/logs/CU-C7}/dotnet-$LABEL-$RID; rm -rf "$OUT"; mkdir -p "$OUT"; R=$OUT/summary.txt; : > "$R"
case "$RID" in
  linux-arm64) TRIPLE=aarch64-unknown-linux-gnu LIBDIR=linux-arm64 ;;
  linux-x64) TRIPLE=x86_64-unknown-linux-gnu LIBDIR=linux-x64 ;;
  osx-arm64) TRIPLE=aarch64-apple-darwin LIBDIR=darwin-arm64 ;;
  osx-x64) TRIPLE=x86_64-apple-darwin LIBDIR=darwin-x64 ;;
  *) echo "unknown rid $RID"; exit 2 ;;
esac
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 CI=true
export CARGO_TARGET_DIR=${CARGO_TARGET_DIR:-/workspaces/target} CARGO_BUILD_JOBS=${CARGO_BUILD_JOBS:-4} CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
LIBPDFIUM=${LIBPDFIUM:-$C/packages/engine/runtime/build/libpdfium}
export PDF_RUNTIME_LIB_DIR=$LIBPDFIUM
case "$RID" in
  linux-*) export LD_LIBRARY_PATH=$LIBPDFIUM/$LIBDIR/lib ;;
  osx-*) export DYLD_LIBRARY_PATH=$LIBPDFIUM/$LIBDIR/lib ;;
esac
cd "$C" || exit 2
ln -sfn "$CARGO_TARGET_DIR" "$C/target" 2>/dev/null || true
echo "checkout $(git rev-parse HEAD) $(git status --short -uno | wc -l) dirty; rid $RID manifest $MANIFEST archive $ARCHIVE start $(date -u +%FT%TZ)" >> "$R"
echo "dotnet $(cd platforms/dotnet && dotnet --version) $(rustc --version)" >> "$R"
tests() { grep -E "^(Passed|Failed)!" "$1" | tail -3 >> "$R"; }
if [[ ${HOST_TESTS:-1} == 1 ]]; then
  cargo build --locked -p epdf-capi > "$OUT/capi-build.log" 2>&1; echo "capi build exit=$?" >> "$R"
  EPDF_NATIVE_DIR=$CARGO_TARGET_DIR/debug dotnet test platforms/dotnet/EmbedPDF.Tests/EmbedPDF.Tests.csproj > "$OUT/dotnet-test.log" 2>&1; echo "dotnet test EmbedPDF.Tests exit=$?" >> "$R"
  tests "$OUT/dotnet-test.log"
  (cd platforms/dotnet && dotnet run --project EmbedPDF.Parity -- self-test) > "$OUT/parity.log" 2>&1
  echo "parity self-test exit=$? passed=$(grep -c '"status":"passed"' "$OUT/parity.log")" >> "$R"
  python3 -m unittest discover -s platforms/dotnet/packaging -v > "$OUT/packaging-unittest.log" 2>&1
  echo "packaging unittest exit=$? $(tail -1 "$OUT/packaging-unittest.log")" >> "$R"
  cargo rustc -p epdf-viewer-host --release --locked --crate-type cdylib > "$OUT/host-build.log" 2>&1; echo "viewer host release build exit=$?" >> "$R"
  bash platforms/dotnet/generate-bindings.sh > "$OUT/generate-bindings.log" 2>&1; echo "generate-bindings exit=$?" >> "$R"
  dotnet test platforms/dotnet/EmbedPDF.Viewer.Tests/EmbedPDF.Viewer.Tests.csproj > "$OUT/viewer-test.log" 2>&1; echo "dotnet test EmbedPDF.Viewer.Tests exit=$?" >> "$R"
  tests "$OUT/viewer-test.log"
  dotnet test platforms/dotnet/EmbedPDF.Models.Tests/EmbedPDF.Models.Tests.csproj > "$OUT/models-test.log" 2>&1; echo "dotnet test EmbedPDF.Models.Tests exit=$?" >> "$R"
  tests "$OUT/models-test.log"
  python3 -m unittest scripts/test_check_api_coverage.py > "$OUT/api-coverage-unittest.log" 2>&1; echo "api coverage unittest exit=$? $(tail -1 "$OUT/api-coverage-unittest.log")" >> "$R"
  python3 scripts/check-api-coverage.py --platform csharp > "$OUT/api-coverage.log" 2>&1; echo "csharp api coverage exit=$? $(tail -1 "$OUT/api-coverage.log")" >> "$R"
fi
if [[ ${ENGINE_CHECK:-1} == 1 ]]; then
  bash platforms/dotnet/check-dotnet-engine.sh > "$OUT/check-dotnet-engine.log" 2>&1; echo "check-dotnet-engine exit=$? $(tail -1 "$OUT/check-dotnet-engine.log")" >> "$R"
fi
cargo build --locked -p epdf-capi --release --target $TRIPLE > "$OUT/capi-release.log" 2>&1; echo "capi release build exit=$?" >> "$R"
env -u LD_LIBRARY_PATH -u DYLD_LIBRARY_PATH bash platforms/dotnet/pack.sh --rid $RID --out "$OUT/pack" --runtime-manifest "$MANIFEST" \
  --runtime-archive "$ARCHIVE" > "$OUT/pack.log" 2>&1; echo "pack exit=$?" >> "$R"
env -u LD_LIBRARY_PATH -u DYLD_LIBRARY_PATH bash platforms/dotnet/validate.sh --rid $RID --runtime-manifest "$MANIFEST" --record "$OUT/validate-record.json" \
  --package "$OUT/pack/EmbedPDF.Engine.0.0.0-local.nupkg" --manifest "$OUT/pack/package-manifest.$RID.json" > "$OUT/validate.log" 2>&1
echo "validate exit=$? $(tail -1 "$OUT/validate.log")" >> "$R"
echo "end $(date -u +%FT%TZ)" >> "$R"; echo DOTNETDONE >> "$R"
