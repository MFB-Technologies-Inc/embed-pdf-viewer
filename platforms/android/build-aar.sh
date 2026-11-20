#!/usr/bin/env bash
# Builds the Android AAR and runs the smoke test on a 16 KB page-size emulator.
#
# `multiplatform-plan.md` Phase 1: an Android AAR with the ABIs, `libc++_shared`
# and the shrinker rules, plus the 16 KB alignment checks. Local only, published
# nowhere.
#
#   bash platforms/android/build-aar.sh          # build and check
#   bash platforms/android/build-aar.sh test     # also run on the emulator
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
# cargo writes the dev profile to target/<triple>/debug.
PROFILE="${PROFILE:-release}"
PROFILE_DIR="$([[ $PROFILE == dev ]] && echo debug || echo "$PROFILE")"

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
# and it has to match `minSdk` in library/build.gradle.kts or the .so will
# reference symbols the device does not have.
API=24

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

  if [[ ! -f "$ROOT/packages/engine/runtime/build/libpdfium/$fork/lib/libembedpdf.a" ]]; then
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
    cargo build -p epdf-uniffi --target "$rust" --profile "$PROFILE" --manifest-path "$ROOT/Cargo.toml"

  jni="$PROJECT/library/src/main/jniLibs/$abi"
  mkdir -p "$jni"
  cp "$ROOT/target/$rust/$PROFILE_DIR/libepdf_uniffi.so" "$jni/"
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
  missing="$("$TC/bin/llvm-nm" -D --undefined-only "$jni/libepdf_uniffi.so" | awk '/ U __arm_/ { print $2 }')"
  if [[ -n "$missing" ]]; then
    echo "libepdf_uniffi.so ($abi) leaves compiler-rt routines undefined: $missing" >&2
    exit 1
  fi
done

echo "generating bindings"
cargo build -p uniffi-bindgen --manifest-path "$ROOT/Cargo.toml"
rm -rf "$PROJECT/library/src/main/kotlin"
"$ROOT/target/debug/uniffi-bindgen" generate \
  --library "$ROOT/target/aarch64-linux-android/$PROFILE_DIR/libepdf_uniffi.so" \
  --language kotlin --no-format --out-dir "$PROJECT/library/src/main/kotlin"

# The smoke test reads the same fixtures the Rust and iOS tests read.
mkdir -p "$PROJECT/library/src/androidTest/assets"
for fixture in report colors encrypted-hello; do
  cp "$ROOT/pdfium-multiplatform-prototype/fixtures/$fixture.pdf" \
    "$PROJECT/library/src/androidTest/assets/"
done

cd "$PROJECT"
if [[ "${1:-}" == "test" ]]; then
  ./gradlew --console=plain :library:connectedDebugAndroidTest
else
  ./gradlew --console=plain :library:assembleRelease
fi

# The APK half of the alignment check. An ELF built with 16 KB LOAD segments
# still fails to map if the zip entry it sits at is not on a 16 KB boundary, so
# the two checks answer different questions and both are in Phase 1's scope.
# The test APK is the only one this project produces; an app packaging the AAR
# has to run the same check on its own output.
APK="$PROJECT/library/build/outputs/apk/androidTest/debug/library-debug-androidTest.apk"
ZIPALIGN="$(ls -d "${ANDROID_HOME:-$HOME/Library/Android/sdk}"/build-tools/* | sort -V | tail -1)/zipalign"
if [[ -f "$APK" && -x "$ZIPALIGN" ]]; then
  if "$ZIPALIGN" -c -P 16 4 "$APK" > /dev/null; then
    echo "  $(basename "$APK") is 16 KB page aligned"
  else
    echo "$APK is not 16 KB page aligned" >&2
    exit 1
  fi
fi

AAR="$PROJECT/library/build/outputs/aar/library-release.aar"
if [[ -f "$AAR" ]]; then
  echo
  echo "  $AAR"
  # Uncompressed and 16 KB aligned inside the archive. An APK packer will only
  # mmap a .so straight out of the APK when it is stored, not deflated, and at
  # a 16 KB boundary; the AAR is where that has to already be true.
  unzip -l "$AAR" | grep '\.so$' | sed 's/^/  /'
fi
