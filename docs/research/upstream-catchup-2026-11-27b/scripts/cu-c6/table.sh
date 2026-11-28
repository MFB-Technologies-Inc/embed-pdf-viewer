#!/usr/bin/env bash
# CU-C6 (2026-11-27b), from 2026-11-27 cu-c7/table.sh: logs under CU-C6, T defaults to
# /workspaces/target, and part tsforce runs the packages/** tests with turbo --force (2026-11-27
# CU-C7-D5).
# CU-C7 (2026-11-27), from 2026-11-18 cu-c7/table.sh.
# usage: table.sh <checkout> <label> [parts]   parts: suite clippy replay evidence tsforce (default: all but tsforce)
# The rows that need no Rust-core payload: cargo test, engine :check, the four plugin checks and
# the packages/** TS tests (../suite.sh), cargo clippy, replay:check and the evidence tests.
# T is the cargo target dir; TOUCH=1 makes the checkout's sources newer than any build another
# checkout of the same workspace left in T (2026-11-18 CU-C7-D3).
set -uo pipefail
W=$1; LB=$2; shift 2; PARTS="${*:-suite clippy replay evidence}"
OUT=/workspaces/logs/CU-C6/$LB; mkdir -p "$OUT"
T=${T:-/workspaces/target}
[[ ${TOUCH:-0} == 1 ]] && find "$W/crates" -type f \( -name '*.rs' -o -name Cargo.toml -o -name '*.h' \) -exec touch {} +
export CARGO_TARGET_DIR=$T CI=true TZ=UTC NPM_TOKEN="" CARGO_BUILD_JOBS=4 CARGO_INCREMENTAL=0 CARGO_PROFILE_DEV_DEBUG=0 CARGO_PROFILE_TEST_DEBUG=0
export PATH="/workspaces/bin:/home/node/.cargo/bin:$PATH"
export PDF_RUNTIME_LIB_DIR=$W/packages/engine/runtime/build/libpdfium
export LD_LIBRARY_PATH=$W/packages/engine/runtime/build/libpdfium/linux-arm64/lib
cd "$W" || exit 2
git rev-parse HEAD > "$OUT/head.txt"
for p in $PARTS; do case $p in
suite) bash /workspaces/scripts/suite.sh "$W" "$OUT" ${SUITE_PARTS:-cargo engconf plugconf} > "$OUT/suite.log" 2>&1; echo "suite exit=$?" ;;
tsforce)
  pnpm exec turbo run test --force --continue --concurrency=6 --filter='./packages/**' > "$OUT/ts-force.log" 2>&1; echo "tsforce exit=$?" | tee "$OUT/ts-force.exit"
  python3 /workspaces/scripts/cu-c6/ts-count.py "$OUT/ts-force.log" | tee -a "$OUT/ts-force.exit" ;;
clippy)
  cargo clippy --workspace --all-targets > "$OUT/clippy.log" 2>&1; echo "clippy exit=$?" | tee "$OUT/clippy.exit"
  grep -cE "^(warning|error)(\[|:)" "$OUT/clippy.log" | sed 's/^/diagnostics=/' | tee -a "$OUT/clippy.exit"
  grep -E "^(warning|error)" "$OUT/clippy.log" | sort | uniq -c | sort -rn > "$OUT/clippy.kinds" ;;
replay)
  (cd tooling/plugin-conformance && timeout 3000 pnpm -s run replay:check) > "$OUT/plugconf.replay:check.log" 2>&1
  echo "replay:check exit=$?" | tee -a "$OUT/plugconf.all" ;;
evidence)
  cd "$W/tooling/plugin-conformance"; EV=$OUT/evidence; mkdir -p "$EV"; : > "$EV/summary.txt"
  for f in test/*.test.ts test/*.test.mjs; do
    n=$(basename "$f")
    timeout 1200 node --import=tsx --conditions=development --test "$f" > "$EV/$n.log" 2>&1; e=$?
    ps=$(grep -E "^ℹ pass" "$EV/$n.log" | awk '{print $3}'); x=$(grep -E "^ℹ fail" "$EV/$n.log" | awk '{print $3}')
    echo "$n exit=$e pass=$ps fail=$x" >> "$EV/summary.txt"
  done
  awk '{for(i=1;i<=NF;i++){if($i~/^pass=/){split($i,a,"=");P+=a[2]} if($i~/^fail=/){split($i,b,"=");F+=b[2]}}} END{print "TOTAL pass="P" fail="F}' "$EV/summary.txt" >> "$EV/summary.txt"
  tail -1 "$EV/summary.txt"; cd "$W" ;;
esac; done
echo TABLE DONE
