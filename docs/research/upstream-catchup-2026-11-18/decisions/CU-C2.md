# CU-C2 decisions

Decisions made while merging upstream `c4e744345` into impl and regenerating the engine vectors on
`research/catchup-2026-11-18-impl`. They're committed with the report, in the commit that adds this
file, unless a different commit is named.

## CU-C2-D1. Don't merge the CU-C1 record branch into impl

Decision: the job branch starts at impl `a7c75f5ad` and has no merge of
`research/catchup-2026-11-18-c1`.
Reason: this time CU-C1's branch sits on plugins `cf7fdbe7a`, not on impl. Merging it would bring
all of plugins into impl. The brief names `a7c75f5ad` as the start and doesn't ask for that merge.
CU-C3 merges both branches into plugins.
Cost: CU-C1's report and scripts aren't on impl until impl is fast-forwarded past plugins.
Reverse: not applicable.

## CU-C2-D2. Set the `runtime-src` gitlink directly

Decision: the conflict was resolved with `git update-index --cacheinfo
160000,af480d0ce420ed916db0de8d2e8827822ad7be1e,packages/engine/runtime/runtime-src`. The submodule
was not initialised or fetched.
Reason: the submodule isn't checked out in the worktree (`git submodule status` prints `-`), and
nothing in prep, the generators or the cargo workspace reads its source. The commit was checked in
`/workspaces/runtime` first: `catchup/2026-11-18-runtime` resolves to it, its parents are
`17d8073d4` and `f2ea75972`, and both are ancestors. `.gitmodules` is unchanged from impl.
Cost: no checkout of the runtime source proves the pin builds from this worktree. CU-C1 built
`wasm32-eh` from that commit.
Reverse: `git -C packages/engine/runtime/runtime-src fetch /workspaces/runtime
catchup/2026-11-18-runtime` after `git submodule init`, then `git add` the path.

## CU-C2-D3. Take upstream's `pnpm-lock.yaml` and let pnpm re-resolve, which moves Playwright

Decision: `git checkout --theirs pnpm-lock.yaml`, then `pnpm install --no-frozen-lockfile`. The
merge commit carries the result.
Reason: the brief. After the install, the lockfile differs from upstream's only in the fork's four
tooling importers (`tooling/engine-baseline`, `tooling/engine-conformance`, `tooling/viewer-e2e`,
`tooling/wasm-eh-check`) and the Playwright packages they pull in. `pnpm install --frozen-lockfile`
then passes.
Cost: `@playwright/test` and `playwright` re-resolved from `^1.55.0` to 1.65.1, where impl had 1.63.0,
and `playwright-core` 1.65.1 is added next to upstream's 1.63.0. npm lists 1.65.1 as published
2026-11-13, five days before this merge. Only `tooling/viewer-e2e` and `tooling/wasm-eh-check` use
it; neither is in the `packages/**` TS suite. `prep-extra.sh` installed the matching browsers.
Reverse: pin `@playwright/test` to `1.63.0` in those two `package.json` files and reinstall, or
carry impl's Playwright entries into the lockfile.

## CU-C2-D4. Prepare impl with a copy of `prep.sh` that doesn't name the rust-viewer package

Decision: prep ran with `scripts/cu-c2/CU-C2-prep.sh`, the shared `/workspaces/prep.sh` with
the filter that excludes the rust-viewer package removed.
Reason: impl still has no package by that name (2026-11-12 CU-C2-D2).
Cost: impl's prep differs from the shared script by that one filter.
Reverse: none needed.

## CU-C2-D5. Build the merge in a second worktree while the baseline ran

Decision: the merge commit `66d6f778e` and the vector commit `7a23f52aa` were made in
`/workspaces/wt/CU-C2-merge`, detached at `a7c75f5ad`, while the baseline suite ran in
`/workspaces/wt/CU-C2`. The job branch was then fast-forwarded to `7a23f52aa`. Prep, the generator
typecheck and both regeneration runs used the second worktree, at `66d6f778e`. It was removed after.
Reason: the baseline holds the job worktree for its whole run, and the merge doesn't depend on it.
Cost: none in the result; both worktrees hold the same commits.
Reverse: not applicable.

## CU-C2-D6. Refresh the PDFium libraries and rebuild cargo from clean after the merge

Decision: before the after-merge prep, `packages/engine/runtime/npm/wasm32/lib` and every
directory under `packages/engine/runtime/build/libpdfium` were deleted in the job worktree, so prep
fetched `linux-arm64` and `wasm32` at `f2ea75972` and `wasm32-eh` at `af480d0ce`
(`CU-C2-libs-after.txt`). The cargo numbers after the merge come from a clean build in the shared
`/workspaces/target`, not from the suite's incremental build in the worktree's `target/`.
Reason: `fetch-payload.sh` leaves a local `wasm32` build in place (2026-11-12 CU-C2-D3). The first
after-merge cargo run finished in 0.07 s with no crate recompiled: `pdfium-sys` watches its include
directory, and the archive's file times are older than the previous build, so its bindings were
still from the `aa59ce554` headers. The clean run compiled 122 crates, `pdfium-sys` among them.
The disk-hygiene rule that arrived mid-job also asks for the shared target directory.
Cost: one extra 38 s build. The incremental run's numbers are kept as evidence; they match.
Reverse: not applicable.

## CU-C2-D7. Commit the regenerated vectors: `policyVersion` 4 to 5

Decision: `7a23f52aa` commits three regenerated files. In each, the only change is
`request.authority.protection.policyVersion` from 4 to 5: 19 cases in `forms/field.json`, 18 in
`forms/write.json`, 24 in `pages/mutate.json`.
Reason: upstream `462380c7a` (PR #1088) raised `SIGNATURE_POLICY_VERSION` in
`packages/engine/core/src/signature/protection.ts` from 4 to 5. The generators record the authority
each write case sends, so the value follows. No Rust test reads the field: the cargo failure list
and every failure's message are the same before and after.
Cost: none.
Reverse: revert `7a23f52aa`.

## CU-C2-D8. Leave `wire:check` and `operations:check` failing on impl

Decision: no change to the wire generator or `operations.ts` on impl.
Reason: both failed at the baseline for the 2026-11-12 reasons (CU-C2-D5 there). Upstream adds two
request kinds, `signatures.addValidationInfo` and `signatures.validationInfo`, so `wire` now says
"expected 89 request kinds, the union has 96" (94 before), and `operations` lists 7 kinds with no
row: the same five font kinds as before (`document.fonts.list`, `document.fonts.program`,
`document.fonts.resolve`, `fonts.list`, `fonts.program`) plus the two signature kinds.
Cost: the two signature rows are new work for the plugins port, on top of the font rows plugins
already carries.
Reverse: add the rows on plugins (CU-C3 onward).

## CU-C2-D9. Record the baseline viewer failures without a rerun

Decision: the baseline TS run's two `@embedpdf/viewer` failures stay as recorded:
`test/fullscreen.test.ts > full screen > keeps a dialog open in the viewer over it when the page
makes it full screen` and `test/small-screens.test.ts > small screens > slides in with its layer
still and the whole viewer dimmed behind it` (583 of 585). They weren't rerun at `a7c75f5ad`.
Reason: the after-merge run passes all 98 tasks, `@embedpdf/viewer` 595 of 595, so the merge adds
no TS failure. Both are browser layout tests at `--concurrency=6`, the load pattern of 2026-11-12
CU-C2-D7.
Cost: the baseline TS number reads 97 of 98 tasks, and whether those two tests are flaky at impl
is not proven.
Reverse: rerun `@embedpdf/viewer` alone at `a7c75f5ad`.
