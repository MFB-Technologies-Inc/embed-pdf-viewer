#!/usr/bin/env bash
# The identity of the EmbedPDF.Runtime package: a hash over every input that
# determines its native libraries. Prints 16 hex chars, or 64 with --full.
#
# EmbedPDF.Runtime is versioned 0.0.0-engine.<hash>, so facade and model work
# that leaves these inputs alone never needs a new runtime package. The
# algorithm is platforms/apple/apple-engine-hash.sh's; the inputs differ.
#
# DOTNET_CRATES is every workspace crate epdf-viewer-host links (normal and
# build dependencies, any target), as its directory under crates/.
# dotnet-engine-hash.test.sh checks it against `cargo tree`, so a new
# dependency fails there rather than going unhashed. Tests, benches, examples
# and docs don't reach the library and are left out. The C# generator is not
# an input: it shapes the facade, not the native library.
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

DOTNET_CRATES=(build-support epdf-engine epdf-ffi epdf-kernel epdf-uniffi epdf-viewer-host pdfium pdfium-sys plugins/acrojs plugins/actions plugins/annotation plugins/annotation-core plugins/attachment plugins/commands plugins/fonts plugins/form plugins/form-core plugins/geometry plugins/history plugins/i18n plugins/interaction plugins/link plugins/measurement plugins/metadata plugins/outline plugins/page-edit plugins/redaction plugins/render plugins/search plugins/selection plugins/shell plugins/signature plugins/signature-core plugins/stage plugins/stage-core plugins/stamp plugins/text-core plugins/ui-core plugins/view-manager)
INPUTS=(
  "Cargo.toml"
  "Cargo.lock"
  "rust-toolchain.toml"
  "platforms/dotnet/build-viewer-host.sh"
  "platforms/dotnet/packaging/package.py"
  "platforms/dotnet/EmbedPDF.Runtime/EmbedPDF.Runtime.csproj"
)
for crate in "${DOTNET_CRATES[@]}"; do
  count=0
  while IFS= read -r rel; do
    INPUTS+=("$rel")
    count=$((count + 1))
  done < <(git -C "$REPO_ROOT" ls-files "crates/$crate" |
    grep -v -E "^crates/$crate/(tests|benches|examples)/|\.md$" | LC_ALL=C sort)
  if ((count == 0)); then
    echo "DOTNET_CRATES lists $crate, but crates/$crate has no tracked files" >&2
    exit 1
  fi
done

ACC=""
for rel in "${INPUTS[@]}"; do
  f="$REPO_ROOT/$rel"
  if [[ -f "$f" ]]; then
    ACC+="$rel:$(sha256_file "$f")\n"
  else
    ACC+="$rel:-\n"
  fi
done

# The host links the PDFium built from runtime-src's commit.
ACC+="runtime-src:$(git -C "$REPO_ROOT" ls-files -s packages/engine/runtime/runtime-src | awk '{print $2}')\n"

if command -v sha256sum >/dev/null 2>&1; then
  printf '%b' "$ACC" | sha256sum | awk -v hash_length="$HASH_LENGTH" '{print substr($1, 1, hash_length)}'
else
  printf '%b' "$ACC" | shasum -a 256 | awk -v hash_length="$HASH_LENGTH" '{print substr($1, 1, hash_length)}'
fi
