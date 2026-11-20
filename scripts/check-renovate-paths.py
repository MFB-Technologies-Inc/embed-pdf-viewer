#!/usr/bin/env python3
"""Check renovate.json's includePaths against what upstream owns.

Usage: check-renovate-paths.py UPSTREAM_REF

UPSTREAM_REF is a commit of embedpdf/embed-pdf-viewer main, such as FETCH_HEAD after
`git fetch --depth=1 --filter=blob:none <upstream> main`. Any file that also exists
there is upstream-owned. The check fails when:

- an includePaths entry covers an upstream-owned file (Renovate would edit it),
- an includePaths entry covers no tracked file (stale), or
- a fork-owned dependency manifest is neither covered nor listed in EXCLUDED below,
  so Renovate would silently skip it.
"""

from pathlib import Path
import json
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parent.parent

# Fork-owned manifests Renovate deliberately leaves alone, with the reason.
EXCLUDED = {
    "packages/core-wasm/": "pnpm workspace member; versions live in the upstream pnpm-lock.yaml",
    "packages/rust-viewer-bundle/": "pnpm workspace member; versions live in the upstream pnpm-lock.yaml",
    "examples/rust-kernel-eval/": "pnpm workspace member; versions live in the upstream pnpm-lock.yaml",
    "tooling/engine-baseline/": "pnpm workspace member; versions live in the upstream pnpm-lock.yaml",
    "tooling/engine-conformance/": "pnpm workspace member; versions live in the upstream pnpm-lock.yaml",
    "tooling/plugin-conformance/": "pnpm workspace member; versions live in the upstream pnpm-lock.yaml",
    "tooling/viewer-e2e/": "pnpm workspace member; versions live in the upstream pnpm-lock.yaml",
    "tooling/wasm-eh-check/": "pnpm workspace member; versions live in the upstream pnpm-lock.yaml",
    "perf-compare/": "Playwright pinned to the Chromium perf-compare/RESULTS.md was measured on",
    "pdfium-binding-benchmarks/": "frozen record",
    "pdfium-multiplatform-prototype/": "frozen record",
    "viewer-rust-port-benchmarks/": "frozen record",
    "docs/research/": "frozen record",
    "Package.swift": "binaryTarget pins only; nothing for Renovate to manage",
}

# Files a Renovate manager reads, by name or by path.
MANIFEST = re.compile(
    r"(^|/)("
    r"package\.json|Cargo\.toml|Cargo\.lock|rust-toolchain(\.toml)?|mise\.toml|\.tool-versions"
    r"|Dockerfile[^/]*|[^/]*\.dockerfile|docker-compose[^/]*\.ya?ml"
    r"|[^/]*\.csproj|[^/]*\.fsproj|global\.json|Directory\.Packages\.props|Package\.swift"
    r"|[^/]*\.gradle(\.kts)?|gradle\.properties|libs\.versions\.toml|gradle-wrapper\.properties"
    r"|go\.mod|requirements[^/]*\.txt|pyproject\.toml|Gemfile|Podfile|project\.yml"
    r")$"
    r"|^\.github/workflows/[^/]+\.ya?ml$|^\.github/actions/.+/action\.ya?ml$"
)


def git_files(*args):
    output = subprocess.run(["git", *args], cwd=ROOT, check=True, capture_output=True, text=True).stdout
    return set(output.splitlines())


def glob_regex(pattern):
    """minimatch-style glob (`**` crosses directories, `*` does not) as a regex."""
    out, i = "", 0
    while i < len(pattern):
        if pattern.startswith("**/", i):
            out, i = out + "(?:.*/)?", i + 3
        elif pattern.startswith("**", i):
            out, i = out + ".*", i + 2
        elif pattern[i] == "*":
            out, i = out + "[^/]*", i + 1
        elif pattern[i] == "?":
            out, i = out + "[^/]", i + 1
        else:
            out, i = out + re.escape(pattern[i]), i + 1
    return re.compile(out + "$")


def covers(pattern, path):
    # Renovate matches includePaths as a glob or as a plain path prefix.
    return path.startswith(pattern) or bool(glob_regex(pattern).match(path))


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    upstream = git_files("ls-tree", "-r", "--name-only", sys.argv[1])
    tracked = git_files("ls-files")
    include = json.loads((ROOT / "renovate.json").read_text())["includePaths"]
    failures = []

    for pattern in include:
        hits = [path for path in tracked if covers(pattern, path)]
        if not hits:
            failures.append(f"includePaths {pattern!r} covers no tracked file")
        owned_upstream = sorted(path for path in hits if path in upstream and MANIFEST.search(path))
        if owned_upstream:
            failures.append(f"includePaths {pattern!r} covers upstream-owned {', '.join(owned_upstream[:5])}")

    for path in sorted(tracked - upstream):
        if not MANIFEST.search(path):
            continue
        if any(covers(pattern, path) for pattern in include):
            continue
        if any(path == key or path.startswith(key) for key in EXCLUDED):
            continue
        failures.append(
            f"{path} is fork-owned but Renovate skips it: add it to includePaths in renovate.json, "
            "or to EXCLUDED in scripts/check-renovate-paths.py with the reason"
        )

    if failures:
        print("renovate.json includePaths disagree with what the fork owns:", file=sys.stderr)
        for failure in failures:
            print(f"  {failure}", file=sys.stderr)
        return 1
    print(f"includePaths cover every fork-owned manifest and no upstream-owned one ({len(include)} entries).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
