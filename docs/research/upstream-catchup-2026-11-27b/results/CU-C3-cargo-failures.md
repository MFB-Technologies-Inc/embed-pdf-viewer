# CU-C3 results: failing `cargo test` by crate, cause and job

`cargo test --workspace --no-fail-fast` at `fc22e1e80` (vectors regenerated) in container
`embed-pdf-catchup1127b`, worktree `/workspaces/wt/CU-C2`: 3,611 passed, 19 failed, 2 ignored. At
`b2ebe03e2` it was 3,630, 0, 2. No Rust changed in between; every failure is a vector the
regeneration moved. Log: `/workspaces/logs/CU-C3/after/cargo.log`, table
`after/cargo-by-crate.tsv`.

The causes come from the vector diffs (`/workspaces/logs/CU-C3/eng-diffpaths.log`,
`plug-diffpaths.log`), which show only four kinds of change: a new row member (`forAnyone`,
`hasImage`, `layout`), `policyVersion` 5 to 6, `forms.setButtonImage` at `3.5` in
`changes/kinds.json`, and a signature mark that takes two fewer objects. A replay stops at the
first difference in each case, so a test listed for one cause may also hold the next one.

Job routing follows CU-C3-D2 and CU-C3-D3: CU-C4 reads all three new row members, CU-C5 owns the
change kind and the mark's shared drawing.

## epdf-engine (16)

| Test | Assertion | First difference | Cause | PR | Job |
|---|---|---|---|---|---|
| `signature_protection::protection_agrees_with_typescript` | `crates/epdf-engine/tests/signature_protection.rs:26` | `policyVersion` 5, expected 6 (case `empty`) | `SIGNATURE_POLICY_VERSION` (`crates/epdf-engine/src/features/signature/protection.rs:20`) | #1161 | CU-C4 |
| `signature_ltv::the_long_term_files_read_and_judge_as_the_typescript_does` | `crates/epdf-engine/tests/signature_ltv.rs:438` | 22 of 22 cases: `policyVersion` 5, expected 6 | same | #1161 | CU-C4 |
| `signature_ltv::validation_data_is_read_and_stored_as_the_typescript_does` | `crates/epdf-engine/tests/signature_ltv.rs:438` | 3 of 10 cases: `policyVersion` | same | #1161 | CU-C4 |
| `signature_ltv::a_document_timestamp_gets_a_field_of_its_own` | `crates/epdf-engine/tests/signature_ltv.rs:438` | 1 of 7 cases: `policyVersion` | same | #1161 | CU-C4 |
| `form_reader::the_snapshot_agrees_with_the_typescript_engine` | `crates/epdf-engine/tests/form_reader.rs:159` | `families`, field 0: `forAnyone` missing | snapshot rows gain `forAnyone`, push buttons `hasImage`, widgets `layout` (`forms/snapshot.json`) | #1163, #1165 | CU-C4 |
| `form_write::a_written_field_reads_back_as_the_typescript_engines_did` | `crates/epdf-engine/tests/form_write.rs:133` | `text-value`: field row | `forAnyone` on 12 of 18 cases' rows; `policyVersion` on all 18 requests | #1163, #1161 | CU-C4 |
| `form_field::a_structural_write_leaves_what_the_typescript_engine_left` | `crates/epdf-engine/tests/form_field.rs:126` | `create-text-bare`: field row | `forAnyone` on 14 of 23 cases; `policyVersion` on 19 | #1163, #1161 | CU-C4 |
| `form_apply::every_change_agrees_with_the_typescript_engine` | `crates/epdf-engine/tests/form_apply.rs:77` | `single-value-applies`: items | `forAnyone` on field rows, `layout` on widget rows (12 of 23 cases) | #1163, #1165 | CU-C4 |
| `annotation_reader::the_reads_agree_with_the_typescript_engine` | `crates/epdf-engine/tests/annotation_reader.rs:132` | `kinds`, annotation 37 (a widget) | widget annotation rows gain `layout` (`annotation/read.json`, 3 of 6 cases) | #1165 | CU-C4 (D2) |
| `change_ops::a_fields_purpose_and_format` | `crates/epdf-engine/tests/change_ops.rs:331` | 25 of 25: `/fields/N/forAnyone` missing | `forAnyone`, `layout`, `hasImage`; `policyVersion` on 23 | #1163, #1165, #1161 | CU-C4 |
| `change_ops::a_new_field_is_in_no_group_unless_named` | `crates/epdf-engine/tests/change_ops.rs:331` | 9 of 9: `/result/field/forAnyone` | `forAnyone`, `layout`; `policyVersion` | #1163, #1165, #1161 | CU-C4 |
| `change_ops::forms_copy` | `crates/epdf-engine/tests/change_ops.rs:331` | 7 of 9: `/result/items/0/fields/0/forAnyone` | `forAnyone`, `layout`, `hasImage`; `policyVersion` | #1163, #1165, #1161 | CU-C4 |
| `change_ops::a_widget_update_renames` | `crates/epdf-engine/tests/change_ops.rs:331` | 2 of 3: `/result/items/0/widget/layout` | `layout`; `policyVersion` | #1165, #1161 | CU-C4 |
| `change_ops::a_signature_mark_is_an_image_or_a_pdf_or_none` | `crates/epdf-engine/tests/change_ops.rs:331` | 8 of 8: `/result/field/forAnyone` | `forAnyone`, `layout`, `policyVersion`; then in `a-png-a-jpeg-and-a-pdf-fill-a-signature-field` the field and widget are objects 78 and 79, were 80 and 81 | #1163, #1165 (`7d5691a5a`, mark through `drawingFor`), #1161 | CU-C4, the object numbers CU-C5 (D3) |
| `change_ops::a_mark_turns_with_its_widget` | `crates/epdf-engine/tests/change_ops.rs:331` | 2 of 2: `/result/field/forAnyone` | as above; `a-turn-turns-the-mark-and-undo-puts-it-back` objects 84 and 85, were 86 and 87 | as above | CU-C4, the object numbers CU-C5 (D3) |
| `change_ops::every_change_kind_has_the_version_it_arrived_in` | `crates/epdf-engine/tests/change_ops.rs:414` | "the change kinds moved" | `forms.setButtonImage` at `3.5`; `API_VERSION` 3.4 to 3.5 (`crates/epdf-engine/src/features/changes/kinds.rs:13`) | #1165, #1163 | CU-C5 (CU-C4 moves `API_VERSION` with `ANYONE_FIELDS_SINCE`) |

## epdf-plugin-form (2)

| Test | Assertion | Cases | Cause | Job |
|---|---|---|---|---|
| `conformance::core_functions_match_the_typescript` | `crates/plugin-conformance/src/lib.rs:356` | `plugins/form-core.json` 3 of 104: `update/create-without-a-group`, `update/create-names-its-group`, `update/copy-through-create`, each `$.returned.snapshot.fields[14].forAnyone` missing | the plugin's own field row for a create or a copy (`crates/plugins/form/src/engine.rs:1206` writes `purpose`, not `forAnyone`) | CU-C4 (#1163) |
| `conformance::design_matches_the_typescript` | `crates/plugin-conformance/src/lib.rs:356` | `plugins/form-design.json` 1 of 33: `duplicate-fields-and-widgets` step 3, `$.outcome.returned[2].forAnyone` missing | same | CU-C4 (#1163) |

## epdf-plugin-annotation (1)

| Test | Assertion | Cases | Cause | Job |
|---|---|---|---|---|
| `conformance::repository_functions_match_the_typescript` | `crates/plugin-conformance/src/lib.rs:356` | `plugins/annotation-repository.json` 7 of 690: `draftOf/toCreateDraft/widget-{text,combobox,listbox,pushbutton,checkbox,radio,signature}`, `$.returned.layout` missing | `layout: 'data'` in `packages/engine/core/src/annotation/kind-facts.generated.ts`; regenerate `crates/plugins/annotation-core/src/engine/kind_facts.rs` with `crates/plugins/annotation-core/scripts/kind-facts.ts` | CU-C5 (#1165) |

The 11 replayed cases above are the whole of `replay:check`'s failures ("3 replay problems",
`/workspaces/logs/CU-C3/after/plugconf.replay:check.log`; `after/replay-cases.tsv`).
