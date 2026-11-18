#!/usr/bin/env bash
# CU-C3-regen2.sh: wire vectors twice, then every plugin vector twice (TZ=UTC), hashing after each run.
set -uo pipefail
W=/workspaces/wt/CU-C3; L=/workspaces/logs/CU-C3
export CI=true TZ=UTC
for r in 1 2; do
  (cd $W/tooling/engine-conformance && pnpm -s run wire) > $L/CU-C3-wire-run$r.log 2>&1; echo "wire run$r exit=$?"
  sha256sum $W/tooling/engine-conformance/vectors/wire/requests.json | cut -c1-64 > $L/CU-C3-wire-run$r.sha
done
cmp $L/CU-C3-wire-run1.sha $L/CU-C3-wire-run2.sha && echo "wire deterministic=yes" || echo "wire deterministic=no"
for r in 1 2; do
  (cd $W/tooling/plugin-conformance && timeout 3600 pnpm -s run vectors) > $L/CU-C3-plugregen-run$r.log 2>&1; echo "plugin run$r exit=$?"
  (cd $W/tooling/plugin-conformance && find vectors -type f | LC_ALL=C sort | xargs sha256sum) > $L/CU-C3-plugregen-run$r.sha
done
cmp $L/CU-C3-plugregen-run1.sha $L/CU-C3-plugregen-run2.sha && echo "plugin deterministic=yes" || echo "plugin deterministic=no"
echo REGEN2 DONE
