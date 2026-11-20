**Pre-release, evaluation only.** The full EmbedPDF viewer on the Rust kernel and plugins (`crates/epdf-web` through `@embedpdf/core-wasm`), packed as one self-contained tarball: the viewer, the kernel's WASM, PDFium's WASM, the engine's workers and Preact. No peer dependencies, no Rust toolchain, no Vite plugin needed. Never published to npm.

This is the first bundle the release train built. It tags the commit the train was dispatched on, which `buildInfo.commit` names. The runtime fork is still `af480d0ce` (upstream runtime `f2ea75972`). Its `wasm32` payload is now published on this repository as a `runtime-payloads-<hash>` prerelease, built by the same train run, rather than on the separate runtime repository that held preview.3's.

## What changed since the previous bundle release

Almost nothing a page can see. Since `f37adb447`, the commit preview.3 was built from, the only change to the code in the bundle is in `crates/pdfium`: it converts PDFium's enum types at the FFI boundary so the Windows build compiles, with no change to a signature or a result ([`884d91776`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/884d91776)). Nothing under `crates/epdf-web`, `crates/plugins`, `packages/core-wasm` or `packages/rust-viewer-bundle/src` changed. That crate is a payload input, so the payload's hash, and with it its release tag, differs from preview.3's `runtime-payloads-56092bfe645aab76`. `fetch:payload` now looks for payloads on this repository by default ([`dfaa5e766`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/dfaa5e766)).

## Install

```sh
npm i https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/releases/download/rust-viewer-0.0.0-preview.4/mfb-embedpdf-rust-viewer-0.0.0-preview.4.tgz
```

```ts
import { createViewer, buildInfo } from '@mfb/embedpdf-rust-viewer';

const el = await createViewer({ target: '#viewer', src: '/report.pdf' }); // give #viewer a height
el.addEventListener('epdf:ready', () => console.log(el.viewer!.documents.list()));
console.log(buildInfo);
```

`createViewer` takes every `EmbedPDF.init` option. Assets resolve relative to the bundle; if a bundler loses them, copy `dist/` to a static path and pass `engine: { assetsUrl: '/embedpdf/' }`. The module uses top-level `await`, so a Vite consumer needs `build.target: 'esnext'` (or another target with top-level await).

## Validation

The train built the bundle on `ubuntu-24.04` from a clean checkout of the dispatched commit (`.github/workflows/release-rust-viewer.yml`), against the payload it had just published. It checked that `packages/rust-viewer-bundle/package.json` is `0.0.0-preview.4`, built `@embedpdf/engine-runtime`, ran `build:all` and `pack:tarball`, and failed if the build left a file that isn't gitignored, since that would mark `buildInfo` dirty. It then checked that exactly `mfb-embedpdf-rust-viewer-0.0.0-preview.4.tgz` was packed, that the packed `package.json` is version `0.0.0-preview.4`, and that the packed `dist/` names the dispatched commit. The tarball's SHA-256 and size are in the run's summary.

That's all the train checks. It doesn't run the bundle in a browser, so unlike preview.3 there was no Vite consumer, no headless Chromium run, and no open, paint, annotate or search check.

## Known gaps

- Signatures are drawn without sealing; no WebCrypto signer host is wired (`packages/rust-viewer-bundle/src/hosts.ts`).
- The Rust annotation adapter ignores a configured `randomUuid` host. Still open from preview.3; nothing it touches has changed since.
- Not checked for this build: the bundle in a browser at all (see Validation), two viewers on one page, text selection from the pointer, and the package typecheck.
