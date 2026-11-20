import CustomDump
import EmbedPDF
import Testing

extension BaseSuite {
  /// `render` and `search` take `for:`, as `stage(for:)` and the other
  /// document accessors do. The `in:` spellings stay as deprecated shims.
  @Suite struct AccessorNameTests {
    func opened() async throws -> (PDFViewer, DocumentInfo) {
      let viewer = try PDFViewer(ViewerConfig(search: SearchConfig(), render: RenderConfig()))
      try await viewer.start()
      return (viewer, try await viewer.open(Fixture.data("report")))
    }

    @Test func renderAndSearchTakeForLikeTheOtherAccessors() async throws {
      let (viewer, document) = try await opened()
      let render = try await viewer.render(for: document)
      let page = try await viewer.pages(of: document)[0].page
      let rendered = try await render.renderPage(page, options: RenderPageOptions(scale: 1))
      #expect(rendered.image.width > 0)
      let search = try await viewer.search(for: document)
      let result = try await search.search(SearchQuery(text: "the"))
      expectNoDifference(result.status, .complete)
      try await viewer.destroy()
    }

    /// Deprecated, so its own uses of the old spellings don't warn.
    @available(*, deprecated)
    @Test func theInSpellingsStillReturnTheSameCapabilities() async throws {
      let (viewer, document) = try await opened()
      let render = try await viewer.render(in: document)
      let renamedRender = try await viewer.render(for: document)
      let settings = try await render.settings()
      let renamedSettings = try await renamedRender.settings()
      expectNoDifference(settings, renamedSettings)
      let search = try await viewer.search(in: document)
      let renamedSearch = try await viewer.search(for: document)
      let hits = try await search.findAll(SearchQuery(text: "Report"))
      let renamedHits = try await renamedSearch.findAll(SearchQuery(text: "Report"))
      #expect(!hits.isEmpty)
      expectNoDifference(hits, renamedHits)
      try await viewer.destroy()
    }
  }
}
