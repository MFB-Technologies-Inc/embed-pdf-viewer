# CU-C1 decisions

Decisions made while preparing the dev container, merging upstream runtime `f2ea75972` into the
runtime fork and preparing the PDFium libraries. Each one is committed in the same commit as this
file on `research/catchup-2026-11-18-c1`.

## CU-C1-D1. Keep the fork's Android secure-random branch

Decision: keep `3c6e02297` ("Give Android a secure random source") in the merge.
Reason: upstream `f2ea75972` still has no Android branch in `core/fxcrt/epdf_secure_random.cpp`.
`git diff aa59ce554 f2ea75972 -- core/fxcrt/epdf_secure_random.cpp` is empty: the platform list is
still `IS_WIN`, `IS_APPLE`, `IS_ASMJS`, `IS_LINUX`, then `#error`.
Cost: the fork keeps a 4-line change in an upstream file.
Reverse: when upstream adds an Android branch, take upstream's side of that file in the next merge.

## CU-C1-D2. Clone the viewer from a bundle of the host repository, detached at plugins

Decision: `/workspaces/embed-pdf-viewer` comes from `git bundle create --branches --remotes` of
`/Users/andrew/repos/embed-pdf-viewer`, as in 2026-11-12 (CU-C1-D2 there): local branches map to
local branches, `fork/*` to `fork/*`, and the host's `origin/*` (embedpdf) to `upstream/*`. The
clone is detached at plugins `cf7fdbe7a`, where the 2026-11-12 clone sat at impl.
Reason: the host has branches GitHub lacks, such as `research/catchup-2026-11-18-plan`. Plugins is
where the Rust port and this job's `pdfium-sys` check run.
Cost: the clone's remote-tracking refs are as fresh as the host's last fetch, not GitHub's.
Reverse: `git fetch fork` and `git fetch upstream` inside the container.

## CU-C1-D3. linux-arm64 PDFium from upstream's release, not a fork build

Decision: use `libembedpdf-runtime-linux-arm64-f2ea75972.tar.gz` from upstream release
`runtime-f2ea7597287f26bdcb450602de6616ff6939d506`.
Reason: the release exists and its sha256 matches upstream viewer `c4e744345`'s
`engine-runtime-build.json`. The fork's delta from upstream is the same 27 files as before the
merge (build scripts, patches, args goldens, `skia/BUILD.gn`, a test, docs and the Android branch),
and none of it changes a linux-arm64 library.
Cost: the linux-arm64 library is built from `f2ea75972`, not from the merge.
Reverse: build linux-arm64 from the merge with the fork's `build-target.sh`.

## CU-C1-D4. wasm-bindgen-cli 0.2.127 comes from the image

Decision: no `cargo install wasm-bindgen-cli`. The image `embedpdf-dev:latest` (`3b69d254a4a3`,
built 2026-11-16) already has `wasm-bindgen` 0.2.127 in `/home/node/.cargo/bin`.
Reason: plugins `cf7fdbe7a` `Cargo.lock` pins `wasm-bindgen` 0.2.127, the version the image carries.
Unlike 2026-11-12, recreating the container keeps it.
Cost: none while the lock stays at 0.2.127.
Reverse: `cargo install wasm-bindgen-cli --version <v> --locked --force` if CU-C2/C3 move the lock.

## CU-C1-D5. wasm32-eh build environment and a merge-only bundle

Decision: container `embedpdf-runtime-build-c1118` from `debian:trixie` (linux/amd64), volume
`embedpdf-runtime-build-c1118-work`, run by `scripts/build-wasm32-eh.sh`. The script now clones the
fork's `research/multi-platform-rust-core` (which holds `17d8073d4`) and fetches upstream's tag
`runtime-f2ea7597287f26bdcb450602de6616ff6939d506` into a second promisor remote, so the bundle
carries only the merge commit (1,640 bytes). Branch, fork branch and tag are variables with this
catch-up's values as defaults.
Reason: the 2026-11-12 script cloned `fix/android-secure-random` and named the 2026-11-12 branch.
Both parents of this merge are on GitHub, so nothing else needs bundling.
Cost: the build needs network access to both GitHub repositories.
Reverse: bundle the whole range and drop the upstream fetch.

## CU-C1-D6. No fork ledger or sync note for this merge

Decision: the runtime merge commit adds no `docs/embedpdf/syncs/` note and leaves
`docs/embedpdf/fork-ledger.md` as is, as for `17d8073d4` (2026-11-12 CU-C1-D6).
Reason: the merge had no conflicts and the fork delta is identical before and after it.
Cost: the fork's own docs do not mention this merge; the merge commit message and the report do.
Reverse: add a note in a later commit on `catchup/2026-11-18-runtime`.

## CU-C1-D7. Leave the untagged Android blocks reported by `check-fork-tags.py`

Decision: no `EmbedPDF(<rule>)` tag added to `epdf_secure_random.cpp`.
Reason: the 2 untagged blocks (lines 16 and 41) are `3c6e02297`'s, unchanged since 2026-11-12. A tag
needs a ledger rule, and the ledger changes only with review. The check exits 0.
Cost: the tag check keeps reporting 2 untagged blocks.
Reverse: add a ledger rule and tag the blocks in a reviewed runtime commit.

## CU-C1-D8. Prove the libraries load with a probe crate outside the workspace

Decision: the `pdfium-sys` check is `cargo test -p pdfium-sys` plus a probe crate,
`scripts/cu-c1-probe/`, that depends on `crates/pdfium-sys` by path and calls into the library:
it makes a document and a page, then exercises `EPDFDss_GetCount`, `EPDFDss_Add`,
`EPDFDss_GetData` and `EPDFDoc_GetLoadedObjectValue`. It runs against linux-arm64 natively and
against wasm32-eh under node.
Reason: `pdfium-sys` has 0 tests of its own (bindgen 0.72.1 emits its layout checks as compile-time
assertions), so `cargo test -p pdfium-sys` proves the bindings compile but loads nothing. On
`wasm32-unknown-emscripten`, `pdfium-sys` emits no link directives at all. The plugins branch must
not change in CU-C1, so the probe lives in this job's scripts.
Cost: one more script directory to keep; the probe uses plugins' `Cargo.lock`.
Reverse: delete `scripts/cu-c1-probe/`.
