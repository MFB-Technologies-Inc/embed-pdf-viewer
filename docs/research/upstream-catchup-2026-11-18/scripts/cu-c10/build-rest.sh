#!/usr/bin/env bash
# Runs inside a linux/amd64 debian:trixie container. Builds and packages the
# fork's linux-arm64 and wasm32 release targets at af480d0ce (the android and
# wasm32-eh archives for the same commit were built by CU-C8c and CU-C1).
# Source and caches live in the /work volume; logs and artifacts go to /out.
set -uo pipefail
export DEBIAN_FRONTEND=noninteractive
SHA=af480d0ce420ed916db0de8d2e8827822ad7be1e
L=/out/logs A=/out/artifacts
mkdir -p $L $A
step() {
  local name="$1"; shift
  if [[ -f "$L/$name.done" ]] && grep -q 'rc=0' "$L/$name.done"; then echo "skip $name"; return 0; fi
  local t0=$SECONDS
  "$@" > "$L/$name.log" 2>&1
  local rc=$?
  echo "$name rc=$rc secs=$((SECONDS - t0))" | tee "$L/$name.done"
  [[ $rc -eq 0 ]] || { echo "STEP FAILED: $name"; exit $rc; }
}
step apt bash -c 'apt-get update && apt-get install -y --no-install-recommends git python3 curl ca-certificates xz-utils bzip2 lsb-release file pkg-config tar patch nodejs unzip sudo procps'
step depot-tools bash -c '[[ -d /work/depot_tools ]] || git clone --depth 1 https://chromium.googlesource.com/chromium/tools/depot_tools.git /work/depot_tools'
export PATH="/work/depot_tools:$PATH" DEPOT_TOOLS_UPDATE=0
git config --global user.name "Andrew Roan"
git config --global user.email roanutil@users.noreply.github.com
git config --global --add safe.directory '*'
step depot-tools-bootstrap /work/depot_tools/ensure_bootstrap
step clone bash -c "
  set -e
  [[ -d /work/runtime/.git ]] || git clone --filter=blob:none --branch research/multi-platform-rust-core https://github.com/roanutil/embed-pdf-runtime.git /work/runtime
  cd /work/runtime
  git fetch origin research/multi-platform-rust-core
  git checkout --detach $SHA
  git remote get-url upstream >/dev/null 2>&1 || git remote add upstream https://pdfium.googlesource.com/pdfium
  git fetch --filter=blob:none upstream main:refs/remotes/upstream/main
  git rev-parse HEAD"
cd /work/runtime
export PDF_RUNTIME_TARGET_OS_LIST="linux emscripten"
step sync scripts/embedpdf-runtime/ensure-deps.sh
step emsdk-clone bash -c '[[ -x third_party/emsdk/emsdk ]] || git clone https://github.com/emscripten-core/emsdk.git third_party/emsdk'
step emsdk-3.1.72 bash -c 'third_party/emsdk/emsdk install 3.1.72 && third_party/emsdk/emsdk activate 3.1.72'
export PATH="/work/runtime/third_party/emsdk/upstream/emscripten:/work/runtime/third_party/emsdk/upstream/bin:$PATH"
step build-linux-arm64 scripts/embedpdf-runtime/build-target.sh linux-arm64
step build-wasm32 scripts/embedpdf-runtime/build-target.sh wasm32
for t in linux-arm64 wasm32; do
  step package-$t env PDF_RUNTIME_ARTIFACT_DIR=$A scripts/embedpdf-runtime/package-target.sh $t
done
step check-c-api python3 scripts/embedpdf-runtime/check-c-api.py
step target-args-test bash scripts/embedpdf-runtime/tests/target-args.test.sh
python3 scripts/embedpdf-runtime/check-upstream-freshness.py --enforce --json $A/../upstream.json --notes $A/../upstream-notes.md > $L/freshness.log 2>&1; echo "freshness rc=$?" | tee $L/freshness.done
(cd $A && sha256sum ./*.tar.gz > ../SHA256SUMS && cat ../SHA256SUMS)
echo ALL BUILT
