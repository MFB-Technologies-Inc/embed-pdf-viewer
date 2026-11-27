#!/usr/bin/env bash
# 2026-11-27 (CU-C8), from ../../../upstream-catchup-2026-11-18/scripts/cu-c8c/kotlin.sh.
# usage: kotlin.sh <checkout> <label> [rows]   in the linux/amd64 dev container (CU-C8-D3)
# The Kotlin rows: the Kotlin API coverage check and its unit tests, test-command-context.sh (the
# bindings JVM test and the checksum gate, with kotlinc 2.2.21), the JVM unit tests of every
# Gradle module and apiCheck, then build-aar.sh (release AAR, both ABIs, the 16 KB checks).
# ROWS picks rows: coverage, context, jvm, api, aar. T sets the cargo target dir. Each row's
# exit code goes to /workspaces/logs/CU-C8/kotlin-<label>/summary.txt; JUnit counts are summed
# from each module's test-results.
set -uo pipefail
C="$1"; LABEL="$2"; ROWS="${3:-coverage context jvm api aar}"
OUT=/workspaces/logs/CU-C8/kotlin-$LABEL; mkdir -p "$OUT"; R=$OUT/summary.txt
TOOLS=/workspaces/tools
export CARGO_TARGET_DIR=${T:-/workspaces/target-c8-x64} CARGO_BUILD_JOBS=${CARGO_BUILD_JOBS:-14} CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
export PDF_RUNTIME_LIB_DIR=$C/packages/engine/runtime/build/libpdfium
export JNA_JAR=$TOOLS/jars/jna-5.19.1.jar COROUTINES_JAR=$TOOLS/jars/kotlinx-coroutines-core-jvm-1.10.2.jar KOTLINC=$TOOLS/kotlinc/bin/kotlinc
cd "$C" || exit 2
echo "checkout $(git rev-parse HEAD) $(git status --short -uno | wc -l) dirty; $(uname -m); rows: $ROWS; start $(date -u +%FT%TZ)" >> "$R"
row() { local name=$1; shift; local t0=$SECONDS; "$@" > "$OUT/$name.log" 2>&1; local e=$?; echo "$name exit=$e secs=$((SECONDS - t0))" >> "$R"; return $e; }
junit() {
  python3 - "$C/platforms/android/embedpdf" "$1" >> "$R" <<'PY'
import glob, sys, xml.etree.ElementTree as ET
root, label = sys.argv[1], sys.argv[2]
counts = {}
for path in glob.glob(f"{root}/*/build/test-results/**/*.xml", recursive=True):
    module = path[len(root) + 1:].split("/")[0]
    suite = ET.parse(path).getroot()
    c = counts.setdefault(module, [0, 0, 0, 0])
    for i, k in enumerate(("tests", "failures", "errors", "skipped")):
        c[i] += int(suite.get(k, 0))
total = [sum(c[i] for c in counts.values()) for i in range(4)]
for module, c in sorted(counts.items()):
    print(f"  {label} {module}: tests={c[0]} failures={c[1]} errors={c[2]} skipped={c[3]}")
print(f"  {label} total: tests={total[0]} failures={total[1]} errors={total[2]} skipped={total[3]}")
PY
}
gradle() { (cd platforms/android/embedpdf && ./gradlew --console=plain --no-daemon "$@"); }
for r in $ROWS; do case $r in
  coverage)
    row coverage python3 scripts/check-api-coverage.py --platform kotlin; tail -1 "$OUT/coverage.log" >> "$R"
    row coverage-unittest python3 -m unittest discover -s platforms/android/tests -p 'test_*.py'; tail -1 "$OUT/coverage-unittest.log" >> "$R" ;;
  context)
    rm -rf platforms/android/embedpdf/bindings/build/test-results
    row context bash platforms/android/test-command-context.sh; junit context ;;
  jvm)
    rm -rf platforms/android/embedpdf/*/build/test-results
    row jvm gradle :bindings:testDebugUnitTest :embedpdf:testDebugUnitTest :models:testDebugUnitTest :compose:testDebugUnitTest :javascript:testDebugUnitTest :smoke:test --continue; junit jvm ;;
  api) row apicheck gradle apiCheck ;;
  aar)
    row aar bash platforms/android/build-aar.sh
    grep -E "16 KB|0x4000|aligned|zipalign|__arm_|sha256|PASS|FAIL" "$OUT/aar.log" | tail -12 | sed 's/^/  /' >> "$R" ;;
esac; done
echo "end $(date -u +%FT%TZ)" >> "$R"; echo KOTLINDONE >> "$R"
