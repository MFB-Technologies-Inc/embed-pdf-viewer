#!/usr/bin/env bash
# Builds the demo app the way an app consumes a release: the five libraries
# published to a Maven repository, then the demo resolved from it by
# coordinates, not as project dependencies, and minified by R8.
#
#   bash platforms/android/build-aar.sh          # first: the .so files and generated Kotlin
#   bash platforms/android/android-consumer.sh
#
# Checks, in order: the build publishes exactly the five artifacts in
# PRODUCTS below, each at one version, so a sixth publication or a missing one
# fails here and the README's product list changes with the build; each
# artifact's POM names a version for every dependency (the Compose ones come
# from the BOM through versionMapping); `embedpdf-bindings` carries its
# native-debug-symbols zip; the release APK packages the engine for both
# ABIs; `zipalign -c -P 16` passes on it; and R8 kept every `uniffi.` and
# `com.sun.jna.` class under its own name, since JNA reaches them by
# reflection.
#
# The repository is a directory of its own (CONSUMER_MAVEN_REPO, under the
# library build's `build/` by default), passed to Gradle as
# -Dmaven.repo.local, so the run leaves ~/.m2 alone. EMBEDPDF_VERSION sets
# the version the four non-engine artifacts publish as.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
LIBS="$ROOT/platforms/android/embedpdf"
DEMO="$ROOT/platforms/android/EmbedPDFDemo"
REPO="${CONSUMER_MAVEN_REPO:-$LIBS/build/consumer-maven}"
VERSION="${EMBEDPDF_VERSION:-0.0.0-consumer}"
# The artifacts the README documents, as ProductsTests pins the Swift package's
# products. `embedpdf-bindings` is versioned by the engine pin, the rest by
# EMBEDPDF_VERSION.
PRODUCTS=(embedpdf embedpdf-bindings embedpdf-compose embedpdf-javascript embedpdf-models)
GRADLE=("$LIBS/gradlew" --console=plain "-Dmaven.repo.local=$REPO" "-Pembedpdf.version=$VERSION")

for abi in arm64-v8a x86_64; do
  if [[ ! -f "$LIBS/bindings/src/main/jniLibs/$abi/libepdf_viewer_host.so" ]]; then
    echo "no $abi engine in bindings/src/main/jniLibs; run platforms/android/build-aar.sh first" >&2
    exit 1
  fi
done

rm -rf "$REPO"
echo "publishing to $REPO"
"${GRADLE[@]}" -p "$LIBS" publishToMavenLocal

engine="$(sed -n 's/^embedpdf\.engineRelease=android-engine-\(.*\)$/\1/p' "$LIBS/gradle.properties")"
# What the build published, read from the repository rather than listed:
# one `<artifact>/<version>` directory per publication.
published="$(cd "$REPO/com/embedpdf" && find . -mindepth 2 -maxdepth 2 -type d | sed 's#^\./##' | LC_ALL=C sort)"
names="$(cut -d/ -f1 <<<"$published" | LC_ALL=C sort)"
if [[ "$names" != "$(printf '%s\n' "${PRODUCTS[@]}" | LC_ALL=C sort)" ]]; then
  echo "the build published $(tr '\n' ' ' <<<"$names")but the products are ${PRODUCTS[*]}" >&2
  echo "a publication was added or removed; change PRODUCTS, the README's product list and the release workflow together" >&2
  exit 1
fi
for artifact in $published; do
  name="${artifact%%/*}"
  version="${artifact#*/}"
  expected="$VERSION"
  [[ $name == embedpdf-bindings ]] && expected="0.0.0-engine.$engine"
  [[ "$version" == "$expected" ]] || { echo "$name published as $version, not $expected" >&2; exit 1; }
  dir="$REPO/com/embedpdf/$name/$version"
  pom="$dir/$name-$version.pom"
  [[ -f "$pom" && -f "$dir/$name-$version.aar" && -f "$dir/$name-$version.module" ]] || {
    echo "missing $name $version (POM, AAR or module metadata) in $dir" >&2
    exit 1
  }
  if [[ $name == embedpdf-bindings && ! -f "$dir/$name-$version-native-debug-symbols.zip" ]]; then
    echo "missing $name $version native-debug-symbols.zip in $dir" >&2
    exit 1
  fi
  # A dependency with no version is one a Maven consumer cannot resolve.
  missing="$(python3 - "$pom" <<'PYTHON'
import sys
import xml.etree.ElementTree as ET
ns = {"m": "http://maven.apache.org/POM/4.0.0"}
root = ET.parse(sys.argv[1]).getroot()
for dep in root.findall("m:dependencies/m:dependency", ns):
    if not (dep.findtext("m:version", default="", namespaces=ns) or "").strip():
        print(dep.findtext("m:groupId", namespaces=ns) + ":" + dep.findtext("m:artifactId", namespaces=ns))
PYTHON
)"
  if [[ -n "$missing" ]]; then
    echo "$pom has dependencies without a version: $missing" >&2
    exit 1
  fi
  echo "  $name $version"
done

echo "assembling the minified demo from $REPO"
"${GRADLE[@]}" -p "$DEMO" -Pembedpdf.fromMavenLocal :app:assembleRelease

APK="$DEMO/app/build/outputs/apk/release/app-release.apk"
[[ -f "$APK" ]] || { echo "no release APK at $APK" >&2; exit 1; }
echo "  $(basename "$APK"): $(stat -c %s "$APK") bytes"

for abi in arm64-v8a x86_64; do
  entry="$(unzip -v "$APK" "lib/$abi/libepdf_viewer_host.so" | awk '/libepdf_viewer_host/ { print $2 }')"
  # `Stored`: deflated, the library could not be mapped straight out of the APK.
  [[ "$entry" == Stored ]] || { echo "lib/$abi/libepdf_viewer_host.so is ${entry:-missing}, not stored" >&2; exit 1; }
done

ZIPALIGN="$(ls -d "${ANDROID_HOME:-$HOME/Library/Android/sdk}"/build-tools/* | sort -V | tail -1)/zipalign"
[[ -x "$ZIPALIGN" ]] || { echo "no zipalign at $ZIPALIGN; set ANDROID_HOME" >&2; exit 1; }
"$ZIPALIGN" -c -P 16 4 "$APK" > /dev/null || { echo "$APK is not 16 KB page aligned" >&2; exit 1; }
echo "  16 KB page aligned"

# R8: usage.txt lists what it removed, a whole class as its name and a class
# with removed members as its name and a colon; mapping.txt lists every kept
# class as `original -> obfuscated:`. A kept JNA or uniffi class must be in
# neither form in usage.txt and must keep its name.
OUT="$DEMO/app/build/outputs/mapping/release"
USAGE="$OUT/usage.txt"
MAPPING="$OUT/mapping.txt"
[[ -f "$USAGE" && -f "$MAPPING" ]] || { echo "no R8 usage.txt or mapping.txt in $OUT" >&2; exit 1; }
# R8 also lists two kinds of member that are not losses: the lambdas it
# synthesizes itself ($r8$lambda$...) and then drops, and a static
# initializer it found free of side effects (JNA's Pointer only assigns
# constants). Anything else under these packages is.
removed="$(python3 - "$USAGE" <<'PYTHON'
import re
import sys

owner = None
for line in open(sys.argv[1]):
    line = line.rstrip("\n")
    if not line.startswith((" ", "\t")):
        name = line.rstrip(":")
        owner = name if re.match(r"(uniffi|com\.sun\.jna)\.", name) else None
        if owner and not line.endswith(":"):
            print(owner)
        continue
    member = line.strip()
    if owner and "$r8$" not in member and member != "static void <clinit>()":
        print(owner + ": " + member)
PYTHON
)"
if [[ -n "$removed" ]]; then
  echo "R8 removed JNA or uniffi classes or members:" >&2
  echo "$removed" | head -20 >&2
  exit 1
fi
# R8's own lambda classes ($$ExternalSyntheticLambda) were never in the input and may be renamed.
renamed="$(grep -E '^(uniffi|com\.sun\.jna)\.[^ ]* -> ' "$MAPPING" | grep -v 'ExternalSynthetic' | awk '$1 ":" != $3' || true)"
if [[ -n "$renamed" ]]; then
  echo "R8 renamed JNA or uniffi classes:" >&2
  echo "$renamed" | head -20 >&2
  exit 1
fi

# Every class the published bindings and JNA hold is in the mapping.
classes() {
  local aar="$1" prefix="$2" tmp
  tmp="$(mktemp -d)"
  unzip -q -o "$aar" classes.jar -d "$tmp"
  unzip -Z1 "$tmp/classes.jar" | grep -E "^$prefix.*\.class$" | sed 's/\.class$//; s#/#.#g' | sort
  rm -rf "$tmp"
}
kept="$(grep -E '^(uniffi|com\.sun\.jna)\.[^ ]* -> ' "$MAPPING" | awk '{ print $1 }' | sort)"
BINDINGS_AAR="$REPO/com/embedpdf/embedpdf-bindings/0.0.0-engine.$engine/embedpdf-bindings-0.0.0-engine.$engine.aar"
JNA_AAR="$(find "${GRADLE_USER_HOME:-$HOME/.gradle}/caches/modules-2/files-2.1/net.java.dev.jna/jna" -name 'jna-*.aar' | sort -V | tail -1)"
[[ -f "$JNA_AAR" ]] || { echo "no JNA AAR in the Gradle cache" >&2; exit 1; }
for source in "$BINDINGS_AAR:uniffi/" "$JNA_AAR:com/sun/jna/"; do
  aar="${source%:*}"
  prefix="${source##*:}"
  expected="$(classes "$aar" "$prefix")"
  lost="$(comm -23 <(echo "$expected") <(echo "$kept"))"
  if [[ -n "$lost" ]]; then
    echo "classes in $(basename "$aar") that R8 did not keep:" >&2
    echo "$lost" | head -20 >&2
    exit 1
  fi
  echo "  R8 kept all $(echo "$expected" | wc -l) ${prefix%/} classes of $(basename "$aar") under their own names"
done
