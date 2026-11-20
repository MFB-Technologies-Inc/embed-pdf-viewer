#!/usr/bin/env bash
# Prints, as markdown for $GITHUB_STEP_SUMMARY, one release the release train
# is about to publish, for its summarize job. GitHub's approval dialog names
# only the environment, and a gated job's steps don't run until it is
# approved, so the job before the gate writes this for the reviewers.
#
#   release-summary.sh release <tag> <title> <target> [asset-path ...]
#   release-summary.sh pin <tag>
#
# release: the release's tag, title and target commit, and each asset with its
# size and SHA-256. Every release the train makes is a prerelease. A target
# that isn't a full sha (a commit not made yet) is printed as given.
# pin: the diff of each Apple manifest in the working tree against HEAD, which
# the train's pin job commits as "build(apple): pin <tag>".
#
# env: GITHUB_REPOSITORY  owner/repo, for the links
#      GITHUB_REF_NAME    the branch pin pushes to (pin only)
#      DRY_RUN            true: say that nothing is published
set -euo pipefail

fail() { echo "::error::$*" >&2; exit 1; }

: "${GITHUB_REPOSITORY:?GITHUB_REPOSITORY is not set}"
url=https://github.com/$GITHUB_REPOSITORY
dry=
[[ "${DRY_RUN:-}" == true ]] && dry=' (dry run)'

sha256() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | cut -d' ' -f1
  else
    shasum -a 256 "$1" | cut -d' ' -f1
  fi
}

case "${1:-}" in
  release)
    [[ $# -ge 4 ]] || fail "usage: release-summary.sh release <tag> <title> <target> [asset-path ...]"
    tag=$2 title=$3 target=$4
    shift 4
    for asset in "$@"; do
      [[ -f "$asset" ]] || fail "no asset at $asset"
    done
    echo "## $title$dry"
    echo
    echo "| Release | |"
    echo "| --- | --- |"
    echo "| Tag | [\`$tag\`]($url/releases/tag/$tag) |"
    echo "| Title | $title |"
    if [[ "$target" =~ ^[0-9a-f]{40}$ ]]; then
      echo "| Target commit | [\`$target\`]($url/commit/$target) |"
    else
      echo "| Target commit | $target |"
    fi
    echo "| Prerelease | yes |"
    echo
    if [[ $# -eq 0 ]]; then
      echo "No assets."
    else
      echo "| Asset | Bytes | SHA-256 |"
      echo "| --- | ---: | --- |"
      for asset in "$@"; do
        echo "| \`${asset##*/}\` | $(wc -c <"$asset" | tr -d ' ') | \`$(sha256 "$asset")\` |"
      done
    fi
    if [[ -n "$dry" ]]; then
      echo
      echo "Dry run: this release is not made."
    fi
    echo
    ;;
  pin)
    [[ $# -eq 2 ]] || fail "usage: release-summary.sh pin <tag>"
    tag=$2
    : "${GITHUB_REF_NAME:?GITHUB_REF_NAME is not set}"
    echo "## pin $tag$dry"
    echo
    manifests=(Package.swift platforms/apple/EmbedPDF/Package.swift)
    head=$(git rev-parse HEAD)
    commit="[\`$head\`]($url/commit/$head)"
    if git diff --quiet HEAD -- "${manifests[@]}"; then
      echo "Both Apple manifests already pin \`$tag\` at $commit; pin commits nothing."
    else
      if [[ -n "$dry" ]]; then
        echo "Dry run: pin commits these changes on $commit as \`build(apple): pin $tag\` and does not push it."
      else
        echo "pin commits these changes on $commit as \`build(apple): pin $tag\` and pushes it to [\`$GITHUB_REF_NAME\`]($url/tree/$GITHUB_REF_NAME)."
      fi
      for manifest in "${manifests[@]}"; do
        echo
        if git diff --quiet HEAD -- "$manifest"; then
          echo "\`$manifest\`: unchanged."
          continue
        fi
        echo "\`$manifest\`:"
        echo
        echo '```diff'
        git diff HEAD -- "$manifest"
        echo '```'
      done
    fi
    echo
    ;;
  *)
    fail "usage: release-summary.sh release|pin ..."
    ;;
esac
