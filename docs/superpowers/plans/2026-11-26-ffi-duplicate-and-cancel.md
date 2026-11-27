# FFI Duplicate and Cancel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `RedactionCapability.MarkMatchesAsync` stop promptly when cancelled mid-call, and
export the nine Rust annotation and form members that neither Swift nor C# has, with C# wrappers,
tests and handoff plans for the Swift and Kotlin packages.

**Architecture:** Both halves are `ffi.rs` changes over unchanged plugin behaviour. Cancellation
hangs an `AbortSignal` on the plugin call's options and aborts it from the cancel token, the way
`crates/plugins/attachment/src/ffi.rs:168-171` and
`crates/plugins/annotation/src/ffi/interchange.rs:10-20` already do. The exports follow their
neighbours: typed uniffi records, `Arc<CancelToken>` on every promise. C# regenerates the bindings
and wraps each export by the facade rules of the umbrella plan (D4).

**Tech Stack:** Rust 1.100.0, `uniffi =0.32.0`, .NET SDK 10.0.404, xUnit 2.9.3.

**Spec:** `docs/superpowers/plans/2026-11-20-dotnet-parity.md` (Global Constraints, D3, D4, the
wave D "Exclusions that need a decision" paragraph, the wave E "Finding", the Phase 6 notes) and
`platforms/dotnet/README.md` ("Known gaps").

Branch `research/dotnet-parity-p7` from `research/dotnet-parity-p6` at `b4bb2b710`, worktree
`/workspaces/wt-p7` in container `embedpdf-dotnet-parity`.

## Global Constraints

- Rust changes only in `crates/plugins/redaction/src/ffi.rs`, `crates/plugins/annotation/src/ffi/*.rs`,
  `crates/plugins/form/src/ffi.rs` and those crates' `tests/`. No plugin behaviour change.
- No TypeScript or `packages/**` change, so no changesets.
- Rust builds use `CARGO_TARGET_DIR=/workspaces/target-p7`, never the shared target.
- `cargo fmt -p <crate>` (revert unrelated reorderings), `cargo clippy -p <crate> --all-targets
  --features uniffi -- -D warnings` clean.
- Generated C# is never committed or hand-edited.
- C#: `Nullable`, `TreatWarningsAsErrors`, PublicApiAnalyzers (`PublicAPI.Unshipped.txt`), XML
  docs on every public member, `Arguments.NoNullElements` for lists, `Ffi.WellFormed` for JSON text,
  `Ffi.Defined` for enums written to Rust, conversions inside the call lambda, a
  `CancellationToken cancellationToken = default` last parameter.
- Commits by explicit path with conventional prefixes, no co-author or session trailer.
- Repo prose names no outside product, company or person, and has no em dashes.

## Review Focus

- A cancel that lands while the search itself runs, before any mark exists: the call still ends
  with `operation-cancelled` and no mark. Task 1's C# test cancels at 300 ms into a whole-document
  search, which is inside the search on `report.pdf`.
- Duplicating an empty list: `duplicate([])` resolves empty and makes no change (core behaviour);
  Task 2 pins it in Rust.
- A duplicate whose refs include one the document lacks rejects `not-found` and copies nothing;
  Task 4 pins it in C#.
- `selection_duplicate` with nothing selected resolves empty rather than failing; Task 4 pins it.
- `StageRemeasureAsync` before the annotations load answers a report with `Error` set and nothing
  updated, rather than throwing; Task 2 pins it in Rust through a handle whose list never answers.

---

### Task 1: `mark_matches` takes its cancel

**Files:**
- Modify: `crates/plugins/redaction/src/ffi.rs` (`mark_matches`, a `signalled()` helper)
- Test: `crates/plugins/redaction/tests/ffi.rs`
- Test: `platforms/dotnet/EmbedPDF.Viewer.Tests/RedactionTests.cs`
- Modify: `platforms/dotnet/EmbedPDF.Viewer/Plugins/Redaction.cs` (remove the "checked only before it
  starts" remark on `MarkMatchesAsync`, if present)

- [ ] **Step 1: Rust test** `cancelling_mark_matches_mid_call_keeps_the_marks_made`: a spec whose
  `search.query` answers 40 hits; the `apply` responder cancels a shared token on its first call.
  Assert the call fails with `operation-cancelled`, and `sent(&thread, "apply").len()` is at least
  1 and below 40.
- [ ] **Step 2:** run `cargo test -p epdf-plugin-redaction --features uniffi --test ffi`; expect the
  new test to fail (all 40 marks made, call succeeds).
- [ ] **Step 3:** in `mark_matches`, build `OperationOptions { signal: Some(signal.clone()) }` and
  return the promise `.with_abort(move |reason| signal.abort(Error::abort(reason)))`.
- [ ] **Step 4:** the Rust test passes, the rest of the file still passes.
- [ ] **Step 5: C# test** `MarkMatchesStopsWhenCancelledMidCall` replaces the pre-start remark in
  `MarkMatchesCancelsWhenTheTaskIsCancelled`: `MarkMatchesAsync(new SearchQuery("e"))` over every
  page of `report.pdf`, a `CancellationTokenSource(300 ms)`, assert `OperationCanceledException`
  within 10 s (`Stopwatch`), then `GetPendingAsync()` still answers. Runs after Task 5's rebuild.
- [ ] **Step 6:** commit `fix(plugin-redaction): abort mark_matches from its cancel token`.

### Task 2: annotation exports

**Files:**
- Modify: `crates/plugins/annotation/src/ffi/mod.rs` (module header; new records)
- Modify: `crates/plugins/annotation/src/ffi/handle.rs`, `crates/plugins/annotation/src/ffi/selection.rs`
- Test: `crates/plugins/annotation/tests/ffi.rs`

**Interfaces (produces):**

```rust
#[derive(Clone, Debug, Default, PartialEq, uniffi::Record)]
pub struct AnnotationDuplicateOptions {
    #[uniffi(default = None)] pub page: Option<PageRef>,
    #[uniffi(default = None)] pub offset: Option<Point>,
    #[uniffi(default = false)] pub select: bool,
}
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct AnnotationDuplicateResult { pub annotations: Vec<AnnotationRecord>, pub dropped_json: String }
#[derive(Clone, Copy, Debug, PartialEq, Eq, uniffi::Enum)]
pub enum AnnotationRemeasureSkipReason { NoAuthority, Locked, ForeignMeasure, Unavailable }
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct AnnotationRemeasureSkip { pub annotation_ref: AnnotationRef, pub reason: AnnotationRemeasureSkipReason }
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct AnnotationRemeasureReport {
    pub page: PageRef, pub updated: Vec<AnnotationRef>, pub skipped: Vec<AnnotationRemeasureSkip>,
    pub failed: Vec<AnnotationBatchFailure>, pub error: Option<PluginErrorInfo>,
}
// AnnotationHandle
async fn duplicate(&self, refs: Vec<AnnotationRef>, options: AnnotationDuplicateOptions, cancel: Arc<CancelToken>) -> Result<AnnotationDuplicateResult, ViewerError>;
async fn can_duplicate(&self, refs: Vec<AnnotationRef>) -> Result<bool, ViewerError>;
async fn is_movable(&self, annotation_ref: AnnotationRef) -> Result<bool, ViewerError>;
async fn when_remeasurable(&self, cancel: Arc<CancelToken>) -> Result<(), ViewerError>;
async fn stage_remeasure(&self, page: PageRef, scale_json: String) -> Result<AnnotationRemeasureReport, ViewerError>;
// AnnotationSelectionHandle
async fn duplicate(&self, offset: Option<Point>, cancel: Arc<CancelToken>) -> Result<AnnotationDuplicateResult, ViewerError>;
async fn can_duplicate(&self) -> Result<bool, ViewerError>;
```

`duplicate` and `selection.duplicate` pass the signal through `cancellable_operation`. `scale_json`
is the engine's `PdfMeasure` object; a value that isn't an object is `invalid-input`.

- [ ] **Step 1: Rust tests**, the fake engine answering `annotations.copy` with a square per source
  under the op's new object numbers:
  `duplicating_answers_the_copies_and_sends_one_copy_op` (one `apply` with one `annotations.copy`
  op naming both refs and the offset; two records with new refs);
  `duplicating_nothing_sends_nothing`;
  `can_duplicate_is_false_for_a_locked_or_missing_annotation` (unlocked `true`, `locked: true` false,
  object 77 false, `[]` false);
  `a_square_is_movable_and_a_missing_one_is_not`;
  `the_selection_duplicates_and_reports_whether_it_can` (empty selection: `can_duplicate` false and
  `duplicate` empty; selected square: true, one copy);
  `remeasuring_a_page_without_measurements_updates_nothing` (`when_remeasurable` resolves,
  `stage_remeasure` answers `updated` empty, `error` `None`);
  `remeasuring_a_line_with_a_measure_stages_its_update` (a line with `intent: "lineDimension"` and a
  rectilinear `measure`: `updated == [ref]`, the `apply` op patches `measure`);
  `a_measure_that_is_not_an_object_is_invalid_input`.
- [ ] **Step 2:** `cargo test -p epdf-plugin-annotation --features uniffi --test ffi` fails to compile.
- [ ] **Step 3:** implement the records and members; update the module header, which lists
  `whenRemeasurable` and `stageRemeasure` as not exported.
- [ ] **Step 4:** tests pass; fmt, clippy.
- [ ] **Step 5:** commit `feat(plugin-annotation): export duplicate, movability and remeasure over ffi`.

### Task 3: form exports

**Files:**
- Modify: `crates/plugins/form/src/ffi.rs`
- Test: `crates/plugins/form/tests/ffi.rs`

**Interfaces (produces):**

```rust
#[derive(Clone, Debug, Default, PartialEq, uniffi::Record)]
pub struct FormDuplicateOptions {
    #[uniffi(default = None)] pub page: Option<PageRef>,
    #[uniffi(default = None)] pub offset: Option<Point>,
    #[uniffi(default = false)] pub values: bool,
}
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormDuplicateResult { pub fields: Vec<FormField>, pub widgets: Vec<AnnotationRef>, pub widgets_json: String, pub dropped_json: String }
// FormHandle
async fn duplicate(&self, fields: Vec<FormFieldRef>, options: FormDuplicateOptions, cancel: Arc<CancelToken>) -> Result<FormDuplicateResult, ViewerError>;
async fn duplicate_widgets(&self, widgets: Vec<AnnotationRef>, options: FormDuplicateOptions, cancel: Arc<CancelToken>) -> Result<FormDuplicateResult, ViewerError>;
```

Both hang an `AbortSignal` on the call as Task 1 does.

- [ ] **Step 1: Rust tests** `duplicating_a_field_sends_one_forms_copy_op` (op `forms.copy` with
  `fields`, `offset`, `values: false`, `objectNumbers`; result typed),
  `duplicating_no_fields_sends_nothing`, `duplicating_an_unknown_widget_is_not_found`.
- [ ] **Step 2:** `cargo test -p epdf-plugin-form --features uniffi --test ffi` fails to compile.
- [ ] **Step 3:** implement; pass; fmt, clippy.
- [ ] **Step 4:** commit `feat(plugin-form): export field and widget duplication over ffi`.

### Task 4: C# members

**Files:**
- Modify: `platforms/dotnet/EmbedPDF.Viewer/Plugins/Annotation.cs`, `Annotation.Sub.cs`,
  `Annotation.Types.cs`, `Form.cs`, `Form.Types.cs`, `Form.Ffi.cs`, `PublicAPI.Unshipped.txt`
- Test: `platforms/dotnet/EmbedPDF.Viewer.Tests/AnnotationDuplicateTests.cs` (new),
  `FormTests.cs`

**Interfaces (produces):**

```csharp
public sealed record AnnotationDuplicateOptions { PageRef? Page; PdfPoint? Offset; bool Select; }
public sealed record AnnotationDuplicateResult(IReadOnlyList<AnnotationInfo> Annotations, string DroppedJson);
public enum AnnotationRemeasureSkipReason { NoAuthority, Locked, ForeignMeasure, Unavailable }
public sealed record AnnotationRemeasureSkip(AnnotationRef AnnotationRef, AnnotationRemeasureSkipReason Reason);
public sealed record AnnotationRemeasureReport(PageRef Page, IReadOnlyList<AnnotationRef> Updated, IReadOnlyList<AnnotationRemeasureSkip> Skipped, IReadOnlyList<AnnotationBatchFailure> Failed, PluginErrorInfo? Error);
// AnnotationCapability
Task<AnnotationDuplicateResult> DuplicateAsync(IEnumerable<AnnotationRef> refs, AnnotationDuplicateOptions? options = null, CancellationToken cancellationToken = default);
Task<bool> CanDuplicateAsync(IEnumerable<AnnotationRef> refs, CancellationToken cancellationToken = default);
Task<bool> IsMovableAsync(AnnotationRef annotationRef, CancellationToken cancellationToken = default);
Task WhenRemeasurableAsync(CancellationToken cancellationToken = default);
Task<AnnotationRemeasureReport> StageRemeasureAsync(PageRef page, PdfMeasurement scale, CancellationToken cancellationToken = default);
// AnnotationSelectionCapability
Task<AnnotationDuplicateResult> DuplicateAsync(PdfPoint? offset = null, CancellationToken cancellationToken = default);
Task<bool> CanDuplicateAsync(CancellationToken cancellationToken = default);
// Form
public sealed record FormDuplicateOptions { PageRef? Page; PdfPoint? Offset; bool Values; }
public sealed record FormDuplicateResult(IReadOnlyList<FormField> Fields, IReadOnlyList<AnnotationRef> Widgets, string WidgetsJson, string DroppedJson);
Task<FormDuplicateResult> DuplicateAsync(IEnumerable<FormFieldRef> fields, FormDuplicateOptions? options = null, CancellationToken cancellationToken = default);
Task<FormDuplicateResult> DuplicateWidgetsAsync(IEnumerable<AnnotationRef> widgets, FormDuplicateOptions? options = null, CancellationToken cancellationToken = default);
```

`StageRemeasureAsync` writes `scale` with `PdfMeasurement.ToNode("scale")`, so a NaN is
`invalid-input` before the engine.

- [ ] **Step 1:** rebuild the host (`build-viewer-host.sh`) and regenerate (`generate-bindings.sh`).
- [ ] **Step 2: C# tests** (fail to compile first):
  `DuplicateCopiesFieldsWithNewRefsAsOneUndoStep` (a square with contents, a link, an ink drawing and
  a file attachment on `report.pdf`: copies have new refs, the same subtype and contents, offset
  boxes; one `HistoryCapability.UndoAsync` removes every copy);
  `ALockedAnnotationRefusesToDuplicate` (`CanDuplicateAsync` false; selection `CanDuplicateAsync`
  false);
  `CanDuplicateTruthTable` (`[]` false, unlocked true, missing ref false, read-only session false);
  `DuplicatingAMissingAnnotationIsNotFound`;
  `SelectionDuplicateCopiesTheSelectionAndSelectsTheCopies` and
  `SelectionDuplicateWithNothingSelectedIsEmpty`;
  `AHighlightIsNotMovableAndASquareIs`;
  `RemeasuringALineRelabelsIt` (a line dimension with a 1 in = 1 in scale, `StageRemeasureAsync` with
  1 in = 2 ft: `Updated` names it and its readout label changes);
  `FormFieldDuplicationMakesANamedCopy` and `FormWidgetDuplicationMakesANewField` (`FormTests`).
- [ ] **Step 3:** implement; `dotnet test` the new tests.
- [ ] **Step 4:** commit `feat(dotnet): wrap annotation and form duplication, movability and remeasure`.

### Task 5: coverage, docs, pin, CI

**Files:**
- Modify: `platforms/dotnet/csharp-api-coverage.json` (9 rows to ported; `get_shared` to
  `internal-resource`), `platforms/dotnet/README.md` ("Known gaps"),
  `docs/superpowers/plans/2026-11-20-dotnet-parity.md` ("Follow-up 2026-11-26"), engine pin files.

- [ ] **Step 1:** `python3 scripts/check-api-coverage.py --platform csharp`: 880 members, 739 ported,
  0 pending, 141 exclusions.
- [ ] **Step 2:** `EmbedPDF.Viewer.Tests` twice, `EmbedPDF.Models.Tests` once,
  `python3 -m unittest discover platforms/dotnet/packaging`, the `*.test.sh` scripts,
  `dotnet-consumer.sh`.
- [ ] **Step 3:** `dotnet-engine-hash.sh` for the new hash; re-pin; commit; push
  `research/dotnet-parity-p7`; dispatch `release-dotnet.yml` with `dotnet_version=0.0.0-dotnet.0`;
  commit the validation records it produces.

### Task 6: handoffs

**Files:**
- Create: `docs/superpowers/plans/2026-11-26-swift-ffi-duplicate-and-cancel.md`
- Create: `docs/superpowers/plans/2026-11-26-kotlin-ffi-duplicate-and-cancel.md`

- [ ] **Step 1:** each is a writing-plans task plan naming its start branch, the Rust commits, the
  exports above, the package API in that platform's idiom, the tests to port (pointing at Task 4's
  C# tests), the coverage rows, and the engine release and pins.
- [ ] **Step 2:** commit `docs: hand off the duplicate and cancel exports to Swift and Kotlin`.
