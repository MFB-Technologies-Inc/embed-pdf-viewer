# CU-C3: vectors and the work order

The engine vectors and the plugin vectors were each regenerated twice, byte-identical both times.
All 36 engine `:check` scripts pass again, and `vectors:check`, `coverage:check` and
`test:harness` pass; `typecheck` still has CU-C2's 9 errors, because the generator was not edited
(CU-C3-D1). `cargo test --workspace` goes from 3,630 passed and 0 failed to 3,611 passed and 19
failed: 16 in `epdf-engine`, 2 in `epdf-plugin-form`, 1 in `epdf-plugin-annotation`. No Rust
changed. Every failure is one of four things the vectors gained: `forAnyone`, `hasImage` and
`layout` on rows, and signature policy version 6, plus `forms.setButtonImage` in the change kinds.
The new behaviour itself (anyone fields, picture buttons, the `signature-appearance` rule) has no
engine vector yet: 40 runner tests and 13 changed ones have no Rust pass. The work order is the
last section. Decisions are in [`../decisions/CU-C3.md`](../decisions/CU-C3.md).

## Commits

| Commit | Summary |
|---|---|
| `d92c1987f` | `scripts/prep.sh` runs `scripts/freetype-src.sh` as its last step (the CU-C2 review finding) |
| `3e52c281e` | Engine vectors regenerated: 21 files |
| `fc22e1e80` | Plugin vectors regenerated: 19 files; `coverage.json` unchanged |
| the commit that adds this file | Report, decisions, results, `scripts/cu-c3/` |

Made in container `embed-pdf-catchup1127b`, worktree `/workspaces/wt/CU-C2`, on
`research/catchup-2026-11-27b-plugins` from `b2ebe03e2`, and brought to the host worktree
`.claude/worktrees/catchup-1127b` as a git bundle, fast-forward only.

## Engine vectors

`regen2x.sh` ran all 35 generator groups twice: identical logs, identical sha256 lists over 68
files (`regen1/engregen-run{1,2}.sha`, both `28a7b91f`), every group `ok`. 21 files changed. No
vector gained an error string (`eng-errgrep.log`).

| Files | Change | Upstream |
|---|---|---|
| `signature/{protection,long-term,timestamp-field,validation-info}`, and every request's `authority.protection` in `forms/{field,write}`, `pages/mutate`, `changes/*` | `policyVersion` 5 to 6 | #1161 (`3f54d7016`) |
| `forms/{snapshot,write,field,apply}`, `changes/{field-groups,field-purpose-format,form-copy,signature-mark,widget-rotation}` | field rows gain `forAnyone: false` | #1163 (`b45d916cb`) |
| `forms/snapshot`, `changes/{field-purpose-format,form-copy}` | push button rows gain `hasImage: false` | #1165 (`7d5691a5a`) |
| `annotation/read`, `forms/{snapshot,apply}`, `changes/{field-groups,field-purpose-format,form-copy,signature-mark,widget-rename,widget-rotation}` | widget rows gain `layout: null` | #1165 |
| `changes/kinds` | `forms.setButtonImage` arrives in `3.5` | #1165 |
| `changes/signature-mark` (`a-png-a-jpeg-and-a-pdf-fill-a-signature-field`), `changes/widget-rotation` (`a-turn-turns-the-mark-and-undo-puts-it-back`) | the next field and widget are objects 78 and 79 (84 and 85), were 80 and 81 (86 and 87) | #1165 (`7d5691a5a`, the mark through the shared drawing; CU-C3-D3) |
| `wire/requests` | `forms.updateWidget`'s `patch` type takes `layout` | #1165 |

No generator in `tooling/engine-conformance` records `runAnyoneFieldsConformance.ts` or
`runPictureButtonConformance.ts`. The vectors above only reshape rows; the behaviour is in the
runners (Coverage gaps).

## Plugin vectors

`TZ=UTC pnpm run vectors` twice: identical sha256 lists over 128 files, `prelude.js` included
(`regen1/plugregen-run{1,2}.sha`, both `702a03d2`). 19 files changed, the 19 CU-C2's
`vectors:check` named; `prelude.js` did not. No error strings (`plug-errgrep.log`).

Every change is a recorded engine row (`plug-diffpaths.log`): widget rows and widget annotation
rows gain `layout: null` in all 19 files; field rows gain `forAnyone: false` in `form-core`,
`form-design` and `form-scripting`, and push buttons `hasImage: false` in `form-scripting`. These
are rows the generator recorded from the TS engine. Rows that come from the generator's own
fixtures (`form-current-fixtures.ts`) still lack `forAnyone` (CU-C3-D1).

## Tests

All in the container, `CARGO_TARGET_DIR=/workspaces/target`.

| Check | Start (`b2ebe03e2`) | Now (`fc22e1e80`) |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,630 passed, 0 failed, 2 ignored | 3,611 passed, 19 failed, 2 ignored |
| engine `:check` scripts | 26 of 36 | 36 of 36 (`engconf.all` hashes `27a10bbd`, as at `0afd87825`) |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | 1 of 4 | 3 of 4: `typecheck` has the same 9 errors (CU-C3-D1) |
| plugin `replay:check` | not run | fails: 11 cases in 3 files (the 3 plugin cargo failures) |
| plugin evidence tests | 769 of 771 at `0afd87825` (measured here) | 752 of 771: the 2 known in `current-suites.test.ts` and 17 pinned vector hashes in 12 more files (CU-C3-D4) |
| TS `turbo run test` over `packages/**` | CU-C2's numbers | not rerun: no file under `packages/` changed |

Every failing test with its assertion, first difference, cause, PR and job is in
[`../results/CU-C3-cargo-failures.md`](../results/CU-C3-cargo-failures.md).

## Coverage gaps

[`../results/CU-C3-coverage-gaps.md`](../results/CU-C3-coverage-gaps.md). The plugin side has none:
`gaps.py` finds 0 uncovered lines added in `9af4d9ee7..cda9e82a4`, since the range changed only
tests under `packages/plugin/**`. The gaps are the engine's: 19 tests in
`runAnyoneFieldsConformance.ts`, 21 in `runPictureButtonConformance.ts`, and new or changed tests
in the attribution, transfer, signature-mark, prediction and change runners, none ported to Rust.
One passing Rust test asserts what upstream dropped: `crates/epdf-engine/tests/form_reader.rs:512-514`
reads `filledBy` `u-42` from the file, where upstream now always reads `null`.

## Work order

CU-C4 and CU-C5 run in sequence on this branch. Routing is by crate and PR, with two moves from
the plan: CU-C4 reads all three new row members (CU-C3-D2), and the signature mark's move to the
shared drawing is CU-C5's (CU-C3-D3). The TS sources named are under
`packages/engine/{core,services}/src/`.

### CU-C4: signatures and anyone fields (#1161, #1162, #1163, D-2, D-3, and the reads of #1165)

Failing now, to fix: 15 of the 16 `epdf-engine` tests (all but
`every_change_kind_has_the_version_it_arrived_in`) and both `epdf-plugin-form` tests. Done when
those pass, apart from the two object-number cases of CU-C3-D3, and the runner tests below have
Rust ports.

1. Policy version 6 and the `signature-appearance` rule (#1161, `3f54d7016`). `SIGNATURE_POLICY_VERSION`
   to 6 (`crates/epdf-engine/src/features/signature/protection.rs:20`); in the analyzer
   (`signature/analysis/evaluate.ts`), a new `/AP` on an unsigned signature field, all of it new
   and nothing else on the field changed, is `permitted` after an approval signature and under a
   P=2 certification; the signed field's own stays `forbidden`. Fixes `signature_protection` and
   the 3 `signature_ltv` tests. Port the 7 `analysis.test.ts` tests for the rule into
   `crates/epdf-engine/src/features/signature/analysis/tests.rs`.
2. D-3: a test in `crates/epdf-engine/tests/` that analyzes each of the 11 PDFs in
   `packages/engine/main/test/fixtures/signature-compat/v4/` and checks it against its entry in
   `policy-expectations.json` (entries carry `id`, `sha256`, `policyVersion`, `relationship`,
   `modifications`), reading the fixtures as `crates/epdf-engine/tests/signature_sign.rs:26` does.
3. Fills record nobody (#1162, `445e34a69`). `forms/internal/fieldAttribution.ts`, `FormMutator.ts`,
   `changes/internal/formChanges.ts`: a fill, a reset and a script's value write no `/FilledBy`
   into `/EMBD_Metadata`; `filledBy`, `filledByName`, `filledAt` read `null` whatever the file
   holds (D-1: keep the members, `None`); a values import takes fill rights only, no
   `doc.forms.import`, and its undo puts the values back. Flip
   `crates/epdf-engine/tests/form_reader.rs:512-514` and the removed attribution tests'
   ports. Port the 3 new `runFormAttributionConformance.ts` tests and the 3 changed
   `runFormTransferConformance.ts` tests (`crates/epdf-engine/tests/form_transfer.rs`).
4. A mark records no fit (#1162): `signature/internal/appearance.ts` drops `recordFit` and
   `writeAppearanceFit`; `SignatureMutator.ts`. Port "a drawing saved into a field that isn't
   signed keeps an earlier signature…" into `crates/epdf-engine/tests/signature_mark.rs`.
5. `forAnyone` (#1163, `b45d916cb`, `4c911282d`). Read `/ForAnyone true` from `/EMBD_Metadata`
   (never on a field in a group or a signature field); `checkedForAnyone` on create and update
   with its two refusals; `authorizeForAnyone` (`fields:set-group:all` to mark, form design to
   unmark); field rights in `auth/scope/resolver.ts` and `authority.ts` take the field
   (`FieldAssignment`), and any fill scope fills an anyone field; the `expect` guard; undo puts
   group and `forAnyone` back together; copy, export and import keep it. Port the 19
   `runAnyoneFieldsConformance.ts` tests (test 7 after CU-C5's `setButtonImage`), the 9 new
   `runFormPredictionConformance.ts` rows, and the unit tests in `fields.test.ts`,
   `anyone.test.ts` and `schema.test.ts`.
6. The signing lock (#1163, `4c911282d`, `SignatureMutator.ts`): a signing that names no lock
   locks every anyone field that holds a value; a signature field with its own `/Lock` signs with
   that alone. The plan's named risk: it changes what every signature locks.
7. Row members (CU-C3-D2): `forAnyone` on every field row, `hasImage` on push button rows
   (`EPDFAnnot_HasMKIcon` over the widgets, `crates/pdfium/src/stamp.rs:430`), `layout` on every
   widget row and widget annotation row (`/MK /TP`, seven values, `caption` the default on a push
   button, `null` elsewhere; `kinds/widget/layout.ts`, `readWidgetAnnotation.ts`,
   `joinWidgetField.ts`). Fixes `form_reader`, `form_write`, `form_field`, `form_apply`,
   `annotation_reader` and the six `change_ops` tests.
8. D-2: `completeFormBundle` (`transfer/FormBundle.ts`) with `withPurposeAndFormat`,
   `withForAnyone`, `withButtonImage` and, for widget rows, `withWidgetLayout`. Closes
   2026-11-27 CU-C4-D9.
9. `ANYONE_FIELDS_SINCE = "3.5"` and `API_VERSION` 3.5 in
   `crates/epdf-engine/src/features/changes/kinds.rs`.
10. `epdf-plugin-form`: the plugin's own field row for a create and a copy carries
    `forAnyone` (`crates/plugins/form/src/engine.rs:1206` writes `purpose` beside it). Fixes
    `form-core.json` `update/create-without-a-group`, `update/create-names-its-group`,
    `update/copy-through-create` and `form-design.json` `duplicate-fields-and-widgets`.
11. New engine cases: give `tooling/engine-conformance` a generator group (or `changes/` cases)
    for the anyone-fields runner, as 2026-11-27 did for the signature mark, so the Rust replays
    have vectors and not only ported tests.

### CU-C5: picture buttons (#1165)

Failing now, to fix: `change_ops::every_change_kind_has_the_version_it_arrived_in` and
`epdf-plugin-annotation`'s `repository_functions_match_the_typescript` (7 cases); the
object-number cases of CU-C3-D3. Done when those pass and the runner tests below have Rust ports.

1. `forms.setButtonImage` as a change op with its payload (`mutation/Change.ts`,
   `resolvePictureBytes`: PNG, JPEG or one-page PDF; `null` takes it out), as the verb in
   `engine/formVerbs.ts` (`applyOne`), registered in `crates/epdf-engine/src/features/changes/kinds.rs`
   at `3.5` (`BUTTON_IMAGE_AND_LAYOUT_SINCE`). Gated as filling in; refused on another kind
   (`InvalidArg`) and on a locked field (`ProtectedDocument`). One undo step.
2. The image in `/MK /I` through `EPDFAnnot_SetMKIcon` and `ClearMKIcon`
   (`crates/pdfium/src/stamp.rs:409-440`), and the shared drawing: one picture stored once for
   every button, signature mark and stamp (`forms/internal/buttonImage.ts`, `stampDrawing.ts`
   `drawingFor`, `DrawingIndex` at `crates/epdf-engine/src/features/annotation/write/stamp.rs:32`).
   The mark moves onto it here (CU-C3-D3).
3. `layout` written by a create, `addWidget` and `updateWidget`; refused on other widgets, `null`
   refused on a push button (`forms/widgets.ts`, `writeWidgetAnnotation.ts`, `widgetLayout.ts`).
4. `addWidget` gives a new place the field's image; a merged field and widget split for it
   (`createPlannedFields.ts`, `FormMutator.ts`).
5. Copy, export and import keep the image and the layout; a `noExport` field goes without its
   image (`FormCopier.ts`, `FormExporter.ts`, `FormImporter.ts`, `WIDGET_RESOURCE_ROLES =
   ['image']` in `transfer/FormBundle.ts`).
6. The analysis: an image put in or taken out of a push button is filling in, `permitted` after an
   approval signature and under P=2; a caption or layout change stays design; a locked button
   stays frozen; another kind's `/MK /I` is unexplained (`evaluate.ts`, 6 `analysis.test.ts`
   tests, `picture-button-analysis.test.ts`).
7. `epdf-annotation-core`: regenerate `crates/plugins/annotation-core/src/engine/kind_facts.rs`
   with `crates/plugins/annotation-core/scripts/kind-facts.ts` (`layout: 'data'` on widgets).
8. The native host: `crates/epdf-viewer-host/src/translate.rs` carries `forms.setButtonImage`'s
   bytes as a payload inside `documents.applyChanges`, as `Change.ts` pushes them into `buffers`.
9. Cases: the 21 `runPictureButtonConformance.ts` tests, the `forms.setButtonImage` rows of
   `runChangeConformance.ts` (`crates/epdf-engine/tests/document_apply.rs`), the 11
   `pictureButton.test.ts` unit tests, and an engine generator group for the runner as in
   CU-C4 item 11.

### CU-C7: what the browser, Kotlin and .NET wrappers expose

Kotlin (`platforms/android`) and .NET (`platforms/dotnet`) reach forms through the form plugin's
UniFFI records in `crates/plugins/form/src/ffi.rs`; the browser through `epdf-web` and
`packages/core-wasm`, which pass engine rows as JSON and type no field members (`git grep purpose`
finds none there), so the browser needs only the new call.

| Surface | Rust record (`crates/plugins/form/src/ffi.rs`) | Owner of the Rust behaviour |
|---|---|---|
| `for_anyone: bool` on a field | `FormField` (`:270`) | CU-C4 5 |
| `for_anyone: Option<bool>` on create and update | `FormCreateFieldInput` (`:893`), `FormFieldPatch` (`:985`) | CU-C4 5 |
| `has_image: bool` on a push button | `FormFieldDetail::Pushbutton` (`:222`, today a unit variant) | CU-C4 7 |
| `layout: Option<FormWidgetLayout>`, seven values | `FormWidget` (`:175`); on input `FormWidgetPlacement` (`:950`) and `FormCreateFieldInput` | CU-C4 7, CU-C5 3 |
| `set_button_image(field_ref, image: Option<Vec<u8>>, cancel)` | new on `FormHandle` (`:1289`); TS's form plugin has no such verb, so this reaches the engine directly | CU-C5 1 |
| D-1 deprecations | none: no wrapper types `filledBy` (CU-C3-D5) | |

Each platform needs a test per new call, as the plan says: create and update with `forAnyone`,
read `hasImage` and `layout`, set and clear an image.

### CU-C8: Swift

The same rows as CU-C7, in `platforms/apple/EmbedPDF` over the same UniFFI records, then
`check-swift-api-coverage.py` and the three test runs. No D-1 deprecation (CU-C3-D5).

## Evidence

Head tested: `fc22e1e80`. Logs in `/workspaces/logs/CU-C3/` in the container; scripts in
[`../scripts/cu-c3/`](../scripts/cu-c3/), copied from 2026-11-27's with `gaps.py`'s range moved
to `9af4d9ee7..cda9e82a4` and `after.sh` calling this run's `suite.sh`. Hashes are sha256, first 8
hex digits.

| Check | Command | Exit | Log | sha256 |
|---|---|---|---|---|
| Regen x2 | `regen2x.sh /workspaces/wt/CU-C2 regen1` | 0 | `regen1/determinism.log` | `2e1b9a5c` |
| Engine runs | | | `regen1/engregen-run{1,2}.sha` | `28a7b91f`, `28a7b91f` |
| Plugin runs | | | `regen1/plugregen-run{1,2}.sha` | `702a03d2`, `702a03d2` |
| Engine vector review | `zcasediff.mjs`, `diffpaths.mjs`, `errgrep.mjs` | 0 | `eng-casediff.log`, `eng-diffpaths.log`, `eng-errgrep.log` | `55a8c73a`, `4771814a`, `8233db58` |
| Plugin vector review | same | 0 | `plug-casediff.log`, `plug-diffpaths.log`, `plug-errgrep.log` | `b4c4cc82`, `77f0f1f0`, `8f2e211c` |
| `vectors:check`, `coverage:check` | `pnpm -s run …` | 0, 0 | `vectors-check.log`, `coverage-check.log` | `e3a920b4`, `36c7df57` |
| Coverage gaps | `python3 gaps.py` | 0 | `coverage-gaps.txt` | `70106076` |
| After suite | `after.sh /workspaces/wt/CU-C2 after` | 0 (script) | `after/cargo.log`, `after/cargo.summary` | `b8186a56`, `d7c01999` |
| Engine `:check` | (in `after.sh`) | | `after/engconf.all` | `27a10bbd` |
| Plugin checks | (in `after.sh`) | | `after/plugconf.all`, `after/plugconf.typecheck.log` | `e7a4a5e9`, `dd6ba4b0` |
| `replay:check` | (in `after.sh`) | 1 | `after/plugconf.replay:check.log` | `9b69d739` |
| Failures by crate and case | `cargo-by-crate.py`, `replay-cases.py` | 0 | `after/cargo-by-crate.tsv`, `after/replay-cases.tsv` | `8b774e84`, `43a04313` |
| Evidence at base | `evrun.sh /workspaces/wt/base base` | 0 | `ev-base/summary.txt` | `6290cd61` (as at `d250a68e3`) |
| Evidence after regen | `evrun.sh /workspaces/wt/CU-C2 regen` | 0 | `ev-regen/summary.txt` | `6268c849` |

## Known gaps

- `typecheck` fails (CU-C3-D1); a ruling is needed before CU-C4 starts, since the fix moves
  fixture-fed plugin vectors.
- The evidence archives carry 17 extra failures until CU-C6 (CU-C3-D4).
- CU-C3-D3's object numbers are inferred from the vector diff, not from a Rust run.
- The 2 expired OCSP fixtures in `@embedpdf/core-signature` (CU-C2) still fail; TS tests were not
  rerun.
- `rust-coverage:check` was not run, as in 2026-11-27.
- No temporary build target was made; `/workspaces/target` (12 GB) stays for CU-C4.
