# EmbedPDF development container

Use Docker directly with any editor or terminal. The compiler toolchains, browser
authentication, and Git credential forwarding work independently
of VS Code. An editor integration can automate setup and port forwarding; the
commands below perform those steps explicitly.

The container clones `https://github.com/MFB-Technologies-Inc/embed-pdf-viewer.git`, branch
`research/multi-platform-rust-core-impl-plugins`, into `/workspaces/embed-pdf-viewer`
inside the Docker volume `embedpdf-workspace`. The host repository is not mounted.
Dependencies, generated files, commits, and edits stay in that volume across
container rebuilds. Push commits to share them with another checkout. Removing
the volume deletes the internal checkout, including unpushed work.

The image contains the clone script, so initial creation works before the
repository exists in the volume. Existing clones are reused without pulling,
resetting, or switching branches. Pass `-e EMBEDPDF_REPOSITORY=URL` and
`-e EMBEDPDF_BRANCH=BRANCH` to `docker run` to select a different initial clone; those
settings do not change an existing clone. Container configuration and toolchain
pins used to build the image still come from the host checkout. To rebuild with
configuration changes made inside the container, push and pull them onto the host.

The existing `packages/engine/runtime/Dockerfile` builds PDFium and
`cloudpdf/server/Dockerfile` packages the server. This container supports development
of the TypeScript engine, Rust engine, geometry parity adapter, and web viewer.
It also supports the native engine C ABI and managed .NET binding toolchain.

## Start with Docker

Run these commands on the host from the repository root. Docker Desktop supplies
`host.docker.internal` on macOS and Windows. On Linux Docker Engine, add
`--add-host=host.docker.internal:host-gateway` to `docker run` and ensure the host
forwarders are reachable through that address; their default loopback listeners
may need a local relay.

1. Build the image:

   ```sh
   .devcontainer/build.sh
   ```

   The script tags the image `embedpdf-dev:<date>-<commit>` as well as `latest`, and
   labels it with the commit. To see which commit a container's image came from, run
   `docker inspect -f '{{index .Config.Labels "org.opencontainers.image.revision"}}' embed-pdf`.
   A plain `docker build -f .devcontainer/Dockerfile -t embedpdf-dev .` still works; its
   label reads `unknown`.

2. Start each host forwarder in its own terminal and leave both running:

   ```sh
   OAUTH2_FORWARDER_PORT=18080 mise exec -- o2f-server
   ```

   ```sh
   GIT_CREDENTIAL_FORWARDER_PORT=18081 mise exec -- gcf-server
   ```

3. Create a persistent container with the repository volume and local viewer ports:

   ```sh
   docker run -d --init --name embed-pdf --user node \
     --mount type=volume,source=embedpdf-workspace,target=/workspaces \
     --mount type=volume,source=embedpdf-cargo-registry,target=/home/node/.cargo/registry \
     --mount type=volume,source=embedpdf-cache,target=/home/node/.cache \
     -e EMBEDPDF_GIT_NAME="$(git config user.name)" \
     -e EMBEDPDF_GIT_EMAIL="$(git config user.email)" \
     --publish 127.0.0.1:3000:3000 \
     --publish 127.0.0.1:5173:5173 \
     --workdir /workspaces/embed-pdf-viewer embedpdf-dev
   ```

4. Clone the repository and install its dependencies:

   ```sh
   docker exec -it embed-pdf python3 /usr/local/share/embedpdf/clone.py
   docker exec -it embed-pdf python3 .devcontainer/setup.py
   ```

   First build and setup take approximately **15–30 minutes**, depending on
   downloads and emulation. The clone script preserves an existing checkout.
   Setup sets the global Git identity from `EMBEDPDF_GIT_NAME` and
   `EMBEDPDF_GIT_EMAIL` when the container has none yet.

5. Open a development shell:

   ```sh
   docker exec -it embed-pdf bash
   ```

The Docker build context includes only container definitions and toolchain pins.
The clone and setup scripts use Python 3's standard library, with no pip packages.
Setup invokes the existing package-manager, compiler, and runtime-fetch tools.
To override pinned image tools, pass build arguments such as
`--build-arg OAUTH2_FORWARDER_VERSION=VERSION` or
`--build-arg GIT_CREDENTIAL_FORWARDER_VERSION=VERSION` to `.devcontainer/build.sh`.

Two more named volumes hold caches that would otherwise be downloaded again for
every new container: `embedpdf-cargo-registry` (crate sources) and `embedpdf-cache`
(`~/.cache`, including the 2 GB of Playwright browsers). Containers can share them.
Cargo's `target/` stays in the workspace volume. A default `CARGO_TARGET_DIR` is not
set because `crates/epdf-web/scripts/build.sh`, `packages/engine/runtime/scripts/build-target.sh`
and `perf-compare/scripts/build-arm.mjs` read `$ROOT/target/` directly.

## Stop, resume, and rebuild

Stop the container with `docker stop embed-pdf`. Resume it and open a shell with:

```sh
docker start embed-pdf
docker exec -it embed-pdf bash
```

The stopped container retains its home directory, authentication sessions, and
installed tools; the named volume retains the repository and dependencies.
After dependency or runtime pin changes, rerun `python3 .devcontainer/setup.py`
inside the container. Missing published artifacts fail setup; the fetch scripts
report the expected release and recovery command.

After changing image toolchain pins, run `python3 .devcontainer/check-pins.py`,
which the `Development image pins` workflow also runs on pull requests. Then rebuild the image, stop and remove the old
container, then repeat creation and setup. Removing the container discards its
home directory and authentication sessions; push work and save any files outside
`/workspaces` that you want to keep before removing it. Keep `embedpdf-workspace`
to preserve the repository.

Renovate tracks the pins marked with a `# renovate:` comment in `Dockerfile` and in
`.github/workflows/release-rust-viewer.yml`, and the Node base image together with
`mise.toml` and the workflows' `node-version`. No CI builds the image, so every update
to it waits for approval on the Dependency Dashboard. Renovate cannot compute
checksums: a binaryen, rustup-init or .NET SDK pull request needs its hashes filled in
by hand, and `check-pins.py --verify-checksums` prints the published values. The emsdk
pins have no annotation, because `build-engine-runtime.yml` belongs to upstream and
they move only with an upstream sync. The Android NDK has none either: its GitHub
releases exist, but Google publishes only a SHA-1 for the archive, so a bump needs its
SHA-256 computed by hand.

## Editor integration

The optional `devcontainer.json` lets a compatible editor run cloning and setup
as lifecycle hooks and forward ports 3000 and 5173. Its VS Code customization
installs suggested editor extensions. Plain Docker does not read that file;
use the commands above for setup, ports, and environment overrides. See the
[Dev Container specification](https://github.com/devcontainers/spec/blob/main/docs/specs/devcontainerjson-reference.md)
for these editor and lifecycle settings.

## Included tools

| Tool                                                      | Version/source                                       |
| --------------------------------------------------------- | ---------------------------------------------------- |
| Node                                                      | 24.20.0, `node:24.20.0-trixie-slim`; matches `mise.toml` |
| pnpm                                                      | `package.json` `packageManager`                      |
| Rust, Cargo, rustfmt, Clippy, rust-src, llvm-tools, cross-target std | `rust-toolchain.toml`; rustup-init 1.29.3, SHA-256 verified |
| cargo-llvm-cov                                            | 0.9.1                                                |
| .NET SDK                                                  | 11.0.100; Microsoft archives verified with SHA512      |
| Clang/libclang, LLD, CMake, Ninja, C/C++ compiler, Python | Debian Trixie packages                               |
| Emscripten / Binaryen                                     | 3.1.72 / 133 (SHA-256 verified), on `PATH`; its `.emscripten` selects Binaryen |
| Emscripten for `wasm32-eh`                                | 4.0.0 with its own Binaryen, sysroot built; `/opt/emsdk-eh` (`$EMSDK_EH`) |
| wasm-bindgen-cli                                          | 0.2.127, matching `crates/epdf-web/Cargo.toml`       |
| Android NDK / cargo-ndk                                   | 29.0.14206865 (amd64 only, SHA-256 verified) / 4.1.2 |
| Git, Git LFS, GitHub CLI, rsync, ripgrep, jq, ShellCheck  | Debian packages                                      |
| OAuth2 / Git credential forwarding clients                | oauth2-forwarder 1.9.4 / git-credential-forwarder 1.8.0 |
| Chromium, Firefox, WebKit                                 | Workspace Playwright version; system libraries in the image, browsers installed during setup |

Docker selects the host architecture: **linux/arm64** on Apple Silicon and ARM64
Linux, or **linux/amd64** on x86-64 hosts. Emscripten detects that architecture;
Binaryen and the native PDFium artifact are selected to match it.

## .NET

The image installs SDK 11.0.100 into `/opt/dotnet` for both supported Linux
architectures. `DOTNET_ROOT` and `/usr/local/bin/dotnet` expose it to the
`node` user in login and non-login shells. Debian dependencies and libclang for
Rust bindgen are already installed by the image. The SDK supports Linux managed
builds and native interop; macOS and Windows native builds still require their
respective hosts or CI.

To update the SDK, change `DOTNET_SDK_VERSION` and both architecture-specific
SHA512 values in `Dockerfile`, using Microsoft's
[.NET 10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json).
The build verifies the archive before extraction and checks the selected SDK
version. Rebuild the image; do not install SDK updates into running containers.

Check the installation with `dotnet --info` and `dotnet --list-sdks`. Run managed
project builds with `dotnet build path/to/project.csproj`; no root `global.json`
is imposed on consumers by this container configuration.

## Android, Swift and iOS

The Android NDK is installed only on amd64: Google does not publish Linux ARM64
host binaries. ARM64 containers still support the TypeScript engine, native Rust
engine, and WASM toolchain. Android compilation requires an
amd64 environment; Android Studio, an emulator, and the Android application SDK
are not included.

The image has no Swift toolchain. Swift, Xcode and iOS work runs on the macOS host,
which also runs the coding agents; they reach this container through `docker exec`.
The Linux Swift toolchain cost 3.55 GB, and the only scripts that used it on Linux
were the benchmarks under `viewer-rust-port-benchmarks/swift-geometry-only` and
`pdfium-multiplatform-prototype/build/test-swift.sh`.

## Use

```sh
cargo test -p epdf-engine
pnpm --filter @embedpdf/engine test
pnpm exec tsx --conditions=development tooling/engine-parity/read-geometry.ts packages/engine/main/test/fixtures/rotated_text.pdf 0
```

## Host authentication forwarding

The image includes `o2f-client`, `o2f-browser`, and `gcf-client`, matching the
versions installed via mise on the host. `BROWSER=o2f-browser` forwards browser
login requests, and a system Git credential helper forwards HTTPS credentials.
These follow the [OAuth2 forwarder](https://github.com/sam-mfb/oauth2-forwarder)
and [Git credential forwarder](https://github.com/sam-mfb/git-credential-forwarder)
client setup instructions.

The startup commands above run the servers on ports 18080 and 18081. The image
connects to `host.docker.internal:18080` and `host.docker.internal:18081` by default.
For custom ports, start the host servers with the desired values and pass
`-e OAUTH2_FORWARDER_SERVER=host.docker.internal:PORT` and
`-e GIT_CREDENTIAL_FORWARDER_SERVER=host.docker.internal:PORT` to `docker run`.
No host socket mounts are required.

The forwarders use the host browser and Git credential manager without copying
host credentials into the image. The `node` user has sudo for development tools.
Use the build arguments described above to change the forwarding tool versions.

For viewer development, bind the dev server to `0.0.0.0`; the Docker command above
publishes ports 3000 and 5173 on the host loopback interface. Editor integration also
forwards ports 5250/5251 for the Rust kernel eval app. Viewer E2E requires the viewer build described in
[`tooling/viewer-e2e/README.md`](../tooling/viewer-e2e/README.md).
