#!/bin/bash
# Run inside the temporary linux/amd64 container (CU-C8c-D5), /work its volume, /logs the host log dir.
set -euo pipefail
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq --no-install-recommends git curl ca-certificates python3 xz-utils lsb-release sudo file pkg-config procps rsync bzip2 unzip >/dev/null
apt-get clean; rm -rf /var/lib/apt/lists/*
git config --global --add safe.directory '*'
[ -d /work/depot_tools ] || git clone -q https://chromium.googlesource.com/chromium/tools/depot_tools.git /work/depot_tools
[ -d /work/runtime ] || git clone -q --no-local /src /work/runtime -b catchup/2026-11-18-runtime
git -C /work/runtime log --oneline -1
export PATH=/work/depot_tools:$PATH DEPOT_TOOLS_UPDATE=0
/work/depot_tools/ensure_bootstrap >/dev/null 2>&1 || true
gclient --version
