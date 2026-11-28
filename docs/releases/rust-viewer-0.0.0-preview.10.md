**Pre-release, evaluation only.** The full EmbedPDF viewer on the Rust kernel and plugins (`crates/epdf-web` through `@embedpdf/core-wasm`), packed as one self-contained tarball: the viewer, the kernel's WASM, PDFium's WASM, the engine's workers and Preact. No peer dependencies, no Rust toolchain, no Vite plugin needed. Never published to npm.

## What changed since preview.9

A rebuild with Rust 1.100.1, up from 1.100.0 ([`104cf9fe6`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/104cf9fe6)). Upstream is still `cda9e82a4` (`@embedpdf/viewer` 3.4.0), and the `createViewer` API, `buildInfo` and the runtime fork (`9472117ed`) are unchanged. `rust-toolchain.toml` is a payload input, so the train builds `runtime-payloads-b56d4368ce580390` instead of reusing preview.9's.

The build pins `taiki-e/install-action` 2.90.11 ([`36049ccd4`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/36049ccd4)), and the hosted demo deploys with wrangler 4.169.0 ([`57904f47d`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/57904f47d)). The train redeploys the demo, https://embed-pdf-rust-kernel-eval.roan-util-cloudfare.workers.dev, from the same build.

## Install

```sh
npm i https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/releases/download/rust-viewer-0.0.0-preview.10/mfb-embedpdf-rust-viewer-0.0.0-preview.10.tgz
```

Usage is as in preview.7.

## Validation

The same checks as preview.7 (`.github/workflows/release-rust-viewer.yml`), with `package.json` at `0.0.0-preview.10`. The train still doesn't run the bundle in a browser.

## Known gaps

As in preview.7.
