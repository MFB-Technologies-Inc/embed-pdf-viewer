# Upstream catch-up, 2026-11-27b (plan)

This plan brings `research/multi-platform-rust-core-impl-plugins` ("plugins") up to upstream
`main` at `cda9e82a4984cd7edb1a69a8fe32b1d7e11693b8`. It follows the morning's catch-up
(`multiplatform-upstream-catchup-2026-11-27.md`, records in `docs/research/upstream-catchup-2026-11-27/`)
job for job, with two changes. The runtime pin did not move, so there is no runtime job. And the
range is four PRs, all in forms and signatures, so the Rust port is two jobs on one chain instead
of three in parallel.

The suffix `b` keeps every name apart from the 2026-11-27 run, whose release train
(run `46064004730`) and cleanup are still in progress in another session.

Upstream stays canonical: the Rust port, the conformance tooling and `platforms/` follow it, and
`packages/**` keeps upstream's behaviour except for the approved exceptions in the
`plugins-ts-exceptions` record.

## Pinned targets

| What | Commit |
|---|---|
| Upstream viewer `embedpdf/embed-pdf-viewer` `main` | `cda9e82a4` (PR #1165, 2026-11-27) |
| Upstream runtime `embedpdf/runtime`, pinned by that `main` | `7f399f838`, unchanged since `9af4d9ee7` |

The previous target `9af4d9ee7` is the merge base. The range is 14 commits (4 first-parent merges),
198 files, 113 of them under `packages/`.

## Starting points

| Branch | Commit | Runtime pin |
|---|---|---|
| plugins (local and `fork/`) | `0afd87825` | `9472117ed` |
| runtime fork `research/multi-platform-rust-core` (`fork/` in `~/repos/embed-pdf-runtime`) | `9472117ed` | |

`9472117ed` already merges `7f399f838`, so `runtime-src` stays where it is.

Run `46064004730` will push an Apple engine pin commit to plugins when it publishes. CU-C6 merges
plugins' tip at that point into the catch-up branch; the pin touches `Package.swift` only.

## What the probe found

`git merge-tree` of `0afd87825` with `cda9e82a4` (2026-11-27): no conflicts. The root
`package.json`, `pnpm-lock.yaml` and `.devcontainer/` are untouched, so the dev image's hashed
inputs don't change.

## What upstream brought

| PR | What | Rust side |
|---|---|---|
| #1161 unsigned signature appearance | A new `/AP` on an unsigned signature field reads as `permitted` (rule `signature-appearance`) after an approval signature or a form-filling certification; a signed field's stays `forbidden`. `SIGNATURE_POLICY_VERSION` is 6. 11 new Acrobat-observed fixtures in `signature-compat/v4/`. | `signature` analyzer and protection |
| #1162 fills and marks keep signatures | A fill no longer writes who filled the field into `/EMBD_Metadata`, and a mark no longer records its fit on the widget. `filledBy`, `filledByName`, `filledAt` always read `null` and are deprecated, as is `FormValuesImportOptions.attribution`. A values import needs only fill rights. | `forms` write and snapshot, `transfer` |
| #1163 anyone fields | `forAnyone` on a field, stored as `/ForAnyone true` in `/EMBD_Metadata`. Any fill right fills it; marking one takes `fields:set-group:all`. A signing with no lock of its own locks every anyone field that holds a value; a signature field with its own `/Lock` signs with that alone. Undo restores group and `forAnyone` together. `ANYONE_FIELDS_SINCE = '3.5'`. | `forms`, auth scope, `signature` lock, `changes` undo, `transfer` |
| #1165 picture buttons | `forms.setButtonImage(field, image \| null)` puts a PNG, JPEG or one-page PDF in a push button's `/MK /I` (runtime `EPDFAnnot_SetMKIcon`, bound last run). `layout` (`/MK /TP`, seven values) on a widget, `hasImage` on a push button. Change kind `forms.setButtonImage`, `BUTTON_IMAGE_AND_LAYOUT_SINCE = '3.5'`. A picture shown in many fields (signature marks included) is stored once. `addWidget` carries the image; copy, export and import keep it, except `noExport`. The analysis counts an image as filling in. | `forms`, `annotation` widget read and write, `changes`, `transfer`, `signature` analyzer |

New conformance runners `runAnyoneFieldsConformance` (812 lines) and `runPictureButtonConformance`
(724 lines), plus changes to the attribution, prediction, transfer, change and signature-mark
runners, become engine vector cases. `packages/plugin/form` changed tests only (fixtures gain
`forAnyone`), so no plugin crate gets new behaviour and no platform gets new UI. The `cloudpdf/**`
and docs changes have no port.

## Rules for this catch-up

The 2026-11-27 rules carry over unchanged: Node, TypeScript, Rust, Kotlin and .NET in the dev
container; Swift and anything Xcode on the Mac (cargo on the host for Apple targets only); nothing
Node on the Mac; `packages/**` keeps upstream behaviour; nothing merges, fast-forwards or pushes
without the user's explicit approval; the frozen impl branch is never touched; every job cleans up
after itself; all work stops, subagents included, when the 5-hour or 7-day usage limit is hit.

Every subagent runs on Opus, the session's model.

## Decisions (for the user)

D-1. The deprecated attribution fields in the wrappers. We'd follow upstream: the Rust core keeps
`filled_by`, `filled_by_name` and `filled_at` and always returns `None`, and Swift, Kotlin and .NET
keep their properties with a deprecation attribute (`@available(*, deprecated)`, `@Deprecated`,
`[Obsolete]`). Removal waits for upstream's 4.0. Cost: three dead properties per platform and
deprecation warnings in our own tests until then. The alternative, removing them now, is a source
break with no user to absorb it, but it diverges from the TS API.

D-2. `completeFormBundle`. CU-C4-D9 of the morning's run left it unported, so an older bundle's
field rows read no `purpose`. This range adds `forAnyone` and `hasImage` defaults to it and
`layout` defaults to `WidgetDTOSchema`. We'd port it now, with the three new defaults and the
`purpose` one. Cost: about two hours in CU-C4, and CU-C4-D9's recorded divergence closes. The
alternative is serde defaults on the new fields alone, which leaves `purpose` divergent.

D-3. The `signature-compat/v4/` corpus. Nothing in the Rust port runs the v1 to v3 corpora; the TS
reads them in `packages/engine/main/test`. v4 is the evidence for #1161. We'd add a Rust test in
`epdf-engine` that analyzes each v4 fixture and checks it against `policy-expectations.json`.
Cost: about half a day in CU-C4, and the test reads fixtures under `packages/` the way
`signature_sign.rs` already does. The alternative is to rely on the `runSignatureMarkConformance`
vectors only.

D-4. Running beside the 2026-11-27 run. We'd start now on `-b` names (branches, container,
worktrees, records), leave that run's container, worktrees and branches to its own CU-C11, and
merge plugins' post-release tip in CU-C6. Cost: two catch-up containers on the disk for a few hours.
The alternative is to wait for run `46064004730` and its cleanup to finish first.

## Jobs

Node, Rust, Kotlin and .NET jobs run in a fresh dev container `embed-pdf-catchup1127b` (volume
`embedpdf-catchup1127b`) on the latest `embedpdf-dev` image, one worktree per job under
`/workspaces/wt/<job>`, heavy builds through `heavy-build.sh`. Container branches come to the host
as git bundles. Swift runs in a host worktree under `.claude/worktrees/`. Reports go to
`docs/research/upstream-catchup-2026-11-27b/reports/CU-C<n>.md`, decisions to `decisions.md` and
`decisions/` beside them, scripts to `scripts/`, reusing the 2026-11-27 scripts where they fit.

| Branch | Jobs | Where |
|---|---|---|
| `research/catchup-2026-11-27b-plugins` | CU-C2, CU-C3, CU-C4, CU-C5, CU-C6 | container |
| `research/catchup-2026-11-27b-platforms` | CU-C7, from CU-C6's tip | container, Mac where noted |
| `research/catchup-2026-11-27b-swift` | CU-C8, from CU-C7's tip | Mac |

CU-C0, setup (about 30 minutes). Check `embedpdf-dev:latest` was built from inputs that match
`0afd87825` (`python3 .devcontainer/check-pins.py` in the container passes); rebuild with
`.devcontainer/build.sh` only if it fails. Create the container and volume. Confirm the clone has
upstream history back to `9af4d9ee7`.

CU-C2, merge (about 1 hour). On `research/catchup-2026-11-27b-plugins` from `0afd87825`, run
`scripts/sync-upstream.sh --from <local branch at cda9e82a4> --runtime-commit 9472117ed`. Run
`pnpm install`. Done when `cargo build --workspace`, the TS package tests and the engine `:check`
scripts run, failures recorded.

CU-C3, measurement (about 2 hours). Regenerate engine and plugin vectors twice each; both must be
deterministic. Done when the report lists every failing `cargo test` by crate and cause, each new
conformance case without a Rust pass, and the new Rust API surface each wrapper must expose. That
list is the work order for CU-C4 to CU-C8.

CU-C4, signatures and anyone fields (1 to 1.5 days). #1161, #1162 and #1163 in `epdf-engine`:
the `signature-appearance` rule and policy version 6, fills that record nobody, `forAnyone` through
auth scope, signing locks, undo and transfer, `ANYONE_FIELDS_SINCE`, and D-2 and D-3 as ruled. Done
when the anyone-fields, attribution, prediction, transfer and signature-mark vectors pass in Rust.

CU-C5, picture buttons (1 to 1.5 days, after CU-C4 on the same branch). #1165: widget `layout`,
`hasImage`, `forms.setButtonImage` as verb and change op, shared drawings stored once,
`addWidget`, copy, export and import, the analysis rule, `BUTTON_IMAGE_AND_LAYOUT_SINCE`. Done when
the picture-button and change vectors pass in Rust.

CU-C6, Rust and Node integration (half a day). Merge plugins' current tip. Build the Rust-core
`wasm32` payload and run the same rows as the 2026-11-27 CU-C7 (`cargo test --workspace`, engine
`:check`, `vectors:check`, `coverage:check`, `test:harness`, `typecheck`, evidence, `packages/**`
tests, `core-wasm` vitest and `test:gc`, `epdf-web`, the `epdf-node` addon test), against
`0afd87825`. Done when nothing is worse than the start and every new divergence is listed first in
`decisions.md`.

CU-C7, browser, Kotlin and .NET (1 day). Expose `forAnyone`, `hasImage`, widget `layout` and
`setButtonImage` through `epdf-web`, `epdf-uniffi` and `epdf-capi`, and D-1's deprecations. In the
container: `epdf-web` tests, the AAR build and its 16 KB checks, Kotlin `test-command-context.sh`,
`dotnet test`, parity, `pack.sh`, `check-dotnet-engine.sh`, `validate.sh` for `linux-arm64` and
`linux-x64`. On the Mac: the Android emulator smoke test and `validate.sh` for `osx-arm64` and
`osx-x64`; `win-x64` reported as not run. Done when each platform has a test for each new call and
results are no worse than `0afd87825`.

CU-C8, Swift (half a day to 1 day, Mac). Package the framework locally
(`EMBEDPDF_PREBUILT_ENGINE=local`, `EMBEDPDF_NM` set), expose the same surface in
`platforms/apple/EmbedPDF`, run `check-swift-api-coverage.py`, then the Swift tests on the iOS
simulator, macOS arm64 and x86_64. Done when coverage has no new gap and the three runs are no
worse than `0afd87825`.

Approval gate. The ledger's final table is filled in, a whole-branch review runs, and the user is
asked to approve fast-forwarding plugins to `research/catchup-2026-11-27b-swift` and pushing it to
`fork`.

CU-C9, release (on go-ahead only). Notes in `docs/releases/` for `0.0.0-swift.12`,
`rust-viewer-0.0.0-preview.8`, `0.0.0-dotnet.2` and `0.0.0-android.3`; bump
`packages/rust-viewer-bundle/package.json`; re-pin .NET and Android engines until
`check-dotnet-engine.sh` and `check-android-engine.sh` exit 0; push; dispatch `release-train.yml`
with `dry_run=false` and review `summarize`'s publish plan at the `approve` gate. No runtime push
(the pin did not move).

CU-C10, cleanup (about 30 minutes). Remove `embed-pdf-catchup1127b`, its volumes and worktrees,
the host worktrees this run made, the bundles, the `research/catchup-2026-11-27b-*` branches once
their commits are in plugins, and untagged `embedpdf-dev` images no container uses (`docker
inspect` first). Done when nothing named `1127b` is left in `git worktree list`, `git branch` or
`docker ps -a`.

## Estimate

CU-C0 to CU-C3 take half a day, the Rust port (CU-C4 to CU-C6) 2.5 to 3.5 days, the platforms
(CU-C7, CU-C8) 1.5 to 2 days, and CU-C9 and CU-C10 half a day: 5 to 6.5 days in all. The risk is
CU-C4's signing lock, which changes what every signature locks and touches the signature vectors
the morning's run just settled.
