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

### Task 2: Stage reader and selection

Port `StageReaderView` (as `StageReader`, with the page overlay slot `pageOverlay: @Composable (StageVisiblePage) -> Unit`), `TextSelectionDrag`, `TextSelectionOverlay` (copy through the platform `TextToolbar`/clipboard), `SearchHitReveals`.

### Task 3: Panels and chrome

Port `SearchBar`, `OutlinePanel`, `AttachmentPanel` (+ `AttachmentExportDocument` via `ActivityResultContracts.CreateDocument`), `HistoryControls`, `ToolPicker`, `PDFReaderView` (as `PdfReader`), `Exports`. UI tests: port `Tests/EmbedPDFUITests/*` under Robolectric.

### Task 4: Demo app

`platforms/android/EmbedPDFDemo/` Gradle app (included build or its own settings, depending on what keeps the library build fast): open a bundled PDF and `ACTION_OPEN_DOCUMENT`, `PdfReader`, metadata sheet, outline/attachment/thumbnail panels, as in `platforms/apple/EmbedPDFDemo/EmbedPDFDemo/*.swift`. Release build type minified with R8. `platforms/android/android-consumer.sh` assembles the minified demo against the artifacts from `./gradlew publishToMavenLocal` (not project dependencies), runs `zipalign -c -P 16` on the APK, and checks `usage.txt` contains no `uniffi.` or `com.sun.jna.` class.

### Task 5: Release workflow and README

`.github/workflows/android-release.yml` (manual `workflow_dispatch` only): builds the AAR in the dev image on `ubuntu-24.04`, runs `build-aar.sh test`, the coverage check and `apiCheck`, publishes the five artifacts to GitHub Packages Maven on the runtime fork with `embedpdf-bindings` versioned `0.0.0-engine.<hash>` from `android-engine-hash.sh`. Gradle `maven-publish` configuration with POMs. `platforms/android/README.md` rewritten in the shape of `platforms/apple/README.md`: products, where the engine comes from, the plugin table (one row per capability), local engine mode, consuming from GitHub Packages, limitations.
