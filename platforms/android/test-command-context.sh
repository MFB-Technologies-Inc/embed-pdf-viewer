#!/usr/bin/env bash
# Executes Kotlin command callbacks against the full native viewer host.
#
# The callbacks run as the Gradle test com.embedpdf.bindings.CommandContextTest
# in the `bindings` module, against Kotlin generated from the same library the
# test loads. The checksum gate is checked separately below with kotlinc,
# since it needs a deliberately corrupted copy of one generated file.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
: "${JNA_JAR:?Set JNA_JAR to jna-5.14.0.jar}"
: "${COROUTINES_JAR:?Set COROUTINES_JAR to kotlinx-coroutines-core-jvm-1.10.2.jar}"
KOTLINC="${KOTLINC:-kotlinc}"
# The full viewer bindings exceed the compiler launcher default heap.
export JAVA_OPTS="${JAVA_OPTS:--Xmx2g -XX:ActiveProcessorCount=2}"
TARGET_DIR="$(cargo metadata --format-version 1 --no-deps | python3 -c "import json,sys; print(json.load(sys.stdin)[\"target_directory\"])")"
LIBRARY="${1:-$TARGET_DIR/debug/libepdf_viewer_host.so}"
OUT="$TARGET_DIR/command-context-kotlin"
BINDINGS="$ROOT/platforms/android/embedpdf/bindings"
mkdir -p "$OUT/generated"
cargo build -p uniffi-bindgen
if [[ $# == 0 ]]; then
  cargo build -p epdf-viewer-host
fi
"$TARGET_DIR/debug/uniffi-bindgen" generate --no-format --library "$LIBRARY" \
  --language kotlin --out-dir "$OUT/generated"

# The module compiles the generated sources build-aar.sh writes; these are the
# same components, generated from the library under test.
rm -rf "$BINDINGS/src/main/kotlin"
cp -R "$OUT/generated" "$BINDINGS/src/main/kotlin"
(cd "$ROOT/platforms/android/embedpdf" &&
  ./gradlew --console=plain :bindings:testDebugUnitTest \
    --tests com.embedpdf.bindings.CommandContextTest \
    -PlinuxHostDir="$(dirname "$LIBRARY")")

# Corrupt one expected value in a separate copy; the generated source above
# stays intact, and the native integrity gate must reject this test module.
CLASSPATH="$JNA_JAR:$COROUTINES_JAR"
NEGATIVE="$OUT/checksum-negative"
mkdir -p "$NEGATIVE"
python3 - "$OUT" <<'PYTHON'
from pathlib import Path
import re
import sys
root = Path(sys.argv[1])
source = (root / "generated/uniffi/epdf_ffi/epdf_ffi.kt").read_text()
pattern = r"(if \(lib\.uniffi_epdf_ffi_checksum_method_canceltoken_cancel\(\)[^\n]* != )(\d+)(\))"
corrupt, count = re.subn(pattern, lambda match: match.group(1) + str(int(match.group(2)) ^ 1) + match.group(3), source)
if count != 1:
    raise RuntimeError(f"Expected one cancel checksum assertion, found {count}")
(root / "checksum-negative/epdf_ffi.kt").write_text(corrupt)
PYTHON
"$KOTLINC" "$NEGATIVE/epdf_ffi.kt" platforms/android/tests/ChecksumMismatchTest.kt \
  -classpath "$CLASSPATH" -include-runtime -d "$NEGATIVE/test.jar"
java -XX:ActiveProcessorCount=2 -Djna.library.path="$(dirname "$LIBRARY")" \
  -cp "$NEGATIVE/test.jar:$CLASSPATH" com.embedpdf.tests.ChecksumMismatchTestKt
