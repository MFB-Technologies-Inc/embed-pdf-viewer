import CoreGraphics
import CustomDump
import EmbedPDF
@testable import EmbedPDFModels
import Testing

/// ``StageReaderModel/error`` is cleared by a later success of the kind of
/// call that set it, and by no other. The engine offers no failure that
/// later heals, so each test records the failure through the model's own
/// `failed(_:_:)` and then makes real calls.
@MainActor
@Suite(.serialized, .timeLimit(.minutes(1)))
struct StageReaderErrorTests {
  let failure = PDFError(.operationFailed, detail: "a call the test failed")

  struct Reader {
    let viewer: PDFViewer
    let model: StageReaderModel
    let interaction: InteractionCapability
    let last: PageRef
    var observing: Task<Void, Never>
  }

  /// A sized reader with its `task()` running and the tool read once.
  func reader(link: Bool) async throws -> Reader {
    let viewer = try PDFViewer(
      ViewerConfig(
        render: RenderConfig(), interaction: true, stage: StageConfig(), link: link, annotation: ""))
    try await viewer.start()
    let document = try await viewer.open(pluginFixture())
    let interaction = try await viewer.interaction(for: document)
    let model = StageReaderModel(
      stage: try await viewer.stage(for: document), render: try await viewer.render(for: document),
      interaction: interaction, link: link ? try await viewer.link(for: document) : nil)
    let observing = Task { await model.task() }
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 2)
    try await expectObserved({ model.activeToolID != nil }, equals: true) {}
    await model.linkLoadsFinished()
    let last = try #require(try await viewer.pages(of: document).last?.page)
    return Reader(viewer: viewer, model: model, interaction: interaction, last: last, observing: observing)
  }

  @Test func aGoodStageCallClearsTheErrorOfItsOwnKind() async throws {
    let reader = try await reader(link: false)
    let model = reader.model
    let size = CGSize(width: 390, height: 844)
    expectNoDifference(model.error, nil)

    model.failed(.resize, failure)
    expectNoDifference(model.error, failure)
    await model.sizeChanged(size, displayScale: 2)
    expectNoDifference(model.error, nil)

    // A resize ends in a read of the pages and the camera.
    model.failed(.read, failure)
    await model.sizeChanged(size, displayScale: 2)
    expectNoDifference(model.error, nil)

    model.failed(.drag, failure)
    await model.dragChanged(translation: .zero)
    expectNoDifference(model.error, nil)
    model.failed(.drag, failure)
    await model.dragEnded(velocity: .zero)
    expectNoDifference(model.error, nil)

    model.failed(.doubleTap, failure)
    await model.doubleTapped(at: CGPoint(x: 195, y: 422))
    expectNoDifference(model.error, nil)

    model.failed(.pageLookup, failure)
    model.tapped(at: CGPoint(x: 195, y: 422))
    await model.pointerCallsFinished()
    expectNoDifference(model.error, nil)
    try await reader.viewer.destroy()
    await reader.observing.value
  }

  @Test func aGoodToolReadClearsAFailedOne() async throws {
    let reader = try await reader(link: false)
    let model = reader.model
    model.failed(.toolRead, failure)
    try await expectObserved({ model.error }, equals: nil) {
      try await reader.interaction.activateTool(.ink)
    }
    expectNoDifference(model.activeToolID, .ink)
    try await reader.viewer.destroy()
    await reader.observing.value
  }

  @Test func aGoodLinkLoadClearsAFailedOne() async throws {
    let reader = try await reader(link: true)
    let model = reader.model
    model.failed(.linkLoad, failure)
    try await expectObserved({ model.visiblePages.map(\.page).contains(reader.last) }, equals: true) {
      try await model.stage.goToPage(reader.last)
    }
    await model.linkLoadsFinished()
    expectNoDifference(model.error, nil)
    try await reader.viewer.destroy()
    await reader.observing.value
  }

  @Test func aGoodLinkTapRegistrationClearsAFailedOne() async throws {
    var reader = try await reader(link: true)
    let model = reader.model
    reader.observing.cancel()
    await reader.observing.value
    model.failed(.linkTapRegistration, failure)
    reader.observing = Task { await model.task() }
    try await expectObserved({ model.error }, equals: nil) {}
    try await reader.viewer.destroy()
    await reader.observing.value
  }

  /// A failed link load stays through good calls of every other kind the
  /// reader makes without a `LinkCapability`.
  @Test func aGoodCallOfAnotherKindKeepsTheError() async throws {
    let reader = try await reader(link: false)
    let model = reader.model
    model.failed(.linkLoad, failure)
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 2)
    await model.dragChanged(translation: CGSize(width: 0, height: -200))
    await model.dragEnded(velocity: .zero)
    await model.doubleTapped(at: CGPoint(x: 195, y: 422))
    model.tapped(at: CGPoint(x: 195, y: 422))
    await model.pointerCallsFinished()
    try await expectObserved({ model.activeToolID }, equals: .ink) {
      try await reader.interaction.activateTool(.ink)
    }
    expectNoDifference(model.error, failure)
    // A newer failure replaces it, and only its own kind clears that one.
    let newer = PDFError(.operationFailed, detail: "a newer failure")
    model.failed(.drag, newer)
    await model.sizeChanged(CGSize(width: 390, height: 844), displayScale: 2)
    expectNoDifference(model.error, newer)
    await model.dragChanged(translation: .zero)
    await model.dragEnded(velocity: .zero)
    expectNoDifference(model.error, nil)
    try await reader.viewer.destroy()
    await reader.observing.value
  }
}
