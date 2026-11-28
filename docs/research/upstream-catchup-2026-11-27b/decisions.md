# Decisions: upstream catch-up 2026-11-27b

Every decision that resolved an ambiguity in this catch-up. Manager decisions are `D-n` (the plan's
"Decisions", accepted by the user on 2026-11-27); a job's decisions are `CU-Cn-Dn`, with the full
text (reason, cost, how to reverse) in [`decisions/`](decisions/). Each line here states the
decision and, where it has one, its cost, and links the job's record for the rest.

Targets: upstream viewer `cda9e82a4984cd7edb1a69a8fe32b1d7e11693b8`, upstream runtime `7f399f838`
(unchanged from the 2026-11-27 run). Starting points: plugins `0afd87825`, runtime pin `9472117ed`,
which already merges `7f399f838`.

## NEW DIVERGENCE (none observed by a vector); recorded Rust-only differences

No NEW DIVERGENCE. CU-C6 ran every Rust and Node row at the head (`0b55dab9f` for cargo, engine
`:check`, clippy, TS and the wasm rows; `f69b5f3c5` for the evidence tests, the plugin checks and
`replay:check`; `f69b5f3c5` and `22f7abd8c` for `rust-coverage:check`) against `0afd87825`, and
nothing is worse: `replay:check` replays all 16,076 cases in 124 files, the evidence tests fail the
same 2 as at the start, and `rust-coverage:check` passes once the record is regenerated
([`reports/CU-C6.md`](reports/CU-C6.md)). The port jobs recorded one Rust-only difference, which no
vector observes:

- CU-C4-D4. `completeFormBundle`'s `withPurposeAndFormat` reads a row's `actions` as
  `FieldActions<Value>`; actions of a shape that doesn't parse read as no format in Rust, where
  upstream's `loneScriptOf` reads them as `other`. The rest of 2026-11-27 CU-C4-D9's `purpose`
  divergence closes for parsed files (D-2).

Failures at both `0afd87825` and the head, upstream-owned, so not divergences: the 2
`current-suites.test.ts` evidence failures (the kept M27 NaN divergence, 2026-11-18 CU-C7), and the
2 `@embedpdf/core-signature` OCSP fixtures whose answers expired `2026-11-25T00:00:00Z`
(`packages/core/signature/test/revocation/interop.test.ts:82`, CU-C2 report).

The divergences accepted in earlier catch-ups stand as before
(`docs/research/upstream-catchup-2026-11-27/decisions.md`,
`docs/research/upstream-catchup-2026-11-18/decisions.md`,
`docs/research/upstream-catchup-2026-11-12/decisions.md`,
`docs/research/rust-plugins-branch-review.md`).

## Scope, authority and branches

- D-1. The deprecated attribution fields follow upstream: the Rust core keeps `filled_by`,
  `filled_by_name` and `filled_at`, always `None`, and the wrappers mark their properties
  deprecated. Cost: three dead properties per platform and deprecation warnings in our own tests
  until upstream's 4.0.
- D-2. `completeFormBundle` is ported in CU-C4, with the `forAnyone`, `hasImage`, `layout` and
  `purpose` defaults. Cost: about two hours in CU-C4; 2026-11-27 CU-C4-D9's divergence closes.
- D-3. A Rust test in `epdf-engine` checks each `signature-compat/v4/` fixture against
  `policy-expectations.json`. Cost: about half a day in CU-C4.
- D-4. This run starts beside the 2026-11-27 run on `-b` names and merges plugins' post-release tip
  in CU-C6. Cost: two catch-up containers on the disk for a few hours.

## CU-C2: setup, merge, start state ([record](decisions/CU-C2.md))

- CU-C2-D1. `pnpm-lock.yaml` stays as merged; upstream changed no dependency. Cost: none.
- CU-C2-D2. `sync-upstream.sh` made no runtime merge; the fork already has `9472117ed`. Cost: none.
- CU-C2-D3. The wasm32-eh `9472117ed` archive is the host copy of 2026-11-27's local build. Cost:
  a local build, not a published one.
- CU-C2-D4. `embedpdf-dev:latest` is reused and the clang shim stays on `PATH`. Cost: the
  generator bug behind the shim is still not fixed.
- CU-C2-D5. The `0afd87825` baseline is measured fresh. Cost: one suite run of container time.
- CU-C2-D6. FreeType's source is fetched into each `runtime-src` for the `core-fonts` test. Cost:
  one more container-only setup step.

## CU-C3: vectors and the work order ([record](decisions/CU-C3.md))

- CU-C3-D1. The plugin generator's 9 type errors stay and the plugin vectors come from it as it is;
  the three-line fixture fix needs a ruling. Cost: `typecheck` stays red, and fixture-fed plugin
  vectors regenerate once more when the fix lands. Reversed by the user's ruling: CU-C4 made the
  fix.
- CU-C3-D2. CU-C4 reads all three new row members, `layout` and `hasImage` included. Cost: about
  an hour moves from CU-C5 to CU-C4.
- CU-C3-D3. The signature mark's move to the shared drawing (`7d5691a5a`) is CU-C5's. Cost: two
  mark cases differ on object numbers until CU-C5.
- CU-C3-D4. The evidence archives are re-pinned in CU-C6, not here. Cost: 17 extra evidence
  failures through CU-C4 and CU-C5.
- CU-C3-D5. D-1 leaves nothing to deprecate in Swift, Kotlin or .NET: no wrapper types `filledBy`.
  Cost: a caller reading it out of `fields_json` gets `null` with no compiler warning.

## CU-C4: signatures and anyone fields ([record](decisions/CU-C4.md))

- CU-C4-D1. A widget's `layout` is written through the widget style in CU-C4, since a row sent back
  as a patch carries it. Cost: CU-C5 item 3 keeps only its tests and `addWidget`'s image.
- CU-C4-D2. The anyone-fields runner is 17 change-vector cases plus a hand-ported Rust file for the
  tests that sign, hand on or import. Cost: that half restates upstream's assertions.
- CU-C4-D3. The group-only field checks stay beside new `FieldAssignment` ones. Cost: two names per
  check where upstream overloads one.
- CU-C4-D4. `completeFormBundle` runs in `codec::parse` only, as upstream's file codec runs it.
  Cost: actions that don't parse read as no format, not `other`.
- CU-C4-D5. The v4 corpus test checks verdict, policy version and file hash per signed signature,
  not `relationship`. Cost: none.
- CU-C4-D6. `kind_facts.rs` is regenerated in CU-C4, not CU-C5. Cost: none.

## CU-C5: picture buttons ([record](decisions/CU-C5.md))

- CU-C5-D1. `API_VERSION` goes back to 3.4, the packages' version; CU-C4 had moved it to 3.5.
  Cost: a 3.5 server holds back 3.5's additions from this build, as from the TypeScript engine.
- CU-C5-D2. The picture-button runner is 19 change-vector cases plus `tests/picture_buttons.rs` for
  pixels, saves, signings and exports. Cost: that half restates upstream's assertions.
- CU-C5-D3. The `allowsField` check is a unit test of `NativeSecurity`: the plugin context sends
  `{ groupId }` alone, as upstream's does. Cost: through the form plugin a group's token can't fill
  an anyone field, on upstream too.
- CU-C5-D4. The op's wire resource keys and the placement schema tests are not ported. Cost: three
  schema tests.

## CU-C6: Rust and Node integration ([record](decisions/CU-C6.md))

- CU-C6-D1. Plugins' tip `e3d6e7fbc` is merged (`5a6d266dc`); release train `46080379002` was still
  running, so a later Apple pin commit is the controller's to merge. Cost: one merge commit.
- CU-C6-D2. The head runs in CU-C2's worktree with `/workspaces/target`; the start, and coverage on
  each side, get target dirs of their own. Cost: 18.4 GB of target dirs for the job's length.
- CU-C6-D3. The start's cargo, engine `:check`, plugin checks, TS and evidence rows are CU-C2's and
  CU-C3's; clippy, `replay:check`, the wasm rows and coverage are measured here. Cost: the TS
  baseline is not a `--force` run.
- CU-C6-D4. The evidence archives are re-pinned by the 2026-11-27 sequence (`f69b5f3c5`), with no
  witness changed and one `sourceHashes` digest replaced by hand. Cost: the archives move another
  step from the bytes they were cut from.
- CU-C6-D5. `drawing_for_format`'s PDF refusal reads "the appearance PDF could not be opened", as
  upstream's `drawingFor` does. Cost: none.
- CU-C6-D6. `vectors/rust-coverage.json` is regenerated (`22f7abd8c`): current at the start, stale
  at the head. Cost: a 4,020-line record diff reviewed as a whole.
- CU-C6-D7. The head's TS row is `turbo run test --force`. Cost: one full TS run.
- CU-C6-D8. `cargo fmt --all --check`'s diffs in `crates/epdf-node/src/lib.rs` and
  `crates/pdfium/tests/documents.rs`, present at the start too, are left. Cost: a workspace-wide
  fmt check stays red.

## CU-C7: browser, Kotlin and .NET ([record](decisions/CU-C7.md))

- CU-C7-D1. `/workspaces/wt/CU-C7` shares `/workspaces/target`; edits and commits on the host.
  Cost: CU-C2's next build there recompiles the touched crates.
- CU-C7-D2. `set_button_image` is a `FormHandle` export over the engine's `apply`, with no `Core`
  method and no coverage row; the browser keeps upstream's `doc.forms.setButtonImage`. Cost: the
  call is outside the plugin's undo history, and only each platform's test guards it.
- CU-C7-D3. Kotlin runs in `embed-pdf-1127b-x64` (amd64, Rosetta). Cost: emulated builds.
- CU-C7-D4. The Android slices are 2026-11-27's local `9472117ed` build, checked against the
  local pin. Cost: not a release's build.
- CU-C7-D5. `layout` is on `FormWidgetRecord`, not `FormWidget`, where upstream keeps it. Cost: a
  field alone doesn't carry its widgets' layouts.
- CU-C7-D6. `Pushbutton` gains `has_image`; `FormField` and `FormWidgetRecord` gain a member.
  Cost: a source break for positional construction on every platform.
- CU-C7-D7. No `*_SINCE` constant and no `epdf-capi` change. Cost: none.
- CU-C7-D8. The start's Kotlin and osx rows are 2026-11-27 CU-C8's head numbers; .NET linux-arm64
  was rerun at `0afd87825`. Cost: those rows are a day old.
- CU-C7-D9. The osx rows build `epdf-capi` with host cargo and scratch .NET. Cost: scratch installs,
  deleted at cleanup.
- CU-C7-D10. linux-x64 .NET installs SDK 11.0.100 in the amd64 container. Cost: emulated, not the
  image's SDK.
- CU-C7-D11. `SignatureRoutesTest` was rerun with CU-C2's built `dist`, and the emulator's
  `embedpdf` module with a lock-screen PIN set ahead. Cost: two counts are sums of reruns.
- CU-C7-D12. The linux-x64 Jint deep-recursion failure is the start's too (6 of 6 at both ends).
  Cost: the linux-x64 row is not green and the cause is not known.

## CU-C8: Swift ([record](decisions/CU-C8.md))

- CU-C8-D1. The framework is built from 2026-11-27's `9472117ed` Apple slices; none rebuilt. Cost:
  a local build, not a release's.
- CU-C8-D2. `setButtonImage(_:image:)` is cancellable and `@discardableResult`, with no coverage
  row, and gets a cancel test besides Kotlin's three. Cost: only the tests guard the call.
- CU-C8-D3. The start's Swift counts are 2026-11-27 CU-C9's head; only coverage was rerun at
  `0afd87825`. Cost: the start column is a day old.
- CU-C8-D4. The signature route tests ran through `routes-bridge.py` to the helper in the
  container. Cost: the start skipped them, so that row is not like for like.
- CU-C8-D5. The iOS run used a simulator made for the job, deleted at cleanup. Cost: a cold boot.
