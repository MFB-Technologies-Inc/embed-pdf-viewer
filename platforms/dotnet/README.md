# EmbedPDF for .NET 10

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

The viewer is two new packages beside `EmbedPDF.Engine`, with no dependency between them.
`EmbedPDF.Runtime` holds the `epdf-viewer-host` library and the PDFium it links, under
`runtimes/<rid>/native/`, and no managed code. `EmbedPDF` is the facade, in the assembly
`EmbedPDF.Viewer.dll`: `EmbedPDF.dll` would collide with win-x64 PDFium's `embedpdf.dll` on a
case-insensitive file system. It compiles the generated
bindings as internal types and depends on `EmbedPDF.Runtime` at the version pinned in
`EmbedPDF.Runtime/EmbedPDF.Runtime.props`. Today the facade is a skeleton: `NativeRuntime` loads the
host and reads a page count, and the document API arrives in Phase 2 of
`docs/superpowers/plans/2026-11-20-dotnet-parity.md`.

The runtime is versioned `0.0.0-engine.<hash>`, where the hash comes from `dotnet-engine-hash.sh`.
Facade work that leaves the Rust alone keeps the same runtime. `check-dotnet-engine.sh` fails when
the pin and the tree disagree.

The facade's resolver loads PDFium from the host's directory, then the host. It looks beside the
application first, then in `runtimes/<rid>/native` under it. A runtime with no library fails with
`NativeRuntimeNotFoundException`, a `PdfException` that names the RID and every path it tried.
When a library is there but won't load, the message says so and the loader's error is the inner
exception.
The packaged Linux host also carries a `$ORIGIN` runpath. `LD_LIBRARY_PATH` is never needed.

Build and check one RID in the dev container, from the repository root:

```sh
bash platforms/dotnet/build-viewer-host.sh linux-arm64          # prints the library path
python3 platforms/dotnet/packaging/package.py stage-runtime --rid linux-arm64 \
  --native-lib target/aarch64-unknown-linux-gnu/release/libepdf_viewer_host.so --out /tmp/rt/slice-linux-arm64
python3 platforms/dotnet/packaging/package.py pack-runtime --slice /tmp/rt/slice-linux-arm64 --out /tmp/rt/pack
cargo rustc -p epdf-viewer-host --release --locked --crate-type cdylib   # unstripped, for the generator
bash platforms/dotnet/generate-bindings.sh
python3 platforms/dotnet/packaging/package.py pack-facade \
  --runtime-package /tmp/rt/pack/EmbedPDF.Runtime.<version>.nupkg --out /tmp/rt/pack
EPDF_IMAGE_ID=sha256:<image> python3 platforms/dotnet/packaging/package.py validate-runtime --rid linux-arm64 \
  --package /tmp/rt/pack/EmbedPDF.Runtime.<version>.nupkg --facade /tmp/rt/pack/EmbedPDF.0.0.0-local.nupkg \
  --manifest /tmp/rt/pack/package-manifest.runtime.json --record /tmp/rt/record.json
```

`pack-runtime` takes one `--slice` per RID. It re-checks every staged file against the slice's
record, so a slice edited after staging can't be packed. The generator needs an unstripped host,
because the uniffi metadata lives in the symbol table that `build-viewer-host.sh` strips.

Sizes from CI run 44799578194 at `8709bf4fb` (engine `0.0.0-engine.822686307291c580`), in bytes.
Every RID was built and validated on its own native runner; the records are under
`validation/2026-11-20-phase-1-runtime-<rid>/`.

| RID | Viewer host | PDFium | Validation |
|---|---|---|---|
| `linux-arm64` | 29,367,528 | 11,376,920 | native, Debian trixie container |
| `linux-x64` | 33,636,560 | 11,616,560 | native, Debian trixie container |
| `osx-arm64` | 28,331,040 | 9,879,008 | native, `macos15 20261118.0550.1` |
| `osx-x64` | 31,151,008 | 10,659,320 | native, `macos15 20260824.0482.1` |
| `win-x64` | 44,866,560 | 10,510,848 | native, `win22 20261116.370.1` |

The five-RID `EmbedPDF.Runtime` nupkg is 96,125,498 bytes, and the `EmbedPDF` nupkg is 1,221,777.
Built in the dev container, both Linux hosts need GLIBC 2.35 and their PDFium 2.25.

An app that loads both `EmbedPDF.Engine` and `EmbedPDF` loads two PDFium copies. Pick one.

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
