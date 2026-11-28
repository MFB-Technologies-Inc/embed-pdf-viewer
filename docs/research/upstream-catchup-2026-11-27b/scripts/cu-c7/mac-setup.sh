#!/usr/bin/env bash
# 2026-11-27b (CU-C7). usage: mac-setup.sh <checkout> <scratch>   on the Mac host; no Node tool.
# The osx rows' inputs (CU-C7-D6): .NET SDK 11.0.100 and runtime 10.0.15 for arm64 and x64 under
# <scratch>/dotnet-{arm64,x64} (dotnet-install.sh), and the darwin-arm64 and darwin-x64 runtime of
# engine-runtime-build.json, fetched with curl and checked against its sha256 (fetch-libpdfium.sh
# calls node), unpacked under <scratch>/libpdfium.
set -euo pipefail
C=$1 S=$2; mkdir -p "$S"
R=$C/packages/engine/runtime
curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$S/dotnet-install.sh"
for arch in arm64 x64; do
  d=$S/dotnet-$arch
  [[ -x $d/dotnet ]] || bash "$S/dotnet-install.sh" --version 11.0.100 --architecture $arch --install-dir "$d" --no-path >/dev/null
  bash "$S/dotnet-install.sh" --runtime dotnet --version 10.0.15 --architecture $arch --install-dir "$d" --no-path >/dev/null
  echo "dotnet-$arch: $(arch -$([[ $arch == x64 ]] && echo x86_64 || echo arm64) "$d/dotnet" --list-runtimes | grep NETCore | tr '\n' ' ')"
done
field() { /usr/bin/python3 -c "import json,sys; print(json.load(open(sys.argv[1]))['artifacts'][sys.argv[2]][sys.argv[3]])" "$@"; }
for t in darwin-arm64 darwin-x64; do
  a=$S/$t.tar.gz
  curl -fsSL "$(field $R/engine-runtime-build.json $t url)" -o "$a"
  got=$(shasum -a 256 "$a" | cut -d' ' -f1) want=$(field $R/engine-runtime-build.json $t sha256)
  [[ $got == "$want" ]] || { echo "sha256 mismatch for $t: $got, want $want"; exit 1; }
  rm -rf "$S/libpdfium/$t"; mkdir -p "$S/libpdfium/$t"; tar -xzf "$a" -C "$S/libpdfium/$t"; echo "$t ok $got"
done
echo MACSETUPDONE
