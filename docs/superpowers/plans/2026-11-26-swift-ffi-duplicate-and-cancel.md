# Swift: Duplicate, Movability, Remeasure and the markMatches Cancel

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `platforms/apple/EmbedPDF` the nine annotation and form members the Rust FFI now
exports, take the `markMatches` cancel fix into a new Apple engine, and clear the Swift coverage
check's drift errors that these members cause.

**Architecture:** No Rust change. The exports below already exist in `ffi.rs` and reach Swift
through the generated `EmbedPDFBindings`. Each new Swift member is a one-line bridge in the
style of its neighbours (`bridged` for a read, `bridgedCancellable` for a promise), and returns
the generated record the way `import(_:optionsJSON:)` returns `AnnotationImportResult`. The
engine needs a new `apple-engine-<hash>` release, because the annotation, form and redaction
crates changed.

**Tech Stack:** Swift 6.4, Xcode 27.1 or later, SwiftPM, swift-testing; Rust 1.100.0 for the
engine build.

**Spec:** `docs/superpowers/plans/2026-11-26-ffi-duplicate-and-cancel.md` (what changed and why),
`platforms/apple/README.md` ("Where the engine comes from", "Audited API coverage").

**Start from:** `research/multi-platform-rust-core-impl-plugins` once `research/dotnet-parity-p7`
is merged into it. Every commit named below is on that branch then. Engine publishing and the
package tests run on a Mac; editing and the coverage check run anywhere.

## What changed in Rust

| Commit | Change |
|---|---|
| `bf0d8af2a` | `RedactionHandle.mark_matches` hangs an `AbortSignal` on the plugin call and aborts it from the cancel token. Before, a cancel during the search was ignored until every mark was made (more than 90 s for `"e"` over `report.pdf`). Now the search aborts and the call rejects `operation-cancelled`, keeping marks already made. Cancelled before the search found a hit, it resolves with no marks, as the plugin does. |
| `5480d0afa` | `AnnotationHandle` exports `duplicate`, `can_duplicate`, `is_movable`, `when_remeasurable`, `stage_remeasure`; `AnnotationSelectionHandle` exports `duplicate`, `can_duplicate` (`crates/plugins/annotation/src/ffi/handle.rs`, `ffi/selection.rs`). |
| `ebf3fa761` | `FormHandle` exports `duplicate` and `duplicate_widgets` (`crates/plugins/form/src/ffi.rs`). |
| `6eee050fe` | `epdf_ffi::signalled` and `signalled_cancellable` carry the cancel token's abort into the plugin call's signal for all of the above. No behaviour change. |
| `c0c2cfb0a` | A duplicate `offset` with a NaN or an infinity is `invalid-input` from the annotation, selection and form exports, rather than a dropped offset. |

The generated Swift signatures (the generator lowers the snake case):

```swift
// AnnotationHandle
func duplicate(refs: [AnnotationRef], options: AnnotationDuplicateOptions, cancel: CancelToken) async throws -> AnnotationDuplicateResult
func canDuplicate(refs: [AnnotationRef]) async throws -> Bool
func isMovable(annotationRef: AnnotationRef) async throws -> Bool
func whenRemeasurable(cancel: CancelToken) async throws
func stageRemeasure(page: PageRef, scaleJson: String) async throws -> AnnotationRemeasureReport
// AnnotationSelectionHandle
func duplicate(offset: Point?, cancel: CancelToken) async throws -> AnnotationDuplicateResult
func canDuplicate() async throws -> Bool
// FormHandle
func duplicate(fields: [FormFieldRef], options: FormDuplicateOptions, cancel: CancelToken) async throws -> FormDuplicateResult
func duplicateWidgets(widgets: [AnnotationRef], options: FormDuplicateOptions, cancel: CancelToken) async throws -> FormDuplicateResult
```

New generated records: `AnnotationDuplicateOptions(page: PageRef? = nil, offset: Point? = nil,
select: Bool = false)`, `AnnotationDuplicateResult(annotations: [AnnotationRecord], droppedJson:
String)`, `AnnotationRemeasureReport(page, updated: [AnnotationRef], skipped:
[AnnotationRemeasureSkip], failed: [AnnotationBatchFailure], error: PluginErrorInfo?)`,
`AnnotationRemeasureSkip(annotationRef, reason: AnnotationRemeasureSkipReason)` with cases
`noAuthority`, `locked`, `foreignMeasure`, `unavailable`; `FormDuplicateOptions(page: PageRef? =
nil, offset: Point? = nil, values: Bool = false)`, `FormDuplicateResult(fields: [FormField],
widgets: [AnnotationRef], widgetsJson: String, droppedJson: String)`.

`get_shared` stays unexported: it hands other plugins the record as the store holds it, the same
`Rc`, and a foreign caller can only receive the copy `get` already answers. It becomes an
`internal-resource` exclusion.

## Early cancel returns the empty list

A `markMatches` cancelled before its search finds a hit resolves with an empty list. The
TypeScript plugin and the Rust core do the same, and C# returns it too. Swift must return that
empty array to the caller; it must not turn a resolved empty result into a cancellation error.
`bridgedCancellable` already hands back what the call returned, so this needs no new code, only
the doc comment and the test below. A cancel after a hit was found rejects `operation-cancelled`,
which reaches the caller as the usual cancellation.

## Global Constraints

- No Rust or TypeScript change. A behaviour question about an export goes back to the Rust
  owner, not into Swift.
- Swift 6 strict concurrency stays clean; every public member is `async throws(PDFError)`.
- Names follow `docs/superpowers/plans/2026-11-10-swift-api-idioms.md`: argument labels read as
  a phrase, `JSON` capitalised in parameter names.
- Repo prose names no outside product, company or person, and has no em dashes.
- Commits by explicit path, `feat(apple): ...`, no co-author or session trailer.

## Review Focus

- `stageRemeasure` before the annotations load answers a report with `error` set and nothing
  updated. Callers should `await whenRemeasurable()` first; Task 1's doc comment says so.
- A duplicate cancelled after the engine admitted the change still lands; Swift's
  `bridgedCancellable` hands back what the call returned (`PDFError.swift`, "Cancellation" in the
  Android README contrasts the two), so document it on `duplicate`.
- `canDuplicate([])` is `false`, and `duplicate([])` copies nothing without failing.

---

### Task 1: Annotation members

**Files:**
- Modify: `platforms/apple/EmbedPDF/Sources/EmbedPDF/Plugins/Annotation.swift`
- Test: `platforms/apple/EmbedPDF/Tests/EmbedPDFTests/AnnotationDuplicateTests.swift` (new)

**Interfaces (produces):**

```swift
// AnnotationCapability
public func duplicate(_ refs: [AnnotationRef], options: AnnotationDuplicateOptions = AnnotationDuplicateOptions()) async throws(PDFError) -> AnnotationDuplicateResult
public func canDuplicate(_ refs: [AnnotationRef]) async throws(PDFError) -> Bool
public func isMovable(_ ref: AnnotationRef) async throws(PDFError) -> Bool
public func whenRemeasurable() async throws(PDFError)
public func stageRemeasure(on page: PageRef, to scale: PdfMeasurement) async throws(PDFError) -> AnnotationRemeasureReport
// AnnotationSelectionCapability
public func duplicate(offset: Point? = nil) async throws(PDFError) -> AnnotationDuplicateResult
public func canDuplicate() async throws(PDFError) -> Bool
```

`stageRemeasure` encodes `scale` with the encoder the drafts use for their `measure` member
(`AnnotationDraft+Shapes.swift:329-368`), so a value JSON can't hold fails the way a draft's
does, before the engine. C# makes that `invalid-input`; check what the Swift encoder throws and
assert that.

- [ ] **Step 1: Port the tests.** Each case in
  `platforms/dotnet/EmbedPDF.Viewer.Tests/AnnotationDuplicateTests.cs` becomes a swift-testing
  `@Test` of the same meaning in a `BaseSuite` subclass: the four-annotation duplicate with one
  undo (square with contents and a link, ink, file attachment; offset `(20, 10)`), duplicating
  nothing, a missing ref is `not-found`, a locked annotation (`canDuplicate` and selection
  `canDuplicate` false), the truth table, a `doc.open`-only session (`permission-denied`), the
  selection duplicate selecting its copy and the empty selection, highlight versus square
  movability, remeasuring a distance line from `1 in = 1 m` to `1 in = 2 ft` (readout label
  changes, value `2`), and a NaN scale.
- [ ] **Step 2:** `swift test --filter AnnotationDuplicateTests` fails to compile.
- [ ] **Step 3:** implement the seven members.
- [ ] **Step 4:** the tests pass.
- [ ] **Step 5:** commit `feat(apple): wrap annotation duplication, movability and remeasure`.

### Task 2: Form members and the markMatches test

**Files:**
- Modify: `platforms/apple/EmbedPDF/Sources/EmbedPDF/Plugins/Form.swift`,
  `platforms/apple/EmbedPDF/Sources/EmbedPDF/Plugins/Redaction.swift` (doc comment only)
- Test: `platforms/apple/EmbedPDF/Tests/EmbedPDFTests/FormTests.swift`,
  `platforms/apple/EmbedPDF/Tests/EmbedPDFTests/RedactionTests.swift`

**Interfaces (produces):**

```swift
public func duplicate(_ fields: [FormFieldRef], options: FormDuplicateOptions = FormDuplicateOptions()) async throws(PDFError) -> FormDuplicateResult
public func duplicateWidgets(_ widgets: [AnnotationRef], options: FormDuplicateOptions = FormDuplicateOptions()) async throws(PDFError) -> FormDuplicateResult
```

- [ ] **Step 1: Port** `FormFieldDuplicationMakesANamedCopy`, `FormWidgetDuplicationMakesANewField`
  and `DuplicatingAFieldTheFormDoesNotHaveIsNotFound` from
  `platforms/dotnet/EmbedPDF.Viewer.Tests/FormTests.cs`, and
  `MarkMatchesStopsWhenCancelledMidCall` from `RedactionTests.cs`: count the hits of `"e"` over
  `report.pdf` with `findAll` (more than 100), then mark them in a task cancelled after 300 ms.
  The task ends within 5 s, and either throws the cancellation or returns fewer marks than hits;
  afterwards fewer marks are pending than there are hits. Both outcomes are correct, so a loaded
  machine can't flake it. Port `a duplicate offset that is not finite` from
  `AnnotationDuplicateTests.AnOffsetThatIsNotFiniteIsInvalidInput` and
  `FormTests.ADuplicateOffsetThatIsNotFiniteIsInvalidInput` too: the generated call answers
  `invalid-input` for a NaN offset, so Swift needs no check of its own.
- [ ] **Step 2:** the form tests fail to compile; the redaction test fails against the pinned
  engine (it runs past 10 s).
- [ ] **Step 3:** implement the two form members; on `markMatches`, document that a cancel aborts
  the search or stops between marks and keeps the marks made, and that a cancel before the first
  hit returns an empty array.
- [ ] **Step 4:** the form tests pass. The redaction test passes only after Task 3's engine.
- [ ] **Step 5:** commit `feat(apple): wrap form field and widget duplication`.

### Task 3: Engine release and pins

**Files:**
- Modify: `platforms/apple/EmbedPDF/Package.swift` (`engineRelease`, `engineChecksum`),
  `Package.swift` at the repository root (the same URL and checksum, by hand)

- [ ] **Step 1:** `bash platforms/apple/check-apple-engine.sh` exits 1 (stale, nothing published).
- [ ] **Step 2:** on a Mac, from a clean tree:
  `node scripts/publish-engine-payload.mjs --dry-run --only apple`, then
  `node scripts/publish-engine-payload.mjs --only apple`. It builds, runs the package tests against
  the packaged framework, creates `apple-engine-<hash>`, and rewrites the package manifest.
- [ ] **Step 3:** copy the new release URL and checksum into the root `Package.swift`.
- [ ] **Step 4:** `bash platforms/apple/check-apple-engine.sh` exits 2 on the release commit, as
  the README explains, and 0 once the pin commit is on top; `swift test` passes with the
  downloaded engine, including `MarkMatchesStopsWhenCancelledMidCall`.
- [ ] **Step 5:** commit `build(apple): pin apple-engine-<hash>` with both manifests.

### Task 4: Coverage rows

**Files:**
- Modify: `platforms/apple/swift-api-coverage.json`

- [ ] **Step 1:** turn the nine members into rows with `export` and `swift`, in the shape of
  `selection_can_group`:

| Rust member (`crate`, `symbol`) | `export.path`, `export.symbol` | `swift.symbol` |
|---|---|---|
| annotation `is_movable` | `ffi/handle.rs`, `is_movable` | `isMovable` |
| annotation `can_duplicate` | `ffi/handle.rs`, `can_duplicate` | `canDuplicate` |
| annotation `duplicate` | `ffi/handle.rs`, `duplicate` | `duplicate` |
| annotation `when_remeasurable` | `ffi/handle.rs`, `when_remeasurable` | `whenRemeasurable` |
| annotation `stage_remeasure` | `ffi/handle.rs`, `stage_remeasure` | `stageRemeasure` |
| annotation `selection_duplicate` | `ffi/selection.rs`, `duplicate` | `duplicate` |
| annotation `selection_can_duplicate` | `ffi/selection.rs`, `can_duplicate` | `canDuplicate` |
| form `duplicate` | `crates/plugins/form/src/ffi.rs`, `duplicate` | `duplicate` (`Form.swift`) |
| form `duplicate_widgets` | `crates/plugins/form/src/ffi.rs`, `duplicate_widgets` | `duplicateWidgets` (`Form.swift`) |

  Annotation export paths are under `crates/plugins/annotation/src/`; annotation `swift.path` is
  `platforms/apple/EmbedPDF/Sources/EmbedPDF/Plugins/Annotation.swift`. Add `get_shared`
  (`crates/plugins/annotation/src/read/annotations.rs`, owner `Core`) as an exclusion with
  classification `internal-resource` and the reason above, and drop the stale
  `remeasure_page` row: the member is gone from `write/measurement.rs`.
- [ ] **Step 2:** `python3 platforms/apple/check-swift-api-coverage.py` (or the unified
  `python3 scripts/check-api-coverage.py --platform swift`, whichever the start branch has).
  On `research/dotnet-parity-p7` at `ebf3fa761` the Swift check reports 18 drift errors; these
  rows clear 11 of them. The 7 that remain are unrelated test counters and members added after
  the Swift port: `commands` `resolve_command_text`, `redaction` `page_list_calls`, `selection`
  `load_starts`, `stage` `wheel_by`, `scene_key_builds` and `page_state_reads`, and the kernel's
  `settings_revision`. Re-run on the start branch and list what remains there.
- [ ] **Step 3:** commit `docs(apple): account for the duplicate and remeasure members`.
