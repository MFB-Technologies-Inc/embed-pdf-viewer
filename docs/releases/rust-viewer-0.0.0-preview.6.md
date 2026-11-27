**Pre-release, evaluation only.** The full EmbedPDF viewer on the Rust kernel and plugins (`crates/epdf-web` through `@embedpdf/core-wasm`), packed as one self-contained tarball: the viewer, the kernel's WASM, PDFium's WASM, the engine's workers and Preact. No peer dependencies, no Rust toolchain, no Vite plugin needed. Never published to npm.

## What changed since preview.5

The JS viewer API is the same. Nothing under `packages/core-wasm`, `packages/rust-viewer-bundle/src` or `examples/rust-kernel-eval` has changed since `1ba25cb9a`, the commit preview.5 tags, and the runtime fork is still `af480d0ce`. What changed is the Rust underneath: a new toolchain, new dependency versions, and one change a page can observe, in signature validation.

The Rust toolchain moved from 1.98.0 to 1.100.0 ([`e4544c225`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/e4544c225)), which renamed `fetch_update` to `try_update` in `crates/epdf-engine` ([`c2203ca2f`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/c2203ca2f)). `Cargo.lock`, `rust-toolchain.toml` and `crates/epdf-engine` are payload inputs, so the train builds a new `runtime-payloads-<hash>` for this release instead of reusing preview.5's `runtime-payloads-ed16549dd6e1e977`. The engine's search crates also took patch bumps, `regex` 1.13.1 to 1.13.2 and `unicode-normalization` 0.1.24 to 0.1.26 ([`a6a2e6f72`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/a6a2e6f72)).

The kernel's WASM is built with `wasm-bindgen` 0.2.130, up from 0.2.127, with `wasm-bindgen-futures` and `js-sys` to match ([`041358bb2`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/041358bb2)). The kernel now uses `base64` 0.23 ([`6266d39bb`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/6266d39bb)). Neither changes an export.

The RustCrypto crates moved together, `rsa` 0.9 to 0.10, `sha1`/`sha2` 0.11, `ecdsa` 0.17 and `p256`/`p384`/`p521` 0.14, and `getrandom` went from 0.2 to 0.4 in the browser build, where `crates/epdf-web` now enables `wasm_js` and draws the kernel's UUID entropy through `getrandom::fill` ([`56fdd0c79`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/56fdd0c79)). Signature validation runs on these crates in the browser, and one result differs: an RSA-PSS signature whose value is at or past the key's modulus now fails to verify, where `rsa` 0.9 verified it when it fit in the key's byte length. OpenSSL refuses it too ([`1ab489544`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/1ab489544)). RSA keys under 1024 bits still import, as WebCrypto imports them ([`84115762d`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/84115762d)).

The rest of the `crates` diff doesn't reach this bundle. Annotation duplicate, movability and remeasure ([`5480d0afa`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/5480d0afa)), form field and widget duplication ([`ebf3fa761`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/ebf3fa761)), the `markMatches` cancel ([`bf0d8af2a`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/bf0d8af2a)) and the shared cancel helpers in `crates/epdf-ffi` ([`6eee050fe`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/6eee050fe)) are all uniffi exports for the Swift, Kotlin and .NET hosts, behind each plugin's `uniffi` feature, which `crates/epdf-web` doesn't enable. The plugin methods they wrap haven't changed, so what JS reaches through `@embedpdf/core-wasm` is what preview.5 reached. The `png` 0.18 bump ([`ca221502e`](https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/commit/ca221502e)) is in `crates/epdf-viewer-host` only, and `crates/uniffi-bindgen-cs` is a new build tool for the .NET bindings.

The train redeploys the hosted demo, https://embed-pdf-rust-kernel-eval.roan-util-cloudfare.workers.dev, from the same build.

## Install

```sh
npm i https://github.com/MFB-Technologies-Inc/embed-pdf-viewer/releases/download/rust-viewer-0.0.0-preview.6/mfb-embedpdf-rust-viewer-0.0.0-preview.6.tgz
```

```ts
import { createViewer, buildInfo } from '@mfb/embedpdf-rust-viewer';

const el = await createViewer({ target: '#viewer', src: '/report.pdf' }); // give #viewer a height
el.addEventListener('epdf:ready', () => console.log(el.viewer!.documents.list()));
console.log(buildInfo);
```

`createViewer` takes every `EmbedPDF.init` option. Assets resolve relative to the bundle; if a bundler loses them, copy `dist/` to a static path and pass `engine: { assetsUrl: '/embedpdf/' }`. The module uses top-level `await`, so a Vite consumer needs `build.target: 'esnext'` (or another target with top-level await).

## Validation

The same checks as preview.5 (`.github/workflows/release-rust-viewer.yml`), now on `ubuntu-26.04` instead of `ubuntu-24.04`: a clean build, a clean tree afterwards, exactly one tarball whose `package.json` is `0.0.0-preview.6`, and a packed `dist/` that names the dispatched commit. The job checks `wasm-bindgen` is 0.2.130 and `wasm-opt` is binaryen 133 before it builds. Since the payload is new, the job builds against the `payload-wasm32` artifact from the same train run rather than a released payload, and fails unless that artifact's `.payload-source` matches the hash the checkout derives. The same job built the demo site and checked that it names the dispatched commit too. The tarball's SHA-256 and size are in the run's summary.

The train still doesn't run the bundle in a browser. The demo deploy is the first place this build runs in one, and nothing checks it there. That matters more for this release than for preview.5: the `getrandom` 0.4 `wasm_js` entropy source and the new RustCrypto crates haven't run in a browser before this build.

## Known gaps

- Signatures are drawn without sealing; no WebCrypto signer host is wired (`packages/rust-viewer-bundle/src/hosts.ts`).
- The Rust annotation adapter ignores a configured `randomUuid` host. Still open from preview.3; `packages/core-wasm` and the annotation plugin outside its uniffi exports haven't changed since preview.5.
- Not checked for this build: the bundle in a browser, two viewers on one page, text selection from the pointer, and the package typecheck.
