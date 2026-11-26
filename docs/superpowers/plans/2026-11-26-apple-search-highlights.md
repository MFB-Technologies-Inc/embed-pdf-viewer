# Apple search highlights handoff

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `PDFReaderView` draws search hits, with a public `SearchHighlightOverlay` an app can place in its own `StageReaderView`, matching what the Android package shipped in `fa4a79d3f`, `acf6a6527` and `26659d172`.

**Architecture:** The engine paints no hits into tiles. `crates/plugins/render/src/invalidation.rs` repaints only for annotation, form and content facts, and the search plugin only stores colours (`SearchHighlight` in `crates/plugins/search/src/contract.rs:201-236`, default `#ffd500`, active `#ff9632`, `multiply`). Every web framework draws its own layer from that setting (`packages/framework/web/src/search-highlights.ts`, `packages/framework/angular/search/src/search-layer.ts`). The native readers have to do the same. `SearchModel` gains the highlight setting, and `EmbedPDFUI` gains an overlay view that draws the page's hits from it.

**Tech Stack:** Swift 6.2, SwiftUI `Canvas`, Observation, Swift Testing with `CustomDump`, iOS 17 and macOS 14 floors.

**Spec:** this document. The Android implementation is the reference for behaviour; where Swift idiom differs, Swift idiom wins and the behaviour stays the same.

## The gap

`PDFReaderView.swift:94-103` stacks only `TextSelectionOverlay` and the app's `pageOverlay` over each page, and `RevealingSearchBar` (`PDFReaderView.swift:129-135`) only scrolls the active hit into view. Nothing in `EmbedPDFUI` reads `SearchModel.hits` rects or the search settings' `highlight`. A search for "Trace" in `report.pdf` reports "1 of 416" and shows nothing on the page. Android had the identical gap until `acf6a6527`. Its device check is in `platforms/android/README.md` under "Device runs".

## Global Constraints

- iOS 17 and macOS 14 floors. No new package dependencies.
- No change to `Sources/EmbedPDFBindings/`, `crates/` or TypeScript, so no engine rebuild and no new `apple-engine-*` release.
- Existing public signatures keep compiling. `StageReaderView(model:)`, `StageReaderView(model:pageOverlay:)` and `PDFReaderView`'s initializers stay source compatible.
- Code style follows the neighbouring files: two-space indent, `throws(PDFError)` typed throws, `///` doc comments in present tense, rules in `docs/conventions/comments.md`.
- Builds and tests run on the Mac host and its local simulator, the only place Swift runs.
- Commit messages follow `feat(apple): …` with no co-author or session trailer.

## Review Focus

- A page turned 90 or 270 degrees, or a view rotation: each hit lands on its word, because the overlay uses the same page-to-local mapping as `TextSelectionOverlay`.
- A hit whose quads are not axis-aligned (rotated text): it draws as the four-corner shape, not a bounding box.
- `updateSettings` changing `highlight.color`: the overlay repaints without a new search.
- The active hit moving with `nextHit`: only the old and new active hits change colour.
- A colour string the overlay can't read (`"not-a-colour"`): that slot falls back to the plugin default, with no crash and no clear.

---

### Task 1: `SearchModel.highlight`

**Files:**
- Modify: `platforms/apple/EmbedPDF/Sources/EmbedPDFModels/SearchModel.swift`
- Test: `platforms/apple/EmbedPDF/Tests/EmbedPDFModelsTests/SearchModelTests.swift`

**Interfaces:**
- Produces: `public private(set) var highlight: SearchHighlightSettings` on `SearchModel`, initially the plugin default (`color: "#ffd500"`, `activeColor: "#ff9632"`, `blendMode: .multiply`), refreshed with the rest of the snapshot in `refresh()` from `search.settings().highlight`.

- [ ] **Step 1: Write the failing test** `theHighlightFollowsTheSearchSettings`. It updates the search settings' highlight colour through `SearchCapability.updateSettings`, lets the model refresh, and expects `model.highlight.color` to equal the new value. Android's twin is `SearchModelTest.theHighlightFollowsTheSearchSettings` in `platforms/android/embedpdf/models/src/test/kotlin/com/embedpdf/models/SearchModelTest.kt`.
- [ ] **Step 2: Run it and see it fail** (`highlight` doesn't exist).
- [ ] **Step 3: Implement.** Read `search.settings().highlight` in the same refresh that reads hits, and keep the existing rule that a refresh started earlier never overwrites one started later. A settings update bumps the store revision, so the model's existing revision loop reaches it. Note the cost in an inline comment: one more engine read per refresh.
- [ ] **Step 4: Run the suite** and see it pass.
- [ ] **Step 5: Commit** `feat(apple): follow the search highlight setting in SearchModel`.

### Task 2: `SearchHighlightOverlay`

**Files:**
- Create: `platforms/apple/EmbedPDF/Sources/EmbedPDFUI/SearchHighlightOverlay.swift`
- Modify: `platforms/apple/EmbedPDF/Sources/EmbedPDFModels/StageVisiblePage+Viewport.swift` (add `localPoint`)
- Test: `platforms/apple/EmbedPDF/Tests/EmbedPDFUITests/SearchHighlightOverlayTests.swift`

**Interfaces:**
- Consumes: `SearchModel.hits`, `SearchModel.activeHit`, `SearchModel.highlight` (Task 1), `StageVisiblePage.localRect(_:info:)`.
- Produces:
  ```swift
  public struct SearchHighlightOverlay: View {
    public init(model: SearchModel, visiblePage: StageVisiblePage, info: PageInfo,
                color: Color? = nil, activeColor: Color? = nil, blendMode: GraphicsContext.BlendMode? = nil)
  }
  extension StageVisiblePage {
    public func localPoint(_ point: Point, info: PageInfo) -> CGPoint
  }
  ```
  The three optional parameters override the setting, as CSS variables do on the web and as the Android overlay's parameters do.

Behaviour, matching `platforms/android/embedpdf/compose/src/main/kotlin/com/embedpdf/compose/SearchHighlightOverlay.kt`:
- Draws the hits whose `page` is `visiblePage.page`. A hit equal to `activeHit` fills with `activeColor`, every other hit with `color`.
- For each segment quad: when `upperLeft.y == upperRight.y`, `lowerLeft.y == lowerRight.y` and `upperLeft.x == lowerLeft.x` (the web layers' upright test), it fills a rounded rectangle (2 pt corners) from the upper-left to the lower-right corner, mapped through `localRect`. Otherwise it fills the four-corner path through `localPoint`.
- Reads colours as CSS: `#rgb`, `#rgba`, `#rrggbb`, `#rrggbbaa`, `rgb()` and `rgba()` (commas or spaces, an optional `/ alpha`, channels as numbers or percentages, clamped), the 16 basic names plus `orange` and `transparent`. A colour it can't read uses the plugin default for that slot. The Android parser is `cssColor` in the same file; port its cases into the tests.
- Maps each of the 16 `SearchBlendMode` cases to the `GraphicsContext.BlendMode` of the same name, with `normal` as `.normal`. SwiftUI supports all 16 on both floors, so unlike Android there's no fallback.
- No added opacity, matching the web.
- Decorative only: `.accessibilityHidden(true)` and `.allowsHitTesting(false)`, so it never takes a touch from the stage or the selection overlay.
- Draws in a `Canvas` so 400 hits on a page stay one view.

- [ ] **Step 1: Write failing tests.**
  - `aHitIsFilledWhereItsWordIs`: render the overlay with `ImageRenderer` over a known `StageVisiblePage` and one hit, and check that a pixel inside the hit's local rect has the fill and a pixel outside is clear.
  - `theActiveHitUsesTheActiveColour`
  - `aTurnedPageMapsTheHitWithThePage` (90 degrees)
  - `aSettingsChangeRepaints`
  - `anOverrideColourWins`
  - `anUnreadableColourFallsBackToTheDefault`
  - `cssColoursParse`: a table of the Android `cssColor` cases.
  - `everyBlendModeMaps`
  - `theOverlayHasNoAccessibilityElement`

  Android's twin is `platforms/android/embedpdf/compose/src/test/kotlin/com/embedpdf/compose/SearchHighlightOverlayTest.kt`.
- [ ] **Step 2: Run them and see them fail.**
- [ ] **Step 3: Implement** `localPoint` (the inverse of `pagePoint`, using the same mapping as `localRect`), then the overlay.
- [ ] **Step 4: Run the suites** and see them pass.
- [ ] **Step 5: Commit** `feat(apple): draw search hits with SearchHighlightOverlay`.

### Task 3: Wire it into `PDFReaderView`, document, and check by eye

**Files:**
- Modify: `platforms/apple/EmbedPDF/Sources/EmbedPDFUI/PDFReaderView.swift:94-103`, `platforms/apple/README.md`
- Test: `platforms/apple/EmbedPDF/Tests/EmbedPDFUITests/` (the reader's existing tests)

- [ ] **Step 1: Write a failing test.** A `PDFReaderView` session with a search that found hits contains a `SearchHighlightOverlay` under each page's `TextSelectionOverlay`.
- [ ] **Step 2: Implement.** In the `pageOverlay` `ZStack`, put `SearchHighlightOverlay(model: search, visiblePage: page, info: info)` first, so it sits below the selection overlay and the app's overlay, and only when the session has a search model and `pageInfo(for:)` answers. Then add a sentence and a code sample to the README showing the overlay in a hand-built `StageReaderView(model:pageOverlay:)`.
- [ ] **Step 3: Run the package tests** on the simulator (`xcodebuild test -scheme EmbedPDF-Package -destination 'platform=iOS Simulator,id=<UDID>'`).
- [ ] **Step 4: Check by eye.** Run `platforms/apple/EmbedPDFDemo`, search "Trace" in `report.pdf`, and confirm the title's "Trace" is orange and the other hits are yellow, with the text readable through them. That's what the Android demo shows after `acf6a6527`.
- [ ] **Step 5: Commit** `feat(apple): show search hits in PDFReaderView`.

The cost is new public API on two types (`SearchModel.highlight`, `SearchHighlightOverlay`) plus `StageVisiblePage.localPoint`, and one more engine read per search-model refresh, scrolls included.
