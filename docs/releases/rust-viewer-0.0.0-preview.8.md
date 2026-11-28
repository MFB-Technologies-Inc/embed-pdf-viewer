**Pre-release, evaluation only.** The full EmbedPDF viewer on the Rust kernel and plugins (`crates/epdf-web` through `@embedpdf/core-wasm`), packed as one self-contained tarball: the viewer, the kernel's WASM, PDFium's WASM, the engine's workers and Preact. No peer dependencies, no Rust toolchain, no Vite plugin needed. Never published to npm.

## What changed since preview.7

One engine fix. Everything in `docs/releases/rust-viewer-0.0.0-preview.7.md` still applies: the same upstream (`@embedpdf/viewer` 3.4.0), the same runtime fork (`9472117ed`) and the same `createViewer` API.

A `ResetForm` action of some fields resets only those fields. The kernel read every `ResetForm` as having no `/Fields`, which resets the whole form, so a chained JavaScript button lost its first script's write; `Hide` targets and `SubmitForm` payloads were missing for the same reason (#39, [`6b71b4243`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/6b71b4243)).

## Install

```sh
npm i https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/releases/download/rust-viewer-0.0.0-preview.8/mfb-embedpdf-rust-viewer-0.0.0-preview.8.tgz
```

Usage is as in preview.7.

## Validation

The same checks as preview.7 (`.github/workflows/release-rust-viewer.yml`), with `package.json` at `0.0.0-preview.8`. The train still doesn't run the bundle in a browser.

## Known gaps

As in preview.7.
