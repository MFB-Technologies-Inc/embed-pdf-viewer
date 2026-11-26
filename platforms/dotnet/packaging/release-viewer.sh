#!/usr/bin/env bash
# Publishes the five viewer packages of one release: pushes them to the GitHub
# Packages NuGet feed of GH_REPO's owner, then tags <sha> as the prerelease
# <version> on GH_REPO with the packages, their .sha256 files and the merged
# runtime manifest. The release train's dotnet-viewer-release job runs it after
# approve; release-dotnet.yml's viewer-release-preview job runs it with
# --dry-run.
#
# Usage: release-viewer.sh <packages-dir> <version> <sha> [--dry-run]
#   packages-dir  EmbedPDF.Runtime.0.0.0-engine.<hash>.nupkg, EmbedPDF,
#                 EmbedPDF.Models, EmbedPDF.Extensions.DependencyInjection and
#                 EmbedPDF.Scripting.Jint at <version>, and
#                 package-manifest.runtime.json
#   version       0.0.0-dotnet.<n>; docs/releases/<version>.md holds the notes
#   sha           the full commit the packages were built from
#   --dry-run     print every push, tag and release command instead (DRY_RUN=true
#                 does the same)
#
# env: GH_REPO      owner/repo; the feed is https://nuget.pkg.github.com/<owner>/index.json
#      GH_TOKEN     a token that can write packages, tags and releases there
#      EPDF_DOTNET  the dotnet binary (default dotnet)
#
# Packages go in dependency order, Runtime first, so the feed never holds one
# whose dependency is missing. --skip-duplicate makes a re-run pass over the
# packages already pushed, as github-release.sh skips a complete release.
#
# Exit 64: bad arguments. 66: a package or the manifest is missing. 1: a package
# fails its checks, the tag or release is in a state github-release.sh check
# refuses, or a push or the release fails.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET="${EPDF_DOTNET:-dotnet}"
PYTHON="${PYTHON:-python3}"

usage() { echo "usage: $0 <packages-dir> <version> <sha> [--dry-run]" >&2; exit 64; }
[[ $# == 3 || ( $# == 4 && "${4:-}" == --dry-run ) ]] || usage
dir=$1 version=$2 sha=$3
[[ $# == 4 ]] && export DRY_RUN=true
[[ "$version" =~ ^0\.0\.0-dotnet\.[0-9]+$ ]] || { echo "version must look like 0.0.0-dotnet.1, got '$version'" >&2; exit 64; }
[[ "$sha" =~ ^[0-9a-f]{40}$ ]] || { echo "sha must be a full commit sha, got '$sha'" >&2; exit 64; }
[[ "${GH_REPO:-}" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]] || { echo "GH_REPO must be owner/repo, got '${GH_REPO:-}'" >&2; exit 64; }
[[ -d "$dir" ]] || { echo "no packages directory: $dir" >&2; exit 66; }
dir="$(cd "$dir" && pwd)"

runtimes=()
for path in "$dir"/EmbedPDF.Runtime.*.nupkg; do [[ -f "$path" ]] && runtimes+=("$path"); done
[[ ${#runtimes[@]} == 1 ]] || { echo "want one EmbedPDF.Runtime.*.nupkg in $dir, found ${#runtimes[@]}" >&2; exit 66; }
runtime_version=$(basename "${runtimes[0]}" .nupkg); runtime_version=${runtime_version#EmbedPDF.Runtime.}
[[ "$runtime_version" =~ ^0\.0\.0-engine\.[0-9a-f]{16}$ ]] || { echo "EmbedPDF.Runtime must be 0.0.0-engine.<16 hex>, got $runtime_version" >&2; exit 1; }

packages=("${runtimes[0]}")
for id in EmbedPDF EmbedPDF.Models EmbedPDF.Extensions.DependencyInjection EmbedPDF.Scripting.Jint; do
  [[ -f "$dir/$id.$version.nupkg" ]] || { echo "missing $id.$version.nupkg in $dir" >&2; exit 66; }
  packages+=("$dir/$id.$version.nupkg")
done
manifest="$dir/package-manifest.runtime.json"
[[ -f "$manifest" ]] || { echo "missing package-manifest.runtime.json in $dir" >&2; exit 66; }

# Each package names this repository, so the feed links it there, and the
# dependency ranges are the ones the packers wrote; then the .sha256 files.
"$PYTHON" - "https://github.com/$GH_REPO" "$version" "$runtime_version" "${packages[@]}" <<'PY'
import hashlib, re, sys, zipfile
from pathlib import Path
repository, version, runtime_version, *packages = sys.argv[1:]
def fail(message):
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)
for path in map(Path, packages):
    with zipfile.ZipFile(path) as bundle:
        names = [n for n in bundle.namelist() if n.endswith(".nuspec") and "/" not in n]
        if len(names) != 1:
            fail(f"{path.name}: no nuspec")
        nuspec = bundle.read(names[0]).decode()
    found = re.search(r'<repository\b[^>]*\burl="([^"]*)"', nuspec)
    if not found or found.group(1).rstrip("/") != repository:
        fail(f"{path.name}: repository url is {found.group(1) if found else 'missing'}, not {repository}")
    def dependency(package_id):
        match = re.search(rf'<dependency id="{re.escape(package_id)}" version="([^"]*)"', nuspec)
        return match.group(1) if match else None
    if path.name.startswith("EmbedPDF.Models.") and dependency("EmbedPDF") != f"[{version}]":
        fail(f"{path.name}: depends on EmbedPDF {dependency('EmbedPDF')}, not exactly [{version}]")
    if path.name == f"EmbedPDF.{version}.nupkg" and dependency("EmbedPDF.Runtime") != runtime_version:
        fail(f"{path.name}: depends on EmbedPDF.Runtime {dependency('EmbedPDF.Runtime')}, not {runtime_version}")
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    Path(f"{path}.sha256").write_text(f"{digest}  {path.name}\n")
    print(f"{path.name}: {digest}")
PY

# The tag and release first, as preflight checks them: a draft, a partial
# release or a tag with no release stops here, before anything reaches the feed.
names=()
for package in "${packages[@]}"; do names+=("${package##*/}" "${package##*/}.sha256"); done
state=$(bash "$ROOT/scripts/release/github-release.sh" check "$version" "${names[@]}" "${manifest##*/}")
echo "release $version on $GH_REPO: $state"

feed="https://nuget.pkg.github.com/${GH_REPO%%/*}/index.json"
for package in "${packages[@]}"; do
  if [[ "${DRY_RUN:-}" == true ]]; then
    echo "dry run: would run $DOTNET nuget push $package --source $feed --api-key *** --skip-duplicate"
  else
    : "${GH_TOKEN:?GH_TOKEN is not set}"
    "$DOTNET" nuget push "$package" --source "$feed" --api-key "$GH_TOKEN" --skip-duplicate
  fi
done

assets=()
for package in "${packages[@]}"; do assets+=("$package" "$package.sha256"); done
bash "$ROOT/scripts/release/github-release.sh" publish "$version" "$sha" "C# viewer $version" \
  "$ROOT/docs/releases/$version.md" "${assets[@]}" "$manifest"
