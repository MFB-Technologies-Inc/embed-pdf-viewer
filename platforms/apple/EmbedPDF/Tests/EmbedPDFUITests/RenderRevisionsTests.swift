import CustomDump
import EmbedPDF
import EmbedPDFUI
import Testing

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
    let (viewer, _, pages, render) = try await opened()
    let revisions = RenderRevisions(render: render)
    try await expectObserved({ revisions.revision(for: pages[0].page) }, equals: 1) {
      try await render.invalidate([pages[0].page], scope: .annotations)
    }
    expectNoDifference(revisions.revision(for: pages[1].page), 0)
    try await viewer.destroy()
  }

  @Test func anInvalidationRightAfterInitIsCounted() async throws {
    let (viewer, _, pages, render) = try await opened()
    let revisions = RenderRevisions(render: render)
    // Sent before the model's own observation has had a turn to run.
    try await render.invalidate([pages[0].page], scope: .annotations)
    try await expectObserved({ revisions.revision(for: pages[0].page) }, equals: 1) {}
    try await viewer.destroy()
  }

  @Test func aReleasedModelStopsObservingWhileTheDocumentStaysOpen() async throws {
    let (viewer, _, pages, render) = try await opened()
    weak var released: RenderRevisions?
    do {
      let revisions = RenderRevisions(render: render)
      released = revisions
      // The model is observing by the time it is released.
      try await expectObserved({ revisions.revision(for: pages[0].page) }, equals: 1) {
        try await render.invalidate([pages[0].page], scope: .annotations)
      }
    }
    let events = render.events
    try await render.invalidate([pages[1].page], scope: .annotations)
    #expect(released == nil)
    var received = events.makeAsyncIterator()
    guard case let .invalidated(invalidated, _) = await received.next() else {
      Issue.record("the next subscriber received no invalidation")
      return
    }
    expectNoDifference(invalidated, [pages[1].page])
    try await viewer.destroy()
  }

  @Test func closingTheDocumentKeepsTheCounts() async throws {
    let (viewer, document, pages, render) = try await opened()
    let revisions = RenderRevisions(render: render)
    try await expectObserved({ revisions.revision(for: pages[0].page) }, equals: 1) {
      try await render.invalidate([pages[0].page], scope: .annotations)
    }
    try await viewer.close(document)
    // Lets the model's observation see its stream finish.
    for _ in 0..<10 { await Task.yield() }
    expectNoDifference(revisions.revision(for: pages[0].page), 1)
    expectNoDifference(revisions.revision(for: pages[1].page), 0)
    try await viewer.destroy()
  }
}
