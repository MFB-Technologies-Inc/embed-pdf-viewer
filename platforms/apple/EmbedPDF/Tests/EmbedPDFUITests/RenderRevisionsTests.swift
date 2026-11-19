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
