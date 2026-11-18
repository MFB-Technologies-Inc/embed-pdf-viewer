#!/usr/bin/env bash
# Runs the signature transport and the catch-up surfaces (tests/SignatureTest.kt)
# against the full native viewer host on a desktop JVM. The timestamp and
# revocation answers come from upstream's routes, served by
# tests/signature-routes.mjs on a loopback port.
#
# Same environment as test-command-context.sh (KOTLINC, JNA_JAR,
# COROUTINES_JAR, the PDFium runtime), plus node and a built
# packages/core/signature/dist. An optional first argument selects an
# already-built full-host .so.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
: "${JNA_JAR:?Set JNA_JAR to jna-5.14.0.jar}"
: "${COROUTINES_JAR:?Set COROUTINES_JAR to kotlinx-coroutines-core-jvm-1.10.2.jar}"
KOTLINC="${KOTLINC:-kotlinc}"
export JAVA_OPTS="${JAVA_OPTS:--Xmx2g -XX:ActiveProcessorCount=2}"
[[ -f packages/core/signature/dist/server.js ]] || {
  echo "build packages/core/signature first: pnpm --filter @embedpdf/core-signature build" >&2
  exit 1
}
TARGET_DIR="$(cargo metadata --format-version 1 --no-deps | python3 -c "import json,sys; print(json.load(sys.stdin)[\"target_directory\"])")"
LIBRARY="${1:-$TARGET_DIR/debug/libepdf_viewer_host.so}"
OUT="$TARGET_DIR/signature-kotlin"
rm -rf "$OUT/generated" "$OUT/routes.out"
mkdir -p "$OUT/generated"
if [[ $# == 0 ]]; then
  cargo build -p epdf-viewer-host -p uniffi-bindgen
fi
"$TARGET_DIR/debug/uniffi-bindgen" generate --no-format --library "$LIBRARY" \
  --language kotlin --out-dir "$OUT/generated"
mapfile -t SOURCES < <(rg --files "$OUT/generated" -g "*.kt" | sort)
CLASSPATH="$JNA_JAR:$COROUTINES_JAR"
"$KOTLINC" "${SOURCES[@]}" platforms/android/viewer/HttpSignatureTransport.kt \
  platforms/android/tests/SignatureTest.kt \
  -classpath "$CLASSPATH" -include-runtime -d "$OUT/signature-tests.jar"

# The routes helper stops when its standard input closes, which is when this
# script exits, however it exits.
exec 3> >(node platforms/android/tests/signature-routes.mjs "$ROOT" > "$OUT/routes.out" 2>&1)
for _ in $(seq 100); do
  PORT="$(awk '/^READY/ { print $2 }' "$OUT/routes.out" 2>/dev/null || true)"
  [[ -n "$PORT" ]] && break
  sleep 0.1
done
[[ -n "${PORT:-}" ]] || { cat "$OUT/routes.out" >&2; echo "the routes helper did not start" >&2; exit 1; }
java -Djna.library.path="$(dirname "$LIBRARY")" \
  -cp "$OUT/signature-tests.jar:$CLASSPATH" com.embedpdf.tests.SignatureTestKt "$PORT"
