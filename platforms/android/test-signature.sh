#!/usr/bin/env bash
# Runs the signature transport tests and the route tests (SignatureRoutesTest)
# through Gradle, failing rather than skipping when node or the
# packages/core/signature dist is missing. The Gradle test starts
# tests/signature-routes.mjs itself.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
[[ -f "$ROOT/packages/core/signature/dist/server.js" ]] || {
  echo "build packages/core/signature first: pnpm --filter @embedpdf/core-signature build" >&2
  exit 1
}
cd "$ROOT/platforms/android/embedpdf"
EMBEDPDF_SIGNATURE_ROUTES_REQUIRED=1 ./gradlew :embedpdf:testDebugUnitTest \
  --tests com.embedpdf.HttpSignatureTransportTest --tests com.embedpdf.SignatureRoutesTest "$@"
