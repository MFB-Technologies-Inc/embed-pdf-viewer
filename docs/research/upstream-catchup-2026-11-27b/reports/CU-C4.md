# CU-C4: signatures and anyone fields

#1161, #1162 and #1163 are ported into the Rust core, with D-2 and D-3. `cargo test --workspace`
goes from CU-C3's 3,611 passed and 19 failed to 3,660 passed and 3 failed. The 3 are the ones the
amended done line leaves out: CU-C3-D3's two signature-mark object-number cases, and
`every_change_kind_has_the_version_it_arrived_in`, which waits for `forms.setButtonImage` (CU-C5).
All 36 engine `:check` scripts pass, and all 4 plugin checks do, `typecheck` included, as does
`replay:check` (124 files, 16,076 cases). Decisions are in
[`../decisions/CU-C4.md`](../decisions/CU-C4.md).

## Commits

On `research/catchup-2026-11-27b-plugins` from `ce0493dfa`, made in container
`embed-pdf-catchup1127b`, worktree `/workspaces/wt/CU-C2`.

| Commit | Items | Summary |
|---|---|---|
| `dad7474af` | 1, 2 | The `signature-appearance` rule, `claimSubtree`'s `admit`, policy version 6; 7 `analysis.test.ts` cases; `tests/signature_compat_v4.rs` (D-3) |
| `cf64ad060` | 7 | `forAnyone`, `hasImage` and `layout` read; `filledBy`, `filledByName`, `filledAt` read `null` (D-1); `hasMark` from the drawing; `layout` through the widget style (CU-C4-D1); five `pdfium` wrappers |
| `fee9339ec` | 3, 4 | A fill, reset or script value writes no filler; a values import needs fill rights only; a mark records no fit; `tests/form_attribution.rs`, three `form_transfer.rs` tests, one `signature_mark.rs` test |
| `db41381cb` | 5, 6, 8, 9, 11 | `forAnyone` write, rights, undo, `expect`, copy, export, import; the signing's default lock and a field's own `/Lock`; `ANYONE_FIELDS_SINCE`, `API_VERSION` 3.5; `completeFormBundle` (D-2); `changes/anyone-fields.json` and `tests/anyone_fields.rs` |
| `8d6745687` | 10 | The form plugin's predicted rows carry `forAnyone`, `hasImage` and `layout`; `kind_facts.rs` regenerated (CU-C4-D6) |
| `a806fb453` | | The 3-line `tooling/plugin-conformance` fix, plugin vectors regenerated |
| the commit that adds this file | | Report, decisions, the CU-C3 record fixes, `scripts/cu-c4/` |

## Items

1. Done (`dad7474af`). `rule_unsigned_signature_appearance` in
   `crates/epdf-engine/src/features/signature/analysis/evaluate.rs` follows `evaluate.ts`: a
   signature field unsigned before and after, whose field and widgets change `/AP` alone, with only
   new objects under the new `/AP` (`claim_subtree_admitting`), is permitted at `fill` and above.
   One that changes more than `/AP` is forbidden once per object with its keys named, the
   amendment's second behaviour (`a_key_beyond_ap_is_named_as_the_cause`). The 7 upstream tests
   are in `crates/epdf-engine/src/features/signature/analysis/tests.rs`.
2. Done (`dad7474af`, CU-C4-D5). All 11 v4 fixtures match `policy-expectations.json`. With the rule
   switched off, 6 of 11 differ (93, 94, 97, 99, 101, 102 read `forbidden`).
3. Done (`fee9339ec`). `stamp_field_fill`, `clear_field_fill` and `restore_field_fill` are gone;
   `FormMutator::set_value` takes no actor. `forms.importValues` ignores `attribution` and `actor`
   and records no `rights`, so neither it nor its undo needs `doc.forms.import`.
   `crates/epdf-engine/tests/form_reader.rs:512-514` now reads `null` from a file holding
   `/FilledBy (u-42)`.
4. Done (`fee9339ec`). `bake_widget_appearance` writes no `AppearanceFit`; `clear_widget_mark`
   checks the drawing and leaves an old `AppearanceFit` in place; a refit uses `contain`.
5. Done (`db41381cb`, CU-C4-D2, CU-C4-D3). `forms/anyone.rs` holds `checked_for_anyone` and
   `with_for_anyone`; `FieldAssignment` and `check_set_every_group` are in `auth::scope`.
   18 of the 19 runner tests have a Rust pass: 8 as 17 change-vector cases, 10 by hand in
   `crates/epdf-engine/tests/anyone_fields.rs` (test 3 among them, for its `required` detail,
   which the vectors don't record). Test 7 waits for CU-C5. The 9 prediction rows are the plugin's (item 10); the
   unit tests of `fields.test.ts` (5), `anyone.test.ts` (3) and `schema.test.ts` (the older-row
   read) are ported.
6. Done (`db41381cb`). `default_lock` in `crates/epdf-engine/src/features/signature/mutator.rs`:
   a signing with no lock locks its group and every anyone field that holds a value
   (`field_holds_value`, a push button's `hasImage` included); a timestamp adds none; a field with
   its own `/Lock` (`ResolvedSignatureField::lock`) adds nothing.
7. Done (`cf64ad060`). `read_button_has_image` opens each widget raw through a lazy
   `PageIndexLookup`; `WidgetLayout` has the seven `/MK /TP` values.
8. Done (`db41381cb`, CU-C4-D4). Closes 2026-11-27 CU-C4-D9 for parsed files.
9. Done (`db41381cb`), and `kinds.json` records `anyoneFieldsSince`.
10. Done (`8d6745687`). The four `epdf-plugin-form` cases pass; the plugin's prediction also checks
    `forAnyone` on a draft and a patch as `formFieldOfDraft` and `formFieldAfter` do.
11. Done (`db41381cb`): `anyone-fields.json` in `generate-change-vectors.ts`, replayed by
    `change_ops::a_field_anyone_fills_in`.

The controller's record fixes: `results/CU-C3-cargo-failures.md:37` cites
`crates/epdf-engine/src/features/changes/kinds.rs:13`, `scripts/prep.sh:2` no longer says
"unchanged", `decisions/CU-C3.md:16` is rewrapped, and CU-C3-D1 says the user's ruling reversed it.

## Vectors

`scripts/cu-c4/regen2x.sh` ran every engine group and the plugin vectors twice: engine sha256 lists
over 69 files identical (`a8903d2a` both runs), plugin lists over 128 files identical (`1f32794a`
both runs). The engine side changed `changes/kinds.json` and added `changes/anyone-fields.json`.
The plugin side changed 12 files, the ones whose fixtures come from `form-current-fixtures.ts`:
`core/acrojs` and `plugins/{actions-dispatcher,actions-script,form-actions,form-core-lifecycle,form-core,form-design,form-fill,form-groups,form-scripting,signature,stamp-placement}`.

## Tests

| Check | CU-C3 (`fc22e1e80`) | Now (`a806fb453`) |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,611 passed, 19 failed, 2 ignored | 3,660 passed, 3 failed, 2 ignored |
| engine `:check` scripts | 36 of 36 | 36 of 36 (`engconf.all` `27a10bbd`, unchanged) |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | 3 of 4 | 4 of 4 |
| plugin `replay:check` | 11 cases in 3 files fail | all 16,076 cases replay |

The 3 failures, all in `crates/epdf-engine/tests/change_ops.rs`: `a_signature_mark_is_an_image_or_a_pdf_or_none`
and `a_mark_turns_with_its_widget` (field and widget at objects 80 and 81, 86 and 87, where the
vectors say 78 and 79, 84 and 85; CU-C3-D3), and `every_change_kind_has_the_version_it_arrived_in`
("the change kinds moved": `forms.setButtonImage`). `epdf-plugin-annotation`'s 7 cases, CU-C5's in
CU-C3's table, pass (CU-C4-D6).

Red run (`scripts/cu-c4/red.sh ce0493dfa a806fb453 red`): the new and changed tests of
`signature_compat_v4.rs`, `signature_mark.rs`, `form_attribution.rs`, `form_transfer.rs` and
`anyone_fields.rs`, on the code from before the job, 26 pass and 18 fail. Every new test fails
except `a_token_that_may_fill_in_no_field_is_refused_naming_the_capability`, whose refusal predates
anyone fields. `change_ops.rs` and `security_scope.rs` were left out: they name the new API and
don't build on the old code.

## Evidence

Logs in `/workspaces/logs/CU-C4/` in the container. Hashes are sha256, first 8 hex digits.

| Check | Command | Exit | Log | sha256 |
|---|---|---|---|---|
| Regen x2 | `regen2x.sh /workspaces/wt/CU-C2 regen1` | 0 | `regen1/determinism.log` | `e6c42bd8` |
| After suite | `after.sh /workspaces/wt/CU-C2 after` | 0 (script) | `after/cargo.log`, `after/cargo.summary` | `90cf847b`, `e0f00905` |
| Failures by crate | (in `after.sh`) | | `after/cargo-by-crate.tsv` | `6976d104` |
| Plugin checks | (in `after.sh`) | | `after/plugconf.all` | `cf0bfd16` |
| `replay:check` | (in `after.sh`) | 0 | `after/plugconf.replay:check.log` | `9af3860b` |
| Red run | `red.sh ce0493dfa a806fb453 red` | | `red/cargo.log`, `red/summary.txt` | `eb91bd0a`, `aa926293` |

## Known gaps

- `runAnyoneFieldsConformance` test 7 (an image field) has no Rust pass until CU-C5.
- `NativeSecurity::allows_field` in `crates/epdf-viewer-host/src/native_engine.rs` now reads a
  target's `forAnyone`; no test covers it (CU-C4-D2).
- The plugin evidence tests were not rerun; CU-C3-D4 leaves their re-pin to CU-C6, and this job's
  plugin vectors moved again.
- TS `turbo run test` was not rerun: no file under `packages/` changed.
- No temporary build target was made; the red worktree was removed.
