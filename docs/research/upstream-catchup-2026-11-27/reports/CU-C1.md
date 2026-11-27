# CU-C1: runtime fork merge and PDFium libraries

The runtime fork branch `catchup/2026-11-27-runtime` (`9472117edbc7d9d980888c2418573081a76f532f`)
merges upstream runtime `7f399f838` into `af480d0ce` with no conflicts. It is a local branch in
`/Users/andrew/repos/embed-pdf-runtime` and is not pushed. Both built libraries show exactly the
change the plan expected: three exports renamed, five added, nothing else. Both load and answer the
renamed and new exports in a `pdfium-sys` probe. Decisions are in
[`../decisions/CU-C1.md`](../decisions/CU-C1.md); the container is described in
[`CU-C2.md`](CU-C2.md#container-layout).

## Commits

| Repository | Branch | Commit | Summary |
|---|---|---|---|
| runtime fork | `catchup/2026-11-27-runtime` | `9472117ed` | Merge upstream `7f399f838` into the fork |
| viewer | `research/catchup-2026-11-27-plugins` | the commit that adds this file | CU-C1 and CU-C2 reports, decisions and scripts |

## Runtime merge

`git merge --no-ff 7f399f838` on `af480d0ce`, made in a scratch worktree (CU-C1-D1). Upstream
brought 62 commits (50 non-merge) since `f2ea75972`. `scripts/runtime-verify.sh` confirms both
parents are ancestors of the merge.

The fork delta is the same 27 files before and after (`f2ea75972..af480d0ce` and
`7f399f838..9472117ed`), 1,306 and 1,305 inserted lines. The one line is in
`fpdfsdk/epdf_layer_embeddertest.cpp`: upstream now includes `fpdfsdk/epdf_edit_test_util.h` itself,
so the fork's added line became context. The file's blob is `72c0360fc` at both `af480d0ce` and the
merge, so the fork's test is unchanged.

Upstream `7f399f838` still has no Android branch in `core/fxcrt/epdf_secure_random.cpp`, so the
fork's `IS_ANDROID` lines stay. `check-fork-tags.py --base 7f399f838` exits 0 and reports the same 2
untagged blocks as 2026-11-18 (lines 16 and 41 of that file). The fork's
`tests/emsdk-version.test.sh` and `tests/target-args.test.sh` pass; their logs hash the same as
2026-11-18's.

### Export diff

| Library | Old | New | `EPDF` | `FPDF` |
|---|---|---|---|---|
| linux-arm64 `.so` | upstream release `f2ea75972` | upstream release `7f399f838` | 599 to 604 | 433 to 433 |
| wasm32-eh `.a` | fork build `af480d0ce` | fork build `9472117ed` | 599 to 604 | 433 to 433 |

Both libraries give the same diff (`scripts/lib-exports.sh`, defined global symbols):

| Removed | Added in its place |
|---|---|
| `EPDFAnnot_GetStampDrawing` | `EPDFAnnot_GetDrawing` |
| `EPDFAnnot_SetStampDrawing` | `EPDFAnnot_SetDrawing` |
| `EPDFDoc_GetStampDrawings` | `EPDFDoc_GetDrawings` |

New: `EPDFAnnot_GetMKIcon`, `EPDFAnnot_SetMKIcon`, `EPDFAnnot_HasMKIcon`, `EPDFAnnot_ClearMKIcon`
(all `public/epdf_annot.h`) and `EPDFText_GetPageGeometry` (`public/epdf_text.h`). No other export
changed.

The header scan in `runtime-verify.sh` also lists `EPDF_DIGEST_SHA256` as removed. That is the
scan, not the API: the pattern matched the name in a comment of `public/epdf_annot.h:1282` at
`af480d0ce` that upstream rewrote. `#define EPDF_DIGEST_SHA256 1` is still at
`public/epdf_digest.h:18`, and `crates/pdfium/src/stamp.rs` still compiles against it.

## PDFium libraries

| Target | Source | Archive sha256 | Container | Host copy |
|---|---|---|---|---|
| `linux-arm64` | upstream release `runtime-7f399f838df31a96393fdac0f8faa0ef30468f68` (CU-C1-D3) | `e79015fcc727bf8233bb85cc020f5bf5186596368f79ae73db3e269dd6bd5bb6`, the value upstream `9af4d9ee7` pins | `/workspaces/pdfium/archives/libembedpdf-runtime-linux-arm64-7f399f838.tar.gz` | none needed: the release is public |
| `wasm32-eh` | fork build of `9472117ed`, emsdk 4.0.0 | `a59c31a29f1c9b2cfa4107693df6e4d53e5f48fda027546b996a699921e5f81f` | `/workspaces/pdfium/archives/libembedpdf-runtime-wasm32-eh-9472117ed.tar.gz` | `/Users/andrew/repos/embed-pdf-runtime-builds/c1127-9472117ed/` (with `SHA256SUMS`, build script and logs) |

The wasm32-eh archive's `BUILD-METADATA.json` names `9472117edbc7d9d980888c2418573081a76f532f` and
`emsdkVersion` `4.0.0`. It was built in `embedpdf-runtime-build-c1127` (`debian:trixie`,
linux/amd64) by `scripts/build-wasm32-eh.sh` (CU-C1-D5), first attempt: 109 s dependency sync,
191 s build, 1 s packaging. `/workspaces/wasm32-eh-pin.json` (copy in `scripts/`) points
`fetch-libpdfium.sh wasm32-eh` at the local archive.

### Both libraries load

The probe crate `scripts/cu-c1-probe/` depends on the CU-C2 worktree's `crates/pdfium-sys`. Its one
test makes a document and a page, checks `EPDFDoc_GetDrawings` returns 0, makes a stamp and reads
`EPDFAnnot_GetDrawing` (0), `EPDFAnnot_HasMKIcon` (false), `EPDFAnnot_GetMKIcon` (0) and
`EPDFAnnot_ClearMKIcon` (false, a stamp has no MK dictionary), then loads the text page and calls
`EPDFText_GetPageGeometry` on its zero glyphs (0).

| Library | How it ran | Result |
|---|---|---|
| linux-arm64 | native test binary; `ldd` resolves `libembedpdf.so` to `/workspaces/pdfium/lib/linux-arm64/lib/` | 1 passed |
| wasm32-eh | `pdfium-sys` built for `wasm32-unknown-emscripten`, probe linked with emcc 4.0.0 (`/opt/emsdk-eh`, CU-C1-D4) against `libembedpdf.a` with `-fwasm-exceptions`, run under node | 1 passed |

`pdfium-sys`'s generated bindings have 1,070 functions on both targets (1,065 at 2026-11-18), the 8
new names among them and no `StampDrawing` name. `pdfium-sys` itself still has 0 tests.

## Evidence

Logs are in `/workspaces/logs/CU-C1/` in the container; the two host-side logs are in the host copy
folder above.

| Check | Command | Exit | Log | Log sha256 |
|---|---|---|---|---|
| Ancestry, fork delta, Android branch, header exports | `bash scripts/runtime-verify.sh` in `/workspaces/runtime` | 0 | `CU-C1-runtime-verify.log` | `b66c305e…` |
| emsdk pin test | `bash scripts/embedpdf-runtime/tests/emsdk-version.test.sh` | 0 | `CU-C1-emsdk-version.test.log` | `4e11ce45…` |
| target args test | `bash scripts/embedpdf-runtime/tests/target-args.test.sh` | 0 | `CU-C1-target-args.test.log` | `d485a117…` |
| Fork tag check | `python3 scripts/embedpdf-runtime/check-fork-tags.py --base 7f399f838` | 0 | `CU-C1-check-fork-tags.log` | `2f79bd31…` |
| linux-arm64 export diff | `bash scripts/lib-exports.sh <f2ea75972 .so> <7f399f838 .so>` | 0 | `CU-C1-exports-linux-arm64.log` | `dea4684c…` |
| wasm32-eh export diff | `bash scripts/lib-exports.sh <af480d0ce .a> <9472117ed .a>` | 0 | `CU-C1-exports-wasm32-eh.log` | `893fb3bc…` |
| wasm32-eh build | `heavy-build.sh CU-C1-wasm32eh -- docker exec embedpdf-runtime-build-c1127 bash /work/in/build.sh` | 0 | `wasm32-eh-run.out` | `8c21ca9a…` |
| `pdfium-sys` and probe, linux-arm64 | `bash scripts/pdfium-sys-check.sh /workspaces/wt/CU-C2 /workspaces/wt/CU-C1-probe` | 0 | `CU-C1-pdfium-sys-linux-arm64.log` | `41f13ead…` |
| `pdfium-sys` and probe, wasm32-eh | `bash scripts/pdfium-sys-wasm32-eh-check.sh /workspaces/wt/CU-C2 /workspaces/wt/CU-C1-probe` | 0 | `CU-C1-pdfium-sys-wasm32-eh.log` | `3ab3666f…` |
| Host copy of the archive | `shasum -a 256 -c SHA256SUMS` | 0 | (output `OK`) | |

The two `pdfium-sys` check scripts now find test binaries and `bindings.rs` with `find`, because
cargo 1.100 writes build output under `target/debug/build/<package>/<hash>/` rather than
`target/debug/deps/`.

## Known gaps

The runtime branch is local only, so `runtime-src` at `9472117ed` names a commit GitHub doesn't have
until the push in CU-C10. linux-arm64 comes from upstream's release, not the merge (CU-C1-D3). The
wasm32 (non-EH) library comes from upstream's release through the checkout's pin.

## Cleanup

Removed: container `embedpdf-runtime-build-c1127` and volume `embedpdf-runtime-build-c1127-work`,
after `docker inspect` showed only that volume and the read-only input folder mounted, and the host
copy passed `shasum -c`. Kept: the `debian:trixie` image (for CU-C9's Apple slices and CU-C11), the
scratch runtime worktree (`git worktree list` in `/Users/andrew/repos/embed-pdf-runtime`), and
`/Users/andrew/repos/embed-pdf-runtime-builds/c1127-9472117ed/`.
