# CU-C4 decisions

Decisions made while porting #1161, #1162 and #1163 (`3f54d7016`, `445e34a69`, `b45d916cb`,
`4c911282d`) and D-2 and D-3 into the Rust core.

## CU-C4-D1. A widget's `layout` is written through the widget style here, not in CU-C5

Decision: `fit_widget_to_family` takes `layout` (a push button's only, `null` refused there) and
`apply_widget_style` writes `/MK /TP`, in both the engine
(`crates/epdf-engine/src/features/forms/field.rs`, `crates/epdf-engine/src/features/annotation/write/widget.rs`)
and the form plugin's prediction (`crates/plugins/form/src/engine.rs`).
Reason: CU-C3-D2 gave CU-C4 the read of `layout`. Once a widget row carries it, a row sent back as
an `updateWidget` patch carries it too, and `form_widgets::a_push_button_has_a_caption_and_an_action_and_holds_no_value`
failed with `widget has no field 'layout'`. Upstream takes `layout` in the same style path
(`writeWidgetAnnotation.ts`, `forms/widgets.ts`), so the write is the same few lines either way.
Cost: CU-C5 item 3 shrinks to its tests and to `addWidget`'s image; this job carries no
`runPictureButtonConformance` test for the write.
Reverse: drop `layout` from `WIDGET_STYLE_NAMES` and the widget field table, and refuse it until
CU-C5.

## CU-C4-D2. The anyone-fields runner is split between change vectors and a Rust test file

Decision: the tests of `runAnyoneFieldsConformance.ts` one session runs (the read, fill rights for
each of the 8 tokens, the refusals, marking, the two undos, `expect`, the two copies) are a new
change-vector group, `tooling/engine-conformance/vectors/changes/anyone-fields.json` (17 cases),
replayed by `change_ops::a_field_anyone_fills_in`. The tests that sign, hand a file on, or export
and import are ported by hand in `crates/epdf-engine/tests/anyone_fields.rs` (10 tests). Test 7,
the image field, waits for `forms.setButtonImage` (CU-C5).
Reason: the change generator records one session against one fixture; it has no step for signing
with a placeholder CMS, handing on the saved bytes, or carrying an export into another document.
The 2026-11-27 run made the same split for the signature mark (its CU-C4-D9).
Cost: the hand-ported half restates upstream's assertions; it is not recorded from a TypeScript
run. `allowsField` (test 2's second half) has no engine route, so only the viewer host's
`NativeSecurity::allows_field` carries the field's `forAnyone`, untested here.
Reverse: give the generator sign, hand-on and import steps.

## CU-C4-D3. `ChangeAuthority` keeps its group-only checks beside the field-assignment ones

Decision: `FieldAssignment { group_id, for_anyone }` is new in `auth::scope`, with
`check_field_action_for`, `allows_field_write_for` and `require_field_write_for`. The existing
`check_field_action(group)`, `allows_field_write(group)` and `require_field_write(group)` stay and
read a group as a field of that group, as upstream's `FieldAssignment | string | null` does.
Every engine site that has the field read (fills, resets, values imports, reverted fills, script
output) passes the field.
Reason: 32 callers pass a group alone, many in tests and in `plugin-conformance`'s fake engine,
which mirrors `tooling/plugin-conformance/src/fake-engine.ts` and that passes `groupId` alone.
Signing still passes the group: a signature field is never for anyone.
Cost: two names per check where upstream has one overloaded function.
Reverse: make the group-only forms take `impl Into<FieldAssignment>` and delete the `_for` names.

## CU-C4-D4. `completeFormBundle` runs in `codec::parse` only

Decision: D-2 is ported as `form_bundle::complete_form_bundle`, called from `codec::parse` for a
form bundle, as upstream's `FormTransfer.parse` calls `spec.complete`. An import over the wire does
not complete rows.
Reason: upstream completes a bundle only in its file codec; the wire path reads field rows through
`FormFieldDTOSchema`, whose defaults the Rust import already gets from reading each row as JSON (a
row without `forAnyone` makes a draft without it, a row without `layout` a placement without one).
This closes 2026-11-27 CU-C4-D9's `purpose` divergence for parsed files.
Cost: `with_purpose_and_format` reads a row's `actions` as `FieldActions<Value>`; actions of a shape
that doesn't parse read as no format, where upstream's `loneScriptOf` reads them as `other`.
Reverse: run the completion over every imported bundle as well.

## CU-C4-D5. The v4 corpus test checks each signed signature's verdict and policy version

Decision: `crates/epdf-engine/tests/signature_compat_v4.rs` analyzes each of the 11 PDFs in
`packages/engine/main/test/fixtures/signature-compat/v4/` from each signed signature to the saved
bytes, and compares the verdict with the entry's `modifications` (one per signature, as
`promote_expectations.py` writes them), the policy version with `policyVersion`, and the file's
SHA-256 with `sha256`. `relationship` is reported, not checked.
Reason: no TypeScript test reads `policy-expectations.json`; the file is the corpus's record of what
the policy says (D-3). `relationship` describes how the verdict relates to Acrobat's, which no
engine output carries.
Cost: none beyond the test's half second.
Reverse: delete the file.

## CU-C4-D6. `kind_facts.rs` is regenerated here, not in CU-C5

Decision: `crates/plugins/annotation-core/src/engine/kind_facts.rs` is regenerated with
`crates/plugins/annotation-core/scripts/kind-facts.ts` in this job (`8d6745687`): one line,
`("layout", FieldOwner::Data)` after `checkStyle`.
Reason: the form plugin's predicted widget row is built by `annotation_of_draft` from these facts.
Without `layout` in them, the three `form-core.json` cases CU-C3 gave CU-C4
(`update/create-without-a-group`, `update/create-names-its-group`, `update/copy-through-create`)
still differ, on member order: `layout` lands after `hasMark` instead of after `checkStyle`. The
same line fixes the 7 `annotation-repository.json` cases CU-C3 gave CU-C5.
Cost: none; CU-C5 item 7 is done.
Reverse: none needed.
