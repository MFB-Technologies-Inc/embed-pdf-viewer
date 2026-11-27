# CU-C3 results: coverage gaps and conformance cases without a Rust pass

## Plugin coverage (c8)

`gaps.py` over `tooling/plugin-conformance/vectors/coverage.json` finds 0 uncovered lines that
`9af4d9ee7..cda9e82a4` added, and 0 upstream-touched files that no generator loads
(`/workspaces/logs/CU-C3/coverage-gaps.txt`). That is expected: the range changed only tests under
`packages/plugin/**` (`packages/plugin/form/test/*.test.ts`, 13 lines, fixtures gaining `forAnyone`
and `hasImage`). `coverage:check` passes and `coverage.json` did not change.

## Engine conformance runners

c8 does not measure the engine. What the range added there is in upstream's conformance runners
under `packages/engine/core/src/conformance/`, which no engine vector generator records yet and
no Rust test ports. Each test below has no Rust pass at `fc22e1e80`: `git grep` finds neither
runner named under `crates/` or `tooling/`, and `crates/` has no `for_anyone`, `has_image`,
`set_button_image` or a widget row's `layout` member.

### `runAnyoneFieldsConformance.ts` (#1163, 19 tests): CU-C4

1. the fact reads from a field's `/EMBD_Metadata`, never with a group or on a signature field
2. every token that may fill in some field fills one in, and no other
3. a token that may fill in no field is refused, naming the capability
4. a group's token resets, imports values into and undoes its fill of such a field
5. Dana fills one in, corrects it and signs: it locks at her value; an empty one stays open
6. a required one is every signer's to fill in, and locks once it holds a value
7. an image field is every signer's to fill in, and locks once it holds a picture (needs CU-C5's `setButtonImage`)
8. the sender's value locks at the first signature
9. a signature field in no group locks the filled ones, and with none filled locks nothing
10. a signing that names its own lock decides for itself
11. a signature field's own lock decides what its signing locks
12. a field in a group, or a signature field, is never for anyone
13. marking one takes `fields:set-group:all`; unmarking takes form design
14. an undo puts the fact back, also after a later change to the field
15. an undo leaves a newer assignment as it is and puts the rest back
16. an update that expects the fact applies only where it holds
17. a copy keeps the fact, which takes the right to mark it
18. an export carries the fact, and an import keeps it under the right rights
19. after a signature, filling one it left open is permitted; changing one it locked is forbidden

### `runPictureButtonConformance.ts` (#1165, 21 tests, one of them run per format): CU-C5

1. a widget reads its layout from the file: all seven, the default, and one past them
2. a push button reads whether it holds a picture; another kind has no such fact
3. another app's icon shows, and exports as a one-page PDF
4. a create and a widget update write any layout, and a save keeps it
5. a layout goes on a push button only, and a push button always has one
6. a `${format}` becomes the picture of every widget of the field (PNG, JPEG, PDF)
7. null takes the picture out, and a field with none is left as it is
8. each widget shows the picture as its layout says, which stays as it is
9. the picture is stored once, however many buttons, signature fields and stamps show it
10. it takes a push button, and a PNG, a JPEG or a one-page PDF
11. it is one change: an undo takes the picture out, and its undo puts it back
12. it is filling in: the fill right for the field's group
13. a field a signature locked takes no picture
14. a save keeps the picture, and the file reads it again
15. a place added later shows the field's picture, and the file holds it once
16. a place added without a layout keeps the picture and shows its caption, as the layout says
17. undoing the added place takes it away, and the picture stays in the others
18. a field and widget in one object split for a new place, and both show the picture
19. a copy keeps the picture with its value, and its layout always
20. an export carries the picture once, and an import places it
21. a field the form doesn't export goes without its picture, as without a value

### Changed runners

| Runner | New or changed tests | Rust port today | Job |
|---|---|---|---|
| `runFormAttributionConformance.ts` (#1162) | added "a fill records nobody, named or not, and neither does a reset", "a named fill after a signature leaves it permitted, and so does a reset", "a mark drawn in and then signed leaves an earlier signature permitted"; removed "a fill names who filled the value in…", "a reset, and an anonymous fill, name nobody", "a script's value counts as filled in by the user whose fill ran it", "undoing a fill puts the previous filler back" | `crates/epdf-engine/tests/form_reader.rs` ports the removed ones and still passes: `crates/epdf-engine/tests/form_reader.rs:512-514` reads `filledBy` `u-42` from the file, where upstream now reads `null` whatever the file holds | CU-C4 |
| `runFormTransferConformance.ts` (#1162) | "restore writes who created the source's fields and the importer…", "restoring a design needs `doc.forms.import`…, and a values import what a fill needs", "a values import is undone and redone: the values back" (renamed, behaviour changed) | `crates/epdf-engine/tests/form_transfer.rs` | CU-C4 |
| `runSignatureMarkConformance.ts` (#1162) | added "a drawing saved into a field that isn't signed keeps an earlier signature; over the signed field it does not" | `crates/epdf-engine/tests/signature_mark.rs` | CU-C4 |
| `runFormPredictionConformance.ts` (#1163) | 9 new rows: "text anyone fills in", "text not for anyone by name", "update: for anyone", "update: from a group to anyone", "update: from anyone to a group", "update: forAnyone, and something else", "forAnyone on a field in a group", "a group for a field anyone fills in", "a signature field for anyone" | none: no Rust port names the runner (only `tooling/plugin-conformance/historical/task-12b2c2/upstream-form-sink.out`) | CU-C4 |
| `runChangeConformance.ts` (#1165) | `forms.setButtonImage` in the op table (set, and `null` takes it out) over a `PICTURE_BUTTON` setup | `crates/epdf-engine/tests/document_apply.rs`, `page_copy.rs` | CU-C5 |

### Engine unit tests the range added (TS only, for the Rust tests to mirror)

| File | Tests | Job |
|---|---|---|
| `packages/engine/core/test/signature/analysis.test.ts` | 7 for the `signature-appearance` rule (corpus v4/93 to 102), 6 for a push button's image under a signature | CU-C4 (rule), CU-C5 (image); Rust has `crates/epdf-engine/src/features/signature/analysis/tests.rs` |
| `packages/engine/core/test/auth/scope/fields.test.ts` | 5 for `forAnyone` in field rights | CU-C4 |
| `packages/engine/core/test/forms/anyone.test.ts` | 3 (`checkedForAnyone`, `withForAnyone`) | CU-C4 |
| `packages/engine/core/test/forms/schema.test.ts` | 2 (`forAnyone` in draft, patch and an older server's row) | CU-C4 |
| `packages/engine/core/test/transfer/formTransfer.test.ts` | "a file from before fields anyone fills in reads `forAnyone: false`…" (D-2), "a push button the form does not export goes without its picture" | CU-C4, CU-C5 |
| `packages/engine/core/test/forms/pictureButton.test.ts` | 11 (`hasImage`, `layout`, bundle completion, the wire) | CU-C5 |
| `packages/engine/main/test/picture-button-analysis.test.ts`, `shared-drawing-size.test.ts` | the analysis per level; a picture shown in ten places saved once (about 17 KB) | CU-C5 |
| `packages/engine/main/test/fixtures/signature-compat/v4/` | 11 Acrobat fixtures, `policy-expectations.json` | CU-C4 (D-3) |
