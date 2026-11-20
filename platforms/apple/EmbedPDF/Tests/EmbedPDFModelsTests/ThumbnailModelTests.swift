import CustomDump
import EmbedPDF
@testable import EmbedPDFModels
import Testing

@MainActor
@Suite(.serialized, .timeLimit(.minutes(1)))
struct ThumbnailModelTests {
  func opened() async throws -> (PDFViewer, DocumentInfo, [PageInfo], RenderCapability) {
    let viewer = try PDFViewer(ViewerConfig(render: RenderConfig(), pageEdit: true))
    try await viewer.start()
    let document = try await viewer.open(pluginFixture())
    return (viewer, document, try await viewer.pages(of: document), try await viewer.render(for: document))
  }

  @Test func aLoadedThumbnailIsTurnedToThePageAsShown() async throws {
    let (viewer, document, unturned, render) = try await opened()
    // report.pdf is portrait (612 by 792) with no rotation, so the test
    // turns its first page a quarter turn.
    #expect(unturned[0].height > unturned[0].width)
    _ = try await viewer.pageEdit(for: document).setRotation(.clockwise90, of: [unturned[0].page])
    let pages = try await viewer.pages(of: document)
    expectNoDifference(pages[0].rotation, 90)
    let model = ThumbnailModel(render: render, pages: pages)
    #expect(model.image(for: pages[0].page) == nil)

    await model.load(pages[0].page)
    let turned = try #require(model.image(for: pages[0].page))
    #expect(turned.width > turned.height)
    await model.load(pages[1].page)
    let upright = try #require(model.image(for: pages[1].page))
    #expect(upright.height > upright.width)
    #expect(model.error == nil)
    try await viewer.destroy()
  }

  @Test func anInvalidationMakesTheNextLoadRenderAgain() async throws {
    let (viewer, _, pages, render) = try await opened()
    let model = ThumbnailModel(render: render, pages: pages)
    let page = pages[0].page
    await model.load(page)
    expectNoDifference(model.renderCount, 1)
    // The revision is the same, so the cached thumbnail answers.
    await model.load(page)
    expectNoDifference(model.renderCount, 1)

    try await expectObserved({ model.revision(for: page) }, equals: 1) {
      try await render.invalidate([page], scope: .annotations)
    }
    await model.load(page)
    expectNoDifference(model.renderCount, 2)
    #expect(model.image(for: page) != nil)
    try await viewer.destroy()
  }

  @Test func twoLoadsOfOnePageShareOneRender() async throws {
    let (viewer, _, pages, render) = try await opened()
    let model = ThumbnailModel(render: render, pages: pages)
    let page = pages[0].page
    async let first: Void = model.load(page)
    async let second: Void = model.load(page)
    _ = await (first, second)
    expectNoDifference(model.renderCount, 1)
    #expect(model.image(for: page) != nil)
    try await viewer.destroy()
  }

  /// Holds the model's first render, before it reaches the engine, until
  /// `release` runs or the render is cancelled. `reached` yields once the
  /// render is held.
  func holdingFirstRender(of model: ThumbnailModel) -> (reached: AsyncStream<Void>, release: @MainActor () -> Void) {
    let (reached, reach) = AsyncStream<Void>.makeStream()
    let (gate, open) = AsyncStream<Void>.makeStream(bufferingPolicy: .bufferingNewest(1))
    var held = false
    model.renderStarted = {
      guard !held else { return }
      held = true
      reach.yield(())
      // Ends when the gate opens, and also when the render is cancelled.
      for await _ in gate { break }
    }
    return (reached, {
      open.yield(())
      open.finish()
    })
  }

  @Test func aLoadCancelledInFlightLeavesNoImageAndNoError() async throws {
    let (viewer, _, pages, render) = try await opened()
    let model = ThumbnailModel(render: render, pages: pages)
    let page = pages[0].page
    let (reached, release) = holdingFirstRender(of: model)
    let load = Task { await model.load(page) }
    for await _ in reached { break }
    expectNoDifference(model.renderCount, 1)
    expectNoDifference(model.waiterCount(for: page), 1)

    // The gate stays shut, so the load returns only because its
    // cancellation cancelled the held render.
    load.cancel()
    await load.value
    #expect(model.image(for: page) == nil)
    #expect(model.error == nil)
    expectNoDifference(model.waiterCount(for: page), 0)

    // The page loads once someone waits for it again.
    release()
    await model.load(page)
    #expect(model.image(for: page) != nil)
    expectNoDifference(model.renderCount, 2)
    try await viewer.destroy()
  }

  @Test func aCancelledWaiterLeavesTheRenderToTheOtherWaiter() async throws {
    let (viewer, _, pages, render) = try await opened()
    let model = ThumbnailModel(render: render, pages: pages)
    let page = pages[0].page
    let (reached, release) = holdingFirstRender(of: model)
    let first = Task { await model.load(page) }
    for await _ in reached { break }
    let second = Task { await model.load(page) }
    while model.waiterCount(for: page) < 2 { await Task.yield() }

    first.cancel()
    while model.waiterCount(for: page) > 1 { await Task.yield() }
    release()
    await second.value
    await first.value
    #expect(model.image(for: page) != nil)
    #expect(model.error == nil)
    expectNoDifference(model.renderCount, 1)
    try await viewer.destroy()
  }

  @Test func anInvalidationInFlightSupersedesTheRender() async throws {
    let (viewer, _, pages, render) = try await opened()
    let model = ThumbnailModel(render: render, pages: pages)
    let page = pages[0].page
    let (reached, _) = holdingFirstRender(of: model)
    let stale = Task { await model.load(page) }
    for await _ in reached { break }
    try await expectObserved({ model.revision(for: page) }, equals: 1) {
      try await render.invalidate([page], scope: .annotations)
    }

    // The gate stays shut: this load cancels the held render and starts one
    // for the new revision.
    await model.load(page)
    await stale.value
    expectNoDifference(model.renderCount, 2)
    #expect(model.image(for: page) != nil)
    #expect(model.error == nil)
    // The image is cached at the new revision, so nothing renders again.
    await model.load(page)
    expectNoDifference(model.renderCount, 2)
    try await viewer.destroy()
  }

  @Test func aFailedLoadKeepsTheEarlierImageAndSetsTheError() async throws {
    let (viewer, document, pages, render) = try await opened()
    let model = ThumbnailModel(render: render, pages: pages)
    let page = pages[0].page
    await model.load(page)
    let earlier = try #require(model.image(for: page))
    try await expectObserved({ model.revision(for: page) }, equals: 1) {
      try await render.invalidate([page], scope: .annotations)
    }
    try await viewer.close(document)

    // The page is invalidated, so this load renders, and the closed
    // document refuses it.
    await model.load(page)
    expectNoDifference(model.error?.code, .instanceClosed)
    #expect(model.image(for: page) === earlier)
    await model.load(pages[1].page)
    #expect(model.image(for: pages[1].page) == nil)
    expectNoDifference(model.renderCount, 3)
    // Each load of a failed page tries again.
    await model.load(page)
    expectNoDifference(model.renderCount, 4)
    expectNoDifference(model.error?.code, .instanceClosed)
    try await viewer.destroy()
  }

  @Test func aPageOutsideTheModelFailsWithoutRendering() async throws {
    let (viewer, _, pages, render) = try await opened()
    let model = ThumbnailModel(render: render, pages: Array(pages.dropFirst()))
    await model.load(pages[0].page)
    expectNoDifference(model.renderCount, 0)
    expectNoDifference(model.error?.code, .notFound)
    #expect(model.image(for: pages[0].page) == nil)
    try await viewer.destroy()
  }

  @Test func theCacheDropsThePageLoadedLongestAgo() async throws {
    let (viewer, _, pages, render) = try await opened()
    let model = ThumbnailModel(render: render, pages: pages)
    model.cacheLimit = 2
    await model.load(pages[0].page)
    await model.load(pages[1].page)
    // A cached load still counts as a use, so page 1 is now the oldest.
    await model.load(pages[0].page)
    await model.load(pages[2].page)
    #expect(model.image(for: pages[0].page) != nil)
    #expect(model.image(for: pages[1].page) == nil)
    #expect(model.image(for: pages[2].page) != nil)
    try await viewer.destroy()
  }
}
