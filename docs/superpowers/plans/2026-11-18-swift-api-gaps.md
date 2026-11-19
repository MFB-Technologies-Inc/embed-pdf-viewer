# Swift API gaps implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close five gaps in `platforms/apple/EmbedPDF` that force every app embedding the viewer to write the same glue: typed drafts for every annotation subtype, page rotation helpers, render revisions outside the stage reader, a drag text-selection model, and a page overlay slot on `StageReaderView`.

**Architecture:** All work lands in the hand-written layer, `Sources/EmbedPDF/` and `Sources/EmbedPDFUI/`. Nothing crosses the UniFFI boundary, so no engine rebuild and no new `apple-engine-*` release is needed; tests run against the published `apple-engine-8a472a28bcfb9c14` binary the package already pins. Task 1 merges finished work from `feat/apple-typed-annotation-kinds`; tasks 2 to 5 are new.

**Tech Stack:** Swift 6.2, SwiftUI, Observation, Swift Testing with `CustomDump`, iOS 17 and macOS 14 floors.

**Spec:** the "Gaps" section below. This plan is its own spec.

## Gaps

These come from reading an app that embeds `EmbedPDF` `0.0.0-swift.6` (`d59dbcafe`) and checking each workaround against the package.

1. Typed drafts exist only for ink, highlight and note (`Sources/EmbedPDF/Plugins/AnnotationDraft.swift:135-325`). Every other subtype goes through the `fieldsJSON` overload of `AnnotationCapability.create`, and free-text `contents` needs a second `update` call. `feat/apple-typed-annotation-kinds` (`9741d9156`) closes this and is not merged.
2. `PageInfo.rotation` is reported, but sizes, hit tests, selection rects, annotation geometry and thumbnails are all in unrotated page space. Each app writes its own point, rect and image rotation. The engine already renders rotated pages through `RenderPageOptions.rotation` ("Absolute clockwise rotation; omitted is unrotated page space", `crates/plugins/render/src/ffi.rs:79`), but `renderThumbnail` takes no rotation.
3. Per-page render revisions live only inside `StageReaderModel` (`Sources/EmbedPDFUI/StageReaderModel.swift:106`, the event loop at `:96-97` and `:129-132`). An app that lays out pages itself and feeds `TiledPageImage` its `revision` has to subscribe to `render.events` and count `.invalidated` by hand.
4. Drag-to-select is a coalescing loop every app rewrites: begin a gesture, `extend(to:on:)` for the latest point only, read `rects(on:)` for every selected page, end the gesture. `EmbedPDFUI` has models for search, outline and history but none for selection.
5. `StageReaderView` has one initializer, `init(model:)` (`Sources/EmbedPDFUI/StageReaderView.swift:10`). An app that needs annotation overlays or tool gestures on pages cannot use the stage reader at all and rebuilds layout, zoom and page tracking itself. Its zero-distance `DragGesture` also takes every touch, so a drawing tool on top of it would pan the document.

## Global Constraints

- iOS 17 and macOS 14 floors; no new package dependencies.
- Every build and test runs on the local machine and its local simulator. No remote host runs any step. Node or TypeScript work, if any turns up, runs in a dev container, never on the host.
- No change to `Sources/EmbedPDFBindings/`, `crates/`, TypeScript or CI files.
- Existing public signatures keep compiling. `StageReaderView(model:)` and `StageReaderModel.renderRevision(for:)` stay source compatible.
- Code style follows the neighboring files: two-space indent, `throws(PDFError)` typed throws, `///` doc comments in present tense, rules in `docs/conventions/comments.md`.
- Commit messages follow the repository's `feat(apple): …` form, with no co-author or session trailer.
- Package prose and comments name no outside product or company. The README gets one short section per new API.
- Work happens on `feat/apple-api-gaps`, branched from `research/multi-platform-rust-core-impl-plugins` at `f329b82e1`, one commit per task. `feat/apple-typed-annotation-kinds` is merged into the work branch, so everything lands in the plugins branch together when the work branch is merged there. That last merge and any push wait for the user.

Test command, from `platforms/apple/EmbedPDF` (written `$TEST` below):

```sh
xcodebuild test -scheme EmbedPDF-Package \
  -destination 'platform=iOS Simulator,id=5FA32A5D-6186-4C16-BE25-C87829DD1BFA' \
  -only-testing:<Target>/<Suite>
```

`EmbedPDFTests` suites are nested in `BaseSuite`, so their path is `EmbedPDFTests/BaseSuite/<Suite>`. `EmbedPDFUITests` suites are top level: `EmbedPDFUITests/<Suite>`.

## Review Focus

- A page whose `/Rotate` is 90 or 270: the display size swaps width and height, a rotated render comes back landscape for a portrait page, and a point converted to display space and back lands where it started. Task 2 pins all three on a real rotated page.
- A rotation value that is negative, above 360 or huge (`-90`, `450`, `Int.max`): it normalizes to a quarter turn and never traps. Task 2 pins it.
- A drag that moves faster than the engine answers: points that arrive while an extension is queued collapse into one, and `dragEnded()` returns after that extension lands, so `rects` read afterward are final. Task 4 pins it with an extension count.
- Gesture callbacks that SwiftUI delivers from separate tasks: a change or an end that arrives while `dragBegan` is still awaiting the engine is applied after the begin, never before it, and never leaves the engine's gesture open. Task 4 pins it.
- A document closing while a model's `task()` runs: the task returns and the model stops changing. Tasks 3 and 4 pin it.

---

### Task 1: Merge `feat/apple-typed-annotation-kinds` into the work branch

**Files:**
- Merge: `9741d9156`, which touches `Sources/EmbedPDF/Plugins/AnnotationDraft*.swift`, `AnnotationInfo*.swift`, `Tests/EmbedPDFTests/AnnotationKindDraftTests.swift`, `AnnotationDraftTests.swift` and `platforms/apple/README.md`.
- Leave out: `docs/superpowers/reports/2026-11-13-swift-typed-annotation-kinds-handoff.md`. It holds session ids and resume instructions that must not enter the repository.

**Interfaces:**
- Produces: `FreeTextAnnotationDraft` with `contents: Nullable<String>?`, plus typed drafts and patches for underline, squiggly, strikeout, redaction, square, circle, line, polygon, polyline, caret and link. Later tasks do not depend on them.

- [ ] **Step 0: Create the work branch and commit this plan**

Run: `git switch -c feat/apple-api-gaps f329b82e1`, then `git add docs/superpowers/plans/2026-11-18-swift-api-gaps.md && git commit -m "docs: plan the Swift API gap work"`.

- [ ] **Step 1: Merge without committing, drop the handoff, commit**

```bash
git merge --no-ff --no-commit feat/apple-typed-annotation-kinds
git rm --cached -q docs/superpowers/reports/2026-11-13-swift-typed-annotation-kinds-handoff.md
rm docs/superpowers/reports/2026-11-13-swift-typed-annotation-kinds-handoff.md
git commit -m "Merge feat/apple-typed-annotation-kinds: typed drafts for every annotation subtype"
```

Expected: no conflicts (`git merge-tree --write-tree f329b82e1 feat/apple-typed-annotation-kinds` reports none). If one appears anyway, keep both sides' additions, preferring HEAD's member where the branch duplicates one, and conclude with the same `git commit`.

- [ ] **Step 2: Build for macOS and run the new suite on iOS**

Run: `swift build` in `platforms/apple/EmbedPDF`, then `$TEST` with `-only-testing:EmbedPDFTests/BaseSuite/AnnotationKindDraftTests`.
Expected: build succeeds; every `AnnotationKindDraftTests` test passes.

- [ ] **Step 3: Run the annotation suites and the coverage checker**

Run: `$TEST` with `-only-testing:EmbedPDFTests/BaseSuite/AnnotationDraftTests -only-testing:EmbedPDFTests/BaseSuite/AnnotationInfoTests -only-testing:EmbedPDFTests/BaseSuite/AnnotationTests`, then from the repo root `python3 platforms/apple/check-swift-api-coverage.py`.
Expected: all pass. A build fix needed after the merge goes in a separate `fix(apple): …` commit, not an amend.

### Task 2: `PageGeometry`, rotation between page and display space

**Files:**
- Create: `platforms/apple/EmbedPDF/Sources/EmbedPDF/PageGeometry.swift`
- Create: `platforms/apple/EmbedPDF/Tests/EmbedPDFTests/PageGeometryTests.swift`
- Modify: `platforms/apple/README.md` (new `## Page rotation` section after `## Observing viewer state`)

**Interfaces:**
- Produces:
  - `public struct PageGeometry: Hashable, Sendable`
  - `public init(rotation: Int, pageSize: Size)`: `rotation` in degrees clockwise. Normalize as `r = rotation % 360`, add 360 if negative, then round to the nearest multiple of 90 (`((r + 45) / 90 % 4) * 90`). Reducing first keeps `Int.max` from overflowing. `pageSize` is the unrotated size `PageInfo` reports.
  - `public let rotation: Int`, `public let pageSize: Size`
  - `public var displaySize: Size`: width and height swapped for 90 and 270.
  - `public var renderRotation: RenderRotation`: `.none`, `.clockwise90`, `.clockwise180`, `.clockwise270`.
  - `public func toDisplay(_ point: Point) -> Point`, `public func toPage(_ point: Point) -> Point`
  - `public func toDisplay(_ rect: Rect) -> Rect`, `public func toPage(_ rect: Rect) -> Rect`: the bounding box of the four converted corners.
  - `public func toDisplay(_ image: CGImage) -> CGImage`: turns an unrotated render clockwise by `rotation`; returns the same instance for `0`.
  - `extension PageInfo { public var geometry: PageGeometry }`

Both spaces have a top-left origin, y down, in PDF points. For a page of unrotated width `w` and height `h`, `toDisplay` maps `(x, y)` to `(h - y, x)` at 90, `(w - x, h - y)` at 180 and `(y, w - x)` at 270; `toPage` is its inverse. This matches the engine's render matrix (`crates/epdf-engine/src/features/render/geometry.rs:226-233`).

- [ ] **Step 1: Write the failing tests**

```swift
extension BaseSuite {
  @Suite struct PageGeometryTests {
    let size = Size(width: 200, height: 100)

    @Test func normalizesRotationToAQuarterTurn() {
      expectNoDifference(PageGeometry(rotation: -90, pageSize: size).rotation, 270)
      expectNoDifference(PageGeometry(rotation: 450, pageSize: size).rotation, 90)
      expectNoDifference(PageGeometry(rotation: 359, pageSize: size).rotation, 0)
      _ = PageGeometry(rotation: .max, pageSize: size)
      _ = PageGeometry(rotation: .min, pageSize: size)
    }

    @Test(arguments: [0, 90, 180, 270])
    func pointsRoundTrip(rotation: Int) {
      let geometry = PageGeometry(rotation: rotation, pageSize: size)
      let point = Point(x: 30, y: 20)
      expectNoDifference(geometry.toPage(geometry.toDisplay(point)), point)
    }

    @Test func quarterTurnMapsTopLeftToTopRight() {
      let geometry = PageGeometry(rotation: 90, pageSize: size)
      expectNoDifference(geometry.displaySize, Size(width: 100, height: 200))
      expectNoDifference(geometry.toDisplay(Point(x: 0, y: 0)), Point(x: 100, y: 0))
      expectNoDifference(
        geometry.toDisplay(Rect(x: 10, y: 20, width: 30, height: 40)),
        Rect(x: 40, y: 10, width: 40, height: 30))
      expectNoDifference(geometry.renderRotation, .clockwise90)
    }

    @Test func imageTurnsClockwiseWithThePage() throws {
      // 2x1 bitmap: left pixel red, right pixel blue. A clockwise quarter
      // turn makes it 1x2 with red on top.
      let image = try #require(Self.bitmap(left: .red, right: .blue))
      #expect(PageGeometry(rotation: 0, pageSize: size).toDisplay(image) === image)
      let turned = PageGeometry(rotation: 90, pageSize: size).toDisplay(image)
      expectNoDifference([turned.width, turned.height], [1, 2])
      expectNoDifference(Self.pixel(turned, x: 0, y: 0), .red)
      expectNoDifference(Self.pixel(turned, x: 0, y: 1), .blue)
    }

    @Test func rotatedPageReportsItsTurnAndRendersToMatch() async throws {
      let viewer = try PDFViewer(ViewerConfig(render: RenderConfig(), pageEdit: true))
      try await viewer.start()
      let document = try await viewer.open(Fixture.data("report"))
      let edit = try await viewer.pageEdit(for: document)
      let before = try await viewer.pages(of: document)[0]
      _ = try await edit.setRotation(.clockwise90, of: [before.page])
      let page = try await viewer.pages(of: document)[0]
      let geometry = page.geometry
      expectNoDifference(geometry.rotation, 90)
      expectNoDifference(geometry.displaySize, Size(width: before.height, height: before.width))
      let corner = Point(x: 10, y: 20)
      expectNoDifference(geometry.toPage(geometry.toDisplay(corner)), corner)
      let render = try await viewer.render(in: document)
      // report.pdf is portrait (612 by 792); turned, it renders landscape.
      let rendered = try await render.renderPage(
        page.page, options: RenderPageOptions(rotation: geometry.renderRotation, width: 200))
      #expect(rendered.image.width > rendered.image.height)
      let thumbnail = try await render.renderThumbnail(page.page)
      #expect(thumbnail.image.height > thumbnail.image.width)
      let turned = geometry.toDisplay(thumbnail.image)
      expectNoDifference(turned.width, thumbnail.image.height)
      expectNoDifference(turned.height, thumbnail.image.width)
      try await viewer.destroy()
    }
  }
}
```

`bitmap(left:right:)` and `pixel(_:x:y:)` are private static helpers in the suite over a small `enum Pixel: Equatable { case red, blue, other }`, drawing into and reading from an RGBA8 `CGContext`. Use the real `RenderPageOptions` initializer labels and the real fixture loader name the suite's neighbors use; the assertions stay as written.

- [ ] **Step 2: Run to verify they fail**

Run: `$TEST` with `-only-testing:EmbedPDFTests/BaseSuite/PageGeometryTests`
Expected: compile failure, "cannot find 'PageGeometry' in scope".

- [ ] **Step 3: Implement `PageGeometry` in `Sources/EmbedPDF/PageGeometry.swift`**

The image turn draws into a `CGContext` of the turned size, translated to its center and rotated by `-rotation` radians (CoreGraphics is y up), using the source's color space or sRGB and `premultipliedLast`. If the context cannot be made, return the source image.

- [ ] **Step 4: Run to verify they pass**

Run: the Step 2 command.
Expected: all `PageGeometryTests` pass.

- [ ] **Step 5: Add the README section**

Under `## Page rotation`: engine geometry is unrotated page space; `PageInfo.geometry` converts to the page as shown; `renderPage` takes `geometry.renderRotation`; `renderThumbnail` output goes through `geometry.toDisplay(_:)`. One short code example.

- [ ] **Step 6: Commit**

```bash
git add platforms/apple/EmbedPDF/Sources/EmbedPDF/PageGeometry.swift \
  platforms/apple/EmbedPDF/Tests/EmbedPDFTests/PageGeometryTests.swift platforms/apple/README.md
git commit -m "feat(apple): convert points, rects and images between page and display space"
```

### Task 3: `RenderRevisions`, render invalidations as an observable

**Files:**
- Create: `platforms/apple/EmbedPDF/Sources/EmbedPDFUI/RenderRevisions.swift`
- Modify: `platforms/apple/EmbedPDF/Sources/EmbedPDFUI/StageReaderModel.swift:27, :86-106, :129-132` (replace the private dictionary and the render-event loop)
- Create: `platforms/apple/EmbedPDF/Tests/EmbedPDFUITests/RenderRevisionsTests.swift`
- Modify: `platforms/apple/README.md` (new paragraph in `## Observing viewer state`)

**Interfaces:**
- Produces:
  - `@MainActor @Observable public final class RenderRevisions`
  - `public init(render: RenderCapability)`: takes `render.events` here and stores the stream, so invalidations between `init` and the first `task()` are not missed.
  - `public func task() async`: the first run consumes the stored stream (`let events = stored ?? render.events; stored = nil`); every later run subscribes fresh, so the model survives SwiftUI cancelling and restarting `.task` on disappear and appear. It runs until the stream finishes (the document closed) or the task is cancelled; each `.invalidated(pages, _)` adds 1 (`&+=`) to each page's revision. Concurrent runs are not supported and are documented as such. `task()` takes its stream before its first suspension point.
  - `public func revision(for page: PageRef) -> UInt64`: `0` for a page never invalidated.
- `StageReaderModel` gains `public let revisions: RenderRevisions`, created in its `init`. `renderRevision(for:)` stays and returns `revisions.revision(for:)`. Its task group runs `await self.revisions.task()` in place of its own render-event loop.

- [ ] **Step 1: Write the failing tests**

```swift
@MainActor
@Suite(.serialized, .timeLimit(.minutes(1)))
struct RenderRevisionsTests {
  func opened() async throws -> (PDFViewer, DocumentInfo, [PageInfo], RenderCapability) {
    let viewer = try PDFViewer(ViewerConfig(render: RenderConfig()))
    try await viewer.start()
    let document = try await viewer.open(pluginFixture())
    return (viewer, document, try await viewer.pages(of: document), try await viewer.render(in: document))
  }

  @Test func invalidatingAPageBumpsOnlyThatPage() async throws {
    let (viewer, document, pages, render) = try await opened()
    let revisions = RenderRevisions(render: render)
    let observing = Task { await revisions.task() }
    try await expectObserved({ revisions.revision(for: pages[0].page) }, equals: 1) {
      try await render.invalidate([pages[0].page], scope: .annotations)
    }
    expectNoDifference(revisions.revision(for: pages[1].page), 0)
    try await viewer.close(document)
    await observing.value
    try await viewer.destroy()
  }

  @Test func anInvalidationBeforeTheTaskStartsIsStillCounted() async throws {
    let (viewer, _, pages, render) = try await opened()
    let revisions = RenderRevisions(render: render)
    try await render.invalidate([pages[0].page], scope: .annotations)
    let observing = Task { await revisions.task() }
    try await expectObserved({ revisions.revision(for: pages[0].page) }, equals: 1) {}
    try await viewer.destroy()
    await observing.value
  }

  @Test func aRestartedTaskKeepsCounting() async throws {
    let (viewer, _, pages, render) = try await opened()
    let revisions = RenderRevisions(render: render)
    let first = Task { await revisions.task() }
    first.cancel()
    await first.value
    let second = Task { await revisions.task() }
    // The main executor runs `second` before the test resumes; task() takes
    // its stream before its first suspension.
    await Task.yield()
    try await expectObserved({ revisions.revision(for: pages[0].page) }, equals: 1) {
      try await render.invalidate([pages[0].page], scope: .annotations)
    }
    try await viewer.destroy()
    await second.value
  }
}
```

`await observing.value` returning after `close` is the close-ends-the-task assertion.

- [ ] **Step 2: Run to verify they fail**

Run: `$TEST` with `-only-testing:EmbedPDFUITests/RenderRevisionsTests`
Expected: compile failure, "cannot find 'RenderRevisions' in scope".

- [ ] **Step 3: Implement `RenderRevisions` and switch `StageReaderModel` to it**

- [ ] **Step 4: Run the new suite and the stage reader suites**

Run: `$TEST` with `-only-testing:EmbedPDFUITests/RenderRevisionsTests -only-testing:EmbedPDFUITests/StageReaderModelTests -only-testing:EmbedPDFUITests/StageReaderDragLagTests`
Expected: all pass, including `rustInvalidationRefreshesOnlyItsPagesWithoutMovingTheCamera`, which now observes through the nested model.

- [ ] **Step 5: README paragraph and commit**

```bash
git add platforms/apple/EmbedPDF/Sources/EmbedPDFUI/RenderRevisions.swift \
  platforms/apple/EmbedPDF/Sources/EmbedPDFUI/StageReaderModel.swift \
  platforms/apple/EmbedPDF/Tests/EmbedPDFUITests/RenderRevisionsTests.swift platforms/apple/README.md
git commit -m "feat(apple): observe render invalidations per page outside the stage reader"
```

### Task 4: `TextSelectionModel`, ordered and coalesced drag selection

**Files:**
- Create: `platforms/apple/EmbedPDF/Sources/EmbedPDFUI/TextSelectionModel.swift`
- Create: `platforms/apple/EmbedPDF/Tests/EmbedPDFUITests/TextSelectionModelTests.swift`
- Modify: `platforms/apple/README.md` (new `## Text selection` section)

**Interfaces:**
- Consumes: `SelectionCapability` (`Sources/EmbedPDF/Plugins/Selection.swift`): `ensureLoaded(_:)`, `isLoaded(_:)`, `beginGesture()`, `beginGesture(at:on:)`, `selectWord(at:on:)`, `extend(to:on:)`, `endGesture()`, `selectedPages()`, `rects(on:)`, `clear()`, `events`.
- Produces:
  - `@MainActor @Observable public final class TextSelectionModel`
  - `public enum Start: Sendable { case caret, word }`
  - `public private(set) var rects: [PageRef: [Rect]]`: rects per selected page, unrotated page points.
  - `public private(set) var isSelecting: Bool`
  - `public private(set) var error: PDFError?`
  - `public init(selection: SelectionCapability)`: takes `selection.events` here and stores it.
  - `public func task() async`: the first run consumes the stored events, later runs subscribe fresh (same rule as `RenderRevisions.task()`); on `.changed` or `.committed` it refreshes `rects`, on `.cleared` it empties them. Returns when the stream finishes (the document closed). This keeps `rects` in step with selections made elsewhere (`selectAll`, `select(hit)`, `clear()`).
  - `public func dragBegan(at point: Point, on page: PageRef, start: Start = .caret) async -> Bool`: `.caret` runs `ensureLoaded(page)` then `beginGesture(at:on:)`. `.word` runs `ensureLoaded(page)`, then `selectWord(at:on:)`, and only on `true` calls `beginGesture()`, so a miss emits nothing. On `true` it sets `isSelecting = true` and refreshes `rects`. On `false` the model stays idle.
  - `public func dragChanged(to point: Point, on page: PageRef)`: synchronous, so a SwiftUI `onChanged` calls it directly. It stores `(point, page)` as the pending point and, if no extension is already queued, enqueues one. The queued extension runs after every earlier call, takes the newest pending point, runs `ensureLoaded` when `isLoaded` is false for that page, calls `extend(to:on:)`, and refreshes `rects`. If it runs while `isSelecting` is false it leaves the pending point for the next begin to apply.
  - `public func dragEnded() async`: runs after every earlier call; calls `endGesture()` if selecting, sets `isSelecting = false`, drops any pending point. Leaves the selection and `rects` in place.
  - `public func clear() async`: runs after every earlier call; calls `selection.clear()` and empties `rects`.
  - Internal test seam: `var extensionCount = 0`, incremented once per `extend(to:on:)` call.

Every call goes through one ordering chain with a synchronous enqueue, the same shape as Task 5's `enqueueDragCall`: read the last `Task<Void, Never>`, create the next one awaiting it, store it before returning. The async calls await the Task they enqueued. A begin that succeeds with a pending point already stored applies that extension inline, inside its own slot in the chain, before the slot ends, so an end queued behind the begin runs after it. Refreshing `rects` reads `selectedPages()` then `rects(on:)` for each. Errors from engine calls land in `error`; nothing throws.

- [ ] **Step 1: Write the failing tests**

```swift
@MainActor
@Suite(.serialized, .timeLimit(.minutes(1)))
struct TextSelectionModelTests {
  @Test func draggingAcrossTextSelectsIt() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    let start = try await Self.pointOverText(page, selection)
    #expect(await model.dragBegan(at: start, on: page.page))
    #expect(model.isSelecting)
    model.dragChanged(to: Point(x: start.x + 120, y: start.y), on: page.page)
    await model.dragEnded()
    #expect(!model.isSelecting)
    let engineRects = try await selection.rects(on: page.page)
    #expect(!engineRects.isEmpty)
    expectNoDifference(model.rects[page.page], engineRects)
    try await viewer.destroy()
  }

  @Test func pointsQueuedBehindOneExtensionCollapseIntoIt() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    let start = try await Self.pointOverText(page, selection)
    #expect(await model.dragBegan(at: start, on: page.page))
    let end = Point(x: start.x + 160, y: start.y)
    model.dragChanged(to: Point(x: start.x + 20, y: start.y), on: page.page)
    model.dragChanged(to: Point(x: start.x + 40, y: start.y), on: page.page)
    model.dragChanged(to: end, on: page.page)
    await model.dragEnded()
    expectNoDifference(model.extensionCount, 1)
    let collapsed = model.rects[page.page]
    let reference = try await Self.selectionRects(dragging: start, to: end, page, selection)
    expectNoDifference(collapsed, reference)
    try await viewer.destroy()
  }

  @Test func changesAndEndsThatArriveDuringTheBeginRunAfterIt() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    let start = try await Self.pointOverText(page, selection)
    let begin = Task { await model.dragBegan(at: start, on: page.page) }
    model.dragChanged(to: Point(x: start.x + 120, y: start.y), on: page.page)
    let end = Task { await model.dragEnded() }
    #expect(await begin.value)
    await end.value
    #expect(!model.isSelecting)
    #expect(!(try await selection.isSelecting()))
    expectNoDifference(model.extensionCount, 1)
    try await viewer.destroy()
  }

  @Test func aDragThatStartsOffTextSelectsNothing() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    let margin = try await Self.pointOffText(page, selection)
    #expect(!(await model.dragBegan(at: margin, on: page.page)))
    model.dragChanged(to: Point(x: 300, y: 300), on: page.page)
    await model.dragEnded()
    expectNoDifference(model.rects, [:])
    expectNoDifference(model.extensionCount, 0)
    #expect(!model.isSelecting)
    try await viewer.destroy()
  }

  @Test func selectionsMadeElsewhereReachTheModelUntilClose() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    let observing = Task { await model.task() }
    try await expectObserved({ model.rects[page.page]?.isEmpty == false }, equals: true) {
      try await selection.ensureLoaded(page.page)
      try await selection.selectPage(page.page)
    }
    try await expectObserved({ model.rects.isEmpty }, equals: true) {
      await model.clear()
    }
    #expect(!(try await selection.hasSelection()))
    try await viewer.destroy()
    await observing.value
  }
}
```

Helpers, all `static` in the suite:
- `opened()` opens `pluginFixture()` with `ViewerConfig(interaction: true, selection: true)` and returns `(viewer, firstPage, selection, TextSelectionModel(selection:))`.
- `pointOverText(_:_:)` runs `ensureLoaded`, `selectPage`, takes the center of the first rect from `rects(on:)`, then `clear()`.
- `pointOffText(_:_:)` runs `ensureLoaded` and returns a point inside the page but outside every rect `selectPage` produces, checked with `isOverText(_:on:)`; then `clear()`.
- `selectionRects(dragging:to:_:_:)` replays the drag on the same capability after `clear()`: `ensureLoaded`, `beginGesture(at:on:)`, `extend(to:on:)`, `endGesture()`, `rects(on:)`.

- [ ] **Step 2: Run to verify they fail**

Run: `$TEST` with `-only-testing:EmbedPDFUITests/TextSelectionModelTests`
Expected: compile failure, "cannot find 'TextSelectionModel' in scope".

- [ ] **Step 3: Implement `TextSelectionModel`**

- [ ] **Step 4: Run to verify they pass**

Run: the Step 2 command.
Expected: all five pass.

- [ ] **Step 5: README section and commit**

The README section shows the three gesture callbacks wired to a `DragGesture`, says the rects are unrotated page points, and points at `PageInfo.geometry` for the page as shown.

```bash
git add platforms/apple/EmbedPDF/Sources/EmbedPDFUI/TextSelectionModel.swift \
  platforms/apple/EmbedPDF/Tests/EmbedPDFUITests/TextSelectionModelTests.swift platforms/apple/README.md
git commit -m "feat(apple): add TextSelectionModel for ordered, coalesced drag selection"
```

### Task 5: Page overlays on `StageReaderView`

**Files:**
- Modify: `platforms/apple/EmbedPDF/Sources/EmbedPDFUI/StageReaderView.swift`
- Modify: `platforms/apple/EmbedPDF/Sources/EmbedPDFUI/StageReaderModel.swift` (add `isPanEnabled`)
- Create: `platforms/apple/EmbedPDF/Sources/EmbedPDFUI/StageVisiblePage+Viewport.swift`
- Modify: `platforms/apple/EmbedPDF/Tests/EmbedPDFUITests/StageReaderModelTests.swift` (add `import SwiftUI`)
- Modify: `platforms/apple/README.md` (a `## Page overlays` section next to the stage reader's existing mention)

**Interfaces:**
- Produces:
  - `public struct StageReaderView<PageOverlay: View>: View`
  - `public init(model: StageReaderModel, @ViewBuilder pageOverlay: @escaping (StageVisiblePage) -> PageOverlay)`: the overlay is laid out in each visible page's frame (`transform.viewWidth` by `transform.viewHeight`, offset by `screenX`, `screenY`), above the page image, and receives that page.
  - `extension StageReaderView where PageOverlay == EmptyView { public init(model: StageReaderModel) }`. Every call `StageReaderView(model:)` keeps compiling; a declaration typed as bare `StageReaderView` would not, and no code in the repository has one (the demo's `DocumentView.swift:97` and the README only call the initializer).
  - `StageReaderModel.isPanEnabled: Bool`, public, default `true`. `dragChanged` still runs through `afterEarlierDragCalls`, and while `isPanEnabled` is `false` its slot does nothing to the stage. Setting it to `false` queues an end for any open stage gesture through a new synchronous `private func enqueueDragCall(_ work: @escaping @MainActor @Sendable (StageReaderModel) async -> Void) -> Task<Void, Never>`, which reads `lastDragCall`, creates the chained Task and stores it before returning; `afterEarlierDragCalls` becomes `await enqueueDragCall(work).value`. A `didSet` wrapping `Task { await afterEarlierDragCalls … }` would join the chain too late and is wrong. The queued end calls `stage.endGesture()` with no fling, so a drag cut off mid-way never leaves the stage gesture open. The view applies the drag with `.gesture(drag, including: model.isPanEnabled ? .all : .subviews)`, which builds on every SDK the iOS 17 floor allows.
  - `extension StageVisiblePage { public func viewportPoint(_ local: CGPoint) -> Point }`: `Point(x: screenX + local.x, y: screenY + local.y)`, the viewport point `StageCapability.viewportToPage(_:_:)` takes.

- [ ] **Step 1: Write the failing tests in `StageReaderModelTests`**

```swift
@Test func disablingPanLeavesTheCameraAlone() async throws {
  let (model, task) = try await model()
  defer { task.cancel() }
  await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 3)
  let before = try await model.stage.camera()
  model.isPanEnabled = false
  await model.dragChanged(translation: CGSize(width: 0, height: -200))
  await model.dragEnded(velocity: .zero)
  expectNoDifference(try await model.stage.camera(), before)
}

@Test func disablingPanMidDragClosesTheStageGesture() async throws {
  let (model, task) = try await model()
  defer { task.cancel() }
  await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 3)
  await model.dragChanged(translation: CGSize(width: 0, height: -50))
  #expect(model.gestureOpen)
  // SwiftUI cancels a disabled gesture without calling onEnded, so no
  // dragEnded arrives. A no-op chained call waits for the queued end.
  model.isPanEnabled = false
  await model.dragChanged(translation: .zero)
  #expect(!model.gestureOpen)
}

@Test func overlayOriginIsThePageOrigin() async throws {
  let (model, task) = try await model()
  defer { task.cancel() }
  await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 3)
  try await expectObserved({ !model.visiblePages.isEmpty }, equals: true) {}
  let page = try #require(model.visiblePages.first)
  let origin = try #require(try await model.stage.pageToViewport(page.page, Point(x: 0, y: 0)))
  let local = page.viewportPoint(.zero)
  #expect(abs(origin.x - local.x) <= 1.0 / 3 && abs(origin.y - local.y) <= 1.0 / 3)
}

@Test func bothInitializersBuild() {
  let model: StageReaderModel? = nil
  if let model {
    _ = StageReaderView(model: model)
    _ = StageReaderView(model: model) { page in Text("\(page.pageIndex)") }
  }
}
```

`StageCapability` has no read for an open gesture, so the test reads the model's own flag: `StageReaderModel.gestureOpen` (`Sources/EmbedPDFUI/StageReaderModel.swift:31`) goes from `private` to `private(set)` internal, visible through `@testable import`.

- [ ] **Step 2: Run to verify they fail**

Run: `$TEST` with `-only-testing:EmbedPDFUITests/StageReaderModelTests`
Expected: compile failure on `isPanEnabled`, `viewportPoint` and the trailing-closure initializer.

- [ ] **Step 3: Implement the generic view, `isPanEnabled` and `viewportPoint`**

The overlay goes inside the same `ForEach` as `PageImageView`, in a `ZStack` with it, so both share the frame and offset.

- [ ] **Step 4: Run the stage reader suites**

Run: `$TEST` with `-only-testing:EmbedPDFUITests/StageReaderModelTests -only-testing:EmbedPDFUITests/StageReaderDragLagTests`
Expected: all pass.

- [ ] **Step 5: README and commit**

The README says SwiftUI already gives an overlay's own gestures priority over the reader's drag; `isPanEnabled` is for overlays that need every touch (a drawing tool, a UIKit recognizer). Pinch and double tap stay active alongside overlay gestures.

```bash
git add platforms/apple/EmbedPDF/Sources/EmbedPDFUI/StageReaderView.swift \
  platforms/apple/EmbedPDF/Sources/EmbedPDFUI/StageReaderModel.swift \
  platforms/apple/EmbedPDF/Sources/EmbedPDFUI/StageVisiblePage+Viewport.swift \
  platforms/apple/EmbedPDF/Tests/EmbedPDFUITests/StageReaderModelTests.swift platforms/apple/README.md
git commit -m "feat(apple): lay app overlays over stage reader pages and let them take the drag"
```

### Task 6: Whole-package verification

- [ ] **Step 1: Full test run**

Run: `xcodebuild test -scheme EmbedPDF-Package -destination 'platform=iOS Simulator,id=5FA32A5D-6186-4C16-BE25-C87829DD1BFA'` in `platforms/apple/EmbedPDF`.
Expected: every suite passes. A failure in a suite these tasks did not touch is checked against the same run on `f329b82e1` before it is called a regression.

- [ ] **Step 2: macOS build and coverage checker**

Run: `swift build` in `platforms/apple/EmbedPDF`; from the repo root `python3 platforms/apple/check-swift-api-coverage.py` and `PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s platforms/apple/tests -p test_swift_api_coverage.py`.
Expected: build succeeds with no new warnings; checker and its tests pass.

- [ ] **Step 3: Neutral wording check**

Run: `git diff f329b82e1..HEAD` and read the added lines for the name of any outside product, company or app, and for session ids.
Expected: none in lines this range adds.
