# CU-C10: releases and pushes

The catch-up is published to the `roanutil` repositories: the runtime fork branch and its
`runtime-af480d0ce…` release, the wasm32 payload, the Apple engine, the pin commit `f37adb447` on
plugins, three viewer releases tagged there, and the impl and plugins branches. Every push was a
fast-forward, nothing went to `origin`, and a fresh clone resolves `runtime-src` and the new Apple
engine. Decisions are in [`../decisions/CU-C10.md`](../decisions/CU-C10.md).

## Runtime fork

`af480d0ce` pushed to `fork` `research/multi-platform-rust-core` (`17d8073d4..af480d0ce`) and as the
new branch `catchup/2026-11-18-runtime`.

Release [`runtime-af480d0ce420ed916db0de8d2e8827822ad7be1e`](https://github.com/roanutil/embed-pdf-runtime/releases/tag/runtime-af480d0ce420ed916db0de8d2e8827822ad7be1e),
prerelease, target `af480d0ce` (CU-C10-D1, CU-C10-D2):

| Asset | Bytes | sha256 | Built by |
|---|---|---|---|
| `libembedpdf-runtime-android-arm64-af480d0ce.tar.gz` | 10,425,555 | `83efd1971ff0a36ff6b547e93905af03938d79907bc5359a2462894166d131b0` | CU-C8c |
| `libembedpdf-runtime-android-x64-af480d0ce.tar.gz` | 10,758,195 | `27b931b2c9d1dc0395196ee2a11c4ba454d769b49dbd581b25c858199fa72019` | CU-C8c |
| `libembedpdf-runtime-linux-arm64-af480d0ce.tar.gz` | 5,032,863 | `232c1c7de1c1516ea373bdc5c9a662741476c1058180eccbbba24b9272098e20` | this job, 397 s |
| `libembedpdf-runtime-wasm32-af480d0ce.tar.gz` | 7,384,855 | `3e168e24be82bc8b51fee335cc21f65438d105f36ce735ce8a8928fb76d48abd` | this job, 192 s, emsdk 3.1.72 |
| `libembedpdf-runtime-wasm32-eh-af480d0ce.tar.gz` | 7,344,318 | `c6597d255e41de23ffa3b6797db7c5afd7d55344157859574479be679fc1f60f` | CU-C1, emsdk 4.0.0 |
| `pdf-runtime-build.generated.json` | 1,658 | `5ecbc7a4cc41aaca3c5fe6d0218c3cd3191456696ac8583ffe2cb70e8bd15419` | `write-manifest.sh` |

`scripts/cu-c10/verify-archives.sh`: every `BUILD-METADATA.json` names
`af480d0ce420ed916db0de8d2e8827822ad7be1e` and its own target, and every library defines
`EPDFDss_Add`, `EPDFDss_GetCount`, `EPDFDss_GetData` and `EPDFDoc_GetLoadedObjectValue`. In the
build container `check-c-api.py` reported 599 functions in 24 headers and `target-args.test.sh`
passed. The freshness gate exits 1 (13 commits behind, none matching the security pattern).

## Payload

[`runtime-payloads-56092bfe645aab76`](https://github.com/roanutil/embed-pdf-runtime/releases/tag/runtime-payloads-56092bfe645aab76),
prerelease, target `af480d0ce`, viewer commit `1b2d18408`, from a clean host worktree with
`PERF_WORK_DIR` and the hand-written stamp (CU-C10-D3). `engine-runtime-payload-wasm32.tar.gz`,
11,145,049 bytes, sha256 `3532285107d1a4db6216125b1d7d5424a7399813e574c1bfa5a12aeba0ac1b80`, the
same digest the dry run packed. `verify-rust-core.mjs` passed. The stamp omits `source`.

## Apple engine

`check-apple-engine.sh` before: exit 1, pinned `9c1ce4a8ff1cd488`, current `8a472a28bcfb9c14`.
`publish-engine-payload.mjs --only apple` with `EMBEDPDF_NM` (the 1.98.0 toolchain's `llvm-nm`),
`EMBEDPDF_TEST_DESTINATION` `platform=iOS Simulator,id=8D9A9044-5872-498A-80F7-D88D9C2C4BD3`, and
the routes helper's address in `EMBEDPDF_SIGNATURE_ROUTES` and
`TEST_RUNNER_EMBEDPDF_SIGNATURE_ROUTES`. The four slices came from CU-C9's stamps.

| Run | API tests | UI/model tests |
|---|---|---|
| iOS simulator (iPhone 17, iOS 27.2) | 519 of 519, 52 suites | 77 of 77, 14 suites |
| macOS arm64 | 519 of 519 | 77 of 77 |
| macOS x86_64 | 519 of 519 | 77 of 77 |

The route tests ran, not skipped: `aGrantedTimestampThroughTheTimestampRoute` and
`aLongTermFillThroughTheRevocationRoute` passed, and after the iOS run the helper's `/log` showed 2 timestamp queries
with 8-octet nonces and one OCSP POST. Skipped, as in CU-C9: `StageReaderDragLagTests` and
`PointerLatencyTests`. The macOS runs printed Xcode's Thread Performance Checker warning (a
user-interactive thread waiting on `ViewerHost.build`), which fails nothing. No test failed, so no
retry.

[`apple-engine-8a472a28bcfb9c14`](https://github.com/roanutil/embed-pdf-runtime/releases/tag/apple-engine-8a472a28bcfb9c14),
prerelease, target `af480d0ce`, viewer commit `1b2d18408`: `EmbedPDFBindings.xcframework.zip`,
114,489,360 bytes, SwiftPM checksum `3660debb61e685bce3c3421fc7baa505088c467711bb474af157f04f3036207d`,
plus its `.sha256`.

## Pin commit and viewer releases

`f37adb447` on plugins, on top of `1b2d18408`: the root `Package.swift` URL and checksum,
`platforms/apple/EmbedPDF/Package.swift` `engineRelease` and `engineChecksum`, and the bundle's
`0.0.0-preview.3` (CU-C10-D5). `check-apple-engine.sh` after: exit 0, current
`apple-engine-8a472a28bcfb9c14`.

| Release | Assets |
|---|---|
| [`0.0.0-swift.7`](https://github.com/roanutil/embed-pdf-viewer/releases/tag/0.0.0-swift.7) | none |
| [`rust-viewer-0.0.0-preview.3`](https://github.com/roanutil/embed-pdf-viewer/releases/tag/rust-viewer-0.0.0-preview.3) | `mfb-embedpdf-rust-viewer-0.0.0-preview.3.tgz`, 28,400,473 bytes, `1672ce2c2ad9d9809cecd39b95f01a0de7294bff59ca73b3f41e3691ece0ba0d` |
| [`dotnet-engine-0.0.0-local-f37adb44799d`](https://github.com/roanutil/embed-pdf-viewer/releases/tag/dotnet-engine-0.0.0-local-f37adb44799d) | `EmbedPDF.Engine.0.0.0-local.nupkg`, 9,384,244 bytes, `08a575929ca3d3821b9c8ed9f2949218be6b427e5112686c849f5221bb2447d9`; its `.sha256`; provenance; `validation.linux-arm64.json`; `EmbedPDF-CSharp-Existing-App.md` |

All three are prereleases with target `f37adb447` (CU-C10-D6).

The bundle was built with `build:all` and `pack:tarball` in `embed-pdf-catchup1118` against the
published payload. `scripts/cu-c10/consumer/` installed it into a fresh Vite 6.4.6 production
build and opened it in headless Chromium 159 with a URL `src` and a bytes `src` on
`examples/react/public/report.pdf`: one document, 14 pages, a painted page, a square annotation
created and read back, 8,610 hits for `e`, no console or page errors, no failed requests or HTTP
errors, `buildInfo` at `f37adb447` with `dirty: false`.

`scripts/cu-c10/dotnet-release.sh` ran the .NET package in the same container (CU-C10-D7):
`dotnet test` 35 of 35, parity self-test 16 of 16, packaging unit tests OK (10), `pack.sh` with the
DejaVu Sans font manifest, `validate.sh` PASS (`linux-arm64` `VALIDATED`), and the guide's steps in
a fresh project rendering `non_identity_text.pdf` page 0 to 918x1188. The native libraries need at
most `GLIBC_2.35`.

## Pushes

| Ref | Old | New |
|---|---|---|
| runtime `research/multi-platform-rust-core` | `17d8073d4` | `af480d0ce` |
| runtime `catchup/2026-11-18-runtime` | none | `af480d0ce` |
| viewer `research/multi-platform-rust-core-impl` | `a7c75f5ad` | `474599496` |
| viewer `research/multi-platform-rust-core-impl-plugins` | `cf7fdbe7a` | `f37adb447`, then this docs commit |
| viewer tags `0.0.0-swift.7`, `rust-viewer-0.0.0-preview.3`, `dotnet-engine-0.0.0-local-f37adb44799d` | none | `f37adb447` |

Each branch was checked with `git merge-base --is-ancestor` first. No force push; `origin` was not
touched.

## Fresh clone

A `--filter=blob:none` clone of roanutil/embed-pdf-viewer at `0.0.0-swift.7` (`f37adb447`):
`git submodule update --init` checked out `runtime-src` at `af480d0ce` from roanutil/embed-pdf-runtime;
`swift package resolve` in `platforms/apple/EmbedPDF` and at the root downloaded
`apple-engine-8a472a28bcfb9c14` and recorded checksum `3660debb…`. The clone was deleted.

## Evidence

Logs in `tmp/cu1118/logs/CU-C10/` of the host checkout, with the container's under `container/`.
Build records in `embed-pdf-runtime-builds/c1118-af480d0ce/` (`release/`, `rest/logs`,
`RELEASE-NOTES.md`, `verify-archives.out`), `rust-viewer-preview.3/` and `dotnet-f37adb447/`.
Scripts: `scripts/cu-c10/`.

## Cleanup

Host free space (`df -g /System/Volumes/Data`) read 412 GB at the start and 404 GB at the end;
`/workspaces` read 24 GB at both. Removed: the build container `embedpdf-runtime-build-c1118-c10`,
its volume and the amd64 `debian:trixie` image (the tag points at the arm64 image again), the host
worktree for the publish (5.6 GB with its `target`, `node_modules` and framework
builds), the container worktree `/workspaces/wt/CU-C10` (2.9 GB), its scratch and log directories
(about 440 MB), `/workspaces/target/aarch64-unknown-linux-gnu` (219 MB), the fresh clone and the
temporary refs. Kept: the `wasm32-eh` stamp and its 21 MB staging directory in the fork cache, so
a later publish at this pin skips the build, and the release records above.
