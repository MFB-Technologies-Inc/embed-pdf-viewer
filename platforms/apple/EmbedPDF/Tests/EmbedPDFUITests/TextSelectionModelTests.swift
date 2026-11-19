import CustomDump
import EmbedPDF
@testable import EmbedPDFUI
import Testing

@MainActor
@Suite(.serialized, .timeLimit(.minutes(1)))
struct TextSelectionModelTests {
  @Test func draggingAcrossTextSelectsIt() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    let start = try await Self.pointOverText(page, selection)
    try await expectObserved({ model.isSelecting }, equals: true) {
      model.dragBegan(at: start, on: page.page)
    }
    model.dragChanged(to: Point(x: start.x + 120, y: start.y), on: page.page)
    #expect(await model.dragEnded())
    #expect(!model.isSelecting)
    let engineRects = try await selection.rects(on: page.page)
    #expect(!engineRects.isEmpty)
    expectNoDifference(model.rects[page.page], engineRects)
    try await viewer.destroy()
  }

  @Test func pointsQueuedBehindOneExtensionCollapseIntoIt() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    let start = try await Self.pointOverText(page, selection)
    try await expectObserved({ model.isSelecting }, equals: true) {
      model.dragBegan(at: start, on: page.page)
    }
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

  @Test func changesAndEndsMadeBeforeTheBeginLandsRunAfterIt() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    let start = try await Self.pointOverText(page, selection)
    // No suspension between the three calls, so the begin has not reached
    // the engine when the change and the end are made.
    model.dragBegan(at: start, on: page.page)
    model.dragChanged(to: Point(x: start.x + 120, y: start.y), on: page.page)
    #expect(await model.dragEnded())
    #expect(!model.isSelecting)
    #expect(!(try await selection.isSelecting()))
    expectNoDifference(model.extensionCount, 1)
    try await viewer.destroy()
  }

  @Test func aDragThatStartsOffTextSelectsNothing() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    let margin = try await Self.pointOffText(page, selection)
    model.dragBegan(at: margin, on: page.page)
    model.dragChanged(to: Point(x: 300, y: 300), on: page.page)
    await model.dragEnded()
    expectNoDifference(model.rects, [:])
    expectNoDifference(model.extensionCount, 0)
    #expect(!model.isSelecting)
    try await viewer.destroy()
  }

  @Test func dragEndedReportsWhetherTextWasUnderTheStart() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    let margin = try await Self.pointOffText(page, selection)
    let text = try await Self.pointOverText(page, selection)
    model.dragBegan(at: margin, on: page.page)
    #expect(!(await model.dragEnded()))
    model.dragBegan(at: text, on: page.page)
    #expect(await model.dragEnded())
    // An end with no begin before it closes nothing.
    #expect(!(await model.dragEnded()))
    try await viewer.destroy()
  }

  @Test func rectsAreFinalAfterTheEndEvenWhenAnEventRefreshInterleaves() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    let start = try await Self.pointOverText(page, selection)
    let (reached, release) = Self.holdingFirstRectsRead(of: model)
    model.dragBegan(at: start, on: page.page)
    // The begin's refresh has read the caret's rects and not applied them.
    for await _ in reached { break }
    try await selection.selectAll()
    release()
    await model.dragEnded()
    let pages = try await selection.selectedPages()
    #expect(!pages.isEmpty)
    var engineRects: [PageRef: [Rect]] = [:]
    for page in pages {
      engineRects[page] = try await selection.rects(on: page)
    }
    expectNoDifference(model.rects, engineRects)
    try await viewer.destroy()
  }

  @Test func aNewDragClearsTheLastError() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    let start = try await Self.pointOverText(page, selection)
    // No page of the document has this object number, so selecting the word
    // throws.
    model.dragBegan(at: start, on: Self.missingPage, start: .word)
    await model.dragEnded()
    #expect(model.error != nil)
    model.dragBegan(at: start, on: page.page)
    #expect(await model.dragEnded())
    expectNoDifference(model.error, nil)
    try await viewer.destroy()
  }

  @Test func clearingClearsTheLastError() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    let start = try await Self.pointOverText(page, selection)
    model.dragBegan(at: start, on: Self.missingPage, start: .word)
    await model.dragEnded()
    #expect(model.error != nil)
    await model.clear()
    expectNoDifference(model.error, nil)
    try await viewer.destroy()
  }

  @Test func selectionsMadeElsewhereReachTheModelUntilClose() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    try await expectObserved({ model.rects[page.page]?.isEmpty == false }, equals: true) {
      try await selection.ensureLoaded(page.page)
      try await selection.selectPage(page.page)
    }
    try await expectObserved({ model.rects.isEmpty }, equals: true) {
      await model.clear()
    }
    #expect(!(try await selection.hasSelection()))
    try await viewer.destroy()
    // The destroy finishes the stream, which ends the observing task.
    await model.observationTask?.value
  }

  @Test func aSelectionMadeRightAfterInitReachesTheModel() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    // Sent before the model's own observation has had a turn to run.
    try await selection.ensureLoaded(page.page)
    try await selection.selectPage(page.page)
    try await expectObserved({ model.rects[page.page]?.isEmpty == false }, equals: true) {}
    try await viewer.destroy()
  }

  @Test func aSelectionClearedElsewhereEmptiesTheRects() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    try await expectObserved({ model.rects[page.page]?.isEmpty == false }, equals: true) {
      try await selection.ensureLoaded(page.page)
      try await selection.selectPage(page.page)
    }
    try await expectObserved({ model.rects.isEmpty }, equals: true) {
      try await selection.clear()
    }
    try await viewer.destroy()
  }

  @Test func closingTheDocumentEndsTheObservation() async throws {
    let (viewer, document) = try await Self.openedDocument()
    let model = TextSelectionModel(selection: try await viewer.selection(for: document))
    try await viewer.close(document)
    let task = try #require(model.observationTask)
    await task.value
    try await viewer.destroy()
  }

  @Test func aReleasedModelStopsObservingWhileTheDocumentStaysOpen() async throws {
    let (viewer, document) = try await Self.openedDocument()
    let page = try #require(try await viewer.pages(of: document).first)
    let selection = try await viewer.selection(for: document)
    weak var released: TextSelectionModel?
    let observation: Task<Void, Never>?
    do {
      let model = TextSelectionModel(selection: selection)
      released = model
      observation = model.observationTask
      // The model is observing by the time it is released.
      try await expectObserved({ model.rects[page.page]?.isEmpty == false }, equals: true) {
        try await selection.ensureLoaded(page.page)
        try await selection.selectPage(page.page)
      }
    }
    // The document is still open, so the observing task ends only if the
    // release cancelled it. A refresh still in the chain holds the model
    // until it returns, and the release follows it.
    let task = try #require(observation)
    await task.value
    #expect(released == nil)
    try await viewer.destroy()
  }

  /// Holds the first rects refresh on `model` between its reads and its
  /// write until `release` runs. `reached` yields once the refresh is held.
  static func holdingFirstRectsRead(
    of model: TextSelectionModel
  ) -> (reached: AsyncStream<Void>, release: @MainActor () -> Void) {
    let (reached, reach) = AsyncStream<Void>.makeStream()
    let (gate, open) = AsyncStream<Void>.makeStream(bufferingPolicy: .bufferingNewest(1))
    var held = false
    model.rectsReadReturned = {
      guard !held else { return }
      held = true
      reach.yield(())
      for await _ in gate { break }
    }
    return (reached, {
      open.yield(())
      open.finish()
    })
  }

  static let missingPage = PageRef(pageObjectNumber: .max)

  static func openedDocument() async throws -> (PDFViewer, DocumentInfo) {
    let viewer = try PDFViewer(ViewerConfig(interaction: true, selection: true))
    try await viewer.start()
    return (viewer, try await viewer.open(pluginFixture()))
  }

  static func opened() async throws -> (PDFViewer, PageInfo, SelectionCapability, TextSelectionModel) {
    let (viewer, document) = try await openedDocument()
    let page = try #require(try await viewer.pages(of: document).first)
    let selection = try await viewer.selection(for: document)
    return (viewer, page, selection, TextSelectionModel(selection: selection))
  }

  static func pointOverText(_ page: PageInfo, _ selection: SelectionCapability) async throws -> Point {
    try await selection.ensureLoaded(page.page)
    try await selection.selectPage(page.page)
    let first = try #require(try await selection.rects(on: page.page).first)
    try await selection.clear()
    return Point(x: first.x + first.width / 2, y: first.y + first.height / 2)
  }

  static func pointOffText(_ page: PageInfo, _ selection: SelectionCapability) async throws -> Point {
    try await selection.ensureLoaded(page.page)
    try await selection.selectPage(page.page)
    let rects = try await selection.rects(on: page.page)
    try await selection.clear()
    let candidates = [Point(x: 2, y: 2), Point(x: page.width - 2, y: 2),
                      Point(x: 2, y: page.height - 2), Point(x: page.width - 2, y: page.height - 2)]
    for point in candidates {
      let inRect = rects.contains {
        point.x >= $0.x && point.x <= $0.x + $0.width
          && point.y >= $0.y && point.y <= $0.y + $0.height
      }
      if !inRect, !(try await selection.isOverText(point, on: page.page)) { return point }
    }
    Issue.record("no point off text on the page")
    throw CancellationError()
  }

  static func selectionRects(
    dragging start: Point, to end: Point, _ page: PageInfo, _ selection: SelectionCapability
  ) async throws -> [Rect] {
    try await selection.clear()
    try await selection.ensureLoaded(page.page)
    #expect(try await selection.beginGesture(at: start, on: page.page))
    try await selection.extend(to: end, on: page.page)
    try await selection.endGesture()
    return try await selection.rects(on: page.page)
  }
}
