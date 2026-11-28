#!/usr/bin/env bash
# 2026-11-27b (CU-C7). usage: x64-dotnet.sh <checkout> <label>   in the linux/amd64 container.
# The .NET rows for linux-x64 (CU-C7-D10): .NET SDK 11.0.100 and runtime 10.0.15 from
# dotnet-install.sh into /workspaces/tools-1127b/dotnet-x64 (global.json pins 11.0.100 and the
# image has 10.0.404), the linux-x64 runtime archive fetched with curl and checked against
# engine-runtime-build.json, then dotnet.sh with the amd64 image's id.
set -euo pipefail
C=$1 LABEL=$2 D=/workspaces/tools-1127b/dotnet-x64 A=/workspaces/slices-1127b/linux-x64.tar.gz
if [[ ! -x $D/dotnet ]]; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --version 11.0.100 --install-dir $D --no-path >/dev/null
  bash /tmp/dotnet-install.sh --runtime dotnet --version 10.0.15 --install-dir $D --no-path >/dev/null
fi
field() { python3 -c "import json,sys; print(json.load(open(sys.argv[1]))['artifacts']['linux-x64'][sys.argv[2]])" $C/packages/engine/runtime/engine-runtime-build.json $1; }
[[ -f $A ]] || curl -fsSL "$(field url)" -o $A
[[ $(sha256sum $A | cut -d' ' -f1) == "$(field sha256)" ]] || { echo "sha256 mismatch for $A"; exit 1; }
export DOTNET_ROOT=$D PATH=$D:/home/node/.cargo/bin:$PATH CARGO_TARGET_DIR=/workspaces/target-x64 CARGO_BUILD_JOBS=14
export EPDF_IMAGE_ID=${EPDF_IMAGE_ID:?the amd64 image id} EPDF_HOST_OS=${EPDF_HOST_OS:-darwin-27.2.0} ENGINE_CHECK=0
bash $C/docs/research/upstream-catchup-2026-11-27b/scripts/cu-c7/dotnet.sh $C $LABEL linux-x64 $A
