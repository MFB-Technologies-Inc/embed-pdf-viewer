#!/usr/bin/env bash
# Uploads artifacts staged by android-consumer.sh to a GitHub Packages Maven
# registry, so the release publishes the files the consumer check built the
# demo from rather than a second build of them.
#
# Usage: publish-staged.sh <group-dir> <owner/repo>
#   group-dir   the staged `com/embedpdf` directory: <artifact>/<version>/<files>
#   owner/repo  the repository whose registry receives them
#
# GITHUB_ACTOR and GITHUB_TOKEN authenticate, as for Gradle's publish; the
# token needs write:packages on owner/repo. MAVEN_REGISTRY_URL replaces
# https://maven.pkg.github.com, for the tests.
#
# Before uploading anything it asks the registry for each artifact's POM. A
# registry version can't be replaced, so an `embedpdf-bindings` engine already
# there is skipped and any other artifact already there stops the run. Each
# file goes up with HTTP PUT, followed by its .md5, .sha1, .sha256 and
# .sha512, as Gradle's maven-publish sends them; the POM of a version goes
# last, after the files it describes. Maven Local's maven-metadata-local.xml stays behind.
set -euo pipefail

if [[ $# -ne 2 || ! -d "$1" || ! "$2" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]]; then
  echo "usage: $0 <group-dir> <owner/repo>" >&2
  exit 64
fi
GROUP_DIR="$(cd "$1" && pwd)"
BASE="${MAVEN_REGISTRY_URL:-https://maven.pkg.github.com}/$2/com/embedpdf"
: "${GITHUB_ACTOR:?set GITHUB_ACTOR}"
: "${GITHUB_TOKEN:?set GITHUB_TOKEN}"

# Writes the token to a curl config file rather than the command line.
AUTH="$(mktemp)"
trap 'rm -f "$AUTH"' EXIT
printf 'user = "%s:%s"\n' "$GITHUB_ACTOR" "$GITHUB_TOKEN" > "$AUTH"

status_of() {
  curl -sS -o /dev/null -w '%{http_code}' -K "$AUTH" "$1"
}

put() {
  local file="$1" url="$2" status
  status="$(curl -sS -o /dev/null -w '%{http_code}' -K "$AUTH" -X PUT --upload-file "$file" "$url")"
  [[ "$status" == 2?? ]] || { echo "PUT $url returned HTTP $status" >&2; exit 1; }
}

versions=()
while IFS= read -r dir; do
  versions+=("$dir")
done < <(cd "$GROUP_DIR" && find . -mindepth 2 -maxdepth 2 -type d | sed 's#^\./##' | LC_ALL=C sort)
if [[ ${#versions[@]} -eq 0 ]]; then
  echo "nothing staged in $GROUP_DIR" >&2
  exit 1
fi

# Every check first, so a refusal uploads nothing.
uploads=()
for entry in "${versions[@]}"; do
  name="${entry%%/*}"
  version="${entry#*/}"
  pom="$name-$version.pom"
  [[ -f "$GROUP_DIR/$entry/$pom" ]] || { echo "no $pom in $GROUP_DIR/$entry" >&2; exit 1; }
  status="$(status_of "$BASE/$entry/$pom")"
  case "$status:$name" in
    404:*) uploads+=("$entry") ;;
    200:embedpdf-bindings) echo "skipping $name $version: already published" ;;
    200:*) echo "$name $version is already published to $2" >&2; exit 1 ;;
    *) echo "asking $2 for $name $version returned HTTP $status" >&2; exit 1 ;;
  esac
done

checksum() {
  case "$1" in
    md5) md5sum "$2" ;;
    sha1) sha1sum "$2" ;;
    sha256) sha256sum "$2" ;;
    sha512) sha512sum "$2" ;;
  esac | awk '{ printf "%s", $1 }'
}

SUMS="$(mktemp -d)"
trap 'rm -f "$AUTH"; rm -rf "$SUMS"' EXIT
for entry in "${uploads[@]}"; do
  name="${entry%%/*}"
  version="${entry#*/}"
  pom="$name-$version.pom"
  files=()
  while IFS= read -r file; do
    files+=("$file")
  done < <(cd "$GROUP_DIR/$entry" && find . -maxdepth 1 -type f ! -name 'maven-metadata*' ! -name "$pom" |
    sed 's#^\./##' | LC_ALL=C sort)
  files+=("$pom")
  for file in "${files[@]}"; do
    put "$GROUP_DIR/$entry/$file" "$BASE/$entry/$file"
    for algorithm in md5 sha1 sha256 sha512; do
      checksum "$algorithm" "$GROUP_DIR/$entry/$file" > "$SUMS/$file.$algorithm"
      put "$SUMS/$file.$algorithm" "$BASE/$entry/$file.$algorithm"
    done
  done
  echo "published $name $version (${#files[@]} files)"
done
