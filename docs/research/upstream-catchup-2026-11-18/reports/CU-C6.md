# CU-C6: annotation draft slot, stamps, and the plugin side of signing

`research/catchup-2026-11-18-c6` ports CU-C3's work order section CU-C6 (#1096, #1039, #1087,
#1088, #1102) onto `16278b3e0`. All four items are done. The 6 replayed cases that failed at the
base pass, `cargo test --workspace` has no failure, and `replay:check` passes over every file. This
is the last Rust port job of the catch-up.
Decisions are in [`../decisions/CU-C6.md`](../decisions/CU-C6.md).

## Commits

| Commit | Item | Summary |
|---|---|---|
| `8446e11b5` | 1 | `drops_waiting_ink`; the pen drawing's wait moves to `write/drafts.rs`; `has_creation_awaiting_click` |
| `417dc903e` | 2 | `ImportLibraryOptions.id` |
| `7db5dd5bd` | 3 | Signature settings and per-call authority, `write/validation_info.rs`, the completion gate, `canFill` |
| `be7546d09` | 4 | `has_area`, `PredictionContext` and `FormPredictor` |
| `02570520c` | | `coverage.json`, the job's scripts |
| `725b9ecc5` | | This report and the decisions |
| `d01c8fb86` | 3 | Fix round 1: three signature cases over the validation-data success path |
| `ef5958b72` | | Fix round 1: lines the formatter reflowed in three suites put back |
| `f29e43828` | 1 | Fix round 1: a throwing finish of the waiting drawing stops the replacing message |
| the round-1 docs commit | | `coverage.json`, this report, the decisions |

## Items

1. Done (`8446e11b5`), after `6b424edce` (`d0601e65e`, `a41ab182c`). `epdf-annotation-core` has
   `update::drops_waiting_ink` (CU-C6-D1). In `epdf-plugin-annotation` the store finishes a waiting
   drawing before a message that would take its place and runs the message again
   (`services/store.rs`); `write/drafts.rs` holds the wait (`wait_for_next_stroke`,
   `finish_waiting_ink`), a download and a tool change finish it (CU-C6-D5), and the draw handler
   lost its grouping window. A finish that throws goes to the caller, so the store does not run
   the replacing message (fix round 1, `f29e43828`). The edit handler declines a press, and shows no cursor or hover,
   while `has_creation_awaiting_click()` holds. The two failing cases `ink-strokes-group` and
   `ink-cancel-inside-the-grouping-window` pass. New cases: `core/annotation-update.json`
   `dropsWaitingInk` (3), `plugins/annotation-tools.json`
   `ink-press-on-an-annotation-keeps-the-waiting-stroke` and `shape-clicks-over-an-annotation`,
   `plugins/annotation-api.json` `creation-awaiting-click`, and the distance case now asks
   `hasCreationAwaitingClick` twice.
2. Done (`417dc903e`), after `a97140dd0`. `ImportLibraryOptions.id` is used over the file's id; a
   taken id gets a fresh one. Case `plugins/stamp-api.json` `import-under-the-callers-id`.
3. Done (`7db5dd5bd`), after `3d167f772`, `32571b4b7`, `b08c0075d` (`7bd135d7f`, `1c165508c`,
   `a425da7d3`, `ceeb36bae`). Settings `timestamp_authority`, `revocation`, `document_timestamp`
   (CU-C6-D2); per-call `timestamp_authority` on `SignFieldInput` and `PrepareSignatureInput`;
   `prepare_signature` reserves 16384 bytes for a token; `complete_signature` runs
   `epdf_signature_core::complete_signature`, so a refused CMS leaves the signing prepared; `sign`
   and `complete_signature` resolve `SignFieldResult` JSON (CU-C6-D3); `add_validation_info`,
   `can_add_validation_info`; `validate` defaults to `Requested::Earliest`; `can_fill`,
   `fill_field` and `clear_field` ask `sign`; `signatures.validationInfoAdded` re-judges. The 4
   failing cases pass. New cases in `plugins/signature.json`: `validation-info-needs-its-settings`,
   `validation-info-requests`, `validation-info-document-timestamp-needs-an-authority`,
   `sign-then-validation-info`, `prepare-with-a-timestamp-authority`, and in fix round 1
   `validation-info-added`, `validation-info-and-a-failed-document-timestamp`,
   `sign-then-validation-info-and-a-failed-document-timestamp`, over the long-term authority
   `signature-core.ts` builds (exported as `LONG_TERM`). Two FFI tests changed with the
   behaviour (CU-C6-D4).
4. Done (`be7546d09`), after `133030b8a` and `134f24b77`. `has_area`; `shown_on_screen` needs an
   area and the lender lends only widgets with one. `PredictionContext { group_id }` and
   `FormPredictor` (the session identity's `groupId`); every prediction in the plugin goes through
   it, and `FieldDraftContext.group_id` fills a draft without a group. Six cases in
   `plugins/form-core.json`; the pinned form case count in `tests/conformance.rs` is 231.

## Rust API for CU-C8 and CU-C9

Each item below exists in Rust and has no uniffi surface yet unless it says so (CU-C6-D6).

- `epdf_plugin_signature::SignatureSettings` and `SignatureConfig`: `timestamp_authority:
  Option<CmsSigner>`, `revocation: Option<Rc<dyn RevocationSource>>`, `document_timestamp: bool`
  (`SignatureConfig` wraps each in `Option` for "not given"). No uniffi field: `ffi/mod.rs`
  `SignatureConfig` and `ffi/settings.rs` carry only `mode` and `allow_certify`.
- `SignFieldInput.timestamp_authority` and `PrepareSignatureInput.timestamp_authority`:
  `Option<Option<CmsSigner>>`, `Some(None)` for none on this call. No uniffi field: the uniffi
  inputs set `None`.
- `SignatureCapability::add_validation_info(field: Option<Value>, OperationOptions) ->
  Promise<Value>` (`{ added, documentTimestamp }`) and `can_add_validation_info() -> bool`. No
  uniffi method.
- The `validationInfo` and `validationInfoError` members of the `sign`, `complete_signature` and
  `place_mark` `signed` result JSON. The uniffi `SignatureCompleteResult` drops them.
- `SignatureValidateOptions.validation_time: Option<Requested>`; `None` and `Requested::Earliest`
  judge at the earliest trusted timestamp, `Requested::At(ValidationTime::Now)` as of now. The
  uniffi `SignatureValidateRequest.validation_time` `None` now means the earliest timestamp, and
  it has no way to ask for `earliest-timestamp` by name.
- The verdict's `revocation`, timestamp and `longTerm` members (signature-core, CU-C4b) are in the
  verdict JSON; the uniffi `SignatureVerdict` (`ffi/records.rs`) has none of them.
- `epdf_plugin_stamp::ImportLibraryOptions.id: Option<String>`; on uniffi as
  `StampImportOptions.id`, the only item here that is.
- `epdf_plugin_annotation` `has_creation_awaiting_click() -> bool` on `AnnotationCapability`, new
  on upstream's public `AnnotationHostCapability`; `ffi/drafts.rs` does not export it.

## Tests

All in container `embed-pdf-catchup1118`, worktree `/workspaces/wt/CU-C6`,
`CARGO_TARGET_DIR=/workspaces/target`. The base column is CU-C5's final run at `16278b3e0`.

| Check | Base (`16278b3e0`) | Now (`f29e43828`, fix round 1) |
|---|---|---|
| `cargo test --workspace --no-fail-fast` | 3,509 passed, 2 failed, 2 ignored | 3,511 passed, 0 failed, 2 ignored |
| signature, stamp and form crates with `--features uniffi` | | all pass |
| engine `:check` scripts | 36 of 36 | 36 of 36 |
| plugin vectors twice | identical | identical sha256 lists |
| plugin `vectors:check`, `coverage:check`, `test:harness`, `typecheck` | all pass | all pass |
| plugin `replay:check` | 6 cases in 2 files | none: 123 files, 16,005 cases |
| TS `turbo run test` over `packages/**` | exit 0 | exit 0 |
| uncovered added lines (`gaps.py`), whole range | 284 in 26 files | 195 in 23 files (219 in 25 before fix round 1) |

Uncovered lines left in the files this job touched:
`packages/plugin/signature/src/write/validation-info.ts`: none (85 at the base, 23 before fix
round 1, whose three cases reach the success path).
`packages/plugin/form/src/write/fill.ts` 152-156, 171-173 (8, unchanged from the base): paths
that existed before the range, which `134f24b77` only renamed (`undoLookOf` to
`predictor.undoLook`), so `gaps.py` counts them as added; no case reaches them before or after. `authority.ts` 63-65 is covered now.

## Divergences

No behaviour divergence is known. The port identity rule of CU-C6-D2 extends the existing
`SigningKey::same` divergence (deep compare upstream, identity here) to the two new ports; no
vector observes it.

## Evidence

Base `16278b3e0`. Logs in `/workspaces/logs/CU-C6/` and `tmp/cu1118/logs/CU-C6/` of the host viewer
checkout. Scripts in [`../scripts/cu-c6/`](../scripts/cu-c6/): `prep.sh` (CU-C5's worktree payload
copied in), `after.sh`, `env.sh`. The first full run is in `round1/`. Hashes are sha256, first 8 hex digits.

| Check | Log | sha256 |
|---|---|---|
| Suite | `after/cargo.log` | `69af4838` |
| `replay:check` | `after/plugconf.replay:check.log` | `510256e4` |
| TS tests | `after/ts.log` | `ae87a8d9` |
| Vectors run 1 hashes (run 2 identical) | `plugvec-run1.sha` | `4b63f9c7` |
| Gaps before and after | `gaps-base.txt`, `gaps-after.txt` | `31c0dde6`, `d5313a12` |
