#!/usr/bin/env bash
# Tests for dotnet-consumer.sh's argument and package checks. Run from anywhere:
#   bash platforms/dotnet/dotnet-consumer.test.sh
# The argument cases stop before any build; the verdict cases run the script
# with a stand-in dotnet, so no .NET build happens.
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "${TMP:?}"' EXIT
failures=0
pass() { echo "PASS: $1"; }
fail() { echo "FAIL: $1"; failures=$((failures + 1)); }

expect_exit() {
  local name=$1 want=$2; shift 2
  "$@" >"$TMP/out" 2>&1
  local got=$?
  if [[ $got == "$want" ]]; then pass "$name"; else fail "$name (exit $got, want $want)"; cat "$TMP/out"; fi
}
expect_output() {
  grep -q -- "$2" "$TMP/out" && pass "$1" || { fail "$1"; cat "$TMP/out"; }
}

expect_exit "unknown flag exits 64" 64 bash "$HERE/dotnet-consumer.sh" --bogus
expect_exit "unknown RID exits 64" 64 bash "$HERE/dotnet-consumer.sh" --rid bogus
expect_output "usage lists the RIDs" "linux-arm64"
expect_exit "--rid without a value exits 64" 64 bash "$HERE/dotnet-consumer.sh" --rid
expect_exit "missing packages directory exits 66" 66 bash "$HERE/dotnet-consumer.sh" --packages /nonexistent/feed
expect_output "missing packages directory is named" "/nonexistent/feed"
mkdir "$TMP/empty"
expect_exit "empty packages directory exits 66" 66 bash "$HERE/dotnet-consumer.sh" --packages "$TMP/empty"
expect_output "the missing package is named" "EmbedPDF.Runtime"
mkdir "$TMP/partial"
for id in EmbedPDF.Runtime.0.0.0-engine.0000000000000000 EmbedPDF.0.0.0-local EmbedPDF.Models.0.0.0-local; do
  touch "$TMP/partial/$id.nupkg"
done
expect_exit "a missing library package exits 66" 66 bash "$HERE/dotnet-consumer.sh" --packages "$TMP/partial"
expect_output "the missing library is named" "EmbedPDF.Extensions.DependencyInjection"

# A stand-in dotnet: `build` and `publish` make their -o directory (publish
# also prints FAKE_PUBLISH_LOG and a binary that runs FAKE_RUN), and running
# the sample prints FAKE_RUN's output. It checks the script's verdicts, not .NET.
fake="$TMP/fake"
mkdir -p "$fake" "$TMP/feed"
for id in EmbedPDF.Runtime.0.0.0-engine.0000000000000000 EmbedPDF.0.0.0-local EmbedPDF.Models.0.0.0-local \
  EmbedPDF.Extensions.DependencyInjection.0.0.0-local EmbedPDF.Scripting.Jint.0.0.0-local; do
  touch "$TMP/feed/$id.nupkg"
done
cat >"$fake/dotnet" <<'FAKE'
#!/usr/bin/env bash
out=""
args=("$@")
for ((i = 0; i < ${#args[@]}; i++)); do [[ ${args[i]} == -o ]] && out=${args[i+1]}; done
case "$1" in
  build) mkdir -p "$out" ;;
  publish)
    mkdir -p "$out"
    printf '#!/usr/bin/env bash\nexec "%s" run "$@"\n' "$0" >"$out/EmbedPDF.Sample.Console"
    chmod +x "$out/EmbedPDF.Sample.Console"
    printf '%b' "${FAKE_PUBLISH_LOG:-}" ;;
  *) exec bash -c "$FAKE_RUN" fake "$@" ;;
esac
FAKE
chmod +x "$fake/dotnet"
# A run where every step passes, writing the 1x1 PNG the render line names.
good_run='mkdir -p "$3"; python3 -c "import struct,zlib,sys
c=lambda k,b: struct.pack(\">I\",len(b))+k+b+struct.pack(\">I\",zlib.crc32(k+b))
open(sys.argv[1],\"wb\").write(b\"\x89PNG\r\n\x1a\n\"+c(b\"IHDR\",struct.pack(\">IIBBBBB\",1,1,8,6,0,0,0))+c(b\"IDAT\",zlib.compress(bytes(5)))+c(b\"IEND\",b\"\"))" "$3/page-1.png"
for s in di open search annotate save model script; do echo "PASS $s: ok"; done; echo "PASS render: page 1 1x1"'

FAKE_RUN="$good_run" EPDF_DOTNET="$fake/dotnet" expect_exit "passing steps pass" 0 \
  bash "$HERE/dotnet-consumer.sh" --rid linux-arm64 --packages "$TMP/feed"
expect_output "the verdict names both modes" "PASS: consumer rid=linux-arm64 fdd aot"
FAKE_RUN='echo "PASS di: ok"; echo "FAIL open: boom"; exit 1' EPDF_DOTNET="$fake/dotnet" expect_exit "a failed step fails the run" 1 \
  bash "$HERE/dotnet-consumer.sh" --rid linux-arm64 --packages "$TMP/feed" --no-aot
expect_output "the failed run is named" "FAIL: fdd: sample exited 1"
FAKE_RUN='for s in di open search render annotate save; do echo "PASS $s: ok"; done' EPDF_DOTNET="$fake/dotnet" \
  expect_exit "a missing step fails the run" 1 bash "$HERE/dotnet-consumer.sh" --rid linux-arm64 --packages "$TMP/feed" --no-aot
expect_output "the missing step is named" "no PASS line for model"
FAKE_RUN="$good_run" FAKE_PUBLISH_LOG="x.cs(1): warning IL2026: EmbedPDF.Viewer uses reflection [p.csproj]\n" EPDF_DOTNET="$fake/dotnet" \
  expect_exit "an allowed code naming EmbedPDF fails" 1 bash "$HERE/dotnet-consumer.sh" --rid linux-arm64 --packages "$TMP/feed"
expect_output "the EmbedPDF warning is named" "names an EmbedPDF assembly"
FAKE_RUN="$good_run" FAKE_PUBLISH_LOG="ILC: warning IL3050: Jint.Engine uses dynamic code [p.csproj]\n" EPDF_DOTNET="$fake/dotnet" \
  expect_exit "a code outside the allow list fails" 1 bash "$HERE/dotnet-consumer.sh" --rid linux-arm64 --packages "$TMP/feed"
expect_output "the unlisted code is named" "IL3050 is not on the allow list"
FAKE_RUN="$good_run" FAKE_PUBLISH_LOG="ILC: warning IL2104: Assembly 'Jint' produced trim warnings. [/x/EmbedPDF.Sample.Console.csproj]\n" \
  EPDF_DOTNET="$fake/dotnet" expect_exit "Jint's allowed warning passes" 0 bash "$HERE/dotnet-consumer.sh" --rid linux-arm64 --packages "$TMP/feed"

echo
if ((failures)); then echo "$failures failure(s)"; exit 1; fi
echo "all dotnet-consumer checks passed"
