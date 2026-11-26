# EmbedPDF for .NET 10

Two package families live here. The sections up to [Manual validation](#manual-validation) describe
`EmbedPDF.Engine`, a narrow headless reader. The viewer packages, `EmbedPDF` and four companions,
start at [Viewer packages](#viewer-packages); [`EmbedPDF.Engine` or the viewer](#embedpdfengine-or-the-viewer)
says which to pick.

The `EmbedPDF.Engine` namespace exposes an in-process, typed read facade over
`epdf-capi`. The package name is provisional; this slice does not package,
relocate or publish native assets. Target framework: **net10.0 only**, 64-bit.
The scoped SDK pin is `10.0.404`.

```csharp
using EmbedPDF.Engine;

await using var engine = await PdfEngine.CreateAsync();
await using var document = await engine.OpenAsync(await File.ReadAllBytesAsync("example.pdf"));
var pages = await document.GetPagesAsync();
var text = await document.GetTextAsync(new PageIndex(0));
var geometry = await document.GetGeometryAsync(new PageIndex(0));
var segments = TextGeometry.GetSegments(geometry, new(new PdfCharIndex(0), 3));
var image = await document.RenderAsync(new PageIndex(0), new RenderOptions { Scale = 1.5 });
```

Open accepts bytes and an optional password. Reads expose unrotated page
metadata, text with explicit charmap, oriented/upright geometry, straight-alpha
RGBA8 rendering and one bounded literal-search slice. `PageInfo.Reference` is
a durable page object reference; `PageIndex` is its zero-based position.
`SearchOptions.Cursor` resumes a slice; null `SearchSlice.NextCursor` means
complete. Cursors are bound to their document/query. No implicit all-pages
search aggregation, regex, file-path native open or write API is exposed.

## Coordinates and index units

Page points/rects/quads use PDF points from the unrotated visible crop-box
upper-left, with y down. `PdfCropBox` separately preserves the original PDF
edges. Page size is never swapped for `/Rotate`; `UserUnit` and rotation remain
explicit metadata. Coordinates stay `double`, without facade rounding or
single-precision narrowing. Render options carry optional page-space region,
scale, quarter-turn rotation, background, annotation/widget flags and pixel cap;
`RgbaImage.Area`, dimensions and stride describe the actual raster. The owned
RGBA bytes need no additional swizzle or alpha conversion and remain valid
after document/engine disposal.

`PdfCharIndex` addresses PDFium's character slots; `Utf16Offset` addresses UTF-16
units in the extracted .NET string. Neither converts to the other. A dropped
character contributes a character slot without text; a supplementary character
contributes two UTF-16 units. Use the native `TextGeometry` charmap helpers for
conversions. `PdfCharRange` has character `Start`/`Count`. Geometry snapshots use
immutable arrays and distinct upright/oriented run/glyph types, preserving
loose, optional tight, empty, space and shear-only oriented variants.
`PageTransforms.Apply` delegates all transform/snapping math to the native
helper. Fold `UserUnit` into input `Scale`/`BaseScale` as the viewer does;
the helper does not silently apply it.

## Admission and lifetime

One managed dispatcher thread per engine performs native create/submit/wait/
close. Native PDFium access stays on its separate confined owner thread.
Async methods perform no native blocking work inline. A bounded channel and
byte admission limit queued and active managed requests; the defaults are 16
requests and 256 MiB. Saturation returns `PdfException` with `Code = "Busy"`
immediately. Callers choose bounded retries or use a bounded pool of engines.
`PdfEngineOptions.Limits` exposes the native limits independently.

Document disposal seals that document and queues ordered close control after
earlier admitted work. Close control needs no user-request slot or payload
budget. Engine disposal seals admission, drains requests/control, waits for
native owner-thread close and releases the engine. Concurrent/repeated disposal
shares completion. An abandoned engine can be finalized; its finalizer only
requests dispatcher shutdown and never waits for native close. Dispose documents
explicitly to release their retained PDF bytes while keeping an engine alive.
SafeHandle leases protect every native engine/job/result call and result view;
frame validation/copy finishes before releasing the view's owner.

Cancellation callbacks only call native `job_cancel`; they never wait or invoke
PDFium. Cancellation is cooperative at existing safe checks. A PDFium call can
delay cancellation, and a native process fault can terminate the application.
`Aborted` envelopes map to `OperationCanceledException`. Other native/engine
errors map to `PdfException.Code`; adapter failures also carry `NativeStatus`,
including when the native error result is unavailable. Password-required and
incorrect-password failures retain their existing typed error codes.

Startup fonts are ordered binary `StartupFont` payloads passed to
`CreateAsync`; native registration/fallback initialization completes before
creation returns. No mutable post-start font API is exposed. Input memories
must remain readable/unmodified until their owning call has accepted/copied
them; await creation before reusing startup-font buffers.

## Manual validation

Build and test inside the pinned project container, with the pinned PDFium
mounted at `/opt/pdfium`:

```sh
export PDF_RUNTIME_LIB_DIR=/opt/pdfium
export LD_LIBRARY_PATH=/opt/pdfium
cargo rustc -p epdf-capi --release --crate-type cdylib
EPDF_NATIVE_DIR="$PWD/target/release" dotnet test platforms/dotnet/EmbedPDF.Tests/EmbedPDF.Tests.csproj
```

The library finds PDFium through `LD_LIBRARY_PATH` (above), so keep it set when the tests run, or loading `libepdf_capi.so` fails with `libembedpdf.so: cannot open shared object file`.

When `CARGO_TARGET_DIR` is configured, point `EPDF_NATIVE_DIR` to its `release`
directory. `EPDF_NATIVE_DIR` is read **only by the test assembly's resolver**;
the production facade does not consume it or widen library search paths.
From `platforms/dotnet`, the SDK resolver uses the scoped `global.json`.

Tests cover native reads/render/search/helpers, binary startup fonts, typed
errors, compile-time index misuse, all committed layout/charmap probes, wire
operation vocabulary, search DTO ranges, malformed frames, result disposal
races, GC, stale documents, concurrent disposal and admission/cancellation
stress. Repeated operations under small native input/job/result limits prove
capacity is reusable; managed slot/byte counters return to zero. The C ABI has
no public resource-counter export, so tests do not claim direct inspection of
native registries. The default annotation/widget renderer also runs in a child
test process, so native crashes are reported without losing the other results.

The checked-in validation record identifies source/image/runtime/font/fixture
pins, commands, exit codes and actual artifact hashes. Linux arm64 is the
only facade integration environment validated here. Five-RID loading/packing,
relocation, native macOS/Windows validation and live browser parity remain the
separate packaging/parity slices. No published platform support is implied.

Known upstream regression: the unchanged pinned PDFium build still crashes
with SIGSEGV in the broad engine
`document_session::a_page_held_past_close_keeps_a_registry_layers_base_alive`
test ([issue #16](https://github.com/roanutil/embed-pdf-viewer/issues/16)).
Ordinary facade default rendering is covered separately and does not reproduce
that held-layer-page case. No test is skipped and no alternate runtime is used.

## Viewer packages

The viewer is five packages beside `EmbedPDF.Engine`, with no dependency between the two families.
They reach every viewer plugin the Swift package reaches, below the UI: there is no UI package, so
an app binds `EmbedPDF.Models` to its own views (see [Binding the models to a UI](#binding-the-models-to-a-ui)).
All five target `net10.0` and set `IsAotCompatible`.

| Package | What it is for | Depends on |
|---|---|---|
| `EmbedPDF.Runtime` | The `epdf-viewer-host` library and the PDFium it links, under `runtimes/<rid>/native/` for `linux-x64`, `linux-arm64`, `osx-arm64`, `osx-x64` and `win-x64`. No managed code; reference `EmbedPDF` instead. | nothing |
| `EmbedPDF` | The facade: `PdfViewer`, the 23 plugin capabilities, `PixelBuffer`, the platform services. Assembly `EmbedPDF.Viewer.dll`, namespace `EmbedPDF`. | `EmbedPDF.Runtime` at the pinned engine version, `Microsoft.Extensions.Logging.Abstractions` 10.0.0 |
| `EmbedPDF.Models` | `INotifyPropertyChanged` models a view binds to: `PdfReaderModel`, `StageReaderModel`, `SearchModel`, `ThumbnailModel` and nine more. | exactly the `EmbedPDF` it was built with, `[x.y.z]` |
| `EmbedPDF.Extensions.DependencyInjection` | `services.AddEmbedPdf(options => ...)`, which registers one `PdfViewer`. | `EmbedPDF`, `Microsoft.Extensions.DependencyInjection.Abstractions` |
| `EmbedPDF.Scripting.Jint` | `JintScriptEngineFactory`, the engine for document JavaScript (Actions). | `EmbedPDF`, Jint 4.18.0 |

The facade's assembly is `EmbedPDF.Viewer.dll` because `EmbedPDF.dll` would collide with win-x64
PDFium's `embedpdf.dll` on a case-insensitive file system. It compiles the UniFFI-generated C# as
`internal` types, so every public type is a hand-written record or wrapper, and
`Microsoft.CodeAnalysis.PublicApiAnalyzers` fails the build when the public surface changes without
its `PublicAPI.Unshipped.txt`. `EmbedPDF.Models` calls the facade's internals through
`InternalsVisibleTo`, which is why its range is exact: any other `EmbedPDF` may lack them.
`packaging/libraries.py pack-libraries` fails the pack when that range isn't `[x.y.z]`.

```csharp
using EmbedPDF;

await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Search = new(), Render = new() });
var document = await viewer.OpenFileAsync("report.pdf");
using var search = await viewer.GetSearchAsync(document);
using var render = await viewer.GetRenderAsync(document);
```

## Where the native library comes from

`EmbedPDF` depends on `EmbedPDF.Runtime` at the version in
`EmbedPDF.Runtime/EmbedPDF.Runtime.props`, `0.0.0-engine.<hash>`. `dotnet-engine-hash.sh` computes
the hash over every input of the native library: the tracked files of each crate
`epdf-viewer-host` links, `Cargo.toml`, `Cargo.lock`, `rust-toolchain.toml`, the PDFium pin and
commit, `build-viewer-host.sh`, `packaging/package.py` and `EmbedPDF.Runtime.csproj`. Facade, model
and documentation work leaves it alone, so it never needs a new runtime package.
`check-dotnet-engine.sh` exits 0 when the pin matches the tree, 1 when it doesn't and the tree's
runtime isn't on the package feed, and 2 when the runtime is on the feed and only the pin is stale.
It asks the feed with `gh api` (the owner's `packages/nuget/EmbedPDF.Runtime/versions`), so off CI
its token needs `read:packages`. CI's `runtime-pack` job runs it before it packs anything.

At run time the facade's resolver looks for the host beside the application, then in
`runtimes/<rid>/native` under it, and loads PDFium from the host's directory first. A RID with no
library fails with `NativeRuntimeNotFoundException`, a `PdfException` that names the RID and every
path it tried. When a library is there but won't load, the message says so and the loader's error
is the inner exception. The packaged Linux host carries a `$ORIGIN` runpath and the macOS host an
`@loader_path` one, so `LD_LIBRARY_PATH` and `DYLD_LIBRARY_PATH` are never needed.

For engine work there is a local mode: build the host from this tree, stage and pack it, generate
the bindings, and pack the facade against that runtime. In the dev container, from the repository
root:

```sh
bash packages/engine/runtime/scripts/fetch-libpdfium.sh linux-arm64
bash platforms/dotnet/build-viewer-host.sh linux-arm64          # prints the library path
python3 platforms/dotnet/packaging/package.py stage-runtime --rid linux-arm64 \
  --native-lib target/aarch64-unknown-linux-gnu/release/libepdf_viewer_host.so --out /tmp/rt/slice-linux-arm64
python3 platforms/dotnet/packaging/package.py pack-runtime --slice /tmp/rt/slice-linux-arm64 --out /tmp/rt/pack --allow-dirty
cargo rustc -p epdf-viewer-host --release --locked --crate-type cdylib   # unstripped, for the generator
bash platforms/dotnet/generate-bindings.sh
python3 platforms/dotnet/packaging/package.py pack-facade \
  --runtime-package /tmp/rt/pack/EmbedPDF.Runtime.<version>.nupkg --out /tmp/rt/pack
EPDF_IMAGE_ID=sha256:<image> python3 platforms/dotnet/packaging/package.py validate-runtime --rid linux-arm64 \
  --package /tmp/rt/pack/EmbedPDF.Runtime.<version>.nupkg --facade /tmp/rt/pack/EmbedPDF.0.0.0-local.nupkg \
  --manifest /tmp/rt/pack/package-manifest.runtime.json --record /tmp/rt/record.json
```

The tests skip the packing: `EmbedPDF.Viewer.Tests` and `EmbedPDF.Models.Tests` copy the host from
`EpdfViewerNativeDir` (default `target/release/`), so after the unstripped build and
`generate-bindings.sh`, `dotnet test` runs against the tree. The generator needs that unstripped
build because the uniffi metadata lives in the symbol table that `build-viewer-host.sh` strips.

`pack-runtime` takes one `--slice` per RID and re-checks every staged file against the slice's
record, so a slice edited after staging can't be packed. Without `--allow-dirty` it refuses a slice
staged from a dirty tree. `validate-runtime` restores the two packages into a fresh consumer with
its own NuGet cache and a `nuget.config` that serves `EmbedPDF` and `EmbedPDF.*` from the local feed
only and everything else from nuget.org, runs it RID-specific and portable, and checks it fails
with the host or PDFium hidden.

Sizes from CI run 45853424872 at `42734fceb` (engine `0.0.0-engine.2e7fdf92c64b1092`, Rust
1.100.0, SDK 10.0.404), in bytes. Every RID was built and validated on its own native runner; the
records are under `validation/2026-11-26-phase-6-runtime-<rid>/`.

| RID | Viewer host | PDFium | Validation |
|---|---|---|---|
| `linux-arm64` | 29,433,168 | 11,376,920 | native, Debian trixie container |
| `linux-x64` | 33,631,552 | 11,616,560 | native, Debian trixie container |
| `osx-arm64` | 28,364,368 | 9,879,008 | native, `macos15 20261118.0550.1` |
| `osx-x64` | 31,155,136 | 10,659,320 | native, `macos15 20260824.0482.1` |
| `win-x64` | 45,318,144 | 10,510,848 | native, `win22 20261116.370.1` |

The five-RID `EmbedPDF.Runtime` nupkg is 96,408,526 bytes.

## Plugins

A plugin is installed by setting its member of `PdfViewerOptions` (or the matching
`PdfViewerOptionsBuilder` method) and reached through its accessor. Every accessor is
`Get{Plugin}Async`, takes the document where the plugin is per document, and returns an
`IDisposable` capability. The platform services column lists the interfaces your app implements,
or the implementations the packages ship, for that plugin.

| Options member | Accessor | Also install | Platform services |
|---|---|---|---|
| `Metadata` | `GetMetadataAsync(document)` | | |
| `Search` | `GetSearchAsync(document)` | | |
| `Render` | `GetRenderAsync(document)` | | `PixelBuffer` output |
| `Interaction` | `GetInteractionAsync(document)` | | `IInteractionHandler`, tool permission `Func<bool>` |
| `Feedback` | `GetFeedbackAsync()` | | `IPlatformFeedback` (`FeedbackConfig.Provider`; none means no feedback) |
| `Stage` | `GetStageAsync(document)` | | `IStageInitialViewProvider` |
| `Selection` | `GetSelectionAsync(document)` | `Interaction` | |
| `Link` | `GetLinkAsync(document)` | `Interaction` | |
| `Annotation` | `GetAnnotationAsync(document)` | `Interaction` | `IAnnotationFilePicker` |
| `History` | `GetHistoryAsync(document)` | | |
| `Outline` | `GetOutlineAsync(document)` | | |
| `Attachment` | `GetAttachmentsAsync(document)` | | |
| `Form` | `GetFormAsync(document)` | `Interaction` | `IFormFocusTarget` |
| `Redaction` | `GetRedactionAsync(document)` | `Interaction`, `Annotation` | |
| `Measurement` | `GetMeasurementAsync(document)` | `Interaction`, `Annotation` | |
| `Stamp` | `GetStampsAsync()` | `Interaction`, `Annotation` to place | `IStampLibraryStore`, `FileStampLibraryStore` |
| `PageEdit` | `GetPageEditAsync(document)` | | |
| `ViewManager` | `GetViewManagerAsync()` | | |
| `Shell` | `GetShellAsync(document)` | | |
| `I18n` | `GetI18nAsync()` | | `ILocaleLoader`, `EmbeddedResourceLocaleLoader` |
| `Commands` | `GetCommandsAsync()` | | `CommandHandler`, `IStandardCommandsPlatform` (`PdfViewer.SetCommandsPlatform`) |
| `Actions` | `GetActionsAsync(document)` | | `IActionExecutor`, `IActionUiHandler`, `IActionSubmitHandler`, `IScriptEngineFactory` (`JintScriptEngineFactory` in `EmbedPDF.Scripting.Jint`) |
| `Signature` | `GetSignaturesAsync(document)` | `Interaction`, `Form` | `ISignatureSigner` (`X509CertificateSigner`), `ISignatureTrust` (`TrustAnchors`), `ISignatureTransport` (`HttpClientSignatureTransport`) |

Documents opened from a `Uri` go through `PdfViewerOptions.DocumentFetcher`, an `IDocumentFetcher`;
the default is `HttpClientDocumentFetcher`, which also reads `file:` URLs.

A plugin left out of the options is refused at its accessor with `PdfException` and
`PdfErrorCode.Unsupported`, even for a document that is closed or still loading, so a capability
you hold has its plugin. `DisabledPluginTests` checks every accessor, fails when
`PdfViewerOptions` gains a plugin without a row there, and fails when this table lacks one. A few
calls also need a companion plugin and refuse on the call: `RedactionCapability` marking a
selection needs `Selection` and marking matches needs `Search`, and
`StageCapability.RegisterScrollHandlerAsync` needs `Interaction`.

`scripts/check-api-coverage.py --platform csharp` checks the facade against the Rust plugins'
`ffi.rs` members (`platforms/dotnet/csharp-api-coverage.json`): 880 members, 730 ported, 0
pending, 150 exclusions, each with its reason.

## Threading, disposal and arguments

Every call is a `Task`-returning `...Async` method that throws `PdfException` (with `Name`, `Code`
and `Detail`) and takes a `CancellationToken` last. Every call checks the token before it is posted
to the kernel. A call whose Rust export takes a cancel token also passes yours on: cancelling it
while the call runs cancels the kernel's token, and the call stops at the engine's next check. That
covers about 140 call sites (the facade's `CallCancellableAsync` and `RunCancellable`): opening from
a `Uri`, opening and saving layers, saving, and the long reads and writes of search, render, outline,
attachment, page edit, metadata, selection, annotation, form, redaction, measurement, stamp and
signature. The rest run to completion once posted, as the Swift calls do. A cancelled call throws
`OperationCanceledException`, with the kernel's `operation-cancelled` as the inner exception when the
kernel saw it. Work that runs on the kernel thread only sees the cancel when it next yields, so
`RedactionCapability.MarkMatchesAsync` over a whole document can't be cancelled once it runs (see
[Known gaps](#known-gaps)). Event streams (`ObserveDocumentEvents`, `ObserveStateRevisions`, `ObserveReportedErrors` and each capability's `ObserveEvents`)
are `IAsyncEnumerable<T>` that subscribe when the method returns, one channel per enumerator.
Document and plugin events are unbounded, so a reader that stops reading holds every later event;
state revisions keep the newest one and reported errors the newest 16.

Your callbacks run on one of two threads, and each interface's remarks say which.

- On the kernel thread, synchronously: `IInteractionHandler`, the tool permission,
  `IPlatformFeedback`, `IActionUiHandler`, `IActionSubmitHandler`, `IScriptEngineFactory` and
  `IScriptEngine`, `IStageInitialViewProvider`, a `CommandHandler`'s state hooks,
  `IStandardCommandsPlatform.SaveFile`, `IFormFocusTarget`, `IStampLibraryStore`,
  `ISignatureSigner.GetCertificateChain`, `Algorithm` and `Digest`, and `ISignatureTrust`.
  Return quickly, and never block waiting for the viewer: the viewer's next call needs this thread.
- On the thread pool: `IDocumentFetcher`, `ILocaleLoader`, `IActionExecutor`, a `CommandHandler`'s
  run, the copy members of `IStandardCommandsPlatform`, `IAnnotationFilePicker`,
  `ISignatureSigner.SignAsync` and `ISignatureTransport.PostAsync`.

Destroying the kernel needs the kernel thread, so `PdfViewer.Dispose()` from a kernel-thread
callback throws `InvalidOperationException` instead of deadlocking. `DisposeAsync()` there starts
disposal and returns a task you may await later; blocking on it with `GetAwaiter().GetResult()`
throws too, but blocking through `AsTask().Wait()` can't be detected and hangs. The same goes for an
event stream: blocking on an enumerator's `DisposeAsync()` from a kernel callback (with
`GetAwaiter().GetResult()` or `.AsTask().Wait()`) can hang, because ending the subscription needs the
kernel thread; await it instead, or dispose it elsewhere. A callback that
throws never reaches Rust as a crash: a member with an error type answers the kernel's host error
with your message, a `bool` member answers `false`, and the rest log through the viewer's `ILogger`
and drop the exception.

`DisposeAsync` drains calls in flight, a second `DisposeAsync` shares the first's completion, and a
call after it throws `PdfException` with `instance-closed`. Capabilities are `IDisposable` and
release only their native handle. A viewer abandoned to the garbage collector releases its handles
from its finalizer; a live event stream or registered callback that captures nothing of the viewer
doesn't keep it alive.

Arguments follow one rule. A method that is not itself `async` throws `ArgumentNullException` (or
`ArgumentException` for a null element of a collection) while the call is made. A method declared
`async` reports a null argument through its faulted task. A present but invalid argument, such as a
negative page index or an undefined enum value, faults the returned task with `invalid-input`.

## `EmbedPDF.Engine` or the viewer

Pick one. `EmbedPDF.Engine` is the narrow reader described at the top of this file: open, page
metadata, text, geometry, one bounded search slice and rendering, with admission control and
per-RID validation records, and no viewer kernel. It suits a server that reads PDFs. The viewer
packages carry the kernel and every plugin, including writes, and suit an app that shows and edits
documents. Each family bundles its own PDFium, so an app that loads both loads two PDFium copies
and pays the memory twice. The decision to keep them separate is D2 in
`docs/superpowers/plans/2026-11-20-dotnet-parity.md`; it is revisited after the first viewer release.

## Binding the models to a UI

`EmbedPDF.Models` is the layer a view binds to. It has no UI framework reference, so any .NET UI
framework, or a console, binds it with the same code. Each model is an
`INotifyPropertyChanged` class, actions are `…Async` methods your framework's commands wrap, and
images are `PixelBuffer`s you copy into your framework's bitmap.

A model has one home `SynchronizationContext`: the one you pass its constructor, else the one
current when you construct it. Make models on the UI thread and every `PropertyChanged` arrives
there, including the ones a kernel callback or a plugin event causes. You may call a model from
any thread; it moves to its home context before it touches state. Without a context, as in a
console app, the model makes a private one that runs its work one item at a time on the thread
pool. A `PropertyChanged` handler that throws there rethrows on a pool thread and ends the process,
as an unhandled exception on a UI dispatcher would, so catch inside the handler what it can raise.

Models that follow the document have a `RunAsync(CancellationToken)`. Start it when the view
appears and cancel the token when it goes; it returns without throwing. Lists are read-only
snapshots replaced as a whole with one notification, so a view rebinds rather than diffing.

`StageReaderModel` is the whole contract a page view implements. The view reports, in viewport
units with the origin at the top left and y growing downward:

| Event | Call |
|---|---|
| The viewport's size, and device pixels per unit | `SizeChangedAsync(width, height, displayScale)` |
| A drag's total translation, then its end velocity per second | `DragChangedAsync(x, y)`, `DragEndedAsync(vx, vy)`, `DragCancelledAsync()` |
| A pinch's total magnification around a point, then its end | `MagnifyChanged(scale, x, y)`, `MagnifyEndedAsync()` |
| A double tap | `DoubleTappedAsync(x, y)` |
| Pointer events while `ForwardsDrags` is true, and taps | `PointerDown`, `PointerMoved`, `PointerUp(PointerInput)`, `PointerCancelled()`, `Tapped(PointerInput)` |

The stage owns the scroll position: the view never sets an offset, it draws `VisiblePages`, each
at `ScreenX` and `ScreenY`, rendered at `StageReaderModel.RenderScale(...)`. Points on a page
overlay map to page space with `StageVisiblePageExtensions.PagePoint`. `PointerInput` is a new
record rather than wave B's `PointerSample`, because the sample carries the page hit the model
looks up and a phase the method name already gives.

The example opens the PDF at `path`. `ReadmeExampleTests` runs this block, extracted from this
file, on a UI-like thread and with no context at all.

```csharp
using EmbedPDF;
using EmbedPDF.Models;

// On the UI thread, so the models' notifications arrive there.
await using var reader = new PdfReaderModel(ReaderFeatures.Search | ReaderFeatures.Links);
await reader.OpenAsync(File.ReadAllBytes(path));
if (reader.Phase is not ReaderPhase.Ready ready) return;

var pages = ready.Session.Reader;
using var shown = new CancellationTokenSource();
var following = pages.RunAsync(shown.Token); // for as long as the view shows the document
pages.PropertyChanged += (_, e) =>
{
    if (e.PropertyName == nameof(StageReaderModel.VisiblePages)) Console.WriteLine($"{pages.VisiblePages.Count} pages in view");
};

// What the view's own events turn into.
await pages.SizeChangedAsync(800, 600, displayScale: 2);
await pages.DragChangedAsync(0, -120);
await pages.DragEndedAsync(0, -900);
pages.Tapped(new PointerInput(400, 300) { Kind = PointerKind.Mouse });

var search = ready.Session.Search!;
search.Text = "the";
await search.SubmitSearchAsync();
Console.WriteLine(search.Summary); // "1 of 985"

shown.Cancel();
await following;
```

The cost of having no UI package (D6 in `docs/superpowers/plans/2026-11-20-dotnet-parity.md`) is
that each app writes this view, its tiling (`TiledPageModel`) and its gesture plumbing itself.
`platforms/apple/EmbedPDF/Sources/EmbedPDFUI/` shows one complete implementation of the same
contract.

## Console sample and consumer check

`samples/EmbedPDF.Sample.Console` is a console app that uses the packages the way an app outside
this repository does. It references `EmbedPDF`, `EmbedPDF.Models`,
`EmbedPDF.Extensions.DependencyInjection` and `EmbedPDF.Scripting.Jint` as packages, never as
projects, and is not in `EmbedPDF.sln`. It builds a `PdfViewer` through `AddEmbedPdf`, opens a PDF,
searches it, renders page 1 to a PNG with its own encoder (`Png.cs`, `ZLibStream` and CRC-32), adds
a square annotation, saves and reopens the document, drives `SearchModel`, and runs one script in
Jint. Each step prints one line, and the first failure exits 1:

```text
PASS di: PdfViewer from AddEmbedPdf, runtime linux-arm64
PASS open: report.pdf, 14 pages
PASS search: "the" 985 hits (Complete)
PASS render: page 1 612x792 -> /tmp/out/page-1.png
PASS annotate: square on page 1 (ObjectNumber)
PASS save: 1017209 bytes -> /tmp/out/annotated.pdf, reopened with 1 square(s)
PASS model: SearchModel "2 of 985", 17 property changes
PASS script: Jint alert "sample 42"
```

`dotnet-consumer.sh` is the check. It packs all five packages (or takes them from `--packages`),
copies the sample out of the tree, restores it from a local feed that alone may serve `EmbedPDF*`
ids, and runs it with no `EPDF_NATIVE_DIR` or `LD_LIBRARY_PATH`. Then it publishes the sample with
`PublishAot=true` and runs the binary. `packaging/png_check.py` checks both PNGs.

```sh
bash platforms/dotnet/dotnet-consumer.sh                    # host RID, builds every package
bash platforms/dotnet/dotnet-consumer.sh --rid linux-x64 --packages <dir> --no-aot
```

The NativeAOT publish reports six warnings, all from Jint: IL2104 and IL3053 for the assembly, and
IL2026 from `Jint.Options.Apply`, its display class, `DefaultTypeConverter.BuildDelegate` and
`DefaultObjectConverter.ConvertSystemTextJsonValue`. They are Jint's CLR interop and JSON paths,
which `EmbedPDF.Scripting.Jint` never enables, and the script step passes in the native binary.
The script fails on any other trim or AOT warning. An app that needs no document JavaScript drops
`EmbedPDF.Scripting.Jint` and publishes with no warning.

Sizes on `linux-arm64` in the dev container, in bytes: the NativeAOT executable is 18,572,016, and
its publish directory is 106,370,152, of which 47,024,824 is the `.dbg` symbol file and 40,744,448
the viewer host and PDFium. Under the container's x86_64 emulation, `linux-x64` gives 18,096,808
and 100,835,330.

`EmbedPDF.Models` depends on exactly the `EmbedPDF` version it was built with (`[x.y.z]`), because
it calls the facade's internals. `packaging/libraries.py pack-libraries` packs the three library
packages and fails when that range is not exact. It is a separate file because `package.py` is an
input of `dotnet-engine-hash.sh`, and managed packaging must not move the runtime's version.

## Releases and the package feed

The release train (`.github/workflows/release-train.yml`) releases the viewer packages when it is
dispatched with `dotnet_version`, `0.0.0-dotnet.<n>`. After its one approval,
`dotnet-viewer-release` runs `packaging/release-viewer.sh`, which pushes the five packages to the
GitHub Packages NuGet feed of the repository's owner, `EmbedPDF.Runtime` first, and tags the
prerelease `<dotnet_version>` with the packages, their `.sha256` files and the merged runtime
manifest. A manual run of `release-dotnet.yml` with a `dotnet_version` prints those pushes, the
tag and the release instead (`viewer-release-preview`). `docs/releases/README.md` has the rest.

A consumer adds one feed entry and one package reference. GitHub Packages needs a token even to
read, so the entry carries credentials: a personal access token with `read:packages`, kept out of
source control. That is the cost of this feed over nuget.org, which would need a reserved prefix and
package signing we don't have.

```xml
<!-- nuget.config -->
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="embedpdf" value="https://nuget.pkg.github.com/<owner>/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="embedpdf"><package pattern="EmbedPDF" /><package pattern="EmbedPDF.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
  <packageSourceCredentials>
    <embedpdf>
      <add key="Username" value="<user>" />
      <add key="ClearTextPassword" value="%EMBEDPDF_FEED_TOKEN%" />
    </embedpdf>
  </packageSourceCredentials>
</configuration>
```

```xml
<PackageReference Include="EmbedPDF" Version="0.0.0-dotnet.<n>" />
```

`EmbedPDF` brings `EmbedPDF.Runtime`; add `EmbedPDF.Models`, `EmbedPDF.Extensions.DependencyInjection`
or `EmbedPDF.Scripting.Jint` at the same version when you use them.

## Known gaps

- Ten Rust members have no `ffi.rs` export and no Swift counterpart, so neither port has them: in
  annotation `get_shared`, `is_movable`, `can_duplicate`, `duplicate`, `when_remeasurable`,
  `stage_remeasure`, `selection_duplicate` and `selection_can_duplicate`, and in form `duplicate`
  and `duplicate_widgets`. They are `no-export` exclusions in `csharp-api-coverage.json`; exporting
  them is a Rust change for Swift and C# together.
- `RedactionCapability.MarkMatchesAsync` can't be cancelled once it runs. The search runs on the
  kernel thread, so the abort posted by `call_promise_cancellable`
  (`crates/epdf-ffi/src/bound.rs:118-135`) waits behind it: a whole-document mark of `"e"` on
  `report.pdf`, cancelled after 300 ms, ran past 90 s. A token cancelled before the call still
  stops it.
- NativeAOT is validated on `linux-x64` and `linux-arm64` only. CI runs the console sample
  framework-dependent on `osx-arm64` and `win-x64`; their NativeAOT rows are a later change.
- The three signature route tests (`SignatureTransportTests`) are skipped unless
  `EMBEDPDF_SIGNATURE_ROUTES` is set, as Swift's are, because their helper needs
  `packages/core/signature/dist` built.
- The Jint engine's NativeAOT publish reports six trim and AOT warnings from Jint's own CLR interop
  and JSON paths (see the sample section). An app that needs no document JavaScript leaves out
  `EmbedPDF.Scripting.Jint` and publishes with none.
