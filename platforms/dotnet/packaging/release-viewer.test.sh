#!/usr/bin/env bash
# Tests for release-viewer.sh, with stand-in dotnet, gh and git, so nothing is
# pushed or tagged. Run from anywhere:
#   bash platforms/dotnet/packaging/release-viewer.test.sh
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "${TMP:?}"' EXIT
failures=0
pass() { echo "PASS: $1"; }
fail() { echo "FAIL: $1"; failures=$((failures + 1)); }
expect_exit() {
  local name=$1 want=$2; shift 2
  "$@" >"$TMP/out" 2>&1
  local got=$?
  if [[ $got == "$want" ]]; then pass "$name"; else fail "$name (exit $got, want $want)"; cat "$TMP/out"; fi
}
expect_output() { grep -q -- "$2" "$TMP/out" && pass "$1" || { fail "$1"; cat "$TMP/out"; }; }
refute_output() { grep -q -- "$2" "$TMP/out" && { fail "$1"; cat "$TMP/out"; } || pass "$1"; }

V=0.0.0-dotnet.7
R=0.0.0-engine.0123456789abcdef
SHA=$(printf 'a%.0s' {1..40})
export GH_REPO=owner/repo GH_TOKEN=secret-token-value

# A nupkg holding only a nuspec. $3 is the repository url, $4 the dependencies.
nupkg() {
  python3 - "$1" "$2" "$3" "$4" <<'PY'
import sys, zipfile
path, package_id, url, deps = sys.argv[1:]
repo = f'<repository type="git" url="{url}" />' if url else ""
with zipfile.ZipFile(path, "w") as z:
    z.writestr(f"{package_id}.nuspec", f'<package><metadata><id>{package_id}</id>{repo}<dependencies>{deps}</dependencies></metadata></package>')
PY
}
feed() {
  local d=$1 url=${2-https://github.com/owner/repo} models=${3-[$V]}
  mkdir -p "$d"
  nupkg "$d/EmbedPDF.Runtime.$R.nupkg" EmbedPDF.Runtime "$url" ""
  nupkg "$d/EmbedPDF.$V.nupkg" EmbedPDF "$url" "<dependency id=\"EmbedPDF.Runtime\" version=\"$R\" />"
  nupkg "$d/EmbedPDF.Models.$V.nupkg" EmbedPDF.Models "$url" "<dependency id=\"EmbedPDF\" version=\"$models\" />"
  nupkg "$d/EmbedPDF.Extensions.DependencyInjection.$V.nupkg" EmbedPDF.Extensions.DependencyInjection "$url" "<dependency id=\"EmbedPDF\" version=\"$V\" />"
  nupkg "$d/EmbedPDF.Scripting.Jint.$V.nupkg" EmbedPDF.Scripting.Jint "$url" "<dependency id=\"EmbedPDF\" version=\"$V\" />"
  echo '{}' >"$d/package-manifest.runtime.json"
}

# Stand-ins: dotnet and git log their arguments; gh reports no release and no tag.
fake="$TMP/fake"
mkdir -p "$fake"
printf '#!/usr/bin/env bash\necho "dotnet $*" >>"%s/calls"\n' "$TMP" >"$fake/dotnet"
printf '#!/usr/bin/env bash\necho "git $*" >>"%s/calls"\n' "$TMP" >"$fake/git"
cat >"$fake/gh" <<FAKE
#!/usr/bin/env bash
echo "gh \$*" >>"$TMP/calls"
case "\$1 \$2" in
  "release view") echo "release not found" >&2; exit 1 ;;
  "api "*) echo "[]" ;;
  *) exit 0 ;;
esac
FAKE
chmod +x "$fake"/*
export PATH="$fake:$PATH"
S="$HERE/release-viewer.sh"

expect_exit "no arguments exits 64" 64 bash "$S"
expect_exit "a bad version exits 64" 64 bash "$S" "$TMP" 1.0.0 "$SHA" --dry-run
expect_exit "a short sha exits 64" 64 bash "$S" "$TMP" "$V" abc --dry-run
expect_exit "a missing directory exits 66" 66 bash "$S" "$TMP/none" "$V" "$SHA" --dry-run

feed "$TMP/partial"; rm "$TMP/partial/EmbedPDF.Scripting.Jint.$V.nupkg"
expect_exit "a missing package exits 66" 66 bash "$S" "$TMP/partial" "$V" "$SHA" --dry-run
expect_output "the missing package is named" "EmbedPDF.Scripting.Jint.$V.nupkg"

feed "$TMP/range" https://github.com/owner/repo "$V"
expect_exit "a minimum Models range fails" 1 bash "$S" "$TMP/range" "$V" "$SHA" --dry-run
expect_output "the range is named" "not exactly \[$V\]"

feed "$TMP/norepo" ""
expect_exit "a package without a repository url fails" 1 bash "$S" "$TMP/norepo" "$V" "$SHA" --dry-run
expect_output "the url is named" "https://github.com/owner/repo"

feed "$TMP/other" https://github.com/someone/else
expect_exit "a package naming another repository fails" 1 bash "$S" "$TMP/other" "$V" "$SHA" --dry-run

feed "$TMP/good"
rm -f "$TMP/calls"
expect_exit "a dry run passes" 0 bash "$S" "$TMP/good" "$V" "$SHA" --dry-run
grep 'would run dotnet nuget push' "$TMP/out" | awk '{print $8}' | xargs -n1 basename >"$TMP/order"
printf '%s\n' "EmbedPDF.Runtime.$R.nupkg" "EmbedPDF.$V.nupkg" "EmbedPDF.Models.$V.nupkg" \
  "EmbedPDF.Extensions.DependencyInjection.$V.nupkg" "EmbedPDF.Scripting.Jint.$V.nupkg" >"$TMP/want"
diff -q "$TMP/order" "$TMP/want" >/dev/null && pass "a dry run pushes Runtime first, then EmbedPDF, then the libraries" || { fail "push order"; cat "$TMP/order"; }
expect_output "the feed is the owner's" "--source https://nuget.pkg.github.com/owner/index.json"
expect_output "the key is masked" "--api-key \*\*\*"
refute_output "the token never prints" "secret-token-value"
expect_output "the release is printed" "would run gh release create $V"
expect_output "the tag is printed" "would run git tag $V $SHA"
grep -q "nuget push" "$TMP/calls" 2>/dev/null && fail "a dry run calls dotnet nuget push" || pass "a dry run calls no push"
[[ -s "$TMP/good/EmbedPDF.$V.nupkg.sha256" ]] && pass "checksums are written" || fail "checksums are written"

mkdir -p "$TMP/notes"
rm -f "$TMP/calls"
# A real run with the stand-ins: five pushes, then the tag and the release.
NOTES="$HERE/../../../docs/releases/$V.md"
touch "$NOTES"
expect_exit "a real run passes" 0 bash "$S" "$TMP/good" "$V" "$SHA"
rm -f "$NOTES"
[[ $(grep -c "^dotnet nuget push" "$TMP/calls") == 5 ]] && pass "a real run pushes five packages" || { fail "five pushes"; cat "$TMP/calls"; }
grep -q -- "--api-key secret-token-value --skip-duplicate" "$TMP/calls" && pass "a real push passes the token and skips duplicates" || fail "real push arguments"
grep -q "^gh release create $V" "$TMP/calls" && pass "a real run creates the release" || fail "release create"

echo
if ((failures)); then echo "$failures failed"; exit 1; fi
echo "all passed"
