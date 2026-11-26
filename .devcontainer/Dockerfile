# syntax=docker/dockerfile:1
# Docker selects the host architecture; supported hosts are amd64 and arm64.
# Must equal `node` in mise.toml; .devcontainer/check-pins.py enforces it.
FROM node:24.20.0-trixie-slim

SHELL ["/bin/bash", "-o", "pipefail", "-c"]
ARG TARGETARCH
# Must equal EMSDK_VERSION in .github/workflows/build-engine-runtime.yml.
ARG EMSDK_VERSION=3.1.72
# The wasm32-eh pin in the runtime fork's scripts/embedpdf-runtime/emsdk-version.sh and
# EH_EMSDK_VERSION in perf-compare/scripts/build-arm.mjs: the Rust-core payload links with it.
ARG EMSDK_EH_VERSION=4.0.0
# Must equal BINARYEN_VERSION and BINARYEN_SHA256 in .github/workflows/release-rust-viewer.yml.
# renovate: datasource=github-releases depName=WebAssembly/binaryen extractVersion=^version_(?<version>\d+)$ versioning=loose
ARG BINARYEN_VERSION=133
ARG BINARYEN_SHA256_AMD64=2dc9c7813f5375db93d96ead4b78222fcc3e2677bbb832297af4797782a37489
ARG BINARYEN_SHA256_ARM64=89c07ea56faf38d0fbecf36ca8ec0721756716185f265b568e133d427f299bf8
# Google publishes SHA-1 (87e2bb7e9be5d6a1c6cdf5ec40dd4e0c6d07c30b); this SHA-256 was taken
# from an archive that matched it.
ARG NDK_SHA256=4abbbcdc842f3d4879206e9695d52709603e52dd68d3c1fff04b3b5e7a308ecf
# Must equal the version and SHA512 in .github/workflows/release-dotnet.yml.
# renovate: datasource=dotnet-version depName=dotnet-sdk
ARG DOTNET_SDK_VERSION=10.0.403
# renovate: datasource=github-tags depName=rust-lang/rustup
ARG RUSTUP_VERSION=1.29.3
ARG RUSTUP_SHA256_AMD64=b1c8cb14e30889e7e2999e6b69e62d9e9b9ffc0f4df8180564d306d9680e6c55
ARG RUSTUP_SHA256_ARM64=9f608c142e0e38a78957d0661ba1dfaf0b6fb37184cfaa4b9a0aa57af6184282
# renovate: datasource=crate depName=cargo-ndk versioning=semver
ARG CARGO_NDK_VERSION=4.1.2
# renovate: datasource=crate depName=cargo-llvm-cov versioning=semver
ARG CARGO_LLVM_COV_VERSION=0.9.2
# Must equal the `wasm-bindgen` pin in crates/epdf-web/Cargo.toml.
# renovate: datasource=crate depName=wasm-bindgen-cli versioning=semver
ARG WASM_BINDGEN_VERSION=0.2.127
# renovate: datasource=npm depName=oauth2-forwarder
ARG OAUTH2_FORWARDER_VERSION=1.9.4
# renovate: datasource=npm depName=git-credential-forwarder
ARG GIT_CREDENTIAL_FORWARDER_VERSION=1.8.0

# The slim base omits what the full buildpack image supplied; git, ssh, procps, file
# and bzip2 are restored here. libicu76 and zlib1g are .NET runtime dependencies.
RUN case "$TARGETARCH" in amd64|arm64) ;; *) echo "Unsupported architecture: $TARGETARCH" >&2; exit 1 ;; esac \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
       build-essential clang libclang-dev lld cmake ninja-build pkg-config \
       libssl-dev python3 python3-venv git git-lfs gh curl ca-certificates \
       openssh-client procps file bzip2 rsync \
       unzip zip xz-utils jq ripgrep fd-find less sudo shellcheck \
       tzdata libicu76 zlib1g \
    && rm -rf /var/lib/apt/lists/* \
    && echo 'node ALL=(ALL) NOPASSWD:ALL' > /etc/sudoers.d/node \
    && chmod 0440 /etc/sudoers.d/node

RUN git clone --depth 1 --branch "$EMSDK_VERSION" https://github.com/emscripten-core/emsdk.git /opt/emsdk \
    && /opt/emsdk/emsdk install "$EMSDK_VERSION" \
    && /opt/emsdk/emsdk activate "$EMSDK_VERSION" \
    && echo "BINARYEN_ROOT = '/opt/binaryen'" >> /opt/emsdk/.emscripten \
    && chown -R node:node /opt/emsdk
# A second emsdk for wasm32-eh, with its own binaryen and a built sysroot: pdfium-sys runs
# bindgen against the cache's sysroot before emcc has installed any headers there.
RUN git clone --depth 1 --branch "$EMSDK_EH_VERSION" https://github.com/emscripten-core/emsdk.git /opt/emsdk-eh \
    && /opt/emsdk-eh/emsdk install "$EMSDK_EH_VERSION" \
    && /opt/emsdk-eh/emsdk activate "$EMSDK_EH_VERSION" \
    && bash -c 'unset EM_CONFIG; source /opt/emsdk-eh/emsdk_env.sh >/dev/null 2>&1 && embuilder build sysroot && emcc --version | grep -F " $0 "' "$EMSDK_EH_VERSION" \
    && chown -R node:node /opt/emsdk-eh
RUN case "$TARGETARCH" in \
      amd64) binaryen_arch=x86_64; binaryen_sha256="$BINARYEN_SHA256_AMD64" ;; \
      arm64) binaryen_arch=aarch64; binaryen_sha256="$BINARYEN_SHA256_ARM64" ;; \
    esac \
    && curl -fsSL --retry 3 \
      "https://github.com/WebAssembly/binaryen/releases/download/version_${BINARYEN_VERSION}/binaryen-version_${BINARYEN_VERSION}-${binaryen_arch}-linux.tar.gz" \
      -o /tmp/binaryen.tar.gz \
    && echo "$binaryen_sha256  /tmp/binaryen.tar.gz" | sha256sum -c - \
    && mkdir /opt/binaryen \
    && tar -xzf /tmp/binaryen.tar.gz -C /opt/binaryen --strip-components=1 \
    && rm /tmp/binaryen.tar.gz

# NDK r29 = 29.0.14206865. Google provides Linux host binaries only for x86-64.
RUN if [ "$TARGETARCH" = amd64 ]; then \
      curl -fsSL --retry 3 https://dl.google.com/android/repository/android-ndk-r29-linux.zip -o /tmp/ndk.zip \
    && echo "$NDK_SHA256  /tmp/ndk.zip" | sha256sum -c - \
    && unzip -q /tmp/ndk.zip -d /opt \
    && rm /tmp/ndk.zip; \
    else \
      echo 'Android NDK omitted: Google does not provide a Linux ARM64 host toolchain.'; \
    fi

# SHA512 values from Microsoft's .NET 10 release metadata, matched to this SDK.
RUN case "$TARGETARCH" in \
      amd64) dotnet_arch=x64; dotnet_sha512=b7b3d8461cf98b07d2552ce9199702fa3ad010ac2671a5f8cd5136f7534e50f6855ba7bbb02b11494563f1d04156276a31cda1103b900e5eecd0d9473dc7b9c5 ;; \
      arm64) dotnet_arch=arm64; dotnet_sha512=0966aa2b70f4677b8319be9df447a78a0dcd6164836425b55a68fd714f4023feb87b4cac21b9a996b79472ad54487cd0adcd2a9dfd570e2b2a1d26423a2baa9b ;; \
    esac \
    && curl -fsSL --retry 3 \
      "https://builds.dotnet.microsoft.com/dotnet/Sdk/${DOTNET_SDK_VERSION}/dotnet-sdk-${DOTNET_SDK_VERSION}-linux-${dotnet_arch}.tar.gz" \
      -o /tmp/dotnet-sdk.tar.gz \
    && echo "$dotnet_sha512  /tmp/dotnet-sdk.tar.gz" | sha512sum -c - \
    && mkdir /opt/dotnet \
    && tar -xzf /tmp/dotnet-sdk.tar.gz -C /opt/dotnet \
    && rm /tmp/dotnet-sdk.tar.gz \
    && ln -s /opt/dotnet/dotnet /usr/local/bin/dotnet

# Seed a Docker-managed volume with writable directories, never a host checkout.
RUN mkdir -p /workspaces/embed-pdf-viewer \
    && chown -R node:node /workspaces \
    && git config --system credential.helper /home/node/.npm-global/bin/git-credential-forwarder-client
COPY .devcontainer/clone.py /usr/local/share/embedpdf/clone.py

ENV EMBEDPDF_REPOSITORY=https://github.com/MFB-Technologies-Inc/embed-pdf-viewer.git \
    EMBEDPDF_BRANCH=research/multi-platform-rust-core-impl-plugins \
    EMBEDPDF_WORKSPACE=/workspaces/embed-pdf-viewer \
    OAUTH2_FORWARDER_SERVER=host.docker.internal:18080 \
    GIT_CREDENTIAL_FORWARDER_SERVER=host.docker.internal:18081 \
    BROWSER=o2f-browser \
    EMSDK=/opt/emsdk \
    EM_CONFIG=/opt/emsdk/.emscripten \
    EMSDK_EH=/opt/emsdk-eh \
    ANDROID_NDK_HOME=/opt/android-ndk-r29 \
    DOTNET_ROOT=/opt/dotnet \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    NPM_CONFIG_PREFIX=/home/node/.npm-global
# emsdk's upstream/bin stays off PATH: its clang has no glibc headers, and
# build/generate-functions.mjs runs bare `clang` for every runtime target. emcc finds
# its own clang and wasm-ld through EM_CONFIG, and Binaryen through BINARYEN_ROOT.
ENV PATH="/opt/binaryen/bin:/home/node/.cargo/bin:/home/node/.npm-global/bin:/opt/emsdk/upstream/emscripten:${PATH}"
# Login shells (`bash -l`, which VS Code may open) reset PATH in /etc/profile.
RUN printf '%s\n' \
      'export PATH="/opt/binaryen/bin:/home/node/.cargo/bin:/home/node/.npm-global/bin:/opt/emsdk/upstream/emscripten:$PATH"' \
      > /etc/profile.d/embedpdf-toolchain.sh

USER node
WORKDIR /home/node
# The registry and cache directories exist so named volumes mounted there start owned by node.
RUN case "$TARGETARCH" in \
      amd64) rustup_triple=x86_64-unknown-linux-gnu; rustup_sha256="$RUSTUP_SHA256_AMD64" ;; \
      arm64) rustup_triple=aarch64-unknown-linux-gnu; rustup_sha256="$RUSTUP_SHA256_ARM64" ;; \
    esac \
    && curl -fsSL --retry 3 \
      "https://static.rust-lang.org/rustup/archive/${RUSTUP_VERSION}/${rustup_triple}/rustup-init" \
      -o /tmp/rustup-init \
    && echo "$rustup_sha256  /tmp/rustup-init" | sha256sum -c - \
    && chmod +x /tmp/rustup-init \
    && /tmp/rustup-init -y --profile minimal --default-toolchain none \
    && rm /tmp/rustup-init \
    && mkdir -p /home/node/.cargo/registry /home/node/.cache

# Toolchain pins are copied as late as possible: a change to one file rebuilds only the
# layers after it, not the emsdk, NDK and .NET layers above.
COPY rust-toolchain.toml /opt/embedpdf-pins/
RUN rust_version="$(python3 -c 'import tomllib; print(tomllib.load(open("/opt/embedpdf-pins/rust-toolchain.toml", "rb"))["toolchain"]["channel"])')" \
    && rustup toolchain install "$rust_version" --profile minimal --component rustfmt,clippy,rust-src,llvm-tools \
    && rustup default "$rust_version" \
    && python3 -c 'import subprocess,tomllib; t=tomllib.load(open("/opt/embedpdf-pins/rust-toolchain.toml", "rb"))["toolchain"]; subprocess.run(["rustup", "target", "add", "--toolchain", t["channel"], *t["targets"]], check=True)' \
    && cargo install --locked cargo-ndk --version "$CARGO_NDK_VERSION" \
    && cargo install --locked wasm-bindgen-cli --version "$WASM_BINDGEN_VERSION" \
    && cargo install --locked cargo-llvm-cov --version "$CARGO_LLVM_COV_VERSION" \
    && rm -rf /home/node/.cargo/registry/* /home/node/.cargo/git

RUN npm install -g "oauth2-forwarder@${OAUTH2_FORWARDER_VERSION}" \
       "git-credential-forwarder@${GIT_CREDENTIAL_FORWARDER_VERSION}" \
    && npm cache clean --force

COPY package.json /opt/embedpdf-pins/
RUN npm install -g "$(node -p "require('/opt/embedpdf-pins/package.json').packageManager")" \
    && npm cache clean --force

# Playwright's system libraries, for the version the workspace pins. setup.py installs
# the browsers themselves into ~/.cache/ms-playwright.
COPY tooling/viewer-e2e/package.json /opt/embedpdf-pins/viewer-e2e/
USER root
RUN playwright_version="$(node -p "require('/opt/embedpdf-pins/viewer-e2e/package.json').devDependencies['@playwright/test']")" \
    && npx -y "playwright@${playwright_version}" install-deps chromium firefox webkit \
    && rm -rf /var/lib/apt/lists/* /root/.npm
USER node

RUN o2f-client --version && gcf-client --version \
    && rustc --version && cargo llvm-cov --version && pnpm --version && emcc --version && dotnet --version \
    && test "$(dotnet --version)" = "$DOTNET_SDK_VERSION" \
    && wasm-opt --version | grep -F "version ${BINARYEN_VERSION}" \
    && wasm-bindgen --version | grep -F "$WASM_BINDGEN_VERSION" \
    && bash -lc 'command -v emcc && command -v cargo && command -v pnpm && command -v o2f-browser && test "$(command -v dotnet)" = /usr/local/bin/dotnet && wasm-opt --version | grep -F "version ${BINARYEN_VERSION}"'

# Set by .devcontainer/build.sh. Kept last so a new revision rebuilds only this layer.
ARG VCS_REF=unknown
ARG BUILD_DATE=unknown
LABEL org.opencontainers.image.title="embedpdf-dev" \
      org.opencontainers.image.source="https://github.com/MFB-Technologies-Inc/embed-pdf-viewer" \
      org.opencontainers.image.revision="$VCS_REF" \
      org.opencontainers.image.created="$BUILD_DATE"

CMD ["sleep", "infinity"]
