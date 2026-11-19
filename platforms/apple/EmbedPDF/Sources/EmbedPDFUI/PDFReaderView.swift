import EmbedPDF
import SwiftUI

/// A reader in one view: it draws a `PDFReaderModel`'s phase.
///
///     @State private var model = PDFReaderModel()
///
///     PDFReaderView(model: model)
///       .task { await model.open(data, name: "report.pdf") }
///
/// Nothing shows while the model is idle and a progress view while it loads.
/// A locked document shows a password prompt that calls
/// `PDFReaderModel.unlock(password:)`, with the reason below the field after
/// a wrong password. A failure shows the error's detail. A ready document
/// shows a `StageReaderView`, with a `SearchBar` above it when the model has
/// `.search`; the reader scrolls to each hit the bar reports active, and
/// not again when the view comes back with the same hit.
///
/// Touch on a ready document divides this way. A drag pans, a pinch zooms
/// and a double tap zooms. With `.selection`, a long press and then a drag
/// selects text. A tap follows a link with `.links`, and clears the text
/// selection with `.selection`. While a tool other than the default is
/// active (`.tools`), a drag goes to that tool instead of the pan and text
/// selection is off.
///
/// With `.selection` the view sets the reader's `panBeginsOnMovement`, so a
/// pan begins once the finger moves, and a still touch or a tap does not
/// stop a fling. Without `.selection` a touch stops one where it lands.
///
/// The view shows no tool picker: which tools to offer is the app's choice.
/// Give `Session.tools` to a `ToolPicker`. The view runs the reader's and
/// the search bar's `task()`; an app does not run them again.
@MainActor
public struct PDFReaderView<Overlay: View>: View {
  let model: PDFReaderModel
  let pageOverlay: (StageVisiblePage, PDFReaderModel.Session) -> Overlay
  let onLinkActivation: @MainActor (LinkActivation) -> Void

  /// A reader that lays `pageOverlay` over each visible page, above the
  /// text selection, in the page's frame with its origin at the page's top
  /// left. The overlay's own gestures take a touch before the reader's drag
  /// and before text selection.
  ///
  /// `onLinkActivation` receives each link activation while a document
  /// shows. Opening a `.uri` is the app's job.
  public init(
    model: PDFReaderModel,
    @ViewBuilder pageOverlay: @escaping (StageVisiblePage, PDFReaderModel.Session) -> Overlay,
    onLinkActivation: @escaping @MainActor (LinkActivation) -> Void = { _ in }
  ) {
    self.model = model
    self.pageOverlay = pageOverlay
    self.onLinkActivation = onLinkActivation
  }

  public var body: some View {
    switch model.phase {
    case .idle:
      Color.clear
    case .loading:
      ProgressView()
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityLabel("Opening document")
        .accessibilityIdentifier("readerLoading")
    case let .locked(document):
      // Keyed to the document, so a second locked document starts with an
      // empty field.
      PDFPasswordPrompt(model: model, document: document).id(document.id)
    case let .ready(session):
      ready(session)
    case let .failed(error):
      VStack(spacing: 8) {
        Text("The document could not be opened").font(.headline)
        Text(error.detail).font(.callout).foregroundStyle(.secondary)
          .accessibilityIdentifier("readerFailureDetail")
      }
      .multilineTextAlignment(.center)
      .padding()
      .frame(maxWidth: .infinity, maxHeight: .infinity)
      .accessibilityElement(children: .combine)
      .accessibilityIdentifier("readerFailure")
    }
  }

  private func ready(_ session: PDFReaderModel.Session) -> some View {
    VStack(spacing: 0) {
      if let search = session.search {
        RevealingSearchBar(model: search).padding(8)
      }
      StageReaderView(
        model: session.reader,
        pageOverlay: { page in
          ZStack(alignment: .topLeading) {
            // Absent under a tool other than the default, whose drags the
            // reader forwards: the long press would take them from it.
            if let selection = session.selection, !session.reader.forwardsDrags,
              let info = session.pageInfo(for: page)
            {
              TextSelectionOverlay(model: selection, visiblePage: page, info: info, activation: .longPress)
            }
            pageOverlay(page, session)
          }
        },
        onLinkActivation: onLinkActivation)
        // The long press above delays the reader's drag to the first move
        // or the lift, so a tap's drag must not open a stage gesture: it
        // would cancel the double tap's zoom or the link's scroll that the
        // same tap started.
        .task(id: ObjectIdentifier(session.reader)) {
          session.reader.panBeginsOnMovement = session.selection != nil
        }
    }
  }
}

extension PDFReaderView where Overlay == EmptyView {
  /// A reader with no page overlay. `onLinkActivation` receives each link
  /// activation while a document shows.
  public init(model: PDFReaderModel, onLinkActivation: @escaping @MainActor (LinkActivation) -> Void = { _ in }) {
    self.init(model: model, pageOverlay: { _, _ in EmptyView() }, onLinkActivation: onLinkActivation)
  }
}

/// A `SearchBar` that brings each hit it reports active into view, and
/// reveals at no other time.
@MainActor
private struct RevealingSearchBar: View {
  let model: SearchModel
  /// The reported hit whose reveal has not finished.
  @State private var reveals = SearchHitReveals()

  var body: some View {
    SearchBar(model: model) { _ in reveals.hitReported() }
      // One reveal waits at a time: a newer report cancels the wait for the
      // last one's reveal and starts its own. The task also runs when the
      // view appears again, and then finds nothing waiting.
      //
      // Performance: the engine already reveals on a next or previous step,
      // so each step the bar reports makes a second reveal call with the
      // same target and settings; only the first hit of a new search needs
      // this one. A search event that tells a scan's first hit from a step
      // lets the view skip the repeat.
      .task(id: reveals.waiting) {
        guard let request = reveals.waiting else { return }
        await model.revealActiveHit()
        reveals.finished(request)
      }
  }
}

/// The password field and unlock button a locked document shows.
@MainActor
private struct PDFPasswordPrompt: View {
  let model: PDFReaderModel
  let document: DocumentInfo
  @State private var password = ""
  /// Whether an unlock is on its way to the engine.
  @State private var submitting = false

  var body: some View {
    VStack(spacing: 12) {
      Text(document.name.map { "\($0) is password protected" } ?? "This document is password protected")
        .font(.headline)
      SecureField("Password", text: $password)
        .textFieldStyle(.roundedBorder)
        .textContentType(.password)
        .submitLabel(.go)
        .onSubmit(submit)
        .frame(maxWidth: 320)
        .accessibilityLabel("Document password")
        .accessibilityIdentifier("readerPassword")
      Button("Unlock", action: submit)
        .disabled(password.isEmpty || submitting)
        .accessibilityIdentifier("readerUnlock")
      if let error = model.unlockError {
        Text(error.detail).font(.caption).foregroundStyle(.red)
          .accessibilityIdentifier("readerUnlockError")
      }
    }
    .multilineTextAlignment(.center)
    .padding()
    .frame(maxWidth: .infinity, maxHeight: .infinity)
  }

  private func submit() {
    guard !password.isEmpty, !submitting else { return }
    submitting = true
    Task {
      await model.unlock(password: password)
      submitting = false
    }
  }
}
