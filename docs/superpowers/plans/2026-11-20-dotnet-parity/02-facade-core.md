# Phase 2: Facade Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A public `PdfViewer` in the `EmbedPDF` assembly that starts, opens documents from bytes, a stream, a file path, a URI and a saved layer, and retries, renames, reorders, saves and closes them, with cancellation, streams, typed errors, a DI extension and a C# API coverage check.

**Architecture:** Hand-written public records and wrappers over the internal generated bindings (D3). Every async member takes a `CancellationToken`; a member whose Rust export takes a `CancelToken` bridges the .NET token to it, the others check the token before posting and then run to completion, as Swift's do. Streams are `IAsyncEnumerable<T>` objects that subscribe when the method returns, each over its own bounded `Channel<T>` fed by one hub. `PdfViewer` owns the host: `DisposeAsync` cancels and drains calls in flight, destroys the kernel, then shuts the host down off the caller's thread; a finalizer only queues that release to the thread pool.

**Tech Stack:** .NET SDK 10.0.403 `net10.0`, xUnit 2.9.3, `Microsoft.Extensions.Logging.Abstractions` 10.0.0, `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.0 (tests: `Microsoft.Extensions.DependencyInjection` 10.0.0), `Microsoft.CodeAnalysis.PublicApiAnalyzers` 5.6.0, Python 3.

**Spec:** `docs/superpowers/plans/2026-11-20-dotnet-parity.md` (D3, D4, D5 document fetch row, D8, Review Focus 1 and 2, "Phase 2"), and the Swift sources it names under `platforms/apple/EmbedPDF/Sources/EmbedPDF/` with their tests under `platforms/apple/EmbedPDF/Tests/EmbedPDFTests/`.

## Global Constraints

- All work in the dev container `embedpdf-dotnet-parity`, repo `/workspaces/embed-pdf-viewer`. Guard script before every heavy build or test run.
- Branch `research/dotnet-parity-p2` from `research/dotnet-parity-p1` (`a54e4cd52`). Commits by explicit path, `feat(dotnet): ...`, `test(dotnet): ...` or `chore(dotnet): ...`, no trailers. No push, no merge.
- No TypeScript or `packages/**` change; fixtures under `packages/` are read, never edited. No plugin Rust change, no `ffi.rs` change.
- `platforms/dotnet/EmbedPDF` (`EmbedPDF.Engine`) untouched; `EmbedPDF.Tests` and `packaging/test_package.py` stay green.
- Facade: `platforms/dotnet/EmbedPDF.Viewer/` (assembly and package `EmbedPDF`, namespace `EmbedPDF`). Tests: `platforms/dotnet/EmbedPDF.Viewer.Tests/`.
- Generated C# stays `internal`; no generated type appears in a public signature.
- Public integers are `int` or `long`, never `uint` or `ulong` (D4).
- `IsAotCompatible`, `TreatWarningsAsErrors`, `Nullable` on every assembly; 0 warnings. `PublicAPI.Unshipped.txt` changes in the same commit as the surface (D8).
- Prose and comments name no outside product, company or person. Docs: no em dashes.

## Review Focus

1. Cancelling `OpenAsync(Uri)` while the transport stalls must cancel the Rust token, close the loading tab, cancel the HTTP request, and leave no completion called twice or after disposal. Task 7 pins it against a stalling loopback `HttpListener`.
2. `DisposeAsync` with renders in flight must not hang or leak: calls in flight end, a second concurrent `DisposeAsync` returns the same task, and a viewer dropped without disposal is released by the GC without blocking the finalizer thread. Task 7 pins it.
3. A missing native runtime must reach the caller of `new PdfViewer(...)`, `PdfViewer.CreateAsync` and `NativeRuntime.ReadPageCountAsync` as `NativeRuntimeNotFoundException`, never as `TypeInitializationException`. Task 6 pins it with an `AsyncLocal` load seam.
4. A subscriber that stops reading must never block the Rust thread that delivers revisions, errors or document events: every channel is bounded with `DropOldest`, and writes use `TryWrite`. Task 3 pins it by writing past capacity with no reader.
5. A call after disposal answers `PdfException` with `instance-closed`, not `ObjectDisposedException` from the generated handle. Task 6 pins it.

---

### Task 1: Errors and wire integers

**Files:**
- Create: `EmbedPDF.Viewer/PdfErrorCode.cs`, `EmbedPDF.Viewer/WireInteger.cs`, `EmbedPDF.Viewer.Tests/PdfErrorTests.cs`, `EmbedPDF.Viewer.Tests/IntegerSurfaceTests.cs` (unit half)
- Modify: `EmbedPDF.Viewer/PdfException.cs`, `PublicAPI.Unshipped.txt`

**Interfaces:**
- `public readonly record struct PdfErrorCode(string Value)`: statics `PermissionDenied`, `Unsupported`, `NotFound`, `NotReady`, `InvalidInput`, `Conflict`, `InstanceClosed`, `OperationCancelled`, `OperationFailed`, `InvalidState` with the Swift wire strings; `implicit operator PdfErrorCode(string)`; `ToString()` is `Value`.
- `PdfException`: `string Name`, `PdfErrorCode Code`, `string Detail`; `Message` is `Detail`. Ctors: `()`, `(string message)`, `(string message, Exception inner)` (code `operation-failed`, name the type's name), `(PdfErrorCode code, string detail, string name = "PluginError")`, `(string name, PdfErrorCode code, string detail)`, `(string name, PdfErrorCode code, string detail, Exception? inner)`.
- Internal `PdfException.From(Exception)`: generated `ViewerException.Kernel` maps name, code (null to `operation-failed`) and detail; `ViewerException.Closed` and `ObjectDisposedException` to `("Closed", instance-closed, "The viewer has shut down.")`; `OperationCanceledException` to `("AbortError", operation-cancelled, "operation cancelled")`; a `PdfException` passes through; anything else `(type name, operation-failed, Message)`.
- Internal `WireInteger`: `uint ToUInt32(int value, string argument)`, `long ToInt64(int value)`, `int ToInt(ulong|uint|long value, string result)`. Out of range input throws `("RangeError", invalid-input, "<argument> <value> is outside 0...4294967295")`; an engine value past `int` throws `invalid-state`.

- [ ] **Step 1: Failing tests.** `PdfErrorTests`: each known code round trips its wire string (theory over the ten); `quota-exceeded` round trips and is none of them; a string compares with a code (`error.Code == "not-found"`); the short constructor names it `PluginError` and the long one keeps `Custom`; `catch (PdfException e) when (e.Code == PdfErrorCode.NotFound)` picks the right branch for not-found, permission-denied and conflict; `Message` is the detail; `From` maps a generated `Kernel` with a null code to `operation-failed`, `Closed` and `ObjectDisposedException` to `instance-closed`, and `OperationCanceledException` to `operation-cancelled`. `IntegerSurfaceTests`: `ToUInt32(-1, "index")` throws invalid-input with detail `index -1 is outside 0...4294967295`; `ToUInt32(0)` is 0 and `ToUInt32(int.MaxValue)` passes; `ToInt(ulong.MaxValue, "count")` throws invalid-state; `ToInt(uint.MaxValue)` throws invalid-state (C# `int` is 32-bit, unlike Swift's `Int`); `ToInt(-1L)` is -1.
- [ ] **Step 2:** `dotnet test EmbedPDF.Viewer.Tests`. Expected: build FAIL.
- [ ] **Step 3:** Implement. Disable test parallelization for the assembly (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`), as Swift's `BaseSuite` is `.serialized`.
- [ ] **Step 4:** Tests PASS, 0 warnings.
- [ ] **Step 5:** Commit `feat(dotnet): add PdfErrorCode and the typed PdfException`.

### Task 2: Value types and their mappings

**Files:**
- Create: `EmbedPDF.Viewer/Documents.cs` (`DocumentInfo`, `DocumentStatus`, `PluginErrorInfo`, `PageRef`, `PageInfo`, `SaveMode`, `DocumentEvent`), `ViewerSettings.cs` (`ViewerSettings`, `ViewerSettingsPatch`, `ViewerSettingsField`), `Geometry.cs` (`PdfPoint`, `PdfSize`, `PdfRect`, `RenderRotation`, `RenderRotationExtensions`), `PageGeometry.cs`, `PageRenderResolution.cs`, `DocumentPermission.cs`, `ContentInclusion.cs`, `Registration.cs`, `Ffi.cs` (internal mappings both ways); tests `PageGeometryTests.cs`, `PageRenderResolutionTests.cs`, `MappingTests.cs`

**Interfaces:**
- Records: `DocumentInfo(string Id, string? Name, int PageCount, DocumentStatus Status, PluginErrorInfo? Error)`; `PluginErrorInfo(PdfErrorCode Code, string Message, string Capability, string? Permission)`; `readonly record struct PageRef(int PageObjectNumber)`; `PageInfo(PageRef Page, int Index, double Width, double Height, int Rotation)` with `PageGeometry Geometry`; `ViewerSettings(string? Accent, string? PageBackground, string? PageShadow)`; `ViewerSettingsPatch { Accent, PageBackground, PageShadow, IReadOnlyList<ViewerSettingsField> Clear }` (init properties). `abstract record DocumentEvent` with nested sealed records `Opened(DocumentInfo Document)`, `OpenFailed(string DocumentId, PluginErrorInfo Error)`, `Locked(string DocumentId, bool PasswordProvided)`, `Closed(string DocumentId)`, `ActiveChanged(string? DocumentId)`, `PagesChanged(string DocumentId)`. Enums `DocumentStatus {Loading, Locked, Ready, Failed}`, `SaveMode {Incremental, Rewrite}`, `ViewerSettingsField {Accent, PageBackground, PageShadow}`, `RenderRotation {None, Clockwise90, Clockwise180, Clockwise270}`.
- Geometry: `readonly record struct PdfPoint(double X, double Y)`, `PdfSize(double Width, double Height)`, `PdfRect(double X, double Y, double Width, double Height)` (the `Pdf` prefix avoids clashing with UI framework `Point` types). `RenderRotationExtensions`: `FromDegrees(int)`, `FromDegrees(double)`, `Degrees(this RenderRotation)`, `IsQuarterTurn(this RenderRotation)`, rounding as `PageGeometry.swift`.
- `readonly record struct PageGeometry(RenderRotation Turn, PdfSize PageSize)` with ctor `(int rotation, PdfSize pageSize)`, `Rotation`, `DisplaySize`, `ToDisplay`/`ToPage` for `PdfPoint` and `PdfRect`. The `CGImage` overload waits for Wave A's `PixelBuffer`.
- `readonly record struct PageRenderResolution(double DeviceWidth, bool IsFull)`: `OffScreenDeviceWidth = 256`, `DeviceWidthStep = 128`, `ForPage(double viewWidth, double displayScale, double zoomScale = 1, bool isOnScreen = true)`, `BandedScale(double scale, double pageWidth)`, `UnrotatedDeviceWidth(double unrotatedWidth, double displayedWidth)`; Swift's `.rounded()` is `MidpointRounding.AwayFromZero`, `.rounded(.up)` is `Math.Ceiling`.
- `readonly record struct DocumentPermission(string Value)` with the 29 Swift constants (`Open` ... `AnnotationsCreate`) and `IReadOnlyList<DocumentPermission> Named` in Swift's order; implicit from string.
- `enum ContentInclusion { WhenPermitted, Always, Never }`, internal `ToEngine()` gives `null`, `true`, `false`.
- `sealed class Registration : IDisposable`: internal ctor over the generated `RegistrationHandle`; `Unregister()` idempotent; `Dispose()` unregisters. No finalizer: dropping it leaves the registration in place, as Swift's.
- `JSONValue.swift` maps to `System.Text.Json.Nodes.JsonNode` where Wave D needs it; Phase 2 adds no type.
- Internal `Ffi` static class: `ToPublic(...)` for each generated record above, and `ToFfi(...)` for `PageRef`, `SaveMode`, `ViewerSettingsPatch`. A `uint` the engine answers converts with `WireInteger.ToInt`.

- [ ] **Step 1: Failing tests.** Port `PageGeometryTests.swift` and `PageRenderResolutionTests.swift` without the Core Graphics cases. `MappingTests`: a generated `DocumentInfo` with a failed error maps every field; every `DocumentEvent` variant maps to its nested record; a patch maps `Clear` in order; `DocumentPermission.Named` has 29 distinct values starting `doc.open`; `ContentInclusion` maps to `null/true/false`.
- [ ] **Step 2:** Run. Expected: build FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Tests PASS, 0 warnings.
- [ ] **Step 5:** Commit `feat(dotnet): add the facade's document, page and geometry types`.

### Task 3: Broadcast hub and subscriptions

**Files:**
- Create: `EmbedPDF.Viewer/BroadcastHub.cs`, `EmbedPDF.Viewer.Tests/RevisionHubTests.cs`

**Interfaces:**
- Internal `sealed class BroadcastHub<T>(int capacity)`: `IAsyncEnumerable<T> Subscribe(CancellationToken cancellationToken = default)` installs a subscriber with its own `Channel.CreateBounded<T>(capacity, DropOldest, SingleReader)` before it returns; `void Publish(T value)` uses `TryWrite` and never blocks; `void Complete()` completes every channel and makes later subscriptions complete at once; a subscriber is removed when its enumerator is disposed or its token fires. Enumerating one subscription twice throws `InvalidOperationException`. Cancellation ends enumeration with `OperationCanceledException`. Revisions use capacity 1, reported errors 16, document events 1024.
- Internal `sealed class Subscription<T> : IAsyncEnumerable<T>` is what `Subscribe` returns, so subscribing is eager, not deferred to the first `MoveNextAsync`.

- [ ] **Step 1: Failing tests** (port `RevisionHubTests.swift`): two subscribers both read 2 after publishing 1 and 2 (coalescing); a cancelled subscriber leaves the survivor live for 20 rounds; `Complete` ends every subscriber and a later `Subscribe` ends at once; 20 errors into a capacity-16 hub with no reader yield the newest 16 in order (`StreamRuleTests.reportedErrorsKeepTheNewestSixteenPerObserver`); `Publish` past capacity with no reader returns at once (Review Focus 4); enumerating one subscription twice throws.
- [ ] **Step 2:** Run. Expected: build FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** PASS.
- [ ] **Step 5:** Commit `feat(dotnet): fan state revisions out over bounded channels`.

### Task 4: Options and the fluent builder

**Files:**
- Create: `EmbedPDF.Viewer/PdfViewerOptions.cs`, `PdfViewerOptionsBuilder.cs`, `PluginConfigs.cs`, `EmbedPDF.Viewer.Tests/ViewerConfigBuilderTests.cs`
- Modify: `EmbedPDF.Viewer/EmbedPDF.Viewer.csproj` (add `Microsoft.Extensions.Logging.Abstractions` 10.0.0)

**Interfaces:**
- `sealed record PdfViewerOptions` with init properties named after the generated `ViewerConfig` fields: `Scope` (`IReadOnlyList<string>?`), `Metadata`, `Search`, `Render`, `Interaction`, `Feedback`, `Stage`, `Selection`, `Link`, `Annotation` (`string?`, settings JSON, `""` is the defaults), `History`, `Outline`, `Attachment`, `Form`, `Redaction`, `Measurement`, `Stamp`, `PageEdit`, `ViewManager`, `Shell`, `I18n`, `Commands`, `StandardCommands` (`IReadOnlyList<StandardCommandList>?`), `Actions`, `Signature`; plus `IDocumentFetcher? DocumentFetcher` (Task 5) and `ILogger Logger` (default `NullLogger.Instance`). Internal `ViewerConfig ToFfi()`.
- Plugin configs: `sealed record SearchConfig`, `RenderConfig`, `StageConfig`, `FormConfig`, `RedactionConfig`, `MeasurementConfig`, `StampConfig`, `I18nConfig`, `ActionsConfig`, `SignatureConfig`, `FeedbackConfig`, each with no members yet, and `enum StandardCommandList` matching the generated one. Each maps to the generated config with its defaults; `FeedbackConfig` passes an internal no-op `PlatformFeedback` (D5). Their fields port with the owning Phase 3 wave and stay `pending` in the coverage manifest.
- `sealed class PdfViewerOptionsBuilder`: `()`, `(PdfViewerOptions)`, `Build()`, one method per field returning a new builder (`Scope`, `Metadata(bool enabled = true)`, `Search(SearchConfig)`, ..., `Annotation(string settingsJson = "")`, `StandardCommands(IEnumerable<StandardCommandList>)`), plus `DocumentFetcher(IDocumentFetcher)` and `Logger(ILogger)`.

- [ ] **Step 1: Failing tests** (port `ViewerConfigBuilderTests.swift`): rows for the 25 fields; every generated `ViewerConfig` property (reflection over the internal record) has exactly one row; `new PdfViewerOptionsBuilder().Build().ToFfi()` equals `new ViewerConfig()` field by field (rendered as text, configs compared by null-ness); each row sets its field and no other; a builder is a value (`base.Selection()` leaves `base` unchanged); a builder starts from existing options.
- [ ] **Step 2:** Run. Expected: build FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** PASS.
- [ ] **Step 5:** Commit `feat(dotnet): add PdfViewerOptions and its fluent builder`.

### Task 5: Document fetch

**Files:**
- Create: `EmbedPDF.Viewer/DocumentFetch.cs` (`IDocumentFetcher`, `DocumentFetchResponse`, `HttpClientDocumentFetcher`, internal `FetcherAdapter`), `EmbedPDF.Viewer.Tests/HttpClientDocumentFetcherTests.cs`, `EmbedPDF.Viewer.Tests/Loopback.cs`

**Interfaces:**
- `public interface IDocumentFetcher { Task<DocumentFetchResponse> FetchAsync(string url, CancellationToken cancellationToken); }`. Thrown errors become the kernel's `HostFailure`: a `PdfException` keeps name, code and detail; `OperationCanceledException` is `AbortError`/`operation-cancelled`; anything else is its type name with no code and its message, which the kernel reports as `operation-failed`.
- `public sealed record DocumentFetchResponse(int StatusCode, byte[] Content)`.
- `public sealed class HttpClientDocumentFetcher(HttpClient? client = null) : IDocumentFetcher`: a URL that is not absolute throws `("InvalidURL", invalid-input, "The document URL is invalid.")`; `file:` URIs read the file; others `GET` and answer the status with the body whatever the status (the kernel maps refusals); a status outside 0 to 65535 throws `("InvalidResponse", operation-failed, "The response has an invalid HTTP status.")`. Default client: one shared static `HttpClient`.
- Internal `FetcherAdapter : DocumentFetcher` (generated): runs `FetchAsync` with `Task.Run` so a fetcher never blocks the kernel thread, completes the generated completion exactly once (an `Interlocked` flag), disposes it, and returns a cancellation that cancels the fetch's `CancellationTokenSource`.

- [ ] **Step 1: Failing tests** against `Loopback` (an `HttpListener` on `127.0.0.1` and a free port, with handlers that answer, answer 404, or stall until released): 200 returns status and bytes; 404 returns 404 and the body; a relative URL throws invalid-input; cancelling a stalled fetch throws `OperationCanceledException`; the adapter calls `Finish` once and maps a thrown `PdfException`, a cancellation and an `HttpRequestException` to the failures above (a recording `IDocumentFetchCompletion` test double through the internal seam).
- [ ] **Step 2:** Run. Expected: build FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** PASS.
- [ ] **Step 5:** Commit `feat(dotnet): add HttpClientDocumentFetcher`.

### Task 6: PdfViewer

**Files:**
- Create: `EmbedPDF.Viewer/PdfViewer.cs`, `PdfViewer.Documents.cs`, `Telemetry.cs`, `Experimental.cs`; tests `Fixtures.cs`, `ViewerTests.cs`, `DocumentAPITests.cs`, `FetcherExportTests.cs`, `StreamRuleTests.cs`, `NativeLoadTests.cs`, the viewer half of `IntegerSurfaceTests.cs`, `TelemetryTests.cs`
- Modify: `NativeRuntime.cs` (`AsyncLocal` load seam for tests), `PublicAPI.Unshipped.txt`

**Interfaces:**
- `public sealed class PdfViewer : IAsyncDisposable`: `PdfViewer(PdfViewerOptions? options = null)`; `static Task<PdfViewer> CreateAsync(PdfViewerOptions? options = null, CancellationToken cancellationToken = default)` (constructs and starts); `Task StartAsync(CancellationToken)`; `ValueTask DisposeAsync()`.
- Open: `OpenAsync(ReadOnlyMemory<byte> pdf, string? name = null, string? password = null, bool activate = true, CancellationToken)`; `OpenAsync(Stream, ...)`; `OpenFileAsync(string path, ...)` (name defaults to the file name); `OpenAsync(Uri, ...)` (bridges to the Rust `CancelToken`); `OpenLayerAsync(ReadOnlyMemory<byte> basePdf, ReadOnlyMemory<byte> layer, ...)` (bridged). All return `Task<DocumentInfo>`.
- Documents: `RetryAsync(DocumentInfo)`, `RenameAsync(DocumentInfo, string)`, `CloseAsync(DocumentInfo)`, `CloseAllAsync()`, `UnlockAsync(DocumentInfo, string password)`, `GetDocumentAsync(string? id = null)`, `HasDocumentAsync(string id)`, `GetDocumentCountAsync()` (`int`), `GetDocumentsAsync()`, `GetActiveDocumentAsync()`, `GetActiveDocumentIdAsync()`, `SetActiveAsync(DocumentInfo)`, `GetDocumentOrderAsync()`, `SetDocumentOrderAsync(IEnumerable<string>)`, `MoveAsync(DocumentInfo, int index)`, `SwapAsync(DocumentInfo, DocumentInfo)`, `GetPagesAsync(DocumentInfo)`, `GetPageAsync(PageRef, DocumentInfo? document = null)`, `GetPageAtAsync(int index, DocumentInfo? document = null)`, `GetPageIndexAsync(PageRef, DocumentInfo? = null)` (`int`, -1 when missing), `GetPageRevisionAsync(DocumentInfo? = null)` (`long`), `AllowsAsync(DocumentPermission, DocumentInfo? = null)`, `CanDownloadAsync(DocumentInfo? = null)`, `SaveAsync(DocumentInfo, SaveMode mode = SaveMode.Incremental)` and `SaveLayerAsync(DocumentInfo)` (both bridged, `Task<byte[]>`), `GetSettingsAsync()`, `UpdateSettingsAsync(ViewerSettingsPatch)`, `ResetSettingsAsync()`. Every one takes `CancellationToken cancellationToken = default` last.
- Streams: `IAsyncEnumerable<long> ObserveStateRevisions(CancellationToken = default)`, `IAsyncEnumerable<PdfException> ObserveReportedErrors(CancellationToken = default)`, `IAsyncEnumerable<DocumentEvent> ObserveDocumentEvents(CancellationToken = default)`. The document stream holds a generated `Subscription` and cancels it when the enumerator ends.
- Internal: `ViewerHost Host`; `Task<T> CallAsync<T>(Func<ViewerHost, Task<T>> call, CancellationToken)` and `CallCancellableAsync<T>(Func<ViewerHost, CancelToken, Task<T>>, CancellationToken)`, which check disposal, count the call in flight, map errors with `PdfException.From`, and turn `operation-cancelled` into `OperationCanceledException(token)` when the caller's token fired (the `PdfException` as inner).
- Telemetry: internal `Telemetry.Source = new ActivitySource("EmbedPDF")`, `Telemetry.Meter = new Meter("EmbedPDF")`, histogram `embedpdf.call.duration` (ms, tag `call`); `open`, `pages` and `save` run in an activity of that name, as `Signposts.swift` measures them.
- `Experimental.cs`: `internal const string Diagnostic = "EPDF001"`, the id `[Experimental]` uses. The generated bindings carry no experimental bridge exports (the host is built without `manual-bridge`), so no member wears it yet.
- Every public entry point calls `NativeRuntime.EnsureLoaded()` before touching generated code. Internal `NativeRuntime.LoadOverride` (`AsyncLocal<Action?>`) runs in place of the real load when set.

- [ ] **Step 1: Failing tests.** `Fixtures` finds `pdfium-multiplatform-prototype/fixtures/{report,encrypted-hello,colors}.pdf`, `tooling/viewer-e2e/fixtures/features.pdf` and `packages/engine/main/test/fixtures/signed_two_approvals.pdf` up the directory tree. Port every test of `ViewerTests.swift` (the metadata accessor case uses the internal `Host.Metadata` until Wave A); `DocumentAPITests.swift` (metadata through the internal host; the URL cases use `Loopback` with `HttpClientDocumentFetcher`: 404 then 200 leaves a failed tab that `RetryAsync` turns ready with 14 pages after two requests); `FetcherExportTests.swift` (a stub `IDocumentFetcher` written against the public API serves `report.pdf` and answers `not-found` otherwise); `StreamRuleTests.swift` (errors provoked through the internal interaction handle with a throwing handler; two observers each get three `HostError`s, a cancelled observer leaves the other live, disposal ends observers and a late subscription ends at once); `IntegerSurfaceTests.viewerPagesAndTabsTakeAndGiveInt`; `NativeLoadTests`: with `LoadOverride` throwing `NativeRuntimeNotFoundException`, `new PdfViewer()`, `PdfViewer.CreateAsync()` and `NativeRuntime.ReadPageCountAsync` each throw exactly that type; a call after `DisposeAsync` throws `PdfException` with `instance-closed`; `OpenFileAsync` and `OpenAsync(Stream)` open `report.pdf` with 14 pages, the file one named `report.pdf`; `TelemetryTests`: an `ActivityListener` on `EmbedPDF` sees `open` and `save`.
- [ ] **Step 2:** Run. Expected: build FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** PASS, 0 warnings.
- [ ] **Step 5:** Commit `feat(dotnet): add PdfViewer with document control and streams`.

### Task 7: Cancellation and disposal under load

**Files:**
- Create: `EmbedPDF.Viewer.Tests/LifetimeTests.cs`, `EmbedPDF.Viewer.Tests/CancellationTests.cs`
- Modify: `EmbedPDF.Viewer/PdfViewer.cs` as the tests demand

**Interfaces:**
- `DisposeAsync`: the first call starts one disposal task and every call returns it. Disposal marks the viewer closed (new calls answer `instance-closed`), cancels the Rust tokens of calls in flight, destroys the kernel, waits for calls in flight to settle, completes the hubs and subscriptions, then runs `Shutdown` and releases the host on the thread pool. The constructor calls `GC.SuppressFinalize` on the generated host so its finalizer cannot join the kernel thread on the finalizer thread; `PdfViewer`'s finalizer queues the shutdown and release to the thread pool and returns. Nothing the kernel calls back (delegate, listeners) holds the `PdfViewer`.

- [ ] **Step 1: Failing tests.** `CancellationTests.OpenUriCancelsTheTransportAndClosesTheLoadingTab`: a stalling `Loopback`, `HttpClient` with a recording handler; start `OpenAsync(uri, token)`, wait until the request arrives and the tab is `Loading`, cancel; expect `OperationCanceledException`, an empty document list, the handler's request token cancelled; then the server releases the stalled reply and no unobserved task exception is raised after a forced GC. `ALateFetcherCompletionAfterDisposeIsIgnored`: a fetcher that completes after `DisposeAsync` raises nothing. `SaveCancelledBeforeItStartsIsCancelled` (port of `saveCancelsWhenTheTaskIsCancelled`) with an already-cancelled token. `LifetimeTests.DisposeWithRendersInFlightEnds`: 8 renders through the internal render handle plus `SaveAsync` in flight, `DisposeAsync` completes within 10 s and every in-flight task ends (result or `PdfException`/`OperationCanceledException`). `ConcurrentDisposeSharesCompletion`: two `DisposeAsync` calls return the same task, and both complete. `AbandonedViewerIsFinalizedWithoutBlocking`: a started viewer with an open document dropped in a `NoInlining` method is collected within 20 GC rounds, each `GC.WaitForPendingFinalizers()` returns within 1 s.
- [ ] **Step 2:** Run. Expected: the new tests FAIL where Task 6 falls short.
- [ ] **Step 3:** Implement what fails.
- [ ] **Step 4:** PASS, then the whole suite 3 times in a row.
- [ ] **Step 5:** Commit `test(dotnet): pin cancellation and disposal of PdfViewer`.

### Task 8: Disabled plugins

**Files:**
- Create: `EmbedPDF.Viewer.Tests/DisabledPluginTests.cs`

**Interfaces:**
- Consumes the internal `PdfViewer.CallAsync` and the generated host accessors. Each Phase 3 wave replaces its rows' calls with the public accessor.

- [ ] **Step 1:** Port `DisabledPluginTests.swift`: 23 rows (field, prerequisites, install, accessor call through `CallAsync` disposing the handle); every plugin property of `PdfViewerOptions` but `Scope` and `StandardCommands` has one row; left out is `unsupported`; installed succeeds; left out for a closed document and for a loading document (held `Loopback`) is `unsupported`. Also `ViewerConfigBuilderTests`' two accessor theories over the same rows.
- [ ] **Step 2:** Run. Expected: PASS if Tasks 4 and 6 are right; a failing row is fixed in the option mapping, not the test.
- [ ] **Step 3:** Commit `test(dotnet): port the disabled plugin table`.

### Task 9: Dependency injection

**Files:**
- Create: `platforms/dotnet/EmbedPDF.Extensions.DependencyInjection/EmbedPDF.Extensions.DependencyInjection.csproj`, `ServiceCollectionExtensions.cs`, `PublicAPI.Shipped.txt`, `PublicAPI.Unshipped.txt`; `EmbedPDF.Viewer.Tests/DependencyInjectionTests.cs`
- Modify: `platforms/dotnet/EmbedPDF.sln`, `EmbedPDF.Viewer.Tests/EmbedPDF.Viewer.Tests.csproj`

**Interfaces:**
- `public static IServiceCollection AddEmbedPdf(this IServiceCollection services, Func<PdfViewerOptionsBuilder, PdfViewerOptionsBuilder>? configure = null)` and `AddEmbedPdf(this IServiceCollection, PdfViewerOptions options)`. Registers the options and a singleton `PdfViewer` whose logger comes from `ILoggerFactory` when one is registered and the options carry none. The viewer is not started; the container disposes it.

- [ ] **Step 1: Failing tests:** a provider built after `AddEmbedPdf(b => b.Metadata())` resolves one `PdfViewer` twice (same instance), which starts and opens `report.pdf`; the options resolve with `Metadata` true; `DisposeAsync` of the provider disposes the viewer (a later call is `instance-closed`).
- [ ] **Step 2:** Run. Expected: build FAIL.
- [ ] **Step 3:** Implement (`IsAotCompatible`, PublicApiAnalyzers, package id `EmbedPDF.Extensions.DependencyInjection`).
- [ ] **Step 4:** PASS.
- [ ] **Step 5:** Commit `feat(dotnet): add AddEmbedPdf`.

### Task 10: API coverage for C#

**Files:**
- Create: `scripts/check-api-coverage.py`, `scripts/test_check_api_coverage.py`, `platforms/dotnet/csharp-api-coverage.json`, `platforms/dotnet/tools/EmbedPDF.ApiDump/EmbedPDF.ApiDump.csproj`, `platforms/dotnet/tools/EmbedPDF.ApiDump/Program.cs`

**Interfaces:**
- `check-api-coverage.py --platform swift [--allow-pending]` reproduces `platforms/apple/check-swift-api-coverage.py` output and exit code exactly (that script is unchanged). `--platform csharp [--members <json>]` reads `platforms/dotnet/csharp-api-coverage.json`; without `--members` it runs `dotnet run -c Release --project platforms/dotnet/tools/EmbedPDF.ApiDump` and reads its stdout.
- ApiDump prints `{"members": ["EmbedPDF.PdfViewer.OpenAsync", ...]}`: each public type's full name and `<type>.<member>` for its public members, from `typeof(PdfViewer).Assembly`.
- Manifest: `schemaVersion: 1`, `crates` and `kernel` as Swift's; one entry per Rust member the shared inventory finds, keyed like Swift's (`crate`, `path`, `owner`, `symbol`), each with exactly one of `csharp: {"type", "member"}` plus `export`, `exclusion` (copied from Swift), or `pending: "phase-3-wave-<A..E>"` (by crate, as the umbrella's wave table). The 38 kernel members Swift maps to `PDFViewer.swift`, and `Kernel.settings_revision`, map to `PdfViewer` members.
- Check: unaccounted or stale Rust member fails; a `csharp` mapping whose `type.member` is not in the dump fails; an unknown `pending` value fails; pending rows pass. Summary line: `C# API accounting: N members (P ported, Q pending, R exclusions).`

- [ ] **Step 1: Failing tests** in `test_check_api_coverage.py`: a manifest row naming a missing C# member fails with `missing C#`; an unknown pending value fails; a pending row passes; swift mode on the real manifest prints the same stderr and exit code as the Apple script.
- [ ] **Step 2:** Run `python3 -m unittest scripts/test_check_api_coverage.py`. Expected: FAIL.
- [ ] **Step 3:** Implement, generating the first manifest with a one-off script that is not committed.
- [ ] **Step 4:** Tests PASS; `python3 scripts/check-api-coverage.py --platform csharp` exits 0; the Apple script's output before and after is identical.
- [ ] **Step 5:** Commit `feat(dotnet): check C# API coverage against the Rust inventory`.

### Task 11: Verify and record

**Files:**
- Modify: `docs/superpowers/plans/2026-11-20-dotnet-parity.md` (Phase 2 boxes, "Phase 2 notes"), this plan's boxes

- [ ] **Step 1:** Run every suite: `dotnet test EmbedPDF.Viewer.Tests`, `dotnet test EmbedPDF.Tests`, `python3 -m unittest discover -s platforms/dotnet/packaging`, `generate-bindings.test.sh`, `dotnet-engine-hash.test.sh`, the coverage check in both modes, and `dotnet build -c Release` of every new project with 0 warnings.
- [ ] **Step 2:** Record test counts, coverage numbers, deviations and what Phase 3 inherits in the umbrella's Phase 2 notes.
- [ ] **Step 3:** Commit `chore(dotnet): record Phase 2`.
