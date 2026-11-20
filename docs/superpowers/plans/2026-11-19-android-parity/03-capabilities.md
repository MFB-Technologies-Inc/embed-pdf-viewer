# Android Parity Phase 3: Plugin Capabilities

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Each wave below is one implementer dispatch and one review. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every plugin capability Swift exposes, as a typed Kotlin capability in `com.embedpdf`, with its platform services and its Swift tests ported.

**Architecture:** The recipe per plugin is fixed by Phase 2 and by `platforms/apple/EmbedPDF/Sources/EmbedPDF/Plugins/Search.swift`:

1. `typealias` every generated record, enum and event the capability's signatures use (Swift's typealias block at the top of the file is the list).
2. `class XCapability internal constructor(private val handle: XHandle, val viewer: PdfViewer)` with one `suspend fun` per Swift public member, same verb, Kotlin parameter idiom, KDoc copied in meaning from the Swift `///`.
3. A `PdfViewer` accessor with Swift's name (`search(document)`, `render(document)`, ...) that throws `PdfException(Code.UNSUPPORTED)` when the plugin isn't in the config, even for a closed or loading document. Companion-plugin checks (`RedactionCapability.markSelection()` needs selection, and the others listed in `platforms/apple/README.md` "Plugins") are ported as the same per-call `UNSUPPORTED` throws.
4. Events: `fun observeEvents(): Flow<XEvent>` per capability that has an event stream in Swift.
5. Port the Swift test file for the plugin (`Tests/EmbedPDFTests/<Plugin>Tests.swift`), extend `DisabledPluginTest` with the accessor, flip its `kotlin-api-coverage.json` rows from `pending` to the Kotlin symbol, run `./gradlew apiDump`.

Foreign traits (generated callback interfaces) are implemented by public Kotlin classes. Their callbacks arrive on Rust threads: an adapter that touches Android UI posts to `Handler(Looper.getMainLooper())` and its KDoc says which thread it runs on.

**Tech Stack:** as Phase 2, plus `kotlinx-serialization-json` (drafts, wave D), `androidx.javascriptengine` (wave B), Robolectric 4.14+ for Android-class tests.

**Spec:** umbrella Phase 3 and D3. Swift reference: `platforms/apple/EmbedPDF/Sources/EmbedPDF/Plugins/` and `platforms/apple/README.md` "Plugins".

## Global Constraints

As Phase 2. Additionally:
- Closed string sets get `enum class`; open sets get a `@JvmInline value class` with companion constants (Swift's `RawRepresentable` structs, such as `ToolID`, `AnnotationSubtype`).
- Typed drafts and patches serialize to exactly the bytes the Swift `Encodable` types produce. Each draft test compares the Kotlin JSON with the Swift test's expected JSON literal.
- A draft field that can be "leave alone", "set" or "clear" uses a sealed `Nullable<T>` (`Unchanged`, `Value(v)`, `Clear`), port of Swift's `Nullable` in `AnnotationDraft.swift`.

## Review Focus

- A capability held after its document closes throws `INSTANCE_CLOSED`, never crashes.
- A foreign adapter invoked off the main thread doesn't touch a `View` (Robolectric test asserts no `CalledFromWrongThreadException`).
- `JavaScriptSandbox` unavailable: actions with JavaScript enabled degrade to "JavaScript disabled" and report once through `observeReportedErrors()`, rather than failing every action.
- Rendering returns a `Bitmap` whose pixel bytes match the RGBA buffer (premultiplied alpha handled the way `CoreGraphicsBridges.swift` handles it).
- Android Keystore signer: a key that requires user authentication fails with a `PdfException`, not a crash.

---

### Task A: Wave A (metadata, search, render, history, outline, attachment, page-edit, view-manager, shell)

Swift files: `Plugins/{Metadata,Search,Render,History,Outline,Attachment,PageEdit,ViewManager,Shell}.swift`, `PageGeometry.swift`, `PageRenderResolution.swift`, `CoreGraphicsBridges.swift`, `TaskEntryPoints.swift`.
Tests: `MetadataTests`, `SearchTests`, `RenderTests`, `RenderViewportTests`, `RenderSnapshotTests` (compare against PNGs decoded on the JVM, not pixel-exact to Apple; assert dimensions and a checksum of the Linux render), `PageGeometryTests`, `PageRenderResolutionTests`, `HistoryTests`, `OutlineTests`, `AttachmentTests`, `PageEditTests`, `ViewManagerTests`, `ShellTests`, `DisabledPluginTests`, `PluginSettingsTests`, `TaskEntryPointTests`.
Android services: `PageImage.toBitmap(): Bitmap`, `PageImage.toImageBitmap()` lives in compose (Phase 5); `RectF`/`PointF` conversions for `PageGeometry`.

- [x] One commit per plugin, tests green at each commit.
- Moved to later waves, because they need capabilities those waves port: `TaskEntryPoints.swift` and `TaskEntryPointTests` (stage and interaction) go to Task C; the capture half of `RenderSnapshotTests` (the snapshot tool through interaction) to Task B; the page attachment case of `AttachmentTests` (annotation drafts) to Task D; each wave adds its own `PluginSettingsTests` cases and `DisabledPluginTest` rows, and shrinks that test's `pending` set.
- Variants of generated sealed types have flat aliases named `<Parent><Variant>` (`SearchEventCompleted`, `PageEditPlacementEnd`), since a nested class isn't reachable through a typealias and names like `SearchCompletedEvent` already belong to the payload records.

### Task B: Wave B (interaction + feedback, i18n, actions)

Swift files: `Plugins/{Interaction,Feedback,I18n,Actions,JavaScriptCoreEngine}.swift`, `EmbedPDFUI/HapticFeedback.swift`.
Tests: `InteractionTests`, `FeedbackTests`, `PointerLatencyTests` (record the JVM number, don't assert Apple's), `I18nTests` (with `Tests/EmbedPDFTests/Locales` copied to test resources), `ActionsTests`, `JavaScriptCoreEngineTests` (as `JavaScriptSandboxEngineTest`; under Robolectric the sandbox is unavailable, so test the unavailable path and the adapter contract with a fake `ScriptEngine`).
Android services: `HapticFeedback(view: View)` implementing `PlatformFeedback` with `View.performHapticFeedback` constants mapped from the Swift mapping; `AssetLocaleLoader(assets: AssetManager, directory: String = "embedpdf/locales")`; `JavaScriptSandboxEngineFactory(context: Context)`. `androidx.javascriptengine` declares `minSdk` 26 (Phase 1 finding), so the manifest uses `tools:overrideLibrary="androidx.javascriptengine"` and the factory reports JavaScript unavailable below API 26 (`@RequiresApi(26)` on the internals, a `Build.VERSION.SDK_INT` gate at the entry).

### Task C: Wave C (stage, selection, link, commands)

Swift files: `Plugins/{Stage,Selection,Link,Commands}.swift`.
Tests: `StageTests`, `SelectionTests`, `LinkTests`, `CommandsTests`, plus `platforms/android/tests/CommandContextTest.kt` cases.
Android services: `CommandHandler` fun-interface adapter (`commands.register(id) { context -> ... }`).

### Task D: Wave D (annotation, form)

Swift files: `Plugins/Annotation*.swift` (14 files, about 6,200 lines), `Plugins/{AnnotationColor,AnnotationPosition,AnnotationSubtype,AnnotationIntent,ToolID}.swift`, `Plugins/Form.swift`.
Tests: `AnnotationTests`, `AnnotationDraftTests`, `AnnotationKindDraftTests`, `AnnotationInfoTests`, `AnnotationColorTests`, `AnnotationPatchDecodingTests`, `TypedOverloadTests`, `WireValueTests`, `FormTests`, `EditingWorkflowsTests`.
Split into two dispatches: D1 the capability and records, D2 typed drafts, patches and `AnnotationInfo` kinds.
Android services: `AnnotationColor` ↔ `androidx.compose.ui.graphics.Color` lives in compose; here `@ColorInt Int` conversions.

### Task E: Wave E (redaction, measurement, stamp, signature)

Swift files: `Plugins/{Redaction,Measurement,Stamp,Signature,KeychainSigner}.swift`, `SignatureTransport.swift`.
Tests: `RedactionTests`, `MeasurementTests`, `StampTests`, `SignatureTests`, `SignatureTransportTests`, `KeychainSignerTests` (as `AndroidKeyStoreSignerTest` under Robolectric with a software key, since Robolectric has no Keystore: test the signer through an injected `KeyStore`/`PrivateKey`), `SecurityTests`.
Android services: `FileStampLibraryStore(directory: File)`; `AndroidKeyStoreSigner(alias: String, keyStore: KeyStore = AndroidKeyStore)`; `TrustAnchors.fromAssets(...)` and `TrustAnchors(certificates: List<X509Certificate>)`; move `platforms/android/viewer/HttpSignatureTransport.kt` into `embedpdf` as `HttpSignatureTransport` with its `SignatureTest` cases, and keep `test-signature.sh` working or replace it with the Gradle test and say so in the README.
