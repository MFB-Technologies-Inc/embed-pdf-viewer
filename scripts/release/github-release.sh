#!/usr/bin/env bash
# Tags a commit and makes a prerelease of it on this repository, for the
# release train's swift-release, viewer-release and dotnet-release jobs, and
# checks ahead of time whether a release is already out, for its preflight.
#
#   github-release.sh check <tag> [asset-name ...]
#   github-release.sh publish <tag> <sha> <title> <notes-file> [asset-path ...]
#
# env: GH_REPO   owner/repo the release lives on (gh reads it too)
#      GH_TOKEN  a token that can read it, and for publish write tags and
#                releases on it
#      DRY_RUN   true: publish prints the git and gh commands instead of
#                running them, and only warns when <notes-file> is missing
#
# A release is complete when it is not a draft and carries every named asset.
# check prints `absent` or `complete`; publish skips a complete release. Both
# stop on a draft or a partial release, which someone must finish or delete.
# check also stops on a tag with no release; publish accepts one that points
# at <sha>, which a run that failed between `git push` and `gh release create`
# leaves behind.
set -euo pipefail

fail() { echo "::error::$*" >&2; exit 1; }

: "${GH_REPO:?GH_REPO is not set}"

# The release's state: absent, complete, or a stop for anything else.
release_state() {
  local tag=$1 view
  shift
  if view=$(gh release view "$tag" -R "$GH_REPO" --json isDraft,assets 2>&1); then
    local names
    names=$(printf '%s\n' "$@" | jq -R . | jq -sc 'map(select(. != ""))')
    if jq -e --argjson want "$names" '(.isDraft | not) and ([.assets[].name] as $have | all($want[]; . as $n | $have | index($n)))' <<<"$view" >/dev/null; then
      echo complete
    else
      fail "$tag exists on $GH_REPO but is a draft or lacks some of its assets ($*); finish or delete it, then run again: $(jq -c '{isDraft, assets: [.assets[].name]}' <<<"$view")"
    fi
  elif grep -qi 'release not found' <<<"$view"; then
    echo absent
  else
    # Unknown (network, rate limit): don't act on a guess.
    fail "gh couldn't say whether release $tag exists on $GH_REPO: $view"
  fi
}

# The commit refs/tags/<tag> names on GH_REPO, or nothing when there is no such
# tag. Only lightweight tags are expected; an annotated one is refused.
remote_tag() {
  local tag=$1 refs
  refs=$(gh api "repos/$GH_REPO/git/matching-refs/tags/$tag") || fail "couldn't list tags $tag* on $GH_REPO"
  jq -r --arg ref "refs/tags/$tag" '
    map(select(.ref == $ref)) | if length == 0 then "" else .[0] |
      if .object.type == "commit" then .object.sha else "\(.object.type):\(.object.sha)" end end
  ' <<<"$refs"
}

run() {
  if [[ "${DRY_RUN:-}" == true ]]; then
    echo "dry run: would run $(printf '%q ' "$@")"
  else
    "$@"
  fi
}

case "${1:-}" in
  check)
    [[ $# -ge 2 ]] || fail "usage: github-release.sh check <tag> [asset-name ...]"
    tag=$2
    shift 2
    state=$(release_state "$tag" "$@")
    if [[ "$state" == absent ]]; then
      commit=$(remote_tag "$tag")
      [[ -z "$commit" ]] || fail "tag $tag already exists on $GH_REPO ($commit) with no release; delete it or pick another version"
    fi
    echo "$state"
    ;;
  publish)
    [[ $# -ge 5 ]] || fail "usage: github-release.sh publish <tag> <sha> <title> <notes-file> [asset-path ...]"
    tag=$2 sha=$3 title=$4 notes=$5
    shift 5
    assets=("$@")
    [[ "$sha" =~ ^[0-9a-f]{40}$ ]] || fail "<sha> must be a full commit sha, got '$sha'"
    # A dry run goes on without the notes, as preflight does: the release
    # command it prints names the file a real run needs.
    if [[ ! -f "$notes" ]]; then
      if [[ "${DRY_RUN:-}" == true ]]; then
        echo "::warning::no release notes at $notes; a real run stops here"
      else
        fail "no release notes at $notes"
      fi
    fi
    names=()
    for asset in "${assets[@]}"; do
      [[ -f "$asset" ]] || fail "no asset at $asset"
      names+=("${asset##*/}")
    done
    state=$(release_state "$tag" "${names[@]}")
    if [[ "$state" == complete ]]; then
      echo "$tag is already released on $GH_REPO with its assets; nothing to do."
      exit 0
    fi
    commit=$(remote_tag "$tag")
    if [[ -z "$commit" ]]; then
      run git tag "$tag" "$sha"
      run git push origin "refs/tags/$tag"
    elif [[ "$commit" == "$sha" ]]; then
      echo "tag $tag already names $sha on $GH_REPO; making its release."
    else
      fail "tag $tag on $GH_REPO names $commit, not $sha; delete it or pick another version"
    fi
    run gh release create "$tag" "${assets[@]}" -R "$GH_REPO" \
      --verify-tag --prerelease --target "$sha" --title "$title" --notes-file "$notes"
    ;;
  *)
    fail "usage: github-release.sh check|publish ..."
    ;;
esac
