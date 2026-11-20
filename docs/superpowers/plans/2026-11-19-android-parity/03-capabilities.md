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

- [x] One commit per plugin or service, tests green at each commit.
- `ToolId` (Swift's `ToolID.swift`, listed under wave D) arrived with interaction, since the hub's calls take it; `AnnotationTool.toolId` stays with wave D. Swift's deprecated `String` overloads were not ported.
- `InteractionHandler` is a Kotlin interface over the generated one with the hub's defaults for the optional members.
- The JavaScript sandbox fallback: a viewer whose config enables JavaScript with a `JavaScriptSandboxEngineFactory` that has no sandbox builds with JavaScript disabled, and reports `unsupported` once, when the first document opens or `actions(document)` is first called.
- `PointerLatencyTest` (opt in with `EMBEDPDF_LATENCY=1`) printed p50 876 us, p95 2427 us, max 50 ms on the container's JVM.
- `EmbedPdfTestRunner` (in the wave A review fix) keeps uniffi and JNA out of Robolectric's sandbox class loader, which replaces `forkEvery = 1`.

### Task C: Wave C (stage, selection, link, commands, task entry points)

Swift files: `Plugins/{Stage,Selection,Link,Commands}.swift`.
Tests: `StageTests`, `SelectionTests`, `LinkTests`, `CommandsTests`, plus `platforms/android/tests/CommandContextTest.kt` cases.
Android services: `CommandHandler` fun-interface adapter (`commands.register(id) { context -> ... }`).

- [x] One commit per plugin, tests green at each commit; `TaskEntryPoints` (`PageNavigator`, `PageZoom`, `ToolSwitcher`) has its own commit after stage.
- `CommandHandler` is a class of optional lambdas with `run` last, not a fun interface: it has to report which hooks it implements (`hooks`), and a fun interface can't tell an overridden default from an inherited one. The trailing-lambda forms are `CommandHandler { context -> ... }` and `commands.register("doc:save") { context -> ... }`, whose `labelKey` defaults to the id. `run` runs on `Dispatchers.Default`; the derive hooks run on the kernel thread, and one that throws answers as a failed hook.
- Swift argument labels became overloads by type: `link(point, page)` beside `link(id, page)`, `selectWord(point, page)`, `activate(point, page)`, `label(targetJson)`. Points come before pages, as in Swift. The stage's point lookup is `pageAt(point)`, not a `page` overload, after the wave C review.
- Not ported: the `AnnotationLinkTarget` overloads of `LinkCapability.resolve`, `activate` and `label`, since the typed target arrives with wave D; Swift's deprecated `UInt32` and `Bool?` overloads.
- `TaskEntryPointTest` checks the forwarding by reflection (same name, parameter and result types on the capability) instead of Swift's compile-time table and source audit.
- `StageSettingsPatch.turnGestures` has no default in the generated Kotlin, so every patch passes `turnGestures = null`. The stage's default responsive rules rewrite `padding` once they apply, so `resetSettings()` can differ from the first `settings()` read; the settings test opts out with `StageConfig(responsive = emptyList())`.

### Task D: Wave D (annotation, form)

Swift files: `Plugins/Annotation*.swift` (14 files, about 6,200 lines), `Plugins/{AnnotationColor,AnnotationPosition,AnnotationSubtype,AnnotationIntent,ToolID}.swift`, `Plugins/Form.swift`.
Tests: `AnnotationTests`, `AnnotationDraftTests`, `AnnotationKindDraftTests`, `AnnotationInfoTests`, `AnnotationColorTests`, `AnnotationPatchDecodingTests`, `TypedOverloadTests`, `WireValueTests`, `FormTests`, `EditingWorkflowsTests`.
Split into two dispatches: D1 the capability and records, D2 typed drafts, patches and `AnnotationInfo` kinds.
Android services: `AnnotationColor` ↔ `androidx.compose.ui.graphics.Color` lives in compose; here `@ColorInt Int` conversions.

- [x] D1: annotation records, the annotation capability, the form capability; one commit each, tests green at each.
- [x] D2: typed drafts and patches, `AnnotationInfo` kinds.
- D1 also brought the items earlier waves moved here: the page-attachment case of `AttachmentTests`, the `AnnotationLinkTarget` overloads of `LinkCapability.resolve`, `activate` and `label`, and `AnnotationTool.toolId`.
- `AnnotationPosition` is a sealed interface of `Start`, `End`, `Before(annotation)` and `After(annotation)`: the engine's whole vocabulary (`ListPosition` in `crates/epdf-engine/src/mutation/list_position.rs`), as in Swift. A front/back/forward/backward set would need positions the engine doesn't read.
- `Nullable<T>` is `Unchanged`, `Value(v)`, `Clear`, as the global constraints say; `AnnotationCommonFields` uses it now. `AnnotationRichText` holds the engine's open style maps as `JsonElement`, where Swift has its own `JSONValue`.
- `AnnotationInfo` is a class over the generated record, equal by record, with the common members, the blend mode and the timestamps. The timestamps are epoch milliseconds (`Long?`), since `java.time.Instant` needs API 26 and minSdk is 24; a fraction finer than a millisecond is dropped. D2 adds `details` and the kind accessors without changing what is there.
- The JSON entry points take Kotlin parameter names (`fieldsJson`, `patchJson`, `draftJson`, `placementJson`), so D2's typed overloads sit beside them by type: `create(draft, page)` next to `create(fieldsJson, page)`, `update(ref, patch)` next to `update(ref, patchJson)`. Swift's argument labels became overloads by type, as in wave C: `field(widget)` beside `field(ref)`, `widgets(field)` beside `widgets(page)`, `widget(point, page)` beside `widget(ref)`. `setTextSelection` takes an `IntRange` (`2 until 5`).
- `AnnotationFieldBag` writes sorted keys and spells integral doubles without a fraction, as Swift's `JSONEncoder` does, for D2's byte-for-byte draft tests. Other doubles differ in exponent spelling only (`1.0E-7` against `1e-07`); D2 decides whether a draft can reach one. `AnnotationSharedMembers` (the measure wire mirror) has no use before the drafts and waits for D2.
- Android services: `AnnotationAttachmentFile(resolver, uri)`, `AnnotationStampSource(resolver, uri)` and `AnnotationPickedFile(resolver, uri)` read a picked document through a `ContentResolver` on `Dispatchers.IO`, with the provider's display name and MIME type. Bytes plus name and MIME type are the generated constructors. `@ColorInt` comes from `androidx.annotation`, now an `implementation` dependency.
- Not ported: Swift's deprecated `AnnotationRecord` read overloads and `String` tool and subtype overloads, `aSubCapabilityHeldAloneKeepsTheViewerAndReleasesIt` (a Kotlin viewer is released by `destroy()`, not by the last reference; the port checks that a held sub-capability answers), and FormTests' `aDocumentScriptRunsInJavaScriptCore` (Robolectric has no sandbox). The hit-test case pins the typing, as Swift's does, not what is hit.
- D2: drafts and patches are data classes; a member Swift spells `Nullable<T>?` is `Nullable<T> = Nullable.Unchanged`, and one Swift spells `T?` is `T? = null`. Each patch's companion is its `AnnotationPatch.Reader` (`InkAnnotationPatch.fromJson(json)`), and Swift's `defaults(for:as:)` is `tools.defaults(tool, InkAnnotationPatch)`. A patch that doesn't read throws `invalid-input` (`DecodingError`), where Swift's `Decodable` throws `DecodingError`.
- `AnnotationInfo.details` is a sealed `Details` with one data class per typed subtype and `Details.Untyped`. Swift's shared `TextMarkup` and `Shape` payloads are sealed interfaces here, with `Underline`/`Squiggly` and `Square`/`Circle` under them, so `info.underline` returns an `Underline`. The detail types have public constructors, where Swift's have none.
- Field bags spell numbers as Swift's `JSONEncoder` does (`Double.description` with `.0` dropped: `0.0005`, `1e-07`, `1e+16`, `-0`) and sort keys as Foundation's `.sortedKeys` does (case-insensitive, numeric). No Swift test expectation pins an exponent or a key pair the two orders sort apart; the rules come from Foundation's encoder. One known gap: `Double.MIN_VALUE` spells `4.9e-324` here and `5e-324` in Swift, since Java's shortest form keeps two digits for it.
- `AnnotationRichText` keeps its open maps as `JsonElement`, which compares by text: the engine's `14.0` is not equal to `JsonPrimitive(14)`. Swift's `JSONValue` compares numbers by value.
- `AnnotationMeasure` (Swift's `WireMeasurement`) writes and reads `PdfMeasurement`; the measurement typealiases (`PdfMeasurement`, `PdfNumberFormat`, `MeasureOrigin`, `MeasureFraction`, `MeasureLabelPosition`) live in `AnnotationSharedMembers.kt` for wave E to use.
- `EditingWorkflowsTests` has no draft cases left to port: its annotation case creates through JSON, as the Kotlin port does.

### Task E: Wave E (redaction, measurement, stamp, signature)

Swift files: `Plugins/{Redaction,Measurement,Stamp,Signature,KeychainSigner}.swift`, `SignatureTransport.swift`.
Tests: `RedactionTests`, `MeasurementTests`, `StampTests`, `SignatureTests`, `SignatureTransportTests`, `KeychainSignerTests` (as `AndroidKeyStoreSignerTest` under Robolectric with a software key, since Robolectric has no Keystore: test the signer through an injected `KeyStore`/`PrivateKey`), `SecurityTests`.
Android services: `FileStampLibraryStore(directory: File)`; `AndroidKeyStoreSigner(alias: String, keyStore: KeyStore = AndroidKeyStore)`; `TrustAnchors.fromAssets(...)` and `TrustAnchors(certificates: List<X509Certificate>)`; move `platforms/android/viewer/HttpSignatureTransport.kt` into `embedpdf` as `HttpSignatureTransport` with its `SignatureTest` cases, and keep `test-signature.sh` working or replace it with the Gradle test and say so in the README.
