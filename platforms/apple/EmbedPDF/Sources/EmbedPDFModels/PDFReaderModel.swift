import EmbedPDF
import Foundation
import Observation

/// A reader in one model: it starts a viewer with the plugins its
/// ``Features`` need, opens one document at a time and builds the models a
/// reader view shows. `PDFReaderView` (EmbedPDFUI) draws ``phase``.
///
///     let model = PDFReaderModel()
///     await model.open(data, name: "report.pdf")
///
/// ``open(_:name:password:)``, ``open(_:name:)``, ``unlock(password:)`` and
/// ``close()`` run one at a time, in the order they are called, and none of
/// them throws: a failure is ``Phase/failed(_:)``. A call made while an
/// earlier `open` is still loading supersedes it. The earlier `open` then
/// returns without touching ``phase``, and ``phase`` follows the later call.
///
/// The model owns its viewer. ``close()`` destroys it and returns when the
/// engine has shut down, unless an open made while it waited superseded it.
/// A model released without ``close()`` releases the
/// viewer, which shuts the engine down once nothing else holds it: a
/// ``Session`` the app kept, or a capability taken from one, keeps the viewer
/// alive until it is released too.
@MainActor
@Observable
public final class PDFReaderModel {
  /// What the reader offers beyond showing and scrolling pages. Each feature
  /// installs the plugins it needs and nothing else.
  public struct Features: OptionSet, Sendable {
    public let rawValue: Int

    public init(rawValue: Int) { self.rawValue = rawValue }

    /// Text search: ``Session/search``.
    public static let search = Features(rawValue: 1 << 0)
    /// Text selection: ``Session/selection``.
    public static let selection = Features(rawValue: 1 << 1)
    /// The interaction tools, the annotation tools included:
    /// ``Session/tools``. A drag under a tool other than the default goes to
    /// that tool, and the built-in pan tool scrolls.
    public static let tools = Features(rawValue: 1 << 2)
    /// Links: a tap on one activates it, and the outcome arrives on the
    /// session reader's `linkActivations`.
    public static let links = Features(rawValue: 1 << 3)
    public static let all: Features = [.search, .selection, .tools, .links]

    /// Whether any of these features needs the pointer hub.
    var needsInteraction: Bool { !isDisjoint(with: [.selection, .tools, .links]) }

    /// The plugins these features need, over the render and stage plugins
    /// every reader has.
    var config: ViewerConfig {
      var builder = ViewerConfigBuilder().render(RenderConfig()).stage(StageConfig())
      if needsInteraction { builder = builder.interaction() }
      if contains(.search) { builder = builder.search(SearchConfig()) }
      if contains(.selection) { builder = builder.selection() }
      if contains(.tools) { builder = builder.annotation() }
      if contains(.links) { builder = builder.link() }
      return builder.build()
    }
  }

  /// Where the reader stands.
  public enum Phase {
    /// Nothing is open: before the first open and after ``close()``.
    case idle
    /// An open is under way.
    case loading
    /// The document needs a password; ``unlock(password:)`` takes it.
    case locked(DocumentInfo)
    case ready(Session)
    /// The open, or what followed it, failed. Nothing is left open, and the
    /// next open starts again.
    case failed(PDFError)
  }

  /// One open document and the models over it. A session belongs to that
  /// document: the next open, and ``close()``, replace it with a new value
  /// and never change this one. Its models are reference types that stop
  /// working when the document closes, so read the session from
  /// ``PDFReaderModel/phase`` each time instead of keeping one.
  public struct Session {
    public let viewer: PDFViewer
    public let document: DocumentInfo
    /// The document's pages as they were when it opened.
    public let pages: [PageInfo]
    public let reader: StageReaderModel
    /// `nil` without ``Features/search``.
    public let search: SearchModel?
    /// `nil` without ``Features/selection``.
    public let selection: TextSelectionModel?
    /// `nil` without ``Features/tools``.
    public let tools: ToolModel?

    /// The `PageInfo` of a page the stage shows, or `nil` for a page added
    /// after the document opened.
    public func pageInfo(for visiblePage: StageVisiblePage) -> PageInfo? {
      let index = Int(visiblePage.pageIndex)
      if pages.indices.contains(index), pages[index].page == visiblePage.page { return pages[index] }
      // A page edit moved the page since the document opened. This scans
      // every page once per call, and a reader calls it per visible page
      // per redraw; a dictionary by `PageRef` built with the session makes
      // it constant if documents whose pages move grow large.
      return pages.first { $0.page == visiblePage.page }
    }
  }

  /// The features the model was made with.
  public let features: Features
  public private(set) var phase = Phase.idle
  /// Why the last ``unlock(password:)`` left the document locked: a wrong
  /// password is `permission-denied`. `nil` once an unlock succeeds and
  /// after the next open or ``close()``.
  public private(set) var unlockError: PDFError?
  /// The running viewer: made by the first open, destroyed by ``close()``.
  @ObservationIgnored private(set) var viewer: PDFViewer?
  /// Orders the opens, the unlocks and the close, so one document's close
  /// never lands between the next one's open and its reads.
  @ObservationIgnored private let calls = SerialCalls()
  /// Bumped by each open and each close. A call that finds it changed after
  /// a suspension has been superseded and leaves ``phase`` alone.
  @ObservationIgnored private var generation = 0
  /// The newest open's or close's work, cancelled when the next supersedes
  /// it so a URL still downloading stops.
  @ObservationIgnored private var newest: Task<Void, Never>?

  private enum Source: Sendable {
    case data(Data, name: String?, password: String?)
    case url(URL, name: String?)
  }

  public init(features: Features = .all) {
    self.features = features
  }

  /// Opens PDF bytes, closing the document that was open. Ends in
  /// ``Phase/ready(_:)``, in ``Phase/locked(_:)`` for a protected document
  /// opened with no password or a wrong one, or in ``Phase/failed(_:)``, as
  /// for bytes that are not a PDF.
  public func open(_ data: Data, name: String? = nil, password: String? = nil) async {
    await load(.data(data, name: name, password: password))
  }

  /// Opens the PDF at `url`, a file or a remote address, closing the
  /// document that was open. Ends as ``open(_:name:password:)`` does; a
  /// download that fails ends in ``Phase/failed(_:)``.
  public func open(_ url: URL, name: String? = nil) async {
    await load(.url(url, name: name))
  }

  /// Unlocks the document ``Phase/locked(_:)`` holds. A wrong password
  /// leaves it locked with the reason in ``unlockError``; any other failure
  /// ends in ``Phase/failed(_:)``. Does nothing unless ``phase`` is
  /// ``Phase/locked(_:)``.
  public func unlock(password: String) async {
    guard case .locked = phase else { return }
    let mine = generation
    await calls.enqueue { await self.applyUnlock(password, as: mine) }.value
  }

  /// Closes the document and destroys the viewer. ``phase`` is
  /// ``Phase/idle`` at once, and the engine has shut down when this returns
  /// unless an open made before the close ran superseded it: the close then
  /// returns with the viewer still running, and that open uses it. An open
  /// made after the close has run starts a new viewer.
  public func close() async {
    let mine = supersede()
    phase = .idle
    await run { await self.applyClose(as: mine) }
  }

  private func load(_ source: Source) async {
    let mine = supersede()
    // Drops the session before its document closes, so no view draws from a
    // closed document.
    phase = .loading
    await run { await self.applyOpen(source, as: mine) }
  }

  /// Makes the calls before this one stale and returns this one's
  /// generation.
  private func supersede() -> Int {
    generation &+= 1
    newest?.cancel()
    unlockError = nil
    return generation
  }

  private func run(_ work: @escaping @MainActor @Sendable () async -> Void) async {
    let task = calls.enqueue(work)
    newest = task
    await task.value
  }

  private func applyOpen(_ source: Source, as mine: Int) async {
    guard generation == mine else { return }
    do {
      let viewer = try await startedViewer()
      // The document open before, and a failed tab an earlier open left.
      try await viewer.closeAll()
      guard generation == mine else { return }
      let document: DocumentInfo
      switch source {
      case let .data(data, name, password):
        document = try await viewer.open(data, name: name, password: password)
      case let .url(url, name):
        document = try await viewer.open(url, name: name)
      }
      // The call that superseded this one closes what it opened.
      guard generation == mine else { return }
      try await settle(document, in: viewer, as: mine)
    } catch {
      await fail(error, as: mine)
    }
  }

  private func applyUnlock(_ password: String, as mine: Int) async {
    // An earlier unlock may have opened the document already.
    guard generation == mine, case let .locked(document) = phase, let viewer else { return }
    if unlockError != nil { unlockError = nil }
    do {
      try await viewer.unlock(document, password: password)
    } catch {
      // A wrong password leaves the tab locked, ready for another try.
      if error.code != .permissionDenied {
        await fail(error, as: mine)
      } else if generation == mine {
        unlockError = error
      }
      return
    }
    guard generation == mine else { return }
    do {
      guard let unlocked = try await viewer.document(id: document.id) else {
        await fail(PDFError(.notFound, detail: "the unlocked document is no longer open"), as: mine)
        return
      }
      guard generation == mine else { return }
      try await settle(unlocked, in: viewer, as: mine)
    } catch {
      await fail(error, as: mine)
    }
  }

  private func applyClose(as mine: Int) async {
    guard generation == mine, let viewer else { return }
    self.viewer = nil
    // Closing has no way to report a failure, so one is dropped. The viewer
    // is released here, and its release shuts the engine down, unless the
    // app kept a `Session` or a capability from one: those hold the viewer,
    // so a failed destroy then leaves the engine running until they go.
    try? await viewer.destroy()
  }

  /// The viewer, made and started on first use.
  private func startedViewer() async throws(PDFError) -> PDFViewer {
    if let viewer { return viewer }
    let viewer = try PDFViewer(features.config)
    try await viewer.start()
    self.viewer = viewer
    return viewer
  }

  /// Moves to the phase `document`'s status calls for.
  private func settle(_ document: DocumentInfo, in viewer: PDFViewer, as mine: Int) async throws(PDFError) {
    switch document.status {
    case .locked:
      phase = .locked(document)
    case .ready:
      let session = try await session(for: document, in: viewer)
      guard generation == mine else { return }
      phase = .ready(session)
    case .loading, .failed:
      throw PDFError(
        document.error.map { PDFError.Code($0.code) } ?? .operationFailed,
        detail: document.error?.message ?? "the document did not finish opening")
    }
  }

  private func session(for document: DocumentInfo, in viewer: PDFViewer) async throws(PDFError) -> Session {
    // Each accessor below is its own trip to the engine, made one after
    // another: up to eight per open with every feature on. They do not
    // depend on each other, so `async let` runs them together if opening
    // shows up in a profile.
    let stage = try await viewer.stage(for: document)
    let render = try await viewer.render(for: document)
    let interaction = features.needsInteraction ? try await viewer.interaction(for: document) : nil
    let link = features.contains(.links) ? try await viewer.link(for: document) : nil
    if features.contains(.tools) {
      // Lets the built-in pan tool drag this reader. The registration lasts
      // as long as the document, which is as long as the session.
      _ = try await stage.registerScrollHandler()
    }
    let search = features.contains(.search) ? SearchModel(search: try await viewer.search(for: document)) : nil
    let selection = features.contains(.selection)
      ? TextSelectionModel(selection: try await viewer.selection(for: document)) : nil
    // Every page's `PageInfo` crosses the bridge here, at each open and
    // after each unlock: a payload that grows with the page count and
    // delays the first frame of a document with thousands of pages. The
    // session needs a page's info only once the stage shows it, so reading
    // `viewer.page(_:in:)` for visible pages and caching by `PageRef`
    // removes the up-front cost.
    let pages = try await viewer.pages(of: document)
    return Session(
      viewer: viewer, document: document, pages: pages,
      reader: StageReaderModel(stage: stage, render: render, interaction: interaction, link: link),
      search: search, selection: selection,
      tools: features.contains(.tools) ? interaction.map { ToolModel(interaction: $0) } : nil)
  }

  /// Ends in ``Phase/failed(_:)`` with nothing left open, unless a later
  /// call has taken over.
  private func fail(_ error: PDFError, as mine: Int) async {
    guard generation == mine else { return }
    // Bytes that are not a PDF leave a failed tab, and a failure after the
    // open leaves the document. A close that fails changes nothing: the
    // next open closes every tab again before it opens.
    try? await viewer?.closeAll()
    guard generation == mine else { return }
    phase = .failed(error)
  }
}
