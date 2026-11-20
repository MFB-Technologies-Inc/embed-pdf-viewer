import CoreGraphics
import EmbedPDF
import Observation

/// An observable model of a document's page thumbnails, which a view
/// reads, each turned to the page as shown.
///
/// ``load(_:)`` renders a page with `RenderCapability.renderThumbnail`, which
/// takes no size: the engine draws the whole page 512 pixels on its long
/// side, so neither the model nor a view picks the pixel size. The engine's
/// thumbnail is unrotated; the model turns it with `PageGeometry`, using the
/// rotation `pages` reports. `pages` is a snapshot. After a page edit that
/// rotates, inserts or removes pages, make a new model from the new page
/// list.
///
/// The model keeps its own `RenderRevisions` over `render`, so it observes
/// invalidations from `init` until the document closes or the model is
/// released, with no task to run. A thumbnail is cached with the page's
/// render revision at the time its render started. ``image(for:)`` keeps
/// answering with that image after the page is invalidated, so a cell shows
/// the old thumbnail until the next ``load(_:)`` replaces it.
///
/// The cache holds the 128 pages used most recently, where a use is a
/// ``load(_:)`` call. A thumbnail is up to 1 MiB of pixels (512 by 512 at 4
/// bytes each), so the cache stays within about 128 MiB however long the
/// document is. A page dropped from the cache has no image until it is
/// loaded again. Nothing loads it by itself: a cell still on screen when its
/// page is dropped, as can happen when two views share one model, shows no
/// image until something calls ``load(_:)`` for it.
///
/// ``error`` is the latest failed load. It clears when the page it is for
/// loads; another page's thumbnail leaves it. The failure for a page that is
/// not in `pages` therefore never clears, since that page cannot load in
/// this model. A failed load leaves the page's earlier image, if it has one,
/// in ``image(for:)``, and the next ``load(_:)`` of that page renders again.
/// A cancelled load is not a failure.
@MainActor
@Observable
public final class ThumbnailModel {
  public private(set) var error: PDFError?
  // Performance: one observed dictionary holds every thumbnail, so each
  // image that lands evaluates again every view that read `image(for:)`,
  // not only that page's cell. It bites while a strip scrolls and many
  // cells load at once. An observable box per page limits a landing image
  // to its own cell.
  private var thumbnails: [PageRef: Thumbnail] = [:]
  /// Cached pages, least recently used first.
  @ObservationIgnored private var recency: [PageRef] = []
  @ObservationIgnored private var loads: [PageRef: Load] = [:]
  @ObservationIgnored private var lastLoadID = 0
  /// The page ``error`` is for.
  @ObservationIgnored private var failedPage: PageRef?
  @ObservationIgnored private let render: RenderCapability
  @ObservationIgnored private let geometries: [PageRef: PageGeometry]
  @ObservationIgnored private let revisions: RenderRevisions
  /// How many thumbnails the cache keeps. Tests lower it.
  // Performance: the cache keeps each thumbnail at the engine's 512 pixels,
  // up to about 128 MiB in all, and nothing drops them under memory
  // pressure. A strip draws a cell 96 points across, 288 pixels at 3x, so
  // much of that is never shown. It bites on a long document on a device
  // short of memory. Scaling each image down to the drawn size before
  // caching it, or emptying the cache on a memory warning, cuts it.
  @ObservationIgnored var cacheLimit = 128
  /// How many renders have started. Tests read it.
  @ObservationIgnored var renderCount = 0
  /// Runs when a render has started, before it reaches the engine. Tests
  /// hold a render open with it.
  @ObservationIgnored var renderStarted: (@MainActor () async -> Void)?
  /// How many ``load(_:)`` calls wait on the page's render. Tests read it.
  func waiterCount(for page: PageRef) -> Int { loads[page]?.waiters ?? 0 }

  private struct Thumbnail {
    let image: CGImage
    let revision: UInt64
  }

  /// One render in flight and the number of ``load(_:)`` calls waiting on it.
  private struct Load {
    let id: Int
    let revision: UInt64
    let task: Task<Void, Never>
    var waiters: Int
  }

  /// - Parameters:
  ///   - render: Render capability for the open document.
  ///   - pages: The document's pages, which give each thumbnail its turn.
  public init(render: RenderCapability, pages: [PageInfo]) {
    self.render = render
    geometries = Dictionary(pages.map { ($0.page, $0.geometry) }, uniquingKeysWith: { _, last in last })
    revisions = RenderRevisions(render: render)
  }

  /// The page's cached thumbnail as shown, `nil` until a load of it lands.
  public func image(for page: PageRef) -> CGImage? { thumbnails[page]?.image }

  /// The page's render revision, which rises each time the engine
  /// invalidates the page. A view keys a cell's load on it, so an edit
  /// loads that cell again.
  public func revision(for page: PageRef) -> UInt64 { revisions.revision(for: page) }

  /// Renders the page's thumbnail unless the cache holds one for the page's
  /// current render revision, and returns when that render ends.
  ///
  /// Calls for one page at one revision share a single render, and each
  /// returns when it ends: with the image cached, with ``error`` set, or
  /// with neither when the render was cancelled or superseded. The render is
  /// cancelled when every call waiting on it is cancelled, or when a call
  /// arrives after the page's revision has moved on; the later call's render
  /// supersedes it. Cancelling one call does not return it early. It gives
  /// up its claim on the render and still returns when the render ends,
  /// which is when the image lands if another call is still waiting. A page
  /// that is not in the model's `pages` sets ``error`` to `not-found`
  /// without rendering.
  public func load(_ page: PageRef) async {
    guard !Task.isCancelled else { return }
    guard let geometry = geometries[page] else {
      error = PDFError(.notFound, detail: "The page is not among the thumbnail model's pages.")
      failedPage = page
      return
    }
    let revision = revisions.revision(for: page)
    if thumbnails[page]?.revision == revision {
      markUsed(page)
      return
    }
    if loads[page]?.revision != revision {
      loads[page]?.task.cancel()
      lastLoadID += 1
      let id = lastLoadID
      loads[page] = Load(
        id: id, revision: revision,
        task: Task { [weak self] in
          await self?.render(page, geometry: geometry, revision: revision, id: id)
        },
        waiters: 0)
    }
    guard let load = loads[page] else { return }
    loads[page]?.waiters += 1
    await withTaskCancellationHandler {
      await load.task.value
    } onCancel: {
      // Performance: one extra task per cancelled wait, because the handler
      // runs off the main actor. It bites on a fast scroll, where each cell
      // that leaves cancels its load. A lock around the waiter count lets
      // the handler cancel the render without a task.
      Task { @MainActor [weak self] in self?.stopWaiting(for: page, id: load.id) }
    }
  }

  /// Cancels the render once no call waits on it. A load that has finished
  /// or been superseded is gone from `loads`, so this leaves it alone.
  private func stopWaiting(for page: PageRef, id: Int) {
    guard var load = loads[page], load.id == id else { return }
    load.waiters -= 1
    if load.waiters > 0 {
      loads[page] = load
    } else {
      load.task.cancel()
      loads[page] = nil
    }
  }

  private func render(_ page: PageRef, geometry: PageGeometry, revision: UInt64, id: Int) async {
    renderCount += 1
    await renderStarted?()
    do {
      let rendered = try await render.renderThumbnail(page)
      // A cancelled or superseded load no longer owns the page.
      guard loads[page]?.id == id else { return }
      // Performance: for a rotated page this draws the thumbnail into a new
      // bitmap, up to 512 by 512 pixels, on the main actor. It bites when a
      // strip of rotated pages loads during a scroll. The turn can run off
      // the main actor, since `CGImage` is immutable.
      let image = try geometry.toDisplay(rendered.image)
      loads[page] = nil
      thumbnails[page] = Thumbnail(image: image, revision: revision)
      markUsed(page)
      if failedPage == page {
        error = nil
        failedPage = nil
      }
    } catch {
      guard loads[page]?.id == id else { return }
      loads[page] = nil
      self.error = error
      failedPage = page
    }
  }

  /// Moves a cached page to the most recent end and drops the pages over
  /// the limit, oldest first.
  private func markUsed(_ page: PageRef) {
    guard thumbnails[page] != nil else { return }
    // Performance: a linear scan of at most `cacheLimit` pages per load. A
    // linked list with a dictionary of its nodes makes it constant if the
    // limit grows.
    if let index = recency.firstIndex(of: page) { recency.remove(at: index) }
    recency.append(page)
    while recency.count > max(cacheLimit, 1) {
      thumbnails[recency.removeFirst()] = nil
    }
  }
}
