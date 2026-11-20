#!/usr/bin/env bash
# Writes the font manifest the linux-arm64 .NET package is packed with: Debian's
# fonts-dejavu-core DejaVu Sans as the `parity-dejavu` fallback, the font the
# parity fixtures use (tooling/dotnet-parity/fixtures/generate.mjs). The
# checked-in packaging/fonts.json is empty, so pack.sh takes this one through
# --font-manifest. The hashes are computed here, from the installed package.
#
# Usage: ci-fonts.sh <output fonts.json>
#
# Write the manifest outside platforms/dotnet: package.py hashes untracked
# files there into the package's source identity and would mark it dirty.
set -euo pipefail

out=${1:?usage: ci-fonts.sh <output fonts.json>}
font=/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf
notice=/usr/share/doc/fonts-dejavu-core/copyright

for path in "$font" "$notice"; do
  if [[ ! -f "$path" ]]; then
    echo "missing $path: install Debian's fonts-dejavu-core" >&2
    exit 1
  fi
done

font_sha=$(sha256sum "$font" | cut -d' ' -f1)
notice_sha=$(sha256sum "$notice" | cut -d' ' -f1)

mkdir -p "$(dirname "$out")"
# Same layout as package.py's json.dumps(indent=2), so the bytes match a
# manifest written by Python.
cat > "$out" <<JSON
{
  "schemaVersion": 1,
  "fonts": [
    {
      "path": "$font",
      "sha256": "$font_sha",
      "notice": "$notice",
      "noticeSha256": "$notice_sha",
      "key": "parity-dejavu",
      "familyName": "DejaVu Sans",
      "weight": 400,
      "italic": false,
      "fallback": true
    }
  ]
}
JSON
