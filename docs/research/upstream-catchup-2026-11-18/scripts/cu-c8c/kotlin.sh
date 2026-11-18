#!/usr/bin/env bash
# usage: kotlin.sh <checkout> <label> [script]  runs a Kotlin JVM check against the full viewer host.
set -uo pipefail
C="$1"; LABEL="$2"; SCRIPT="${3:-platforms/android/test-command-context.sh}"
OUT=/workspaces/logs/CU-C8c/$LABEL; mkdir -p "$OUT"
T=/workspaces/tools
export JAVA_HOME=$T/jdk PATH=$T/jdk/bin:$T/kotlinc/bin:$PATH
export CARGO_TARGET_DIR=/workspaces/target CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
LIBPDFIUM=$C/packages/engine/runtime/build/libpdfium
export PDF_RUNTIME_LIB_DIR=$LIBPDFIUM LD_LIBRARY_PATH=$LIBPDFIUM/linux-arm64/lib:$CARGO_TARGET_DIR/debug
export JNA_JAR=$T/jars/jna-5.14.0.jar COROUTINES_JAR=$T/jars/kotlinx-coroutines-core-jvm-1.10.2.jar KOTLINC=$T/kotlinc/bin/kotlinc
cd "$C" || exit 2
echo "checkout $(git rev-parse HEAD) $(git status --short | wc -l) dirty; start $(date -u +%FT%TZ)" > $OUT/summary.txt
bash "$SCRIPT" > $OUT/run.log 2>&1; rc=$?
echo "$SCRIPT exit=$rc end $(date -u +%FT%TZ)" >> $OUT/summary.txt
exit $rc
