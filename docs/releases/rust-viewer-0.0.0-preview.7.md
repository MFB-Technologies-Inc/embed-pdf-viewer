**Pre-release, evaluation only.** The full EmbedPDF viewer on the Rust kernel and plugins (`crates/epdf-web` through `@embedpdf/core-wasm`), packed as one self-contained tarball: the viewer, the kernel's WASM, PDFium's WASM, the engine's workers and Preact. No peer dependencies, no Rust toolchain, no Vite plugin needed. Never published to npm.

## What changed since preview.6

This release catches up with upstream `embedpdf/embed-pdf-viewer` `9af4d9ee7`, which is `@embedpdf/viewer` 3.4.0, up from 3.3.0 ([`d670a1cbf`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/d670a1cbf)). The `createViewer` API and `buildInfo` are unchanged, and nothing under `packages/core-wasm/src`, `packages/rust-viewer-bundle/src` or `examples/rust-kernel-eval` changed since `d250a68e3`, the commit preview.6 tags. What changed is the viewer UI on top and the Rust and PDFium underneath.

PDFium is the runtime fork at `9472117ed` (upstream runtime `7f399f838`), up from `af480d0ce`. It renamed the three stamp drawing exports to `EPDFAnnot_GetDrawing`, `EPDFAnnot_SetDrawing` and `EPDFDoc_GetDrawings`, which the `pdfium` crate now calls ([`f8a124edc`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/f8a124edc), [`defe697f2`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/defe697f2)); nothing a page calls is named after them. The kernel reads a page's glyphs 4,096 at a time through the new `EPDFText_GetPageGeometry` ([`1f2411c1c`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/1f2411c1c)). The runtime and the crates are payload inputs, so the train builds `runtime-payloads-4f2cb5fbc177da69` for this release instead of reusing preview.6's `runtime-payloads-3b4e5f2a666a966b`.

The Rust plugins follow upstream's form work. Forms gain groups, with each field shown in its group's color and the designer placing fields for a chosen group ([`f7121c0df`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/f7121c0df), [`46be230a5`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/46be230a5)), field purpose and date and time formats ([`a88255b34`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/a88255b34), [`7047385c5`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/7047385c5)), and form fields that read upright on a turned page and turn in the designer ([`ef50e73bf`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/ef50e73bf), [`cf2dee995`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/cf2dee995)). A signature mark can be a PNG or a JPEG, where only a one-page PDF worked before, and clearing a signature field takes its mark out ([`b04fefba9`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/b04fefba9), [`54d2cba66`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/54d2cba66)). Tab passes over a signature field nobody can sign and treats a radio group as one stop. The upstream UI that came with 3.4.0 draws all of this: form controls attached to a field box (#1138), group colors and the designer's group choice (#1141), the Orientation control and the rotate commands (#1118), radio buttons announced by their own option (#1136), and the scrollbar bound through `attachScrollbar` (#1144).

One change alters results: a field placed, created or copied without a group is in no group, whatever the session's own group is (upstream #1140). Before, it took the session's group.

Parse errors read as zod 4 words them, since the port reproduces zod's issue text ([`e859e3112`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/e859e3112)). Text in a font the PDF names but doesn't embed now draws in the registered fallback fonts (embedpdf/runtime#132, upstream #1159).

The train redeploys the hosted demo, https://embed-pdf-rust-kernel-eval.roan-util-cloudfare.workers.dev, from the same build.

## Install

```sh
npm i https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/releases/download/rust-viewer-0.0.0-preview.7/mfb-embedpdf-rust-viewer-0.0.0-preview.7.tgz
```

```ts
import { createViewer, buildInfo } from '@mfb/embedpdf-rust-viewer';

const el = await createViewer({ target: '#viewer', src: '/report.pdf' }); // give #viewer a height
el.addEventListener('epdf:ready', () => console.log(el.viewer!.documents.list()));
console.log(buildInfo);
```

`createViewer` takes every `EmbedPDF.init` option. Assets resolve relative to the bundle; if a bundler loses them, copy `dist/` to a static path and pass `engine: { assetsUrl: '/embedpdf/' }`. The module uses top-level `await`, so a Vite consumer needs `build.target: 'esnext'` (or another target with top-level await).

## Validation

The same checks as preview.6 (`.github/workflows/release-rust-viewer.yml`): a clean build, a clean tree afterwards, exactly one tarball whose `package.json` is `0.0.0-preview.7`, and a packed `dist/` that names the dispatched commit. Since the payload is new, the job builds against the `payload-wasm32` artifact from the same train run, and fails unless that artifact's `.payload-source` matches the hash the checkout derives. The same job builds the demo site and checks that it names the dispatched commit too. The tarball's SHA-256 and size are in the run's summary.

Before the release, the catch-up ran the new form surface through `WasmKernel` in `crates/epdf-web/tests/form_groups.rs` ([`ad53d425a`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/ad53d425a)), and replayed all 16,076 plugin conformance cases, recorded from the TypeScript plugins at `9af4d9ee7`, through the Rust plugins (`docs/research/upstream-catchup-2026-11-27/reports/CU-C7.md`). The train still doesn't run the bundle in a browser. The demo deploy is the first place this build runs in one, and nothing checks it there.

## Known gaps

- Signatures are drawn without sealing; no WebCrypto signer host is wired (`packages/rust-viewer-bundle/src/hosts.ts`).
- The Rust annotation adapter ignores a configured `randomUuid` host. Still open from preview.3.
- Not checked for this build: the bundle in a browser, the new form controls and group colors by hand, two viewers on one page, text selection from the pointer, and the package typecheck.
