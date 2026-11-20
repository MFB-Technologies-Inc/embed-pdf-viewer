#!/usr/bin/env python3
"""Fail when a development-image pin disagrees with the repository file that owns it.

Each check names a value in .devcontainer/Dockerfile and the files that must carry the
same value. Bump them together; this script only reports the ones that drifted.

With --verify-checksums it also downloads the published checksums for the pinned
binaryen, rustup-init and .NET SDK archives and compares them with the pinned ones.
Renovate bumps those versions but cannot compute their hashes, so its pull requests
fail here until the hashes are filled in.
"""

import json
from pathlib import Path
import re
import sys
import tomllib
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


def fetch(url):
    request = urllib.request.Request(url, headers={"User-Agent": "embedpdf-check-pins"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read().decode()


def dotnet_matrix():
    """The release-dotnet.yml matrix rows as (rid, sdk_url, sdk_sha512)."""
    return re.findall(
        r"^\s*- rid: (\S+)\n(?:\s+\w+: .*\n)*?\s+sdk_url: (\S+)\n\s+sdk_sha512: (\S+)$",
        read(".github/workflows/release-dotnet.yml"),
        re.MULTILINE,
    )


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

    sdk = arg("DOTNET_SDK_VERSION")
    channel = ".".join(sdk.split(".")[:2])
    releases = json.loads(fetch(
        f"https://builds.dotnet.microsoft.com/dotnet/release-metadata/{channel}/releases.json"
    ))["releases"]
    files = next(
        (s["files"] for r in releases for s in r.get("sdks", []) if s["version"] == sdk), None
    )
    if files is None:
        failures.append(f".NET SDK {sdk}: not in Microsoft's {channel} release metadata")
        return

    def published_sha512(rid, extension):
        return next(
            (f["hash"] for f in files if f["rid"] == rid and f["name"].endswith(extension)), None
        )

    dockerfile_hashes = dict(re.findall(r"dotnet_arch=(\w+); dotnet_sha512=(\w+)", DOCKERFILE.read_text()))
    for dotnet_arch in ("x64", "arm64"):
        compare(f".NET SDK {sdk} linux-{dotnet_arch} SHA-512 in the Dockerfile",
                dockerfile_hashes.get(dotnet_arch), published_sha512(f"linux-{dotnet_arch}", ".tar.gz"))
    for rid, url, sha in dotnet_matrix():
        extension = ".zip" if url.endswith(".zip") else ".tar.gz"
        compare(f".NET SDK {sdk} {rid} SHA-512 in release-dotnet.yml", sha, published_sha512(rid, extension))


def main():
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

    dotnet = ".github/workflows/release-dotnet.yml"
    expect(".NET SDK", arg("DOTNET_SDK_VERSION"), dotnet,
           match(dotnet, r"^\s*- name: Install \.NET SDK (\S+)$"))
    # The workflow's matrix pins one SDK archive per RID; the image builds for
    # the two Linux ones.
    for rid in ("linux-arm64", "linux-x64"):
        dotnet_sha = match(dotnet, rf"^\s*- rid: {rid}\n(?:\s+\w+: .*\n)*?\s+sdk_sha512: (\S+)$")
        if dotnet_sha is None or dotnet_sha not in DOCKERFILE.read_text():
            failures.append(f".NET SDK {rid} SHA-512: {dotnet} has {dotnet_sha}, absent from the Dockerfile")
    for rid, url, _ in dotnet_matrix():
        if f"/Sdk/{arg('DOTNET_SDK_VERSION')}/" not in url:
            failures.append(f".NET SDK {rid}: {dotnet} downloads {url}")
    expect(".NET SDK", arg("DOTNET_SDK_VERSION"), "platforms/dotnet/global.json",
           json.loads(read("platforms/dotnet/global.json"))["sdk"]["version"])

    if "--verify-checksums" in sys.argv[1:]:
        verify_checksums(failures)

    if failures:
        print("Development image pins disagree with the repository:", file=sys.stderr)
        for failure in failures:
            print(f"  {failure}", file=sys.stderr)
        return 1
    print("Development image pins match the repository.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
