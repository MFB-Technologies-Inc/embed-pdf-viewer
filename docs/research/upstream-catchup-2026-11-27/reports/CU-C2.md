# CU-C2 (with CU-C0): upstream merge, dev image, container, start state

`research/catchup-2026-11-27-plugins` merges upstream main `9af4d9ee7` into plugins `d250a68e3`, with
`runtime-src` at CU-C1's fork merge `9472117ed`. Only `runtime-src` and `PageGeometryReader.ts`
conflicted, as the probe said. `cargo build --workspace` is green after a three-name binding rename,
and `cargo test --workspace` passes in full: 3,567 passed, 0 failed, 2 ignored, the same counts as at
`d250a68e3`. The breakage is all on the TS and vector side: 6 of 36 engine `:check` scripts and all
4 plugin checks fail at the merge, against 36 of 36 and 3 of 4 at the baseline. Decisions are in
[`../decisions/CU-C2.md`](../decisions/CU-C2.md).

## Commits

| Commit | Summary |
|---|---|
| `d670a1cbf` | Merge upstream `9af4d9ee7`; `runtime-src` to `9472117ed`; `PageGeometryReader.ts` upstream's and `peekWords` removed (CU-C2-D1); lockfile upstream's |
| `5cacfd717` | `pnpm-lock.yaml` re-resolved in the container (CU-C2-D3) |
| `f8a124edc` | `crates/pdfium/src/stamp.rs` and `crates/epdf-engine/src/features/annotation/write/stamp.rs` call `EPDFAnnot_GetDrawing`, `EPDFAnnot_SetDrawing`, `EPDFDoc_GetDrawings` |
| the commit that adds this file | CU-C1 and CU-C2 reports, decisions and scripts |

All on `research/catchup-2026-11-27-plugins`, on top of the plan commits `26856f365` and `3cc3c7887`.
`5cacfd717` and `f8a124edc` were made in the container and fetched onto the host branch from
`catchup-plugins-jobA.bundle` with a fast-forward.

## Merge

`scripts/sync-upstream.sh --from upstream/9af4d9ee7-tmp --runtime-commit 9472117ed…` on the host,
no `--push`, after `git submodule update --init --reference /Users/andrew/repos/embed-pdf-runtime`
for `runtime-src`. The script set `runtime-src` to `9472117ed` and kept `.gitmodules`, then stopped
on the one content conflict. It also moved a branch in the worktree's own submodule clone
(CU-C2-D2). `upstream/9af4d9ee7-tmp` is a local branch at `9af4d9ee7`.

| Path | Resolution |
|---|---|
| `packages/engine/runtime/runtime-src` | `9472117edbc7d9d980888c2418573081a76f532f` |
| `packages/engine/services/src/features/geometry/PageGeometryReader.ts` | upstream's file, byte for byte. `PdfRuntimeMemory.peekWords` dropped from `packages/engine/runtime/src/core/pdf-runtime-module.ts` and `packages/engine/runtime/src/wasm/wasm-runtime.ts` (CU-C2-D1) |
| `pnpm-lock.yaml` | merged cleanly, replaced by upstream's, then re-resolved: the importers `examples/rust-kernel-eval`, `packages/core-wasm`, `packages/rust-viewer-bundle`, `tooling/engine-baseline`, `tooling/engine-conformance`, `tooling/plugin-conformance`, `tooling/viewer-e2e` and `tooling/wasm-eh-check` come back |

`git merge-base d250a68e3 9af4d9ee7` is `c4e744345`, the previous target, so the host clone was deep
enough.

## CU-C0: image

`.devcontainer/build.sh` at `d670a1cbf` built `embedpdf-dev:20261127-d670a1cbfb10`, also tagged
`embedpdf-dev:latest`: image id `sha256:a3b58f11b8e0a1c7341e356de20b36888762a4d8ec545695ec1b12143ed2af5f`,
arm64, 8.12 GB, created `2026-11-27T08:30:12Z`. `docker image inspect` shows
`org.opencontainers.image.revision` = `d670a1cbfb1064026012b53860dd75499fc6df46`, with no `-dirty`
suffix. The previous `latest` (`00a0acd5a4b2`, from `efb66a7bd`) is now untagged, and
`embedpdf-dev:20261127-94822b7a9e29` (`9b30cd30bea5`) is untouched; both are CU-C11's to remove.

`python3 .devcontainer/check-pins.py` in `/workspaces/wt/CU-C2` exits 0: "Development image pins
match the repository."

## Container layout

Container `embed-pdf-catchup1127`, image `embedpdf-dev:20261127-d670a1cbfb10`, volume
`embedpdf-catchup1127` at `/workspaces`, `--init`, `sleep infinity`, user `node`. Git identity
`Andrew Roan <roanutil@users.noreply.github.com>` is set globally. Use
`docker exec embed-pdf-catchup1127 bash -c '…'`; `source /workspaces/scripts/env.sh <checkout>
<target-dir>` sets the cargo and PDFium variables.

| Path | Contents |
|---|---|
| `/workspaces/embed-pdf-viewer` | Viewer clone from a bundle of the host repository (`--branches`, `fork/*`, `origin/main` as `upstream/main` = `9af4d9ee7`), detached at `d250a68e3`. Remotes `fork` and `upstream` point at GitHub; `hostbundle` is the bundle. Not shallow. |
| `/workspaces/runtime` | Runtime fork, blobless clone of `MFB-Technologies-Inc/embed-pdf-runtime` (remote `fork`), remote `upstream` (embedpdf/runtime) with tags `runtime-7f399f83…` and `runtime-f2ea7597…`. Branch `catchup/2026-11-27-runtime` at `9472117ed` checked out. |
| `/workspaces/wt/CU-C2` | Worktree on `research/catchup-2026-11-27-plugins` (at `f8a124edc` when measured), prepped: dependencies, PDFium, a wasm32 payload without the Rust core, the linux-arm64 addon, all package builds. Target `/workspaces/target`. |
| `/workspaces/wt/base` | Worktree detached at `d250a68e3`, prepped the same way, for baseline runs. Its target dir was deleted after the run. |
| `/workspaces/wt/CU-C1-probe` | The probe crate |
| `/workspaces/scripts/` | Every file of `scripts/` here |
| `/workspaces/bin/clang` | The clang shim (`scripts/clang-shim.sh`, CU-C2-D4). Put `/workspaces/bin` first on `PATH` for any prep or runtime build. |
| `/workspaces/logs/CU-C1/`, `/workspaces/logs/CU-C2/` | Logs. `CU-C2/head`, `CU-C2/base` (full suite), `CU-C2/head-ts2`, `CU-C2/base-ts2` (TS rerun) |
| `/workspaces/bundles/` | `catchup-plugins-jobA.bundle` (`d670a1cbf..f8a124edc`), `runtime-catchup-2026-11-27.bundle` (the merge commit) |
| `/workspaces/pdfium/archives/` | linux-arm64 `7f399f838` and `f2ea75972` (upstream releases), wasm32-eh `9472117ed` and `af480d0ce` (fork builds) |
| `/workspaces/pdfium/lib/{linux-arm64,wasm32-eh}`, `/workspaces/pdfium/prev/{linux-arm64,wasm32-eh}` | The new and previous pairs extracted. `PDF_RUNTIME_LIB_DIR=/workspaces/pdfium/lib`. |
| `/workspaces/wasm32-eh-pin.json` | Pin for the local wasm32-eh archive |

The image now carries emsdk 3.1.72 (`/opt/emsdk`), emsdk 4.0.0 with a built sysroot
(`/opt/emsdk-eh`), `wasm-bindgen` 0.2.130, JDK 21.0.13.1, .NET SDK 11.0.100, cargo 1.100.0, node
26.13.1 and pnpm 10.34.0, so no tool goes into the volume (CU-C1-D4). Playwright's browsers
(`chromium-1243`, `firefox-1543`, `webkit-2359`) are in `/home/node/.cache/ms-playwright` inside
the container, not the volume: recreating the container loses them, and `scripts/prep-extra.sh`
puts them back. Volume size after cleanup: 15 GB. Host free space: 708 GiB at the start, 671 GiB at
the end.

## Start state

All in the container, `scripts/suite.sh <checkout> <out> [parts]`, head `f8a124edc`, baseline
`d250a68e3`, each on the PDFium its own pin names (`7f399f838` and `f2ea75972`). The TS row is a
second run (`head-ts2`, `base-ts2`) after `scripts/prep-extra.sh` built the linux-arm64 addon and
installed Playwright's browsers. The first TS run lacked both, and `@embedpdf/engine` failed on a
missing `pdf-runtime.node` while the viewer packages fell back to a Google Chrome the arm64 image
cannot have.

| Check | Baseline `d250a68e3` | Head `f8a124edc` |
|---|---|---|
| `cargo build --workspace --locked` | (not run separately) | exit 0, one warning: unused dependency `uniffi` in `epdf-uniffi-bindgen-cs` |
| `cargo test --workspace --no-fail-fast` | 3,567 passed, 0 failed, 2 ignored | 3,567 passed, 0 failed, 2 ignored |
| engine `:check` scripts | 36 of 36 | 30 of 36 |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | 3 of 4: `coverage:check` fails | 0 of 4 |
| TS `turbo run test` over `packages/**` | 97 of 98 tasks; 8,767 passed, 2 failed, 23 skipped | 97 of 98 tasks; 9,225 passed, 2 failed, 23 skipped |

### Failures by crate

None. No crate has a failing test at either commit, so there is no list to break down.

### Engine `:check` failures at head

| Script | Output |
|---|---|
| `forms:check` | `DIFFERS forms/snapshot.json` |
| `form-write:check` | `STALE forms/write.json` |
| `form-field:check` | `STALE forms/field.json` |
| `form-apply:check` | `DIFFERS forms/apply.json` |
| `changes:check` | `DIFFERS changes/form-copy.json`, `DIFFERS changes/kinds.json` |
| `wire:check` | `DIFFERS wire/requests.json` |

All six are the forms work upstream brought (#1138, #1140, #1141, #1147, #1149) and the wire kinds
that came with it. Regeneration is CU-C3's.

### Plugin check failures at head

`vectors:check` and `test:harness` stop at `ERR_MODULE_NOT_FOUND` for
`packages/plugin/signature/src/blank-page`, imported by
`tooling/plugin-conformance/src/suites/signature-plugin.ts`: upstream deleted `blank-page.ts` in this range.
`typecheck` reports 48 errors, 39 in `src/suites/form-core.ts`, 8 in
`src/suites/form-current-fixtures.ts` (the form field `purpose` is now `string | null | undefined`
where the fixture type says `string | null`) and the `blank-page` import. `coverage:check` stops
with "the generators failed under coverage; fix vectors:check first".

The plugin checks also regenerate `crates/plugins/acrojs/prelude.js` from the merged form scripting
TS, and the result differs from the committed file by 32 insertions and 3 deletions
(`parseDateLoose` becomes `parseDateFrom(raw, format, today)` among them). The working tree was
restored and the diff kept as `/workspaces/logs/CU-C2/head/prelude-regenerated.diff`. The cargo run
had finished before the file changed.

### Failures that are the environment, at both commits

`@embedpdf/core-signature` fails 2 tests at both commits, `answers another program made
(certomancer) > OpenSSL reads the same answers the same way` and `OpenSSL as the reader > the test
authority's answers verify, and say what the checker reads in them`. OpenSSL 3.5.9 reports "error
in thisupdate field" and "error in nextupdate field" from `OCSP_check_validity`: the fixtures'
answers were valid until `2026-11-25T00:00:00Z`
(`packages/core/signature/test/revocation/interop.test.ts:82`), and today is 2026-11-27. The test
file is upstream's unchanged. It is a date bomb, not a regression.

The baseline's `coverage:check` fails in `c8`: `yargs@17.7.2` throws "require is not defined in ES
module scope" under node 26.13.1, the image's node. The 2026-11-18 container ran node 24.20.0 and
passed. At head the same check fails earlier, on `vectors:check`, so this one is hidden there.

## Evidence

| Check | Command | Exit | Log (in `/workspaces/logs/CU-C2/`) | sha256 (first 8) |
|---|---|---|---|---|
| Image build | `heavy-build.sh CU-C0-image -- bash .devcontainer/build.sh` (host) | 0 | host scratchpad `CU-C0-image-build.log` | `df888b7a` |
| Pin check | `python3 .devcontainer/check-pins.py` | 0 | `check-pins.log` | `41498074` |
| Lockfile | `pnpm install --no-frozen-lockfile` | 0 | `pnpm-install.log` | `7aaeb144` |
| Workspace build | `cargo build --workspace --locked` | 0 | `cargo-build.log` | `7ec9e2f1` |
| cargo, head and base | `suite.sh … cargo` | 0, 0 | `head/cargo.log`, `base/cargo.log`, `*/cargo.summary` | `f2d11a37`, `836b84d4`, `42410b50` (both) |
| engine `:check` | `suite.sh … engconf` | | `head/engconf.all`, `base/engconf.all` | `8abc02a5`, `27a10bbd` |
| plugin checks | `suite.sh … plugconf` | | `head/plugconf.all`, `base/plugconf.all` | `fe881a73`, `10747b01` |
| TS tests | `suite.sh … ts` after `prep-extra.sh` | 1, 1 | `head-ts2/ts.log`, `base-ts2/ts.log` | `33a98cda`, `a594502e` |
| Regenerated prelude | `git diff` after plugconf | | `head/prelude-regenerated.diff` | `ec6e117c` |

The baseline's `engconf.all` hashes `27a10bbd…`, the same as 2026-11-18 CU-C7's head run.

## Scripts

`scripts/` holds `runtime-verify.sh`, `lib-exports.sh`, `build-wasm32-eh.sh`, `pdfium-sys-check.sh`,
`pdfium-sys-wasm32-eh-check.sh`, `cu-c1-probe/` and `wasm32-eh-pin.json` (CU-C1), and `env.sh`,
`prep.sh`, `prep-extra.sh`, `suite.sh`, `cargo-by-crate.py` and `clang-shim.sh` (CU-C2). Each is
2026-11-18's (or 2026-11-12's for the shim) with this catch-up's commits, paths and tools.

## Cleanup

Removed: `/workspaces/target-probe`, `/workspaces/target-base`, the 474 MB viewer bundle in the
volume and on the host, and the rename patch. Kept: container `embed-pdf-catchup1127`, volume
`embedpdf-catchup1127`, the worktrees and `/workspaces/target` (9.5 GB) for the jobs that follow.
On the host: branch `upstream/9af4d9ee7-tmp` (a pointer to an upstream commit, for CU-C11 to
delete), the runtime submodule checkout in this worktree, and the scratch runtime worktree
registered in `/Users/andrew/repos/embed-pdf-runtime`.
