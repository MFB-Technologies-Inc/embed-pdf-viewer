import EmbedPDF
import Foundation
import Testing

/// A fetcher written against `EmbedPDF` alone: this file does not import the
/// bindings module, so it compiles only while `DocumentFetcher`,
/// `DocumentFetchCompletion` and `DocumentFetchCancellation` are exported.
private final class StubFetcher: DocumentFetcher, Sendable {
  let body: Data

  init(body: Data) { self.body = body }

  func fetch(url: String, completion: DocumentFetchCompletion) -> any DocumentFetchCancellation {
    if url.hasSuffix("/report.pdf") {
      completion.finish(status: 200, bytes: body)
    } else {
      completion.failWith(failure: HostFailure(name: "StubFetcher", code: "not-found", detail: "no such document"))
    }
    return StubCancellation()
  }
}

private final class StubCancellation: DocumentFetchCancellation, Sendable {
  func cancel() {}
}

extension BaseSuite {
  @Suite struct FetcherExportTests {
    @Test func aFetcherWrittenAgainstEmbedPDFAloneServesAURLDocument() async throws {
      let viewer = try PDFViewer(ViewerConfig(), documentFetcher: StubFetcher(body: try Fixture.data("report")))
      try await viewer.start()
      let document = try await viewer.open(URL(string: "https://embedpdf.test/report.pdf")!)
      #expect(document.status == .ready)
      #expect(document.pageCount == 14)
      await #expect { _ = try await viewer.open(URL(string: "https://embedpdf.test/missing.pdf")!) } throws: {
        ($0 as? PDFError)?.code == .notFound
      }
      try await viewer.destroy()
    }
  }
}
