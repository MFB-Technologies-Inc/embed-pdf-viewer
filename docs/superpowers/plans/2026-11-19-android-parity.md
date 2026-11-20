# Android Package Parity Plan

> **For agentic workers:** this is an umbrella plan. Each phase below gets its own task-level plan
> (`docs/superpowers/plans/2026-11-19-android-parity/NN-<phase>.md`, written with
> superpowers:writing-plans) before anyone executes it. Execute those with
> superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring `platforms/android` from an engine smoke test to a full Android library with the
same reach as `platforms/apple/EmbedPDF`: every viewer plugin as a typed Kotlin capability, models a
View or Compose app can drive, a Compose UI layer, platform services, a demo app and a published
artifact.

**Architecture:** Same three layers as Apple, over the same Rust. A generated bindings module wraps
`epdf-viewer-host` (all plugins), a hand-written Kotlin facade (`com.embedpdf`) wraps it in
`suspend` functions, `Flow`s and sealed types, a models module projects state into `StateFlow`
with no Compose dependency, and a Compose module draws it. Behaviour stays in Rust; Kotlin renames
and reshapes.

**Tech Stack:** Rust (pinned by `rust-toolchain.toml`), `uniffi =0.32.0` with the repo's
`crates/uniffi-bindgen`, NDK r29 (`29.0.14206865`), Kotlin 2.2.21, JDK 21 toolchain targeting JVM
17, AGP and Gradle through a checked-in wrapper, `kotlinx-coroutines` 1.10.2, JNA 5.14.0,
`kotlinx-serialization`, Jetpack Compose (BOM), JUnit 4 + `kotlinx-coroutines-test`, AndroidX
Test.

**Spec:** this document, argued from the Apple package as it stands at `53a39771b`:
`platforms/apple/README.md`, `docs/superpowers/plans/2026-10-01-swift-package.md`,
`docs/superpowers/plans/2026-10-06-swift-plugins/00-umbrella.md`,
`docs/superpowers/plans/2026-11-10-swift-api-idioms.md` and
`docs/superpowers/plans/2026-11-18-swift-api-gaps.md`. Those plans already paid for the API design
decisions. This one ports the outcome, not the path.

## Where Android is today

The AAR packages `epdf-uniffi` (`platforms/android/build-aar.sh:65`), the engine-only crate with no
viewer kernel and no plugins. Consumers get raw generated `uniffi.epdf_uniffi.PdfEngine`, nothing
hand-written. The library module declares one dependency, JNA
(`platforms/android/embedpdf/library/build.gradle.kts:67`). Eight instrumented tests in
`library/src/androidTest/kotlin/com/embedpdf/SmokeTest.kt` prove the `.so` loads on a 16 KB page
emulator. The build calls a system `gradle` (`platforms/android/build-aar.sh:113`, `:115`); there
is no wrapper.

Two pieces already prove the full host works from Kotlin. `platforms/android/test-command-context.sh`
generates Kotlin for every component of `epdf-viewer-host` and runs command callbacks on the JVM.
`platforms/android/viewer/HttpSignatureTransport.kt` is a working `SignatureTransport`, tested by
`platforms/android/tests/SignatureTest.kt`. Neither ships in the AAR.

Apple, for comparison: 23 plugin capabilities, 20 models in `EmbedPDFModels`, 19 SwiftUI files in
`EmbedPDFUI`, 94 test files, a demo app, a published engine binary pinned by hash, and an API
coverage check (`platforms/apple/check-swift-api-coverage.py` over
`platforms/apple/swift-api-coverage.json`) that fails when a Rust capability member has no Swift
counterpart.

## Global Constraints

- Work lands on `research/multi-platform-rust-core-impl-plugins` through feature branches, one
  worktree per phase or wave. Merges into the plugins branch and every push wait for the user.
- No TypeScript or npm package changes, so no changesets.
- No change to a plugin's Rust behaviour. A phase may add `ffi.rs` members only where Apple already
  calls the same member through Swift; if Kotlin needs something Swift doesn't, stop and ask.
- Generated Kotlin is a build output. It is never committed and never hand-edited; fixes go into
  `crates/uniffi-bindgen/src/kotlin.rs` or the Rust `ffi.rs`.
- Every build and test runs in the dev container (amd64, since the NDK has no Linux arm64 host).
  Nothing runs on the Mac host. Docker on the Mac has no nested virtualization, so there is no
  emulator: tests that need Android classes run under Robolectric on the JVM, and the 16 KB device
  run is replaced by the static ELF and `zipalign` checks. `platforms/android/README.md` records
  that limitation with the date.
- `minSdk = 24`, `compileSdk = 36`, ABIs `arm64-v8a` and `x86_64`. The 16 KB ELF and `zipalign`
  checks in `build-aar.sh` stay and extend to every `.so`.
- Package prose and comments name no outside product, company or person.
- Commit messages follow `feat(android): …` with no co-author or session trailer.
- Every public rename keeps a `@Deprecated(replaceWith = ReplaceWith(...))` shim for one tagged
  release once the first Android release is tagged. Before that, rename freely.

## Decisions

These are the calls an implementer can't make alone. The user asked for the plan to be executed
on 2026-11-20 without changing them, so each recommendation below stands.

**D1. Modules.** Four Gradle modules under `platforms/android/embedpdf/`, published as four
artifacts under group `com.embedpdf`:

| Module | Artifact | Apple counterpart | Depends on |
|---|---|---|---|
| `bindings` | `embedpdf-bindings` | `EmbedPDFBindings` binary target | JNA, coroutines |
| `embedpdf` | `embedpdf` | `EmbedPDF` | `bindings` (as `api`) |
| `models` | `embedpdf-models` | `EmbedPDFModels` | `embedpdf`, `androidx.lifecycle` (no Compose) |
| `compose` | `embedpdf-compose` | `EmbedPDFUI` | `models`, Compose |

`bindings` holds the `.so` files and generated Kotlin and is versioned by engine hash, the way
`apple-engine-<hash>` is. Facade, model and UI work then never rebuilds Rust. The cost is the same
one Apple accepted on 2026-11-10: `embedpdf` must expose `bindings` as `api` for its typealiases to
compile in consumers, so the generated `uniffi.*` types are visible. We document them as
unsupported and pin only `com.embedpdf.*` with the API dump (D5).

**D2. Kotlin shape.** Swift idioms map one to one, so the port doesn't relitigate them:

| Swift | Kotlin |
|---|---|
| `async throws(PDFError)` | `suspend` that throws `PdfException` |
| `bridgedCancellable` (`PDFError.swift:171-180`) | `suspendCancellableCoroutine` plus `invokeOnCancellation { token.cancel() }` |
| `AsyncStream` per subscriber (`observeStateRevisions()`) | cold `Flow` per collector, via `callbackFlow` over the same hub |
| `PDFError.Code` with `unknown(String)` | `sealed interface PdfException.Code` with `data class Unknown(val raw: String)` |
| closed string sets (`AnnotationPosition`, tool ids) | `sealed interface` for open sets, `enum class` for closed ones |
| `Int` at the surface, conversion in the wrapper | `Int`/`Long` at the surface; `UInt`/`ULong` never public |
| `Codable` drafts and patches | `@Serializable` data classes, `kotlinx-serialization` |
| `@MainActor @Observable` model | class exposing `StateFlow`, collected on `Dispatchers.Main.immediate` |
| `CGImage`/`CoreGraphicsBridges` | `Bitmap`/`ImageBitmap` bridges, `RectF`/`PointF` |
| `Data`, `URL` | `ByteArray`, `android.net.Uri` and `java.net.URL` overloads |

Names follow Kotlin style: `PdfViewer`, `PdfException`, `SearchCapability`. Accessors keep Swift's
verbs (`viewer.search(document)`), so the coverage manifest can share member names.

**D3. Actions JavaScript.** Android has no system JavaScriptCore. Recommendation:
`androidx.javascriptengine` (`JavaScriptSandbox`), behind `ScriptEngine`, with a runtime
`isSupported` check that leaves JavaScript disabled on devices without a capable WebView.
Cost: availability depends on the installed WebView, and the sandbox is async and out of process,
so each script call pays IPC. The alternative, bundling QuickJS, adds a second native library and
its own 16 KB and CVE upkeep. Phase 0 confirms the library's `minSdk` and the WebView floor.

**D4. Publishing.** Recommendation: a Maven repository in GitHub Packages on
`MFB-Technologies-Inc/embed-pdf-runtime`, `embedpdf-bindings` versioned `0.0.0-engine.<hash>` and
the other three by package release. Cost: GitHub Packages requires a token even for reads, which
every consumer's Gradle setup must carry. Maven Central avoids that but needs a namespace and
signing setup we don't have.

**D5. API stability.** `org.jetbrains.kotlinx.binary-compatibility-validator` dumps
`com.embedpdf.*` into `api/*.api` per module. A change to the public surface fails `apiCheck`
until the dump is updated in the same commit.

## Review Focus

- Coroutine cancellation mid-call: cancelling a scope during `open(url)`, `search` or a render
  cancels the Rust token, closes a loading tab, and never leaks a callback that resumes a dead
  continuation. Phase 2 pins it on `open(url)` against a stalling loopback server.
- Configuration change and process death: a rotated `Activity` keeps its viewer through a
  `ViewModel`, and a restored process with no viewer shows an empty state rather than crashing on a
  stale handle. Phase 5 pins it with a Robolectric `ActivityScenario.recreate()` test.
- Callbacks from Rust threads: a foreign trait (`PlatformFeedback`, `CommandHandler`,
  `StampLibraryStore`) invoked off the main thread touches no `View` directly. Phase 3 pins it by
  invoking each adapter from a background thread in a Robolectric test, where touching a
  `View` throws `CalledFromWrongThreadException`.
- R8 in a consumer release build: `consumer-rules.pro` keeps JNA, every generated `uniffi.*`
  class and every callback interface, so a minified app loads. Phase 6 pins it with a minified demo
  build whose R8 output still contains every class JNA loads by name.
- Large documents and memory: rendering tiles of a 500-page document with fast flings doesn't
  exhaust the heap; bitmaps are pooled and released when tiles leave the viewport. Phase 5 pins it
  with a test that scrolls through every page and asserts the pool never holds more bitmaps than
  its budget.

---

## Phase 0: Toolchain and decisions

About 2 days. Exit: `./gradlew :library:assembleRelease` works in the dev container.

- [ ] Add a Gradle wrapper and `gradle/libs.versions.toml` under `platforms/android/embedpdf`.
  `build-aar.sh` calls `./gradlew`.
- [ ] Add Android SDK command-line tools, `platforms;android-36` and `build-tools` to
  `.devcontainer/Dockerfile` on amd64, SHA-256 pinned like the NDK layer, and a row in
  `.devcontainer/README.md`.
- [ ] Record D1 to D5 as confirmed or changed at the top of this file.
- [ ] Check `androidx.javascriptengine`'s `minSdk` and record it under D3.

## Phase 1: Full engine artifact

About 4 days. Exit: an `embedpdf-bindings` AAR built from `epdf-viewer-host` passes the ELF and
`zipalign` checks, and a JVM test opens a document through the same generated Kotlin against the
Linux host library.

- [ ] Build `epdf-viewer-host` instead of `epdf-uniffi` in `build-aar.sh`, for both ABIs, with the
  builtins link argument and `__arm_` check unchanged. Generate Kotlin with the repo's
  `uniffi-bindgen`, which already applies `fix_external_error_buffers`.
- [ ] Split the current `library` module into `bindings` (D1). The `epdf-uniffi` smoke test moves
  to a separate `smoke` test module so its assertions stay identical to
  `crates/epdf-uniffi/tests/native_surface.rs`.
- [ ] Extend `consumer-rules.pro` to keep every generated component package and every callback
  interface.
- [ ] Add `platforms/android/android-engine-hash.sh` and `check-android-engine.sh`, mirroring
  `platforms/apple/apple-engine-hash.sh` and `check-apple-engine.sh`, over the same inputs plus
  `build-aar.sh`.
- [ ] Record the packaged `.so` sizes per ABI in the README. Today's `epdf-uniffi` slice is 16 MB
  per ABI; the full host will be larger and the number belongs in the record, not a guess.

## Phase 2: Facade core

About 1 week. Exit: `PdfViewer` opens, retries, renames and closes documents from bytes, a `Uri`
and a URL; JVM unit tests run against the Linux host `.so` in the dev container.

Port, from `platforms/apple/EmbedPDF/Sources/EmbedPDF/`: `PDFViewer.swift`, `PDFError.swift`,
`ViewerConfigBuilder.swift`, `StateRevisionHub.swift`, `EventRelay.swift`, `DocumentFetch.swift`,
`DocumentPermission.swift`, `PageGeometry.swift`, `PageRenderResolution.swift`, `JSONValue.swift`,
`WireInteger.swift`, `Registration.swift`, `Exports.swift`, `ContentInclusion.swift`,
`Experimental.swift` and `Signposts.swift` (as `androidx.tracing` sections).

- [ ] `HttpDocumentFetcher` on `HttpURLConnection`, no new HTTP dependency, matching
  `URLSessionDocumentFetcher`'s cancellation and error mapping. `ContentResolverDocumentFetcher`
  for `content://` URIs.
- [ ] Generalize `platforms/apple/check-swift-api-coverage.py` into
  `scripts/check-api-coverage.py --platform swift|kotlin`, with a Kotlin matcher and
  `platforms/android/kotlin-api-coverage.json`. Members not yet ported are listed as `pending`
  with the phase that owns them, so the check passes from day one and shrinks to zero.
- [ ] Wire D5's `apiCheck` into `build-aar.sh`.
- [ ] Tests: port `ViewerTests`, `DocumentAPITests`, `PDFErrorTests`, `RevisionHubTests`,
  `StreamRuleTests`, `IntegerSurfaceTests`, `FetcherExportTests`, `ViewerConfigBuilderTests`,
  `DisabledPluginTests` and `ProductsTests` (as a check that only the four artifacts publish).

## Phase 3: Plugin capabilities

About 4 weeks across waves. Exit: `kotlin-api-coverage.json` has no `pending` rows.

Each plugin follows the same recipe: typealiases for the generated records, a `*Capability` class
wrapping the handle, a `PdfViewer` accessor that throws `Code.Unsupported` when the plugin is left
out of the config, a coverage manifest update, and the port of the matching Swift test file.
Waves follow the Swift umbrella's dependency order
(`docs/superpowers/plans/2026-10-06-swift-plugins/00-umbrella.md`, "Waves"), so a capability never
lands before one it requires.

| Wave | Plugins | Android platform service it brings |
|---|---|---|
| A | metadata, search, render, history, outline, attachment, page-edit, view-manager, shell | render: `Bitmap` and `ImageBitmap` bridges (port of `CoreGraphicsBridges.swift`) |
| B | interaction + feedback, i18n, actions | `HapticFeedback` via `View.performHapticFeedback`; `AssetLocaleLoader` for locale JSON in `assets/`; `JavaScriptSandboxEngineFactory` (D3) |
| C | stage, selection, link, commands | `CommandHandler` adapter |
| D | annotation (with every typed draft and patch from `AnnotationDraft*.swift`), form | `@Serializable` drafts; SAF pickers for image and file annotations |
| E | redaction, measurement, stamp, signature | `FileStampLibraryStore` on `Context.filesDir`; `AndroidKeyStoreSigner`; `TrustAnchors` from assets; `HttpSignatureTransport` moved from `platforms/android/viewer/` into `embedpdf` |

Wave D is the largest single piece: `Annotation*.swift` is about 6,200 lines of the 17,866 in
`Sources/`. Its own plan should split drafts from the capability.

Foreign callbacks arrive on Rust threads. Each service adapter documents its thread, and none
touches a `View` without posting to the main looper (Review Focus 3).

## Phase 4: Models

About 1.5 weeks. Exit: every model in `Sources/EmbedPDFModels/` has a Kotlin counterpart with its
tests ported, runnable on the JVM with `kotlinx-coroutines-test`.

- [ ] Port the 20 models. Each exposes `StateFlow` properties and `suspend` or fire-and-forget
  actions, owns no `CoroutineScope` it didn't receive, and has a `run()` suspend function that is
  the Kotlin form of Swift's `task()`. A thin `PdfViewerViewModel` in `models` holds a viewer
  across configuration changes and calls `destroy()` in `onCleared()`.
- [ ] Keep each Swift model's documented contract (snapshot lag, error clearing, close behaviour,
  as in the `SearchModel` header). The KDoc carries the same rules.

## Phase 5: Compose UI

About 3 weeks. Exit: `PdfReader` composable renders, scrolls, zooms, searches, selects text and
annotates the fixture corpus in Robolectric Compose UI tests.

- [ ] Port the 19 files in `Sources/EmbedPDFUI/`: `StageReader` (with the page overlay slot from
  `2026-11-18-swift-api-gaps.md` gap 5 from the start), `TiledPageImage` with a bitmap pool,
  `ThumbnailStrip` on `LazyRow`, `SearchBar`, `OutlinePanel`, `AttachmentPanel` (export through
  `ActivityResultContracts.CreateDocument`), `HistoryControls`, `ToolPicker`,
  `TextSelectionOverlay` with the platform text toolbar, and `PdfReader`.
- [ ] Material 3 theming through `MaterialTheme`, no hard-coded colours; TalkBack labels and
  semantics on every control; RTL layout checked once.
- [ ] Gestures through `pointerInput` with `detectTransformGestures`, keeping Apple's rule that a
  drawing tool on top of the stage doesn't pan it.
- [ ] Tests: port `EmbedPDFUITests` as Robolectric `createComposeRule` tests, plus the two
  Review Focus tests owned here.

## Phase 6: Demo app and consumer check

About 4 days. Exit: a minified release build of the demo assembles, passes `zipalign -c -P 16`, and
its R8 mapping keeps every class the native side loads.

- [ ] `platforms/android/EmbedPDFDemo`: a Compose app at parity with
  `platforms/apple/EmbedPDFDemo` (document view, metadata sheet, side panels).
- [ ] `platforms/android/android-consumer.sh`, like `platforms/apple/macos-consumer.mjs`: builds the
  demo against the published artifacts rather than project dependencies.

## Phase 7: Release and documentation

About 4 days. Exit: one tagged Android release a consumer can depend on with four lines of Gradle.

- [ ] Publish workflow for D4, triggered the way `2026-11-19-release-workflows.md` triggers the
  Apple engine release, consuming the `runtime-<sha>` Android slices that plan already builds.
- [ ] `platforms/android/README.md` rewritten in the shape of `platforms/apple/README.md`:
  products, where the engine comes from, the plugin table, local engine mode.
- [ ] A plugin table row per capability, and `DisabledPluginTests` failing when `ViewerConfig`
  gains a plugin without one, as on Apple.

## Definition of done

`kotlin-api-coverage.json` has no `pending` rows and the coverage check passes. Every Swift test
file has a Kotlin counterpart or a recorded reason it doesn't apply. `apiCheck` passes. The minified demo
assembles and passes the alignment checks. The publish workflow is ready; publishing the first
release waits for the user.

Total: about 11 weeks of focused work if waves run one at a time; Phase 3 waves A and B, and
Phase 4 behind them, can overlap in separate worktrees, which brings it nearer 8. The estimate
assumes Phase 1 needs no Rust changes. If the full host trips another Kotlin name collision like
`message` or `close` (README, "Two things this shook out"), each one costs about a day in
`crates/uniffi-bindgen/src/kotlin.rs` or the owning `ffi.rs`.
