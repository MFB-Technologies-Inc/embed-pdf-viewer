#!/usr/bin/env python3
"""Install dependencies and prepare the TypeScript and Rust development tools."""

import os
from pathlib import Path
import subprocess
import sys


def run(*command, check=True):
    return subprocess.run(command, check=check)


def configure_git_identity():
    """Set the commit identity from EMBEDPDF_GIT_NAME/EMAIL unless one is already set."""
    for key, variable in (("user.name", "EMBEDPDF_GIT_NAME"), ("user.email", "EMBEDPDF_GIT_EMAIL")):
        value = os.environ.get(variable, "").strip()
        current = subprocess.run(
            ["git", "config", "--global", key], capture_output=True, text=True
        ).stdout.strip()
        if value and not current:
            run("git", "config", "--global", key, value)


def main():
    os.chdir(Path(__file__).resolve().parent.parent)
    configure_git_identity()
    # Public dependencies need no publishing token; preserve one if supplied.
    os.environ.setdefault("NPM_TOKEN", "")
    subprocess.run(
        ["pnpm", "install", "--frozen-lockfile"],
        env={**os.environ, "CI": "true"},
        check=True,
    )

    # Rust links the pinned fork; the TypeScript reference needs its WASM payload.
    runtime_target = subprocess.check_output(
        ["node", "-p", "'linux-' + process.arch"], text=True
    ).strip()
    run("bash", "packages/engine/runtime/scripts/fetch-libpdfium.sh", runtime_target)

    # This artifact is still pending in the fork, matching build/test-all.sh.
    pending = run(
        "bash", "packages/engine/runtime/scripts/fetch-libpdfium.sh", "wasm32-eh",
        check=False,
    )
    if pending.returncode:
        print("skipped: wasm32-eh is pending in engine-runtime-build.json", file=sys.stderr)

    # scripts/publish-engine-payload.mjs links the Rust-core payload with the emsdk in
    # perf-compare/.work; point it at the image's wasm32-eh emsdk instead of a fresh clone.
    work_emsdk = Path(os.environ.get("PERF_WORK_DIR", "perf-compare/.work")) / "emsdk"
    image_emsdk = os.environ.get("EMSDK_EH")
    if image_emsdk and not work_emsdk.exists() and not work_emsdk.is_symlink():
        work_emsdk.parent.mkdir(parents=True, exist_ok=True)
        work_emsdk.symlink_to(image_emsdk)

    run("bash", "packages/engine/runtime/scripts/fetch-payload.sh", "wasm32")
    # The trailing "..." builds the engine's workspace dependencies first, in order,
    # so a new one (such as @embedpdf/core-fonts) needs no change here.
    run("pnpm", "--filter", "@embedpdf/engine...", "build")

    # The engine worker and stamp locales used by the Rust kernel eval app.
    run("pnpm", "--filter", "@embedpdf/example-rust-kernel-eval", "bootstrap")

    # Browser versions follow the workspace's Playwright dependency.
    run(
        "pnpm", "--filter", "@embedpdf/tooling-viewer-e2e", "exec", "playwright",
        "install", "--with-deps", "chromium", "firefox", "webkit",
    )
    run("cargo", "fetch", "--locked")
    print("\nReady: cargo test -p epdf-engine; pnpm --filter @embedpdf/engine test")


if __name__ == "__main__":
    main()
