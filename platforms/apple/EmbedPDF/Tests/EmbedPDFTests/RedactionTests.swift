import CustomDump
import EmbedPDF
import Foundation
import Testing

extension BaseSuite {
  @Suite struct RedactionTests {
    func opened() async throws -> (PDFViewer, DocumentInfo, RedactionCapability, SearchCapability) {
      let viewer = try PDFViewer(
        ViewerConfig(
          search: SearchConfig(), interaction: true, annotation: "",
          redaction: RedactionConfig()))
      try await viewer.start()
      let document = try await viewer.open(Fixture.data("report"))
      try await viewer.annotations(for: document).refresh()
      return (viewer, document, try await viewer.redaction(for: document), try await viewer.search(for: document))
    }

    @Test func markingAnAreaIsPendingAndObserved() async throws {
      let (viewer, document, redaction, _) = try await opened()
      let page = try await viewer.pages(of: document)[0].page
      let events = redaction.events
      let mark = try await redaction.markArea(Rect(x: 72, y: 72, width: 200, height: 20), on: page)
      let pending = try await redaction.pending(on: page)
      expectNoDifference(pending, [mark])
      for await event in events {
        if case .pendingChanged = event { break }
      }
      _ = try await redaction.unmark([mark.annotationRef])
      let after = try await redaction.pendingCount(on: page)
      expectNoDifference(after, 0)
    }

    @Test func applyingMatchesRemovesTheText() async throws {
      let (_, _, redaction, search) = try await opened()
      #expect(try await redaction.canApply())
      let before = try await search.findAll(SearchQuery(text: "Report"))
      let page = try #require(before.first?.page)
      let marks = try await redaction.markMatches(SearchQuery(text: "Report"), on: [page])
      #expect(!marks.isEmpty)
      let result = try await redaction.apply(pages: [page])
      expectNoDifference(result.results.first?.status, .applied)
      let after = try await search.findAll(SearchQuery(text: "Report"))
      #expect(after.allSatisfy { $0.page != page })
    }

    @Test func aClosedDocumentsCapabilityThrows() async throws {
      let (viewer, document, redaction, _) = try await opened()
      try await viewer.close(document)
      await #expect {
        _ = try await redaction.pending()
      } throws: { error in
        let error = error as? PDFError
        return error?.code == .instanceClosed && error?.name == "PluginError"
      }
    }

    @Test func droppingTheStreamUnsubscribes() async throws {
      let (_, _, redaction, _) = try await opened()
      let task = Task { for await _ in redaction.events {} }
      try await Task.sleep(for: .milliseconds(20))
      task.cancel()
      _ = await task.value
      _ = try await redaction.pending()
    }

    @Test func applyAllCancelsWhenTheTaskIsCancelled() async throws {
      let (_, _, redaction, _) = try await opened()
      let task = Task { try await redaction.applyAll() }
      task.cancel()
      do {
        _ = try await task.value
      } catch {
        expectNoDifference((error as? PDFError)?.code, .operationCancelled)
      }
      _ = try await redaction.pending()
    }

    @Test func markMatchesCancelsWhenTheTaskIsCancelled() async throws {
      let (_, _, redaction, _) = try await opened()
      let task = Task { try await redaction.markMatches(SearchQuery(text: "e")) }
      task.cancel()
      do {
        _ = try await task.value
      } catch {
        expectNoDifference((error as? PDFError)?.code, .operationCancelled)
      }
      _ = try await redaction.pending()
    }

    @Test func markMatchesStopsWhenCancelledMidCall() async throws {
      let (_, _, redaction, search) = try await opened()
      // Every hit of "e" on report.pdf: an uncancelled mark makes one per
      // hit, which takes far longer than the cancel below allows.
      let hits = try await search.findAll(SearchQuery(text: "e"), snippets: .never).count
      try #require(hits > 100, "only \(hits) hits")
      let clock = ContinuousClock()
      let start = clock.now
      let task = Task { try await redaction.markMatches(SearchQuery(text: "e")) }
      try await Task.sleep(for: .milliseconds(300))
      task.cancel()
      let marks: [RedactionMark]?
      do {
        marks = try await task.value
      } catch {
        expectNoDifference((error as? PDFError)?.code, .operationCancelled)
        marks = nil
      }
      let elapsed = clock.now - start
      #expect(elapsed < .seconds(5), "the cancel took \(elapsed)")
      // A cancel before the search finds a hit returns no marks rather than
      // throwing, which a slow runner can hit; either way it never marks
      // every hit.
      if let marks { #expect(marks.count < hits, "\(marks.count) of \(hits) marked") }
      let pending = try await redaction.pendingCount()
      #expect(pending < hits)
    }

    // One per member that takes a `CancelToken`, beyond `applyAll` and
    // `markMatches` above. Each is called in an already-cancelled task.

    /// A pending mark on the first page.
    func marked() async throws -> (RedactionCapability, AnnotationRef, PageRef) {
      let (viewer, document, redaction, _) = try await opened()
      let page = try await viewer.pages(of: document)[0].page
      let mark = try await redaction.markArea(Rect(x: 72, y: 72, width: 200, height: 20), on: page)
      return (redaction, mark.annotationRef, page)
    }

    @Test func markSelectionCancelsWhenTheTaskIsCancelled() async throws {
      let (redaction, _, _) = try await marked()
      await expectCancelledBeforeItStarts { try await redaction.markSelection() }
    }

    @Test func markAreaCancelsWhenTheTaskIsCancelled() async throws {
      let (redaction, _, page) = try await marked()
      await expectCancelledBeforeItStarts {
        try await redaction.markArea(Rect(x: 72, y: 200, width: 50, height: 20), on: page)
      }
      let count = try await redaction.pendingCount(on: page)
      expectNoDifference(count, 1)
    }

    @Test func markPageCancelsWhenTheTaskIsCancelled() async throws {
      let (redaction, _, page) = try await marked()
      await expectCancelledBeforeItStarts { try await redaction.markPage(page) }
    }

    @Test func unmarkCancelsWhenTheTaskIsCancelled() async throws {
      let (redaction, mark, page) = try await marked()
      await expectCancelledBeforeItStarts { try await redaction.unmark([mark]) }
      let count = try await redaction.pendingCount(on: page)
      expectNoDifference(count, 1)
    }

    @Test func clearPendingCancelsWhenTheTaskIsCancelled() async throws {
      let (redaction, _, _) = try await marked()
      await expectCancelledBeforeItStarts { try await redaction.clearPending() }
    }

    @Test func updateLabelCancelsWhenTheTaskIsCancelled() async throws {
      let (redaction, mark, _) = try await marked()
      await expectCancelledBeforeItStarts {
        try await redaction.updateLabel(mark, RedactionLabelPatch(overlayText: "X"))
      }
    }

    @Test func applyCancelsWhenTheTaskIsCancelled() async throws {
      let (redaction, mark, page) = try await marked()
      await expectCancelledBeforeItStarts { try await redaction.apply([mark]) }
      let count = try await redaction.pendingCount(on: page)
      expectNoDifference(count, 1)
    }

    @Test func applyPagesCancelsWhenTheTaskIsCancelled() async throws {
      let (redaction, _, page) = try await marked()
      await expectCancelledBeforeItStarts { try await redaction.apply(pages: [page]) }
    }

    @Test func markingTheSelectionCreatesOneMarkPerPage() async throws {
      let viewer = try PDFViewer(
        ViewerConfig(interaction: true, selection: true, annotation: "", redaction: RedactionConfig()))
      try await viewer.start()
      let document = try await viewer.open(Fixture.data("report"))
      try await viewer.annotations(for: document).refresh()
      let page = try await viewer.pages(of: document)[0].page
      let selection = try await viewer.selection(for: document)
      try await selection.ensureLoaded(page)
      try await selection.selectPage(page)
      let marks = try await viewer.redaction(for: document).markSelection()
      expectNoDifference(marks.count, 1)
    }
  }
}
