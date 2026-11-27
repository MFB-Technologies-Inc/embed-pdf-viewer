# CU-C2 decisions

Decisions made while setting up (CU-C0), merging upstream viewer `cda9e82a4` into
`research/catchup-2026-11-27b-plugins` and measuring the start state (CU-C2).

## CU-C2-D1. `pnpm-lock.yaml` stays as merged; no re-resolve commit

Decision: the merge commit's lockfile is the lockfile. There is no follow-up `pnpm install
--no-frozen-lockfile` commit, unlike CU-C2-D3 of 2026-11-27.
Reason: `git diff --stat 9af4d9ee7 cda9e82a4 -- pnpm-lock.yaml '**/package.json'` is empty, so
upstream changed no dependency in this range and git had nothing to merge in the lockfile. `pnpm
install --frozen-lockfile` passes at the merge in the container ("Lockfile is up to date, resolution
step is skipped", `/workspaces/logs/CU-C2/prep-head.log`).
Cost: none.
Reverse: run `pnpm install --no-frozen-lockfile` in the container and commit whatever it changes.

## CU-C2-D2. `sync-upstream.sh` left the submodule branch where the fork has it

Decision: accepted as is. The script ran `git checkout -B research/multi-platform-rust-core
9472117ed` in this worktree's own `runtime-src` clone and printed "already has runtime 9472117ed".
Reason: the fork's `research/multi-platform-rust-core` on GitHub is at `9472117ed` today
(`git ls-remote`), so, unlike 2026-11-27 CU-C2-D2, the local branch names a pushed commit and no
runtime merge was made.
Cost: none.
Reverse: not needed.

## CU-C2-D3. The wasm32-eh archive comes from the host's 2026-11-27 build directory

Decision: `/workspaces/pdfium/archives/libembedpdf-runtime-wasm32-eh-9472117ed.tar.gz` in the new
volume is a copy of
`/Users/andrew/repos/embed-pdf-runtime-builds/c1127-9472117ed/libembedpdf-runtime-wasm32-eh-9472117ed.tar.gz`,
and `scripts/wasm32-eh-pin.json` is 2026-11-27's file unchanged.
Reason: no wasm32-eh release for `9472117ed` is published on `MFB-Technologies-Inc/embed-pdf-runtime`
yet (the newest runtime release is `runtime-af480d0ce…`), and the brief keeps this run off the
2026-11-27 container. The copy's sha256 is
`a59c31a29f1c9b2cfa4107693df6e4d53e5f48fda027546b996a699921e5f81f`, the pin's value.
Cost: the archive is a local build, not a published one, as it was for 2026-11-27.
Reverse: point the pin's `url` at the release asset once CU-C10 of 2026-11-27 publishes it.

## CU-C2-D4. The image is reused; the clang shim stays on `PATH`

Decision: `embedpdf-dev:latest` (`a3b58f11b8e0`, built from `d670a1cbf`) is used without a rebuild,
and `scripts/clang-shim.sh` is installed as `/workspaces/bin/clang` as in 2026-11-27 CU-C2-D4.
Reason: `python3 .devcontainer/check-pins.py` exits 0 at both `0afd87825` and the merge, and
`git diff --stat d670a1cbf 0afd87825 -- .devcontainer` is empty. With the shim, both checkouts'
wasm32 payloads report "all 1070 bound functions match their bindings".
Cost: the generator bug behind the shim is still not fixed.
Reverse: as 2026-11-27 CU-C2-D4.

## CU-C2-D5. The `0afd87825` baseline is measured fresh

Decision: the baseline columns come from a run at `0afd87825` in `/workspaces/wt/base` with the
same scripts and container as the head run, not from 2026-11-27's records.
Reason: 2026-11-27 measured `d250a68e3` and its own port tips, not `0afd87825`, and a different
container day changes date-sensitive tests (the `@embedpdf/core-signature` OCSP fixtures in the report).
Cost: about one suite run of container time.
Reverse: none needed.

## CU-C2-D6. FreeType's source is fetched into each `runtime-src` for the `core-fonts` test

Decision: `runtime-src/third_party/freetype/src` in both container checkouts is a shallow clone of
`https://chromium.googlesource.com/chromium/src/third_party/freetype2.git` at
`2b01fa2d6723bf12490822268c9627697ed89643`, the `freetype_revision` that
`packages/engine/runtime/runtime-src/DEPS:145` names. `scripts/freetype-src.sh` makes the clone
and the copies. The head's `@embedpdf/core-fonts` test was rerun after the copy; the baseline's TS
run came after it.
Reason: `packages/core/fonts/test/standard-fonts.test.ts:90` runs "are what the fork font data
gives" whenever `runtime-src/core/fxge/fontdata` exists, and the generator it calls,
`packages/core/fonts/scripts/generate-standard-fonts.mjs:44`, reads
`third_party/freetype/src/src/psnames/pstables.h`, which is a `DEPS` checkout and not part of the
runtime git tree. Without it the head run failed that one test with `ENOENT`. Neither file changed
in `9af4d9ee7..cda9e82a4`, so this is the environment, not the merge.
Cost: one more container-only setup step. The path is in `runtime-src`'s `.gitignore`
(`/third_party/freetype/src`), so the submodule stays clean.
Reverse: delete the two copies; the test then fails with `ENOENT` again.
