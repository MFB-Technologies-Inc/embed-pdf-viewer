# CU-C5 decisions

Decisions made while porting the forms half of upstream `c4e744345..9af4d9ee7` to the Rust crates
(Job D), on `research/catchup-2026-11-27-c5` from `f66f5e72e`.

## CU-C5-D1. The branch starts at CU-C4's tip, in a worktree and target dir of its own

Decision: `research/catchup-2026-11-27-c5` was made with `git worktree add -b` at `f66f5e72e`
(the tip of `research/catchup-2026-11-27-c4`) in `/workspaces/wt/CU-C5`. Builds use
`/workspaces/target-c5`, a `cp -a` of `/workspaces/target`; the worktree was prepped with
`/workspaces/scripts/prep.sh`. Logs are in `/workspaces/logs/CU-C5/`, scripts in
`/workspaces/scripts/cu-c5/` (committed as `scripts/cu-c5/`).
Reason: the brief names `-plugins` as the start in one paragraph and `f66f5e72e` as the
controller's ruling in the next; the ruling is the later and more specific instruction, and CU-C4
changed the field DTO this job reads (CU-C4-D11). `/workspaces/wt/CU-C2` has `-c4` checked out
and builds into `/workspaces/target`, so a target dir of its own leaves that build alone.
Cost: 20 GB more on the volume until CU-C11, and one cold build of the workspace crates.
Reverse: delete `/workspaces/target-c5` and build into `/workspaces/target`.

## CU-C5-D2. The script-right import gets an assertion that tells it apart

Decision: Job C's re-review Minor B is fixed in `form_transfer.rs` by treating `"*"` as the script
right in `Engine::may_script`, and by one assertion upstream doesn't have: after the restore with
the script right, `plain_two` reads its format script alone, keystroke `null`.
Reason: upstream's own assertion (`time_pattern` keeps its two scripts) holds with the script
right and without it, because a time format's scripts are rebuilt from the format when the right
is missing. A probe with `may_script` forced false passed the upstream assertion
(`fixB-probe-noscript.log`). `plain_two` is written with a format script only (`FIELDS` in
`runFieldPurposeFormatConformance.ts:69`), so copied as written it has no keystroke script, and
the designer import above it gets the format's own. With the old `may_script` the new assertion
fails (`fixB-red.log`).
Cost: one assertion that restates the Rust engine's answer, not one recorded from a TypeScript
run.
Reverse: delete the `plain_two` assertion.

## CU-C5-D3. Minor A has no red run

Decision: the non-picture test now draws a styled box (`signatureField(..., true)`), checks the
box drew something, and sends the bytes as a `document.apply` op too, expecting `InvalidArg`. It
passed on its first run.
Reason: the engine already refused the op (`937eb6767`); the Minor was about what the test
checks, not about behaviour.
Cost: the op leg has no failing run behind it.
Reverse: not applicable.

## CU-C5-D4. The FFI focus target keeps its one call, and FFI bounds stay the standing rect

Decision: the Rust `FocusTarget` has `takes_cursor` and a `focus` that answers whether it took
the cursor, as `FormFocusTarget` upstream. The uniffi trait `FormFocusTarget` keeps `focus(select)`
with no answer and no `takes_cursor`, so a native target always takes the cursor
(`FocusTarget::new`). `FormFillItem.bounds` and `FormWidgetHit.bounds` stay the rect the widget
stands in, as they were before the fill item gained `box`, `rotation` and `rect`. No new field of
the fill item, the hit, the groups API or the anchor API is added to `ffi.rs`.
Reason: CU-C3's wrapper table gives the platforms' exposure of this surface to CU-C8 and CU-C9;
changing the foreign trait here would break the Swift, Kotlin and .NET targets before those jobs
add the two members. `bounds` was the rect before (`pageWidgetsOf` put `row.rect` in `box`), so
keeping the rect keeps its meaning for an unturned and a turned widget alike.
Cost: until CU-C8, a native signature field with nothing to sign it is not passed over by Tab,
and native hosts see neither the frame nor the turn.
Reverse: CU-C8 adds `takes_cursor() -> bool` and a `bool` answer to the trait, and the new record
fields.

## CU-C5-D5. Three pieces of #1118 and #1141 wait for CU-C6's annotation half

Decision: three upstream changes are left for CU-C6, which owns `epdf-annotation-core` and
`epdf-plugin-annotation`. The lender's `accentOf` (`write/lending.ts`), with the lender's records
memo keyed on the groups and the tint: the Rust `AnnotationLender` has no such member yet. The
drag preview's display rotation (`previewPlacement(..., displayRotation)` in `tools/handlers.ts`):
the Rust `preview_placement` takes none yet, so the preview of a drag on a turned page is drawn
upright while the commit is placed upright. And `standingRect` (`core/annotation/src/rect.ts`):
`tools/handlers.rs` has a private `standing_rect` over the annotation core's
`transposed_about_center` and `rotated_aabb` until the core has it.
Reason: the work order routes #1118's and #1141's annotation halves to CU-C6 and says the job that
lands second takes the other's `RecordAccent` type; the form side needs only `{ color, strength }`
as JSON, which the fill item already carries.
Cost: until CU-C6, lent widgets show no group accent in design mode, and a drag on a turned page
previews the wrong shape.
Reverse: CU-C6 adds `accent_of` to the lender and wires it from `group_look_of`, passes the
gesture's display rotation to `preview_placement`, and replaces the private `standing_rect`.

## CU-C5-D6. The new plugin cases are a vector file of their own

Decision: the cases for the groups, the design group, radio names, anchors and the cursor are
`plugins/form-groups.json` (10 scenarios, `src/suites/form-groups.ts`), replayed by
`groups_and_controls_match_the_typescript`. The 40 date cases go into `core/acrojs.json`, which
already holds `core/acrojs`'s function cases.
Reason: the existing form files pin counts per file (`every_form_case_replays`), and a file per
surface keeps a failing replay pointing at the upstream PRs that brought it. One scenario adds
the Stage (the box asked for before its page comes in), which the other form files don't build.
Cost: one more file for `every_form_case_replays` to pin (7 files, 240 cases).
Reverse: delete the suite and the vector, and the test.

## CU-C5-D7. `formatDate` and `parseDate` are transcribed, not run

Decision: `epdf-acrojs` gains `dates.rs`, `format_date` and `parse_date`, transcribed from the
prelude's `printd` and `parseDateFrom` over ECMAScript date arithmetic (`MakeDay`, `TimeClip`,
`Date.UTC`'s 1900 rule for a year from 0 to 99).
Reason: upstream's `dates.ts` installs the prelude into an object of its own and calls into it;
this crate ships the prelude as data and has no JavaScript engine (`lib.rs`). The 40 recorded cases
pin the transcription to the TypeScript.
Cost: two copies of the date rules, the prelude's and Rust's; a prelude change that moves them
shows up only as a failing date case if a case reaches it.
Reverse: delete `dates.rs` and run the prelude in a host sandbox instead.

## CU-C5-D8. Red runs share the job's target dir

Decision: `scripts/cu-c5/red.sh` builds an earlier commit in a temporary worktree with
`CARGO_TARGET_DIR=/workspaces/target-c5`, then touches the job's sources, as CU-C4-D10 does.
Reason: the same as CU-C4-D10: a separate target dir means a full rebuild of every dependency.
Cost: the job's next build after a red run rebuilds the workspace crates.
Reverse: give the red worktree a target dir of its own.
