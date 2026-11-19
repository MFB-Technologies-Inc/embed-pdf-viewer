import CoreGraphics
import CustomDump
import EmbedPDF
@testable import EmbedPDFModels
import Testing

@MainActor
@Suite(.serialized, .timeLimit(.minutes(1)))
struct StagePointerForwarderTests {
  struct Reader {
    let viewer: PDFViewer
    let document: DocumentInfo
    let model: StageReaderModel
    let stage: StageCapability
    let interaction: InteractionCapability
    let annotations: AnnotationCapability
    let link: LinkCapability
    let observing: Task<Void, Never>
  }

  /// A sized reader over `fixture` with its `task()` running and the tool
  /// state read once.
  func reader(_ fixture: String = "annotation_kinds") async throws -> Reader {
    let viewer = try PDFViewer(
      ViewerConfig(
        render: RenderConfig(), interaction: true, stage: StageConfig(), link: true, annotation: ""))
    try await viewer.start()
    let document = try await viewer.open(pluginFixture(fixture))
    let stage = try await viewer.stage(for: document)
    let interaction = try await viewer.interaction(for: document)
    let link = try await viewer.link(for: document)
    let model = StageReaderModel(
      stage: stage, render: try await viewer.render(for: document), interaction: interaction, link: link)
    let observing = Task { await model.task() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 2)
    try await expectObserved({ model.activeToolID != nil }, equals: true) {}
    let annotations = try await viewer.annotations(for: document)
    // The fixture's own annotations are read before any test counts them.
    try await annotations.refresh()
    return Reader(
      viewer: viewer, document: document, model: model, stage: stage, interaction: interaction,
      annotations: annotations, link: link, observing: observing)
  }

  /// A viewport point inside the first visible page, `fraction` of the way
  /// across and down it.
  func point(in model: StageReaderModel, _ fraction: Double) throws -> CGPoint {
    let page = try #require(model.visiblePages.first)
    return CGPoint(
      x: page.screenX + page.transform.viewWidth * fraction,
      y: page.screenY + page.transform.viewHeight * fraction)
  }

  func inkCount(_ annotations: AnnotationCapability) async throws -> Int {
    let all: [AnnotationInfo] = try await annotations.list()
    return all.filter { $0.subtype == .ink }.count
  }

  @Test func aForwardedDragUnderTheInkToolCreatesInk() async throws {
    let reader = try await reader()
    let model = reader.model
    let before = try await inkCount(reader.annotations)
    try await expectObserved({ model.forwardsDrags }, equals: true) {
      try await reader.interaction.activateTool(.ink)
    }
    expectNoDifference(model.activeToolID, .ink)
    model.pointerDown(at: try point(in: model, 0.30))
    for fraction in [0.35, 0.40, 0.45] {
      model.pointerMoved(to: try point(in: model, fraction))
      await model.pointerCallsFinished()
    }
    model.pointerUp(at: try point(in: model, 0.45))
    await model.pointerCallsFinished()
    try await reader.annotations.drafts.finishInkDraft()
    let after = try await inkCount(reader.annotations)
    expectNoDifference(after, before + 1)
    expectNoDifference(model.error, nil)
    try await reader.viewer.destroy()
    await reader.observing.value
  }

  @Test func aDragUnderTheDefaultToolIsNotForwarded() async throws {
    let reader = try await reader()
    let fallback = try await reader.interaction.defaultToolID()
    expectNoDifference(reader.model.activeToolID, fallback)
    expectNoDifference(reader.model.forwardsDrags, false)
    try await reader.viewer.destroy()
    await reader.observing.value
  }

  /// Without an interaction hub the reader behaves as it did before it
  /// forwarded: no tool, no forwarding, and the pointer calls send nothing.
  @Test func aReaderWithoutAHubForwardsNothing() async throws {
    let viewer = try PDFViewer(ViewerConfig(render: RenderConfig(), stage: StageConfig()))
    try await viewer.start()
    let document = try await viewer.open(pluginFixture("annotation_kinds"))
    let model = StageReaderModel(
      stage: try await viewer.stage(for: document), render: try await viewer.render(for: document))
    let observing = Task { await model.task() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 2)
    var sent = 0
    model.pointerSampleDispatched = { _ in sent += 1 }
    model.pointerDown(at: try point(in: model, 0.3))
    model.pointerMoved(to: try point(in: model, 0.4))
    model.pointerUp(at: try point(in: model, 0.4))
    model.tapped(at: try point(in: model, 0.4))
    model.pointerCancelled()
    await model.pointerCallsFinished()
    expectNoDifference(sent, 0)
    expectNoDifference(model.activeToolID, nil)
    expectNoDifference(model.forwardsDrags, false)
    expectNoDifference(model.error, nil)
    try await viewer.destroy()
    await observing.value
  }

  @Test func aCancelledDragLeavesNoGestureOpen() async throws {
    let reader = try await reader()
    let model = reader.model
    let before = try await inkCount(reader.annotations)
    try await expectObserved({ model.forwardsDrags }, equals: true) {
      try await reader.interaction.activateTool(.ink)
    }
    var phases: [PointerPhase] = []
    model.pointerSampleDispatched = { phases.append($0.phase) }
    model.pointerDown(at: try point(in: model, 0.30))
    model.pointerMoved(to: try point(in: model, 0.40))
    await model.pointerCallsFinished()
    model.pointerCancelled()
    await model.pointerCallsFinished()
    expectNoDifference(phases, [.down, .move, .cancel])
    try await reader.annotations.drafts.finishInkDraft()
    let cancelled = try await inkCount(reader.annotations)
    expectNoDifference(cancelled, before)

    expectNoDifference(model.activeToolID, .ink)
    phases = []
    model.pointerDown(at: try point(in: model, 0.50))
    model.pointerMoved(to: try point(in: model, 0.60))
    model.pointerUp(at: try point(in: model, 0.60))
    await model.pointerCallsFinished()
    expectNoDifference(phases, [.down, .move, .up])
    try await reader.annotations.drafts.finishInkDraft()
    let after = try await inkCount(reader.annotations)
    expectNoDifference(after, before + 1)
    try await reader.viewer.destroy()
    await reader.observing.value
  }

  @Test func movesBehindOneInFlightCollapse() async throws {
    let reader = try await reader()
    let model = reader.model
    try await reader.interaction.activateTool(.ink)
    var samples: [PointerSample] = []
    model.pointerSampleDispatched = { samples.append($0) }
    let start = try point(in: model, 0.2)
    model.pointerDown(at: start)
    var last = start
    for step in 1...50 {
      last = CGPoint(x: start.x + Double(step), y: start.y + Double(step))
      model.pointerMoved(to: last)
    }
    await model.pointerCallsFinished()
    let moves = samples.filter { $0.phase == .move }
    #expect(!moves.isEmpty)
    #expect(moves.count < 50)
    expectNoDifference(samples.first?.phase, .down)
    expectNoDifference(samples.last?.viewport, Point(x: last.x, y: last.y))
    expectNoDifference(samples.last?.pointerType, .touch)
    #expect(samples.allSatisfy { $0.page != nil })
    model.pointerUp(at: last)
    await model.pointerCallsFinished()
    expectNoDifference(samples.last?.phase, .up)
    try await reader.viewer.destroy()
    await reader.observing.value
  }

  /// One sample as a test compares it: its phase and where it was.
  struct Sent: Equatable {
    var phase: PointerPhase
    var x: Double
    var y: Double

    init(_ phase: PointerPhase, _ location: CGPoint) {
      self.phase = phase
      x = location.x
      y = location.y
    }

    init(_ sample: PointerSample) {
      phase = sample.phase
      x = sample.viewport.x
      y = sample.viewport.y
    }
  }

  /// A reader under the ink tool that records every sample it sends.
  func recording() async throws -> (Reader, () -> [Sent]) {
    let reader = try await reader()
    try await reader.interaction.activateTool(.ink)
    var sent: [Sent] = []
    reader.model.pointerSampleDispatched = { sent.append(Sent($0)) }
    return (reader, { sent })
  }

  @Test func gesturesSentInOneTurnKeepTheirOwnMoves() async throws {
    let (reader, sent) = try await recording()
    let model = reader.model
    let at = try (1...6).map { try point(in: model, 0.1 * Double($0)) }
    model.pointerDown(at: at[0])
    model.pointerMoved(to: at[1])
    model.pointerUp(at: at[2])
    model.pointerDown(at: at[3])
    model.pointerMoved(to: at[4])
    model.pointerUp(at: at[5])
    await model.pointerCallsFinished()
    expectNoDifference(
      sent(),
      [
        Sent(.down, at[0]), Sent(.move, at[1]), Sent(.up, at[2]),
        Sent(.down, at[3]), Sent(.move, at[4]), Sent(.up, at[5]),
      ])
    try await reader.viewer.destroy()
    await reader.observing.value
  }

  /// A down right after an up leaves the earlier gesture's waiting move in
  /// place, and a tap does too.
  @Test func aNewGestureDoesNotDropTheLastOnesWaitingMove() async throws {
    let (reader, sent) = try await recording()
    let model = reader.model
    let at = try (1...5).map { try point(in: model, 0.1 * Double($0)) }
    model.pointerDown(at: at[0])
    model.pointerMoved(to: at[1])
    model.pointerUp(at: at[1])
    model.pointerDown(at: at[2])
    model.pointerMoved(to: at[3])
    model.pointerUp(at: at[3])
    model.tapped(at: at[4])
    await model.pointerCallsFinished()
    expectNoDifference(
      sent(),
      [
        Sent(.down, at[0]), Sent(.move, at[1]), Sent(.up, at[1]),
        Sent(.down, at[2]), Sent(.move, at[3]), Sent(.up, at[3]),
        Sent(.down, at[4]), Sent(.up, at[4]),
      ])
    try await reader.viewer.destroy()
    await reader.observing.value
  }

  /// The cancel's sample is at the dropped move's point: the last place
  /// the touch was.
  @Test func aCancelDropsOnlyItsOwnWaitingMove() async throws {
    let (reader, sent) = try await recording()
    let model = reader.model
    let before = try await inkCount(reader.annotations)
    let at = try (1...5).map { try point(in: model, 0.1 * Double($0)) }
    model.pointerDown(at: at[0])
    model.pointerMoved(to: at[1])
    model.pointerCancelled()
    model.pointerDown(at: at[2])
    model.pointerMoved(to: at[3])
    model.pointerUp(at: at[4])
    await model.pointerCallsFinished()
    expectNoDifference(
      sent(),
      [
        Sent(.down, at[0]), Sent(.cancel, at[1]),
        Sent(.down, at[2]), Sent(.move, at[3]), Sent(.up, at[4]),
      ])
    try await reader.annotations.drafts.finishInkDraft()
    let after = try await inkCount(reader.annotations)
    expectNoDifference(after, before + 1)
    try await reader.viewer.destroy()
    await reader.observing.value
  }

  /// A pinch's first finger opens a tool gesture. The pinch cancels it, and
  /// the rest of that touch sends nothing; the next touch draws again.
  @Test func aPinchCancelsTheOpenToolGesture() async throws {
    let (reader, sent) = try await recording()
    let model = reader.model
    let before = try await inkCount(reader.annotations)
    let at = try (1...6).map { try point(in: model, 0.1 * Double($0)) }
    model.pointerDown(at: at[0])
    model.pointerMoved(to: at[1])
    // The move has reached the hub when the pinch begins; one still waiting
    // would be dropped by the cancel.
    await model.pointerCallsFinished()
    model.magnifyChanged(1.1, at: at[1])
    model.pointerMoved(to: at[2])
    model.magnifyChanged(1.2, at: at[1])
    model.pointerMoved(to: at[3])
    model.pointerUp(at: at[3])
    model.pointerCancelled()
    await model.magnifyEnded()
    await model.pointerCallsFinished()
    expectNoDifference(sent(), [Sent(.down, at[0]), Sent(.move, at[1]), Sent(.cancel, at[1])])
    try await reader.annotations.drafts.finishInkDraft()
    let pinched = try await inkCount(reader.annotations)
    expectNoDifference(pinched, before)

    model.pointerDown(at: at[4])
    model.pointerMoved(to: at[5])
    model.pointerUp(at: at[5])
    await model.pointerCallsFinished()
    expectNoDifference(
      Array(sent().dropFirst(3)), [Sent(.down, at[4]), Sent(.move, at[5]), Sent(.up, at[5])])
    try await reader.viewer.destroy()
    await reader.observing.value
  }

  /// `features.pdf` has the link. `document_actions.pdf` has none: every
  /// page loads with an empty `links(on:)`.
  @Test func aTapOnALinkYieldsItsActivation() async throws {
    let reader = try await reader("features")
    let model = reader.model
    let pages = try await reader.viewer.pages(of: reader.document)
    var found: LinkItem?
    for page in pages {
      try await reader.link.ensureLoaded(page.page)
      if let first = try await reader.link.links(on: page.page).first {
        found = first
        break
      }
    }
    let link = try #require(found)
    try await reader.stage.goToPage(link.page)
    let center = Point(
      x: link.bounds.x + link.bounds.width / 2, y: link.bounds.y + link.bounds.height / 2)
    let viewport = try #require(try await reader.stage.pageToViewport(link.page, center))

    let activations = model.linkActivations
    let second = model.linkActivations
    var samples: [PointerSample] = []
    model.pointerSampleDispatched = { samples.append($0) }
    model.tapped(at: CGPoint(x: viewport.x, y: viewport.y))
    await model.pointerCallsFinished()
    expectNoDifference(samples.map(\.phase), [.down, .up])
    expectNoDifference(samples.map(\.clickCount), [1, 1])
    expectNoDifference(samples.map { $0.page?.page }, [link.page, link.page])

    var received: [LinkActivation] = []
    for await activation in activations {
      received.append(activation)
      break
    }
    expectNoDifference(received.count, 1)
    // A second reader gets the same activation, not a share of the first's.
    var other: [LinkActivation] = []
    for await activation in second {
      other.append(activation)
      break
    }
    expectNoDifference(other, received)
    try await reader.viewer.destroy()
    await reader.observing.value
  }

  /// Without the annotation plugin a tap reads the link plugin's own page
  /// mirror, which is empty until the page loads. The reader loads each page
  /// it shows, so the test loads nothing itself.
  @Test func aReaderWithOnlyTheLinksFeatureActivatesALinkOnATap() async throws {
    let reader = PDFReaderModel(features: [.links])
    await reader.open(try pluginFixture("features"), name: "features.pdf")
    guard case let .ready(session) = reader.phase else {
      Issue.record("expected .ready, got \(reader.phase)")
      return
    }
    do {
      _ = try await session.viewer.annotations(for: session.document)
      Issue.record("expected no annotation plugin")
    } catch {
      expectNoDifference(error.code, .unsupported)
    }
    let model = session.reader
    let observing = Task { await model.task() }
    defer { observing.cancel() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 2)
    // The link tap handler registers in `task()`, before the tool is read.
    try await expectObserved({ model.activeToolID != nil }, equals: true) {}
    let activation = try await tapAVisibleLink(model, try await session.viewer.link(for: session.document))
    #expect(activation != nil, "the tap found no link")
    #expect(model.error == nil)
    await reader.close()
  }

  /// The same for a reader built by hand on a viewer with the link and
  /// interaction plugins and no annotation plugin.
  @Test func aHandBuiltReaderWithoutTheAnnotationPluginActivatesALinkOnATap() async throws {
    let viewer = try PDFViewer(
      ViewerConfig(render: RenderConfig(), interaction: true, stage: StageConfig(), link: true))
    try await viewer.start()
    let document = try await viewer.open(pluginFixture("features"))
    let link = try await viewer.link(for: document)
    let model = StageReaderModel(
      stage: try await viewer.stage(for: document), render: try await viewer.render(for: document),
      interaction: try await viewer.interaction(for: document), link: link)
    let observing = Task { await model.task() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 2)
    try await expectObserved({ model.activeToolID != nil }, equals: true) {}
    let activation = try await tapAVisibleLink(model, link)
    #expect(activation != nil, "the tap found no link")
    #expect(model.error == nil)
    try await viewer.destroy()
    await observing.value
  }

  /// A page the reader has scrolled to gets its links loaded too, once.
  @Test func aPageScrolledIntoViewHasItsLinksLoaded() async throws {
    let viewer = try PDFViewer(
      ViewerConfig(render: RenderConfig(), interaction: true, stage: StageConfig(), link: true))
    try await viewer.start()
    let document = try await viewer.open(pluginFixture())
    let link = try await viewer.link(for: document)
    let pages = try await viewer.pages(of: document)
    let last = try #require(pages.last?.page)
    let model = StageReaderModel(
      stage: try await viewer.stage(for: document), render: try await viewer.render(for: document),
      interaction: try await viewer.interaction(for: document), link: link)
    let observing = Task { await model.task() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 2)
    #expect(!model.visiblePages.map(\.page).contains(last))
    let before = try await link.isLoaded(last)
    #expect(!before)
    try await expectObserved({ model.visiblePages.map(\.page).contains(last) }, equals: true) {
      try await model.stage.goToPage(last)
    }
    await model.linkLoadsFinished()
    let after = try await link.isLoaded(last)
    #expect(after)
    try await viewer.destroy()
    await observing.value
  }

  /// Taps the first link on the reader's first visible page and returns the
  /// activation, or `nil` when none arrives in 10 seconds. Loads no links
  /// itself: `links(on:)` is empty unless the reader loaded the page.
  func tapAVisibleLink(_ model: StageReaderModel, _ links: LinkCapability) async throws -> LinkActivation? {
    await model.linkLoadsFinished()
    let page = try #require(model.visiblePages.first?.page)
    let link = try #require(try await links.links(on: page).first, "the reader did not load the page's links")
    let center = Point(
      x: link.bounds.x + link.bounds.width / 2, y: link.bounds.y + link.bounds.height / 2)
    let spot = try #require(try await model.stage.pageToViewport(page, center))
    let activations = model.linkActivations
    model.tapped(at: CGPoint(x: spot.x, y: spot.y))
    return await firstValue(of: activations, within: .seconds(10))
  }
}
