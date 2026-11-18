# CU-C1: dev container and runtime fork merge

The dev container `embed-pdf-catchup1118` is up with both clones, the helper scripts, linux-arm64
and wasm32-eh PDFium, emsdk 4.0.0 and `wasm-bindgen` 0.2.127. The runtime fork branch
`catchup/2026-11-18-runtime` (`af480d0ce420ed916db0de8d2e8827822ad7be1e`) merges upstream runtime
`f2ea75972` into `17d8073d4` with no conflicts. It is a local branch in
`/Users/andrew/repos/embed-pdf-runtime` and is not pushed. Upstream added 4 exports and removed
none, in the headers and in both built libraries. Both libraries load and answer the new exports
in a `pdfium-sys` probe test. Decisions are in [`../decisions/CU-C1.md`](../decisions/CU-C1.md).

## Commits

| Repository | Branch | Commit | Summary |
|---|---|---|---|
| runtime fork | `catchup/2026-11-18-runtime` | `af480d0ce` | Merge upstream `f2ea75972` into the fork |
| viewer | `research/catchup-2026-11-18-c1` | this commit | CU-C1 report, decisions and the changed or new helper scripts |

No viewer code, vectors or packages changed, so there are no test counts before or after and no
regenerated vector groups.

## Container layout

Disk at the start: 459 GiB free on `/System/Volumes/Data`; 455 GiB after the build
(`container-state.log`).

Container `embed-pdf-catchup1118`, image `embedpdf-dev:latest` (arm64, `3b69d254a4a3`, built
2026-11-16), volume `embedpdf-catchup1118` at `/workspaces`, started with `--init` and
`sleep infinity`. Git identity is set globally for user `node`. Use
`docker exec embed-pdf-catchup1118 bash -c '...'`.

| Path | Contents |
|---|---|
| `/workspaces/embed-pdf-viewer` | Viewer clone, detached at plugins `cf7fdbe7a`. Local branches from the host, `fork/*`, and `upstream/*` (upstream `main` = `c4e744345`). Remotes `fork` and `upstream` point at GitHub (CU-C1-D2). |
| `/workspaces/runtime` | Runtime fork, blobless clone. Remotes `fork` (roanutil/embed-pdf-runtime) and `upstream` (embedpdf/runtime, also blobless). Branch `catchup/2026-11-18-runtime` checked out. |
| `/workspaces/wt/` | Job worktrees. `CU-C1` (detached at `cf7fdbe7a`, holds `target/` from the checks) and `CU-C1-probe` (the probe crate). |
| `/workspaces/logs/`, `/workspaces/bundles/` | Logs (`logs/CU-C1/`), git bundles (`viewer-host.bundle`, `runtime-catchup-2026-11-18.bundle`) |
| `/workspaces/pdfium/archives/` | `libembedpdf-runtime-linux-arm64-f2ea75972.tar.gz`, `libembedpdf-runtime-wasm32-eh-af480d0ce.tar.gz`, and the previous pair (`linux-arm64-aa59ce554`, `wasm32-eh-17d8073d4`) for the export diff |
| `/workspaces/pdfium/lib/{linux-arm64,wasm32-eh}` | The two new archives extracted. `PDF_RUNTIME_LIB_DIR=/workspaces/pdfium/lib` points `pdfium-sys` here. |
| `/workspaces/pdfium/prev/{linux-arm64,wasm32-eh}` | The previous pair extracted |
| `/workspaces/wasm32-eh-pin.json` | Pin file for the new wasm32-eh archive |
| `/workspaces/emsdk4` | emsdk 4.0.0 (emsdk `708dcbc`), for wasm32-eh links. The image's own emsdk at `/opt/emsdk` stays 3.1.72. |
| `/workspaces/bin/clang` | Clang shim (`clang-shim.sh`) |
| `/workspaces/*.sh`, `jdiff.py`, `cu-c3/`, `cu-c5/`, `swift-linux/` | Every file of `docs/research/upstream-catchup-2026-11-12/scripts/`, with `build-wasm32-eh.sh` and `wasm32-eh-pin.json` replaced by the versions in `scripts/` here, plus this job's new scripts |

`wasm-bindgen` 0.2.127 comes with the image, in `/home/node/.cargo/bin` (CU-C1-D4).
`platforms.sh` expects `/workspaces/tools` (.NET, JDK, kotlinc), which this job did not install.

## Runtime merge

`git merge --no-ff f2ea75972` on `17d8073d4` had no conflicts. Upstream brought 57 commits (52
non-merge) since `aa59ce554`: the PDFium sync to `1f02bc7d90`, free text border dash and width
(runtime PR #118, #121), and the document security store (runtime PR #120). The fork's diff against
upstream is the same 27 files, 1,306 insertions and 112 deletions, before and after the merge: the
diffs `aa59ce554..17d8073d4` and `f2ea75972..af480d0ce` match line for line once `index` and `@@`
lines are dropped.

Upstream `f2ea75972` still has no Android branch in `core/fxcrt/epdf_secure_random.cpp`: the file is
unchanged since `aa59ce554`. The fork's `3c6e02297` stays (CU-C1-D1).

The fork's script tests pass on the merge: `tests/emsdk-version.test.sh` and
`tests/target-args.test.sh`. `check-fork-tags.py --base f2ea75972` exits 0 and reports 2 untagged
blocks, both the Android lines at 16 and 41 of `epdf_secure_random.cpp` (CU-C1-D7).

### Export diff, `aa59ce554` to `f2ea75972`

Names matching `(EPDF|FPDF)\w*(` in `public/*.h` (`scripts/runtime-verify.sh`): `EPDF` 628 before,
632 after; `FPDF` 477 both. The built libraries agree (`scripts/lib-exports.sh`, defined global
symbols): `EPDF` 595 to 599 and `FPDF` 433 to 433 in both the linux-arm64 `.so` (release
`aa59ce554` to release `f2ea75972`) and the wasm32-eh `.a` (`17d8073d4` to `af480d0ce`). The header
counts are higher because the pattern also matches names in comments and macros.

| Added | Header |
|---|---|
| `EPDFDoc_GetLoadedObjectValue` | `public/epdf_signature.h` |
| `EPDFDss_Add` | `public/epdf_signature.h` |
| `EPDFDss_GetCount` | `public/epdf_signature.h` |
| `EPDFDss_GetData` | `public/epdf_signature.h` |

Removed: none, in the headers or in either library. Nothing for Rust or TS loses a symbol.

Upstream viewer `c4e744345` calls the three `EPDFDss_*` functions in
`packages/engine/services/src/features/signature/internal/validationStore.ts` and
`EPDFDoc_GetLoadedObjectValue` in `packages/engine/services/src/features/signature/SignatureAnalyzer.ts`.
All four are listed in `packages/engine/runtime/src/core/pdf-functions.generated.ts`, and they are
named in `packages/engine/services/CHANGELOG.md` and `api/embedpdf__engine-services/index.api.txt`.
Plugins `cf7fdbe7a` has no reference to any of them anywhere in the tree. `pdfium-sys` picks them
up from the headers with no change (its generated bindings have 1,065 functions, the 4 among them),
so the signature port in a later job can call them directly.

## PDFium libraries

| Target | Source | Archive sha256 | Container | Host copy |
|---|---|---|---|---|
| `linux-arm64` | Upstream release `runtime-f2ea7597287f26bdcb450602de6616ff6939d506` | `6a153ba12c3fe05a4177e278ac7130008a9598070eb23a46849bac08b209509a` (matches upstream `engine-runtime-build.json` at `c4e744345`) | `/workspaces/pdfium/archives/libembedpdf-runtime-linux-arm64-f2ea75972.tar.gz` | none needed: the release is public |
| `wasm32-eh` | Fork build of `af480d0ce`, emsdk 4.0.0 | `c6597d255e41de23ffa3b6797db7c5afd7d55344157859574479be679fc1f60f` | `/workspaces/pdfium/archives/libembedpdf-runtime-wasm32-eh-af480d0ce.tar.gz` | `/Users/andrew/repos/embed-pdf-runtime-builds/c1118-f2ea75972/` (with `SHA256SUMS`, build script and logs) |

The wasm32-eh archive's `BUILD-METADATA.json` names `af480d0ce420ed916db0de8d2e8827822ad7be1e` and
`emsdkVersion` `4.0.0`. Its `lib/libembedpdf.a` has sha256
`fc55c432157a980f3670214cf053d22ed10d8fa3673bff13028c0ad79fdf4e53`.

The build ran in `embedpdf-runtime-build-c1118` (`debian:trixie`, linux/amd64, volume
`embedpdf-runtime-build-c1118-work`) with `scripts/build-wasm32-eh.sh`, first attempt: 103 s
dependency sync, 212 s build, 1 s packaging. The script changes are in CU-C1-D5.

### Both libraries load

`cargo test -p pdfium-sys` passes against linux-arm64, but `pdfium-sys` has 0 tests, so the check
that loads the libraries is a probe crate (`scripts/cu-c1-probe/`, CU-C1-D8). Its one test
initialises PDFium, makes a document and a page, checks `EPDFDss_GetCount` is 0 then -1 for an
unknown kind, adds 5 bytes with `EPDFDss_Add` (object 6), reads them back with `EPDFDss_GetData`,
and calls `EPDFDoc_GetLoadedObjectValue` (0, status -1: a new document has no parser).

| Library | How it ran | Result |
|---|---|---|
| linux-arm64 | native test binary; `ldd` resolves `libembedpdf.so` to `/workspaces/pdfium/lib/linux-arm64/lib/` | 1 passed |
| wasm32-eh | `pdfium-sys` built for `wasm32-unknown-emscripten` (bindgen over the wasm32-eh headers), probe linked with emcc 4.0.0 against `libembedpdf.a` with `-fwasm-exceptions`, run under node 24.20.0 | 1 passed |

### How later jobs fetch the libraries

Once a checkout's `packages/engine/runtime/engine-runtime-build.json` pins `f2ea75972` (after CU-C2),
`prep.sh` does both:

```sh
bash packages/engine/runtime/scripts/fetch-libpdfium.sh linux-arm64
PDF_RUNTIME_BUILD_FILE=/workspaces/wasm32-eh-pin.json \
  bash packages/engine/runtime/scripts/fetch-libpdfium.sh wasm32-eh
```

The second was run in `/workspaces/wt/CU-C1`: it copied the local archive (`file://` URL), checked its
sha256 and unpacked a `libembedpdf.a` identical to `/workspaces/pdfium/lib/wasm32-eh`.

## Tool versions

| Tool | Version | Where |
|---|---|---|
| emsdk (image) | 3.1.72 | `/opt/emsdk` |
| emsdk (wasm32-eh) | 4.0.0 | `/workspaces/emsdk4` |
| `wasm-bindgen-cli` | 0.2.127 | `/home/node/.cargo/bin` (image) |
| cargo | 1.98.0 | image |
| node | 24.20.0 | image |
| pnpm | 10.34.0 | image |

## Evidence

Viewer branch base `cf7fdbe7a26e20863684bb7cced4de9cc6d47356`; head is the commit that adds this
file. Runtime: base `17d8073d49bab5c29d956568345652bf893b459c`, upstream
`f2ea7597287f26bdcb450602de6616ff6939d506`, head `af480d0ce420ed916db0de8d2e8827822ad7be1e`.
Logs are in `/tmp/cu1118/logs/CU-C1/` on the host, hashed in its `SHA256SUMS`, and in
`/workspaces/logs/CU-C1/` in the container.

| Check | Command | Exit | Log | Log sha256 |
|---|---|---|---|---|
| Merge ancestry, fork delta, Android branch, header export diff | `bash scripts/runtime-verify.sh` (in `/workspaces/runtime`) | 0 | `CU-C1-runtime-verify.log` | `c20c696526a9719346c94863227855007d21e2ec83d945e9fceb6791a4630169` |
| emsdk pin test | `bash scripts/embedpdf-runtime/tests/emsdk-version.test.sh` | 0 | `CU-C1-emsdk-version.test.log` | `4e11ce45a8448fdb27d5fa86a205593efffad143a951fa836ec6473963f03c36` |
| target args test | `bash scripts/embedpdf-runtime/tests/target-args.test.sh` | 0 | `CU-C1-target-args.test.log` | `d485a1179d9c0901e8ddf3479e2cffdac88fb9f2613e13cd4e29c8776b7daecc` |
| Fork tag check | `python3 scripts/embedpdf-runtime/check-fork-tags.py --base f2ea75972` | 0 | `CU-C1-check-fork-tags.log` | `38bca7b064bf1d9a0f899263cafe2b88442a8ac4d3e981b629ea1871ba6efdb5` |
| Viewer callers of the new exports | `git grep -cE` over `c4e744345` and `cf7fdbe7a` | 0 | `CU-C1-export-callers.log` | `d07f7a8031f53499911d6a52acd6b9e20412f745145246c525d6622930c6682e` |
| linux-arm64 library export diff | `bash scripts/lib-exports.sh <aa59ce554 .so> <f2ea75972 .so>` | 0 | `CU-C1-exports-linux-arm64.log` | `e5d865c92415696a136fb103a6dc49649372dac6f13790d18a128bcfe7535248` |
| wasm32-eh library export diff | `bash scripts/lib-exports.sh <17d8073d4 .a> <af480d0ce .a>` | 0 | `CU-C1-exports-wasm32-eh.log` | `f0202873a6d70fc1396b271f48778e29f50bf766fdb615afc13292e586a58d36` |
| wasm32-eh build | `heavy-build.sh CU-C1-wasm32eh -- docker exec embedpdf-runtime-build-c1118 bash /work/in/build.sh` | 0 | `wasm32-eh-run.out` | `b2396d23213ca8c77f4579f9e644a6dda0173a7ff67adbbfeea3e38daaa37578` |
| wasm32-eh fetch through the pin | `PDF_RUNTIME_BUILD_FILE=/workspaces/wasm32-eh-pin.json fetch-libpdfium.sh wasm32-eh` | 0 | `CU-C1-fetch-wasm32-eh.log` | `9f7597cfe55035911961d2dd779e93fcb67df60641ab27de858c9dc53498a3a9` |
| `pdfium-sys` and probe, linux-arm64 | `heavy-build.sh CU-C1-pdfium-sys -- docker exec … bash /workspaces/pdfium-sys-check.sh /workspaces/wt/CU-C1 /workspaces/wt/CU-C1-probe` | 0 | `CU-C1-pdfium-sys-linux-arm64.log` | `63156bfcb7d432989cb7e524cb92e8cb8820badbf8d64a4724fea58b8298c0e0` |
| `pdfium-sys` and probe, wasm32-eh | `heavy-build.sh CU-C1-pdfium-sys-wasm -- docker exec … bash /workspaces/pdfium-sys-wasm32-eh-check.sh /workspaces/wt/CU-C1 /workspaces/wt/CU-C1-probe` | 0 | `CU-C1-pdfium-sys-wasm32-eh.log` | `9760aee6588172a09b2fa30580451fba7e8d46334c3271e5537d22b2641ca52f` |
| emsdk 4.0.0 install | `./emsdk install 4.0.0 && ./emsdk activate 4.0.0` | 0 | `CU-C1-emsdk4.log` | `4092546b6b099540315503156635e137136df90a1ad0db8bda3f2414bf59edbe` |
| Container state, disk, tool versions, script hashes | `df`, `docker system df`, `docker ps`, `ls`, `--version`, `sha256sum` | 0 | `container-state.log` | `aec1473b21c90b2b15b6627473af17be9ff12aee495faf2b351c01eba26992d7` |
| wasm32-eh archive on host | `shasum -a 256 -c SHA256SUMS` in the host copy | 0 | (output `OK`) | |

## Known gaps

The runtime branch is local only: a `runtime-src` pin to `af480d0ce` names a commit GitHub doesn't
have until `catchup/2026-11-18-runtime` is pushed. linux-arm64 comes from upstream's `f2ea75972`
release, not the fork merge (CU-C1-D3). No native addon, payload or viewer suite has run against the
new libraries yet; CU-C2 does the first. The wasm32 (non-EH) library is not prepared here:
checkouts fetch it from upstream's release through their own pin.

## Cleanup

Removed: container `embedpdf-runtime-build-c1118` and volume `embedpdf-runtime-build-c1118-work`
(after `docker inspect` showed only that volume mounted, and the archive passed `shasum -c` on the
host). Kept for later jobs: container `embed-pdf-catchup1118`, volume `embedpdf-catchup1118`, and in
it the worktrees `/workspaces/wt/CU-C1` and `/workspaces/wt/CU-C1-probe`. Host files:
`/tmp/cu1118/` (bundles, logs) and `/Users/andrew/repos/embed-pdf-runtime-builds/c1118-f2ea75972/`.
