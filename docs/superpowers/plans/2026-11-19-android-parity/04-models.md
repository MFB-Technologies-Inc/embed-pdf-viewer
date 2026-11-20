# Android Parity Phase 4: Models

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An `embedpdf-models` module with a Kotlin counterpart for every model in `platforms/apple/EmbedPDF/Sources/EmbedPDFModels/`, usable from a View-based app or from Compose, with no Compose dependency.

**Architecture:** Each model is a plain class taking its capabilities in the constructor (same parameters as the Swift `init`). Observable state is `StateFlow<T>` (`val hits: StateFlow<List<SearchHit>>`), backed by a private `MutableStateFlow`. Swift's `func task() async` becomes `suspend fun run()`: it collects revisions and events until cancelled, and the caller decides the scope (`lifecycleScope`, `viewModelScope`, `LaunchedEffect`). Actions are `suspend fun`s with Swift's names. Writable Swift properties (`SearchModel.text`, `StageReaderModel.isPanEnabled`) become `MutableStateFlow` exposed as `var` setters plus a `StateFlow` reader. All state mutation happens on `Dispatchers.Main.immediate`; `run()` switches there itself.

`PdfViewerViewModel(application, config)` (an `AndroidViewModel` in this module, `androidx.lifecycle:lifecycle-viewmodel` only) owns a `PdfViewer` across configuration changes, calls `start()` on creation and `destroy()` in `onCleared()`.

**Tech Stack:** as Phase 3, plus `androidx.lifecycle:lifecycle-viewmodel-ktx`, `kotlinx-coroutines-test` (`StandardTestDispatcher`, `Dispatchers.setMain`), Robolectric for `PdfViewerViewModel`.

**Spec:** umbrella Phase 4. Each Swift model's header doc comment is the contract for its Kotlin twin (snapshot lag, error clearing, close behaviour, refresh ordering); the KDoc says the same.

## Global Constraints

As Phase 3. Additionally:
- No `androidx.compose.*` import in this module (a Gradle dependency check fails the build if one appears).
- No model creates its own `CoroutineScope`; long-running work happens inside `run()` or inside a suspend action.
- Internal helpers port as internal: `SerialCalls`, `SnapshotRefresh`, `CancelOnRelease`, `CapabilityObservation`, `PageRenderTask`, `StagePointerForwarder`.
- Geometry that Swift returns as `CGPoint`/`CGRect`/`CGImage` returns `PointF`/`RectF`/`Bitmap`.

## Review Focus

- A refresh started earlier never overwrites one started later (port Swift's ordering tests).
- `run()` cancelled while a refresh is in flight leaves no coroutine running and the model stops changing.
- The document closes while `run()` collects: the model ends empty with `error` set, as in Swift.
- `TextSelectionModel`: drag points that arrive while an extension is queued collapse into one; `dragEnded()` returns after the last extension lands.
- `PdfViewerViewModel` survives `ActivityScenario.recreate()` with the same `PdfViewer` instance, and `onCleared()` destroys it exactly once.

---

### Task 1: Module and the simple models

Port `ViewerModel`, `HistoryModel`, `OutlineModel`, `AttachmentModel`, `AnnotationModel`, `ToolModel`, `SearchModel` and the internal helpers, plus `PdfViewerViewModel`. Tests: the Swift test files in `Tests/EmbedPDFUITests/` and `Tests/EmbedPDFTests/` that cover each model (find them with `grep -l <ModelName> platforms/apple/EmbedPDF/Tests -r`).

### Task 2: Rendering and stage models

Port `RenderRevisions`, `ThumbnailModel`, `TiledPageModel` (with a bounded `Bitmap` pool: `BitmapPool(maxBytes: Long)` reused by Phase 5), `StageReaderModel`, `StageVisiblePage+Viewport`, `TextSelectionModel`, `PDFReaderModel` (as `PdfReaderModel`). Tests as Task 1, plus the Review Focus cases.
