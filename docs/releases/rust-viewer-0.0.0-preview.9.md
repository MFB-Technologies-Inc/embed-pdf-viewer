**Pre-release, evaluation only.** The full EmbedPDF viewer on the Rust kernel and plugins (`crates/epdf-web` through `@embedpdf/core-wasm`), packed as one self-contained tarball: the viewer, the kernel's WASM, PDFium's WASM, the engine's workers and Preact. No peer dependencies, no Rust toolchain, no Vite plugin needed. Never published to npm.

## What changed since preview.8

This release catches up with upstream `embedpdf/embed-pdf-viewer` `cda9e82a4`, still `@embedpdf/viewer` 3.4.0. The `createViewer` API, `buildInfo` and the runtime fork (`9472117ed`) are unchanged. The crates are payload inputs, so the train builds a new `runtime-payloads-<hash>` instead of reusing preview.8's `runtime-payloads-3a9695da0a92494b`.

The kernel ports upstream's form and signature work. A field can be for anyone: any signer may fill it, whatever its group, and a signing that names no lock locks every such field that holds a value (upstream #1163, [`db41381cb`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/db41381cb)). A push button can hold a picture, a PNG, a JPEG or a one-page PDF, through `doc.forms.setButtonImage`, laid out by each widget's `layout`, and the PDF stores it once however many buttons show it (upstream #1165, [`8ed1f4f77`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/8ed1f4f77)). Filling a field or drawing a mark keeps earlier signatures valid: neither writes the filler or a mark's fit into objects a signature covers any more (upstream #1162, [`fee9339ec`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/fee9339ec)). A new appearance on a signature field nobody signed no longer marks an earlier signature as broken (upstream #1161, [`dad7474af`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/dad7474af)). The range changes no viewer UI, so these reach a page through the engine and plugin APIs only.

One change alters results: `filledBy`, `filledByName` and `filledAt` on a field now always read `null`, as upstream's do. A picture put in with `setButtonImage` is outside the history's undo, as upstream's is.

The train redeploys the hosted demo, https://embed-pdf-rust-kernel-eval.roan-util-cloudfare.workers.dev, from the same build.

## Install

```sh
npm i https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/releases/download/rust-viewer-0.0.0-preview.9/mfb-embedpdf-rust-viewer-0.0.0-preview.9.tgz
```

Usage is as in preview.7.

## Validation

The same checks as preview.7 (`.github/workflows/release-rust-viewer.yml`), with `package.json` at `0.0.0-preview.9`. The train still doesn't run the bundle in a browser. `crates/epdf-web/tests/picture_buttons.rs` reaches the new members through `WasmKernel` ([`5503edcaf`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/5503edcaf)).

## Known gaps

As in preview.7.
