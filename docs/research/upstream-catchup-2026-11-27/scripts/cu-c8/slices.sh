#!/bin/bash
# 2026-11-27 (CU-C8), from ../../../upstream-catchup-2026-11-18/scripts/cu-c8c/.
# Run inside the temporary linux/amd64 container (CU-C8-D4), /work its volume, /logs the host log dir.
# Builds and packages the android-arm64 and android-x64 fork slices in cu-c8-android.
set -uo pipefail
export PATH=/work/depot_tools:$PATH DEPOT_TOOLS_UPDATE=0
export PDF_RUNTIME_TARGET_OS_LIST="linux android"
export PDF_RUNTIME_ARTIFACT_DIR=/work/archives
cd /work/runtime
step() {
  local name=$1; shift
  local t0=$SECONDS
  "$@" > "/logs/$name.log" 2>&1
  local rc=$?
  echo "$name rc=$rc secs=$((SECONDS - t0))" | tee -a /logs/slices.status
  [ $rc -eq 0 ] || exit $rc
}
step build-android-arm64 scripts/embedpdf-runtime/build-target.sh android-arm64
step build-android-x64 scripts/embedpdf-runtime/build-target.sh android-x64
step package-android-arm64 scripts/embedpdf-runtime/package-target.sh android-arm64
step package-android-x64 scripts/embedpdf-runtime/package-target.sh android-x64
ls -la /work/archives | tee -a /logs/slices.status
sha256sum /work/archives/*.tar.gz | tee -a /logs/slices.status
