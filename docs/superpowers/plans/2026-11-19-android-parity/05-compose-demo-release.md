# Android Parity Phases 5 to 7: Compose UI, Demo, Release

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An `embedpdf-compose` module at parity with `EmbedPDFUI`, a Compose demo app at parity with `EmbedPDFDemo`, a consumer check, and a publish workflow ready to run.

**Architecture:** Composables take models from `embedpdf-models` (never capabilities directly, except where the Swift view does). Each Swift view becomes one public composable with a `Modifier` parameter first among optional parameters, slot lambdas where Swift takes a `@ViewBuilder`, and `remember`/`LaunchedEffect(model) { model.run() }` where Swift uses `.task`. Theming comes from `MaterialTheme`; strings from `res/values/strings.xml` (and the i18n plugin where Swift uses it).

**Tech Stack:** Compose BOM (latest stable that resolves with AGP 9.4.1 and Kotlin 2.2.21, Compose compiler through the Kotlin plugin), Material 3, `androidx.activity:activity-compose`, Robolectric + `androidx.compose.ui:ui-test-junit4` for UI tests.

**Spec:** umbrella Phases 5 to 7. Swift reference: `platforms/apple/EmbedPDF/Sources/EmbedPDFUI/`, `platforms/apple/EmbedPDFDemo/`, `platforms/apple/macos-consumer.mjs`, `docs/superpowers/plans/2026-11-19-release-workflows.md`.

## Global Constraints

As Phase 4. Additionally:
- Every interactive element has a content description or text and a semantics role; `HistoryControls` and `ToolPicker` buttons expose state (`selected`, `disabled`) through semantics.
- No hard-coded colours outside `MaterialTheme.colorScheme`, except page paper white.
- Gestures: a drawing tool active on the stage consumes the drag, so the stage doesn't pan (Swift `StageReaderView` overlay rule, `2026-11-18-swift-api-gaps.md` gap 5).
- The demo app and consumer check never publish anything. The publish workflow is written and validated (`actionlint` if available, a `--dry-run` local run in the container) but not triggered; the user triggers the first release.

## Review Focus

- Fast scroll through a 500-page synthetic document (generate with the page-edit plugin or reuse a large fixture): the bitmap pool never exceeds its budget.
- Configuration change mid-render: no crash, no leaked render job after `recreate()`.
- RTL locale: thumbnail strip and search bar mirror correctly.
- TalkBack: every control in `PdfReader` has a label (assert with `onAllNodes(hasClickAction()).assertAll(hasContentDescription() or hasText())`-style checks).
- Minified demo: R8 keeps every `uniffi.**` and JNA class (inspect `mapping.txt` / `usage.txt`).

---

### Task 1: Compose foundations

Port `PageImageView`, `TiledPageImage`, `ThumbnailStrip` (`LazyRow`), `ScrollVisibility`, `ModelScope`, `ControlPresentation`, `HapticFeedback` wiring (the service is in `embedpdf`), plus `ImageBitmap` and `Color` bridges for `AnnotationColor`.

- [x] Module `compose` (`embedpdf-compose`, `com.embedpdf.compose`) on Compose BOM 2026.06.01 (UI 1.11.4, Material 3 1.4.0): 2026.08.00 and later need `compileSdk` 37. `compose/api/compose.api` is checked by `apiCheck` in `check` and in `build-aar.sh`, which also builds and tests the module.
- [x] `PageImageView` keeps Swift's name because `PageImage` is the facade's raster. `TiledPageImage` attaches as the model's painter (`DisposableEffect`), acknowledges frames from its draw phase, acknowledges each tile two frames after it appears, and releases the page in a non-cancellable `finally` when it leaves. `ThumbnailStrip` runs its model and keys loads on `ThumbnailModel.pageRevisions`. `trackingScrollVisibility` takes a callback. `ModelScope` is non-skippable so only identity decides. Strings: `ResourceHistoryStrings`, `ResourceSearchStrings`, `rememberHistoryStrings()`, `rememberSearchStrings()`; haptics: `rememberHapticFeedback()`.
- [x] Tests under Robolectric with a paused looper that the tests advance a frame at a time (`ComposeTestSupport.kt`). Review focus: a 500-page fast scroll keeps a shared pool within budget, and `recreate()` mid-render leaves no demand behind.

### Task 2: Stage reader and selection

Port `StageReaderView` (as `StageReader`, with the page overlay slot `pageOverlay: @Composable (StageVisiblePage) -> Unit`), `TextSelectionDrag`, `TextSelectionOverlay` (copy through the platform `TextToolbar`/clipboard), `SearchHitReveals`.

- [x] `StageReader(model, modifier, onLinkActivation, pageOverlay)` in dp viewport units. The route is chosen at touch-down; a forwarded drag never pans; a second finger closes the drag and pinches; a double tap is sent after the pan's end so the end does not stop its zoom. An overlay that consumes the down keeps the touch from the pan, and one that consumes a move drops it. `TextSelectionOverlay` copies through `TextToolbar` and a `copyText` semantics action, reading `TextSelectionModel.selectedText()`.
- [x] Open: under Robolectric the first ink stroke after the tool turns on does not commit (direct `StageReaderModel` pointer calls reproduce it; the plain JVM model test does not), so `StageReaderTest` draws until ink lands.

### Task 3: Panels and chrome

Port `SearchBar`, `OutlinePanel`, `AttachmentPanel` (+ `AttachmentExportDocument` via `ActivityResultContracts.CreateDocument`), `HistoryControls`, `ToolPicker`, `PDFReaderView` (as `PdfReader`), `Exports`. UI tests: port `Tests/EmbedPDFUITests/*` under Robolectric.

### Task 4: Demo app

`platforms/android/EmbedPDFDemo/` Gradle app (included build or its own settings, depending on what keeps the library build fast): open a bundled PDF and `ACTION_OPEN_DOCUMENT`, `PdfReader`, metadata sheet, outline/attachment/thumbnail panels, as in `platforms/apple/EmbedPDFDemo/EmbedPDFDemo/*.swift`. Release build type minified with R8. `platforms/android/android-consumer.sh` assembles the minified demo against the artifacts from `./gradlew publishToMavenLocal` (not project dependencies), runs `zipalign -c -P 16` on the APK, and checks `usage.txt` contains no `uniffi.` or `com.sun.jna.` class.

### Task 5: Release workflow and README

`.github/workflows/android-release.yml` (manual `workflow_dispatch` only): builds the AAR in the dev image on `ubuntu-24.04`, runs `build-aar.sh test`, the coverage check and `apiCheck`, publishes the five artifacts to GitHub Packages Maven on the runtime fork with `embedpdf-bindings` versioned `0.0.0-engine.<hash>` from `android-engine-hash.sh`. Gradle `maven-publish` configuration with POMs. `platforms/android/README.md` rewritten in the shape of `platforms/apple/README.md`: products, where the engine comes from, the plugin table (one row per capability), local engine mode, consuming from GitHub Packages, limitations.
