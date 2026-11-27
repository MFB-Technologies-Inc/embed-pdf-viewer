#!/usr/bin/env bash
# CU-C6 (2026-11-27), from cu-c4/after.sh: cargo test --workspace, engine :check, the plugin
# checks, replay:check, the failures by crate, and the evidence tests. Engine vectors are not
# regenerated: the job changes no engine generator (plugin vectors: regen-plugins2x.sh).
# usage: after.sh <checkout> <label>   Logs in /workspaces/logs/CU-C6/<label>/.
set -uo pipefail
W=$1; L=/workspaces/logs/CU-C6/$2; C3=/workspaces/scripts/cu-c3; mkdir -p "$L"
export CARGO_TARGET_DIR=/workspaces/target-c5 CI=true TZ=UTC PATH="/workspaces/bin:$PATH"
bash /workspaces/scripts/suite.sh "$W" "$L" cargo engconf plugconf > "$L/suite.log" 2>&1; echo "suite exit=$?"
cd "$W" && CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0 \
  PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium \
  cargo test --workspace --no-run --message-format=json 2>/dev/null \
  | node -e 'let s="";process.stdin.on("data",d=>s+=d).on("end",()=>{for(const l of s.split("\n")){if(!l.startsWith("{"))continue;const m=JSON.parse(l);if(m.reason==="compiler-artifact"&&m.executable){const id=m.package_id,h=id.split("#")[1]||"",n=h.includes("@")?h.split("@")[0]:id.split("#")[0].split("/").pop();console.log(require("path").basename(m.executable),n,m.target.name)}}})' > "$L/test-binaries.txt"
python3 /workspaces/scripts/cargo-by-crate.py "$L/cargo.log" "$L/cargo-by-crate.tsv" "$L/test-binaries.txt" > "$L/cargo-by-crate.summary"
python3 "$C3/replay-cases.py" "$L/cargo.log" > "$L/replay-cases.tsv"
cd "$W/tooling/plugin-conformance" && CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0 \
  PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium LD_LIBRARY_PATH=$W/packages/engine/runtime/build/libpdfium/linux-arm64/lib \
  timeout 3000 pnpm -s run replay:check > "$L/plugconf.replay:check.log" 2>&1; echo "replay:check exit=$?" | tee -a "$L/plugconf.all"
# Evidence: every plugin-conformance evidence test file, one result line each (cu-c3/evrun.sh).
cd "$W/tooling/plugin-conformance"; OUT=$L/ev; mkdir -p "$OUT"; : > "$OUT/summary.txt"
for f in test/*.test.ts test/*.test.mjs; do
  n=$(basename "$f")
  timeout 1200 node --import=tsx --conditions=development --test "$f" > "$OUT/$n.log" 2>&1; e=$?
  p=$(grep -E "^ℹ pass" "$OUT/$n.log" | awk '{print $3}'); x=$(grep -E "^ℹ fail" "$OUT/$n.log" | awk '{print $3}')
  echo "$n exit=$e pass=$p fail=$x" >> "$OUT/summary.txt"
done
awk '{for(i=1;i<=NF;i++){if($i~/^pass=/){split($i,a,"=");P+=a[2]} if($i~/^fail=/){split($i,b,"=");F+=b[2]}}} END{print "TOTAL pass="P" fail="F}' "$OUT/summary.txt" >> "$OUT/summary.txt"
echo AFTER DONE
