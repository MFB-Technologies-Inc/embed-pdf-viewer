**Pre-release, evaluation only.** The full EmbedPDF viewer on the Rust kernel and plugins (`crates/epdf-web` through `@embedpdf/core-wasm`), packed as one self-contained tarball: the viewer, the kernel's WASM, PDFium's WASM, the engine's workers and Preact. No peer dependencies, no Rust toolchain, no Vite plugin needed. Never published to npm.

## What changed since preview.4

Only the version. Nothing under `crates`, `packages/core-wasm`, `packages/rust-viewer-bundle/src` or the runtime fork (still `af480d0ce`) has changed since `43f939500`, the commit preview.4 tags, and the bundle carries the same payload, `runtime-payloads-ed16549dd6e1e977`. This release exists because the release train now redeploys the hosted demo, https://embed-pdf-rust-kernel-eval.roan-util-cloudfare.workers.dev, from the same build ([`eb304e3fc`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/eb304e3fc)), and preview.4 was already out. The demo served a build of `1a56bf8e9` from 2026-10-13 until this release.

## Install

```sh
npm i https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/releases/download/rust-viewer-0.0.0-preview.5/mfb-embedpdf-rust-viewer-0.0.0-preview.5.tgz
```

```ts
import { createViewer, buildInfo } from '@mfb/embedpdf-rust-viewer';

const el = await createViewer({ target: '#viewer', src: '/report.pdf' }); // give #viewer a height
el.addEventListener('epdf:ready', () => console.log(el.viewer!.documents.list()));
console.log(buildInfo);
```

`createViewer` takes every `EmbedPDF.init` option. Assets resolve relative to the bundle; if a bundler loses them, copy `dist/` to a static path and pass `engine: { assetsUrl: '/embedpdf/' }`. The module uses top-level `await`, so a Vite consumer needs `build.target: 'esnext'` (or another target with top-level await).

## Validation

The same checks as preview.4 (`.github/workflows/release-rust-viewer.yml`): a clean build on `ubuntu-24.04`, a clean tree afterwards, exactly one tarball whose `package.json` is `0.0.0-preview.5`, and a packed `dist/` that names the dispatched commit. The same job built the demo site and checked that it names that commit too. The tarball's SHA-256 and size are in the run's summary.

The train still doesn't run the bundle in a browser. The demo deploy is the first place this build runs in one, and nothing checks it there.

## Known gaps

- Signatures are drawn without sealing; no WebCrypto signer host is wired (`packages/rust-viewer-bundle/src/hosts.ts`).
- The Rust annotation adapter ignores a configured `randomUuid` host. Still open from preview.3.
- Not checked for this build: the bundle in a browser, two viewers on one page, text selection from the pointer, and the package typecheck.
