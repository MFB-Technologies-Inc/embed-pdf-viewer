# Decisions: upstream catch-up 2026-11-27

Every decision that resolved an ambiguity in this catch-up. Manager decisions are `D-n` (the plan's
"Decisions", accepted by the user on 2026-11-27); a job's decisions are `CU-Cn-Dn`, with the full
text (reason, cost, how to reverse) in [`decisions/`](decisions/). Each line here states the
decision and, where it has one, its cost, and links the job's record for the rest.

Targets: upstream viewer `9af4d9ee755d6e5c20a8055654e895a81b95fb83`, upstream runtime `7f399f838`.
Starting points: plugins `d250a68e3`, runtime pin `af480d0ce`; runtime fork merge `9472117ed`.

## NEW DIVERGENCE (none observed by a vector); recorded Rust-only differences

No NEW DIVERGENCE. CU-C7 ran every Rust and Node row at `84a64f0d8` and `2c49e0685`, and
`rust-coverage:check` at `cf65c1cc2` (code = `2c49e0685`) and at `cf65c1cc2` plus the uncommitted
record later committed as `57ad31610` (see the report), against `d250a68e3`, and nothing is worse:
`replay:check` replays all 16,076 cases in 124 files, `rust-coverage:check` passes after the record
is regenerated, and the evidence tests fail the same 2 as at the start
([`reports/CU-C7.md`](reports/CU-C7.md)). The port jobs recorded these Rust-only differences, none
observed by a vector:

- CU-C4-D7. An infinite number: on the TypeScript worker an in-process `Infinity` is an
  `InvalidArg` from zod; on the Rust wire the nearest thing, `1e999`, is a `WireFormat` for the
  whole request. JSON cannot carry `Infinity`, so no wire caller sees the TS answer.
- CU-C4-D9. `completeFormBundle` is not ported: a parsed older bundle's field rows carry no
  `purpose` member in Rust. No wrapper shows those rows today.
- CU-C6-D7. `PageBoxes` is `IndexMap<String, Rect>` keyed by the page number as JavaScript prints
  it, where upstream's type is `Record<number, Rect>`. Once the message is JSON the keys are
  strings on both sides, so a caller sees the same keys; the difference is in the type only.

Platform work, not divergences of the Rust core: CU-C5-D4 (FFI Tab skip for an unsignable
signature field, frame and turn) and CU-C6-D5 (FFI `preview_placement` display rotation, the
record accent) are done in CU-C8 (`e3c57618e`).

Failures at both `d250a68e3` and the head, upstream-owned, so not divergences: the 2
`current-suites.test.ts` evidence failures (the kept M27 NaN divergence, 2026-11-18 CU-C7), and the
2 `@embedpdf/core-signature` OCSP fixtures whose answers expired `2026-11-25T00:00:00Z`
(`packages/core/signature/test/revocation/interop.test.ts:82`, CU-C2 report).

The divergences accepted in earlier catch-ups stand as before
(`docs/research/upstream-catchup-2026-11-18/decisions.md`,
`docs/research/upstream-catchup-2026-11-12/decisions.md`,
`docs/research/rust-plugins-branch-review.md`).

## Scope, authority and branches

- D-1. The stamp drawing rename follows upstream in `pdfium-sys` and `pdfium`, with the push-button
  icon exports in the same pass. Cost: a source break for Swift, Kotlin and .NET users of wrapper
  calls that surfaced "stamp drawing" names.
- D-2. `PageGeometryReader.ts` is upstream's; `peekWords` is re-applied only if upstream still
  peeks the glyph struct field by field. Cost: a possible measured `readGlyphRaw` regression.
- D-3. Releases and pushes wait for the user's explicit go-ahead (CU-C10). Cost: until then
  `runtime-src` names a commit that exists only in `~/repos/embed-pdf-runtime`.
- CU-C3-D9. The work order is routed by crate, which moves three PR halves; CU-C4 owns
  `pdfium-sys`, `pdfium`, `epdf-engine`, `epdf-viewer-host`, `epdf-fonts`. Cost: CU-C4 grows by
  most of #1147.
- CU-C7-D1. The port jobs ran on one chain, so `-plugins` fast-forwarded `0e83ecb84..643d2b429`.
  Cost: none.

## CU-C1: runtime merge and PDFium builds ([record](decisions/CU-C1.md))

- CU-C1-D1. The runtime merge is made in a scratch worktree of the runtime clone. Cost: a
  registered worktree in `/Users/andrew/repos/embed-pdf-runtime` until it is removed.
- CU-C1-D2. No fork ledger or sync note for this merge. Cost: the fork's docs do not mention it.
- CU-C1-D3. linux-arm64 PDFium from upstream's release, not a fork build. Cost: that library is
  built from `7f399f838`, not the merge.
- CU-C1-D4. The image's emsdk 4.0.0 (`/opt/emsdk-eh`) replaces `/workspaces/emsdk4`. Cost: none
  while the Dockerfile pin stays 4.0.0.
- CU-C1-D5. The wasm32-eh build is from a merge-only bundle, cloning the MFB fork. Cost: it needs
  network access to both repositories.

## CU-C2: merge, image, container, start state ([record](decisions/CU-C2.md))

- CU-C2-D1. `PageGeometryReader.ts` is upstream's and `peekWords` goes (D-2). Cost: no perf run
  compares the two readers.
- CU-C2-D2. `sync-upstream.sh` moved a branch in the worktree's own submodule clone. Cost: a local
  branch with a canonical name points at an unpushed commit, in that clone only.
- CU-C2-D3. `pnpm-lock.yaml` is upstream's, then re-resolved. Cost: the merge commit alone does not
  install with `--frozen-lockfile`.
- CU-C2-D4. The clang shim is back on `PATH` for every prep. Cost: a container-only workaround
  later jobs carry.
- CU-C2-D5. The `d250a68e3` baseline is measured, not taken from 2026-11-18. Cost: one more suite
  run.
- CU-C2-D6. A `cargo fmt` reflow of `crates/pdfium/tests/documents.rs` is reverted. Cost: `cargo
  fmt --check -p pdfium` still reports that file, as at `d250a68e3`.

## CU-C3: generators, vectors, work order ([record](decisions/CU-C3.md))

- CU-C3-D1. Work in the branch's own container worktree (`/workspaces/wt/CU-C2`). Cost: the
  worktree's name says CU-C2.
- CU-C3-D2. The blank-page function cases go, as upstream's `blank-page.ts` did. Cost:
  `blank_page_pdf` loses its oracle (CU-C5 removed it).
- CU-C3-D3. Form-core cases follow upstream's new signatures. Cost: four session-group cases leave
  the vectors, with the behaviour upstream removed.
- CU-C3-D4. A fixture widget's box is its rect. Cost: every vector building a widget with a `rect`
  and no `box` has a different `box` input.
- CU-C3-D5. The regenerated `prelude.js` is upstream's output and is committed. Cost: any Rust test
  pinning the prelude's bytes moves.
- CU-C3-D6. `c8` 12 in the plugin conformance tooling. Cost: transitive lockfile moves
  (`test-exclude`, `glob`, `semver`).
- CU-C3-D7. The wire vector's flipped union order is kept. Cost: a meaningless one-line diff if
  upstream flips it back.
- CU-C3-D8. The evidence archives are re-baselined by re-pin, re-capture and re-stamp. Cost: the
  archives move another step from the bytes they were cut from.
- CU-C3-D9. See "Scope" above.
- CU-C3-D10. Coverage gaps are listed, no cases added. Cost: `coverage:check` passes against a
  record of the gaps.
- CU-C3-D11. The TS package tests are not rerun. Cost: closed by CU-C7, which ran them with
  `--force` (CU-C7-D5).
- CU-C3-D12. `rust-coverage:check` is not run. Cost: closed by CU-C7-D8, which regenerated the
  record.

## CU-C4: engine half ([record](decisions/CU-C4.md))

- CU-C4-D1. Work in the branch's own container worktree. Cost: the worktree's name says CU-C2.
- CU-C4-D2. The D-1 rename reaches the `pdfium` crate's safe wrappers. Cost: a source break for
  Rust callers of the three old names.
- CU-C4-D3. The new engine cases are change-generator groups. Cost: `field-purpose-format.json` is
  1.0 MB; steps the TS client refuses before sending record no exchange.
- CU-C4-D4. A verb's `event` is the host's and left out of the comparison. Cost: these vectors do
  not compare it.
- CU-C4-D5. Pixels are checked by hand-written tests over the vector's fixtures. Cost: the
  expectations are restated, not recorded.
- CU-C4-D6. #1159's case runs on the fallback fonts upstream ships. Cost: no red run; the evidence
  is the probe.
- CU-C4-D7. #1152, #1150 and #1151's engine half need no engine change. Cost: the `Infinity`
  difference listed above.
- CU-C4-D8. The host's mark wire is upstream's, and the payload-only shape is refused. Cost: a
  caller sending only payload 0 is refused.
- CU-C4-D9. Imports are a hand-written test; `completeFormBundle` is not ported. Cost: listed above.
- CU-C4-D10. A red run builds in a worktree that shares the target dir. Cost: the next build
  rebuilds the workspace crates.
- CU-C4-D11. The plugin cross-check's fixtures gain the two new members. Cost: an edit in CU-C5's
  crate.

## CU-C5: form and signature plugins ([record](decisions/CU-C5.md))

- CU-C5-D1. The branch starts at CU-C4's tip, in a worktree and target dir of its own. Cost: 20 GB
  on the volume until CU-C11.
- CU-C5-D2. The script-right import gets an assertion that tells it apart. Cost: one assertion
  that restates the Rust answer.
- CU-C5-D3. Minor A has no red run. Cost: the op leg has no failing run behind it.
- CU-C5-D4. The FFI focus target keeps its one call, and FFI bounds stay the standing rect. Cost:
  until CU-C8, native Tab does not skip an unsignable signature field.
- CU-C5-D5. Three pieces of #1118 and #1141 wait for CU-C6. Cost: closed by CU-C6.
- CU-C5-D6. The new plugin cases are a vector file of their own. Cost: one more file for
  `every_form_case_replays`.
- CU-C5-D7. `formatDate` and `parseDate` are transcribed, not run. Cost: two copies of the date
  rules.
- CU-C5-D8. Red runs share the job's target dir. Cost: the next build rebuilds the workspace
  crates.

## CU-C6: annotation half and payload ([record](decisions/CU-C6.md))

- CU-C6-D1. The branch reuses CU-C5's target dir, and edits come in from a host worktree. Cost:
  CU-C5's tree no longer builds incrementally from `target-c5`.
- CU-C6-D2. The port reproduces zod 4's issue text. Cost: the next zod text change shows up as
  replay failures.
- CU-C6-D3. Minor 2 is coverage, not a fix, and has no red run. Cost: none beyond the case.
- CU-C6-D4. i18n has nothing to port. Cost: none.
- CU-C6-D5. The FFI surface stays as it was. Cost: until CU-C8, a native drag on a turned page
  previews the field upright, and native hosts cannot read the accent.
- CU-C6-D6. The new cases go into the existing files. Cost: the drag's accepted path has one case.
- CU-C6-D7. `PageBoxes` is keyed by the page number as JavaScript prints it. Cost: listed above.
- CU-C6-D8. Prettier only where the file was clean. Cost: none.
- CU-C6-D9. The Rust-core payload is the `wasm32-eh` build. Cost: CU-C6's suites did not run on it;
  CU-C7 did.

## CU-C7: integration and the full table ([record](decisions/CU-C7.md))

- CU-C7-D1. See "Scope" above.
- CU-C7-D2. A worktree of its own, `target-c5` with sources touched, and `/workspaces/target-base`
  for the baseline. Cost: one more target dir until CU-C11.
- CU-C7-D3. Baseline rows Job A did not run are measured here; the baseline `wasm32-eh` uses its
  own `af480d0ce` archive. Cost: about an hour of container time.
- CU-C7-D4. The baseline's plain `wasm32` row is its second run, after stale headers. Cost: none.
- CU-C7-D5. The TS row is a `turbo --force` run. Cost: one more full TS run.
- CU-C7-D6. Job E's Minors and the clippy fix are edited on the host and verified in the
  container. Cost: none.
- CU-C7-D7. The clippy warning in `form/src/ffi.rs` is the port's and is fixed. Cost: none.
- CU-C7-D8. `vectors/rust-coverage.json` is regenerated (`57ad31610`); stale at `d250a68e3` too, and
  replay reaches more lines at the tip. Cost: one diff of 4,212 insertions and 3,322 deletions,
  reviewed whole.

## CU-C8: browser, Kotlin and .NET ([record](decisions/CU-C8.md))

- CU-C8-D1. A worktree and target dir of its own; edits come in from a host worktree by `tar`.
  Cost: 41 GB for `target-c8` until CU-C11.
- CU-C8-D2. The CU-C7 record fix labels both coverage rows and the two prose places that cite
  them. Cost: two more lines than the Minor listed.
- CU-C8-D3. Kotlin runs in an amd64 container from the existing amd64 dev image, because the
  arm64 image has no Android SDK or NDK. Cost: that image's inputs are `94822b7a9`'s; every
  Kotlin build is emulated.
- CU-C8-D4. The Android slices are built locally at runtime `9472117ed` and the local pin takes
  their sha256. Cost: a local build, not a release's, until CU-C10.
- CU-C8-D5. FFI names: `frame` for upstream's `box`, `groups`, `design_group`,
  `selected_fields`, `widget_anchor`; `RecordAccent` in `epdf-ffi`; one `FormFieldFormat` for
  reads and writes; a non-finite tint refused. Cost: `frame` is not upstream's word.
- CU-C8-D6. `FormFocusTarget` gains `takes_cursor` and a `bool` from `focus`; C# defaults
  `TakesCursor` to true. Cost: a source break for every Kotlin, C# and Swift target.
- CU-C8-D7. The nine new form members get coverage rows in the C#, Kotlin and Swift manifests;
  the Swift rows are pending CU-C9. Cost: the Swift check's failure changes wording until CU-C9.
- CU-C8-D8. A lent record's accent is read from the model record. Cost: one lookup per record.
- CU-C8-D9. `epdf-capi`, `epdf-web` and `packages/core-wasm` need no code. Cost: none.
- CU-C8-D10. No form controls UI (#1138): no Kotlin or .NET layer renders forms. Cost: apps
  build the controls themselves.
- CU-C8-D11. The osx rows build `epdf-capi` with cargo on the Mac. Cost: an exception to "no
  cargo on the host" for two builds.
- CU-C8-D12. `check-dotnet-engine.sh`'s verdict comes from the Mac: stale, unpublished. Cost: the
  check fails on this branch until CU-C10.
- CU-C8-D13. The emulator runs the container's AAR libraries. Cost: the device tests the
  container's build.
- CU-C8-D14. The linux-x64 .NET rows use SDK 11.0.100 installed into the amd64 container. Cost:
  not the image's SDK, and emulated.

## CU-C9: Swift on the Mac ([record](decisions/CU-C9.md))

- CU-C9-D1. The Apple slices are built by a shell port of `buildFork` in a clone of the
  `af480d0ce` fork tree, which resyncs gclient deps once for `9472117ed`'s `DEPS`. Cost: the shell
  copy can drift from `buildFork`.
- CU-C9-D2. `takesCursor()` defaults to `true` in Swift, as in C#; `focus(select:)` answers a
  `Bool`. Cost: a declining target must remember to write `takesCursor()`.
- CU-C9-D3. Swift names follow the facade: `groups()`, `setGroups(_:)`, `moveToGroup(_:groupID:)`,
  `widgetAnchor(_:)` and the rest; `previewPlacement` gains `displayRotation`. Cost: none.
- CU-C9-D4. No form controls UI, MK icon, stamp-drawing or text-geometry surface in Swift:
  `EmbedPDFUI` has no form layer. Cost: apps draw their own form controls.
- CU-C9-D5. The start column is `89dde2cf9`, `d250a68e3`'s tree with its published engine
  `apple-engine-c55b85806b96c5b2`. Cost: a downloaded start against a local head.
- CU-C9-D6. The test runner stages the macOS consumer with Python, and `APPLE_CRATES` is checked
  by a Python port of `linked-crates.mjs`. Cost: two Python copies of `.mjs` logic.
- CU-C9-D7. The three signature route tests stay skipped (their helper is Node). Cost: those paths
  are not exercised on Swift this catch-up.
