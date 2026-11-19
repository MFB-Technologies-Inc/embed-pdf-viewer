import CustomDump
import EmbedPDF
@testable import EmbedPDFUI
import Foundation
import Observation
import SwiftUI
import Testing

@MainActor
@Suite(.serialized, .timeLimit(.minutes(1)))
struct StageReaderModelTests {
  @Test func rustInvalidationRefreshesOnlyItsPagesWithoutMovingTheCamera() async throws {
    let viewer = try PDFViewer(ViewerConfig(render: RenderConfig(), stage: StageConfig()))
    try await viewer.start()
    let document = try await viewer.open(pluginFixture())
    let pages = try await viewer.pages(of: document)
    let stage = try await viewer.stage(for: document)
    let render = try await viewer.render(in: document)
    try await stage.setViewportSize(Size(width: 800, height: 600))
    let model = StageReaderModel(stage: stage, render: render)
    let observing = Task { await model.task() }
    defer { observing.cancel() }
    try await expectObserved({ !model.visiblePages.isEmpty }, equals: true) {}
    let before = model.renderRevision(for: pages[0].page)
    let other = model.renderRevision(for: pages[1].page)
    let camera = try await stage.camera()
    try await expectObserved({ model.renderRevision(for: pages[0].page) > before }, equals: true) {
      try await render.invalidate([pages[0].page], scope: .annotations)
    }
    expectNoDifference(model.renderRevision(for: pages[1].page), other)
    let after = try await stage.camera()
    expectNoDifference(after, camera)
    try await viewer.destroy()
    await observing.value
  }

  func model() async throws -> (StageReaderModel, Task<Void, Never>) {
    let viewer = try PDFViewer(ViewerConfig(render: RenderConfig(), stage: StageConfig()))
    try await viewer.start()
    let url = try #require(Bundle.module.url(forResource: "report", withExtension: "pdf", subdirectory: "Fixtures"))
    let document = try await viewer.open(try Data(contentsOf: url))
    let model = StageReaderModel(
      stage: try await viewer.stage(for: document), render: try await viewer.render(in: document))
    let task = Task { await model.task() }
    return (model, task)
  }

  @Test func sizingShowsPages() async throws {
    let (model, task) = try await model()
    defer { task.cancel() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 3)
    #expect(!model.visiblePages.isEmpty)
    expectNoDifference(model.pageIndex, 0)
  }

  /// The `UInt32` spelling reads the same page as ``StageReaderModel/pageIndex``.
  @available(*, deprecated)
  @Test func theUInt32CurrentPageIndexFollowsPageIndex() async throws {
    let (model, task) = try await model()
    defer { task.cancel() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 3)
    for _ in 0..<12 {
      await model.dragChanged(translation: CGSize(width: 0, height: -800))
      await model.dragEnded(velocity: .zero)
    }
    #expect(model.pageIndex > 0)
    expectNoDifference(Int(model.currentPageIndex), model.pageIndex)
  }

  @Test func draggingScrollsAndUpdatesTheVisiblePages() async throws {
    let (model, task) = try await model()
    defer { task.cancel() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 3)
    let before = model.camera
    await model.dragChanged(translation: CGSize(width: 0, height: -400))
    await model.dragEnded(velocity: .zero)
    #expect(model.camera != before)
  }

  /// A drag sample does not read the camera back itself: the stage's camera
  /// event brings it, with the pages it now shows.
  @Test func aDragSampleReachesTheModelThroughTheStageEvents() async throws {
    let (model, task) = try await model()
    defer { task.cancel() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 3)
    let before = model.visiblePages
    await model.dragChanged(translation: CGSize(width: 0, height: -400))
    let camera = try await model.stage.camera()
    let pages = try await model.stage.visiblePages()
    #expect(pages != before)
    try await expectObserved({ model.camera }, equals: camera) {}
    try await expectObserved({ model.visiblePages }, equals: pages) {}
    await model.dragEnded(velocity: .zero)
  }

  @Test func resizingKeepsTheCurrentPage() async throws {
    let (model, task) = try await model()
    defer { task.cancel() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 3)
    for _ in 0..<12 {
      await model.dragChanged(translation: CGSize(width: 0, height: -800))
      await model.dragEnded(velocity: .zero)
    }
    let page = model.pageIndex
    #expect(page > 0)
    await model.sizeChanged(CGSize(width: 844, height: 390), displayScale: 3)
    expectNoDifference(model.pageIndex, page)
    #expect(!model.visiblePages.isEmpty)
  }

  /// SwiftUI delivers a tap's change and end back to back, each in its own
  /// task. The gesture bracket must still open before it closes, or the
  /// stage keeps an elastic overscroll it never springs back from.
  @Test func backToBackGestureCallsKeepTheBracketOrdered() async throws {
    let (model, task) = try await model()
    defer { task.cancel() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 3)
    let resting = try await model.stage.camera()
    for _ in 0..<20 {
      let change = Task { await model.dragChanged(translation: CGSize(width: 0, height: 300)) }
      let end = Task { await model.dragEnded(velocity: .zero) }
      await change.value
      await end.value
    }
    // The host clock runs frames, so the spring-back after the last end
    // animates. Wait for the motion to stop, bounded so a bracket that never
    // closes still fails.
    let deadline = ContinuousClock.now + .seconds(20)
    while try await model.stage.isMoving(), ContinuousClock.now < deadline {
      try await Task.sleep(for: .milliseconds(50))
    }
    let camera = try await model.stage.camera()
    expectNoDifference(camera, resting)
  }

  /// Until the reader draws tiles, a deep zoom would render each page whole
  /// at the zoomed scale: 7200 by 9600 pixels for a 600-point page at zoom 4
  /// on a 3x screen. The scale it renders at is capped by pixel count.
  @Test func aDeepZoomRendersAtACappedScale() {
    let capped = StageReaderModel.renderScale(12, contentWidth: 600, contentHeight: 800)
    #expect(capped < 12)
    #expect(600 * capped * 800 * capped <= StageReaderModel.maxPagePixels * 1.000001)
  }

  /// While zooming, under the cap, the page renders at the stage's scale
  /// rounded up to a width band, so a pinch within one band keeps the render
  /// it has.
  @Test func whileZoomingTheRenderScaleRoundsUpToAWidthBand() {
    let scale = StageReaderModel.renderScale(2, contentWidth: 600, contentHeight: 800, zooming: true)
    expectNoDifference(scale, PageRenderResolution.bandedScale(2, pageWidth: 600))
    expectNoDifference(
      StageReaderModel.renderScale(2.1, contentWidth: 600, contentHeight: 800, zooming: true), scale)
    #expect(scale >= 2.1)
  }

  /// Otherwise the page renders at the stage's scale itself: a 612-point
  /// page at 2 is 1224 pixels wide, not the 1280 of its band.
  @Test func atRestTheRenderScaleIsTheStageScale() {
    expectNoDifference(StageReaderModel.renderScale(2, contentWidth: 612, contentHeight: 792), 2)
    expectNoDifference(StageReaderModel.renderScale(2.05, contentWidth: 612, contentHeight: 792), 2.05)
    let zooming = StageReaderModel.renderScale(2, contentWidth: 612, contentHeight: 792, zooming: true)
    expectNoDifference((zooming * 612).rounded(), 1280)
    // The cap holds either way.
    expectNoDifference(
      StageReaderModel.renderScale(12, contentWidth: 600, contentHeight: 800),
      StageReaderModel.renderScale(12, contentWidth: 600, contentHeight: 800, zooming: true))
  }

  /// The scale each visible page renders at, as `StageReaderView` asks for it.
  func renderScales(_ model: StageReaderModel) -> [Double] {
    model.visiblePages.map {
      StageReaderModel.renderScale(
        $0.transform.renderScale, contentWidth: $0.transform.contentWidth,
        contentHeight: $0.transform.contentHeight, zooming: model.isZooming)
    }
  }

  /// A pan, a fling and a tap (a zero-distance drag) change no scale, so the
  /// reader never zooms and every page keeps its at-rest render scale: the
  /// view starts no new render for them.
  @Test func aDragWithNoZoomKeepsTheRestingRenderScale() async throws {
    let (model, task) = try await model()
    defer { task.cancel() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 3)
    let resting = renderScales(model)
    #expect(!resting.isEmpty)
    // `isZooming` is written only when it changes, so any write is a zoom.
    let written = Written()
    withObservationTracking { _ = model.isZooming } onChange: { written.set() }
    // A tap.
    await model.dragChanged(translation: .zero)
    await model.dragEnded(velocity: .zero)
    expectNoDifference(renderScales(model), resting)
    // A pan, then a fling.
    for step in 1...5 {
      await model.dragChanged(translation: CGSize(width: 0, height: -40 * Double(step)))
      #expect(!model.isZooming)
    }
    await model.dragEnded(velocity: CGSize(width: 0, height: -1500))
    let deadline = ContinuousClock.now + .seconds(20)
    while try await model.stage.isMoving(), ContinuousClock.now < deadline {
      try await Task.sleep(for: .milliseconds(50))
    }
    #expect(!written.value, "a drag made the reader zoom")
    #expect(!model.isZooming)
    // Same zoom, other pages: each still renders at its own at-rest scale.
    for page in model.visiblePages {
      expectNoDifference(
        StageReaderModel.renderScale(
          page.transform.renderScale, contentWidth: page.transform.contentWidth,
          contentHeight: page.transform.contentHeight, zooming: model.isZooming),
        StageReaderModel.renderScale(
          page.transform.renderScale, contentWidth: page.transform.contentWidth,
          contentHeight: page.transform.contentHeight))
    }
  }

  /// A pinch zooms from its first change until its end and any animation
  /// after it settle.
  @Test func aPinchZoomsUntilItSettles() async throws {
    let (model, task) = try await model()
    defer { task.cancel() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 3)
    #expect(!model.isZooming)
    model.magnifyChanged(1.2, at: CGPoint(x: 195, y: 422))
    #expect(model.isZooming)
    try await expectObserved({ model.isZooming }, equals: false) {
      await model.magnifyEnded()
    }
  }

  /// A double tap's zoom animation zooms until the stage settles.
  @Test func aDoubleTapZoomsUntilItSettles() async throws {
    let (model, task) = try await model()
    defer { task.cancel() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 3)
    try await expectObserved({ model.isZooming }, equals: false) {
      await model.doubleTapped(at: CGPoint(x: 195, y: 422))
    }
    #expect(try await !model.stage.isMoving())
  }

  /// A refresh that runs while a double tap's stage call is still on its way
  /// reads the stage before the zoom starts and finds it still. That read
  /// must not end the zoom the tap then starts.
  @Test func aRefreshAheadOfTheDoubleTapDoesNotEndItsZoom() async throws {
    let (model, task) = try await model()
    defer { task.cancel() }
    let size = CGSize(width: 390, height: 844)
    await model.sizeChanged(size, displayScale: 3)
    let (reached, reach) = AsyncStream<Void>.makeStream()
    let (gate, open) = AsyncStream<Void>.makeStream(bufferingPolicy: .bufferingNewest(1))
    model.zoomStartReached = {
      reach.yield(())
      for await _ in gate { break }
    }
    let tap = Task { await model.doubleTapped(at: CGPoint(x: 195, y: 422)) }
    for await _ in reached { break }
    #expect(try await !model.stage.isMoving())
    // Ends in a refresh, which reads the still stage.
    await model.sizeChanged(size, displayScale: 3)
    open.yield(())
    open.finish()
    await tap.value
    #expect(try await model.stage.isMoving())
    #expect(model.isZooming)
    try await expectObserved({ model.isZooming }, equals: false) {}
    #expect(try await !model.stage.isMoving())
  }

  /// A refresh that began before a double tap, with no zoom start in flight,
  /// reads the still stage. If that read lands only after the tap has started
  /// its zoom, it must not end that zoom.
  @Test func aReadTakenBeforeTheDoubleTapDoesNotEndItsZoom() async throws {
    let (model, task) = try await model()
    defer { task.cancel() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 3)
    let (reached, reach) = AsyncStream<Void>.makeStream()
    let (gate, open) = AsyncStream<Void>.makeStream(bufferingPolicy: .bufferingNewest(1))
    var held = false
    model.zoomReadReturned = {
      guard !held else { return }
      held = true
      reach.yield(())
      for await _ in gate { break }
    }
    // A pinch starts no motion, so its end's refresh reads the still stage
    // and is held there.
    model.magnifyChanged(1.2, at: CGPoint(x: 195, y: 422))
    let pinch = Task { await model.magnifyEnded() }
    for await _ in reached { break }
    // The held read saw a still stage. If it had seen motion, applying it
    // late would be harmless and this test would not cover the epoch check.
    #expect(try await !model.stage.isMoving())
    await model.doubleTapped(at: CGPoint(x: 195, y: 422))
    #expect(try await model.stage.isMoving())
    #expect(model.isZooming)
    // The held read must leave `isZooming` alone. This relies on the 240 ms
    // tween outlasting the hops from the tap's return to here.
    let ended = await observationFires({ model.isZooming }) {
      open.yield(())
      open.finish()
      await pinch.value
    }
    #expect(!ended, "a read taken before the double tap ended its zoom")
    #expect(model.isZooming)
    try await expectObserved({ model.isZooming }, equals: false) {}
    #expect(try await !model.stage.isMoving())
  }

  /// A double tap while a pinch is open starts a zoom animation. When that
  /// animation settles the pinch is still open, so the reader keeps zooming
  /// until the pinch ends too.
  @Test func aDoubleTapDuringAPinchKeepsZoomingUntilThePinchEnds() async throws {
    let (model, task) = try await model()
    defer { task.cancel() }
    let size = CGSize(width: 390, height: 844)
    await model.sizeChanged(size, displayScale: 3)
    model.magnifyChanged(1.2, at: CGPoint(x: 195, y: 422))
    #expect(model.isZooming)
    await model.doubleTapped(at: CGPoint(x: 195, y: 422))
    #expect(model.isZooming)
    let fired = armObservation { model.isZooming }
    let deadline = ContinuousClock.now + .seconds(20)
    while try await model.stage.isMoving(), ContinuousClock.now < deadline {
      try await Task.sleep(for: .milliseconds(50))
    }
    #expect(try await !model.stage.isMoving())
    // Ends in a refresh, which reads the still stage with no start in flight.
    await model.sizeChanged(size, displayScale: 3)
    #expect(!fired.value, "the double tap's settling ended the zoom under an open pinch")
    #expect(model.isZooming)
    try await expectObserved({ model.isZooming }, equals: false) {
      await model.magnifyEnded()
    }
  }

  /// A pinch's end reads the still stage. If a new pinch starts before that
  /// read lands, the read must not end the new pinch's zoom.
  @Test func aPinchStartedWhileASettleReadIsOutKeepsZooming() async throws {
    let (model, task) = try await model()
    defer { task.cancel() }
    let size = CGSize(width: 390, height: 844)
    await model.sizeChanged(size, displayScale: 3)
    let (reached, reach) = AsyncStream<Void>.makeStream()
    // Every settle read is held, not only the first: a refresh the first
    // pinch's `zoomChanged` event started can reach the hook ahead of the
    // pinch end's own, and its read is the one the epoch check drops.
    var isOpen = false
    var held: [CheckedContinuation<Void, Never>] = []
    model.zoomReadReturned = {
      reach.yield(())
      if !isOpen { await withCheckedContinuation { held.append($0) } }
    }
    // A pinch starts no motion, so the refresh at its end reads the still
    // stage and is held there.
    model.magnifyChanged(1.2, at: CGPoint(x: 195, y: 422))
    let pinch = Task { await model.magnifyEnded() }
    for await _ in reached { break }
    // The pinch end's refresh makes three stage calls before the hook. Three
    // more from here return after them, so its read is held by now too.
    for _ in 1...3 { #expect(try await !model.stage.isMoving()) }
    model.magnifyChanged(1.1, at: CGPoint(x: 195, y: 422))
    #expect(model.isZooming)
    let ended = await observationFires({ model.isZooming }) {
      isOpen = true
      for continuation in held { continuation.resume() }
      held = []
      await pinch.value
    }
    #expect(!ended, "a settle read taken before the pinch ended its zoom")
    #expect(model.isZooming)
    try await expectObserved({ model.isZooming }, equals: false) {
      await model.magnifyEnded()
    }
  }

  @Test func disablingPanLeavesTheCameraAlone() async throws {
    let (model, task) = try await model()
    defer { task.cancel() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 3)
    let before = try await model.stage.camera()
    model.isPanEnabled = false
    await model.dragChanged(translation: CGSize(width: 0, height: -200))
    await model.dragEnded(velocity: .zero)
    let after = try await model.stage.camera()
    expectNoDifference(after, before)
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
}

/// Set once, from any thread: an observation's `onChange`.
final class Written: @unchecked Sendable {
  private let lock = NSLock()
  private var written = false
  var value: Bool { lock.withLock { written } }
  func set() { lock.withLock { written = true } }
}
