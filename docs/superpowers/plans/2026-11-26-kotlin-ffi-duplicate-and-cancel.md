# Kotlin: Duplicate, Movability, Remeasure and the markMatches Cancel

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the Android package (`platforms/android/embedpdf/embedpdf`) the nine annotation and
form members the Rust FFI now exports, take the `markMatches` cancel fix into a new Android
engine, and account for the members in `platforms/android/kotlin-api-coverage.json`.

**Architecture:** No Rust change. The exports already exist in `ffi.rs` and reach Kotlin through
the bindings `build-aar.sh` generates. Each new member is a one-line `viewer.call` or
`viewer.callCancellable` in the style of its neighbours, and returns the generated record behind a
`typealias`, as `AnnotationImportResult` and `FormImportResult` are today. The engine version
moves, because the annotation, form and redaction crates changed.

**Tech Stack:** Kotlin, Gradle, JDK 21, NDK r29, JUnit on the JVM (no emulator); Rust 1.100.0.

**Spec:** `docs/superpowers/plans/2026-11-26-ffi-duplicate-and-cancel.md`,
`docs/superpowers/plans/2026-11-26-swift-ffi-duplicate-and-cancel.md` (the same members for
Swift), `platforms/android/README.md` ("Where the engine comes from", "Releasing",
"Cancellation", "Building in the dev container").

**Start from:** `research/multi-platform-rust-core-impl-plugins` after both of these have landed
on it: `research/dotnet-parity-p7` (the Rust exports) and the Android parity work in progress on
its own branch. This plan edits files that work owns (`Annotation.kt`, `Form.kt`, `Redaction.kt`,
their tests, `kotlin-api-coverage.json`, the engine pin), so start it only after that branch is
merged, and rebase rather than run the two at once.

## What changed in Rust

| Commit | Change |
|---|---|
| `bf0d8af2a` | `RedactionHandle.mark_matches` aborts its plugin call from the cancel token. A cancel during the search used to be ignored until every mark was made; now the search aborts and the call rejects `operation-cancelled`, keeping marks already made. Cancelled before the search found a hit, it resolves with no marks, which Kotlin's `bridgedCancellable` turns into the caller's `CancellationException` anyway. |
| `5480d0afa` | `AnnotationHandle` exports `duplicate`, `canDuplicate`, `isMovable`, `whenRemeasurable`, `stageRemeasure`; `AnnotationSelectionHandle` exports `duplicate`, `canDuplicate`. |
| `ebf3fa761` | `FormHandle` exports `duplicate` and `duplicateWidgets`. |

Generated Kotlin (package `uniffi.epdf_plugin_annotation` or `uniffi.epdf_plugin_form`):

```kotlin
// AnnotationHandle
suspend fun duplicate(refs: List<AnnotationRef>, options: AnnotationDuplicateOptions, cancel: CancelToken): AnnotationDuplicateResult
suspend fun canDuplicate(refs: List<AnnotationRef>): Boolean
suspend fun isMovable(annotationRef: AnnotationRef): Boolean
suspend fun whenRemeasurable(cancel: CancelToken)
suspend fun stageRemeasure(page: PageRef, scaleJson: String): AnnotationRemeasureReport
// AnnotationSelectionHandle
suspend fun duplicate(offset: Point?, cancel: CancelToken): AnnotationDuplicateResult
suspend fun canDuplicate(): Boolean
// FormHandle
suspend fun duplicate(fields: List<FormFieldRef>, options: FormDuplicateOptions, cancel: CancelToken): FormDuplicateResult
suspend fun duplicateWidgets(widgets: List<AnnotationRef>, options: FormDuplicateOptions, cancel: CancelToken): FormDuplicateResult
```

New records: `AnnotationDuplicateOptions(page: PageRef? = null, offset: Point? = null, select:
Boolean = false)`, `AnnotationDuplicateResult(annotations: List<AnnotationRecord>, droppedJson:
String)`, `AnnotationRemeasureReport(page, updated, skipped: List<AnnotationRemeasureSkip>,
failed: List<AnnotationBatchFailure>, error: PluginErrorInfo?)`, `AnnotationRemeasureSkip(annotationRef,
reason)`, enum `AnnotationRemeasureSkipReason { NO_AUTHORITY, LOCKED, FOREIGN_MEASURE, UNAVAILABLE }`,
`FormDuplicateOptions(page: PageRef? = null, offset: Point? = null, values: Boolean = false)`,
`FormDuplicateResult(fields: List<FormField>, widgets: List<AnnotationRef>, widgetsJson: String,
droppedJson: String)`. `get_shared` stays unexported (an `internal-resource` exclusion).

## Global Constraints

- No Rust or TypeScript change.
- Work in the dev container; the Android README's "Building in the dev container" is the
  reference. `apiCheck` must pass, so `platforms/android/embedpdf/embedpdf/api/embedpdf.api` is
  regenerated with `apiDump` in the same commit as the members.
- A member that changes something says in its KDoc that a started change lands even when the
  caller is cancelled, as `import` does.
- Repo prose names no outside product, company or person, and has no em dashes.
- Commits by explicit path, `feat(android): ...`, no co-author or session trailer.

## Review Focus

- `stageRemeasure` before the annotations load answers a report with `error` set and nothing
  updated; the KDoc tells callers to `whenRemeasurable()` first.
- `canDuplicate(emptyList())` is `false`; `duplicate(emptyList())` copies nothing.
- The mid-call `markMatches` test needs the new engine: against the old one it runs for more
  than a minute. Give it a `withTimeout` so a regression fails instead of hanging the suite.

---

### Task 1: Annotation and form members

**Files:**
- Modify: `platforms/android/embedpdf/embedpdf/src/main/kotlin/com/embedpdf/Annotation.kt`,
  `platforms/android/embedpdf/embedpdf/src/main/kotlin/com/embedpdf/Form.kt`,
  `platforms/android/embedpdf/embedpdf/api/embedpdf.api`
- Test: `platforms/android/embedpdf/embedpdf/src/test/kotlin/com/embedpdf/AnnotationDuplicateTest.kt`
  (new), `platforms/android/embedpdf/embedpdf/src/test/kotlin/com/embedpdf/FormTest.kt`

**Interfaces (produces):**

```kotlin
public typealias AnnotationDuplicateOptions = uniffi.epdf_plugin_annotation.AnnotationDuplicateOptions
public typealias AnnotationDuplicateResult = uniffi.epdf_plugin_annotation.AnnotationDuplicateResult
public typealias AnnotationRemeasureReport = uniffi.epdf_plugin_annotation.AnnotationRemeasureReport
public typealias AnnotationRemeasureSkip = uniffi.epdf_plugin_annotation.AnnotationRemeasureSkip
public typealias AnnotationRemeasureSkipReason = uniffi.epdf_plugin_annotation.AnnotationRemeasureSkipReason
public typealias FormDuplicateOptions = uniffi.epdf_plugin_form.FormDuplicateOptions
public typealias FormDuplicateResult = uniffi.epdf_plugin_form.FormDuplicateResult
// AnnotationCapability
public suspend fun duplicate(refs: List<AnnotationRef>, options: AnnotationDuplicateOptions = AnnotationDuplicateOptions()): AnnotationDuplicateResult
public suspend fun canDuplicate(refs: List<AnnotationRef>): Boolean
public suspend fun isMovable(ref: AnnotationRef): Boolean
public suspend fun whenRemeasurable()
public suspend fun stageRemeasure(page: PageRef, scale: PdfMeasurement): AnnotationRemeasureReport
// AnnotationSelectionCapability
public suspend fun duplicate(offset: Point? = null): AnnotationDuplicateResult
public suspend fun canDuplicate(): Boolean
// FormCapability
public suspend fun duplicate(fields: List<FormFieldRef>, options: FormDuplicateOptions = FormDuplicateOptions()): FormDuplicateResult
public suspend fun duplicateWidgets(widgets: List<AnnotationRef>, options: FormDuplicateOptions = FormDuplicateOptions()): FormDuplicateResult
```

`stageRemeasure` writes `scale` with the encoder `AnnotationWire.kt` uses for a draft's `measure`.

- [ ] **Step 1: Port the tests** from `platforms/dotnet/EmbedPDF.Viewer.Tests/AnnotationDuplicateTests.cs`
  (eleven cases: the four-annotation duplicate undone in one step, duplicating nothing, a missing
  ref is `not-found`, a locked annotation, the truth table, a `doc.open`-only session, the
  selection duplicate and the empty selection, highlight versus square movability, remeasuring a
  distance line to `1 in = 2 ft`, a NaN scale) and the three form cases from `FormTests.cs`
  (`FormFieldDuplicationMakesANamedCopy`, `FormWidgetDuplicationMakesANewField`,
  `DuplicatingAFieldTheFormDoesNotHaveIsNotFound`), with `viewerTest { }` and `expectCode`.
- [ ] **Step 2:** `./gradlew :embedpdf:testDebugUnitTest --tests '*Duplicate*'` fails to compile.
- [ ] **Step 3:** implement the members and typealiases; `./gradlew :embedpdf:apiDump`.
- [ ] **Step 4:** the tests and `./gradlew :embedpdf:apiCheck` pass.
- [ ] **Step 5:** commit `feat(android): wrap annotation and form duplication, movability and remeasure`.

### Task 2: The markMatches cancel

**Files:**
- Modify: `platforms/android/embedpdf/embedpdf/src/main/kotlin/com/embedpdf/Redaction.kt` (KDoc)
- Test: `platforms/android/embedpdf/embedpdf/src/test/kotlin/com/embedpdf/RedactionTest.kt`

- [ ] **Step 1:** add `markMatchesStopsWhenCancelledMidCall`: `withTimeoutOrNull(300)` around
  `markMatches(SearchQuery(text = "e"))` on `report.pdf` returns `null`, the whole test runs
  under `withTimeout(10_000)`, and `pending()` still answers afterwards. Port of
  `RedactionTests.MarkMatchesStopsWhenCancelledMidCall` in the C# suite.
- [ ] **Step 2:** with the engine the bindings build from (the tree), the test passes; the KDoc of
  `markMatches` says a cancel aborts the search or stops between marks, keeping marks made.
- [ ] **Step 3:** commit `test(android): cancel markMatches mid-call`.

### Task 3: Engine pin

**Files:**
- Modify: `platforms/android/embedpdf/gradle.properties` (`embedpdf.engineRelease`)

- [ ] **Step 1:** `bash platforms/android/android-engine-hash.sh` prints the new hash;
  `bash platforms/android/check-android-engine.sh` exits 1.
- [ ] **Step 2:** set `embedpdf.engineRelease=android-engine-<hash>`; `bash
  platforms/android/build-aar.sh test` passes.
- [ ] **Step 3:** commit `build(android): pin android-engine-<hash>`. Publishing the engine is
  `android-release.yml` with `dry_run` on first; a real publish waits for the user.

### Task 4: Coverage rows

**Files:**
- Modify: `platforms/android/kotlin-api-coverage.json`

- [ ] **Step 1:** add the nine members as rows with `kotlin: { path, symbol }` in the shape of
  `selection_can_group` (`Annotation.kt` for the seven annotation members, `Form.kt` for form
  `duplicate` and `duplicate_widgets`; symbols `duplicate`, `canDuplicate`, `isMovable`,
  `whenRemeasurable`, `stageRemeasure`, `duplicate`, `canDuplicate`, `duplicate`,
  `duplicateWidgets`), and `get_shared` as an exclusion. The Kotlin manifest follows the Swift
  one, so if the Swift handoff has not landed yet, take its rows and reasons from
  `docs/superpowers/plans/2026-11-26-swift-ffi-duplicate-and-cancel.md` Task 4 and say so in the
  commit message.
- [ ] **Step 2:** `python3 scripts/check-api-coverage.py --platform kotlin` passes. This is the
  unified script that checks `swift`, `kotlin` and `csharp`; run `--platform swift` and
  `--platform csharp` as well, since the three manifests now move together.
- [ ] **Step 3:** commit `docs(android): account for the duplicate and remeasure members`.
