#!/usr/bin/env bash
# 2026-11-27b (CU-C7). usage: x64-setup.sh <checkout> <android-slice-dir>
# In the linux/amd64 container (CU-C7-D3), as node. The Kotlin rows' inputs, none from Node:
# the linux-x64 runtime of engine-runtime-build.json, fetched with curl and checked against its
# sha256 (fetch-libpdfium.sh calls node); the Android slices from <android-slice-dir>, checked
# against engine-runtime-build.local.json (CU-C7-D4); kotlinc 2.2.21 and the two jars
# test-command-context.sh puts on the classpath, in /workspaces/tools-1127b.
set -euo pipefail
C=$1 SLICES=$2 TOOLS=/workspaces/tools-1127b
R=$C/packages/engine/runtime
lib=$R/build/libpdfium; mkdir -p "$lib" "$TOOLS/jars"
field() { python3 -c "import json,sys; print(json.load(open(sys.argv[1]))['artifacts'][sys.argv[2]][sys.argv[3]])" "$@"; }
unpack() { # <archive> <expected sha256> <target>
  local got; got=$(sha256sum "$1" | cut -d' ' -f1)
  [[ $got == "$2" ]] || { echo "sha256 mismatch for $1: $got, want $2"; exit 1; }
  rm -rf "$lib/$3"; mkdir -p "$lib/$3"; tar -xzf "$1" -C "$lib/$3"; echo "$3 ok $got"
}
if [[ ! -f $lib/linux-x64/lib/libembedpdf.so ]]; then
  curl -fsSL "$(field $R/engine-runtime-build.json linux-x64 url)" -o /tmp/linux-x64.tar.gz
  unpack /tmp/linux-x64.tar.gz "$(field $R/engine-runtime-build.json linux-x64 sha256)" linux-x64; rm -f /tmp/linux-x64.tar.gz
fi
for abi in arm64 x64; do
  unpack "$(ls $SLICES/libembedpdf-runtime-android-$abi-*.tar.gz)" "$(field $R/engine-runtime-build.local.json android-$abi sha256)" android-$abi
done
[[ -x $TOOLS/kotlinc/bin/kotlinc ]] || {
  curl -fsSL https://github.com/JetBrains/kotlin/releases/download/v2.2.21/kotlin-compiler-2.2.21.zip -o /tmp/kotlinc.zip
  unzip -q -o /tmp/kotlinc.zip -d $TOOLS; rm -f /tmp/kotlinc.zip; }
M=https://repo1.maven.org/maven2
[[ -f $TOOLS/jars/jna-5.19.1.jar ]] || curl -fsSL $M/net/java/dev/jna/jna/5.19.1/jna-5.19.1.jar -o $TOOLS/jars/jna-5.19.1.jar
[[ -f $TOOLS/jars/kotlinx-coroutines-core-jvm-1.10.2.jar ]] || curl -fsSL $M/org/jetbrains/kotlinx/kotlinx-coroutines-core-jvm/1.10.2/kotlinx-coroutines-core-jvm-1.10.2.jar -o $TOOLS/jars/kotlinx-coroutines-core-jvm-1.10.2.jar
sha256sum $TOOLS/jars/*.jar; $TOOLS/kotlinc/bin/kotlinc -version 2>&1 | tail -1
echo SETUPDONE
