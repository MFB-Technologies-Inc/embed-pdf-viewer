# Upstream catch-up, 2026-11-27 (plan)

This plan brings `research/multi-platform-rust-core-impl-plugins` ("plugins") up to upstream
`main` at `9af4d9ee755d6e5c20a8055654e895a81b95fb83`. It follows the 2026-11-18 catch-up
(`multiplatform-upstream-catchup-2026-11-18.md`, on branch `research/catchup-2026-11-18-plan`,
commit `1878ddacc`) job for job, with three changes. Impl is frozen since 2026-11-19 at
`ae3ce9944`, so there is no impl job and upstream merges straight into plugins with
`scripts/sync-upstream.sh`. Releases go through `.github/workflows/release-train.yml`, which builds
and pins the Apple engine itself, so CU-C10's hand pins are gone. And every job ends by removing its
branches, worktrees, containers and volumes.

Upstream stays canonical: the Rust port, the conformance tooling and `platforms/` follow it, and
`packages/**` keeps upstream's behaviour except for the approved exceptions in the
`plugins-ts-exceptions` record.

## Pinned targets

| What | Commit |
|---|---|
| Upstream viewer `embedpdf/embed-pdf-viewer` `main` | `9af4d9ee7` (PR #1104, 2026-11-25) |
| Upstream runtime `embedpdf/runtime`, pinned by that `main` | `7f399f838` (runtime PR #132, 2026-11-25) |

The previous target `c4e744345` is the merge base. The viewer range is 84 commits (26 first-parent
merges), 930 files, 431 of them under `packages/`. The runtime range `f2ea75972..7f399f838` is 62
commits.

## Starting points

| Branch | Commit | Runtime pin |
|---|---|---|
| plugins (local and `fork/`) | `d250a68e3` | `af480d0ce` |
| runtime fork `research/multi-platform-rust-core` (`fork/` in `~/repos/embed-pdf-runtime`) | `af480d0ce` | |

The local clone was shallow for upstream history. `git fetch --deepen=500 origin main` fixed it on
2026-11-27; CU-C0 repeats that check on every clone it uses.

## What the probe found

Dry-run merges (`git merge-tree`, 2026-11-27):

- Runtime fork `af480d0ce` with `7f399f838`: no conflicts.
- plugins `d250a68e3` with `9af4d9ee7`: conflicts in `runtime-src` (expected) and
  `packages/engine/services/src/features/geometry/PageGeometryReader.ts`. `pnpm-lock.yaml`,
  `package.json` and `.github/workflows/release.yml` merge cleanly.

The geometry conflict is our own TS change. `061a544c4` (2026-10-01) made the reader take each
glyph's struct through one `peekWords` view, a 44% cut in `readGlyphRaw`; upstream rewrote the same
reader for text page geometry (#1157). D-2 decides it.

## What upstream brought

Runtime. Three exports are renamed, not just added: `EPDFAnnot_GetStampDrawing`,
`EPDFAnnot_SetStampDrawing` and `EPDFDoc_GetStampDrawings` become `EPDFAnnot_GetDrawing`,
`EPDFAnnot_SetDrawing` and `EPDFDoc_GetDrawings`. The old names are called from
`crates/pdfium/src/stamp.rs` and `crates/epdf-engine/src/features/annotation/write/stamp.rs`, so
plugins will not link against the new runtime until CU-C2 renames them. New exports:
`EPDFAnnot_GetMKIcon`, `EPDFAnnot_SetMKIcon`, `EPDFAnnot_HasMKIcon`, `EPDFAnnot_ClearMKIcon`
(push-button icons, runtime #129) and `EPDFText_GetPageGeometry` (runtime #126). The rest is a
PDFium sync (#131), widget appearance fixes (#123, #124, #130), content stream splitting (#125), a
raw annotation page leak (#127), the thread lifecycle count (#128) and a crash in registered font
fallback (#132). CU-C1 confirms the export list against the built library, not the headers.

Viewer, grouped by where the port lands:

| Area | Upstream PRs | Rust side |
|---|---|---|
| Forms | #1138 form controls, #1140 field group default, #1141 form signers, #1147 field purpose and format, #1149 radio group tab order, #1136 radio accessible names, #1146 signature field tab, #1135 dropdown focus | `form-core`, `form`, `signature`, engine group `forms` |
| Engine and runtime | #1157 text page geometry, #1145 signature mark image and clear, #1150 layer bytes base SHA-256, #1159 unembedded font fallback, #1151 zod 4 (DTO schemas), #1152 worker fonts | `epdf-engine`, `pdfium-sys`, `pdfium`, `fonts`, `geometry`, `text-core`, engine vectors |
| Annotation | #1118 upright fields | `annotation-core`, `annotation`, `i18n` (new chrome locale keys) |
| Framework UI | #1138, #1144 attach scrollbar, #1135 | no Rust crate; checked against the Swift, Kotlin and .NET UI in CU-C8 and CU-C9 |
| Build only | #1125 runtime build speed, #1143 compact function table, #1155 minify worker | `packages/core-wasm` and the `wasm32` payload build, if they share the script |
| No port | #1104 changeset release, #1123, #1126, #1127, #1156 (docs and website), #1153 (`cloudpdf/server`), #1158 (release notes) | none |

Forms are about half the work, as signatures were last time. `plugin/form` gained seven new source
files and the engine core ten.

## Rules for this catch-up

The 2026-11-18 rules carry over, as recorded in `plugins-branch-rules`:

- All Node, TypeScript and Rust work runs in the dev container. Nothing Node or TS runs on the Mac.
- Kotlin and .NET work runs in the dev container wherever it can. The Android emulator smoke test
  and the `osx-*` .NET RIDs run on the Mac, and the job report says which steps ran where and why.
- All Swift work runs on the Mac.
- Every wrapper and platform is caught up, not only the Rust core: each upstream feature in this
  range that the Rust API exposes is reachable from Swift, Kotlin, .NET and the browser build.
- Nothing merges or fast-forwards into plugins, and nothing is pushed, without the user's explicit
  approval.

New for this one:

- The dev container uses an image built from the catch-up branch's own merge commit (CU-C0). The
  current `embedpdf-dev:latest` (`00a0acd5a4b2`) was built from `efb66a7bd`, and plugins has
  changed `.devcontainer/Dockerfile` and `check-pins.py` since. Upstream also changes the root
  `package.json`, which the image hashes. So the image is rebuilt with `.devcontainer/build.sh`
  after the merge, and again if a later job changes any of its inputs.
- Cleanup is part of done, for every job (CU-C11).

## Decisions to make before execution

D-1. The stamp drawing rename. We'd follow upstream: rename the three bindings in `pdfium-sys` and
`pdfium`, keep the Rust API names the engine exposes unless upstream's TS renamed them too
(`packages/engine/core` decides), and add the push-button icon exports in the same pass. Cost: any
wrapper that surfaced "stamp drawing" names follows the TS rename, a source break for Swift,
Kotlin and .NET users of those calls.

D-2. `PageGeometryReader.ts`. We'd take upstream's file and re-apply the `peekWords` view only if
upstream's new reader still peeks the glyph struct field by field, then measure it in the perf
harness as before. If upstream already reads it in one call, drop `061a544c4`'s reader change and
keep its runtime half (`pdf-runtime-module.ts`, `wasm-runtime.ts`) only if something still calls
it. Cost: a measured `readGlyphRaw` regression is possible if we drop it, and keeping it extends a
TS divergence the user has to approve again.

D-3. Releases and pushes. As last time, CU-C0 to CU-C9 stay local. The runtime fork push, the
plugins fast-forward, the push to `fork` and a `release-train.yml` dispatch (dry run first) happen
only on the user's explicit go-ahead. Cost: until then, `runtime-src` names a commit that exists
only in `~/repos/embed-pdf-runtime`.

## Jobs

Node, Rust, Kotlin and .NET jobs run in a fresh dev container `embed-pdf-catchup1127` (volume
`embedpdf-catchup1127`), one worktree per job under `/workspaces/wt/<job>`, heavy builds through
`heavy-build.sh` with its 50 GB guard. Container branches come to the host as git bundles. Swift
jobs run in a host worktree under `.claude/worktrees/`. Reports go to
`docs/research/upstream-catchup-2026-11-27/reports/CU-C<n>.md`, decisions to `decisions.md`
beside them, scripts to `scripts/`, reusing `docs/research/upstream-catchup-2026-11-18/scripts/`
where they still fit.

| Branch | Repository | Jobs | Where |
|---|---|---|---|
| `catchup/2026-11-27-runtime` | `~/repos/embed-pdf-runtime` | CU-C1 | container (Mac for the Apple slices in CU-C9) |
| `research/catchup-2026-11-27-plugins` | viewer | CU-C2, CU-C3, then CU-C7 | container |
| `research/catchup-2026-11-27-c4` .. `-c6` | viewer | port jobs, from CU-C3's tip | container |
| `research/catchup-2026-11-27-platforms` | viewer | CU-C8, from CU-C7's tip | container, Mac where noted |
| `research/catchup-2026-11-27-swift` | viewer | CU-C9, from CU-C8's tip | Mac |

CU-C0, image (about 30 minutes). Make CU-C1's runtime merge and CU-C2's merge commit first (both are
git-only and cheap; builds wait for the image), then run
`.devcontainer/build.sh` on it so the image tag carries that commit. Check
`docker image inspect embedpdf-dev:latest` names it in `org.opencontainers.image.revision`, then
create the container from that tag. Done when `python3 .devcontainer/check-pins.py` passes in the
container.

CU-C1, runtime (about 1 hour, most of it the `wasm32-eh` build). Merge `7f399f838` into
`af480d0ce` on `catchup/2026-11-27-runtime`. Diff the built library's export list against
`af480d0ce`'s with `lib-exports.sh`: expect three renames and five additions, no other removal.
Take linux-arm64 PDFium from upstream's `runtime-7f399f83...` release and build `wasm32-eh` with
`build-wasm32-eh.sh`. Done when the export diff is recorded and both libraries load in
`pdfium-sys` tests.

CU-C2, merge (about 1 hour). On `research/catchup-2026-11-27-plugins` from `d250a68e3`, run
`scripts/sync-upstream.sh --from <local branch at 9af4d9ee7> --runtime-commit <CU-C1 merge>`.
Resolve `PageGeometryReader.ts` per D-2 and the stamp renames per D-1, enough for the workspace to
build. Run `pnpm install` so the fork's extra packages re-resolve. Done when `cargo build
--workspace`, the TS package tests and the engine `:check` scripts run, failures recorded.

CU-C3, measurement (2 to 3 hours). Regenerate the engine and plugin vectors twice each; both must
be deterministic. Re-baseline the evidence archives. Done when the report lists every failing
`cargo test` by crate and cause, the coverage gaps in upstream's new code, the new engine
behaviour that needs engine cases, and the new Rust API surface each wrapper must expose. That list
is the work order for CU-C4 to CU-C9 and may move a PR between jobs.

CU-C4, engine and runtime (1 to 2 days). The D-1 renames and the push-button icon bindings, text
page geometry (#1157), signature mark image and clear (#1145), layer bytes SHA-256 (#1150), font
fallback (#1159, #1152), the DTO changes from zod 4 (#1151), and new engine vector cases for each.
Done when the new cases pass in Rust.

CU-C5, forms (2 to 3 days). #1138, #1140, #1141, #1147, #1149, #1136, #1146 and #1135 in
`form-core`, `form` and `signature`, including form signers and field purpose and format. Done
when the form and signature vectors and the touched plugins' evidence tests pass.

CU-C6, annotation and build (half a day). #1118 upright fields in `annotation-core` and
`annotation`, its chrome locale keys in `i18n`, and #1125, #1143 and #1155 wherever the
`core-wasm` payload build shares their scripts. Done when those crates' vectors pass and the
payload builds.

CU-C7, Rust and Node integration (half a day). Merge c4, c5 and c6 into
`research/catchup-2026-11-27-plugins`, regenerating `coverage.json` on conflict. Build the
Rust-core `wasm32` payload, then run `cargo test --workspace`, engine `:check`, plugin
`vectors:check`, `coverage:check`, `test:harness`, `typecheck`, evidence, `packages/**` tests,
`core-wasm` vitest and `test:gc`, `epdf-web`, and the `epdf-node` addon test. Compare every result
against `d250a68e3`. Done when nothing is worse than the start and every new divergence is listed
first in `decisions.md`.

CU-C8, browser, Kotlin and .NET (1 to 2 days). Expose CU-C3's new API surface through `epdf-web`,
`epdf-uniffi` and `epdf-capi`, and the form controls UI (#1138) where the platform renders forms.
In the container: `epdf-web` tests, the AAR build and its 16 KB checks, Kotlin
`test-command-context.sh`, `dotnet test`, parity, `pack.sh`, `check-dotnet-engine.sh` and
`validate.sh` for `linux-arm64` and `linux-x64`. On the Mac: the Android emulator smoke test and
`validate.sh` for `osx-arm64` and `osx-x64`; `win-x64` is reported as not run. Done when each
platform's tests cover the new form features and the results are no worse than `d250a68e3`.

CU-C9, Swift (1 day, Mac). Build the iOS and macOS fork slices for CU-C1's merge, package the
framework locally (`EMBEDPDF_PREBUILT_ENGINE=local`, `EMBEDPDF_NM` set), expose the new surface in
`platforms/apple/EmbedPDF`, and run `check-swift-api-coverage.py`, then the Swift tests on the iOS
simulator, macOS arm64 and x86_64. Confirm the renamed and new exports in each slice with
`llvm-nm -gUj`. Done when coverage has no new gap and the three runs are no worse than
`d250a68e3`.

Approval gate. The ledger's final table is filled in, a whole-branch review runs, and the user is
asked to approve fast-forwarding plugins to `research/catchup-2026-11-27-swift`.

CU-C10, pushes and release (on go-ahead only). Push `catchup/2026-11-27-runtime` to the runtime
fork's `research/multi-platform-rust-core` and publish `runtime-<sha>`, push plugins to `fork`,
then follow the `release-train-prep` checklist: notes in `docs/releases/`, bump
`packages/rust-viewer-bundle/package.json`, `check-dotnet-engine.sh` exits 0, and dispatch
`release-train.yml` as a dry run before the real one.

CU-C11, cleanup (about 30 minutes, after CU-C10 or after the user declines it). Remove the
`embed-pdf-catchup1127` container and its volumes, every `/workspaces/wt/*` worktree inside it, the
host worktrees under `.claude/worktrees/` this catch-up made, and the bundles. Delete the
`research/catchup-2026-11-27-*` and `catchup/2026-11-27-runtime` branches once their commits are in
plugins and the runtime fork. Keep the shared reference containers and anything another job
mounts (`docker inspect` first). The older leftovers (`research/catchup-2026-11-18-plan`,
`research/catchup-2026-11-c6`, `research/catchup-2026-11-plan`, runtime
`catchup/2026-11-12-runtime` and `catchup/2026-11-18-runtime`, and the untagged dev images) are
listed for the user to approve, not deleted unasked. Done when `git worktree list` shows only the
main checkout and `docker ps -a` shows no catch-up container.

## Estimate

CU-C0 to CU-C3 take about half a day. The Rust port (CU-C4 to CU-C7) takes 3 to 5 days, most of
it forms. The platforms (CU-C8, CU-C9) add 2 to 3 days, and CU-C10 and CU-C11 half a day: 6 to 9
days in all. It has more commits than 2026-11-18 but no new subsystem the size of revocation; the
risk is the form UI work spreading into four platform UIs.
