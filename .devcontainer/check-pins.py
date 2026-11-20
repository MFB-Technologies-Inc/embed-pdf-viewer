#!/usr/bin/env python3
"""Fail when a development-image pin disagrees with the repository file that owns it.

Each check names a value in .devcontainer/Dockerfile and the files that must carry the
same value. Bump them together; this script only reports the ones that drifted.
"""

from pathlib import Path
import re
import sys
import tomllib

ROOT = Path(__file__).resolve().parent.parent
DOCKERFILE = ROOT / ".devcontainer/Dockerfile"


def read(path):
    return (ROOT / path).read_text()


def match(path, pattern):
    found = re.search(pattern, read(path), re.MULTILINE)
    return found.group(1) if found else None


def arg(name):
    return match(".devcontainer/Dockerfile", rf"^ARG {name}=(\S+)$")


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
    dotnet_sha = match(dotnet, r"^\s*SDK_SHA512: (\S+)$")
    if dotnet_sha is None or dotnet_sha not in DOCKERFILE.read_text():
        failures.append(f".NET SDK arm64 SHA-512: {dotnet} has {dotnet_sha}, absent from the Dockerfile")

    if failures:
        print("Development image pins disagree with the repository:", file=sys.stderr)
        for failure in failures:
            print(f"  {failure}", file=sys.stderr)
        return 1
    print("Development image pins match the repository.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
