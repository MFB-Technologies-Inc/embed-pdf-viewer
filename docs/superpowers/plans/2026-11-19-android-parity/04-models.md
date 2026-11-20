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

- [x] One commit for the module and the Task 1 models with `PdfViewerViewModel`, tests green. `models/api/models.api` is checked by `apiCheck` in `check` and in `build-aar.sh`; `checkNoCompose` fails `check` and `preBuild` on an `androidx.compose` dependency, direct or transitive, or import.
- The models reach a capability's viewer through a new public `viewer` on `AnnotationCapability`, `InteractionCapability` and `SearchCapability` (a separate `fix(android)` commit). Swift keeps it `package`, which Kotlin has no counterpart for across modules.
- Writable Swift properties are a `var` plus a flow: `SearchModel.text` and `textFlow`, `StageReaderModel.isPanEnabled` and `isPanEnabledFlow`. Swift's computed `activeHit`, `summary` and `canClear` are state flows the model recomputes after each change. The deprecated `UInt32` spellings (`activeIndex`, `currentPageIndex`) were not ported.
- An action whose caller is cancelled rethrows the `CancellationException`, where Swift returns. `OutlineModel` and `AttachmentModel` record `operation-cancelled` in `error` first, as Swift does.
- `HistoryModel`'s "Undo" and "Redo" and `SearchModel.summary`'s "of" are English: the module has no `Context` for string resources. The summary groups numbers with `NumberFormat`.
- `AttachmentModel.importFiles` takes `List<File>`, or `List<Uri>` with a `ContentResolver`, and reads on `Dispatchers.IO`. `ToolModel.buttonTapped` is public, since a picker in another module uses it.
- `PdfViewerViewModel(application, config, documentFetcher)` starts the viewer in `viewModelScope`; `startedViewer()` awaits it; `factory(config)` builds it for `by viewModels`. `onCleared()` runs `destroy()` and then `close()` on a detached coroutine, since the view model's scope is already cancelled by then.
- Swift's "a cancelled task releases the model" tests check a weak reference. Kotlin checks instead that a cancelled `run()` leaves no child running and that the model stops changing.

### Task 2: Rendering and stage models

Port `RenderRevisions`, `ThumbnailModel`, `TiledPageModel` (with a bounded `Bitmap` pool: `BitmapPool(maxBytes: Long)` reused by Phase 5), `StageReaderModel`, `StageVisiblePage+Viewport`, `TextSelectionModel`, `PDFReaderModel` (as `PdfReaderModel`). Tests as Task 1, plus the Review Focus cases.

- [x] Two commits: the rendering, stage and selection models, then `PdfReaderModel`, tests green at each (139, then 156 JVM tests in `:models:testDebugUnitTest`).
- A model owns no scope, so what Swift starts as tasks of their own runs in `run()`: `RenderRevisions` counts invalidations only while its `run()` runs (Swift counts from `init`), `ThumbnailModel.run()` carries shared renders and counts revisions, `TextSelectionModel.run()` follows selection events and sends drag calls, and `StageReaderModel.run()` sends pointer, drag and link-load calls. Without `run()`, a fire-and-forget call waits for the next suspend call on the same queue (`dragEnded`, `clear`, `dragChanged`), which sends everything queued before it.
- `SerialCalls` runs a call in whichever coroutine drains its queue. A call whose drainer is cancelled goes back to the front of the queue; only `Call.cancel()` drops one.
- A cancelled `ThumbnailModel.load` gives up its claim and throws at once, where Swift's returns when the render ends. `TiledPageModel.tileDisplayed` polls in the caller's coroutine until the plan settles; Swift returns at once and polls in a task.
- `BitmapPool(maxBytes)` is public for Phase 5. `TiledPageModel(pool = ...)` fills tiles and the base from it and returns a bitmap to it one reconcile after the bitmap left `tiles` or `base`.
- Gesture calls take floats (`pointerDown(x, y)`, `dragChanged(translationX, translationY)`, `sizeChanged(width, height, displayScale)`); `StageVisiblePage.localRect` returns `RectF` and `pagePoint`/`viewportPoint` take `PointF`.
- `linkActivations` is a `SharedFlow` (16 buffered, oldest dropped) that never completes, where Swift's streams finish when the model is released.
- `PdfReaderModel.Features` is `Set<Feature>`; `open(URL)` stands for Swift's `open(URL)`. A caller cancelled while its open runs sees its cancellation; a caller whose open a later call superseded returns normally.
- Not ported: the Swift tests that drive SwiftUI views, the 120 Hz drag-lag benchmark, and the pinch-during-settle and read-before-double-tap ordering tests (the read-ahead-of-double-tap and double-tap-during-pinch cases are ported).
