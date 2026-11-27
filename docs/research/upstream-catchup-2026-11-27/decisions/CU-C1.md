# CU-C1 decisions

Decisions made while merging upstream runtime `7f399f838` into the runtime fork and preparing the
PDFium libraries, as part of Job A (CU-C0, CU-C1, CU-C2) on `research/catchup-2026-11-27-plugins`.

## CU-C1-D1. Make the runtime merge in a scratch worktree of the runtime clone

Decision: `catchup/2026-11-27-runtime` was created with `git worktree add` in the session
scratchpad, from `af480d0ce`, and `git merge --no-ff 7f399f838` ran there. The checkout of
`/Users/andrew/repos/embed-pdf-runtime` itself (on `embedpdf/main`) was not switched.
Reason: the clone's own checkout belongs to whoever uses it next; a worktree leaves it alone.
Cost: a registered worktree in `/Users/andrew/repos/embed-pdf-runtime` until it is removed.
Reverse: `git worktree remove` it; the branch stays.

## CU-C1-D2. No fork ledger or sync note for this merge

Decision: the merge commit `9472117ed` adds no `docs/embedpdf/syncs/` note and leaves
`docs/embedpdf/fork-ledger.md` as it is, as for `af480d0ce` (2026-11-18 CU-C1-D6).
Reason: the merge had no conflicts and the fork delta is the same 27 files before and after it.
Cost: the fork's own docs do not mention this merge; the commit message and the report do.
Reverse: add a note in a later commit on `catchup/2026-11-27-runtime`.

## CU-C1-D3. linux-arm64 PDFium from upstream's release, not a fork build

Decision: use `libembedpdf-runtime-linux-arm64-7f399f838.tar.gz` from upstream release
`runtime-7f399f838df31a96393fdac0f8faa0ef30468f68` (sha256
`e79015fcc727bf8233bb85cc020f5bf5186596368f79ae73db3e269dd6bd5bb6`, the value upstream viewer
`9af4d9ee7` pins in `packages/engine/runtime/engine-runtime-build.json`).
Reason: the release exists, and none of the fork's 27 delta files changes a linux-arm64 library
(2026-11-18 CU-C1-D3).
Cost: the linux-arm64 library is built from `7f399f838`, not from the merge.
Reverse: build linux-arm64 from `9472117ed` with the fork's `build-target.sh`.

## CU-C1-D4. The image's emsdk 4.0.0 replaces `/workspaces/emsdk4`

Decision: `lib-exports.sh` and `pdfium-sys-wasm32-eh-check.sh` use `/opt/emsdk-eh`, the emsdk 4.0.0
with a built sysroot that `.devcontainer/Dockerfile` now installs (`EMSDK_EH_VERSION=4.0.0`). No
emsdk is installed into the volume.
Reason: plugins added it to the image after 2026-11-18; a second copy is 1 GB of duplicate.
Cost: none while the Dockerfile pin stays 4.0.0, the runtime fork's wasm32-eh pin.
Reverse: install emsdk into `/workspaces/emsdk4` and point the scripts back.

## CU-C1-D5. wasm32-eh build from a merge-only bundle, cloning the MFB fork

Decision: `scripts/build-wasm32-eh.sh` is 2026-11-18's with three values changed: branch
`catchup/2026-11-27-runtime`, upstream tag `runtime-7f399f838df31a96393fdac0f8faa0ef30468f68`, and
the fork clone URL `https://github.com/MFB-Technologies-Inc/embed-pdf-runtime.git` (the fork moved
to the MFB organization on 2026-11-19). The bundle holds only `9472117ed`.
Reason: both parents are on GitHub (`af480d0ce` on the fork's `research/multi-platform-rust-core`,
`7f399f838` behind upstream's release tag).
Cost: the build needs network access to both repositories.
Reverse: bundle the whole range and drop the upstream fetch.
