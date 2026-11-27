#!/usr/bin/env bash
# CU-C2 (2026-11-27b), unchanged from 2026-11-27 scripts/prep-extra.sh.
# usage: prep-extra.sh <checkout>   the linux native addon and Playwright's browsers, for the TS suites
set -uo pipefail
cd "$1" || exit 2
export CARGO_TARGET_DIR="${CARGO_TARGET_DIR:-/workspaces/target}" PATH="/workspaces/bin:$PATH" CI=true NPM_TOKEN=""
pnpm --filter @embedpdf/engine-runtime build:target linux-$(node -p process.arch) || echo "FAILED native addon"
pnpm --filter @embedpdf/tooling-viewer-e2e exec playwright install --with-deps chromium firefox webkit || echo "FAILED playwright"
echo "EXTRA DONE"
