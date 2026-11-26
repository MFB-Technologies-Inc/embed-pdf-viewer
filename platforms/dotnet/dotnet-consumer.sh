#!/usr/bin/env bash
# Builds the console sample (samples/EmbedPDF.Sample.Console) against packed
# EmbedPDF packages from a local feed, with no project reference and no native
# library path, runs it framework-dependent, then publishes it with
# PublishAot=true and runs the native binary. Every sample step must pass in
# both. The NativeAOT output is measured and then deleted.
#
#   dotnet-consumer.sh [--rid RID] [--packages DIR] [--no-aot] [--no-script]
#                      [--work DIR] [--keep]
#
# Without --packages it makes the packages first, for the host's RID: builds
# the viewer host, stages and packs EmbedPDF.Runtime (--allow-dirty, a local
# package), builds the unstripped host and generates the bindings, then packs
# EmbedPDF and the three library packages. With --packages DIR it takes
# EmbedPDF.Runtime.*.nupkg, EmbedPDF.<version>.nupkg, and EmbedPDF.Models,
# EmbedPDF.Extensions.DependencyInjection and EmbedPDF.Scripting.Jint at that
# version from DIR, as the release workflow's consumer job does.
#
# The NativeAOT publish may report IL2026, IL2104 and IL3053 from Jint's CLR
# interop paths (the sample's csproj says why). Any other trim or AOT warning,
# or one of those three naming an EmbedPDF assembly, fails the run.
#
# Environment: EPDF_DOTNET picks the dotnet that builds and runs the sample
# (an x64 SDK under emulation for linux-x64 on an arm64 host), PYTHON the
# Python (default python3), EPDF_AOT_ARGS extra NativeAOT publish arguments.
# Packing uses the dotnet on PATH.
#
# Exit codes: 0 every step passed, 1 a step failed, 64 bad arguments,
# 66 missing packages.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
RIDS="linux-x64 linux-arm64 osx-arm64 osx-x64 win-x64"
LIBRARIES="EmbedPDF.Models EmbedPDF.Extensions.DependencyInjection EmbedPDF.Scripting.Jint"
ALLOWED_AOT_WARNINGS="IL2026 IL2104 IL3053"
STEPS="di open search render annotate save model"
FIXTURE="$ROOT/pdfium-multiplatform-prototype/fixtures/report.pdf"
DOTNET="${EPDF_DOTNET:-dotnet}"
PYTHON="${PYTHON:-python3}"

usage() {
  echo "usage: $0 [--rid RID] [--packages DIR] [--no-aot] [--no-script] [--work DIR] [--keep]" >&2
  echo "RIDs: $RIDS" >&2
  exit 64
}

host_rid() {
  case "$(uname -s):$(uname -m)" in
    Linux:aarch64 | Linux:arm64) echo linux-arm64 ;;
    Linux:x86_64) echo linux-x64 ;;
    Darwin:arm64) echo osx-arm64 ;;
    Darwin:x86_64) echo osx-x64 ;;
    MINGW*:* | MSYS*:* | CYGWIN*:*) echo win-x64 ;;
    *) echo unknown ;;
  esac
}

rid="" packages="" aot=1 script=1 work="" keep=0
while (($#)); do
  case "$1" in
    --rid) (($# >= 2)) || usage; rid=$2; shift 2 ;;
    --packages) (($# >= 2)) || usage; packages=$2; shift 2 ;;
    --work) (($# >= 2)) || usage; work=$2; shift 2 ;;
    --no-aot) aot=0; shift ;;
    --no-script) script=0; shift ;;
    --keep) keep=1; shift ;;
    *) usage ;;
  esac
done
rid=${rid:-$(host_rid)}
[[ " $RIDS " == *" $rid "* ]] || { echo "unknown RID: $rid" >&2; usage; }

# Package checks come before any build, so a bad feed fails fast.
version=""
if [[ -n "$packages" ]]; then
  [[ -d "$packages" ]] || { echo "no packages directory: $packages" >&2; exit 66; }
  packages="$(cd "$packages" && pwd)"
  runtime=$(ls "$packages"/EmbedPDF.Runtime.*.nupkg 2>/dev/null | head -n 1 || true)
  [[ -n "$runtime" ]] || { echo "missing EmbedPDF.Runtime.*.nupkg in $packages" >&2; exit 66; }
  facade=$(ls "$packages"/EmbedPDF.[0-9]*.nupkg 2>/dev/null | head -n 1 || true)
  [[ -n "$facade" ]] || { echo "missing EmbedPDF.<version>.nupkg in $packages" >&2; exit 66; }
  version=$(basename "$facade" .nupkg); version=${version#EmbedPDF.}
  for id in $LIBRARIES; do
    [[ -f "$packages/$id.$version.nupkg" ]] || { echo "missing $id.$version.nupkg in $packages" >&2; exit 66; }
  done
fi
[[ -f "$FIXTURE" ]] || { echo "missing fixture $FIXTURE" >&2; exit 66; }

if [[ -z "$work" ]]; then
  work="$(mktemp -d "${TMPDIR:-/tmp}/epdf-consumer.XXXXXX")"
  ((keep)) || trap 'rm -rf "${work:?}"' EXIT
else
  mkdir -p "$work"
  [[ -z "$(ls -A "$work")" ]] || { echo "work directory must be new or empty: $work" >&2; exit 64; }
  work="$(cd "$work" && pwd)"
fi
feed="$work/feed"
mkdir -p "$feed"
fail() { echo "FAIL: $*"; exit 1; }
size_of() { "$PYTHON" -c 'import os,sys
p=sys.argv[1]
print(os.path.getsize(p) if os.path.isfile(p) else sum(os.path.getsize(os.path.join(d,f)) for d,_,fs in os.walk(p) for f in fs))' "$1"; }

if [[ -n "$packages" ]]; then
  cp "$packages"/*.nupkg "$feed/"
else
  [[ "$rid" == "$(host_rid)" ]] || fail "building packages needs the host's RID $(host_rid); pass --packages for $rid"
  target_dir="${CARGO_TARGET_DIR:-$ROOT/target}"
  host=$(bash "$HERE/build-viewer-host.sh" "$rid" | tail -n 1)
  "$PYTHON" "$HERE/packaging/package.py" stage-runtime --rid "$rid" --native-lib "$host" --out "$work/slice-$rid"
  mkdir -p "$work/runtime"
  "$PYTHON" "$HERE/packaging/package.py" pack-runtime --slice "$work/slice-$rid" --out "$work/runtime" --allow-dirty
  # The generator reads uniffi metadata that the stripped host has lost.
  (cd "$ROOT" && cargo rustc -p epdf-viewer-host --release --locked --crate-type cdylib)
  case "$rid" in osx-*) unstripped="$target_dir/release/libepdf_viewer_host.dylib" ;; *) unstripped="$target_dir/release/libepdf_viewer_host.so" ;; esac
  bash "$HERE/generate-bindings.sh" --library "$unstripped"
  runtime=$(ls "$work"/runtime/EmbedPDF.Runtime.*.nupkg)
  cp "$runtime" "$feed/"
  "$PYTHON" "$HERE/packaging/package.py" pack-facade --runtime-package "$runtime" --out "$feed"
  "$PYTHON" "$HERE/packaging/libraries.py" pack-libraries --facade "$feed/EmbedPDF.0.0.0-local.nupkg" \
    --runtime-package "$runtime" --out "$feed"
  version=0.0.0-local
fi
echo "Feed: $(cd "$feed" && ls *.nupkg | tr '\n' ' ')"

# The sample, outside the repository: only the feed can satisfy it.
src="$work/src"
mkdir -p "$src"
cp "$HERE/global.json" "$HERE/samples/Directory.Build.props" "$src/"
cp "$HERE"/samples/EmbedPDF.Sample.Console/*.cs "$HERE"/samples/EmbedPDF.Sample.Console/*.csproj "$src/"
cat >"$src/nuget.config" <<NUGET
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <!-- EmbedPDF packages come from the local feed and nowhere else. -->
  <packageSourceMapping>
    <packageSource key="local"><package pattern="EmbedPDF" /><package pattern="EmbedPDF.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
NUGET

# No native path from the environment: the package layout must be enough.
unset EPDF_NATIVE_DIR PDF_RUNTIME_LIB_DIR LD_LIBRARY_PATH DYLD_LIBRARY_PATH DYLD_FALLBACK_LIBRARY_PATH
export NUGET_PACKAGES="$work/nuget" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
project="$src/EmbedPDF.Sample.Console.csproj"
sample_args=()
((script)) || sample_args+=(--no-script)

check_run() { # name, output file, exit code
  local name=$1 out=$2 code=$3
  [[ $code == 0 ]] || fail "$name: sample exited $code"
  ! grep -q '^FAIL' "$out" || fail "$name: a sample step failed"
  for step in $STEPS; do
    grep -q "^PASS $step: " "$out" || fail "$name: no PASS line for $step"
  done
  if ((script)); then grep -q '^PASS script: ' "$out" || fail "$name: no PASS line for script"; fi
  local size
  size=$(grep '^PASS render: ' "$out" | grep -oE '[0-9]+x[0-9]+' | head -n 1)
  "$PYTHON" "$HERE/packaging/png_check.py" "$work/out-$name/page-1.png" --width "${size%x*}" --height "${size#*x}"
  echo "PASS: $name sample run"
}

run_sample() { # name, command...
  local name=$1; shift
  local code=0
  "$@" "$FIXTURE" "$work/out-$name" "${sample_args[@]}" >"$work/run-$name.log" 2>&1 || code=$?
  sed "s/^/  [$name] /" "$work/run-$name.log"
  check_run "$name" "$work/run-$name.log" "$code"
}

"$DOTNET" build "$project" -c Release -o "$work/fdd" -p:EmbedPdfVersion="$version" >"$work/build-fdd.log" 2>&1 ||
  { cat "$work/build-fdd.log"; fail "framework-dependent build"; }
grep -E "^ *[0-9]+ Warning\(s\)" "$work/build-fdd.log" | sed 's/^ */framework-dependent build: /' || true
run_sample fdd "$DOTNET" "$work/fdd/EmbedPDF.Sample.Console.dll"
echo "SIZE fdd-dir=$(size_of "$work/fdd")"
modes="fdd"

if ((aot)); then
  # EPDF_AOT_ARGS adds publish arguments, such as a cross linker for an
  # emulated RID (-p:CppCompilerAndLinker=x86_64-linux-gnu-gcc).
  read -r -a aot_args <<<"${EPDF_AOT_ARGS:-}"
  "$DOTNET" publish "$project" -c Release -r "$rid" -p:PublishAot=true -o "$work/aot" -p:EmbedPdfVersion="$version" "${aot_args[@]}" \
    >"$work/publish-aot.log" 2>&1 || { cat "$work/publish-aot.log"; fail "NativeAOT publish"; }
  # Each warning once: MSBuild repeats them in its summary.
  { grep -E 'warning IL[0-9]+' "$work/publish-aot.log" || true; } | sed -E 's/ \[[^]]*\]$//' | sort -u >"$work/aot-warnings.txt"
  while IFS= read -r line; do
    code=$(grep -oE 'IL[0-9]+' <<<"${line#*warning }" | head -n 1)
    message=${line#*warning $code: }
    echo "WARN aot $code: $message"
    [[ " $ALLOWED_AOT_WARNINGS " == *" $code "* ]] || fail "NativeAOT warning $code is not on the allow list"
    [[ "$message" != *EmbedPDF* ]] || fail "NativeAOT warning $code names an EmbedPDF assembly"
    [[ "$message" == *Jint* ]] || fail "NativeAOT warning $code does not come from Jint"
  done <"$work/aot-warnings.txt"
  echo "AOT warnings: $(wc -l <"$work/aot-warnings.txt" | tr -d ' ')"
  binary="$work/aot/EmbedPDF.Sample.Console"
  [[ "$rid" == win-* ]] && binary="$binary.exe"
  [[ -f "$binary" ]] || fail "no NativeAOT binary at $binary"
  run_sample aot "$binary"
  echo "SIZE aot-binary=$(size_of "$binary")"
  echo "SIZE aot-dir=$(size_of "$work/aot")"
  for file in "$work"/aot/*; do
    [[ -f "$file" ]] && echo "SIZE aot-file $(basename "$file")=$(size_of "$file")"
  done
  echo "du before delete: $(du -sh "$work" | cut -f1)"
  rm -rf "${work:?}/aot" "${work:?}/src/obj" "${work:?}/src/bin"
  echo "du after delete: $(du -sh "$work" | cut -f1)"
  modes="fdd aot"
fi

echo "PASS: consumer rid=$rid $modes"
