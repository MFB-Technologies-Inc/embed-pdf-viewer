# CU-C5: picture buttons

#1165 (`7d5691a5a`, `bfbaac1f2`) is ported into the Rust core. `cargo test --workspace` goes from
CU-C4's 3,660 passed and 3 failed to 3,698 passed and 0 failed (2 ignored). All 36 engine `:check`
scripts pass, all 4 plugin checks do, `typecheck` included, and `replay:check` replays all 16,076
cases in 124 files. The three failures CU-C4 left are fixed: CU-C3-D3's two signature-mark
object-number cases (the mark now goes onto the shared drawing) and
`every_change_kind_has_the_version_it_arrived_in`, which also needed `API_VERSION` back at 3.4
(CU-C5-D1). Decisions are in [`../decisions/CU-C5.md`](../decisions/CU-C5.md).

## Commits

On `research/catchup-2026-11-27b-plugins` from `d7378a9f1`, edited on the host and built and tested
in container `embed-pdf-catchup1127b`, worktree `/workspaces/wt/CU-C2`.

| Commit | Items | Summary |
|---|---|---|
| `28e2224cb` | | `API_VERSION` 3.4, as the packages are (CU-C5-D1) |
| `9b6145df6` | 6 | `imageOnlyMkChange` and the `MK/I` claim in `rule_form_fill`; 6 `analysis.test.ts` cases |
| `8ed1f4f77` | 1-5, 8 | `forms.setButtonImage` op, item, kinds row; `FormMutator::set_button_image`; `forms/button_image.rs`; `picture_format_of`, `drawing_for_format`; the mark on the shared drawing; `addWidget`, copy, import, export, `noExport`; the placement's `layout`; `filled_anyone_fields` reads `ForAnyone` first |
| `1426f3e90` | 9 | `changes/picture-buttons.json` (19 cases) and its generator steps; `tests/picture_buttons.rs`; anyone-fields test 7; the two `runChangeConformance.ts` round trips |
| `a719b31b3` | 8 | `NativeSecurity::allows_field` unit test; the picture as an `apply` payload through `translate` |
| the commit that adds this file | | Report, decisions, `scripts/cu-c5/` |

## Items

1. Done (`8ed1f4f77`). `ChangeOp::FormsSetButtonImage { field, image }` in
   `crates/epdf-engine/src/features/changes/op.rs`, `image` required and nullable as
   `ButtonImageResourceKeysSchema` is, resolved from a payload; `ChangeItem::FormsSetButtonImage`
   publishes `forms.updated`; `forms.setButtonImage` sits after `forms.setSignatureAppearance` in
   `CHANGE_KINDS` with `BUTTON_IMAGE_AND_LAYOUT_SINCE` (3.5) in `CHANGE_KIND_SINCE`, and is a fill.
   `changes::forms::set_button_image` checks the fill right with the whole field (bfbaac1f2), so an
   anyone field takes any signer's image.
2. Done (`8ed1f4f77`). `button_image::set_widget_icon` writes `/MK /I` through
   `Annotation::set_mk_icon` (`crates/pdfium/src/stamp.rs:421`) or `clear_mk_icon` and draws the
   look again; nothing new was needed in `pdfium-sys` or `pdfium`. `drawing_for_format` in
   `crates/epdf-engine/src/features/annotation/write/stamp.rs` serves stamps, marks and buttons from
   one `DrawingIndex`. `bake_widget_appearance` places the mark with `EPDFAnnot_SetDrawing`; a
   signing's candidate gets a `DrawingIndex` of its own, as upstream's `SignatureMutator` does.
3. Done. CU-C4-D1 wrote `layout`; the three `pictureButton.test.ts` fit tests are in
   `crates/epdf-engine/src/features/forms/field.rs`. `addWidget` reads the field's image before a
   merged field splits and sets it on the new widget (`forms/field.rs`, `PlannedWidget::image`).
4. Done with item 3.
5. Done (`8ed1f4f77`). `WIDGET_RESOURCE_ROLES = ["image"]`; a copy with values places each source
   widget's drawing (`PlannedFieldCopy::widgets` is new); an import with values places each row's
   `image`; the export's `ButtonImageCollector` keeps each drawing once and skips a field whose
   exported row has `hasImage: false`; `exported_field` gives a `noExport` push button
   `hasImage: false`. `plan_form::placement_of` carries `layout` unless it is `caption`.
6. Done (`9b6145df6`). `claim_subtree_by_parent` passes the edge's parent to `accept`.
7. Done in CU-C4 (CU-C4-D6).
8. Done: `translate` already turns every `{"$bytes"}` in a request into a payload, so an `apply`
   carrying the op needed no code; `a_push_buttons_picture_travels_as_a_payload_of_its_change` in
   `crates/epdf-viewer-host/tests/translate.rs` proves it, and `document.applyChanges` resolves the
   payload through `resolve_payloads`.
9. Done (`1426f3e90`, CU-C5-D2). Each of the runner's 23 tests (the three formats counted apart)
   has a vector case, a Rust test in `picture_buttons.rs`, or both; both upstream main tests (`picture-button-analysis.test.ts`, `shared-drawing-size.test.ts`) have Rust ports. Of the 11
   `pictureButton.test.ts` tests, 8 have Rust equivalents (the fit tests, `a_layout_outside_the_seven_is_no_layout`,
   `a_push_button_without_has_image_holds_none`, CU-C4's completion test, `kinds.json`); 3 schema
   tests are not ported (CU-C5-D4).

The controller's amendments: anyone-fields test 7 is
`an_image_field_is_every_signers_to_fill_in_and_locks_once_it_holds_a_picture` in
`crates/epdf-engine/tests/anyone_fields.rs`. `filled_anyone_fields` in
`crates/epdf-engine/src/features/signature/mutator.rs` reads the raw field's `ForAnyone` before
`read_at`, upstream's order, so it is no behaviour change; the whole read that stays for the value
check of an anyone field carries an inline perf comment. The `allowsField` test is a unit test in
`crates/epdf-viewer-host/src/native_engine.rs`, not in `viewer_host.rs` (CU-C5-D3).

One Rust test changed meaning: `a_malformed_pdf_mark_on_a_widget_with_no_page_is_not_found` in
`signature/mutator.rs` became `a_malformed_pdf_mark_is_refused_before_the_widget_is_looked_for`
and expects `MalformedPdf`, since upstream's `pictureFormatOf` now opens a `{ pdf }` mark before the
widget is loaded.

## New Rust API surface for CU-C7 and CU-C8

- The change op `forms.setButtonImage`, `{ "type", "field": FormFieldRef, "image": bytes | null }`,
  in `document.apply` and `document.applyChanges`. There is no worker verb of its own, as upstream
  has none (`formVerbs`); CU-C3's table already plans `set_button_image(field_ref, image, cancel)` on
  `FormHandle`, which builds this op.
- Its result item `forms.setButtonImage` (`FormFieldUpdateResult`: `field`, `widgets`, `meta`) and
  event `forms.updated`.
- `BUTTON_IMAGE_AND_LAYOUT_SINCE = "3.5"` in `epdf_engine::features::changes::kinds`;
  `kinds.json` names it `buttonImageAndLayoutSince`.
- Row members, all read since CU-C4 and unchanged here: `hasImage` on a push button field, `layout`
  on every widget row (seven values, `null` off a push button), `forAnyone` on every field. A
  `noExport` push button's exported row reads `hasImage: false`.
- A form bundle's widget row may name `resources.image`, a one-page PDF.

## Vectors

`scripts/cu-c5/regen2x.sh` ran every engine group and the plugin vectors twice from `a719b31b3`:
engine sha256 lists over 70 files identical (`9b7cb63d` both runs), plugin lists over 128 files
identical (`1f32794a` both runs, the same as CU-C4's). Nothing regenerated differs from the commit:
the engine side adds `changes/picture-buttons.json` and `buttonImageAndLayoutSince` in
`changes/kinds.json`, and no plugin vector moved, so CU-C6 has no plugin vector of this job to
re-pin.

## Tests

| Check | CU-C4 (`a806fb453`) | Now (`a719b31b3`) |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,660 passed, 3 failed, 2 ignored | 3,698 passed, 0 failed, 2 ignored |
| engine `:check` scripts | 36 of 36 | 36 of 36 (`engconf.all` `27a10bbd`, unchanged) |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | 4 of 4 | 4 of 4 |
| plugin `replay:check` | 16,076 cases | 16,076 cases in 124 files, all replayed |

Red run (`scripts/cu-c5/red.sh d7378a9f1 a719b31b3 red`): `picture_buttons.rs`, `anyone_fields.rs`
and `document_apply.rs` on the code from before the job, 35 pass and 20 fail. Every new test fails
except `a_create_and_a_widget_update_write_any_layout_and_a_save_keeps_it`, whose layout write
CU-C4 ported (CU-C4-D1). `change_ops.rs` was left out: it names `BUTTON_IMAGE_AND_LAYOUT_SINCE` and
doesn't build on the old code; the analysis unit tests live in `src/` and weren't red-run.

## Evidence

Logs in `/workspaces/logs/CU-C5/` in the container. Hashes are sha256, first 8 hex digits.

| Check | Command | Exit | Log | sha256 |
|---|---|---|---|---|
| Regen x2 | `regen2x.sh /workspaces/wt/CU-C2 regen1` | 0 | `regen1/determinism.log` | `e79162c4` |
| After suite | `after.sh /workspaces/wt/CU-C2 after` | 0 | `after/cargo.log`, `after/cargo.summary` | `5b45637d`, `2014460a` |
| Plugin checks | (in `after.sh`) | | `after/plugconf.all` | `cf0bfd16` |
| `replay:check` | (in `after.sh`) | 0 | `after/plugconf.replay:check.log` | `c89f0a5f` |
| Red run | `red.sh d7378a9f1 a719b31b3 red` | | `red/cargo.log`, `red/summary.txt` | `d3910d41`, `fa1e2a26` |

`after.sh` called `cargo-by-crate.py` and `replay-cases.py` through a relative path that broke once
it changed directory, so `cargo-by-crate.tsv` and `replay-cases.tsv` were not written; with no
failure in either there was nothing to sort. `scripts/cu-c5/after.sh` and `regen2x.sh` now resolve
their own directory.

## Known gaps

- Through the form plugin a group's token can't fill in a field anyone fills in: the plugin context
  asks with `{ groupId }` alone, on upstream (`packages/core/main/src/context.ts:264`) as in the
  kernel (`crates/epdf-kernel/src/plugin.rs:719-723`). The engine's write takes it. CU-C5-D3.
- CU-C3-D4's evidence archives still wait for CU-C6.
- TS `turbo run test` was not rerun: no file under `packages/` changed.
- No temporary build target was made; the red worktree was removed.
