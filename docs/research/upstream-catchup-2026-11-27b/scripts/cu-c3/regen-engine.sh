#!/usr/bin/env bash
# CU-C3 (2026-11-27), from 2026-11-18 cu-c2/CU-C2-regen-engine.sh.
# CU-C3 (2026-11-27b): copied from 2026-11-27 scripts/cu-c3/regen-engine.sh.
# usage: regen-engine.sh <checkout>  regenerate every engine-conformance vector group that has a :check
set -uo pipefail
RLOG=${RLOG:-/workspaces/logs/CU-C3/regen-tmp}; mkdir -p "$RLOG"
cd "$1/tooling/engine-conformance" || exit 2
export PATH="/workspaces/bin:$PATH" CI=true TZ=UTC
for s in $(node -p "Object.keys(require('./package.json').scripts).filter(s=>s+':check' in require('./package.json').scripts && s!=='operations').join(' ')"); do
  if pnpm -s run "$s" > "$RLOG/regen.$s.log" 2>&1; then echo "$s ok"; else echo "$s FAIL: $(grep -vE '^\s+at |WARN|^\s*$' $RLOG/regen.$s.log | tail -1 | cut -c1-200)"; fi
done
