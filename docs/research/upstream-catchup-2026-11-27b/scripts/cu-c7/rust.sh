#!/usr/bin/env bash
# 2026-11-27b (CU-C7), from ../../../upstream-catchup-2026-11-27/scripts/cu-c8/rust.sh.
# usage: rust.sh <label>   in the arm64 container
# The Rust rows of this job: fmt, clippy and tests of the crates the job touches with their uniffi
# features, then the epdf-web wasm tests. Each row's exit code goes to
# /workspaces/logs/CU-C7/<label>/rust.all.
set -uo pipefail
LABEL=$1; source "$(dirname "$0")/env.sh" "${W:-/workspaces/wt/CU-C7}"
OUT=$L/$LABEL; mkdir -p "$OUT"; : > "$OUT/rust.all"
echo "checkout $(git rev-parse HEAD) $(git status --short -uno | wc -l) dirty; start $(date -u +%FT%TZ)" >> "$OUT/rust.all"
row() { local name=$1; shift; "$@" > "$OUT/$name.log" 2>&1; echo "$name exit=$?" >> "$OUT/rust.all"; }
P="-p epdf-ffi -p epdf-plugin-form -p epdf-plugin-annotation -p epdf-viewer-host"
F="--features epdf-plugin-form/uniffi,epdf-plugin-annotation/uniffi"
row fmt cargo fmt --check -p epdf-ffi -p epdf-plugin-form -p epdf-plugin-annotation -p epdf-viewer-host -p epdf-web
row clippy cargo clippy --locked $P $F --all-targets
row test cargo test --locked --no-fail-fast $P $F
grep -E "^test result" "$OUT/test.log" | awk '{p+=$4; f+=$6; i+=$8} END {print "tests passed=" p " failed=" f " ignored=" i}' >> "$OUT/rust.all"
row epdf-web env CARGO_TARGET_WASM32_UNKNOWN_UNKNOWN_RUNNER=wasm-bindgen-test-runner cargo test -p epdf-web --target wasm32-unknown-unknown --test '*'
grep -E "^test result" "$OUT/epdf-web.log" | awk '{p+=$4; f+=$6} END {print "epdf-web passed=" p " failed=" f}' >> "$OUT/rust.all"
echo "end $(date -u +%FT%TZ)" >> "$OUT/rust.all"; echo RUSTDONE >> "$OUT/rust.all"
