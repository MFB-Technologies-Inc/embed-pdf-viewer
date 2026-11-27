# Phase 4: Models Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A UI-neutral `EmbedPDF.Models` assembly with a C# counterpart of each of the 20 files in `platforms/apple/EmbedPDF/Sources/EmbedPDFModels/`, their Swift tests ported to `EmbedPDF.Models.Tests`, and a README section that tells a consumer what its view must feed the models.

**Architecture:** Each model is an `INotifyPropertyChanged` class over the Phase 3 capabilities. A model has one home `SynchronizationContext` (captured at construction or supplied), and every public entry point hops onto it before it touches state, so the model behaves as Swift's `@MainActor` class does and every `PropertyChanged` is raised there. Swift's `task()` is `RunAsync(CancellationToken)`; Swift's internal helpers (`SerialCalls`, `SnapshotRefresh`, `PageRenderTask`, `CancelOnRelease`, `observeCapability`) are internal C# types with the same rules.

**Tech Stack:** .NET SDK 10.0.403 `net10.0`, xUnit 2.9.3, PublicApiAnalyzers 5.6.0. No new package reference.

**Spec:** `docs/superpowers/plans/2026-11-20-dotnet-parity.md` (Global Constraints with "No UI components", D3, D4, D6, D8, Review Focus 3, the Phase 2 and Phase 3 notes, Phase 4), the Swift sources under `platforms/apple/EmbedPDF/Sources/EmbedPDFModels/` and their tests under `platforms/apple/EmbedPDF/Tests/EmbedPDFModelsTests/`.

## Global Constraints

- Dev container `embedpdf-dotnet-parity`, worktree `/workspaces/wt-p4`, branch `research/dotnet-parity-p4` from `b206fe923` (tip of `research/dotnet-parity-p3e`). Guard script before every heavy build or test run and between tasks; exit 75 or 76 stops the phase after the last complete task.
- Bindings: `CARGO_TARGET_DIR=/workspaces/embed-pdf-viewer/target platforms/dotnet/generate-bindings.sh --library /workspaces/embed-pdf-viewer/target/release/libepdf_viewer_host.so`; tests run with `-p:EpdfViewerNativeDir=/workspaces/embed-pdf-viewer/target/release/`.
- Commits by explicit path, `feat(dotnet): ...`, `test(dotnet): ...`, `docs(dotnet): ...`, no trailers. No push, no merge.
- No TypeScript or `packages/**` change, no Rust change. `platforms/dotnet/EmbedPDF` untouched.
- No UI framework reference, no `ICommand`, no framework bitmap type in `EmbedPDF.Models` (D6). Images are `PixelBuffer`.
- `IsAotCompatible`, `TreatWarningsAsErrors`, `Nullable`, 0 warnings. `PublicAPI.Unshipped.txt` changes in the same commit as the surface.
- Prose and comments name no outside product, company or person. No em dashes.

## Decisions this plan makes

- **Project:** `platforms/dotnet/EmbedPDF.Models/` (package `EmbedPDF.Models`, assembly `EmbedPDF.Models`, namespace `EmbedPDF.Models`), referencing `EmbedPDF.Viewer` only. Tests in `platforms/dotnet/EmbedPDF.Models.Tests/`. Both in `EmbedPDF.sln`.
- **Notification:** hand-written. An internal abstract `ObservableModel : INotifyPropertyChanged` holds the home context and a `SetProperty(ref T field, T value, [CallerMemberName] string? name = null)` that writes and raises only when the value changed (`EqualityComparer<T>.Default`, reference equality for lists), which is Swift's "write only what changed". No CommunityToolkit.Mvvm: the generator would add a package for a few dozen setters.
- **Home context:** every public constructor takes `SynchronizationContext? context = null` last. `null` means `SynchronizationContext.Current` at construction; when that is also `null` (a console app, a test without a context), the model makes an internal `SerialSynchronizationContext` that runs posted work one item at a time on the thread pool and sets itself as `Current` while it runs. Each async entry point starts with `await home.SwitchTo()` (an awaitable that continues inline when already on the context and posts otherwise); each synchronous entry point runs inline on the context and posts itself otherwise. Awaits inside a model keep the default `ConfigureAwait(true)`, so continuations return to the home context.
- **`RunAsync(CancellationToken)`** is Swift's `task()`: it returns, without throwing, when the token is cancelled or the observed streams end; a second call supersedes the first, as Swift's session counter does.
- **Collections:** every list a view reads is an `IReadOnlyList<T>` (or `IReadOnlyDictionary`/`IReadOnlySet`) snapshot replaced wholesale with one `PropertyChanged`. No model uses `INotifyCollectionChanged`: every Swift model replaces its arrays and dictionaries whole. Per-page lookups (`GetRevision(PageRef)`, `GetImage(PageRef)`, `GetHitCount(PageRef)`) read a dictionary property (`Revisions`, `Images`) whose change is announced.
- **Errors:** `PdfException? Error`. Swift's `controlError` is internal `ModelErrors.Control(Exception)`: a `PdfException` passes through, `OperationCanceledException` becomes `operation-cancelled`, anything else `operation-failed` with the exception's type name and message.
- **Capability viewer:** Swift reaches a capability's viewer through `package let viewer`. The facade grants `InternalsVisibleTo("EmbedPDF.Models")` and adds `internal PdfViewer Viewer` to `SearchCapability`, `AnnotationCapability` and `InteractionCapability`, so `SearchModel(search)` keeps Swift's shape. Cost: `EmbedPDF.Models` must ship with the exact `EmbedPDF` version it was built against (Phase 6 packs that dependency as an exact range).
- **Self-observing models** (`RenderRevisions`, `ThumbnailModel`, `TextSelectionModel`, and `StageReaderModel` through its `RenderRevisions`) start their loop at construction, as Swift's `init` does. The loop holds the model through a `WeakReference`, so an abandoned model is collectable; the model is `IDisposable` (Dispose cancels the loop, Swift's `deinit`) and its finalizer queues the same cancel. Models given capabilities never dispose them.
- **Geometry inputs:** Swift's `CGPoint`/`CGSize` become plain doubles. Pointer events are `PointerInput(double X, double Y)` with `init` properties `PointerKind Kind = PointerKind.Touch` and `PointerModifiers Modifiers`, a new record struct: wave B's `PointerSample` does not fit, because it carries the page hit the model computes and a phase the method name already gives. Translations and velocities are `(double x, double y)` parameter pairs. Page-space points stay `PdfPoint`. Swift's models take no scroll offset (the stage owns the camera; a drag pans it), so neither do these; a consumer whose framework scrolls calls `StageCapability.ScrollToAsync` and the model follows the camera event.
- **Localized text:** Swift's `String(localized:)` keys are English literals here ("Undo", "Redo", "change", "Some changes could not be undone.", "{0} of {1}" with `CultureInfo.CurrentCulture` number grouping). An app that words them otherwise builds its own from the raw properties, as the Swift docs say.
- **`AttachmentModel.ImportFilesAsync(IEnumerable<string> paths)`** reads files on the thread pool; the MIME type comes from a small extension table, `null` when unknown (Swift asks the system type registry, which .NET lacks). `FileOperationFailed(Exception)` ignores `OperationCanceledException` as Swift ignores a user-cancelled Cocoa error.
- **`PdfReaderModel`:** `ReaderFeatures` is a `[Flags]` enum (`Search`, `Selection`, `Tools`, `Links`, `All`); `ReaderPhase` is an abstract record with `Idle`, `Loading`, `Locked(DocumentInfo)`, `Ready(ReaderSession)`, `Failed(PdfException)`; `configure` is `Func<PdfViewerOptionsBuilder, PdfViewerOptionsBuilder>?`. `OpenAsync(ReadOnlyMemory<byte>, OpenDocumentOptions?)` and `OpenAsync(Uri, string? name)` (RS0026 suppressed, as the facade's paired overloads are), `UnlockAsync(string)`, `CloseAsync()`, and `IAsyncDisposable` as a close.
- **Test hooks** (`ReadFinished`, `RenderStarted`, `HubCallStarting`, `ZoomStartReached`, ...) are internal, visible to `EmbedPDF.Models.Tests`.

## Review Focus

1. A notification caused by a kernel-thread callback or a plugin stream (a revision, a search event, a render invalidation) arrives on the model's home thread. Every model test runs inside `SingleThreadContext.Run` and a `PropertyChangedRecorder` fails the test on any notification raised off that thread (Tasks 1 to 12).
2. A model constructed with no `SynchronizationContext` still serializes its work and raises on one logical context at a time. Task 1 pins it (`AModelWithoutAContextUsesASerialOne`).
3. An abandoned self-observing model is collectable while its document stays open, and its event subscription ends. Tasks 2 and 8 pin it (`AReleasedModelStopsObservingWhileTheDocumentStaysOpen`).
4. A model whose `RunAsync` was cancelled does not keep the model alive. Tasks 3 to 5 port Swift's `aCancelledTaskReleasesTheModel`.
5. Calling an entry point from a worker thread neither races the home thread nor raises there. Task 1 pins it (`AnEntryPointCalledOffTheContextRunsOnIt`).

---

### Task 1: Project, infrastructure and the test harness

**Files:** create `platforms/dotnet/EmbedPDF.Models/{EmbedPDF.Models.csproj, PublicAPI.Shipped.txt, PublicAPI.Unshipped.txt, ObservableModel.cs, ModelContext.cs, SerialSynchronizationContext.cs, SerialCalls.cs, SnapshotRefresh.cs, PageRenderTask.cs, CapabilityObservation.cs, ModelErrors.cs, WeakLoop.cs}`, `platforms/dotnet/EmbedPDF.Models.Tests/{EmbedPDF.Models.Tests.csproj, SingleThreadContext.cs, PropertyChangedRecorder.cs, Observed.cs, Fixtures.cs, ImportTests.cs, ModelContextTests.cs, SerialCallsTests.cs, SnapshotRefreshTests.cs, PageRenderTaskTests.cs}`; modify `EmbedPDF.sln`, `EmbedPDF.Viewer/EmbedPDF.Viewer.csproj` (`InternalsVisibleTo EmbedPDF.Models`), `Plugins/{Search,Annotation,Interaction}.cs` (`internal PdfViewer Viewer`).

**Interfaces (produces):** `ModelContext.Resolve(SynchronizationContext?)`, `ModelContext.SwitchTo()` awaitable, `ModelContext.Run(Action)`; `ObservableModel(SynchronizationContext?)` with `SetProperty`, `Raise(string)`, `Home`; `SerialCalls.Enqueue<T>(Func<Task<T>>) : Task<T>` and `Enqueue(Func<Task>) : Task`; `SnapshotRefresh.RunAsync<T>(Func<Task<T>> load, Action<T> apply, Action<Exception> failure, CancellationToken)`, `Invalidate()`, `Func<Task>? ReadFinished`; `PageRenderTask.RunAsync(Func<CancellationToken, Task>, CancellationToken)`, `StartIfIdle(Func<CancellationToken, Task>)`; `CapabilityObservation.ObserveAsync<TEvent>(PdfViewer, IAsyncEnumerable<TEvent>, Func<Task> initial, Func<Task> refresh, Func<TEvent, Task> receive, CancellationToken)`; tests: `SingleThreadContext.Run(Func<Task>)`, `PropertyChangedRecorder(INotifyPropertyChanged)` with `AssertAllOnThread()`, `Observed.UntilAsync(model, Func<bool>, TimeSpan?)`, `Observed.Fires(model, name, Func<Task>)`.

- [x] **Step 1: Failing tests.** Port `SerialCallsTests.swift` (2), `SnapshotRefreshTests.swift` (2), `PageRenderTaskTests.swift` (4) and `ImportTests.swift` (as `TheModelsReferenceNoUiAssembly`: the referenced assemblies of `EmbedPDF.Models` are `EmbedPDF.Viewer` plus shared-framework assemblies, an allow-list no UI framework assembly passes). New: `ModelContextTests.AModelWithoutAContextUsesASerialOne` and `AnEntryPointCalledOffTheContextRunsOnIt`.
- [x] **Step 2:** `dotnet test EmbedPDF.Models.Tests`: build FAIL.
- [x] **Step 3:** Implement. Project settings as the facade's: `IsAotCompatible`, PublicApiAnalyzers, `InternalsVisibleTo EmbedPDF.Models.Tests`.
- [x] **Step 4:** PASS, 0 warnings.
- [x] **Step 5:** Commit `feat(dotnet): add the EmbedPDF.Models project and its scheduling helpers`.

### Task 2: RenderRevisions

**Files:** `RenderRevisions.cs`; test `RenderRevisionsTests.cs`.

**Interfaces:** `RenderRevisions(RenderCapability render, SynchronizationContext? context = null) : ObservableModel, IDisposable`; `long GetRevision(PageRef)`; `IReadOnlyDictionary<PageRef, long> Revisions`; internal `Task ObservationTask`.

- [x] Port the 4 Swift cases; `AReleasedModelStopsObservingWhileTheDocumentStaysOpen` drops the model, forces a GC and expects `ObservationTask` to finish and the weak reference to be dead. FAIL, implement, PASS. Commit `feat(dotnet): add RenderRevisions`.

### Task 3: ViewerModel and AnnotationModel

**Files:** `ViewerModel.cs`, `AnnotationModel.cs`; tests `ViewerModelTests.cs`, `AnnotationModelTests.cs`.

**Interfaces:** `ViewerModel(PdfViewer)`: `ActiveDocument`, `DocumentCount`, `Error`, `RunAsync`, `RefreshAsync`. `AnnotationModel(AnnotationCapability)`: `CanRead`, `CanCreate`, `Error`, `RunAsync`, `RefreshAsync`. Internal `Snapshots`.

- [x] Port the 6 + 6 Swift cases. FAIL, implement, PASS. Commit `feat(dotnet): add ViewerModel and AnnotationModel`.

### Task 4: SearchModel

**Files:** `SearchModel.cs`; test `SearchModelTests.cs`.

**Interfaces:** `SearchModel(SearchCapability)`: settable `Text`; `Hits`, `ActiveHitIndex` (`int?`), `Status`, `HitCount`, `Progress`, `Error`, `ActiveHit`, `Summary`, `CanClear`, `GetHitCount(PageRef)`; `RunAsync`, `RefreshAsync`, `SubmitSearchAsync`, `NextHitAsync`, `PreviousHitAsync`, `RevealActiveHitAsync`, `ClearAsync`. Swift's deprecated `activeIndex` has no counterpart (C# never shipped it), so `theUInt32ActiveIndexFollowsActiveHitIndex` is not ported.

- [x] Port the 23 remaining Swift cases. FAIL, implement, PASS. Commit `feat(dotnet): add SearchModel`.

### Task 5: ToolModel

**Files:** `ToolModel.cs`; test `ToolModelTests.cs`.

**Interfaces:** `ToolModel(InteractionCapability)`: `Tools`, `ActiveToolId` (`ToolId?`), `Usable` (`IReadOnlySet<ToolId>`), `Error`, `Submitting`; `RunAsync`, `RefreshAsync`, `ActivateAsync(ToolId)`, `ActivateDefaultAsync()`, `IsSelected(ToolId)`, `IsDisabled(ToolId)`, `ButtonTappedAsync(ToolId)` (public: Swift's is `package` for its picker, and a C# app builds its own picker). Internal `HubCallStarting`.

- [x] Port the 24 Swift cases. FAIL, implement, PASS. Commit `feat(dotnet): add ToolModel`.

### Task 6: HistoryModel, OutlineModel and AttachmentModel

**Files:** `HistoryModel.cs`, `OutlineModel.cs`, `AttachmentModel.cs`; tests `PluginIntegrationTests.cs`, `PanelModelTests.cs`.

**Interfaces:** `HistoryModel(PdfViewer, HistoryCapability, I18nCapability? translator = null)`: `CanUndo`, `CanRedo`, `UndoLabel`, `RedoLabel`, `UndoTitle`, `RedoTitle`, `Submitting`, `Notice`, `Error`, `UndoAsync`, `RedoAsync`. `OutlineModel(PdfViewer, OutlineCapability)`: `Items`, `Rows`, `SelectedItems`, `CurrentItem`, `Status`, `Incomplete`, `CanEdit`, `Performing`, `Error`, `LastActivation`, `ReloadAsync`, `SelectAsync`, `ExpandAsync`, `CollapseAsync`, `ToggleAsync`, `CreateAsync`, `UpdateAsync`, `MoveAsync`, `DeleteAsync`, `ActivateAsync`. `AttachmentModel(PdfViewer, AttachmentCapability)`: `Files`, `SelectedFiles`, `Status`, `CanAdd`, `CanDownload`, `CanRemoveSelection`, `PendingIds`, `Performing`, `Error`, `ReloadAsync`, `SelectAsync`, `AddAsync`, `RemoveAsync`, `RevealAsync`, `DownloadAsync`, `ImportFilesAsync`, `FileOperationFailed`. All with `RunAsync` and `RefreshAsync`.

- [x] Port `PluginIntegrationTests.swift` (3). Swift has no per-model test file for these three; new small cases: a history `UndoFailed` sets `Error`, an attachment action while `Performing` is ignored, `ImportFilesAsync` adds a file read from disk. FAIL, implement, PASS. Commit `feat(dotnet): add the history, outline and attachment models`.

### Task 7: ThumbnailModel

**Files:** `ThumbnailModel.cs`; test `ThumbnailModelTests.cs`.

**Interfaces:** `ThumbnailModel(RenderCapability, IEnumerable<PageInfo> pages) : IDisposable`: `Error`, `Images` (`IReadOnlyDictionary<PageRef, PixelBuffer>`), `GetImage(PageRef)`, `GetRevision(PageRef)`, `LoadAsync(PageRef, CancellationToken)`. Internal `CacheLimit`, `RenderCount`, `RenderStarted`, `WaiterCount(PageRef)`.

- [x] Port the 9 Swift cases. FAIL, implement, PASS. Commit `feat(dotnet): add ThumbnailModel`.

### Task 8: TextSelectionModel

**Files:** `TextSelectionModel.cs`; test `TextSelectionModelTests.cs`.

**Interfaces:** `TextSelectionModel(SelectionCapability) : IDisposable`: `Rects` (`IReadOnlyDictionary<PageRef, IReadOnlyList<PdfRect>>`), `IsSelecting`, `Error`; `DragBegan(PdfPoint, PageRef, SelectionStart start = SelectionStart.Caret)`, `DragChanged(PdfPoint, PageRef)`, `Task<bool> DragEndedAsync()`, `ClearAsync()`. `SelectionStart { Caret, Word }`. Internal `ExtensionCount`, `RectsReadReturned`, `ObservationTask`.

- [x] Port the 13 Swift cases. FAIL, implement, PASS. Commit `feat(dotnet): add TextSelectionModel`.

### Task 9: Page geometry and the pointer forwarder

**Files:** `StageVisiblePageExtensions.cs`, `PointerInput.cs`, `StagePointerForwarder.cs` (internal); tests `StageVisiblePageGeometryTests.cs` (forwarder cases land with Task 10, which owns the reader that drives it).

**Interfaces:** static `ViewportPoint(this StageVisiblePage, double localX, double localY) : PdfPoint`, `GetGeometry(this StageVisiblePage, PageInfo) : PageGeometry`, `PagePoint(this StageVisiblePage, double localX, double localY, PageInfo) : PdfPoint`, `LocalRect(this StageVisiblePage, PdfRect, PageInfo) : PdfRect`; `PointerInput`; internal `StagePointerForwarder(StageCapability, InteractionCapability)` with `Down/Move/Up(PointerInput)`, `Cancel()`, `Interrupt()`, `Tap(PointerInput)`, `FinishedAsync()`, `LookedUp`, `Dispatched`.

- [x] Port the 4 geometry cases. FAIL, implement, PASS. Commit `feat(dotnet): add the stage page geometry helpers and PointerInput`.

### Task 10: StageReaderModel

**Files:** `StageReaderModel.cs`; tests `StagePointerForwarderTests.cs`, `StageReaderErrorTests.cs`, `StageReaderDragLagTests.cs`.

**Interfaces:** `StageReaderModel(StageCapability, RenderCapability, InteractionCapability? interaction = null, LinkCapability? link = null) : IDisposable`: `VisiblePages`, `Camera` (`Camera?`), `PageIndex`, `Error`, `IsZooming`, `ActiveToolId`, `ForwardsDrags`, settable `IsPanEnabled`, `PanBeginsOnMovement`, `Revisions`, `Stage`, `Render`, `GetRenderRevision(PageRef)`, `ObserveLinkActivations(CancellationToken)` (`IAsyncEnumerable<LinkActivation>`); `RunAsync`, `SizeChangedAsync(double width, double height, double displayScale)`, `DragChangedAsync(double translationX, double translationY)`, `DragEndedAsync(double velocityX, double velocityY)`, `DragCancelledAsync()`, `MagnifyChanged(double magnification, double x, double y)`, `MagnifyEndedAsync()`, `DoubleTappedAsync(double x, double y)`, `PointerDown/PointerMoved/PointerUp(PointerInput)`, `PointerCancelled()`, `Tapped(PointerInput)`; static `MaxPagePixels`, `RenderScale(double scale, double contentWidth, double contentHeight, bool zooming = false)`.

- [x] Port `StagePointerForwarderTests.swift` (14), `StageReaderErrorTests.swift` (5) and `StageReaderDragLagTests.swift` (1). FAIL, implement, PASS. Commit `feat(dotnet): add StageReaderModel`.

### Task 11: TiledPageModel

**Files:** `TiledPageModel.cs`; test `TiledPageModelTests.cs`.

**Interfaces:** `TiledPageModel(RenderCapability, RenderViewDemand, PageRef, bool includeAnnotations = true)`: `Base` (`PixelBuffer?`), `Tiles` (`IReadOnlyList<PageTile>`), `Page`, `IncludeAnnotations`; `UpdateAsync(double deviceScale, double unrotatedWidth, PdfRect? visibleRect, Action? onPainted = null, CancellationToken)`, `TileDisplayedAsync(string key)`, `ReleaseAsync()`. `PageTile(string Id, PdfRect Rect, double Z, PixelBuffer Image)` whose equality skips `Image`.

- [x] Port the 5 Swift cases. FAIL, implement, PASS. Commit `feat(dotnet): add TiledPageModel`.

### Task 12: PdfReaderModel

**Files:** `PdfReaderModel.cs`, `ReaderPhase.cs`; test `PdfReaderModelTests.cs`.

**Interfaces:** as in Decisions; `ReaderSession(PdfViewer Viewer, DocumentInfo Document, IReadOnlyList<PageInfo> Pages, StageReaderModel Reader, SearchModel? Search, TextSelectionModel? Selection, ToolModel? Tools)` with `GetPageInfo(StageVisiblePage)`.

- [x] Port the 16 Swift cases (`opensAFileURL` opens a `file:` `Uri`). FAIL, implement, PASS. Commit `feat(dotnet): add PdfReaderModel`.

### Task 13: README, notes and the final run

- [x] `platforms/dotnet/README.md`: new section "Binding the models to a UI": the home context rule, `RunAsync`, the inputs a view feeds `StageReaderModel` (size, drags, pinch, double tap, `PointerInput`), `PixelBuffer` images, and a short framework-neutral example.
- [x] Release build of every project, 0 warnings; `EmbedPDF.Models.Tests` twice; `EmbedPDF.Viewer.Tests` once.
- [x] Umbrella "Phase 4 notes"; tick Phase 4 and this plan's boxes. Commit `docs(dotnet): record phase 4`.
