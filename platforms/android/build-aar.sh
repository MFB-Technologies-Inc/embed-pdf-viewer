#!/usr/bin/env bash
# Builds the `bindings` AAR from `epdf-viewer-host`, the full viewer host with
# every plugin, and runs the 16 KB alignment checks.
#
# The AAR carries both ABIs, `libc++_shared`, the generated Kotlin for every
# uniffi component and the shrinker rules. Local only, published nowhere.
#
#   bash platforms/android/build-aar.sh          # build and check
#   bash platforms/android/build-aar.sh test     # also run the JVM tests
#
# There is no emulator in the dev container, so `test` runs the JVM tests
# against the host library for this machine instead of a device run.
#
# The `libembedpdf.a` slices come from the runtime release for the commit
# runtime-src pins; fetch-android-runtime.sh downloads and verifies them when
# one is missing. They cannot be built on macOS: the fork's Android config
# asserts `host_os == "linux"`, so a macOS build fails at `gn gen` before it
# compiles anything.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJECT="$ROOT/platforms/android/embedpdf"
# `release` is what ships. `PROFILE=dev` builds far faster for iterating;
# cargo writes the dev profile to <target>/<triple>/debug.
PROFILE="${PROFILE:-release}"
PROFILE_DIR="$([[ $PROFILE == dev ]] && echo debug || echo "$PROFILE")"
# Where cargo writes, as cargo reads it: CARGO_TARGET_DIR, relative to the
# workspace when it is relative, or the workspace's `target`. The Gradle
# tasks resolve it the same way.
TARGET_DIR="${CARGO_TARGET_DIR:-target}"
[[ "$TARGET_DIR" == /* ]] || TARGET_DIR="$ROOT/$TARGET_DIR"

# The dev container installs NDK r29 at /opt/android-ndk-r29; a macOS SDK
# install keeps it under the SDK's ndk/ directory.
if [[ -z "${ANDROID_NDK_HOME:-}" && -d /opt/android-ndk-r29 ]]; then
  ANDROID_NDK_HOME=/opt/android-ndk-r29
fi
NDK="${ANDROID_NDK_HOME:-$HOME/Library/Android/sdk/ndk/29.0.14206865}"
[[ -d "$NDK" ]] || { echo "no NDK at $NDK; set ANDROID_NDK_HOME" >&2; exit 1; }
HOST_TAG="$(ls "$NDK/toolchains/llvm/prebuilt" | head -1)"
TC="$NDK/toolchains/llvm/prebuilt/$HOST_TAG"

# The floor the AAR declares. It has to be one the NDK ships a linker stub for,
# and it has to match `minSdk` in bindings/build.gradle.kts or the .so will
# reference symbols the device does not have.
API=24

# Where fetch-android-runtime.sh unpacks the slices and the Rust build reads
# them; both honour PDF_RUNTIME_LIB_DIR.
RUNTIME_LIB_DIR="${PDF_RUNTIME_LIB_DIR:-$ROOT/packages/engine/runtime/build/libpdfium}"

# Rust target -> Android ABI directory name -> NDK triple.
SLICES=(
  "aarch64-linux-android:arm64-v8a:aarch64-linux-android"
  "x86_64-linux-android:x86_64:x86_64-linux-android"
)

export ANDROID_NDK_HOME="$NDK"
for slice in "${SLICES[@]}"; do
  rust="${slice%%:*}"
  rest="${slice#*:}"
  abi="${rest%%:*}"
  triple="${rest##*:}"
  fork="android-$([[ $abi == arm64-v8a ]] && echo arm64 || echo x64)"

  if [[ ! -f "$RUNTIME_LIB_DIR/$fork/lib/libembedpdf.a" ]]; then
    bash "$ROOT/platforms/android/fetch-android-runtime.sh" "$fork"
  fi

  echo "building $rust"
  # Rust picks the linker from this per-target variable. Without it cargo calls
  # the host `cc`, which is Apple clang and rejects every GNU ld flag rustc
  # passes.
  linker="CARGO_TARGET_$(echo "$rust" | tr 'a-z-' 'A-Z_')_LINKER"
  # The fork's arm64 libyuv has SME rows that call compiler-rt's SME ABI
  # routines (`__arm_get_current_vg`, `__arm_tpidr2_save`). rustc links with
  # -nodefaultlibs, so clang's builtins never come in and the .so leaves them
  # undefined; dlopen on the device then fails. The NDK's builtins archive
  # has them, and only the members that answer an undefined symbol are taken.
  builtins="$(ls "$TC"/lib/clang/*/lib/linux/libclang_rt.builtins-"${triple%%-*}"-android.a | head -1)"
  rustflags="CARGO_TARGET_$(echo "$rust" | tr 'a-z-' 'A-Z_')_RUSTFLAGS"
  env "$linker=$TC/bin/$triple$API-clang" "$rustflags=-C link-arg=$builtins" \
    cargo build -p epdf-viewer-host --target "$rust" --profile "$PROFILE" --manifest-path "$ROOT/Cargo.toml"

  jni="$PROJECT/bindings/src/main/jniLibs/$abi"
  rm -rf "$jni"
  mkdir -p "$jni"
  cp "$TARGET_DIR/$rust/$PROFILE_DIR/libepdf_viewer_host.so" "$jni/"
  # The NDK's libc++, which the .so has a NEEDED entry for. Android does not
  # ship one; an AAR that leaves it out loads on a device that happens to have
  # another copy and fails on one that does not.
  cp "$TC/sysroot/usr/lib/$triple/libc++_shared.so" "$jni/"

  # 16 KB page size. Android 15 devices can use a 16 KB page, and a .so whose
  # LOAD segments are 4 KB aligned will not map there. NDK r28 and later align
  # to 16 KB by default, which is exactly the kind of default that changes back
  # under you, so it is checked rather than assumed.
  for so in "$jni"/*.so; do
    align="$("$TC/bin/llvm-readelf" -l "$so" |
      awk '/LOAD/ { getline; print $NF }' | sort -u | grep -v '^0x8$' | head -1)"
    if [[ "$align" != "0x4000" && "$align" != "0x10000" ]]; then
      echo "$so has LOAD alignment $align, which is below the 16 KB floor" >&2
      exit 1
    fi
    echo "  $(basename "$so") LOAD alignment $align"
  done
  # Neither bionic nor libc++_shared exports a compiler-rt routine, so one
  # left undefined is a library that will not load.
  missing="$("$TC/bin/llvm-nm" -D --undefined-only "$jni/libepdf_viewer_host.so" | awk '/ U __arm_/ { print $2 }')"
  if [[ -n "$missing" ]]; then
    echo "libepdf_viewer_host.so ($abi) leaves compiler-rt routines undefined: $missing" >&2
    exit 1
  fi
done

# One Kotlin file per uniffi component (the host, epdf-ffi and each plugin),
# all naming the one library they load. The repository's generator applies
# the fixes the stock one lacks (see crates/uniffi-bindgen/src/kotlin.rs).
echo "generating bindings"
cargo build -p uniffi-bindgen --manifest-path "$ROOT/Cargo.toml"
rm -rf "$PROJECT/bindings/src/main/kotlin"
"$TARGET_DIR/debug/uniffi-bindgen" generate \
  --library "$TARGET_DIR/aarch64-linux-android/$PROFILE_DIR/libepdf_viewer_host.so" \
  --language kotlin --no-format --out-dir "$PROJECT/bindings/src/main/kotlin"

cd "$PROJECT"
# The debug test APK is built only to give the zipalign check below an APK.
./gradlew --console=plain :bindings:assembleRelease :bindings:assembleDebugAndroidTest :embedpdf:assembleRelease
# The facade's public surface has to match embedpdf/api/embedpdf.api, and
# every Swift capability member has to be ported or named pending by phase.
./gradlew --console=plain :embedpdf:apiCheck
python3 "$ROOT/scripts/check-api-coverage.py" --platform kotlin
if [[ "${1:-}" == "test" ]]; then
  ./gradlew --console=plain :bindings:testDebugUnitTest :embedpdf:testDebugUnitTest :smoke:test
fi

# The APK half of the alignment check. An ELF built with 16 KB LOAD segments
# still fails to map if the zip entry it sits at is not on a 16 KB boundary, so
# the two checks answer different questions and both are in Phase 1's scope.
# The test APK is the only one this project produces; an app packaging the AAR
# has to run the same check on its own output.
APK="$PROJECT/bindings/build/outputs/apk/androidTest/debug/bindings-debug-androidTest.apk"
ZIPALIGN="$(ls -d "${ANDROID_HOME:-$HOME/Library/Android/sdk}"/build-tools/* | sort -V | tail -1)/zipalign"
[[ -f "$APK" ]] || { echo "no test APK at $APK to check" >&2; exit 1; }
[[ -x "$ZIPALIGN" ]] || { echo "no zipalign at $ZIPALIGN; set ANDROID_HOME" >&2; exit 1; }
if unzip -l "$APK" | grep -q 'libepdf_viewer_host\.so$'; then
  if "$ZIPALIGN" -c -P 16 4 "$APK" > /dev/null; then
    echo "  $(basename "$APK") is 16 KB page aligned"
  else
    echo "$APK is not 16 KB page aligned" >&2
    exit 1
  fi
else
  echo "$APK does not package libepdf_viewer_host.so, so its alignment proves nothing" >&2
  exit 1
fi

AAR="$PROJECT/bindings/build/outputs/aar/bindings-release.aar"
if [[ -f "$AAR" ]]; then
  echo
  echo "  $AAR"
  # Uncompressed and 16 KB aligned inside the archive. An APK packer will only
  # mmap a .so straight out of the APK when it is stored, not deflated, and at
  # a 16 KB boundary; the AAR is where that has to already be true.
  unzip -l "$AAR" | grep '\.so$' | sed 's/^/  /'
fi
