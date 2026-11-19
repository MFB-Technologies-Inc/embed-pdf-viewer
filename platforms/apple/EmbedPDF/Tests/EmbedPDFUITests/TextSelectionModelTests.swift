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

  @Test func aChangeQueuedBehindTheBeginAppliesOnce() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    let start = try await Self.pointOverText(page, selection)
    let begin = Task { await model.dragBegan(at: start, on: page.page) }
    // Lets the begin enqueue first. A change queued behind it applies exactly
    // once, and the end still closes the engine gesture.
    await Task.yield()
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

  @Test func aSelectionClearedElsewhereEmptiesTheRects() async throws {
    let (viewer, page, selection, model) = try await Self.opened()
    let observing = Task { await model.task() }
    try await expectObserved({ model.rects[page.page]?.isEmpty == false }, equals: true) {
      try await selection.ensureLoaded(page.page)
      try await selection.selectPage(page.page)
    }
    try await expectObserved({ model.rects.isEmpty }, equals: true) {
      try await selection.clear()
    }
    try await viewer.destroy()
    await observing.value
  }

  @Test func closingTheDocumentEndsTheTask() async throws {
    let (viewer, document) = try await Self.openedDocument()
    let model = TextSelectionModel(selection: try await viewer.selection(for: document))
    let observing = Task { await model.task() }
    try await viewer.close(document)
    await observing.value
    try await viewer.destroy()
  }

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
