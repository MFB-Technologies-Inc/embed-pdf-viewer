# CU-C2 decisions

Decisions made while merging upstream viewer `9af4d9ee7` into `research/catchup-2026-11-27-plugins`
and measuring the start state (Job A).

## CU-C2-D1. `PageGeometryReader.ts` is upstream's, and `peekWords` goes (D-2)

Decision: the merge takes upstream `9af4d9ee7`'s
`packages/engine/services/src/features/geometry/PageGeometryReader.ts` unchanged, so `061a544c4`'s
per-glyph `peekWords` view is not re-applied. Its runtime half, the optional
`PdfRuntimeMemory.peekWords` in `packages/engine/runtime/src/core/pdf-runtime-module.ts` and its
wasm implementation in `packages/engine/runtime/src/wasm/wasm-runtime.ts`, is removed in the merge
commit.
Reason: upstream's reader (#1157) no longer reads the glyph struct field by field through the
runtime. It calls `EPDFText_GetPageGeometry` for up to 4,096 glyphs at a time
(`GLYPHS_PER_CALL`), copies the buffer once with `mem.readBytes`, and decodes it with a `DataView`
in `appendGlyphRecords`. That is the one-view read `061a544c4` added, done better. After taking it,
`git grep peekWords -- packages crates platforms` finds only the two definitions, so D-2's
condition for keeping the runtime half (a remaining caller) fails.
Cost: no perf-harness run in this job compares upstream's reader with the old `peekWords` one. Two
TS divergences from upstream are gone (`PageGeometryReader.ts`, `PdfRuntimeMemory.peekWords`).
Reverse: cherry-pick `061a544c4`'s runtime half; the reader half no longer applies.

## CU-C2-D2. `sync-upstream.sh` moved a branch in the worktree's own submodule clone

Decision: accepted. `scripts/sync-upstream.sh --from upstream/9af4d9ee7-tmp --runtime-commit
9472117ed…` checks out `research/multi-platform-rust-core` in the submodule clone at
`.git/worktrees/catchup-1127/modules/packages/engine/runtime/runtime-src` and fast-forwards it from
`af480d0ce` to `9472117ed`. That clone was made for this worktree by `git submodule update --init
--reference /Users/andrew/repos/embed-pdf-runtime`. The runtime fork clone's
`research/multi-platform-rust-core` (`/Users/andrew/repos/embed-pdf-runtime`) and the fork on
GitHub did not move, and the script ran without `--push`.
Reason: the brief asks for the script; this is how it records the runtime merge.
Cost: a local branch with a canonical name points at an unpushed commit, in a clone only this
worktree uses.
Reverse: `git -C packages/engine/runtime/runtime-src branch -f research/multi-platform-rust-core
af480d0ce` (after checking out something else), or remove the worktree.

## CU-C2-D3. `pnpm-lock.yaml` is upstream's, then re-resolved

Decision: the merge commit carries upstream `9af4d9ee7`'s lockfile, though git merged it cleanly;
`pnpm install --no-frozen-lockfile` in the container re-resolves the fork's importers in a
follow-up commit.
Reason: the brief and the 2026-11-18 precedent (CU-C2-D3 there). A clean textual merge of two
lockfiles is not a resolution pnpm produced.
Cost: the merge commit alone does not install with `--frozen-lockfile`.
Reverse: take git's merged lockfile instead.

## CU-C2-D4. The clang shim is back on `PATH` for every prep

Decision: `scripts/clang-shim.sh` (2026-11-12's, unchanged) is installed as `/workspaces/bin/clang`
and `scripts/prep.sh` puts `/workspaces/bin` first on `PATH`. The first prep without it failed at
`packages/engine/runtime/build/verify-wasm-abi.mjs`: "2 of 1070 bound functions don't match their
bindings", `EPDFAttachment_ExtractFile` and `EPDFAttachment_ExtractFileToOwnedBuffer`, "the runtime
passes 5 arguments, the export takes 6". The image's `/usr/bin/clang` parses the headers for
`aarch64-linux-gnu`, where `uint64_t` is `unsigned long`, and `generate-functions.mjs` then types it
as `i32`. Both checkouts were re-prepped after restoring `pdf-functions.generated.ts` and deleting
`npm/wasm32/lib`, because `fetch-payload.sh` keeps a "locally built payload" it finds.
Reason: the generator's result depends on the host's data model; the shim makes Linux match macOS,
where the committed `pdf-functions.generated.ts` comes from. With it the head payload reports "all
1070 bound functions match their bindings" and the baseline's "all 1065", and neither checkout's
generated file changes.
Cost: one more container-only workaround that later jobs must carry. The generator bug itself is
not fixed.
Reverse: classify `uint64_t` and `int64_t` by name in `generate-functions.mjs` before resolving
typedefs, then drop the shim.

## CU-C2-D5. The d250a68e3 baseline is measured, not taken from 2026-11-18

Decision: the baseline columns come from a fresh run at `d250a68e3` in `/workspaces/wt/base`, with
the same scripts as the head run, each checkout on the PDFium its own
`engine-runtime-build.json` pins. The 2026-11-18 CU-C7 numbers (`5f4abdabc`: 3,514
passed, 0 failed, 2 ignored) are not reused.
Reason: `d250a68e3` is 674 commits past `94b2e7be0` (the .NET, Android and Kotlin parity merges among
them), so CU-C7 does not describe it.
Cost: about one more suite run of container time.
Reverse: none needed; the CU-C7 numbers stay in their report.

## CU-C2-D6. `cargo fmt` reflow of `crates/pdfium/tests/documents.rs` reverted

Decision: the rename commit carries only the formatting of the two files it edits.
`cargo fmt -p pdfium -p epdf-engine` also reflowed three statements in
`crates/pdfium/tests/documents.rs`, a file this job does not touch; that change was reverted.
Reason: common.md, "cargo fmt -p <crate> only; revert unrelated reorders".
Cost: `cargo fmt --check -p pdfium` still reports that file at this tip, as it did at `d250a68e3`.
Reverse: format it in its own commit.
