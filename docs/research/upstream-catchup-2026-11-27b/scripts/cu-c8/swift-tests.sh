#!/usr/bin/env bash
# CU-C8 (2026-11-27b), copied from CU-C9 (2026-11-27): the three Swift test runs publish-engine-payload.mjs makes, without
# publishing: the iOS simulator in place, then macOS arm64 and x86_64 through a
# private consumer manifest staged in a
# temporary directory by stage-mac-consumer.py (macos-consumer.mjs without Node).
#
#   swift-tests.sh <checkout> <log dir> <engine: local|download> [routes address]
#
# `local` reads EmbedPDF/EmbedPDFBindings.xcframework (package-engine-framework.sh);
# `download` uses the release Package.swift pins. A routes address (from
# platforms/android/tests/signature-routes.mjs) reaches the tests as
# EMBEDPDF_SIGNATURE_ROUTES. EMBEDPDF_TEST_DESTINATION overrides the simulator.
set -uo pipefail
CHECKOUT="$(cd "$1" && pwd)"
LOGS="$2"
ENGINE="$3"
ROUTES="${4:-}"
DESTINATION="${EMBEDPDF_TEST_DESTINATION:?set EMBEDPDF_TEST_DESTINATION to the simulator, by id}"
mkdir -p "$LOGS"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/cu-c8-swift.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT

ENV=(env -u EMBEDPDF_LOCAL_ENGINE -u EMBEDPDF_EXPERIMENTAL_BRIDGE -u PDF_RUNTIME_LIB_DIR
  -u EMBEDPDF_FORK_TARGET -u LIB_ROOT -u DYLD_LIBRARY_PATH -u DYLD_FALLBACK_LIBRARY_PATH -u EMBEDPDF_PREBUILT_ENGINE)
[[ "$ENGINE" == local ]] && ENV+=(EMBEDPDF_PREBUILT_ENGINE=local)
[[ -n "$ROUTES" ]] && ENV+=(TEST_RUNNER_EMBEDPDF_SIGNATURE_ROUTES="$ROUTES")

run() {
  local name="$1" dir="$2" destination="$3"
  shift 3
  echo "== $name: $destination"
  (cd "$dir" && "${ENV[@]}" xcodebuild test -scheme EmbedPDF-Package -destination "$destination" \
    -derivedDataPath "$WORK/derived-$name" "$@") > "$LOGS/$name.log" 2>&1
  local status=$?
  echo "$name exit $status" | tee -a "$LOGS/status.txt"
  grep -E '^(Test run with|✘|✔ Test run|\*\* TEST)' "$LOGS/$name.log" | tail -5
  grep -E 'Executed [0-9]+ tests' "$LOGS/$name.log" | tail -2
}

: > "$LOGS/status.txt"
run ios "$CHECKOUT/platforms/apple/EmbedPDF" "$DESTINATION"
python3 -I "$(dirname "$0")/stage-mac-consumer.py" "$CHECKOUT/platforms/apple/EmbedPDF" "$WORK/EmbedPDF"
for arch in arm64 x86_64; do
  run "macos-$arch" "$WORK/EmbedPDF" "platform=macOS,arch=$arch" -jobs 2
done
