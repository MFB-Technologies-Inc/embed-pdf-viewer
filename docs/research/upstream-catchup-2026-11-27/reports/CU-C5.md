# CU-C5: the forms port

Every item CU-C3's work order gives CU-C5 is ported. The 12 failing tests it routed here pass:
7 in `epdf-plugin-form`, 3 in `epdf-plugin-signature` and the 2 in `epdf-plugin-stamp` that only
differed by the form plugin's state, and with them all 167 replayed cases in the nine vector files.
Two new sets of recorded cases cover what upstream added and no vector reached: 10 scenarios in
`plugins/form-groups.json` and 40 date cases in `core/acrojs.json`, every one red on the code before
it. `plugin/form`'s uncovered lines that the range added go from 236 to 17, `core/acrojs`'s from 21 to
0. The three Minors from Job C's re-review are the first commit. Decisions are in
[`../decisions/CU-C5.md`](../decisions/CU-C5.md).

## Commits

On `research/catchup-2026-11-27-c5` from `f66f5e72e` (CU-C5-D1).

| Commit | Item | Summary |
|---|---|---|
| `a2003f27b` | Minors A, B, C | The mark is sniffed before its widget is found and opened after; the non-picture test draws a styled box and sends an op too; `"*"` is the script right in `form_transfer.rs` |
| `54d2cba66` | 6 | `setAppearance(field, mark \| null)`, `sign` and `prepareSignature` send the mark's bytes; `blank_page` removed |
| `f7121c0df` | 1, 2, 3, 5 | Widget place (`box`, `rotation`, `rect`); `designGroup` and `groups` in the state, `listGroups`, `setGroups`, `setDesignGroup`, `canSetGroup` and their events; fill item `ref`, `groupId`, `canFill`, `required`, `empty`, `accent`, radio `inGroup` and labels; `widgetAt` by rect; upright tools; predictions without a session group; purpose and format in predictions |
| `46be230a5` | 1, 4 | `moveToGroup`, group checks on `create` and `update`, `listSelectedFields`, the group setting, `fields.tint`; `getWidgetAnchor`, `isWidgetFocused`; the cursor over controls that take none; Tab from a radio group's stop |
| `9a1ea72f5` | 1, 2, 4 | `plugins/form-groups.json` (10 scenarios), its suite and its replay |
| `7047385c5` | 5 | `epdf-acrojs` `format_date` and `parse_date` (CU-C5-D7) |
| `945d60531` | 5 | 40 date cases in `core/acrojs.json` and their harness functions |
| `99537e2ca` | 2 | FFI `bounds` stay the standing rect (CU-C5-D4) |
| `57e341d15` | | `coverage.json` regenerated |
| `78299da65` | 5 | `acrojs-current.test.ts` pins 115 pure cases; `source-evidence.json` pins `suites/acrojs.ts` anew |
| `7b56fe383` | | A clippy fix in `read/settings.rs` |
| the docs commit | | This report, the decisions, `scripts/cu-c5/` |

## The three Minors

A. `crates/epdf-engine/tests/signature_mark.rs`: `signature_field` takes `styled` as upstream's
`signatureField(..., styled)` does (`#1a1a1a` border, `#eeeeee` fill); the non-picture test styles
its box, checks the box drew something, and sends the bytes as a `document.apply` op as well,
expecting `InvalidArg` (`runSignatureMarkConformance.ts:247-252`). It passed on its first run, since
the engine already refused the op (CU-C5-D3).

B. `crates/epdf-engine/tests/form_transfer.rs`: `Engine::may_script` takes `"*"` as the script right.
The upstream assertion still passes without the right, so one more assertion tells the two apart:
`plain_two`, written with a format script alone, keeps no keystroke script when copied as written.
With the old `may_script` it fails at `form_transfer.rs:976` (`fixB-red.log`); now it passes
(`fixB-green.log`, 18 of 18). CU-C5-D2.

C. `crates/epdf-engine/src/features/signature/mutator.rs`: `mark_format` sniffs (before the widget's
page is looked for), `mark_document` opens (after), as `markFormat` and `withAppearanceDocument`
sit around `withWidgetAnnotation` in `appearance.ts:39-79`. The new unit test
`a_malformed_pdf_mark_on_a_widget_with_no_page_is_not_found` failed with `MalformedPdf` on the old
code (`fixC-red.log`) and passes now (`fixC-green.log`). The annotation-not-found order inside
`Document::bake_widget_appearance` is unchanged; the controller deferred it to the final review.

## Items

1. Named groups and the design group (#1141): done (`f7121c0df`, `46be230a5`).
   `services/groups.rs` lists the named groups, then the groups the fields are in, each in its own
   color or one of `GROUP_COLORS` by place; `group_look_of` gives the label and the accent
   (`{ color, strength }`, none at tint 0). `write/groups.rs` has `setGroups` (each id once),
   `getDesignGroup`, `setDesignGroup` (refused for a group the session may not set). The two events
   are derived from state changes. `moveToGroup` stages one change of the fields not already in the
   group; `create` and `update` check a group the session may not set; `getFieldSettings` offers
   the group choice while the document has groups; `FORM_DEFAULTS.fields.tint` is 0.16. The
   lender's accent waits for CU-C6 (CU-C5-D5).
2. Fill items and hits (#1118, #1141, #1136): done (`f7121c0df`). `WidgetPlace` (`placeOf`) is in
   page widgets, shown widgets, hits and fill items; `widgetAt` hits by `rect`; `projectWidget`
   takes `canFill`, `inGroup` and the group look; radio labels name their option and owner;
   `services/tab_order.rs` gives each radio button its place in Tab's order. The tools are
   `upright` and place a widget as its `box` and turn (`placed_on_page`), in the design group.
3. Prediction without a session group (#1140): done (`f7121c0df`). `predict_form_op` and
   `undo_look_of` take no context; `FormPredictor` is gone; a draft's `groupId` or none.
   `every_form_case_replays` pins 104 for `form-core.json`, and 10 for the new file.
4. Control attach and focus (#1138, #1135, #1146, #1149): done (`46be230a5`). `getWidgetAnchor`
   (the lent anchor in design mode), `isWidgetFocused`; `FocusTarget` with `takes_cursor` and a
   `focus` that answers; `go_to` returns `Taken`, `Declined` or `Unreachable` and gives a box that
   came in holding nothing a turn before sending the cursor `onward`; `focus` tries the field's
   stops in order; `step` goes on from a radio group's stop and asks each stop once. #1135 changed
   framework UI only. The FFI trait is unchanged until CU-C8 (CU-C5-D4).
5. Purpose and format (#1147): done (`f7121c0df`, `7047385c5`). `engine/format.rs` transcribes
   `checkedPurpose`, the pattern checks, `formatWriteOf`, `formatScripts` and `formatsEqual`;
   `formFieldOfDraft` and `formFieldAfter` give a predicted field its purpose and its format with
   the two scripts. `epdf-acrojs` gets `format_date` and `parse_date` (CU-C5-D7).
6. The signature mark (#1145 plugin half): done (`54d2cba66`). A clear sends `null`; `sign` and
   `prepareSignature` send the bytes as `appearance`; `blank_page_pdf` and the harness's
   `blankPagePdf` are gone.

## TDD

The replays CU-C3 routed here were red at the branch start: `base-crates.log` (`a2003f27b`, no
plugin source changed) has the 12 tests failing, 167 cases in nine files. After `f7121c0df` the
form replays were down to `form-core.json` (purpose, `upright`) and `form-design.json` (the placed
`box`), `step1-conf.log`; after the tools and purpose work all six passed (`step2-conf.log`).
Signature: 12 of 47 cases and the two `behavior` tests failed before `54d2cba66`, none after
(`step3-sig.log`). The new cases: `red.sh f7121c0df 9a1ea72f5` replays `form-groups.json` against
the first form commit, and all 10 differ (`red-groups-at-f7121c0df/summary.txt`); `red.sh
9a1ea72f5 945d60531` replays the date cases without `dates.rs`, and all 40 differ
(`red-acrojs-dates/summary.txt`).

## Tests

All in container `embed-pdf-catchup1127`, worktree `/workspaces/wt/CU-C5`,
`CARGO_TARGET_DIR=/workspaces/target-c5`, measured at `57e341d15` (the evidence pins and the clippy
fix came after and were rerun on their own).

| Check | Start (`f66f5e72e`, CU-C4's record) | Now |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,592 passed, 16 failed, 2 ignored | 3,612 passed, 4 failed, 2 ignored |
| engine `:check` scripts | 36 of 36 | 36 of 36 |
| plugin vectors regenerated twice | | 128 files, identical sha256 lists, no vector diff to the tree |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | 4 of 4 | 4 of 4 |
| plugin `replay:check` | 216 cases in 13 files | 49 cases in 4 files, all CU-C6's |
| plugin evidence tests | 769 of 771 | 769 of 771, the same 2 (`ev-final/summary.txt` byte-identical to CU-C3's) |
| `cargo clippy --all-targets` on the four touched crates | | one warning left, in CU-C4's `font_fallback_named.rs:127` |
| TS `turbo run test` over `packages/**` | not rerun | not rerun: no file under `packages/` changed |

The 4 failures are CU-C3's CU-C6 list exactly: `epdf-annotation-core` `kinds_match_the_typescript`
and `update_matches_the_typescript`, `epdf-plugin-annotation` `pure_functions_match_the_typescript`
and `repository_functions_match_the_typescript`. The 49 replayed cases are `core/annotation-kinds.json`
(30), `core/annotation-update.json` (1), `plugins/annotation-pure.json` (10) and
`plugins/annotation-repository.json` (8). The first evidence run after the date cases had 767 of 771:
`acrojs-current.test.ts` pinned 75 pure cases and `actions-script-current.test.ts` the hash of
`suites/acrojs.ts`; both were re-pinned, and the rerun is 769 of 771.

## Coverage gaps

`coverage.json` now reads statements 79,252 of 83,439 and branches 15,712 of 17,268 (CU-C3: 78,920 and
15,505). `gaps.py` finds 37 uncovered lines the range added (CU-C3: 277). `plugin/form` keeps 17:
`model.ts:487`, `read/fields.ts:226-229` (a selected widget in `listSelectedFields`),
`services/cursor.ts:173`, `services/groups.ts:113`, `write/fields.ts:171-172,307-308` (an
`addWidget` beside a turned widget, a refused `moveToGroup` apply), `write/fill.ts:147,162`,
`write/focus.ts:116-117,182,199`. `core/acrojs` keeps none. The other 20 are CU-C6's
(`core/annotation` 16, `plugin/annotation` 4), and `state.ts` stays unloaded: it is framework
state with no Rust port.

## Evidence

Logs in `/workspaces/logs/CU-C5/` in the container. Scripts in [`../scripts/cu-c5/`](../scripts/cu-c5/).
sha256, first 8 hex digits.

| Check | Command | Exit | Log | sha256 |
|---|---|---|---|---|
| Start, touched crates | `t.sh base-crates -- cargo test -p epdf-plugin-form -p epdf-plugin-signature -p epdf-plugin-stamp -p epdf-form-core -p epdf-signature-core -p epdf-acrojs` | 101 (the 12) | `base-crates.log` | `b564821b` |
| Minor C | `cargo test -p epdf-engine --lib a_malformed_pdf_mark…`, then `features::signature::mutator` | 101, then 0 | `fixC-red.log`, `fixC-green.log` | `2d2332e4`, `47d2b662` |
| Minor B probe | upstream assertion with `may_script` false | 0 | `fixB-probe-noscript.log` | `8b25b4a2` |
| Minor B | `--test form_transfer` with the old `may_script`, then the fix | 101, then 0 | `fixB-red.log`, `fixB-green.log` | `d423c824`, `58319d8d` |
| Minors A to C | `cargo test -p epdf-engine --lib --test form_transfer --test signature_mark --test signature_sign` | 0 | `fixABC-all.log` | `8f3c0116` |
| Form replays | `--test conformance` after `f7121c0df`'s model work, then after the tools | 101, then 101 (count only) | `step1-conf.log`, `step2-conf.log` | `a9beaf4d`, `0c9eef0a` |
| Signature | `cargo test -p epdf-plugin-signature` | 0 | `step3-sig.log` | `f1152f9a` |
| Form crate | `cargo test -p epdf-plugin-form` | 0 | `step4-form.log` | `1a116a30` |
| New form cases, red | `red.sh f7121c0df 9a1ea72f5 groups-at-f7121c0df …` | 0 (script), 10 of 10 differ | `red-groups-at-f7121c0df/summary.txt` | `ca76199a` |
| New form cases | `--test conformance` | 0 | `groups-green.log` | `8528f9a2` |
| Date cases, red | `EXTRA=… red.sh 9a1ea72f5 945d60531 acrojs-dates …` | 0 (script), 40 of 115 differ | `red-acrojs-dates/summary.txt` | `81ced6ea` |
| Date cases | `cargo test -p epdf-acrojs --test conformance` | 0 | `acrojs-dates.log` | `4f9f4077` |
| Generator typecheck | `pnpm -s run typecheck` | 0 | `typecheck2.log` (empty) | `e3b0c442` |
| Plugin regeneration x2, coverage | `regen-plugins2x.sh … regen1` | 0 | `regen1/determinism.log`, `regen1/plugregen-run{1,2}.sha`, `regen1/coverage-write.log` | `a551d0a7`; both `3cdcd8e2`; `5e0e6162` |
| Coverage gaps | `gaps.py /workspaces/wt/CU-C5` | 0 | `coverage-gaps.txt` | `e6c40be8` |
| After suite | `heavy-build.sh CU-C5 -- docker exec … after.sh /workspaces/wt/CU-C5 after1` | 0 (script) | `after1/cargo.log`, `after1/cargo.summary` | `87da7465`, `bfcc03ff` |
| Engine `:check` | `suite.sh … engconf` | 0 | `after1/engconf.all` | `27a10bbd` |
| Plugin checks | `suite.sh … plugconf`, `replay:check` | 0, 1 (CU-C6's 49) | `after1/plugconf.all`, `after1/plugconf.replay:check.log` | `09dd3f6f`, `51d99f85` |
| Failures by crate and case | `cargo-by-crate.py`, `replay-cases.py` | 0 | `after1/cargo-by-crate.tsv`, `after1/replay-cases.tsv` | `cca3fc3e`, `6fab0e40` |
| Evidence | evidence loop after the re-pin | 0 | `ev-final/summary.txt` | `6290cd61` |
| Clippy | `cargo clippy -p epdf-plugin-form -p epdf-plugin-signature -p epdf-acrojs -p epdf-engine --all-targets` | 0 | `clippy.log` | `f4e8f4a8` |

## For CU-C6, CU-C8 and CU-C9

CU-C6: wire the lender's `accentOf` from `group_look_of` and key its records memo on
`list_groups()` and the tint; give `preview_placement` the display rotation and pass
`Gesture::display_rotation` from `tools/handlers.rs`; replace the private `standing_rect` there with
the annotation core's (CU-C5-D5). `RecordAccent` on the form side is plain `{ color, strength }`
JSON in fill items.

CU-C8 and CU-C9: the Rust surface is `Core::list_groups`, `set_groups`, `get_design_group`,
`set_design_group`, `can_set_group`, `move_to_group`, `list_selected_fields`,
`on_design_group_changed`, `on_groups_changed`, `get_widget_anchor`, `is_widget_focused`; fill items
carry `ref`, `box`, `rotation`, `rect`, `groupId`, `canFill`, `required`, `empty`, `accent`, and a
radio's `inGroup`; hits carry `box`, `rotation`, `rect`. None of it is in `ffi.rs` yet, and the uniffi
`FormFocusTarget` needs `takes_cursor` and a `bool` answer from `focus` (CU-C5-D4). The signature
plugin's FFI needs no change: a mark is bytes, and `clearField` sends `null` inside the plugin.
`epdf_acrojs::format_date` and `parse_date` are new public Rust API with no wrapper.

## Known gaps

- The 17 uncovered `plugin/form` lines above.
- Lent widgets show no accent and a drag on a turned page previews upright until CU-C6
  (CU-C5-D5); native focus targets always take the cursor until CU-C8 (CU-C5-D4).
- The annotation-not-found order inside `Document::bake_widget_appearance` is unchanged (deferred
  by the controller to the final review).
- `rust-coverage:check` was not run, as in CU-C3 and CU-C4.
- `runtime-src` names `9472117ed`, only in the local runtime fork until CU-C10 (D-3).
