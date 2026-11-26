# .NET Package Parity Plan

> **For agentic workers:** this is an umbrella plan. Each phase below gets its own task-level plan
> (`docs/superpowers/plans/2026-11-20-dotnet-parity/NN-<phase>.md`, written with
> superpowers:writing-plans) before anyone executes it. Execute those with
> superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring `platforms/dotnet` from a read-only engine facade to a full .NET library with the
same reach as `platforms/apple/EmbedPDF` below the UI: every viewer plugin as a typed C#
capability, UI-neutral models any .NET UI framework can bind to, platform services, a sample, and
a published package. There is no UI layer (D6).

**Architecture:** Apple's layers minus the UI, over the same Rust. UniFFI-generated C# wraps
`epdf-viewer-host` (all plugins) and compiles into the facade assembly as `internal`. A
hand-written facade (`EmbedPDF`) reshapes it into `Task`-returning methods with
`CancellationToken`, `IAsyncEnumerable<T>` streams, records and `IAsyncDisposable`. A models
assembly projects state through `INotifyPropertyChanged` with no UI dependency; consumers draw it
with whatever framework they use. Behaviour stays in Rust; C# renames and reshapes.

**Tech Stack:** Rust (pinned by `rust-toolchain.toml`), `uniffi =0.32.0`, a C# UniFFI generator
(D1), .NET SDK `10.0.403` targeting `net10.0`, xUnit 2.9.3, `System.Text.Json` source generation,
`Microsoft.Extensions.Logging.Abstractions`, `Microsoft.CodeAnalysis.PublicApiAnalyzers`, Jint for
Actions JavaScript (D5). No UI framework dependency anywhere.

**Spec:** this document, argued from the Apple package as it stands at `53a39771b`:
`platforms/apple/README.md`, `docs/superpowers/plans/2026-10-06-swift-plugins/00-umbrella.md`,
`docs/superpowers/plans/2026-11-10-swift-api-idioms.md`,
`docs/superpowers/plans/2026-11-18-swift-api-gaps.md`, and the sibling
`docs/superpowers/plans/2026-11-19-android-parity.md`, whose phase shape this plan shares so the
two ports can reuse each other's tooling. Those documents paid for the API design. This one ports
the outcome.

## Where .NET is today

`platforms/dotnet/EmbedPDF` is about 580 lines of C# over `crates/epdf-capi`, a hand-written C ABI
over `epdf-engine::host::Host` with no kernel and no plugins. It opens bytes, reads page metadata,
text, character geometry, one bounded literal-search slice, and renders RGBA
(`platforms/dotnet/README.md`). It is good at what it does: a managed dispatcher thread, bounded
admission, SafeHandle leases and checked-in per-RID validation records under
`platforms/dotnet/validation/`. There is no viewer, no state, no annotation, form, history, stage
or any other plugin, and no write path.

The October design chose the C ABI on purpose
(`docs/superpowers/specs/2026-10-30-dotnet-native-binding-design.md:39-44`): UniFFI's C#
generator then trailed the workspace's 0.32 by one minor version, and the scope was a narrow
engine reader. Parity changes the scope. Apple reaches 23 plugins through 25 generated modules
(`platforms/apple/EmbedPDF/Sources/EmbedPDFBindings/`), and hand-writing a C ABI for each plugin
would duplicate that generator by hand, plugin by plugin, forever.

Apple, for comparison: 40 files under `Sources/EmbedPDF/Plugins`, 20 models in
`Sources/EmbedPDFModels`, 19 SwiftUI files in `Sources/EmbedPDFUI`, 93 test files, a demo app, and
an API coverage check (`platforms/apple/check-swift-api-coverage.py` over
`platforms/apple/swift-api-coverage.json`) that fails when a Rust capability member has no Swift
counterpart.

Two facts make the generated route cheaper for C# than it looks. The viewer host exports no
UniFFI `async` functions (a grep of `crates/plugins`, `crates/epdf-viewer-host`, `crates/epdf-ffi`
and `crates/epdf-uniffi` for `export(async` or `async_runtime` finds none), which is the area
third-party generators support least. It does use foreign traits: 45 `with_foreign` or
`callback_interface` sites, so callback support is the thing Phase 0 must prove.

## Global Constraints

- Work lands on `research/multi-platform-rust-core-impl-plugins` through feature branches, one
  worktree per phase or wave. Merges into the plugins branch and every push wait for the user.
- No TypeScript or npm package changes, so no changesets.
- No change to a plugin's Rust behaviour. A phase may add `ffi.rs` members only where Apple
  already calls the same member through Swift; if C# needs something Swift doesn't, stop and ask.
- Generated C# is a build output. It is never committed and never hand-edited; fixes go into the
  generator configuration, a post-processing step in `crates/uniffi-bindgen`, or the Rust `ffi.rs`.
- Builds and tests run in the dev container first (SDK `10.0.403`, `.devcontainer/Dockerfile:21`).
  macOS and Windows runs are manual, recorded under `platforms/dotnet/validation/` against
  `validation/record.schema.json`, as slice 6 records are today. An RID with no record stays
  `UNVALIDATED`.
- A passing row of a hosted CI runner in `.github/workflows/release-dotnet.yml` counts as a native
  run (`VALIDATED`), recorded with the runner image (`ImageOS ImageVersion`) or the container
  digest. There is no Windows development host, so for `win-x64` that row is the only route to
  `VALIDATED`. An emulated run stays `container-emulated`.
- RIDs: `linux-x64`, `linux-arm64`, `osx-arm64`, `osx-x64`, `win-x64`. `win-arm64` is out of scope
  unless the user adds it. There is no Windows development host, so `win-x64` is packed but stays
  `UNVALIDATED` until one is available; nothing in this plan may depend on Windows-only APIs.
- No UI components: no controls, views, `ICommand` implementations, framework bitmap types or
  UI framework package references in any shipped assembly (D6).
- `net10.0` only. `Nullable`, `TreatWarningsAsErrors` and `ImplicitUsings` stay on
  (`platforms/dotnet/Directory.Build.props`). Every shipped assembly sets `IsAotCompatible=true`
  and builds clean under the trim and AOT analyzers.
- `EmbedPDF.Engine` (the current package) is not changed by this plan except for README
  cross-references (D2).
- Package prose and comments name no outside product, company or person.
- Commit messages follow `feat(dotnet): …` with no co-author or session trailer.
- Before the first tagged release, rename freely. After it, a public rename keeps an
  `[Obsolete("Use X")]` shim for one tagged release.

## Decisions

The user confirms or overrides each before Phase 1 starts.

**D1. Binding generator.** *Changed 2026-11-20 by Phase 0: works only from a fork of the generator, see "Phase 0 results".* Recommendation: generate C# with UniFFI's community C# generator
(`uniffi-bindgen-cs`), run through a thin wrapper binary in `crates/uniffi-bindgen-cs` pinned to
the same `uniffi =0.32.0` as everything else, the way `crates/uniffi-bindgen` exists for Swift and
Kotlin (`crates/uniffi-bindgen/Cargo.toml:12-14`). Phase 0 proves it on the full host: generation,
a clean compile under `TreatWarningsAsErrors` (generated files get a `<NoWarn>` scope, not the
handwritten ones), one record round trip and one foreign-trait callback round trip.

If the generator has not caught up to 0.32, the fallback is a pinned fork with the version bump
applied, referenced by git revision from the wrapper crate. Cost: we carry that fork until
upstream catches up, and every UniFFI bump in this repo then needs a matching fork bump. The last
resort, writing our own C# backend, is weeks of work and is not planned; if Phase 0 points there,
stop and re-plan with the user.

Rejected: extending `epdf-capi` to the viewer. It would mean a hand-maintained C function, frame
layout and managed marshaller for every one of several hundred plugin members, kept in step with
Rust by review alone.

**D2. Relationship with `EmbedPDF.Engine`.** *Confirmed 2026-11-20 (execute instruction).* Recommendation: keep it as a separate, narrow package
for headless reading, and build the viewer as new packages beside it with no dependency between
them. Its admission control and validation records are worth keeping for server use, where a
viewer kernel is dead weight. Cost: two native libraries each carry their own PDFium. An app that
loads both pays the memory twice, so the README says to pick one. Revisit after the first viewer
release: if nobody needs the narrow package, deprecate it then.

**D3. Packages.** *Confirmed 2026-11-20 (execute instruction).* Three NuGet packages, plus the small DI extension package from D4, all
`net10.0`:

| Package | Apple counterpart | Contents | Depends on |
|---|---|---|---|
| `EmbedPDF.Runtime` | `EmbedPDFBindings` binary target | `runtimes/<rid>/native/` viewer-host library for each RID | nothing |
| `EmbedPDF` | `EmbedPDF` | facade (assembly `EmbedPDF.Viewer.dll`), with the generated C# compiled in as `internal` | `EmbedPDF.Runtime`, `Microsoft.Extensions.Logging.Abstractions` |
| `EmbedPDF.Models` | `EmbedPDFModels` | UI-neutral models | `EmbedPDF` |

*Changed 2026-11-20:* the facade's assembly is `EmbedPDF.Viewer`, not `EmbedPDF`, because `EmbedPDF.dll` and win-x64 PDFium's `embedpdf.dll` collide on a case-insensitive file system once a RID-specific build copies both into one directory. The package id and root namespace stay `EmbedPDF`.

`EmbedPDFUI` has no counterpart (D6). `EmbedPDF.Runtime` is versioned `0.0.0-engine.<hash>`, the
hash computed like `apple-engine-<hash>`, so facade and model work never rebuilds Rust.

Generating the bindings as `internal` gets by construction what Apple could not: Apple's
`EmbedPDF` re-exports 539 of 710 generated types because Swift's generator has no access-level
option (`platforms/apple/README.md`, "Products"). If the C# generator's `access_modifier` setting
works, nothing generated is public, and each public type is a hand-written record or a
deliberate wrapper. Cost: every generated record the API exposes needs a hand-written twin and a
mapping, which is more code than a typealias. Phase 0 confirms the setting; if it doesn't exist,
fall back to Apple's arrangement and document the generated namespace as unsupported.

**D4. C# shape.** *Confirmed 2026-11-20 (execute instruction).* Swift idioms map one to one, so the port doesn't relitigate them:

| Swift | C# |
|---|---|
| `async throws(PDFError)` | `Task<T>`/`ValueTask<T>` returning `…Async` method that throws `PdfException` |
| `bridgedCancellable` (`PDFError.swift:171-180`) | `CancellationToken cancellationToken = default` last parameter; `token.Register(() => cancelToken.Cancel())`; cancellation surfaces as `OperationCanceledException` |
| `AsyncStream` per subscriber (`observeStateRevisions()`) | `IAsyncEnumerable<T>` per enumerator, `[EnumeratorCancellation]`, over a bounded `Channel<T>` fed by the same hub |
| `PDFError.Code` with `unknown(String)` | `readonly record struct PdfErrorCode(string Value)` with static known values, so new Rust codes don't break callers |
| closed string sets / open string sets | `enum` for closed sets; `readonly record struct` with static members for open ones (tool ids, `AnnotationPosition`) |
| `Int` at the surface, conversion in the wrapper | `int`/`long`; `uint`/`ulong` never public |
| `Codable` drafts and patches | `sealed record` types with a `System.Text.Json` source-generated `JsonSerializerContext` (AOT safe) |
| `@MainActor @Observable` model | `INotifyPropertyChanged` class; events raised on the `SynchronizationContext` captured at construction |
| `deinit`/ownership | `IAsyncDisposable` on `PdfViewer` and every handle owner; finalizers only release native handles |
| `CGImage`/`CoreGraphicsBridges` | `PixelBuffer` (owned BGRA or RGBA bytes, width, height, stride, premultiplied flag) in `EmbedPDF`; consumers copy it into their framework's bitmap |
| `Data`, `URL` | `ReadOnlyMemory<byte>`, `Stream`, file path and `Uri` overloads |
| `os_signpost` (`Signposts.swift`) | `ActivitySource("EmbedPDF")` and `Meter("EmbedPDF")` |
| `print`/`Logger` | `ILogger<T>` taken from options, `NullLogger` by default |

Names follow .NET design guidelines: `PdfViewer`, `PdfException`, `SearchCapability`,
`OpenAsync`. *Changed 2026-11-20 in wave A:* capability accessors are `Get{Plugin}Async`
(`viewer.GetSearchAsync(document)`, `GetViewManagerAsync()`), since .NET method names are verbs
and `viewer.SearchAsync(document)` reads as searching the document. The coverage manifest maps
capability members, not accessors, so the Swift names stay on the Swift side. Configuration takes a
`PdfViewerOptions` class with init properties plus a fluent builder, and an optional
`services.AddEmbedPdf(options => …)` extension in a separate
`EmbedPDF.Extensions.DependencyInjection` assembly so the core package carries no DI dependency.

**D5. Platform services.** *Confirmed 2026-11-20 (execute instruction).*

| Plugin service | Apple | .NET recommendation | Cost |
|---|---|---|---|
| Actions JavaScript | JavaScriptCore | Jint, a managed interpreter: no native code, every RID, AOT compatible | slower than a JIT engine; acceptable for form scripts |
| Signing | `KeychainSigner` | `X509CertificateSigner` over `X509Certificate2` and `System.Security.Cryptography.Pkcs`, loading from `X509Store` or a PKCS#12 file | Linux has no system user store, so Linux callers pass a file |
| Trust anchors | bundled | embedded resource plus `X509Chain` with custom trust | none beyond the resource |
| Stamp library | file store | `FileStampLibraryStore` under `Environment.SpecialFolder.LocalApplicationData` | none |
| Locales | bundle JSON | embedded-resource loader | none |
| Haptics, feedback | UIKit feedback | interface with a no-op default; desktop has no haptics | none |
| Document fetch | `URLSessionDocumentFetcher` | `HttpClientDocumentFetcher`, taking an `HttpClient` so callers can use `IHttpClientFactory` | none |
| Signature transport | `SignatureTransport.swift` | `HttpClient`-based transport | none |

**D6. No UI layer (decided 2026-11-20).** *Confirmed 2026-11-20 (execute instruction).* Apple has SwiftUI and Android has Compose: one
first-party framework each, so a UI package there is the obvious next layer. .NET has no such
framework. WPF and WinUI 3 are Windows only, .NET MAUI has no Linux, and Avalonia is third
party. Picking one would serve only that framework's users, and there is no Windows host to
develop or test the Windows ones on. So the C# package stops at the models: `EmbedPDF.Models` is
the parity line, and an app binds it to its own views. Cost: every .NET consumer writes the page
view, tiling, gesture handling and panels that `EmbedPDFUI` gives Swift apps for free, and the
models must document their inputs well enough to make that practical (Phase 4). A UI package can
be added later as a separate plan without changing the models.

**D7. Publishing.** *Confirmed 2026-11-20 (execute instruction).* `.github/workflows/release-dotnet.yml` already exists for `EmbedPDF.Engine`.
Recommendation: extend it to the four new packages and publish to GitHub Packages on
the runtime repository, where release assets already go. Cost: GitHub Packages
NuGet feeds need a token even to read, which every consumer's `nuget.config` must carry. nuget.org
avoids that but needs a reserved prefix and signing we don't have. Publishing itself needs the
user's separate approval.

**D8. API stability.** *Confirmed 2026-11-20 (execute instruction).* `Microsoft.CodeAnalysis.PublicApiAnalyzers` with `PublicAPI.Shipped.txt`
and `PublicAPI.Unshipped.txt` per assembly. A public surface change fails the build until the file
changes in the same commit. After the first release, add `EnablePackageValidation` with that
release as the baseline.

## Review Focus

- Cancellation mid-call: cancelling the token during `OpenAsync(uri)`, a search or a render
  cancels the Rust token, closes a loading document, and never completes a `TaskCompletionSource`
  twice or after disposal. Phase 2 pins it on `OpenAsync(uri)` against a stalling loopback
  `HttpListener`.
- Disposal under load: `DisposeAsync` on a viewer with renders in flight waits for or cancels
  them, a second `DisposeAsync` shares the first's completion, and a viewer abandoned to the GC
  releases native handles from its finalizer without blocking. Phase 2 pins it, porting the
  pattern of `platforms/dotnet/EmbedPDF.Tests/LifetimeTests.cs`.
- Callbacks from Rust threads: a foreign trait (`CommandHandler`, `StampLibraryStore`, the
  JavaScript engine) runs on a Rust thread, and a model must not raise `PropertyChanged` there.
  Phase 4 pins it by installing a single-threaded `SynchronizationContext` in the test and
  asserting every notification arrives on its thread.
- Trimmed and NativeAOT consumers: `dotnet publish -p:PublishAot=true` of the sample produces a
  binary that opens a document, so generated P/Invokes, callback thunks and JSON contexts survive
  trimming. Phase 5 pins it in the container on `linux-x64` and `linux-arm64`.
- Native library resolution: a consumer restoring from the package, with no `EPDF_NATIVE_DIR` or
  `LD_LIBRARY_PATH`, loads the right `runtimes/<rid>/native` library on each RID, and a missing
  RID fails with a `PdfException` naming the RID rather than a bare `DllNotFoundException`.
  Phase 1 pins it with a consumer test like `platforms/dotnet/packaging/Smoke.cs`.

---

## Phase 0: Generator spike and decisions

About 4 days. Exit: a throwaway branch where generated C# for every component of
`epdf-viewer-host` compiles under `net10.0`, and a test opens a fixture, reads its page count and
receives one foreign-trait callback, on `linux-arm64` in the dev container.

- [x] Try the C# generator against `epdf-viewer-host` at `uniffi =0.32.0`. Record the generator
  version, whether it needed a fork (D1), and any Rust names that collide with C# keywords or
  members, as Kotlin's `message` and `close` did.
- [x] Confirm the generator's `access_modifier = "internal"` setting (D3) and that callbacks work
  when the generated types are internal.
- [x] Check the generated P/Invoke style against the trim and AOT analyzers; record warnings.
- [x] Pin the Jint version and record its license in this file.
- [x] Record D1 to D8 as confirmed or changed at the top of this file.

## Phase 1: Runtime package

About 4 days. Exit: `EmbedPDF.Runtime` packs for all five RIDs, and a consumer project restoring
it from a local feed loads the library and passes the Phase 0 test on `linux-arm64`, with a
validation record. *Changed 2026-11-20:* the Phase 1 job could not use the macOS host, so
`osx-arm64` validation comes from `.github/workflows/release-dotnet.yml` (`runtime-validate`) or a
later manual run, and the five-RID pack is that workflow's `runtime-pack` job. In the container,
`linux-arm64` and `linux-x64` packed into one package.

- [x] `crates/uniffi-bindgen-cs` wrapper (D1) and a `platforms/dotnet/generate-bindings.sh` that
  writes generated C# under `platforms/dotnet/EmbedPDF.Viewer/Generated/`, git-ignored.
- [x] Build `epdf-viewer-host` as a cdylib per RID. Extend `platforms/dotnet/packaging/package.py`
  and its schema to a second package, keeping the checksum-verified input rule.
- [x] `platforms/dotnet/dotnet-engine-hash.sh` and `check-dotnet-engine.sh`, mirroring
  `platforms/apple/apple-engine-hash.sh` and `check-apple-engine.sh`.
- [x] A `NativeLibrary.SetDllImportResolver` that maps the generated library name to the package
  layout and turns a missing RID into a typed error (Review Focus 5).
- [x] Record the packaged library size per RID in the README. The number belongs in the record,
  not a guess.

### Phase 1 notes (2026-11-20)

Task plan: `docs/superpowers/plans/2026-11-20-dotnet-parity/01-runtime-package.md`. Branch
`research/dotnet-parity-p1`, not pushed.

Naming. `platforms/dotnet/EmbedPDF` is the `EmbedPDF.Engine` package and stays untouched, so the
facade lives at `platforms/dotnet/EmbedPDF.Viewer/` (package id `EmbedPDF`, assembly `EmbedPDF.Viewer`, namespace
`EmbedPDF`) and the runtime package project at `platforms/dotnet/EmbedPDF.Runtime/`. Generated C#
goes to `platforms/dotnet/EmbedPDF.Viewer/Generated/`. The four spike tests moved to
`platforms/dotnet/EmbedPDF.Viewer.Tests/`, and `platforms/dotnet/spike/` is gone.

Packaging. `package.py` gained `stage-runtime` (one RID: host plus checksum-verified PDFium, the
runpath set to `$ORIGIN` with `patchelf` or to `@loader_path` with `install_name_tool`, native
inspection, a slice record), `pack-runtime` (merges slices after re-hashing every file),
`pack-facade` and `validate-runtime`. The manifest schema takes `EmbedPDF.Runtime` with several
packed RIDs and no fonts, and the record schema takes `package` and `phase` in place of `slice`.
`crates/epdf-viewer-host/build.rs` is unchanged: it is an `apple-engine-hash.sh` input, so the
runpath is fixed at staging rather than at link time.

Two findings. The host is linked with `-C strip=symbols` (29,367,528 bytes on `linux-arm64`
instead of 37,946,320), and stripping removes the uniffi metadata the generator reads, so bindings
come from an unstripped build of the same tree. The consumer run shows they match the stripped
library. Second, `package.py` is a hash input, so any change to it moves the engine version; the
pin is the last commit before staging.

Validation. `linux-arm64`: native, in the dev container. `linux-x64`: cross-built with
`crossbuild-essential-amd64`, then staged and consumed under the container's x86_64 emulation with
the x64 SDK 10.0.403, recorded as `container-emulated`. Both consumers pass the RID-specific and
portable layouts, report `NativeRuntimeNotFoundException` naming the RID when the host is hidden,
and fail when PDFium is hidden. `osx-arm64`, `osx-x64` and `win-x64` stay `UNVALIDATED`; the
workflow builds, packs and validates them but has not run. Sizes are in `platforms/dotnet/README.md`,
"Viewer packages".

Layout. The runtime package keeps `epdf-assets.json` (the slice record), `epdf-runtime.json`
(the PDFium pin) and the license files under `runtimes/<rid>/native/` beside the libraries. That
matches `EmbedPDF.Engine`, whose `package.py pack` puts the same identity files, notices and fonts
in `native/`, so both packages read the same way.

Review fixes (2026-11-20). The engine hash now covers `LICENSE` and
`packages/engine/runtime/engine-runtime-build.json`, which staging copies into every slice, and a
test edits each packaged input and checks the hash moves. The resolver keeps the loader's error
when a host is present but won't load ("found but failed to load", inner exception or
`AggregateException`). `pack-runtime` checks each slice's archive hash against the pin, refuses
slices staged from a dirty tree without `--allow-dirty`, compares packed bytes rather than names,
removes its staging directory, and lists package paths in the manifest. `EmbedPDF` tracks its
public surface with `Microsoft.CodeAnalysis.PublicApiAnalyzers` 5.6.0 (D8). The workflow validates
`linux-x64` natively on an x64 runner and merges every row's status into one manifest
(`runtime-record`). Each fix that touched `package.py` moved the engine version: the pin is now
`0.0.0-engine.96f2f8af10194b63` and both records were rerun at `0d97ddc53`.

CI (2026-11-20). Run 44799578194 of `release-dotnet.yml` at `8709bf4fb` passed every job: all
five runtime slices, the merged pack, and the local-feed consumer on each RID's native runner.
All five RIDs are `VALIDATED` at engine `0.0.0-engine.822686307291c580`, and those records
replaced the container ones under `platforms/dotnet/validation/2026-11-20-phase-1-runtime-<rid>/`.
Two Windows bugs surfaced on the way and are fixed: backslash paths in slice records, and
`EmbedPDF.dll` colliding with win-x64 PDFium's `embedpdf.dll` (now `EmbedPDF.Viewer.dll`, see D3).
This supersedes the `osx-arm64` caveat in the Phase 1 exit above.

## Phase 2: Facade core

About 1 week. Exit: `PdfViewer` opens, retries, renames and closes documents from bytes, a stream,
a file path and a URL, with tests passing in the container.

Port, from `platforms/apple/EmbedPDF/Sources/EmbedPDF/`: `PDFViewer.swift`, `PDFError.swift`,
`ViewerConfigBuilder.swift`, `StateRevisionHub.swift`, `EventRelay.swift`, `DocumentFetch.swift`,
`DocumentPermission.swift`, `PageGeometry.swift`, `PageRenderResolution.swift`, `JSONValue.swift`
(as `System.Text.Json.Nodes.JsonNode` where it fits), `WireInteger.swift`, `Registration.swift`,
`ContentInclusion.swift`, `Experimental.swift` (as `[Experimental("EPDF001")]`) and
`Signposts.swift` (as `ActivitySource`).

- [x] `HttpClientDocumentFetcher` matching `URLSessionDocumentFetcher`'s cancellation and error
  mapping.
- [x] API coverage check for C#. If the Android plan's `scripts/check-api-coverage.py` exists,
  add `--platform csharp`; otherwise this phase creates it from
  `platforms/apple/check-swift-api-coverage.py`. Manifest at
  `platforms/dotnet/csharp-api-coverage.json`, matching public members by reflection over the
  built assembly. Unported members are `pending` with the phase that owns them, so the check
  passes from day one and shrinks to zero.
- [x] PublicApiAnalyzers (D8) on `EmbedPDF`.
- [x] `EmbedPDF.Extensions.DependencyInjection` with `AddEmbedPdf` (D4).
- [x] Every public entry point calls `NativeRuntime.EnsureLoaded()` before it touches generated
  code, so a missing runtime reaches the caller as `NativeRuntimeNotFoundException`, not wrapped
  in a `TypeInitializationException` from a generated static constructor. A test pins it (deferred
  from the Phase 1 review).
- [x] Tests: port `ViewerTests`, `DocumentAPITests`, `PDFErrorTests`, `RevisionHubTests`,
  `StreamRuleTests`, `IntegerSurfaceTests`, `FetcherExportTests`, `ViewerConfigBuilderTests` and
  `DisabledPluginTests`, plus the two Review Focus tests owned here.

### Phase 2 notes (2026-11-20)

Task plan: `docs/superpowers/plans/2026-11-20-dotnet-parity/02-facade-core.md`. Branch
`research/dotnet-parity-p2` from `research/dotnet-parity-p1` at `a54e4cd52`, not pushed.

What exists. `PdfViewer` (`platforms/dotnet/EmbedPDF.Viewer/PdfViewer.cs`,
`PdfViewer.Documents.cs`) opens from `ReadOnlyMemory<byte>`, `Stream`, a file path
(`OpenFileAsync`), a `Uri` and a saved layer (`OpenLayerAsync`), and covers every document member
of `PDFViewer.swift`. `PdfException` carries `Name`, `PdfErrorCode Code` and `Detail`;
`PdfViewerOptions` is a record with a fluent `PdfViewerOptionsBuilder`; `HttpClientDocumentFetcher`
implements a public `IDocumentFetcher`; the three streams are `IAsyncEnumerable<T>` objects that
subscribe when the method returns (`BroadcastHub.cs`). `AddEmbedPdf` lives in
`platforms/dotnet/EmbedPDF.Extensions.DependencyInjection/`. The coverage check is
`scripts/check-api-coverage.py --platform csharp` over `platforms/dotnet/csharp-api-coverage.json`:
880 members, 39 ported, 706 pending, 135 exclusions. `EmbedPDF.Viewer.Tests` runs 293 test cases,
all passing (321 after the review fixes below); `EmbedPDF.Tests` passes its 36 with
`EPDF_NATIVE_DIR` and PDFium on `LD_LIBRARY_PATH`, and `packaging` its 26.

Deviations, each with its reason:

- An open takes an `OpenDocumentOptions` record (`Name`, `Password`, `Activate`) rather than
  three optional parameters. PublicApiAnalyzers refuses several overloads with optional
  parameters (RS0026); a record lets an open setting grow without a new overload.
- The plugin config records (`SearchConfig` to `SignatureConfig`, `FeedbackConfig`) have no
  members yet. They install their plugin with the engine's defaults, and `FeedbackConfig` passes a
  no-op provider. Each wave adds its config's fields.
- A call whose Rust export takes no `CancelToken` checks the .NET token before posting and then
  runs to completion, as the Swift call does. Only `OpenAsync(Uri)`, `OpenLayerAsync`, `SaveAsync`
  and `SaveLayerAsync` cancel mid-call. A cancelled token surfaces as
  `OperationCanceledException` whose inner exception is the kernel's `operation-cancelled`.
- Swift's deprecated single-consumer `stateRevisions` and `reportedErrors` have no
  counterpart, so `DeprecatedStreamTests` is not ported.
- `DisposeAsync` replaces Swift's `destroy()`. A call after it throws `PdfException` with
  `instance-closed`, never `ObjectDisposedException`.
- Document events are unbounded per subscriber, as Swift's stream is (changed after review,
  2026-11-20; the first cut dropped the oldest past 1,024). `TryWrite` on an unbounded channel
  never blocks the kernel thread, and a dropped `Opened` or `Closed` would leave a consumer's
  tab list wrong. The cost: a subscriber that stops reading holds every later event in memory.
  Revisions (newest 1) and reported errors (newest 16) stay bounded.
- `PageRef` and page indexes are `int`. A `uint` the engine answers past `int.MaxValue` throws
  `invalid-state`; Swift's 64-bit `Int` never does.
- `Exception.Message` is the detail, Swift's `localizedDescription`. There is no
  `"code: detail"` description; `ToString()` stays the framework's.
- No type ports `JSONValue.swift`: Wave D uses `JsonNode`. No member wears
  `[Experimental("EPDF001")]` yet, since the packaged host has no `manual-bridge` exports; the
  id is `Experimental.Diagnostic`. `PageGeometry` has no image overload until Wave A's
  `PixelBuffer`.
- `HttpClientDocumentFetcher` reads `file:` URLs itself, which `URLSession` did for Swift.
- `scripts/check-api-coverage.py` holds the Swift check, and
  `platforms/apple/check-swift-api-coverage.py` is now a wrapper over it (`0d6c8b0b3`) with the
  same command line and the `inventory`/`validate` functions `platforms/apple/tests` import.
  Its output and exit code are byte-identical before and after, for no flag, `--allow-pending`
  and a bad flag. Both still print 18 errors and exit 1 on this branch, a Rust inventory drift
  that predates this phase.


Review fixes (2026-11-20). An independent review asked for these, each landed with a test that
failed first:

- A fetch cancelled by something other than the kernel, such as `HttpClient.Timeout`, now fails
  the tab with no code, so it is retryable; only a kernel cancel is `AbortError`.
- `PdfViewer` implements `IDisposable` as well, so a container disposed synchronously works.
- Disposal finishes even when releasing the host throws, on the finalizer path too, and a call
  that disposal aborted reports `instance-closed` rather than `operation-cancelled`.
- Every public member checks reference arguments up front; a closed viewer refuses a file or
  stream open before reading it; read errors of the caller's file or stream escape unwrapped,
  as `PdfException`'s remarks now say.
- Fewer copies: whole arrays pass without one, seekable streams and sized HTTP bodies read into
  one buffer, and the shared client recycles pooled connections every 2 minutes.
- A fetcher returning a null task, response or body fails the tab as `InvalidResponse`.
- `BroadcastHub` no longer leaks a token registration when it completes mid-subscribe.
- `PdfException(PdfErrorCode, string, string)` is gone: three strings bound to it and to
  `(name, code, detail)` alike.
- `Registration.Dispose` releases the native handle; `AddEmbedPdf` twice keeps the first.
- The C# coverage check matches overloads by parameter types and asserts each pending row's
  wave from a crate table in `scripts/check-api-coverage.py`.
- `release-dotnet.yml` already ran `EmbedPDF.Viewer.Tests` in `runtime-pack`; it now runs the
  coverage check there too.

What Phase 3 inherits:

- A capability accessor goes through the internal `PdfViewer.CallAsync` or
  `CallCancellableAsync`, which map errors and count the call in flight. Calls on a capability
  handle should do the same, so `DisposeAsync` drains them; `LifetimeTests` renders through the
  generated handle today, which bypasses that count.
- `DisabledPluginTests` and the two accessor theories in `ViewerConfigBuilderTests` call the
  generated accessors. Each wave switches its rows to the public accessor. The plugin cases of
  `IntegerSurfaceTests.swift`, `StreamRuleTests`' generated interaction handle, and the
  metadata calls in `DocumentAPITests` and `ViewerTests` move the same way.
- A test in namespace `EmbedPDF.Viewer.Tests` finds `EmbedPDF`'s public twin before a generated
  type of the same name, so it aliases the generated one after the namespace line
  (`ViewerHostTests.cs`).
- Pending rows keep Swift's `export` where Swift had one. A wave flips its rows to
  `csharp: {type, member}`; the check then requires the member in the built assembly
  (`platforms/dotnet/tools/EmbedPDF.ApiDump`).
- RS0026 and RS0027 shape every overload set: one overload with optional parameters, or an
  options record.

## Phase 3: Plugin capabilities

About 4 weeks across waves. Exit: `csharp-api-coverage.json` has no `pending` rows. *Met 2026-11-26, see "Phase 3 wave E notes".*

Each plugin follows one recipe: public records mirroring the generated ones (D3), a `*Capability`
class wrapping the handle, a `PdfViewer` accessor that throws `PdfException` with
`PdfErrorCode.Unsupported` when the plugin is left out of the options, a coverage manifest
update, and the port of the matching Swift test file. Waves follow the Swift umbrella's
dependency order (`docs/superpowers/plans/2026-10-06-swift-plugins/00-umbrella.md:55-61`), so a
capability never lands before one it requires.

| Wave | Plugins | .NET platform service it brings |
|---|---|---|
| A | metadata, search, render, history, outline, attachment, page-edit, view-manager, shell | render: `PixelBuffer` (port of `CoreGraphicsBridges.swift` without the UI types) |
| B | interaction + feedback, i18n, actions | no-op feedback; embedded-resource locale loader; `JintScriptEngine` (D5) |
| C | stage, selection, link, commands | `CommandHandler` adapter |
| D | annotation (every typed draft and patch from `AnnotationDraft*.swift`), form | source-generated JSON for drafts; image and file annotations take `Stream` |
| E | redaction, measurement, stamp, signature | `FileStampLibraryStore`; `X509CertificateSigner`; trust anchors; `HttpClient` signature transport |

Wave D is the largest single piece, as on Android: its own plan should split drafts from the
capability. Each foreign-trait adapter documents which thread calls it.

*Added 2026-11-20 from the Phase 2 review:* `PdfViewer.Dispose()`, or waiting on
`DisposeAsync()`, from a callback on the kernel thread deadlocks, because destroying the kernel
needs that thread. Phase 2 only documents it (`PdfViewer.cs`, the remarks on both members),
since no public callback exists yet. When waves B and C add callback adapters (`CommandHandler`,
interaction handlers, the script engine, document listeners), the adapter marks the kernel
thread while it runs the app's code, and `Dispose()` and the disposal wait throw
`InvalidOperationException` there instead of hanging. A test calls `Dispose()` from a handler
and expects that exception. The cost is one thread-static check per disposal.

### Phase 3 wave A notes (2026-11-20)

Task plan: `docs/superpowers/plans/2026-11-20-dotnet-parity/03a-wave-a.md`. Branch
`research/dotnet-parity-p3a` from `research/dotnet-parity-p2` at `cf1e8f765`, not pushed.

What exists. Nine capabilities under `platforms/dotnet/EmbedPDF.Viewer/Plugins/`, one file for
the capability and its records (`Search.cs` with `Search.Types.cs`, `Render.cs` with
`Render.Types.cs`) and one `*.Ffi.cs` for the internal mappings. `PixelBuffer`
(`platforms/dotnet/EmbedPDF.Viewer/PixelBuffer.cs`) carries render output, and
`PageGeometry.ToDisplay(PixelBuffer)` is the image overload Phase 2 deferred. `SearchConfig`
gained `Reveal`, `RenderConfig` gained `FullPage` and `Debug`. The coverage check reads 880
members: 225 ported, 520 pending (B 68, C 167, D 143, E 142), 135 exclusions. Every wave A row
is ported; none needed a new exclusion. `EmbedPDF.Viewer.Tests` runs 443 cases, all passing
twice in a row; `EmbedPDF.Tests` passes its 36, `packaging` its 30 and
`scripts/test_check_api_coverage.py` its 11. Release builds of the facade, the DI package, the
tests and `EmbedPDF.ApiDump` report 0 warnings.

What later waves reuse:

- `PdfViewer.Stream(relay, subscribe, token)` in `Plugins/PluginEvents.cs` turns a generated
  listener into an `IAsyncEnumerable<T>`. A plugin subclasses `EventRelay<TFfi, TPublic>` once,
  forwarding `OnEvent` to `Deliver` and `OnEnd` to `Finish`. The stream is unbounded, as
  Swift's `AsyncStream` is, ends on the kernel's `OnEnd`, the caller's token or viewer disposal,
  and cancels the kernel subscription exactly once. A mapping that throws drops that event and
  logs it rather than ending the stream.
- Live streams are tracked by a `StreamRegistry` the viewer owns, and a stream's end callback
  captures only that registry, never the viewer. The kernel's callback table holds each relay
  until the kernel ends it, so a path from a relay to the viewer kept an abandoned viewer alive
  for good (review fix, pinned by
  `LifetimeTests.ALivePluginStreamDoesNotKeepAnAbandonedViewerAlive`). Kernel shutdown does not
  call a listener's `OnEnd`: with the registry close removed from the finalizer path, that test's
  reader never ended. Disposal and the finalizer path both close the registry, and that is what
  ends a reader still holding a stream. Callback adapters in later waves should not count on
  `OnEnd` at shutdown either.
- Argument rules (review fixes): a null argument, or a null element in a collection argument
  (`Arguments.NoNullElements`), throws `ArgumentException` while the call is made in every
  method that is not itself `async`. A present but invalid argument (a negative index or page
  number, an undefined enum value) faults the returned task, because conversions run inside the
  `Run` lambda (`ValidationShapeTests`). The rule for every wave (decided 2026-11-20): a method
  that is not `async` throws `ArgumentNullException` while the call is made; a method declared
  `async` reports a null argument through its faulted task, and is not split to make it eager.
- Public enums reach the engine only through `Ffi.Defined` or an explicit switch arm, so an
  undefined value is `invalid-input` (`EnumRangeTests`). Phase 2's `SaveMode`,
  `ViewerSettingsField` and `StandardCommandList` mappings follow the same rule since
  `be98c4b1c`, and `SetDocumentOrderAsync` refuses a null id since `0a94b2e4a`.
- `PdfViewer.Run` and `RunCancellable` wrap a capability call the way `CallAsync` wraps a host
  call, so disposal drains capability calls too.
- `Plugins/Shared.cs` holds the cross-plugin types: `ResourceStatus`, `DocumentEventOrigin`,
  `PdfQuad`, `AnnotationRef`, `PageDestination`, `PageLinkTarget`, `LinkActivation` and
  `PdfActionTree`. Waves B to D extend these rather than adding twins. `PdfActionTree` owns the
  generated tree's handle; wave B adds parsing and execution to it.
- Public enums that mirror a generated enum convert by value (`(Public)(int)generated`).
  `EnumMirrorTests` pins that each pair lists the same cases in the same order; add a row per
  new pair.
- `NullArgumentTests.EveryWaveACapabilityChecksEveryReferenceArgument` reflects over each
  capability; later waves add theirs to its list and extend `Valid` for their types.
- `DisabledPluginTests` rows for wave A call the public accessors through `Public(...)`.

Deviations, each with its reason:

- Accessors are `GetMetadataAsync(document)`, `GetSearchAsync(document)` and so on, through
  `GetViewManagerAsync()`, not Swift's `search(for:)` shape. Decided 2026-11-20: .NET method
  names are verbs, and `viewer.SearchAsync(document)` or `viewer.RenderAsync(document)` would
  read as doing the thing rather than returning a capability. D4 records the change; later
  waves follow it.
- Plugin event streams are unbounded per subscriber, where D4's table says a bounded
  `Channel<T>`. This follows the Phase 2 decision for document events: a dropped `Undone`,
  `Changed` or `Closed` event would leave a consumer's state wrong, and `TryWrite` on an
  unbounded channel never blocks the kernel thread. The cost is the same: a subscriber that
  stops reading holds every later event in memory.
- Capabilities are `IDisposable`, not `IAsyncDisposable`: releasing a capability handle never
  waits on the kernel. `RenderViewDemand` is `IAsyncDisposable` because its `DisposeAsync` is the
  engine's `dispose`, which is a kernel call.
- Single-purpose event records are folded into their event case
  (`SearchEvent.Completed(int HitCount)` instead of `Completed(SearchCompletedEvent)`), which
  removes a type per case. Metadata keeps `MetadataUpdatedEvent` and
  `CustomMetadataUpdatedEvent` because they carry three fields each.
- `RenderPageOptions` takes `ContentInclusion` for its two layers, not `bool?`, matching every
  other render member.
- `PixelBuffer`'s constructor throws `ArgumentException`, not `PdfException`: it validates the
  caller's own array, and the plan's `invalid-input` would have dressed a programming error as
  an engine refusal. A malformed raster from the engine is `invalid-state`.
- The turned copy from `PageGeometry.ToDisplay(PixelBuffer)` keeps the source's format and
  alpha. Swift's draws into a premultiplied context, so its output is always premultiplied.
- Shell props take a `JsonTypeInfo<T>` where Swift takes any `Encodable`, so the typed overloads
  stay trim and AOT safe. Swift's `focus` open option is not exposed, as in Swift.
- The page-attachment case of `AttachmentTests.swift` creates its file through annotation
  drafts, so it ports with wave D. The deprecated `in:` accessors in `AccessorNameTests.swift`
  have no C# counterpart; C# never shipped them.
- The snapshot test drives the snapshot tool through the generated interaction handle, since
  interaction is wave B. Wave B switches it to the public capability.
- History's task ran after page edit's, because `HistoryTests.swift` edits pages.

### Phase 3 wave B notes (2026-11-20)

Task plan: `docs/superpowers/plans/2026-11-20-dotnet-parity/03b-wave-b.md`. Branch
`research/dotnet-parity-p3b` from `research/dotnet-parity-p3a` at `7e62f53a1`, not pushed. Wave
A's close (`110916bf9`, with the Phase 2 follow-ups `30bcc1c35` and `99c258f75`) is merged in at
`b6731ee64`. The one conflict was the `Dispose`/`DisposeAsync` remarks in `PdfViewer.cs`; the
merge keeps the kernel-thread guard and its wording and folds in `99c258f75`'s point that
starting disposal from a callback is safe.

What exists. Four capabilities under `platforms/dotnet/EmbedPDF.Viewer/Plugins/`:
`InteractionCapability` (with `ToolId`, the pointer records and `IInteractionHandler`),
`FeedbackCapability` (with `IPlatformFeedback`), `I18nCapability` (with `ILocaleLoader` and
`EmbeddedResourceLocaleLoader`) and `ActionsCapability` (with `IActionExecutor`,
`IActionUiHandler`, `IActionSubmitHandler`, `IScriptEngineFactory`, `IScriptEngine` and
`ScriptEngineCheck`). `FeedbackConfig`, `I18nConfig` and `ActionsConfig` carry their fields.
`JintScriptEngineFactory` ships in a new assembly, `platforms/dotnet/EmbedPDF.Scripting.Jint`
(package `EmbedPDF.Scripting.Jint`, Jint 4.18.0). The coverage check reads 880 members: 293
ported, 452 pending (C 167, D 143, E 142), 135 exclusions. All 68 wave B rows are ported
(interaction 30, i18n 15, actions 23); none needed an exclusion, and feedback has no rows, as
the manifest's crate note says. `EmbedPDF.Viewer.Tests` runs 610 cases after the merge and review fixes, 609 passing twice
in a row and one skipped (the pointer latency test, which runs with `EMBEDPDF_LATENCY=1`: p95
0.101 ms against an 8.33 ms frame on `linux-arm64`). `EmbedPDF.Tests` passes its 36,
`packaging` its 30 and `scripts/test_check_api_coverage.py` its 11. Release builds of the
facade, the DI package, the Jint package, the tests and `EmbedPDF.ApiDump` report 0 warnings.

Kernel callbacks. The Phase 3 note from the Phase 2 review (added on `research/dotnet-parity-p3a` in `99c258f75`) is done. `KernelCallback`
(`platforms/dotnet/EmbedPDF.Viewer/KernelCallback.cs`) marks the thread with the viewer's
`KernelMark` while app code runs on the kernel thread; the mark holds no reference to the
viewer. `Dispose()` there throws `InvalidOperationException`. `DisposeAsync()` there starts
disposal and returns a task that can be awaited, while blocking on it with
`GetAwaiter().GetResult()` throws. Blocking through `AsTask()` cannot be detected, and the
remarks say so. Tests dispose from an interaction handler, a feedback provider, a UI handler and
a script engine. Every adapter catches what app code throws: a member with an error type
(`OnDown`, the UI and submit handlers, the engine factory) answers `HostException.Failed` with
the message, `Evaluate` answers `Threw`, a `bool` member answers `false`, and the rest log and
drop it. Left alone, an exception from a member with no error type reaches Rust as an
unexpected callback error, which panics the kernel thread.

Calling threads. On the kernel thread, synchronous and marked: `IInteractionHandler`, the tool
permission (`Func<bool>`), `IPlatformFeedback`, `IActionUiHandler`, `IActionSubmitHandler`,
`IScriptEngineFactory` and `IScriptEngine`. On the thread pool, where disposing is safe:
`ILocaleLoader`, `IActionExecutor` and the operation of `RunDocumentVerbAsync`. Each
interface's remarks name its thread.

Jint. `Jint.dll` for `net10.0` declares `IsAotCompatible` and builds with no analyzer warning,
but a NativeAOT publish of a console app running one script reports IL2026 from
`Jint.Options.Apply` and `DefaultTypeConverter.BuildDelegate` plus IL2104 and IL3053 for the
assembly. Those are its CLR interop paths, which the engine never enables, but they are why
Jint is not a dependency of `EmbedPDF`: an AOT consumer that wants document JavaScript takes
the warnings with `EmbedPDF.Scripting.Jint`, and one that doesn't never sees them. Each realm
owns a background thread with a 16 MiB stack and one `Jint.Engine` with no CLR access, a memory
limit (64 MiB by default), `Constraints.StackOverflowGuard` and a regex timeout (300 ms by
default). The time limit is a per-call `Constraint`, so a runaway script stops inside the
interpreter and frees its thread; the wait gives up 250 ms past the limit in case it doesn't,
and anything that stops a script after its limit reads as a timeout. Packing the package into a
release is Phase 6.

Review fixes (2026-11-20). The first cut capped recursion with
`Constraints.MaxExecutionStackCount`, which counts call expressions only. A probe showed a
getter, a Proxy trap, a `toString` coercion or `new` recursing past it overflowed the native
stack and ended the process, on a 16 MiB stack as on 1 MiB; setting that count also turns off
Jint's `StackOverflowGuard`. The guard measures the stack on every entry into a script function
and raises a catchable `RangeError`, so the engine now uses it alone (`39ad80a0f`), and
`MaxCallDepth` is gone: `ThreadStackBytes` (at least 1 MiB, about 2 KiB per plain call) is the
bound. `JintScriptEngineTests.AnIndirectRecursionIsABudgetFailure` crashed the test host before
the change. A catastrophic regex used to run for Jint's default 10 s after its script timed out;
`JintScriptEngineOptions.RegexTimeout` (`4b5c0af02`) stops it, as a fixed value because Jint
reads it once when the engine is built. The executor adapter releases the node and context it
is given (`d9cb05ac4`), the guarded disposal task flows the execution context (`70864e8fc`), and
the callback docs now say what a registered or configured callback keeps alive and that none may
block waiting for the viewer.

Wave A's review rules apply here too (`6d674ac69`). Every wave B stream goes through
`PdfViewer.Stream` and so the `StreamRegistry`, and
`LifetimeTests.LiveWaveBStreamsAndCallbacksDoNotKeepAnAbandonedViewerAlive` abandons a viewer
holding interaction, i18n and actions streams plus a handler, a tool permission, a feedback
provider, a UI handler and an executor, and expects the viewer collected and every stream ended.
Public enums reach the engine through `Ffi.Defined` or an explicit arm (`EnumRangeTests`, 18 new
rows). Conversions run inside the `Run` lambda, so an invalid argument faults the task
(`ValidationShapeTests.InvalidWaveBArgumentsFaultTheReturnedTask`; `DispatchPointer` returns no
task and throws at once). A null element or dictionary value throws `ArgumentException` up front
in methods that are not `async` (`Arguments.NoNullElements`, and `Arguments.NoNullValues` for
cursor, translation and parameter dictionaries); `RegisterToolAsync` and `RegisterLocaleAsync`
are declared `async` and fault their task instead, per the null-argument rule.

Deviations, each with its reason:

- Swift's completion objects (`LocaleLoadCompletion`, `ActionExecutorCompletion`,
  `DocumentVerbCompletion`) are `Task`-returning members (`ILocaleLoader.LoadAsync`,
  `IActionExecutor.ExecuteAsync` answering `ActionExecutorResult`, `Func<Task>` for a document
  verb), as `IDocumentFetcher` was in Phase 2. `ToolPermission` is a `Func<bool>`.
- Swift's `BundleLocaleLoader` is `EmbeddedResourceLocaleLoader(assembly, resourcePrefix)`; the
  pack for `es` is the manifest resource `resourcePrefix + "es.json"`. The library bundles no
  locale files, as Swift's doesn't; the test assembly embeds Swift's `es.json`.
- `t` is `TranslateAsync`, `register` is `RegisterLocaleAsync`, `actionTree(for:)` is
  `GetActionTreeAsync`, `setUIHandler` is `SetUiHandlerAsync`. Swift's deprecated string
  overloads of the tool members have no counterpart.
- `InteractionSettings.DefaultTool` and the tool event fields are `ToolId`, where Swift keeps
  `String`.
- Action unions name their reference fields `AnnotationRef`, `PageRef` and `FieldRef`, because a
  case's property can't share a name with a sibling case (`ActionSource.Annotation`). The Phase 0
  generator renames hit the same rule.
- A UI handler that throws fails the whole `ExecuteAsync` with `HostError`, not one node. That
  is the engine's documented behaviour; the plan's first cut assumed a failed node. A submit
  handler that throws fails only its node, with the message as the detail
  (`ActionsTests.AThrowingSubmitHandlerFailsTheNodeAndTheViewerKeepsWorking`), and is called
  only when the form plugin is installed.
- `PdfObjectRef` is a record over its JSON and `PdfActionNode` a record of type and JSON; Swift
  passes the generated objects. A malformed reference is `invalid-input` when it is sent.
- Jint answers a memory-limit stop as `Threw("out of memory: ...")`, which the viewer reports as
  `InvalidOutput` and poisons the realm. Only a script-visible error can be `Budget`, and Jint's
  memory stop is not one. JavaScriptCore enforces no memory budget at all.
- `ScriptEngineCheck.RunAsync` takes a `CancellationToken` that is checked before the run starts;
  the run itself cannot be cancelled, as in Swift.

### Phase 3 wave C notes (2026-11-20)

Task plan: `docs/superpowers/plans/2026-11-20-dotnet-parity/03c-wave-c.md`. Branch
`research/dotnet-parity-p3c` from `research/dotnet-parity-p3b` at `2e7df6c39`, not pushed.
Wave B's close (`f820ffc16`) is merged in at `d8f574ce4` with no conflicts. After the merge the
wave C callbacks carry wave B's documentation rules: the initial-view provider, the
`CommandHandler` hooks and `SaveFile` say not to block on the viewer there, and
`ProvideInitialViewAsync` and `SetCommandsPlatform` say what a capture keeps alive. Their
ExecutionContext behaviour is unchanged.

What exists. Four capabilities under `platforms/dotnet/EmbedPDF.Viewer/Plugins/`:
`StageCapability` (`Stage.cs`, `Stage.Types.cs`, `Stage.Ffi.cs`, with `IStageInitialViewProvider`),
`SelectionCapability`, `LinkCapability` and `CommandsCapability` (`Commands.cs`,
`Commands.Types.cs`, with `CommandHandler` and `IStandardCommandsPlatform`), plus
`PageNavigator`, `PageZoom` and `ToolSwitcher` in `TaskEntryPoints.cs`, the port of
`TaskEntryPoints.swift`. `StageConfig` gained `Settings` and `Responsive`;
`PdfViewer.SetCommandsPlatform` is the port of `setCommandsPlatform`. The coverage check reads 880
members: 456 ported, 285 pending (D 143, E 142), 139 exclusions. All 167 wave C rows are done: 163
ported and 4 excluded. `EmbedPDF.Viewer.Tests` runs 766 cases after the merge, 765 passing twice in a row and
one skipped (the latency test); `packaging` passes its 30 and `scripts/test_check_api_coverage.py` its
11. Release builds of the facade, the DI package, the Jint package, the tests and
`EmbedPDF.ApiDump` report 0 warnings.

The stage surface is plain values: `double` coordinates in `PdfPoint`, `PdfSize` and `PdfRect`,
`Camera`, `ViewAnchor` and `StageScrollMetrics` as record structs, nothing rounded
(`StageTests.CoordinatesRoundTripWithoutRounding`). Phase 4 can drive it from a viewport size
(`SetViewportSizeAsync`), a scroll offset (`ScrollToAsync`, `GetScrollMetricsAsync`, `WheelByAsync`)
and `PointerSample`s through `InteractionCapability.DispatchPointer` once
`RegisterScrollHandlerAsync` has run. `SetCamera`, `PanBy`, `ScrollBy` and `ZoomAround` return no
task, as in Swift: they convert first, so an undefined enum throws `invalid-input` at once, and after
the document or viewer has gone they do nothing, as `DispatchPointer` does.

Calling threads. On the kernel thread, synchronous and marked: `IStageInitialViewProvider`, the
`CommandHandler` derivations (`enabled`, `active`, `visible`, `iconAccent`, `locked`) and
`IStandardCommandsPlatform.SaveFile`. A throw from the first two is the kernel's host error
(`KernelCallback.RunOrFail<T>`, new here): the stage places the document as if no provider answered,
and a resolve leaves the command disabled. `SaveFile` has no error type, so its throw is logged and
dropped. On the thread pool, where disposing is safe: a `CommandHandler`'s `run` and the two copy
members of `IStandardCommandsPlatform`. A run's failure maps through `FetcherAdapter.Failure`, so a
`PdfException` keeps its name and code and an `OperationCanceledException` is `operation-cancelled`.
A handler whose delegates capture a capability keeps the viewer alive until unregistered, as Swift's
does; `LifetimeTests.LiveWaveCStreamsAndCallbacksDoNotKeepAnAbandonedViewerAlive` covers handlers
that capture nothing.

Exclusions. `scene_key_builds` and `page_state_reads` (stage) and `load_starts` (selection) are test
counters with no export; `resolve_command_text` is a conformance-harness helper that prints
`resolveCommand` as JSON. None has an `ffi.rs` export. `wheel_by` had an export Swift never wrapped,
so C# ports it as `StageCapability.WheelByAsync`.

Deviations, each with its reason:

- Stage destinations take and return the wave A `PageDestination`. The generated
  `StagePageDestination` has the same cases and the same page space
  (`crates/plugins/stage/src/ffi.rs:912-916`, `crates/epdf-ffi/src/annotation.rs:279-285`), so a
  twin would be a second name for one thing.
- `SetAppStateAsync` and `ExecuteAsync` take JSON as `string`, as Shell's props do, where Swift takes
  `Data`. Swift's reason was to refuse bytes that are not UTF-8; the C# twin refuses a string with an
  unpaired surrogate (`Ffi.WellFormed`, `("EncodingError", invalid-input)`), which UTF-8 encoding
  would otherwise turn into U+FFFD.
- Link's `AnnotationLinkTarget` overloads of `resolve`, `activate` and `label`, and
  `TypedOverloadTests.swift`'s link case, wait for wave D, which owns the annotation drafts. Wave C
  takes target JSON.
- A `CommandDefinition` takes its hooks from its `CommandHandler`. There is no public
  `ICommandRunner`, so the Swift route of passing the generated runner with explicit hooks has no
  counterpart. `IStandardCommandsPlatform`'s copies return `Task` instead of answering a completion,
  per the wave B rule.
- `CommandHandler` delegates are constructor parameters, as Swift's closures are; `Hooks` lists the
  ones given.
- Selection's point members take the page first (`SelectWordAtAsync(page, point)`), the engine's
  order, where Swift reads `selectWord(at:on:)`. `beginGesture(at:on:)` is `BeginGestureAtAsync`.
- The entry points are classes with internal constructors that dispose their capability. The source
  audit in `TaskEntryPointTests` reads `Plugins/TaskEntryPoints.cs` as Swift's reads its source.
- `ViewerHostTests` aliases twelve generated command types, since the facade now has public twins
  with those names.
- `IsNavigationEngagedAsync` answers `true` on a fresh document with the default pointer tool; the
  port asserts that rather than Swift's silence on it.

### Phase 3 wave D notes (2026-11-20)

Task plan: `docs/superpowers/plans/2026-11-20-dotnet-parity/03d-wave-d.md`. Branch
`research/dotnet-parity-p3d` from `research/dotnet-parity-p3c` at `b3a1346c3`, not pushed.

What exists. `AnnotationCapability` with four sub-capabilities (`GetSelection()`, `GetTools()`,
`GetDrafts()`, `GetThreads()`, each its own `IDisposable` handle) and `FormCapability`, under
`platforms/dotnet/EmbedPDF.Viewer/Plugins/`. Every typed draft and patch from `AnnotationDraft*.swift`
is a `sealed record` (15 drafts, 17 patches) in `AnnotationDraft.cs`, `.Markup.cs`, `.Shapes.cs` and
`.Boxes.cs`; reads are `AnnotationInfo` with `AnnotationDetails` records per kind
(`AnnotationInfo.cs`, `AnnotationInfo.Kinds.cs`). `FormConfig` gained `Validation`. Link has
`AnnotationLinkTarget` overloads of `ResolveAsync`, `ActivateAsync` and `GetLabelAsync`. The
coverage check reads 880 members: 589 ported, 142 pending (all wave E), 149 exclusions. Of the 143
wave D rows, 133 are ported (84 annotation, 49 form) and 10 excluded. `EmbedPDF.Viewer.Tests` runs 947
cases, 946 passing twice in a row and one skipped (the latency test); `packaging` passes its 30
and `scripts/test_check_api_coverage.py` its 11. Release builds of the facade, the DI package,
the Jint package, the tests and `EmbedPDF.ApiDump` report 0 warnings.

JSON. A draft or patch writes the engine's field bag through an internal `FieldBag` over
`System.Text.Json.Nodes`, keys sorted ordinally at every level with
`JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, so the output matches Swift's exact strings in the
ported tests. The leaf wire records (point, rect, quad, line points, measure, caption offset,
leader, attached file) go through the source-generated `AnnotationJsonContext`, with required
constructor parameters, so a read missing one fails as Swift's `Decodable` does. Nothing uses
reflection. Each patch implements `IAnnotationPatch<TSelf>` with a static `FromJson`, which is
how `GetDefaultsAsync<TPatch>(ToolId)` decodes without reflection. A NaN, an infinity or an
undefined enum value is `invalid-input` (`EncodingError`) before anything reaches the engine; a
declared key of the wrong shape is `invalid-input` (`DecodingError`).

Calling threads. `IAnnotationFilePicker.PickAsync` runs on the thread pool (the kernel awaits the
task), so disposing the viewer there is safe; a throw declines the file and is logged.
`IFormFocusTarget.Focus` runs on the kernel thread, marked: a throw is logged and dropped, and
`Dispose()` there throws `InvalidOperationException` (`FormTests.DisposingFromAFocusTargetThrowsIntoIt`).
`LifetimeTests.LiveWaveDStreamsAndCallbacksDoNotKeepAnAbandonedViewerAlive` covers a picker and a
focus target that capture nothing.

Exclusions that need a decision. Ten Rust members have no `ffi.rs` export and no Swift
counterpart, because they were added after the Swift port: annotation `get_shared`, `is_movable`,
`can_duplicate`, `duplicate`, `when_remeasurable`, `stage_remeasure`, `selection_duplicate`,
`selection_can_duplicate`, and form `duplicate`, `duplicate_widgets` (most from `f74c06c6d`).
Exporting them is a Rust change this phase may not make, so they are exclusions with the new
classification `no-export`. They are also why the Swift check reports Rust inventory drift. A
later pass that exports them for Swift and C# together would turn them into rows.

Deviations, each with its reason:

- Swift's `Nullable<Value>` is `JsonNullable<T>`: `System.Nullable<T>` owns the name. A member is
  `JsonNullable<T>?`, so left out, `Null` and a value stay three states. A list value goes in
  through `JsonNullable.Of<IReadOnlyList<double>>(...)`, since C# has no implicit conversion from
  an interface type.
- `AnnotationColor` has `Parse` and `TryParse` and no implicit conversion from `string`; Swift's
  string literal traps on a bad colour, and a C# conversion that throws would hide the same trap.
- Records hold lists as `IReadOnlyList<T>` and compare them by reference, as every C# record does.
  The ported round-trip tests compare the JSON a patch writes, or compare structurally
  (`AnnotationEngineDraftTests.Same`), where Swift compared values.
- `AnnotationInfo.BorderStyle` is top-level `AnnotationBorderStyleName`, and each kind's members are
  a top-level `Annotation*Details` record: a nested type and a property can't share a name in C#.
  `Details` is `null` where Swift has `.untyped`; `Underline` and `Squiggly` share
  `AnnotationTextMarkupDetails` and `Square` and `Circle` share `AnnotationShapeDetails`, picked by
  `Subtype`. Swift's deprecated overloads returning the generated record have no counterpart.
- The sub-capabilities are methods (`GetSelection()`), not properties: each call makes a new
  native handle the caller disposes, which a property would hide.
- `CreateAsync` takes `AnnotationCreateOptions { Tool, Select }` rather than two optional
  parameters, because RS0026 refuses two overloads with optional parameters (Phase 2's rule).
- `AnnotationSelectionCapability.UpdateLinkAsync` takes a new public `PdfLinkTarget` and
  `PdfDestination` (file coordinates), the twins of the generated records Swift re-exports. The
  page-space `PageLinkTarget` from wave A is a different record.
- `PdfMeasurement`, `PdfNumberFormat`, `MeasureOrigin`, `MeasureFraction` and `MeasureLabelPosition`
  are defined here because line, polygon and polyline carry a measure; wave E reuses them.
- Form: `FormValueInput` cases are `Value(string? Text)`, `Checked(bool IsChecked)` and
  `SelectedValues(IReadOnlyList<string> Values)`, since a case's property can't share its name.
  `FormActivationResult.Form` is `Committed`. `uint` fields are `int`. Swift's `widgets(on:)` is
  `GetFillItemsAsync`, and `removeWidget(_:from:)` takes the widget first as Swift does.
- `IntegerSurfaceTests.swift` has no annotation-only or form-only case (its annotation use is the
  redaction case, wave E); `IntegerSurfaceViewerTests.AnnotationNumbersBelowZeroThrowInvalidInput`
  checks the page numbers, object numbers and indexes the drafts write instead. The measurement
  cases of `TypedOverloadTests.swift` wait for wave E.

### Phase 3 wave E notes (2026-11-26)

Task plan: `docs/superpowers/plans/2026-11-20-dotnet-parity/03e-wave-e.md`. Branch
`research/dotnet-parity-p3e` from `research/dotnet-parity-p3d` at `a728cf2c3`, not pushed.

Phase 3 exit is met. The coverage check reads 880 members: 730 ported, 0 pending, 150 exclusions.
Of the 142 wave E rows, 141 are ported (measurement 34, redaction 29, stamp 39, signature 39) and
one is excluded: redaction `page_list_calls` is a `#[cfg(feature = "harness")]` counter with no
export (`crates/plugins/redaction/src/read/pending.rs:315`), classified `internal-resource` like
wave C's `load_starts`. `EmbedPDF.Viewer.Tests` runs 1,101 cases, 1,097 passing twice in a row and
4 skipped: the latency test and the three route tests below. `packaging` passes its 30 and
`scripts/test_check_api_coverage.py` its 13. Release builds of the facade, the DI package, the Jint
package, the tests and `EmbedPDF.ApiDump` report 0 warnings with the trim and AOT analyzers on.

What exists. `RedactionCapability`, `MeasurementCapability`, `StampCapability` and
`SignatureCapability` under `platforms/dotnet/EmbedPDF.Viewer/Plugins/`, each with a `.Types.cs` of
public twins and a `.Ffi.cs` of mappings, and the accessors `GetRedactionAsync`,
`GetMeasurementAsync`, `GetStampsAsync()` (one per viewer, as Swift's `stamps()`) and
`GetSignaturesAsync`. `RedactionConfig`, `MeasurementConfig`, `StampConfig` and `SignatureConfig`
carry their fields; the first three are also their plugin's settings patch, as in Swift. Wave D's
`PdfMeasurement` and `PdfNumberFormat` now map to the generated measure records, refusing NaN, an
infinity or an undefined enum as `invalid-input`. The measurement cases of `TypedOverloadTests.swift`
are ported (`ReadoutAsync(AnnotationInfo)` and `ReadoutAsync(AnnotationRef)` against the JSON
overload).

Platform services (D5). `FileStampLibraryStore` keeps one PDF per library under
`LocalApplicationData/EmbedPDF/stamps` or a given directory, percent-escapes every id byte outside
`A-Z a-z 0-9 - _` so `../x`, `a/b` or `.` stay inside it and read back unchanged, and writes through
a temporary file and one `File.Move`. `X509CertificateSigner` signs with an `X509Certificate2`'s RSA
(PKCS#1 v1.5, SHA-256) or ECDSA key (IEEE P1363 `r || s`), loads from PKCS#12 bytes, a PKCS#12 file
or an `X509Store`, and builds its chain once with `X509Chain` in `CustomRootTrust` over the given
certificates. `TrustAnchors` reads DER, PEM, certificates or embedded resources (`.pem`, `.crt`,
`.cer`, `.der` under a prefix); the Swift side bundles no anchors, and neither does this package.
`HttpClientSignatureTransport` ports `SignatureTransport.swift` over `HttpClient`, with the same
limits and outcomes. The tests use a generated self-signed EC certificate and the TEST-ONLY RSA pair
from `packages/engine/main/test/fixtures/signature-compat/keys/`, never a system store.

Calling threads. On the kernel thread, synchronous and marked: `IStampLibraryStore`,
`ISignatureSigner.GetCertificateChain`, `Algorithm` and `Digest`, and `ISignatureTrust`. A store's
throw is the kernel's host error, so `RestoreAsync` fails with `HostError`; a signer's or trust's
`PdfException` keeps its name and code, anything else is `operation-failed`. `Algorithm` and `Digest`
have no error type, so a throw there is logged and answered with RSA PKCS#1 and SHA-256. On the
thread pool: `ISignatureSigner.SignAsync` and `ISignatureTransport.PostAsync`, whose adapters answer
the completion exactly once (`SignatureTests.APendingSignatureDoesNotBlockTheViewer`,
`SignatureTransportTests.ATransportThatThrowsOrAnswersNothingFailsTheRequestOnce`).
`LifetimeTests.LiveWaveEStreamsAndCallbacksDoNotKeepAnAbandonedViewerAlive` covers the four streams,
a persisting store, a signer, trust and a transport that capture nothing.

Deviations, each with its reason:

- Swift's completion objects (`SignatureSignCompletion`, `SignatureTransportCompletion`) are
  `Task`-returning members, per the wave B rule: `ISignatureSigner.SignAsync` answers the signature,
  and `ISignatureTransport.PostAsync` answers a `SignatureTransportOutcome` union (`Replied`,
  `TimedOut`, `TooLarge`, `Failed`).
- D5 names `System.Security.Cryptography.Pkcs` for the signer. It is not used: the engine assembles
  the CMS and asks the signer only for a raw signature over the signed attributes
  (`crates/plugins/signature/src/ffi/ports.rs:56-62`), which the shared framework's `RSA` and `ECDsa`
  give. No new package reference, so nothing to isolate for AOT. `CompleteSignatureAsync` still takes
  a caller's detached CMS for the two-phase path, as in Swift.
- `StampPreview` is `Data` and `MimeType`; Swift's decoded `CGImage` has no counterpart (D6).
- Swift's `place(_:in:on:center:targetWidth:rotation:)` and `place(_:in:onPages:...)` are
  `PlaceAsync` and `PlaceOnPagesAsync` with a `StampPlacementOptions` record, and `createAsset(from:)`
  is `CreateAssetFromAnnotationsAsync`; `validate(field)` is `ValidateFieldAsync`, `apply(pages:)` is
  `ApplyPagesAsync`, `pendingItem` is `GetPendingItemAsync`. RS0026 refuses the overload sets; the
  three `ReadoutAsync` overloads suppress it, as wave D's typed and JSON pairs do.
- Union case members the generator renamed keep those names (`MeasurementPageTarget.Page.PageValue`,
  `StampMark.Text.TextValue`, `SignaturePlaceTarget.Field.FieldValue`), since a nested record's
  property can't share its name.
- A signer that throws `PdfException` with `operation-cancelled` fails `SignAsync` with that
  `PdfException`, not `OperationCanceledException`, which stays reserved for the caller's token.
- `FileStampLibraryStore` escapes ids itself rather than with `Uri.EscapeDataString`, so `.` and `~`
  are escaped too and no id can name `.` or `..`.
- The three route tests of `SignatureTransportTests.swift` are ported but skipped without
  `EMBEDPDF_SIGNATURE_ROUTES`, as Swift's are; the helper needs `packages/core/signature/dist`, which
  this wave did not build.
- The deprecated fixed-width overloads (`pendingCount` as `UInt32`, `analyzeChanges(since: UInt32)`)
  have no counterpart; C# never shipped them.

Finding. A `MarkMatchesAsync(new SearchQuery("e"))` over every page of `report.pdf`, cancelled
300 ms in, did not return within 90 s and held a core at 100%: the search runs on the kernel thread,
so the abort posted by `call_promise_cancellable` (`crates/epdf-ffi/src/bound.rs:118-135`) waits
behind it. Swift's test cancels before the call starts, and so does the port
(`RedactionTests.MarkMatchesCancelsWhenTheTaskIsCancelled`). The fix would be in Rust, so this wave
records it rather than changing it.

## Phase 4: Models

About 1.5 weeks. Exit: every model in `Sources/EmbedPDFModels/` has a C# counterpart with its
tests ported, running in the container. *Met 2026-11-26, see "Phase 4 notes".*

- [x] Port the 20 models into `EmbedPDF.Models`. Each implements `INotifyPropertyChanged`, exposes
  commands as `…Async` methods (no `ICommand`; consumers wrap them in their framework's), owns no background
  loop it didn't receive, and has a `RunAsync(CancellationToken)` that is the C# form of Swift's
  `task()`.
- [x] Keep each Swift model's documented contract (snapshot lag, error clearing, close behaviour,
  as in the `SearchModel` header) in its XML doc comments.
- [x] Tests run each model under a single-threaded `SynchronizationContext` (Review Focus 3).
- [x] Models whose Swift counterpart takes geometry from a SwiftUI view (`StageReaderModel`,
  `TiledPageModel`, `StagePointerForwarder`, `TextSelectionModel`) take plain inputs instead:
  viewport size and scroll offset as `double`s, pointer events as a `PointerInput` record. That is
  the whole contract a consumer's view implements, and the README documents it.

### Phase 4 notes (2026-11-26)

Task plan: `docs/superpowers/plans/2026-11-20-dotnet-parity/04-models.md`. Branch
`research/dotnet-parity-p4` from `research/dotnet-parity-p3e` at `b206fe923`, not pushed.

What exists. `platforms/dotnet/EmbedPDF.Models/` (package `EmbedPDF.Models`, assembly and namespace
`EmbedPDF.Models`) references `EmbedPDF.Viewer` and nothing else; `ImportTests` checks its
referenced assemblies. Every file of `Sources/EmbedPDFModels/` has a counterpart. The public models
are `ViewerModel`, `AnnotationModel`, `SearchModel`, `ToolModel`, `HistoryModel`, `OutlineModel`,
`AttachmentModel`, `ThumbnailModel`, `TextSelectionModel`, `StageReaderModel`, `TiledPageModel`
(with `PageTile`), `PdfReaderModel` (with `ReaderFeatures`, `ReaderPhase`, `ReaderSession`) and
`RenderRevisions`; `StageVisiblePage+Viewport.swift` is `StageVisiblePageExtensions`, and
`PointerInput` is new. Swift's internal helpers are internal here: `SerialCalls`,
`SnapshotRefresh`, `PageRenderTask`, `CapabilityObservation` (`observeCapability` and
`controlError`, the latter as `ModelErrors.Control`), `StagePointerForwarder`, and
`WeakObservation` for `CancelOnRelease` and its weakly held loop. `EmbedPDF.Models.Tests` runs
154 cases, 153 passing twice in a row and one skipped (the drag latency test, which runs with
`EMBEDPDF_LATENCY=1`: p95 0.16 ms per 120 Hz drag change on `linux-arm64`); a 20-run soak of the
suite passed. `EmbedPDF.Viewer.Tests` runs 1,113 cases, 1,109 passing and 4 skipped, as on `b206fe923`. A Release build of `EmbedPDF.sln` reports 0
warnings with the trim and AOT analyzers on.

Decisions, each with its reason:

- Notification is hand-written: an `ObservableModel` base with a `private protected` constructor
  and `Set`, `SetList` and `Raise`. A source generator package would have added a dependency for a
  few dozen setters.
- Each model has one home `SynchronizationContext`: the constructor's `context` argument, else
  `SynchronizationContext.Current`, else a private `SerialSynchronizationContext` that runs posted
  work one item at a time on the thread pool. Every async entry point starts with
  `await Home.SwitchTo()`, and every synchronous one runs inline on the context or posts itself, so
  a model plays Swift's `@MainActor` and every `PropertyChanged` is raised on its context. Tests run
  each model inside `SingleThreadContext.Run`, whose `Watch` fails the test on a notification
  raised off its thread (Review Focus 3), kernel-thread and plugin-stream causes included.
- `RunAsync(CancellationToken)` is `task()`: it returns, without throwing, when the token is
  cancelled or its streams end, and a later call supersedes an earlier one, as Swift's session
  counter does.
- Collections are `IReadOnlyList<T>`, `IReadOnlySet<T>` or `IReadOnlyDictionary` snapshots replaced
  as a whole with one notification. No model uses `INotifyCollectionChanged`: every Swift model
  replaces its arrays and dictionaries whole. A per-page lookup (`GetRevision`, `GetImage`,
  `GetHitCount`) reads a dictionary property whose change is announced. Lists whose records hold
  lists (`SearchHit`, `InteractionTool`, `OutlineItem`, `HistoryChangeLabel`) compare item by item
  (`Structural`), since C# records compare a list member by reference.
- The models reach a capability's viewer as Swift's do through `package let viewer`: the facade
  grants `InternalsVisibleTo("EmbedPDF.Models")` and gains `internal PdfViewer Viewer` on
  `SearchCapability`, `AnnotationCapability` and `InteractionCapability`, so `SearchModel(search)`
  keeps Swift's shape. The cost: `EmbedPDF.Models` must ship with the exact `EmbedPDF` version it
  was built against, which Phase 6 packs as an exact dependency range. Kept 2026-11-26 (coordinator
  decision); the Phase 6 checklist carries the `[x.y.z]` dependency.
- Models that observe from construction (`RenderRevisions`, `ThumbnailModel`,
  `TextSelectionModel`, and `StageReaderModel` through its `Revisions`) are `IDisposable`; their
  loop holds the model through a `WeakReference`, and a finalizer queues the cancel Swift's `deinit`
  makes. `AReleasedModelStopsObservingWhileTheDocumentStaysOpen` and each
  `ACancelledTaskReleasesTheModel` collect the model. Models never dispose a capability they were
  given. `PdfReaderModel` owns its viewer and is `IAsyncDisposable`, a close.
- Geometry inputs are plain values. `StageReaderModel` takes the viewport size and display scale,
  drag translation and velocity, pinch magnification and centre, and double-tap points as `double`
  pairs, and pointer events as `PointerInput(X, Y)` with `Kind` (default `Touch`) and `Modifiers`.
  Wave B's `PointerSample` does not fit: it carries the page hit the model looks up and a phase the
  method gives. Swift's models take no scroll offset, because the stage owns the camera and a drag
  pans it, so these take none either; a view whose framework scrolls calls
  `StageCapability.ScrollToAsync`. `platforms/dotnet/README.md`, "Binding the models to a UI", is
  the contract, and `ReadmeExampleTests` runs its example.
- Images are `PixelBuffer`: `ThumbnailModel.Images`, `TiledPageModel.Base` and `PageTile.Image`.
- Swift's `String(localized:)` texts are English literals ("Undo", "Redo", "change", the two
  history notices, `"{0} of {1}"` with `CultureInfo.CurrentCulture` grouping, plus `+` while
  searching). An app that words them otherwise builds its own from the raw properties.
- Test hooks (`ReadFinished`, `RenderStarted`, `HubCallStarting`, `ZoomStartReached`,
  `ZoomReadReturned`, `RectsReadReturned`, `PointerSampleDispatched`) are internal, visible to
  `EmbedPDF.Models.Tests`.

Deviations from Swift, each with its reason:

- Actions are verbs with `Async`: `searchSubmitted`, `nextButtonTapped`, `previousButtonTapped`,
  `clearButtonTapped` and `revealActiveHit` are `SubmitSearchAsync`, `NextHitAsync`,
  `PreviousHitAsync`, `ClearAsync` and `RevealActiveHitAsync`; `sizeChanged(_:displayScale:)` is
  `SizeChangedAsync(width, height, displayScale)`. `ToolModel.ButtonTappedAsync` is public, where
  Swift's is `package` for its picker, since a .NET app builds its own picker.
- Swift's deprecated `SearchModel.activeIndex` and `StageReaderModel.currentPageIndex` have no
  counterpart, so `theUInt32ActiveIndexFollowsActiveHitIndex` is not ported.
- `linkActivations` is `ObserveLinkActivations(CancellationToken)`, an `IAsyncEnumerable` that
  subscribes when called, as the facade's streams do; disposing the model ends every stream.
- `PdfReaderModel.OpenAsync` takes `OpenDocumentOptions` (its `Name` and `Password`) for bytes and
  `(Uri, name)` for a URL; RS0026 is suppressed on the pair. `Session.pageInfo(for:)` is
  `ReaderSession.GetPageInfo`.
- `AttachmentModel.importFiles(_:)` is `ImportFilesAsync(IEnumerable<string> paths)`; the media
  type comes from a small extension table and is `null` otherwise, because .NET has no system type
  registry. `fileOperationFailed` is `FileOperationFailed(Exception)` and ignores
  `OperationCanceledException`, as Swift ignores the system's user-cancelled error.
- Swift has no per-model tests for history, outline and attachments beyond
  `PluginIntegrationTests.swift`; `PanelModelTests` adds five.
- `ToolModel` yields once after setting `Submitting` and before the hub call, and the guarded
  actions of `HistoryModel`, `OutlineModel` and `AttachmentModel` do the same. Finding: a facade
  call can complete before it returns, when the Rust future is ready on its first poll, so without
  the yield an action could finish inside the call that started it and a second tap made in the
  same turn undid it (`ASecondTapWhileAnActivationIsInFlightIsIgnored` failed about one run in ten).
  Swift's hub call always hops off the main actor, which is what the yield restores.
- `StageReaderModel` numbers its page reads and applies one only if no read that started after it
  has landed. Swift's has no such guard. Here `RunAsync`'s first read, taken before the viewport was
  sized, could land after `SizeChangedAsync`'s own read and leave `VisiblePages` empty. A first cut
  that dropped a read whenever a newer one had started was wrong: it discarded the resize's read
  while a newer camera read was still out.
- Finding: a plugin event reaches a model through the facade's channel and a thread-pool hop, so it
  is not ordered with the completion of the call that caused it, as it is on Swift's main actor.
  The models' contracts hold, since they say what a snapshot lags. Three ported tests assumed
  Swift's order and now wait for the model to take the events: the text selection tests find their
  probe points before making the model, `RectsAreFinalAfterTheEndEvenWhenAnEventRefreshInterleaves`
  waits on the internal `TextSelectionModel.EventsFollowed`, and
  `AnActivationCompletesWhileStateRevisionsKeepArriving` counts churn rounds rather than waiting to
  see the churned tool in `Tools`. The search rerun tests also require the rerun's full hit count,
  because the rerun's empty start reads as complete with different hits; Swift's predicate has the
  same gap.

Wave E merge (2026-11-26). `research/dotnet-parity-p3e` at `b82ee27a0` (the wave E review fixes) is
merged in with no conflicts. After it, 26 Models tests that close a document or dispose a viewer under
live models, or release a model, run under `SingleThreadContext.RunWatched`, which fails on a task
exception nobody observed. It uses `CancellationTests.UnobservedWatch`'s approach in a
test-local `UnobservedWatch`; `TheUnobservedWatchSeesAnUnobservedFault` checks that the watch catches
one, and runs in a collection with parallelization off, because the event is process-wide and its
deliberate fault otherwise failed three watched tests running beside it. None fired otherwise.
`EmbedPDF.Models.Tests` then runs 155 cases, 154 passing four times in a row and one skipped;
`EmbedPDF.Viewer.Tests` runs 1,123, 1,119 passing and 4 skipped; the Release build of `EmbedPDF.sln`
reports 0 warnings.

Review fixes (2026-11-26), each with a test that failed first:

- A model recognised its home context by identity alone, and some UI dispatchers install a fresh
  context instance for each callback, so on the model's own thread a setter posted and read back
  the old value (`search.Text = "abc"` then `Text` was `""`). A context captured as the current one
  now records its thread, and a later context of the same type on that thread counts as home; a
  model handed that context by another model inherits the thread through a weak table. The private
  serial context and a context given from elsewhere keep the identity check.
  `FreshInstanceContextTests` runs models under a context that installs a fresh instance per
  callback (`a6fb77b56`).
- `PdfReaderModel` disposes a session's models, capabilities and scroll handler when the phase
  leaves it, for another open, a failure or a close, and disposes a session built for a superseded
  open at once (`6ea6e1a7d`).
- `CapabilityObservation.ObserveAsync` cancels its loops in a `finally`, so a throwing first read
  no longer leaves them running (`147a615ea`).
- `ReadmeExampleTests` checks that the README block is the test's `Example` method line for line,
  and runs it on a UI-like thread and with no context (`5e8506e19`). The README says a throwing
  `PropertyChanged` handler on the private serial context ends the process.

`EmbedPDF.Models.Tests` then runs 164 cases, 163 passing three times in a row and one skipped;
`EmbedPDF.Viewer.Tests` runs 1,123, 1,119 passing and 4 skipped; the Release build reports 0 warnings.

## Phase 5: Sample and consumer check

About 3 days. Exit: the sample restores from a local feed, opens a document, and publishes with
`PublishAot=true` to a binary that still opens it (Review Focus 4). *Met 2026-11-26, see "Phase 5 notes".*

- [x] `platforms/dotnet/samples/EmbedPDF.Sample.Console`: open, search, render a page to PNG,
  add an annotation, save, and drive one model (`SearchModel`) to show the binding contract.
- [x] `platforms/dotnet/dotnet-consumer.sh`, like `platforms/apple/macos-consumer.mjs`: builds
  the sample against packed packages rather than project references.

### Phase 5 notes (2026-11-26)

Task plan: `docs/superpowers/plans/2026-11-20-dotnet-parity/05-sample.md`. Branch
`research/dotnet-parity-p5` from `research/dotnet-parity-p4` at `d16ef2e6d`, not pushed.

The exit is met on both Linux RIDs. The NativeAOT build of the console sample, restored from a local
feed, opens `report.pdf` and passes every step on `linux-arm64` (native, dev container) and on
`linux-x64` (the container's x86_64 emulation with the x64 SDK 10.0.403, so `container-emulated`, not
native). The release workflow's new `consumer` job runs the same check natively on both Linux runners
and framework-dependent on `osx-arm64` and `win-x64`; run 45817553981 passed every row (see "CI" below).

What exists. `platforms/dotnet/samples/EmbedPDF.Sample.Console` (with `samples/Directory.Build.props`,
which keeps the repository's build settings out, as an outside app has none) references the four
managed packages and `Microsoft.Extensions.DependencyInjection` 10.0.0, and is not in `EmbedPDF.sln`.
Its steps are `di` (`AddEmbedPdf` resolves the `PdfViewer`), `open`, `search`, `render` (page 1 to
`page-1.png`, encoded by `Png.cs` with `ZLibStream` and CRC-32), `annotate` (a `SquareAnnotationDraft`),
`save` (save, reopen, count the squares), `model` (`SearchModel` over the search capability with no
`SynchronizationContext`) and `script` (`ScriptEngineCheck` over `JintScriptEngineFactory`, which goes
through the kernel's foreign-trait engine). `platforms/dotnet/dotnet-consumer.sh` packs or takes the
five packages, restores the sample in a scratch copy with `packageSourceMapping` that serves
`EmbedPDF*` from the local feed only, scrubs `EPDF_NATIVE_DIR`, `PDF_RUNTIME_LIB_DIR`,
`LD_LIBRARY_PATH` and the `DYLD_*` paths, runs the framework-dependent build and the NativeAOT binary,
checks both PNGs with `packaging/png_check.py`, prints sizes and deletes the AOT output.
`packaging/libraries.py pack-libraries` packs `EmbedPDF.Models`, `EmbedPDF.Extensions.DependencyInjection`
and `EmbedPDF.Scripting.Jint` at the facade's version; `EmbedPDF.Models.csproj` rewrites its
`EmbedPDF` dependency to `[x.y.z]` after NuGet's `_GetProjectReferenceVersions`, and the pack fails
unless the nuspec says exactly that. The other two keep NuGet's minimum range.

Commands, from the repository root in the dev container:

```sh
bash platforms/dotnet/dotnet-consumer.sh            # linux-arm64: builds and packs everything, then FDD and AOT
EPDF_DOTNET=/workspaces/dotnet-x64/dotnet \
EPDF_AOT_ARGS="-p:CppCompilerAndLinker=x86_64-linux-gnu-gcc -p:ObjCopyName=x86_64-linux-gnu-objcopy" \
  bash platforms/dotnet/dotnet-consumer.sh --rid linux-x64 --packages <feed with linux-arm64 and linux-x64 slices>
```

The emulated `linux-x64` publish runs the x64 ILCompiler under emulation and links with the
`crossbuild-essential-amd64` gcc, which is what `EPDF_AOT_ARGS` names. The x64 slice was cross-built
with `build-viewer-host.sh linux-x64` and packed with the `linux-arm64` slice into one runtime package.

Sample output, identical in the four runs apart from the property change count (17 or 26):

```text
PASS di: PdfViewer from AddEmbedPdf, runtime linux-arm64
PASS open: report.pdf, 14 pages
PASS search: "the" 985 hits (Complete)
PASS render: page 1 612x792 -> <work>/out-aot/page-1.png
PASS annotate: square on page 1 (ObjectNumber)
PASS save: 1017209 bytes -> <work>/out-aot/annotated.pdf, reopened with 1 square(s)
PASS model: SearchModel "2 of 985", 17 property changes
PASS script: Jint alert "sample 42"
```

Sizes, in bytes:

| | `linux-arm64` (native) | `linux-x64` (emulated) |
|---|---|---|
| NativeAOT executable | 18,572,016 | 18,096,808 |
| `.dbg` symbols beside it | 47,024,824 | 37,441,072 |
| AOT publish directory | 106,370,152 | 100,835,330 |
| framework-dependent output | 51,148,005 | 96,447,216 |

The framework-dependent output is a portable build, so it holds every RID's native files the
runtime package carries: one RID in the `linux-arm64` run, two in the `linux-x64` one. A consumer
work directory measured 912M before the AOT output was deleted and 548M after (`du -sh`); the rest is
the private NuGet cache, and the script removes the whole directory unless `--keep` or `--work`.

Warnings. The framework-dependent build reports 0. The NativeAOT publish reports six, all from Jint,
the same on both RIDs: IL2104 and IL3053 for the assembly, and IL2026 from `Jint.Options.Apply`, its
`<>c__DisplayClass77_0.<Apply>b__0`, `DefaultTypeConverter.BuildDelegate` and
`DefaultObjectConverter.ConvertSystemTextJsonValue`. The last is new against the wave B notes: Jint
4.18.0's `JsonNode` conversion calls `JsonSerializer.Deserialize<double>` without a type info. The
script step still passes in the native binary. The sample lists IL2026, IL2104 and IL3053 in
`WarningsNotAsErrors`, and `dotnet-consumer.sh` fails on any other code, on one of those three that
names an `EmbedPDF` assembly, and on one that does not come from Jint. No `EmbedPDF` assembly produced
a trim or AOT warning.

Tests. `packaging` runs 41 (11 new: `LibraryPackTests` 6, `PngCheckTests` 5), `dotnet-consumer.test.sh`
runs 21 checks, 11 of them through a stand-in `dotnet` that drives the script's verdicts (a failed or
missing step, an unlisted warning code, an allowed code naming `EmbedPDF`). `generate-bindings.test.sh`,
`dotnet-engine-hash.test.sh` and `render-guide.test.sh` pass; `check-dotnet-engine.sh` still reads
`current 0.0.0-engine.822686307291c580`; `EmbedPDF.Models.Tests` runs 155, 154 passing and one skipped;
the Release build of `EmbedPDF.sln` reports 0 warnings.

Deviations, each with its reason:

- The library packing is `packaging/libraries.py`, not a new action in `package.py`. `package.py` is
  an input of `dotnet-engine-hash.sh` (Phase 1 notes), so any edit moves the engine version and
  forces a new `EmbedPDF.Runtime` for a change that never touches the native library.
  `libraries.py` imports `package.py`'s helpers and leaves it byte for byte unchanged.
- Jint stays in the AOT sample rather than behind a flag or a second project. It runs in the native
  binary, and its six warnings are held to an allow list the script enforces. The cost: the sample's
  AOT publish is not warning-free, and a Jint upgrade that adds an unlisted warning fails the check
  until someone reviews it.
- `osx-arm64` and `win-x64` run framework-dependent only in the workflow. NativeAOT there needs the
  platform linker setup on the hosted image, which nothing in this phase could try. The script takes
  `--rid` with either and would publish if asked.
  Follow-up (coordinator decision 2026-11-26): not now; add NativeAOT rows for `osx-arm64` and
  `win-x64` in a later phase.
- The Windows row relies on Git Bash converting paths for `dotnet` and `python`, and on `cygpath -w`
  for the feed path in `nuget.config`. It passed on its first run.

CI (2026-11-26). `research/dotnet-parity-p4` at `720091979` (the Phase 4 review fixes) is merged in at
`3eed4b326` with no conflicts; after it the consumer passed again on `linux-arm64` and
`EmbedPDF.Models.Tests` ran 164, 163 passing and one skipped. Four runs of `release-dotnet.yml` on
`research/dotnet-parity-p5`:

| Run | Head | Result | Fix that followed |
|---|---|---|---|
| 45798933504 | `3eed4b326` | `runtime-pack` failed in "Validate linux-arm64": NU1101 for `Microsoft.Extensions.Logging.Abstractions` | `6e7c75dec` |
| 45804857099 | `6e7c75dec` | `runtime-pack` failed in "Viewer tests": `MetadataTests.DroppingTheStreamUnsubscribes` saw 1 live stream | `8e2c61a24` |
| 45811979676 | `8e2c61a24` | `consumer` failed on `linux-arm64` and `linux-x64` (AOT `model` step: no active hit) and `osx-arm64` (`sample_args[@]: unbound variable`) | `9aa53103c` |
| 45817553981 | `9aa53103c` | every job passed: 5 packages, 5 runtime slices, `runtime-pack`, 4 `runtime validate`, 4 `consumer`, `runtime-record` | |

The fixes:

- `6e7c75dec`: the facade has depended on `Microsoft.Extensions.Logging.Abstractions` since Phase 2,
  and `package.py validate-runtime` restores from its local feed alone, so the Phase 1 validation had
  been broken since then without a run to show it. The two validate steps set
  `RestoreAdditionalProjectSources` to nuget.org in the environment, which MSBuild reads as a
  property. The real fix belongs in `package.py`, an engine hash input, so it waits for the next
  engine change; a local `validate-runtime` needs the same variable until then.
- `8e2c61a24`: a facade race. When a cancelled token's callback won `Subscription.End`, the
  enumerator's `DisposeAsync` returned while that `End` was still unsubscribing, so an `await foreach`
  could finish with its plugin stream still registered. Disposal now awaits the end's completion;
  the token callback and the kernel's end still never wait.
  `RevisionHubTests.DisposingTheEnumeratorWaitsForAnEndAlreadyUnderWay` failed before the change.
  `EmbedPDF.Viewer.Tests` then ran 1,124, 1,120 passing twice in a row and 4 skipped.
- `9aa53103c`: on the runners the sample stepped `SearchModel` while its search still ran
  (`"2 of 848+"`), so the AOT runs had no active hit. The sample now follows the model with `RunAsync`
  and waits for `SearchStatus.Complete`, as a view would. And macOS's bash 3.2 treats an empty array
  as unbound under `set -u`, so the script expands its optional arguments through `${a[@]+...}`.

Sizes from run 45817553981, in bytes: the NativeAOT executable is 18,705,768 on `linux-arm64` and
18,230,472 on `linux-x64` (native runners), the AOT publish directories 106,798,387 and 101,185,301.
Framework-dependent outputs are about 232,000,000 on every row, since the runtime package there
carries all five RIDs. Both Linux AOT publishes report the same six Jint warnings. One AOT
`linux-arm64` run summarised `"1 of 985"` where the others read `"2 of 985"`: the model's documented
snapshot lag, not a failure.

## Phase 6: Release and documentation

About 3 days. Exit: one tagged release a consumer can add with one `PackageReference` and a feed
entry. *The release path is built and dry-run 2026-11-26, see "Phase 6 notes"; the release itself waits for the user.*

- [x] Extend `.github/workflows/release-dotnet.yml` for D7, consuming the `runtime-<sha>` slices
  that `2026-11-19-release-workflows.md` already builds.
- [x] `platforms/dotnet/README.md` rewritten in the shape of `platforms/apple/README.md`:
  packages, where the native library comes from, the plugin table, local engine mode, and when to
  choose `EmbedPDF.Engine` instead (D2).
- [x] `DisabledPluginTests` fails when the options gain a plugin without a table row, as on Apple.
- [x] Pack `EmbedPDF.Models` with an exact-version dependency on `EmbedPDF` (`[x.y.z]`, the version
  it was built with). The models read capabilities' `internal Viewer` through
  `InternalsVisibleTo("EmbedPDF.Models")` (Phase 4 notes, kept 2026-11-26), so any other `EmbedPDF`
  version may lack the members they call. A pack check fails when the range is not exact.
  *Done in Phase 5:* `packaging/libraries.py pack-libraries`, see "Phase 5 notes".

### Phase 6 notes (2026-11-26)

Task plan: `docs/superpowers/plans/2026-11-20-dotnet-parity/06-release.md`. Branch
`research/dotnet-parity-p6` from `research/dotnet-parity-p5` at `647e0e846`, pushed to `origin` for
CI; nothing published.

Merge. `origin/research/multi-platform-rust-core-impl-plugins` (74 commits past `e7aa7f4f5`) is merged
in at `92fa839c9` with no conflicts. It moved `rust-toolchain.toml` to Rust 1.100.0,
`platforms/dotnet/global.json` to SDK 10.0.404 (`rollForward: disable`), `Cargo.lock`, and the
engine package's workflow jobs to SDK 10.0.404, `actions/checkout@v7`, `setup-node@v7` with Node 26
and `upload-artifact@v7`. Two things the merge left broken are fixed here: `package.py` still demanded
`Rust 1.98.0` in `check_toolchain` and `stage-runtime`, and the viewer jobs still installed SDK
10.0.403, which `global.json` now refuses. `package.py` reads both versions from `global.json` and
`rust-toolchain.toml` now (`ToolchainTests`), and the viewer jobs use SDK 10.0.404 (osx-x64's
SHA-512 from Microsoft's release metadata) and the same action versions as the engine jobs. After
the merge the host was rebuilt with Rust 1.100.0 and the bindings regenerated; `EmbedPDF.Viewer.Tests`
ran 1,124 (1,120 passing, 4 skipped) and `EmbedPDF.Models.Tests` 164 (163 passing, 1 skipped).

Re-pin. The merge and the `package.py` fix moved the engine to `0.0.0-engine.dc140b58fa617042`
(`eb2718c6c`, from `822686307291c580`). `check-dotnet-engine.sh` reads `current` at the branch tip.

What exists:

- `package.py smoke-runtime` writes a `nuget.config` beside its consumer: `EmbedPDF` and `EmbedPDF.*`
  from the local feed only, everything else from nuget.org (`consumer_nuget_config`,
  `ConsumerConfigTests`). The `RestoreAdditionalProjectSources` workaround is gone from both validate
  steps, and a local `validate-runtime` passes without it (`6e22ddf29`).
- `packaging/release-viewer.sh <dir> <version> <sha> [--dry-run]` checks the five packages (one
  `EmbedPDF.Runtime.0.0.0-engine.<16 hex>`, the four managed ones at `0.0.0-dotnet.<n>`, `EmbedPDF.Models`
  on exactly `[<version>]`, `EmbedPDF` on the runtime it ships with, every nuspec naming
  `https://github.com/$GH_REPO`), writes `.sha256` files, pushes Runtime, EmbedPDF, Models, DI and Jint
  in that order to `https://nuget.pkg.github.com/<owner>/index.json` with `--skip-duplicate`, and makes
  the prerelease `<version>` through `scripts/release/github-release.sh`. A dry run prints every
  command with the key as `***`. `release-viewer.test.sh` runs 24 checks against stand-in `dotnet`,
  `gh` and `git`.
- `release-dotnet.yml` takes `dotnet_version` (empty packs `0.0.0-local` as before), sets
  `RepositoryUrl` for every pack so the feed links the packages to the repository, and on a manual
  run with a version runs `viewer-release-preview`, which calls the release script with `--dry-run`
  and a read-only token.
- `release-train.yml` takes an optional `dotnet_version`. Preflight checks its shape, its notes
  (`docs/releases/<dotnet_version>.md`, a warning on a dry run) and whether the release is out;
  summarize lists the five packages; `dotnet-viewer-release` (needs `approve`, `contents: write` and
  `packages: write`) runs the release script, which reads `DRY_RUN`. `check-release-gate.py` counts a
  `dotnet nuget push`, a `release-viewer.sh` line without `--dry-run`, and `packages: write` as
  publishing; four new gate tests pin it, and the gate passes with `dotnet-viewer-release` among the
  publishers.
- `platforms/dotnet/README.md` in the shape of `platforms/apple/README.md`: packages, where the native
  library comes from and local engine mode, the plugin table, threading, disposal and arguments,
  `EmbedPDF.Engine` or the viewer (D2), binding the models and the console sample (kept), releases and
  the feed, known gaps. `DisabledPluginTests.TheReadmePluginTableHasARowPerPlugin` parses the table and
  fails when the options gain a plugin without a row or a row names an accessor `PdfViewer` lacks.
- `docs/releases/README.md` says how the viewer packages release and that reading the feed needs a
  `read:packages` token.

Deviations, each with its reason:

- `package.py` changed, so the runtime version moved. The Phase 5 notes deferred the restore fix to the
  next engine change; the merge was one, so both landed before one re-pin.
- `dotnet_version` is optional on the train, where `swift_version` and `viewer_version` are required:
  existing dispatches keep working, and a train without it releases no viewer packages. Making it
  required is a one-line change once the first viewer release is wanted.
- The release tags `<dotnet_version>` with the packages as assets, beside the feed push, so a release
  is also a downloadable record.
- `check-dotnet-engine.sh` asked for a `dotnet-engine-<hash>` GitHub release that no workflow makes,
  so its exit 2 could never fire. It now lists `EmbedPDF.Runtime` versions on the release repository
  owner's NuGet feed with `gh api` (`orgs/<owner>` or `users/<owner>` `packages/nuget/EmbedPDF.Runtime/versions`):
  listed is 2, absent or "Package not found" is 1, any other failure 69 (`712c17ec7`).
  `dotnet-engine-hash.test.sh` drives the four verdicts through a stand-in `gh`.
- `RepositoryUrl` comes from the job environment, which MSBuild reads as a property, so no project
  file (and no engine hash input) changed. A local pack has no repository URL, and the release script
  refuses it.

CI. Three runs of `release-dotnet.yml`, each green on the first try, on `research/dotnet-parity-p6`, dispatched with
`dotnet_version=0.0.0-dotnet.0`:

| Run | Head | Result |
|---|---|---|
| 45825062921 | `eb2718c6c` | every job passed: 4 engine `package` rows, 5 runtime slices, `runtime-pack`, 4 `runtime validate`, 4 `consumer`, `runtime-record`, `viewer-release-preview` |
| 45831861397 | `b194bbb47` | every job passed again, now with the README and its plugin-table test, and the records and pin committed |
| 45840321520 | `712c17ec7` | all 21 jobs passed again, with the three test ports and the feed check in `check-dotnet-engine.sh` |

All five RIDs are `VALIDATED` at `0.0.0-engine.dc140b58fa617042`; those records replace the Phase 1
ones under `platforms/dotnet/validation/2026-11-26-phase-6-runtime-<rid>/` (`b194bbb47`). The NativeAOT
sample ran on `linux-arm64` (executable 18,705,768 bytes, publish directory 106,866,467) and `linux-x64`
(18,230,472 and 101,204,461) with the same six Jint warnings; `osx-arm64` and `win-x64` ran
framework-dependent. The preview printed the five pushes to `https://nuget.pkg.github.com/<owner>/index.json`
in dependency order, `git tag 0.0.0-dotnet.0 <sha>`, `git push origin refs/tags/0.0.0-dotnet.0` and
`gh release create 0.0.0-dotnet.0` with eleven assets, and warned that `docs/releases/0.0.0-dotnet.0.md`
is missing, which a real run would stop on.

Gap ports (2026-11-26, after the first review). `APICoverageTests.swift`, `EditingWorkflowsTests.swift`
and `SecurityTests.swift` are ported as `ApiCoverageTests` (5), `EditingWorkflowsTests` (7) and
`SecurityTests` (4) in `EmbedPDF.Viewer.Tests` (`c1d00ede2`, `aa92b7b05`, `a3757d1a1`). All 16 passed on
their first run against the facade as it stood, so no bug turned up. `EmbedPDF.Viewer.Tests` then runs
1,141, 1,137 passing and 4 skipped; `EmbedPDF.Models.Tests` 164, 163 passing and 1 skipped.

Final review fixes and the second merge (2026-11-26):

- The train's `dotnet:` call granted no permissions, and `viewer-release-preview` asks for
  `packages: read`. A called job may ask for no more than its caller job grants, whether or not it
  runs, so every `release-train.yml` dispatch would have failed at startup. The call now grants
  `contents: read` and `packages: read`, and `check-release-gate.py` flags a called job asking for a
  permission its caller doesn't grant (`d07e127b3`, with a gate test).
- `origin/research/multi-platform-rust-core-impl-plugins` at `d8de5874d` (137 commits, Android
  parity among them) is merged in at `3b80a57a5`. `Cargo.lock` takes that branch and adds back the C#
  generator wrapper's packages with no existing version changed. `scripts/check-api-coverage.py` is
  one script for `--platform swift`, `kotlin` and `csharp`; Swift and Kotlin output is byte-identical
  to that branch's script, and `platforms/apple/check-swift-api-coverage.py` is its wrapper. The
  Swift check still reports the branch's own inventory drift (`settings_revision`, `remeasure_page`);
  the C# manifest already matched the new inventory. The host was rebuilt, the bindings regenerated,
  and the engine re-pinned to `0.0.0-engine.2e7fdf92c64b1092` (`42734fceb`). A stale link in the
  shared target pointed the host's RUNPATH into another checkout; `cargo clean -p epdf-viewer-host`
  fixed it, and is the thing to try when every test fails to load `libembedpdf.so`.
- `release-viewer.sh` runs `github-release.sh check` before it pushes, so a draft, partial release or
  stray tag stops the run before anything reaches the feed (`6b3c947c8`; its test checks a stray tag pushes
  nothing). `viewer-release-preview` runs on `ubuntu-26.04`, the train's image. The README says which
  calls cancel mid-call (every call whose Rust export takes a cancel token, about 140 sites) and that
  blocking on a stream enumerator's `DisposeAsync` from a kernel callback can hang. The facade's
  package description no longer says "early skeleton", and the Jint package's no longer names a call
  depth limit.
- `release-fork-slices.yml` installs `libatomic1` and `libstdc++6`: the plugins branch's Node 26 bump
  left `setup-node` failing with exit 127 in `debian:trixie`, so the train failed at its fork slices
  (run 45860472875, cancelled) before reaching the .NET path (`979fd3998`).

| Run | Workflow | Head | Result |
|---|---|---|---|
| 45853424872 | `release-dotnet.yml` | `42734fceb` | all 21 jobs passed after one re-run of `consumer (osx-arm64)`, whose first attempt failed the sample's `script` step with "The script sandbox has been disposed": the Jint boot ran past its time budget on a cold runner and poisoned the realm. The re-run passed; nothing changed |
| 45860837730 | `release-train.yml`, `dry_run=true`, `swift_version=0.0.0-swift.10`, `viewer_version=0.0.0-preview.5`, `dotnet_version=0.0.0-dotnet.0` | `979fd3998` | The train started and ran every build and test job, `summarize` and `approve`. `release dotnet viewer` passed: it checked the five packages, found release `0.0.0-dotnet.0` absent, and printed the five pushes in order, the tag, the release and the missing-notes warning. `release dotnet` (the `EmbedPDF.Engine` release) failed: the win-x64 `.sha256` asset began with a backslash, because `sha256sum` escapes a line whose file name has one and `GITHUB_WORKSPACE` on Windows does. Fixed in `8cf87c121` by hashing from stdin |
| 45870444350 | `release-dotnet.yml` | `8cf87c121` | every engine `.sha256` asset passed `sha256sum -c`, win-x64 included; `runtime slice (win-x64)` failed fetching the PDFium archive (HTTP 504), and the run was cancelled once the pin moved again |
| 45875026242 | `release-dotnet.yml` | `136f5d8bd` | `runtime-pack` failed in `RedactionTests.ALabelAndTheSettingsRoundTrip`: the pending count read 1 just after `ApplyAsync`. Fixed in `c582ac24f` |
| 45878960809 | `release-dotnet.yml` | `c582ac24f` | all 21 jobs passed; its records are the current ones |
| 45884429305 | `release-train.yml`, `dry_run=true`, `dotnet_version=0.0.0-dotnet.0` | `b0c22eb24` | every `release-dotnet.yml` job passed inside the train, but `release-android.yml (build) / build` failed its Kotlin unit tests twice with different tests (`PdfViewerViewModelTest.clearingWhileTheStartIsInFlightDestroysTheViewer`, then `OutlinePanelTest.addingABookmarkThroughTheFormShowsItsRow`), so `summarize`, `approve` and the release jobs were skipped. The dry-run `release dotnet viewer` path last ran in 45860837730 |

Android release merge (2026-11-26). The plugins branch gained the Android release work (`17bd82627`).
A pre-resolved merge, `research/dotnet-parity-p6-sync` at `8fa938fdf` (p6 at `979fd3998` merged with
`9790877d2`, the same tree as `17bd82627`), was reviewed and merged in at `6efbe0477`, then `17bd82627`
itself at `793584b89` with no change. Its resolutions keep both sides: the train's .NET and Android
inputs, jobs and summaries; `publish-staged.sh` beside `dotnet nuget push` and `release-viewer.sh` in
the gate; both release-notes paragraphs; `Cargo.lock` with `smallvec` 1.16.2 for the generator. One
slip is fixed in `b1d76569b`: two gate tests shared the name `test_packages_write_counts_as_writing`, so
the called-job case was silently replaced. The engine moved to `0.0.0-engine.5b0a47900f002e9a`
(`136f5d8bd`).

The records from the last green `release-dotnet.yml` run replace the earlier ones under `platforms/dotnet/validation/2026-11-26-phase-6-runtime-<rid>/`
(`91397ade5`).

Swift test files and their C# counterparts (Definition of done):

| Swift test file | C# counterpart, or the reason there is none |
|---|---|
| `ActionsTests`, `AnnotationInfoTests`, `AnnotationPatchDecodingTests`, `AnnotationTests`, `AttachmentTests`, `CommandsTests`, `DisabledPluginTests`, `DocumentAPITests`, `FeedbackTests`, `FetcherExportTests`, `FormTests`, `HistoryTests`, `I18nTests`, `IntegerSurfaceTests`, `InteractionTests`, `LinkTests`, `MeasurementTests`, `MetadataTests`, `OutlineTests`, `PageEditTests`, `PageGeometryTests`, `PageRenderResolutionTests`, `PointerLatencyTests`, `RedactionTests`, `RenderTests`, `RevisionHubTests`, `SearchTests`, `SelectionTests`, `ShellTests`, `SignatureTests`, `SignatureTransportTests`, `StageTests`, `StampTests`, `StreamRuleTests`, `TaskEntryPointTests`, `TypedOverloadTests`, `ViewManagerTests`, `ViewerConfigBuilderTests`, `ViewerTests` | The file of the same name in `EmbedPDF.Viewer.Tests` (`PDFErrorTests` is `PdfErrorTests`) |
| `AnnotationColorTests` | `AnnotationVocabularyTests` |
| `AnnotationDraftTests`, `AnnotationKindDraftTests` | `AnnotationDraftJsonTests`, `AnnotationEngineDraftTests` |
| `PluginSettingsTests` | Split by plugin into `SearchTests`, `RenderTests`, `SelectionTests`, `CommandsTests`, `StampTests`, `SignatureTests` |
| `RenderSnapshotTests`, `RenderViewportTests` | `RenderTests` |
| `CoreGraphicsBridgesTests` | `PixelBufferTests`; the point, size and rect bridges are CoreGraphics types with no .NET counterpart (D6) |
| `JavaScriptCoreEngineTests` | `JintScriptEngineTests` (D5) |
| `KeychainSignerTests` | `X509CertificateSignerTests` (D5) |
| `RevisionTests` | `RevisionHubTests` (`RepeatedCancellationLeavesTheSurvivingObserverLive`) and `StreamRuleTests`: a C# stream subscribes when the method returns, so no mutation lands before the first read |
| `WireValueTests` | `EnumMirrorTests` and `AnnotationVocabularyTests.TheVocabularyIsTheEnginesSpelling`; Swift's `Codable` round trips have no counterpart because the C# wire strings are internal |
| `AccessorNameTests` | Not ported: it pins Swift's deprecated `in:` accessors, which C# never shipped (wave A notes) |
| `AliasRemovalTests` | Not ported: it pins Swift's deprecated `PDFDocument` and `Annotation` aliases, which C# never had |
| `ProductsTests` | No counterpart: SwiftPM products have none. `ImportTests` and PublicApiAnalyzers keep the generated layer `internal` instead (D3) |
| `BaseSuite`, `LoopbackServer` | Test support: `[assembly: CollectionBehavior]` and `Loopback.cs` |
| `APICoverageTests` | `ApiCoverageTests`, its five cases ported behaviourally; `scripts/check-api-coverage.py --platform csharp` is the static half, finding each member in the built assembly |
| `EditingWorkflowsTests` | `EditingWorkflowsTests`, all seven workflows |
| `SecurityTests` | `SecurityTests`, all four cases |
| `AnnotationModelTests`, `PDFReaderModelTests`, `PluginIntegrationTests`, `RenderRevisionsTests`, `SearchModelTests`, `StagePointerForwarderTests`, `StageReaderDragLagTests`, `StageReaderErrorTests`, `StageVisiblePageGeometryTests`, `TextSelectionModelTests`, `ThumbnailModelTests`, `TiledPageModelTests`, `ToolModelTests`, `ViewerModelTests` | The file of the same name in `EmbedPDF.Models.Tests` |
| `ImportTests`, `PageRenderTaskTests`, `SerialCallsTests`, `SnapshotRefreshTests` | `InfrastructureTests` |
| `PluginControlTestSupport` | Test support: `SingleThreadContext.cs`, `Observed.cs` |

### Follow-up 2026-11-26: the duplicate exports and the markMatches cancel

Task plan: `docs/superpowers/plans/2026-11-26-ffi-duplicate-and-cancel.md`. Branch
`research/dotnet-parity-p7` from `research/dotnet-parity-p6` at `b4bb2b710`. The user approved both
halves as a Rust change after wave D's "Exclusions that need a decision" and wave E's "Finding", so
the Global Constraints rule that `ffi.rs` grows only where Swift already calls a member does not
apply to these ten.

The cancel. `RedactionHandle.mark_matches` passed the plugin no signal, so the abort that
`call_promise_cancellable` posts reached a promise that never checked it. It now hangs an
`AbortSignal` on the plugin options and aborts it from the cancel token (`bf0d8af2a`), as the
attachment and annotation bundle exports already did. The search was never holding the kernel
thread: it yields between engine slices, and the signal stops it there. A cancel 300 ms into a
whole-document mark of `"e"` on `report.pdf` now ends 300 ms later
(`RedactionTests.MarkMatchesStopsWhenCancelledMidCall`). A cancel before the search found a hit
resolves with no marks, which is the plugin's own behaviour in Rust and TypeScript.

The exports. Annotation `duplicate`, `can_duplicate`, `is_movable`, `when_remeasurable`,
`stage_remeasure`, `selection_duplicate` and `selection_can_duplicate` (`5480d0afa`), and form
`duplicate` and `duplicate_widgets` (`ebf3fa761`), with typed records and cancel tokens. C# wraps
them as `AnnotationCapability.DuplicateAsync`, `CanDuplicateAsync`, `IsMovableAsync`,
`WhenRemeasurableAsync`, `StageRemeasureAsync`, `AnnotationSelectionCapability.DuplicateAsync` and
`CanDuplicateAsync`, and `FormCapability.DuplicateAsync` and `DuplicateWidgetsAsync`.
`get_shared` stays unexported: it hands other plugins the record the store holds, and a foreign
caller can only get the copy `GetAsync` answers. Its exclusion is now `internal-resource`.

Review fixes (`6eee050fe`, `6b4df850d`, `c0c2cfb0a`, `26e9fe056`, `c2945607f`). The signal
plumbing is two shared helpers in `crates/epdf-ffi/src/cancel.rs`: `signalled` (redaction
`mark_matches`, form `duplicate` and `duplicate_widgets`) and `signalled_cancellable` (every
annotation export that had its own `cancellable_operation`). Each duplicate export is cancelled in a
Rust test while the engine holds its copy; the form case fails if the export stops passing its
signal. A NaN or infinite duplicate offset is `invalid-input` in Rust and, before the call, in C#,
since JSON would carry it as `null` and drop it. `MarkMatchesStopsWhenCancelledMidCall` counts the
hits first and accepts either outcome of the cancel within 5 s: `OperationCanceledException`, or
fewer marks than hits. A cancel before the first hit returns an empty list on every platform, and
the Swift and Kotlin handoffs say so.

Merge and release path. The final Phase 6 (`2ad6655ff`) is merged in at `93e3210d8` with no
conflicts; the README's `MarkMatchesAsync` caveat went with it (`6fc93d452`). The console sample
warms Jint in a throwaway realm with a 30 s budget before its script check (`8114b0224`), because a
cold osx-arm64 runner once spent longer than the 50 ms script budget booting the first realm. The
engine is pinned at `0.0.0-engine.06ced45f4c89b5ad` (`2b298c636`). CI run 45895350480 of
`release-dotnet.yml` at `8114b0224` passed all 21 jobs, dry run with `dotnet_version=0.0.0-dotnet.0`;
its records replace the Phase 6 ones under `platforms/dotnet/validation/2026-11-26-phase-6-runtime-<rid>/`
(`704f99a71`), all five RIDs `VALIDATED`.

Tests. Rust `ffi` suites: redaction 13, annotation 32, form 15; full suites with `--features uniffi`:
`epdf-ffi` 70, redaction 33, annotation 73, form 83, all passing. C#: `AnnotationDuplicateTests`
(12), four `FormTests` cases and the redaction mid-call case are new; `EmbedPDF.Viewer.Tests` runs
1,159, 1,155 passing and 4 skipped, twice in a row; `EmbedPDF.Models.Tests` 164, 163 passing and 1
skipped. `packaging` passes its 45, `scripts/test_check_api_coverage.py` its 14, the release gate
tests their 25, and `dotnet-consumer.sh` passes FDD and NativeAOT on `linux-arm64`. The unified
coverage script reads C# 880 members (739 ported, 0 pending, 141 exclusions) and Kotlin 864 (728
ported, 0 pending, 136 exclusions); Swift still reports the 18 drift errors its handoff clears 11 of.

Handoffs for the other packages: `docs/superpowers/plans/2026-11-26-swift-ffi-duplicate-and-cancel.md`
and `docs/superpowers/plans/2026-11-26-kotlin-ffi-duplicate-and-cancel.md`.

## Definition of done

`csharp-api-coverage.json` has no `pending` rows and the coverage check passes. Every Swift test
file under `EmbedPDFTests` and `EmbedPDFModelsTests` has a C# counterpart or a recorded reason it
doesn't apply. PublicApiAnalyzers passes. The console sample runs from packed packages and as a
NativeAOT binary. Validation records exist for `linux-x64`, `linux-arm64` and `osx-arm64`
(`osx-arm64` from the release workflow or a later manual run, since Phase 1 could not use a macOS
host);
`osx-x64` and `win-x64` stay `UNVALIDATED` until someone runs them on an Intel Mac and a Windows
host. One release is published once the user approves it.

There is no UI to compare, so Swift's `EmbedPDFUITests` has no counterpart by design.

### Definition of done, checked 2026-11-26

| Item | State | Evidence |
|---|---|---|
| `csharp-api-coverage.json` has no `pending` rows and the check passes | Met | `scripts/check-api-coverage.py --platform csharp`: 880 members, 730 ported, 0 pending, 150 exclusions; CI's `runtime-pack` runs it |
| Every Swift test file has a C# counterpart or a recorded reason | Met, 0 gaps | The table in "Phase 6 notes". `ApiCoverageTests`, `EditingWorkflowsTests` and `SecurityTests` were ported after the first review (16 cases, all passing on first run; no bug found) |
| PublicApiAnalyzers passes | Met | `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` in `EmbedPDF.Viewer`, `EmbedPDF.Models`, `EmbedPDF.Extensions.DependencyInjection` and `EmbedPDF.Scripting.Jint`; the Release build of `EmbedPDF.sln` at `b194bbb47` reports 0 warnings and 0 errors |
| The console sample runs from packed packages and as a NativeAOT binary | Met | `dotnet-consumer.sh` in the dev container at `2c449137a` (all eight steps, FDD and AOT, `linux-arm64`); CI run 45825062921 `consumer` rows: AOT on `linux-arm64` and `linux-x64`, FDD on `osx-arm64` and `win-x64` |
| Validation records for `linux-x64`, `linux-arm64` and `osx-arm64` | Met, and `osx-x64` and `win-x64` too | `platforms/dotnet/validation/2026-11-26-phase-6-runtime-<rid>/`, all five `VALIDATED` on native runners in run 45825062921 |
| One release published once the user approves it | Not done, by design | The path is built and dry-run: run 45825062921's `viewer-release-preview`. Publishing waits for the user: write `docs/releases/0.0.0-dotnet.<n>.md`, then dispatch `release-train.yml` with `dotnet_version` and approve |

Total: about 8 weeks of focused work with waves run one at a time, nearer 6 if Phase 3 waves A
and B and Phase 4 behind them overlap in separate worktrees. The estimate assumes Phase 0 finds a
working generator. A fork (D1) adds about 3 days up front plus a bump per UniFFI upgrade; each C#
name collision costs about half a day in the generator config or the owning `ffi.rs`.

## Phase 0 results (2026-11-20)

Outcome: works, with a fork. Generated C# for all 25 components of `epdf-viewer-host` compiles
under `net10.0` with `TreatWarningsAsErrors` and `IsAotCompatible`, and four xUnit tests pass on
`linux-arm64` in the dev container: open `report.pdf` and read 14 pages, receive
`ViewerDelegate.StateChanged` callbacks from Rust in revision order, receive a
`DocumentEvent.Opened` through `DocumentEventListener`, and drive a `CommandRunner` both ways (C#
answers `Derive`/`IconAccent`, and a C# `HostException.Failed` thrown from the callback reaches
Rust and disables the command). Branch `spike/dotnet-generator`, not pushed.

### Generator

The newest upstream release is `v0.12.0+v0.32.0` (tag at `fc32e9b`, 2026-11-09), which targets
this repository's uniffi. As released it does not work here, for two reasons, both with open
upstream pull requests:

1. It does not compile in this workspace. The generator declares `toml = ">=0.8, <0.10"`;
   `uniffi_bindgen` 0.32.0 declares `>=0.9, <2` and resolves to `toml` 1.1.6 here. `toml` values
   cross between the two crates (`BindingGenerator::new_config`, `interface::rename`), so the
   build fails with E0053 and E0308. Upstream pull request 196 (`370010a`) widens the generator's
   range to `>=0.9, <2`. Pinning `toml` 0.9 in our `Cargo.lock` would also work until the next
   `cargo update`, so we did not take that route.
2. Its output does not compile. An object type from another crate used inside an optional,
   sequence or record goes through a `using` alias to the other file's converter, and each file
   has its own `BigEndianStream`, so 10 sites fail with CS1503 (`epdf_plugin_outline.cs` reading
   `PdfActionTree`, `epdf_viewer_host.cs` reading interaction and signature objects). Upstream
   pull request 186 (`d24df88`) replaces the alias with a forwarding converter. It conflicts with
   upstream main only in `CHANGELOG.md`.

The fork is branch `epdf/v0.12.0-v0.32.0` at `91345690ef91dd2dbc9fa0738967c8f49478f056`: the
tag, plus `370010a` (PR 196), plus `d24df88` cherry-picked as `9134569` (PR 186, our side of
the `CHANGELOG.md` conflict kept). The spike took it from a container-only path
(`file:///workspaces/uniffi-bindgen-cs`). *Changed 2026-11-20 in Phase 1:* the user chose to host
the fork, and `crates/uniffi-bindgen-cs` (package `epdf-uniffi-bindgen-cs`, binary
`uniffi-bindgen-cs`, the package name differs because Cargo otherwise finds two
`uniffi-bindgen-cs` packages and `-p` is ambiguous) now takes it from the hosted repository's
HTTPS git URL at that same revision (`crates/uniffi-bindgen-cs/Cargo.toml`). Any machine that can
fetch from that host resolves the workspace. Cost: every UniFFI bump in this repo still needs a
matching fork bump until an upstream release carries both pull requests. The wrapper adds 48 packages to
`Cargo.lock` (askama 0.13, clap 3 and their trees) and changes no existing version.

Other open upstream pull requests worth tracking: 190 (variant fields that collide with sibling
variant records, which we hit, see below), 191 and 192 (doc comment warnings and escaping),
193 (callback marking for Mono and IL2CPP AOT), 182 (object trait implementations), 194 (uniffi
0.32.2).

### Generation

```sh
cargo rustc -p epdf-viewer-host --release --locked --crate-type cdylib     # 3m24s warm
cargo build -p epdf-uniffi-bindgen-cs
target/debug/uniffi-bindgen-cs --no-format --library target/release/libepdf_viewer_host.so \
  --config platforms/dotnet/spike/uniffi-cs.toml --out-dir target/csharp-spike/generated
```

PDFium came from `packages/engine/runtime/build/libpdfium/linux-arm64`, already fetched. No
features or flags beyond the cdylib crate type. Output: 25 files, 154,833 lines, about 1.5 s.
`--no-format` skips CSharpier, which is not installed; without it the generator warns once per
file and writes the same output.

The release cdylib `libepdf_viewer_host.so` is 37,937,696 bytes, 29,367,528 after `strip`. On
Linux it links PDFium dynamically: `NEEDED libembedpdf.so` (11,376,920 bytes) with an absolute
`RUNPATH` into the build tree. Phase 1's runtime package has to ship both files per RID and set
the runpath to `$ORIGIN` or resolve `libembedpdf.so` itself; the spike worked only because the
build-tree path exists in the container. This also bears on D2: if `epdf-capi` links the same
way, the two packages carry the same PDFium file rather than two copies inside two libraries.

### Access modifier (D3)

`access_modifier` exists and defaults to `internal`. With the default, or with
`access_modifier = "internal"` set, every generated type is `internal` (295 classes, 92
interfaces, 425 records, 98 enums). With `"public"` the exported types become public and the
FFI plumbing (`RustBuffer`, converters, `_UniFFILib`) stays internal. The spike's tests implement
the internal callback interfaces from the same assembly, so callbacks work with internal
bindings. Because generated types are internal, tests that touch them have to live in the facade
assembly or use `InternalsVisibleTo`.

### Name collisions

Each is fixed in `platforms/dotnet/spike/uniffi-cs.toml` with the generator's `rename` table,
keyed by crate then Rust path. No Rust change was needed.

| Rust | Collision | C# name |
|---|---|---|
| `ViewerHost.destroy` | the generated `Destroy()` that frees the handle (CS0111) | `DestroyViewer` (Kotlin renames the same member in `crates/epdf-viewer-host/uniffi.toml`) |
| `RenderViewDemandHandle.dispose` | `IDisposable.Dispose()` (CS0111) | `DisposeDemand` |
| `ScriptEngine.dispose` (foreign trait) | `IDisposable.Dispose()` on `ScriptEngineImpl` (CS0111) | `DisposeEngine` |
| `ActionSource::{Widget, Link}.{annotation, page}`, `ActionSource::Annotation.page`, `ActionTreeSource::Annotation.page`, `ActionTrigger::Activate.{annotation, page}`, `ActionTrigger::Annotation.page` | a variant field named like a sibling variant's nested record (CS8866, 9 sites) | `AnnotationRef`, `PageRef` |

Renaming a foreign trait's method is safe: the vtable is positional. Kotlin's `message`
collision does not occur, since no error variant has a `message` field. Things that compile but
the facade should not expose as they are: method and property names equal to their type
(`CommandRunner.IconAccent` returning `IconAccent?`), error variant fields as lowercase public
fields (`ViewerException.Kernel.@detail`), exception messages that read `@name=...` because the
template quotes the escaped identifier, and records holding objects are `IDisposable`
(`CommandDefinition`).

### Warnings and AOT

No `<NoWarn>` was needed. Every generated file starts with an `<auto-generated>` header, so
analyzers skip it, and the generator wraps its own CS8625 sites in `#pragma warning disable 8625`.
The build reports 0 warnings and 0 errors with `TreatWarningsAsErrors` and `IsAotCompatible`
(trim, AOT and single-file analyzers).

To see what the header hides, the files were rebuilt with it stripped: no IL2xxx or IL3xxx
warning, and 75 CA2022 warnings (3 per file, inexact `Stream.Read` in `BigEndianStream`). A probe
calling `Type.GetType(string)` raised IL2057 in the same build, so the analyzers were live.
P/Invokes are `[LibraryImport]` with `CallConvCdecl` on `net8.0` and later; callbacks register
vtables through `Marshal.GetFunctionPointerForDelegate` on static delegate fields. Nothing was
published with `PublishAot`; Phase 5 still owns that.

### Async

"Where .NET is today" says the host exports no UniFFI `async` functions. That is wrong: the
grep looked for `export(async`, but `#[uniffi::export]` on an `impl` exports each `pub async fn`
as async, and there are 697 of them under `crates/epdf-viewer-host/src`, `crates/plugins/*/src`
and `crates/epdf-ffi/src` (`ViewerHost.start`, `open_document`, every capability accessor). The
generator supports them: each becomes a `Task`-returning method over `_UniFFIAsync`, with no
`CancellationToken`, and every spike test goes through them. Cancellation stays on the explicit
`CancelToken` arguments, which is what D4's `token.Register(() => cancelToken.Cancel())` row
already assumes.

### Test

```sh
cd platforms/dotnet/spike/EmbedPDF.Spike
dotnet build          # 0 warnings, 0 errors
dotnet test --no-build
# Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 389 ms
```

The project compiles `target/csharp-spike/generated/*.cs` into the test assembly and copies
`target/release/libepdf_viewer_host.so` beside it.

### Jint (D5)

Jint 4.18.0, the newest stable on NuGet (published 2026-11-13), BSD-2-Clause, with a `net10.0`
dependency group whose only dependency is Acornima 1.9.1 (BSD-3-Clause). The previous release,
4.17.0, was published 2026-10-07. Pin 4.18.0 in Wave B, or 4.17.0 if packages less than two
weeks old are out of bounds then.

### Decisions after Phase 0

D1 changed: C# generation works, but only from a fork (tag plus pull requests 196 and 186)
until upstream releases both. The fork is hosted (Phase 1, user decision 2026-11-20). The
cost named in D1 applies now rather than as a fallback. D3 is confirmed as written: internal
works, callbacks included. D2, D4, D5 (with Jint 4.18.0 and Acornima 1.9.1 recorded above), D6,
D7 and D8 are confirmed 2026-11-20 (execute instruction).
