#!/usr/bin/env bash
# The canonical identity of the prebuilt Android engine (the `bindings` AAR):
# a hash over every input that determines its contents. Prints 16 hex chars.
#
# The engine release is named `android-engine-<hash>`, as the Apple one is
# `apple-engine-<hash>`, and check-android-engine.sh compares it with the pin
# in platforms/android/embedpdf/gradle.properties.
#
# The AAR links the same Rust as the Apple engine, so the crate list is read
# from APPLE_CRATES in platforms/apple/apple-engine-hash.sh rather than kept
# twice; the publish script checks that list against `cargo metadata`. Tests,
# benches, examples and docs don't reach the library and are left out. A
# listed crate with no files is a path that doesn't exist, and fails rather
# than hashing nothing.
set -euo pipefail

HASH_LENGTH=16
case "$#:${1:-}" in
  0:) ;;
  1:--full) HASH_LENGTH=64 ;;
  *) echo "usage: $0 [--full]" >&2; exit 2 ;;
esac

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

sha256_file() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print $1}'
  else
    shasum -a 256 "$1" | awk '{print $1}'
  fi
}

crates_line="$(grep -E '^APPLE_CRATES=\(' "$REPO_ROOT/platforms/apple/apple-engine-hash.sh")" || {
  echo "no APPLE_CRATES line in platforms/apple/apple-engine-hash.sh" >&2
  exit 1
}
crates_line="${crates_line#APPLE_CRATES=(}"
read -r -a CRATES <<< "${crates_line%)}"

INPUTS=(
  "Cargo.toml"
  "Cargo.lock"
  "rust-toolchain.toml"
  # How the slices are linked, checked and packaged.
  "platforms/android/build-aar.sh"
  "platforms/android/fetch-android-runtime.sh"
  "platforms/android/embedpdf/gradle/libs.versions.toml"
  "platforms/android/embedpdf/bindings/build.gradle.kts"
  "platforms/android/embedpdf/bindings/consumer-rules.pro"
  "platforms/android/embedpdf/bindings/src/main/AndroidManifest.xml"
)
for crate in "${CRATES[@]}"; do
  count=0
  while IFS= read -r rel; do
    INPUTS+=("$rel")
    count=$((count + 1))
  done < <(git -C "$REPO_ROOT" ls-files "crates/$crate" |
    grep -v -E "^crates/$crate/(tests|benches|examples)/|\.md$" | LC_ALL=C sort)
  if ((count == 0)); then
    echo "APPLE_CRATES lists $crate, but crates/$crate has no tracked files" >&2
    exit 1
  fi
done
# uniffi-bindgen generates the Kotlin the AAR compiles. It isn't linked, so it
# isn't in the crate list.
while IFS= read -r rel; do
  INPUTS+=("$rel")
done < <(git -C "$REPO_ROOT" ls-files crates/uniffi-bindgen | grep -v '\.md$' | LC_ALL=C sort)

ACC=""
for rel in "${INPUTS[@]}"; do
  f="$REPO_ROOT/$rel"
  if [[ -f "$f" ]]; then
    ACC+="$rel:$(sha256_file "$f")\n"
  else
    # Missing inputs contribute deterministically (mirrors hashFiles semantics).
    ACC+="$rel:-\n"
  fi
done

# The AAR links the Android runtime slices published for runtime-src's commit.
ACC+="runtime-src:$(git -C "$REPO_ROOT" ls-files -s packages/engine/runtime/runtime-src | awk '{print $2}')\n"

if command -v sha256sum >/dev/null 2>&1; then
  printf '%b' "$ACC" | sha256sum | awk -v hash_length="$HASH_LENGTH" '{print substr($1, 1, hash_length)}'
else
  printf '%b' "$ACC" | shasum -a 256 | awk -v hash_length="$HASH_LENGTH" '{print substr($1, 1, hash_length)}'
fi
