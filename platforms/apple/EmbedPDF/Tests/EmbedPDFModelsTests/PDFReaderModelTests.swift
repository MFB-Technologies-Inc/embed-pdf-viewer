import CustomDump
import EmbedPDF
@testable import EmbedPDFModels
import Foundation
import Testing

@MainActor
@Suite(.serialized, .timeLimit(.minutes(1)))
struct PDFReaderModelTests {
  @Test func opensToReady() async throws {
    let model = PDFReaderModel()
    #expect(Self.isIdle(model))
    await model.open(try pluginFixture(), name: "report.pdf")
    let session = try Self.session(model)
    let pages = try await session.viewer.pages(of: session.document)
    expectNoDifference(session.pages, pages)
    #expect(!session.pages.isEmpty)
    expectNoDifference(session.document.status, .ready)
    #expect(session.search != nil)
    #expect(session.selection != nil)
    #expect(session.tools != nil)
    await model.close()
    #expect(Self.isIdle(model))
    #expect(model.viewer == nil)
  }

  @Test func opensAFileURL() async throws {
    let url = try #require(Bundle.module.url(forResource: "report", withExtension: "pdf", subdirectory: "Fixtures"))
    let model = PDFReaderModel(features: [])
    await model.open(url)
    let session = try Self.session(model)
    #expect(!session.pages.isEmpty)
    await model.close()
  }

  @Test func nonPDFBytesFail() async throws {
    let model = PDFReaderModel()
    await model.open(Data("no".utf8))
    guard case .failed = model.phase else {
      Issue.record("expected .failed, got \(model.phase)")
      return
    }
    // The failed tab the engine leaves behind is closed.
    let left: Int? = try await model.viewer?.documentCount()
    expectNoDifference(left, 0)
    // A failure does not end the model: the next open works.
    await model.open(try pluginFixture())
    _ = try Self.session(model)
    await model.close()
  }

  @Test func anEncryptedDocumentLocksThenUnlocks() async throws {
    let model = PDFReaderModel()
    await model.open(try pluginFixture("encrypted-hello"))
    guard case let .locked(document) = model.phase else {
      Issue.record("expected .locked, got \(model.phase)")
      return
    }
    expectNoDifference(document.status, .locked)
    #expect(model.unlockError == nil)
    await model.unlock(password: "wrong")
    guard case .locked = model.phase else {
      Issue.record("expected .locked after a wrong password, got \(model.phase)")
      return
    }
    expectNoDifference(model.unlockError?.code, .permissionDenied)
    await model.unlock(password: "hôtel")
    let session = try Self.session(model)
    expectNoDifference(session.document.id, document.id)
    expectNoDifference(session.document.status, .ready)
    #expect(!session.pages.isEmpty)
    #expect(model.unlockError == nil)
    await model.close()
  }

  @Test func anEncryptedDocumentOpensWithItsPassword() async throws {
    let model = PDFReaderModel(features: [])
    await model.open(try pluginFixture("encrypted-hello"), password: "hôtel")
    _ = try Self.session(model)
    await model.close()
  }

  @Test func unlockDoesNothingUnlessLocked() async throws {
    let model = PDFReaderModel(features: [])
    await model.unlock(password: "hôtel")
    #expect(Self.isIdle(model))
    await model.open(try pluginFixture())
    let before = try Self.session(model)
    await model.unlock(password: "hôtel")
    expectNoDifference(try Self.session(model).document, before.document)
    await model.close()
  }

  @Test func featuresPickThePlugins() async throws {
    let model = PDFReaderModel(features: [])
    await model.open(try pluginFixture())
    let session = try Self.session(model)
    #expect(session.search == nil)
    #expect(session.selection == nil)
    #expect(session.tools == nil)
    do {
      _ = try await session.viewer.interaction(for: session.document)
      Issue.record("expected no interaction plugin")
    } catch {
      expectNoDifference(error.code, .unsupported)
    }
    await model.close()
  }

  @Test func eachFeatureBringsItsOwnModel() async throws {
    let model = PDFReaderModel(features: [.search, .links])
    await model.open(try pluginFixture())
    let session = try Self.session(model)
    #expect(session.search != nil)
    #expect(session.selection == nil)
    #expect(session.tools == nil)
    _ = try await session.viewer.link(for: session.document)
    await model.close()
  }

  @Test func openingASecondDocumentClosesTheFirst() async throws {
    let model = PDFReaderModel()
    await model.open(try pluginFixture())
    let first = try Self.session(model)
    await model.open(try pluginFixture("features"))
    let second = try Self.session(model)
    #expect(first.viewer === second.viewer)
    #expect(first.document.id != second.document.id)
    let count: Int = try await second.viewer.documentCount()
    expectNoDifference(count, 1)
    #expect(first.reader !== second.reader)
    await model.close()
  }

  @Test func anOpenSupersededByASecondOpenLeavesTheSecond() async throws {
    let model = PDFReaderModel(features: [])
    let report = try pluginFixture()
    let features = try pluginFixture("features")
    let first = Task { await model.open(report, name: "first") }
    try await Self.untilLoading(model)
    await model.open(features, name: "second")
    await first.value
    let session = try Self.session(model)
    expectNoDifference(session.document.name, "second")
    let count: Int = try await session.viewer.documentCount()
    expectNoDifference(count, 1)
    await model.close()
  }

  @Test func anOpenSupersededByCloseEndsIdle() async throws {
    let model = PDFReaderModel(features: [])
    let report = try pluginFixture()
    let opening = Task { await model.open(report) }
    try await Self.untilLoading(model)
    await model.close()
    await opening.value
    #expect(Self.isIdle(model))
    #expect(model.viewer == nil)
  }

  @Test func openShowsLoadingUntilItLands() async throws {
    let model = PDFReaderModel(features: [])
    let report = try pluginFixture()
    let opening = Task { await model.open(report) }
    try await Self.untilLoading(model)
    await opening.value
    _ = try Self.session(model)
    await model.close()
  }

  /// Returns once an `open` started in another task has set `.loading`,
  /// which it does before its first suspension.
  static func untilLoading(_ model: PDFReaderModel) async throws {
    for _ in 0..<1000 {
      if case .loading = model.phase { return }
      await Task.yield()
    }
    Issue.record("expected .loading, got \(model.phase)")
    throw CancellationError()
  }

  static func isIdle(_ model: PDFReaderModel) -> Bool {
    if case .idle = model.phase { true } else { false }
  }

  static func session(_ model: PDFReaderModel) throws -> PDFReaderModel.Session {
    guard case let .ready(session) = model.phase else {
      Issue.record("expected .ready, got \(model.phase)")
      throw CancellationError()
    }
    return session
  }
}
