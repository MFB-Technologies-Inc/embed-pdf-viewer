#!/usr/bin/env python3
"""Fail when a development-image pin disagrees with the repository file that owns it.

Each check names a value in .devcontainer/Dockerfile and the files that must carry the
same value. Bump them together; this script only reports the ones that drifted.

With --verify-checksums it also downloads the published checksums for the pinned
binaryen, rustup-init, .NET SDK and .NET runtime archives and compares them with the pinned ones.
Renovate bumps those versions but cannot compute their hashes, so its pull requests
fail here until the hashes are filled in.
"""

import argparse
import http.client
import json
from pathlib import Path
import re
import sys
import time
import tomllib
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parent.parent
DOCKERFILE = ROOT / ".devcontainer/Dockerfile"


def read(path):
    return (ROOT / path).read_text()


def match(path, pattern):
    found = re.search(pattern, read(path), re.MULTILINE)
    return found.group(1) if found else None


def arg(name):
    return match(".devcontainer/Dockerfile", rf"^ARG {name}=(\S+)$")


class FetchError(Exception):
    pass


def fetch(url, attempts=3):
    """GET url, retrying transient failures; FetchError names the URL when all fail."""
    request = urllib.request.Request(url, headers={"User-Agent": "embedpdf-check-pins"})
    for attempt in range(1, attempts + 1):
        try:
            with urllib.request.urlopen(request, timeout=20) as response:
                return response.read().decode()
        except urllib.error.HTTPError as error:
            retryable = error.code == 429 or error.code >= 500
            if not retryable or attempt == attempts:
                raise FetchError(f"{url}: HTTP {error.code}") from error
        except (urllib.error.URLError, TimeoutError, ConnectionError, http.client.HTTPException) as error:
            if attempt == attempts:
                raise FetchError(f"{url}: {error}") from error
        time.sleep(2 * attempt)


DOTNET_ACTION = ".github/actions/setup-dotnet/action.yml"
DOTNET_RIDS = ("linux-arm64", "linux-x64", "osx-arm64", "osx-x64", "win-x64")


def dotnet_pins():
    """The setup-dotnet action's SDK and runtime pins: versions, and per RID the two SHA-512s."""
    text = read(DOTNET_ACTION)
    rows = {
        rid: {"sdk": sdk, "runtime": runtime}
        for rid, sdk, runtime in re.findall(
            r"^\s*([a-z]+-[a-z0-9]+)\)\n\s*sdk_sha512=(\w+)\n\s*runtime_sha512=(\w+)$", text, re.MULTILINE
        )
    }
    return (match(DOTNET_ACTION, r"^\s*DOTNET_SDK_VERSION: (\S+)$"),
            match(DOTNET_ACTION, r"^\s*DOTNET_RUNTIME_VERSION: (\S+)$"),
            rows)


def dockerfile_dotnet_hashes():
    """The Dockerfile's SDK and runtime SHA-512s by .NET architecture (x64, arm64)."""
    return {
        dotnet_arch: {"sdk": sdk, "runtime": runtime}
        for dotnet_arch, sdk, runtime in re.findall(
            r"dotnet_arch=(\w+); dotnet_sha512=(\w+);\s*\\\n\s*dotnet_runtime_sha512=(\w+)", DOCKERFILE.read_text()
        )
    }


def verify_checksums(failures):
    def compare(label, pinned, published):
        if pinned != published:
            failures.append(f"{label}: pinned {pinned}, published {published}")

    binaryen = arg("BINARYEN_VERSION")
    for docker_arch, binaryen_arch in (("AMD64", "x86_64"), ("ARM64", "aarch64")):
        published = fetch(
            f"https://github.com/WebAssembly/binaryen/releases/download/version_{binaryen}/"
            f"binaryen-version_{binaryen}-{binaryen_arch}-linux.tar.gz.sha256"
        ).split()[0]
        compare(f"binaryen {binaryen} {binaryen_arch} SHA-256", arg(f"BINARYEN_SHA256_{docker_arch}"), published)

    rustup = arg("RUSTUP_VERSION")
    for docker_arch, triple in (("AMD64", "x86_64-unknown-linux-gnu"), ("ARM64", "aarch64-unknown-linux-gnu")):
        published = fetch(
            f"https://static.rust-lang.org/rustup/archive/{rustup}/{triple}/rustup-init.sha256"
        ).split()[0]
        compare(f"rustup-init {rustup} {triple} SHA-256", arg(f"RUSTUP_SHA256_{docker_arch}"), published)

    _, _, rows = dotnet_pins()
    dockerfile_hashes = dockerfile_dotnet_hashes()

    def published_files(kind, version):
        channel = ".".join(version.split(".")[:2])
        releases = json.loads(fetch(
            f"https://builds.dotnet.microsoft.com/dotnet/release-metadata/{channel}/releases.json"
        ))["releases"]
        if kind == "sdk":
            entries = (s for r in releases for s in r.get("sdks", []))
        else:
            entries = (r.get("runtime") or {} for r in releases)
        files = next((e["files"] for e in entries if e.get("version") == version), None)
        if files is None:
            failures.append(f".NET {kind} {version}: not in Microsoft's {channel} release metadata")
        return files

    for kind, label, version in (("sdk", "SDK", arg("DOTNET_SDK_VERSION")),
                                 ("runtime", "runtime", arg("DOTNET_RUNTIME_VERSION"))):
        files = published_files(kind, version)
        if files is None:
            continue
        prefix = f"dotnet-{kind}-{version}-"

        def published_sha512(rid):
            extension = ".zip" if rid.startswith("win-") else ".tar.gz"
            return next((f["hash"] for f in files if f["url"].endswith("/" + prefix + rid + extension)), None)

        for dotnet_arch in ("x64", "arm64"):
            compare(f".NET {label} {version} linux-{dotnet_arch} SHA-512 in the Dockerfile",
                    dockerfile_hashes.get(dotnet_arch, {}).get(kind), published_sha512(f"linux-{dotnet_arch}"))
        for rid, row in rows.items():
            compare(f".NET {label} {version} {rid} SHA-512 in {DOTNET_ACTION}",
                    row[kind], published_sha512(rid))

def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0], allow_abbrev=False)
    parser.add_argument("--verify-checksums", action="store_true",
                        help="also compare the pinned checksums with the published ones")
    args = parser.parse_args()
    failures = []

    def expect(label, image_value, path, other_value):
        if other_value is None:
            failures.append(f"{label}: no value found in {path}")
        elif image_value != other_value:
            failures.append(f"{label}: Dockerfile has {image_value}, {path} has {other_value}")

    node = match(".devcontainer/Dockerfile", r"^FROM node:([0-9.]+)-trixie-slim$")
    mise_node = tomllib.loads(read("mise.toml"))["tools"].get("node")
    expect("Node", node, "mise.toml", mise_node)

    emsdk = arg("EMSDK_VERSION")
    expect("emsdk", emsdk, ".github/workflows/build-engine-runtime.yml",
           match(".github/workflows/build-engine-runtime.yml", r"^\s*EMSDK_VERSION: (\S+)$"))
    emsdk_eh = arg("EMSDK_EH_VERSION")
    expect("emsdk wasm32-eh", emsdk_eh, "perf-compare/scripts/build-arm.mjs",
           match("perf-compare/scripts/build-arm.mjs", r"^const EH_EMSDK_VERSION = '([^']+)'"))
    # The runtime fork is a submodule; check it only when it is checked out.
    fork_pins = "packages/engine/runtime/runtime-src/scripts/embedpdf-runtime/emsdk-version.sh"
    if (ROOT / fork_pins).exists():
        expect("emsdk", emsdk, fork_pins, match(fork_pins, r'^\s*wasm32\) echo "([^"]+)"'))
        expect("emsdk wasm32-eh", emsdk_eh, fork_pins, match(fork_pins, r'^\s*wasm32-eh\) echo "([^"]+)"'))

    viewer = ".github/workflows/release-rust-viewer.yml"
    wasm_bindgen = arg("WASM_BINDGEN_VERSION")
    expect("wasm-bindgen", wasm_bindgen, "crates/epdf-web/Cargo.toml",
           match("crates/epdf-web/Cargo.toml", r'^wasm-bindgen = "=([^"]+)"'))
    expect("wasm-bindgen", wasm_bindgen, viewer, match(viewer, r"^\s*WASM_BINDGEN_VERSION: '?([^'\s]+)'?$"))
    expect("binaryen", arg("BINARYEN_VERSION"), viewer, match(viewer, r"^\s*BINARYEN_VERSION: '?([^'\s]+)'?$"))
    expect("binaryen x86_64 SHA-256", arg("BINARYEN_SHA256_AMD64"), viewer,
           match(viewer, r"^\s*BINARYEN_SHA256: (\S+)$"))

    sdk, runtime, rows = dotnet_pins()
    expect(".NET SDK", arg("DOTNET_SDK_VERSION"), DOTNET_ACTION, sdk)
    expect(".NET runtime", arg("DOTNET_RUNTIME_VERSION"), DOTNET_ACTION, runtime)
    expect(".NET SDK", arg("DOTNET_SDK_VERSION"), "platforms/dotnet/global.json",
           json.loads(read("platforms/dotnet/global.json"))["sdk"]["version"])
    # The action pins one SDK and one runtime archive per RID; the image
    # builds for the two Linux ones.
    for rid in DOTNET_RIDS:
        if rid not in rows:
            failures.append(f".NET {rid}: {DOTNET_ACTION} has no SDK and runtime SHA-512s")
    dockerfile_hashes = dockerfile_dotnet_hashes()
    for dotnet_arch in ("x64", "arm64"):
        for kind in ("sdk", "runtime"):
            expect(f".NET {kind} linux-{dotnet_arch} SHA-512",
                   dockerfile_hashes.get(dotnet_arch, {}).get(kind), DOTNET_ACTION,
                   rows.get(f"linux-{dotnet_arch}", {}).get(kind))

    if args.verify_checksums:
        try:
            verify_checksums(failures)
        except FetchError as error:
            failures.append(f"could not fetch a published checksum: {error}")

    if failures:
        print("Development image pins disagree with the repository:", file=sys.stderr)
        for failure in failures:
            print(f"  {failure}", file=sys.stderr)
        return 1
    print("Development image pins match the repository.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
