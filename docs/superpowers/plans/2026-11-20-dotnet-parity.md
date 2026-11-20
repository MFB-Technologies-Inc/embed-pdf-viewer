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

**D1. Binding generator.** Recommendation: generate C# with UniFFI's community C# generator
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

**D2. Relationship with `EmbedPDF.Engine`.** Recommendation: keep it as a separate, narrow package
for headless reading, and build the viewer as new packages beside it with no dependency between
them. Its admission control and validation records are worth keeping for server use, where a
viewer kernel is dead weight. Cost: two native libraries each carry their own PDFium. An app that
loads both pays the memory twice, so the README says to pick one. Revisit after the first viewer
release: if nobody needs the narrow package, deprecate it then.

**D3. Packages.** Three NuGet packages, plus the small DI extension package from D4, all
`net10.0`:

| Package | Apple counterpart | Contents | Depends on |
|---|---|---|---|
| `EmbedPDF.Runtime` | `EmbedPDFBindings` binary target | `runtimes/<rid>/native/` viewer-host library for each RID | nothing |
| `EmbedPDF` | `EmbedPDF` | facade, with the generated C# compiled in as `internal` | `EmbedPDF.Runtime`, `Microsoft.Extensions.Logging.Abstractions` |
| `EmbedPDF.Models` | `EmbedPDFModels` | UI-neutral models | `EmbedPDF` |

`EmbedPDFUI` has no counterpart (D6). `EmbedPDF.Runtime` is versioned `0.0.0-engine.<hash>`, the
hash computed like `apple-engine-<hash>`, so facade and model work never rebuilds Rust.

Generating the bindings as `internal` gets by construction what Apple could not: Apple's
`EmbedPDF` re-exports 539 of 710 generated types because Swift's generator has no access-level
option (`platforms/apple/README.md`, "Products"). If the C# generator's `access_modifier` setting
works, nothing generated is public, and each public type is a hand-written record or a
deliberate wrapper. Cost: every generated record the API exposes needs a hand-written twin and a
mapping, which is more code than a typealias. Phase 0 confirms the setting; if it doesn't exist,
fall back to Apple's arrangement and document the generated namespace as unsupported.

**D4. C# shape.** Swift idioms map one to one, so the port doesn't relitigate them:

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
`OpenAsync`. Capability accessors keep Swift's member names (`viewer.Search(document)`), so the
coverage manifest can share them across Swift, Kotlin and C#. Configuration takes a
`PdfViewerOptions` class with init properties plus a fluent builder, and an optional
`services.AddEmbedPdf(options => …)` extension in a separate
`EmbedPDF.Extensions.DependencyInjection` assembly so the core package carries no DI dependency.

**D5. Platform services.**

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

**D6. No UI layer (decided 2026-11-20).** Apple has SwiftUI and Android has Compose: one
first-party framework each, so a UI package there is the obvious next layer. .NET has no such
framework. WPF and WinUI 3 are Windows only, .NET MAUI has no Linux, and Avalonia is third
party. Picking one would serve only that framework's users, and there is no Windows host to
develop or test the Windows ones on. So the C# package stops at the models: `EmbedPDF.Models` is
the parity line, and an app binds it to its own views. Cost: every .NET consumer writes the page
view, tiling, gesture handling and panels that `EmbedPDFUI` gives Swift apps for free, and the
models must document their inputs well enough to make that practical (Phase 4). A UI package can
be added later as a separate plan without changing the models.

**D7. Publishing.** `.github/workflows/release-dotnet.yml` already exists for `EmbedPDF.Engine`.
Recommendation: extend it to the four new packages and publish to GitHub Packages on
the runtime repository, where release assets already go. Cost: GitHub Packages
NuGet feeds need a token even to read, which every consumer's `nuget.config` must carry. nuget.org
avoids that but needs a reserved prefix and signing we don't have. Publishing itself needs the
user's separate approval.

**D8. API stability.** `Microsoft.CodeAnalysis.PublicApiAnalyzers` with `PublicAPI.Shipped.txt`
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

- [ ] Try the C# generator against `epdf-viewer-host` at `uniffi =0.32.0`. Record the generator
  version, whether it needed a fork (D1), and any Rust names that collide with C# keywords or
  members, as Kotlin's `message` and `close` did.
- [ ] Confirm the generator's `access_modifier = "internal"` setting (D3) and that callbacks work
  when the generated types are internal.
- [ ] Check the generated P/Invoke style against the trim and AOT analyzers; record warnings.
- [ ] Pin the Jint version and record its license in this file.
- [ ] Record D1 to D8 as confirmed or changed at the top of this file.

## Phase 1: Runtime package

About 4 days. Exit: `EmbedPDF.Runtime` packs for all five RIDs, and a consumer project restoring
it from a local feed loads the library and passes the Phase 0 test on `linux-arm64` and
`osx-arm64`, with validation records.

- [ ] `crates/uniffi-bindgen-cs` wrapper (D1) and a `platforms/dotnet/generate-bindings.sh` that
  writes generated C# under `platforms/dotnet/EmbedPDF/Generated/`, git-ignored.
- [ ] Build `epdf-viewer-host` as a cdylib per RID. Extend `platforms/dotnet/packaging/package.py`
  and its schema to a second package, keeping the checksum-verified input rule.
- [ ] `platforms/dotnet/dotnet-engine-hash.sh` and `check-dotnet-engine.sh`, mirroring
  `platforms/apple/apple-engine-hash.sh` and `check-apple-engine.sh`.
- [ ] A `NativeLibrary.SetDllImportResolver` that maps the generated library name to the package
  layout and turns a missing RID into a typed error (Review Focus 5).
- [ ] Record the packaged library size per RID in the README. The number belongs in the record,
  not a guess.

## Phase 2: Facade core

About 1 week. Exit: `PdfViewer` opens, retries, renames and closes documents from bytes, a stream,
a file path and a URL, with tests passing in the container.

Port, from `platforms/apple/EmbedPDF/Sources/EmbedPDF/`: `PDFViewer.swift`, `PDFError.swift`,
`ViewerConfigBuilder.swift`, `StateRevisionHub.swift`, `EventRelay.swift`, `DocumentFetch.swift`,
`DocumentPermission.swift`, `PageGeometry.swift`, `PageRenderResolution.swift`, `JSONValue.swift`
(as `System.Text.Json.Nodes.JsonNode` where it fits), `WireInteger.swift`, `Registration.swift`,
`ContentInclusion.swift`, `Experimental.swift` (as `[Experimental("EPDF001")]`) and
`Signposts.swift` (as `ActivitySource`).

- [ ] `HttpClientDocumentFetcher` matching `URLSessionDocumentFetcher`'s cancellation and error
  mapping.
- [ ] API coverage check for C#. If the Android plan's `scripts/check-api-coverage.py` exists,
  add `--platform csharp`; otherwise this phase creates it from
  `platforms/apple/check-swift-api-coverage.py`. Manifest at
  `platforms/dotnet/csharp-api-coverage.json`, matching public members by reflection over the
  built assembly. Unported members are `pending` with the phase that owns them, so the check
  passes from day one and shrinks to zero.
- [ ] PublicApiAnalyzers (D8) on `EmbedPDF`.
- [ ] `EmbedPDF.Extensions.DependencyInjection` with `AddEmbedPdf` (D4).
- [ ] Tests: port `ViewerTests`, `DocumentAPITests`, `PDFErrorTests`, `RevisionHubTests`,
  `StreamRuleTests`, `IntegerSurfaceTests`, `FetcherExportTests`, `ViewerConfigBuilderTests` and
  `DisabledPluginTests`, plus the two Review Focus tests owned here.

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
NativeAOT binary. Validation records exist for `linux-x64`, `linux-arm64` and `osx-arm64`;
`osx-x64` and `win-x64` stay `UNVALIDATED` until someone runs them on an Intel Mac and a Windows
host. One release is published once the user approves it.

There is no UI to compare, so Swift's `EmbedPDFUITests` has no counterpart by design.

Total: about 8 weeks of focused work with waves run one at a time, nearer 6 if Phase 3 waves A
and B and Phase 4 behind them overlap in separate worktrees. The estimate assumes Phase 0 finds a
working generator. A fork (D1) adds about 3 days up front plus a bump per UniFFI upgrade; each C#
name collision costs about half a day in the generator config or the owning `ffi.rs`.
