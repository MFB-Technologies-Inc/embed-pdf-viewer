#!/usr/bin/env bash
# Build the development image, tagged by date and commit and labelled with the commit,
# so `docker inspect` on a container shows which revision its image came from.
# Extra arguments go to `docker build`, for example `--no-cache`.
set -euo pipefail

root="$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)"
revision="$(git -C "$root" rev-parse HEAD)"
dirty=""
if ! git -C "$root" diff --quiet HEAD -- .devcontainer package.json rust-toolchain.toml tooling/viewer-e2e/package.json; then
  dirty="-dirty"
fi
tag="$(date -u +%Y%m%d)-${revision:0:12}${dirty}"

docker build -f "$root/.devcontainer/Dockerfile" \
  --build-arg "VCS_REF=${revision}${dirty}" \
  --build-arg "BUILD_DATE=$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
  -t "embedpdf-dev:${tag}" -t embedpdf-dev:latest \
  "$@" "$root"
echo "Built embedpdf-dev:${tag} (also tagged latest)"
