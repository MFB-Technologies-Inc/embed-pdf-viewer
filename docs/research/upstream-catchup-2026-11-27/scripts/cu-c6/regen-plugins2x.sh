#!/usr/bin/env bash
# CU-C6 (2026-11-27), from cu-c3/regen2x.sh: the plugin vectors twice (TZ=UTC), hashing every
# vector file after each run, then coverage.json. Engine vectors are not regenerated: the job
# changes no engine generator.
# usage: regen-plugins2x.sh <checkout> <label>   Logs in /workspaces/logs/CU-C6/<label>/.
set -uo pipefail
W=$1; L=/workspaces/logs/CU-C6/$2; mkdir -p "$L"
export CI=true TZ=UTC PATH="/workspaces/bin:$PATH"
: > "$L/determinism.log"
for r in 1 2; do
  (cd "$W/tooling/plugin-conformance" && timeout 3600 pnpm -s run vectors) > "$L/plugregen-run$r.log" 2>&1
  echo "plugin run$r exit=$?" >> "$L/determinism.log"
  (cd "$W/tooling/plugin-conformance" && find vectors -type f | LC_ALL=C sort | xargs sha256sum; sha256sum "$W/crates/plugins/acrojs/prelude.js") > "$L/plugregen-run$r.sha"
done
{ cmp "$L/plugregen-run1.sha" "$L/plugregen-run2.sha" && echo "plugin deterministic=yes" || echo "plugin deterministic=no"
  echo "plugin files: $(wc -l < "$L/plugregen-run2.sha")"; git -C "$W" status --short; } >> "$L/determinism.log"
(cd "$W/tooling/plugin-conformance" && timeout 3600 pnpm -s run coverage) > "$L/coverage-write.log" 2>&1
echo "coverage exit=$?" >> "$L/determinism.log"
git -C "$W" status --short >> "$L/determinism.log"
cat "$L/determinism.log"
echo REGEN DONE
