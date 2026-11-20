# Phase 3 Wave A: Plugin Capabilities Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Typed C# capabilities for the nine wave A plugins (metadata, search, render, history, outline, attachment, page-edit, view-manager, shell), with `PixelBuffer` for render output, so every wave A row of `platforms/dotnet/csharp-api-coverage.json` is ported.

**Architecture:** Each plugin gets a public `*Capability` class over its internal generated handle, hand-written public records and enums mirroring the generated ones (D3) with internal mappings both ways, and a `PdfViewer` accessor. Calls go through `PdfViewer.CallAsync`/`CallCancellableAsync`, so disposal drains them and errors map to `PdfException`. Event streams are `IAsyncEnumerable<T>` built on the Phase 2 `Subscription<T>` and end when Rust ends the subscription, when the caller cancels, or when the viewer is disposed.

**Tech Stack:** .NET SDK 10.0.403 `net10.0`, xUnit 2.9.3, `System.Text.Json` (source-generated `JsonTypeInfo<T>` overloads for shell props), PublicApiAnalyzers 5.6.0.

**Spec:** `docs/superpowers/plans/2026-11-20-dotnet-parity.md` (D3, D4, D8, "Phase 2 notes", "Phase 3"), the Swift sources in `platforms/apple/EmbedPDF/Sources/EmbedPDF/Plugins/` (`Metadata.swift`, `Search.swift`, `Render.swift`, `History.swift`, `Outline.swift`, `Attachment.swift`, `PageEdit.swift`, `ViewManager.swift`, `Shell.swift`), `CoreGraphicsBridges.swift`, `PageGeometry.swift` and their tests in `platforms/apple/EmbedPDF/Tests/EmbedPDFTests/`.

## Global Constraints

- All work in the dev container `embedpdf-dotnet-parity`, worktree `/workspaces/wt-p3a`, branch `research/dotnet-parity-p3a` from `cf1e8f765`. Guard script before every heavy build or test run.
- Bindings: `CARGO_TARGET_DIR=/workspaces/embed-pdf-viewer/target platforms/dotnet/generate-bindings.sh --library /workspaces/embed-pdf-viewer/target/release/libepdf_viewer_host.so`; tests run with `-p:EpdfViewerNativeDir=/workspaces/embed-pdf-viewer/target/release/`.
- Commits by explicit path, `feat(dotnet): ...` or `test(dotnet): ...`, no trailers. No push, no merge.
- No TypeScript or `packages/**` change, no plugin Rust change, no `ffi.rs` change. `platforms/dotnet/EmbedPDF` untouched.
- Generated C# stays internal; no generated type in a public signature. Public integers are `int` or `long` (D4); a `uint` from the engine converts with `WireInteger.ToInt`, a caller's `int` with `WireInteger.ToUInt32`.
- `IsAotCompatible`, `TreatWarningsAsErrors`, `Nullable`; 0 warnings. `PublicAPI.Unshipped.txt` and the coverage manifest change in the same commit as the surface.
- Prose and comments name no outside product, company or person. No em dashes.

## Shared shape (every plugin task)

- Files live in `platforms/dotnet/EmbedPDF.Viewer/Plugins/<Plugin>.cs` (capability, records, accessor as a `partial class PdfViewer`) and `Plugins/<Plugin>.Ffi.cs` (internal mappings). Tests in `EmbedPDF.Viewer.Tests/<Plugin>Tests.cs`.
- Accessor: `Task<XCapability> GetXAsync(DocumentInfo document, CancellationToken cancellationToken = default)` (`GetMetadataAsync`, `GetSearchAsync`, `GetRenderAsync`, `GetHistoryAsync`, `GetOutlineAsync`, `GetAttachmentsAsync`, `GetPageEditAsync`, `GetShellAsync`; `GetViewManagerAsync(CancellationToken)` has no document). *Changed after execution:* the first cut named them `SearchAsync` and so on; see D4. The host answers `unsupported` for a plugin left out of the options.
- Capability: `public sealed class XCapability : IDisposable`; `Dispose` releases the native handle (it never waits on the kernel, so no `IAsyncDisposable`). Members follow Phase 2 naming: Swift `snapshot()` is `GetSnapshotAsync`, `hits(on:)` is `GetHitsAsync`, `canUpdate()` is `CanUpdateAsync`, verbs keep their verb (`UpdateAsync`, `UndoAsync`). Every member takes `CancellationToken cancellationToken = default` last; members whose Rust export takes a `CancelToken` use `CallCancellableAsync`.
- Streams: Swift's `events` is `IAsyncEnumerable<XEvent> ObserveEvents(CancellationToken cancellationToken = default)`, unbounded per subscriber as Swift's `AsyncStream`, subscribed when the method returns.
- Records: `sealed record` for generated records, `abstract record` with nested `sealed record` cases for generated unions (as `DocumentEvent`), `enum` for closed sets. Patches are records with init properties and `IReadOnlyList<...> Clear` where the generated patch has a clear list.

## Review Focus

1. A capability used after its viewer is disposed throws `PdfException` with `instance-closed`, never `ObjectDisposedException`. Task 1 pins it for a generic call, Task 2 on metadata.
2. A plugin event stream must end when the document closes, when the viewer is disposed and when the caller cancels, and must never block the Rust thread that delivers. Task 1 pins all three with a fake listener and Task 2 against the real metadata plugin.
3. A `RenderedImage` whose stride exceeds `width * 4` keeps its row padding in `PixelBuffer`; the rotated copy (`PageGeometry.ToDisplay(PixelBuffer)`) must address rows by stride. Task 5 pins it with a padded buffer.
4. A negative index or concurrency (`GoToHitAsync(-1)`, `MovePaneAsync(id, -1)`, `RenderPagesAsync(..., concurrency: -1)`) throws `invalid-input` before posting. Tasks 4, 6 and 10 pin it.
5. Shell props given as a `JsonTypeInfo<T>` round trip without reflection (AOT). Task 11 pins it with a source-generated context in the test assembly.

---

### Task 1: Plugin plumbing and shared types

**Files:**
- Create: `EmbedPDF.Viewer/Plugins/PluginEvents.cs`, `EmbedPDF.Viewer/Plugins/Shared.cs`, `EmbedPDF.Viewer/Plugins/Shared.Ffi.cs`; `EmbedPDF.Viewer.Tests/PluginEventsTests.cs`
- Modify: `PdfViewer.cs` (stream registry finished on dispose)

**Interfaces:**
- Internal `PdfViewer.Stream<TFfi, TPublic>(Func<EventRelay<TFfi, TPublic>, GF.Subscription> subscribe, Func<TFfi, TPublic> map, CancellationToken)` returns an `IAsyncEnumerable<TPublic>`; internal abstract `EventRelay<TFfi, TPublic>` with `Deliver(TFfi)` and `Finish()`; each plugin adds a one-line subclass implementing its generated listener. A mapping that throws drops that event and logs it.
- Shared public types: `ResourceStatus {Idle, Loading, Ready, Forbidden, Error}`; `DocumentEventOrigin(DocumentEventOriginKind Kind, string SessionId, string? Subject, string At, long? ServerSequence, DocumentEventTransaction? Transaction, string? UndoOf)`, `DocumentEventTransaction(string Id, int Index, int Count)`, `enum DocumentEventOriginKind {Local, Remote}`; `readonly record struct PdfQuad(PdfPoint UpperLeft, PdfPoint UpperRight, PdfPoint LowerLeft, PdfPoint LowerRight)` with `PdfQuad(PdfRect)` (the `CoreGraphicsBridges.swift` `Quad(Rect)` port); `AnnotationRef` union (`ObjectNumber(PageRef Page, int AnnotObjectNumber)`, `Index(PageRef Page, int IndexValue)`); `PageDestination` and `PageLinkTarget` unions as `epdf_ffi`; `LinkActivation` union as `epdf_plugin_link`; `PdfActionTree : IDisposable` over the generated object with `IsIncomplete`, `Json`, `RootType` (wave B extends it).

- [x] **Step 1: Failing tests.** `PluginEventsTests`: a fake subscribe whose relay gets three events yields them in order; `Finish` ends enumeration; cancelling ends it with `OperationCanceledException` and cancels the generated subscription once; disposing the viewer ends a live stream and a stream opened after disposal ends at once; `Deliver` with no reader returns at once 10,000 times. `MappingTests` additions: `PdfQuad(new PdfRect(1, 2, 3, 4))` is `(1,2),(4,2),(1,6),(4,6)`; every `PageLinkTarget` and `LinkActivation` case maps.
- [x] **Step 2:** Run. Expected: build FAIL.
- [x] **Step 3:** Implement.
- [x] **Step 4:** PASS, 0 warnings.
- [x] **Step 5:** Commit `feat(dotnet): add plugin event streams and shared plugin types`.

### Task 2: Metadata

**Files:** `Plugins/Metadata.cs`, `Plugins/Metadata.Ffi.cs`, `MetadataTests.cs`; modify `DocumentAPITests.cs` and `ViewerTests.cs` to the public accessor.

**Interfaces:** `GetMetadataAsync(DocumentInfo)`. `MetadataCapability`: `GetSnapshotAsync` (`DocumentMetadata?`), `GetStatusAsync`, `CanUpdateAsync`, `EnsureLoadedAsync`, `UpdateAsync(MetadataPatch)` (`DocumentMetadata`), `RefreshAsync`, `GetCustomSnapshotAsync` (`IReadOnlyDictionary<string, string>?`), `GetCustomStatusAsync`, `UpdateCustomAsync(IReadOnlyDictionary<string, string?>)`, `RefreshCustomAsync`, `ObserveEvents`. Records `DocumentMetadata`, `MetadataPatch`, `MetadataField`, `DocumentMetadataTrapped`, `MetadataEvent` (`Updated`, `Resynced`, `CustomUpdated`, `CustomResynced`), `MetadataUpdatedEvent`, `CustomMetadataUpdatedEvent`.

- [x] **Step 1:** Port `MetadataTests.swift`; add review focus 1 and 2 against the real plugin.
- [x] **Step 2-4:** FAIL, implement, PASS.
- [x] **Step 5:** Flip the 15 metadata rows; commit `feat(dotnet): add the metadata capability`.

### Task 3: History

Ran after Task 9: `HistoryTests.swift` edits pages.


`GetHistoryAsync`; `HistoryCapability`: `CanUndoAsync`, `CanRedoAsync`, `GetUndoLabelAsync`, `GetRedoLabelAsync` (`HistoryChangeLabel?`), `UndoAsync`, `RedoAsync`, `ClearAsync`, `GetSettingsAsync`, `UpdateSettingsAsync(HistorySettingsPatch)`, `ResetSettingsAsync`, `ObserveEvents`. `HistoryEvent` (`Undone(Label, Redo, long Skipped)`, `UndoFailed(Label, Redo, HistoryUndoFailure Reason, PluginErrorInfo Error)`, `SettingsChanged(HistorySettings, IReadOnlyList<string> Changed)`), `HistoryLabelValue` (`Text`, `Number`). Port `HistoryTests.swift` (needs metadata edits for undo). Flip 13 rows. Commit `feat(dotnet): add the history capability`.

### Task 4: Search

`GetSearchAsync`; `SearchConfig` gains `SearchRevealOptions? Reveal`. `SearchCapability`: settings trio, `CanSearchAsync(bool snippets = false)`, `SearchAsync(SearchQuery, PageRef? from = null)` (cancellable, `SearchResult`), `RefreshAsync`, `CancelAsync`, `ClearAsync`, `NextHitAsync`/`PreviousHitAsync(SearchRevealOptions? reveal = null)`, `GoToHitAsync(int index, SearchRevealOptions? reveal = null)`, `RevealActiveHitAsync`, `GetQueryAsync`, `GetStatusAsync`, `GetHitsAsync(PageRef? page = null)`, `GetHitCountAsync(PageRef? page = null)` (`int`), `GetPagesWithHitsAsync`, `GetActiveHitIndexAsync` (`int?`), `GetActiveHitAsync`, `GetProgressAsync`, `GetErrorAsync` (`PluginErrorInfo?`), `FindAllAsync(SearchQuery, ContentInclusion snippets = WhenPermitted)` (cancellable), `ObserveEvents`. Records as `epdf_plugin_search`. Tests: port `SearchTests.swift`, the search rows of `PluginSettingsTests.swift`, `GoToHitAsync(-1)` is `invalid-input`. Flip 31 rows. Commit `feat(dotnet): add the search capability`.

### Task 5: PixelBuffer and geometry bridges

**Files:** `PixelBuffer.cs`, modify `PageGeometry.cs`; `PixelBufferTests.cs`, `PageGeometryTests.cs` (image cases).

**Interfaces:** `public sealed class PixelBuffer(byte[] pixels, int width, int height, int stride, PixelFormat format, bool isPremultiplied)` exposing `ReadOnlyMemory<byte> Pixels`, `Width`, `Height`, `Stride`, `Format`, `IsPremultiplied`, `GetRow(int y)` (`ReadOnlySpan<byte>` of `Width * 4` bytes), `ToPacked()` (stride `Width * 4`), `ToPremultiplied()`; `enum PixelFormat {Rgba8, Bgra8}`; `ToBgra()` swaps channels. Constructor checks `stride >= width * 4` and `pixels.Length >= stride * height` and throws `ArgumentException` (changed in execution: it validates the caller's array, not an engine answer). `PageGeometry.ToDisplay(PixelBuffer)` returns the buffer itself for `None`, otherwise a packed turned copy, as `PageGeometry.swift:109`.

- [x] Tests: port the image cases of `PageGeometryTests.swift` (a 2x1 buffer turned 90 is 1x2 with pixels in the turned order, 180 reverses, 270) plus a padded-stride case (review focus 3); `ToPremultiplied` halves color at alpha 128; `ToBgra` swaps R and B; a short buffer throws. Commit (done). `feat(dotnet): add PixelBuffer and the image overload of PageGeometry`.

### Task 6: Render

`GetRenderAsync`; `RenderConfig` gains `RenderFullPageOptions? FullPage`, `bool? Debug`. `RenderedPage(int Width, int Height, double Scale, PixelBuffer Image)` (straight-alpha RGBA, the engine's format); `RenderedPageResult`, `RenderBatchResult(Applied, Failed)`, `RenderSnapshot` (`Page`, `Rect`, `GetImageAsync`, disposable). `RenderCapability`: settings trio, `CanRenderAsync`, `CanRenderThumbnailAsync`, `RenderPageAsync(PageRef, RenderPageOptions? options = null)`, `RenderThumbnailAsync(PageRef, ContentInclusion annotations, ContentInclusion formFields)`, `RenderPagesAsync(IEnumerable<PageRef>, RenderPageOptions? options = null, int concurrency = 4)`, `GetRenderEpochAsync` (`long`), `InvalidateAsync(IEnumerable<PageRef>? pages = null, RenderInvalidateScope? scope = null)`, `GetRenderPolicyAsync`, `RenderSourceAsync`, `GetSourceKeyAsync`, `ConformViewportAsync`, `GetPaintSettingsAsync`, `CreateViewDemandAsync(string viewId)`, `CanSnapshotAsync`, `GetSnapshotAsync`, `ClearSnapshotAsync`, `GetSnapshotBoxAsync(PageRef)`, `ObserveSnapshotChanges` (`IAsyncEnumerable<RenderSnapshot?>`), `ObserveEvents`. `RenderViewDemand : IAsyncDisposable`: `SetDemandAsync`, `GetPlanAsync`, `GetTileImageAsync`, `MarkPaintedAsync`, `MarkUnpaintedAsync`, `ReleaseAsync`, `DisposeAsync` (the Rust `dispose`). Tests: port `RenderTests.swift`, `RenderSnapshotTests.swift` (as far as no interaction tool is needed), `RenderViewportTests.swift`, render rows of `PluginSettingsTests.swift`, concurrency -1 is `invalid-input`; `LifetimeTests` renders through the public capability. Flip 31 rows. Commit `feat(dotnet): add the render capability`.

### Task 7: Outline

`GetOutlineAsync`; members as `Outline.swift` with C# names (`ListAsync`, `GetItemAsync`, `GetChildrenAsync(OutlineItemRef? parent = null)`, `ListRowsAsync`, `GetCurrentItemAsync`, `GetSelectedAsync`, `SelectAsync`, `GetStatusAsync`, `IsIncompleteAsync`, `IsExpandedAsync`, `CanEditAsync`, `GetCurrentViewTargetAsync`, `ActivateAsync`, `EnsureLoadedAsync`, `RefreshAsync`, `CreateAsync(OutlineItemInput, OutlineCreateOptions? options = null)`, `UpdateAsync`, `MoveAsync(refs, OutlineItemRef? parent, OutlinePosition? position = null)` (null is end), `DeleteAsync`, `ExpandAsync`, `CollapseAsync`, `ToggleAsync`, `RevealAsync`, `ExpandAllAsync`, `CollapseAllAsync`, `ObserveEvents`). `OutlineItem` holds `PdfActionTree? Action`. Port `OutlineTests.swift`. Flip 27 rows. Commit.

### Task 8: Attachment

`GetAttachmentsAsync`; members as `Attachment.swift` (`ListAsync`, `GetAsync(string id)`, `GetSelectedAsync`, `SelectAsync`, `IsPendingAsync`, `GetStatusAsync`, `CanDownloadAsync`, `CanAddAsync`, `CanRemoveAsync`, `RevealAsync`, `EnsureLoadedAsync`, `RefreshAsync`, `AddAsync(IEnumerable<AttachmentFileSource>)`, `RemoveAsync`, `DownloadAsync`, `ObserveEvents`). `AttachedFile.SizeBytes` is `long?`; data is `ReadOnlyMemory<byte>` in sources and `byte[]` in content. Port `AttachmentTests.swift`. Flip 17 rows. Commit.

### Task 9: Page edit

`GetPageEditAsync`; `GetSelectedAsync`, `SelectAsync`, `CanEditAsync`, `CanExtractAsync`, `RotateAsync(pages, PageEditTurn)`, `SetRotationAsync(PageEditRotation, pages)`, `MovePagesAsync(pages, PageEditPlacement)`, `DeletePagesAsync`, `InsertBlankAsync(PageEditBlankOptions? options = null)`, `InsertAsync(ReadOnlyMemory<byte>, PageEditBytesOptions? options = null)`, `InsertAsync(IEnumerable<PageRef>, DocumentInfo from, PageEditPlacement? placement = null)`, `DuplicateAsync`, `ExtractAsync` (`byte[]`). Port `PageEditTests.swift`. Flip 13 rows. Commit.

### Task 10: View manager

`GetViewManagerAsync()`; members as `ViewManager.swift` (`ListPanesAsync`, `GetPaneAsync(string id)`, `GetPaneOrderAsync`, `GetFocusedPaneIdAsync`, `GetPaneOfDocumentAsync(DocumentInfo)`, `CreatePaneAsync(IEnumerable<DocumentInfo>? documents = null)`, `RemovePaneAsync(id, string? movingDocumentsTo = null)`, `MovePaneAsync(id, int index)`, `SetFocusedPaneAsync(string?)`, `SplitPaneAsync(DocumentInfo, string? from = null)`, `SetActiveDocumentAsync(DocumentInfo?, string paneId)`, `AddDocumentAsync(DocumentInfo, string paneId, int? index = null)`, `RemoveDocumentAsync`, `MoveDocumentAsync(DocumentInfo, string paneId, int index)` (within), `MoveDocumentAsync(DocumentInfo, string from, string to, int? index = null)` (between), `ObserveEvents`). Port `ViewManagerTests.swift`; `MovePaneAsync(id, -1)` is `invalid-input`. Flip 19 rows. Commit.

### Task 11: Shell

`GetShellAsync`; members as `Shell.swift`, with props as `string? propsJson` or a `JsonTypeInfo<TProps>` overload (`OpenAsync<TProps>(id, TProps props, JsonTypeInfo<TProps> typeInfo, string? exclusive = null)`, same for `ToggleAsync` and `UpdatePropsAsync`); `ShellSurface.GetProps<T>(JsonTypeInfo<T>)` and the same on `ShellSurfaceOpenedEvent`. Encoding failure is `("EncodingError", invalid-input, ...)`. Port `ShellTests.swift`, plus review focus 5. Flip 20 rows. Commit.

### Task 12: Accessor tables, null checks, verify and record

- [x] Switch the wave A rows of `DisabledPluginTests` and `ViewerConfigBuilderTests` to the public accessors; extend `NullArgumentTests` to every wave A capability; port the plugin cases of `IntegerSurfaceTests.swift` that wave A owns.
- [x] Run `dotnet test EmbedPDF.Viewer.Tests` twice, `EmbedPDF.Tests`, `python3 -m unittest discover -s platforms/dotnet/packaging`, `python3 -m unittest scripts/test_check_api_coverage.py`, `python3 scripts/check-api-coverage.py --platform csharp`, `dotnet build -c Release` with 0 warnings.
- [x] Tick this plan, add "Phase 3 wave A notes" to the umbrella. Commit `chore(dotnet): record Phase 3 wave A`.
