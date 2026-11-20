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

About 4 weeks across waves. Exit: `csharp-api-coverage.json` has no `pending` rows.

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

## Phase 4: Models

About 1.5 weeks. Exit: every model in `Sources/EmbedPDFModels/` has a C# counterpart with its
tests ported, running in the container.

- [ ] Port the 20 models into `EmbedPDF.Models`. Each implements `INotifyPropertyChanged`, exposes
  commands as `…Async` methods (no `ICommand`; consumers wrap them in their framework's), owns no background
  loop it didn't receive, and has a `RunAsync(CancellationToken)` that is the C# form of Swift's
  `task()`.
- [ ] Keep each Swift model's documented contract (snapshot lag, error clearing, close behaviour,
  as in the `SearchModel` header) in its XML doc comments.
- [ ] Tests run each model under a single-threaded `SynchronizationContext` (Review Focus 3).
- [ ] Models whose Swift counterpart takes geometry from a SwiftUI view (`StageReaderModel`,
  `TiledPageModel`, `StagePointerForwarder`, `TextSelectionModel`) take plain inputs instead:
  viewport size and scroll offset as `double`s, pointer events as a `PointerInput` record. That is
  the whole contract a consumer's view implements, and the README documents it.

## Phase 5: Sample and consumer check

About 3 days. Exit: the sample restores from a local feed, opens a document, and publishes with
`PublishAot=true` to a binary that still opens it (Review Focus 4).

- [ ] `platforms/dotnet/samples/EmbedPDF.Sample.Console`: open, search, render a page to PNG,
  add an annotation, save, and drive one model (`SearchModel`) to show the binding contract.
- [ ] `platforms/dotnet/dotnet-consumer.sh`, like `platforms/apple/macos-consumer.mjs`: builds
  the sample against packed packages rather than project references.

## Phase 6: Release and documentation

About 3 days. Exit: one tagged release a consumer can add with one `PackageReference` and a feed
entry.

- [ ] Extend `.github/workflows/release-dotnet.yml` for D7, consuming the `runtime-<sha>` slices
  that `2026-11-19-release-workflows.md` already builds.
- [ ] `platforms/dotnet/README.md` rewritten in the shape of `platforms/apple/README.md`:
  packages, where the native library comes from, the plugin table, local engine mode, and when to
  choose `EmbedPDF.Engine` instead (D2).
- [ ] `DisabledPluginTests` fails when the options gain a plugin without a table row, as on Apple.

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
