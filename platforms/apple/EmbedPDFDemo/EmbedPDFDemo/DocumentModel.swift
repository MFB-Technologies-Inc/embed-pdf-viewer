import EmbedPDF
import EmbedPDFUI
import Foundation
import Observation

@MainActor
@Observable
final class DocumentModel {
  /// The inspectors over the open document, built from its session.
  struct Panels {
    let document: DocumentInfo
    let metadata: MetadataCapability
    let history: HistoryModel
    let outline: OutlineModel
    let attachments: AttachmentModel
  }

  struct MetadataSheet: Identifiable {
    let id = UUID()
    let metadata: DocumentMetadata
    let custom: CustomMetadata
  }

  /// Opens, unlocks and shows the document. `PDFReaderView` draws its phase.
  /// The four plugins the reader's features leave out are for the panels.
  let reader = PDFReaderModel(configure: { $0.metadata().history().outline().attachment() })
  private(set) var panels: Panels?
  var error: PDFError?
  private(set) var canAddNote = false
  var isImporting = false
  var metadataSheet: MetadataSheet?
  /// The last link activation the reader reported.
  private(set) var linkActivation: LinkActivation?
  @ObservationIgnored private var started = false

  /// The open document's session, or `nil` in every phase but ready.
  var session: PDFReaderModel.Session? {
    if case let .ready(session) = reader.phase { session } else { nil }
  }

  func task() async {
    guard !started else { return }
    started = true
    if ProcessInfo.processInfo.arguments.contains("--attachment-fixture") {
      do {
        let folder = try FileManager.default.url(for: .documentDirectory, in: .userDomainMask, appropriateFor: nil, create: true)
        try Data("Attachment file picker round trip".utf8).write(to: folder.appendingPathComponent("attachment-fixture.txt"), options: .atomic)
      } catch {
        self.error = PDFError(.operationFailed, detail: error.localizedDescription, name: "FileError")
      }
    }
    await open(bundled: "report")
  }

  func sampleButtonTapped(_ name: String) async {
    await open(bundled: name)
  }

  func openButtonTapped() {
    isImporting = true
  }

  func fileImported(_ result: Result<URL, any Error>) async {
    guard case let .success(url) = result else { return }
    let accessing = url.startAccessingSecurityScopedResource()
    defer { if accessing { url.stopAccessingSecurityScopedResource() } }
    guard let data = try? Data(contentsOf: url) else { return }
    await reader.open(data, name: url.lastPathComponent)
  }

  /// Builds the inspectors for the session the reader now shows, or drops
  /// them when it shows none. The view runs it each time the ready document
  /// changes, which covers an unlock made in the reader's own prompt.
  func sessionChanged() async {
    panels = nil
    canAddNote = false
    linkActivation = nil
    guard let session else { return }
    let viewer = session.viewer
    let document = session.document
    do {
      let built = Panels(
        document: document,
        metadata: try await viewer.metadata(for: document),
        history: HistoryModel(viewer: viewer, history: try await viewer.history(for: document)),
        outline: OutlineModel(viewer: viewer, outline: try await viewer.outline(for: document)),
        attachments: AttachmentModel(viewer: viewer, attachments: try await viewer.attachments(for: document)))
      // A document opened while these were read has its own run.
      guard self.session?.document.id == document.id else { return }
      panels = built
    } catch {
      guard self.session?.document.id == document.id else { return }
      self.error = error
    }
    await refreshCanAddNote()
  }

  func metadataButtonTapped() async {
    guard let panels else { return }
    do {
      try await panels.metadata.refresh()
      try await panels.metadata.refreshCustom()
      if let metadata = try await panels.metadata.snapshot() {
        metadataSheet = MetadataSheet(metadata: metadata, custom: try await panels.metadata.customSnapshot() ?? [:])
      }
    } catch {
      self.error = error
    }
  }

  func addNoteButtonTapped() async {
    guard let session, let page = session.pages.first?.page else { return }
    do {
      _ = try await session.viewer.annotations(for: session.document).create(
        NoteAnnotationDraft(rect: Rect(x: 72, y: 72, width: 24, height: 24), contents: "A note"), on: page)
      error = nil
    } catch { self.error = error }
    await refreshCanAddNote()
  }

  func linkActivated(_ activation: LinkActivation) {
    linkActivation = activation
  }

  func errorDismissed() {
    error = nil
  }

  private func refreshCanAddNote() async {
    guard let session else { return }
    let allowed = (try? await session.viewer.annotations(for: session.document).canCreate()) ?? false
    if self.session?.document.id == session.document.id { canAddNote = allowed }
  }

  private func open(bundled name: String) async {
    guard
      let url = Bundle.main.url(forResource: name, withExtension: "pdf"),
      let data = try? Data(contentsOf: url)
    else { return }
    await reader.open(data, name: "\(name).pdf")
  }
}
