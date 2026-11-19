import EmbedPDF
import EmbedPDFUI
import SwiftUI

struct DocumentView: View {
  @Bindable var model: DocumentModel
  @State private var showingBookmarks = false
  @State private var showingAttachments = false

  var body: some View {
    NavigationStack {
      PDFReaderView(model: model.reader) { model.linkActivated($0) }
        // Names the reader for the UI tests. An identifier on the reader
        // view itself would replace the ones its search bar and password
        // prompt carry.
        .background {
          Color.clear
            .accessibilityElement()
            .accessibilityAddTraits(.isImage)
            .accessibilityIdentifier("pages")
        }
        // Floats over the reader: a label that changed the reader's height
        // would resize the viewport while the link's reveal is animating.
        .overlay(alignment: .bottom) {
          if let activation = model.linkActivation {
            Text("Link: \(String(describing: activation))")
              .font(.caption).lineLimit(1)
              .padding(.horizontal, 12).padding(.vertical, 6)
              .background(.regularMaterial, in: Capsule())
              .padding(8)
              .allowsHitTesting(false)
              .accessibilityIdentifier("linkActivation")
          }
        }
        .safeAreaInset(edge: .bottom) { controls }
        .navigationTitle(model.session?.document.name ?? "EmbedPDF")
        .navigationBarTitleDisplayMode(.inline)
        .toolbar { toolbar }
    }
    .task { await model.task() }
    .task(id: model.session?.document.id) { await model.sessionChanged() }
    .fileImporter(isPresented: $model.isImporting, allowedContentTypes: [.pdf]) { result in
      Task { await model.fileImported(result) }
    }
    .alert("Something went wrong", isPresented: $model.error.isPresented, presenting: model.error) { _ in
      Button("OK") { model.errorDismissed() }
    } message: { error in
      Text(error.detail)
    }
    .sheet(isPresented: $showingBookmarks) {
      if let panels = model.panels {
        NavigationStack {
          OutlinePanel(model: panels.outline)
            .toolbar { ToolbarItem(placement: .confirmationAction) { Button("Done") { showingBookmarks = false } } }
        }
      }
    }
    .sheet(isPresented: $showingAttachments) {
      if let panels = model.panels {
        NavigationStack {
          AttachmentPanel(model: panels.attachments)
            .toolbar { ToolbarItem(placement: .confirmationAction) { Button("Done") { showingAttachments = false } } }
        }
      }
    }
    .sheet(item: $model.metadataSheet) { sheet in
      MetadataView(metadata: sheet.metadata, custom: sheet.custom)
    }
  }

  /// The tool picker and the history buttons, under the reader. The reader view places no tool picker of its own.
  @ViewBuilder
  private var controls: some View {
    if let session = model.session {
      VStack(alignment: .leading, spacing: 8) {
        if let tools = session.tools {
          ToolPicker(model: tools, tools: [.pointer, .ink]) { id in
            id == .ink ? Label("Ink", systemImage: "pencil.tip") : Label("Select", systemImage: "hand.point.up.left")
          }
        }
        if let panels = model.panels, panels.document.id == session.document.id {
          HistoryControls(model: panels.history)
        }
      }
      .frame(maxWidth: .infinity, alignment: .leading)
      .padding([.horizontal, .top])
      .background(.bar)
    }
  }

  @ToolbarContentBuilder
  private var toolbar: some ToolbarContent {
    ToolbarItem(placement: .topBarLeading) {
      Menu {
        Button("Open from Files…", systemImage: "folder") { model.openButtonTapped() }
        Button("Sample: report.pdf") { Task { await model.sampleButtonTapped("report") } }
        Button("Sample: encrypted-hello.pdf (hôtel)") { Task { await model.sampleButtonTapped("encrypted-hello") } }
      } label: {
        Label("Open", systemImage: "doc.badge.plus")
      }
    }
    ToolbarItem(placement: .topBarTrailing) {
      Button("Add note", systemImage: "note.text.badge.plus") { Task { await model.addNoteButtonTapped() } }
        .labelStyle(.iconOnly)
        .disabled(!model.canAddNote)
        .accessibilityIdentifier("addNote")
    }
    ToolbarItem(placement: .topBarTrailing) {
      Button("Bookmarks", systemImage: "bookmark") { showingBookmarks = true }
        .disabled(model.panels == nil)
        .accessibilityIdentifier("showBookmarks")
    }
    ToolbarItem(placement: .topBarTrailing) {
      Button("Attachments", systemImage: "paperclip") { showingAttachments = true }
        .disabled(model.panels == nil)
        .accessibilityIdentifier("showAttachments")
    }
    ToolbarItem(placement: .topBarTrailing) {
      Button("Info", systemImage: "info.circle") { Task { await model.metadataButtonTapped() } }
        .disabled(model.panels == nil)
    }
  }
}

extension Optional {
  fileprivate var isPresented: Bool {
    get { self != nil }
    set {
      guard !newValue else { return }
      self = nil
    }
  }
}
