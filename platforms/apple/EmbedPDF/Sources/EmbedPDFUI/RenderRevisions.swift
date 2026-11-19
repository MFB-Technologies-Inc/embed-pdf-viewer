import EmbedPDF
import Observation

/// A per-page counter that rises each time the engine invalidates a page's
/// render. Views key their page images on it, so an edit redraws only the
/// pages it touched.
@MainActor
@Observable
public final class RenderRevisions {
  private var revisions: [PageRef: UInt64] = [:]
  @ObservationIgnored private let render: RenderCapability
  /// The stream `init` subscribed, held until the first `task()` takes it.
  @ObservationIgnored private var subscribed: AsyncStream<RenderEvent>?

  /// Subscribes at once, so a page invalidated before the first ``task()``
  /// still counts. The subscription buffers every render event until
  /// ``task()`` runs, so run it soon after creating the model.
  public init(render: RenderCapability) {
    self.render = render
    subscribed = render.events
  }

  /// Counts invalidations until the document closes or the task is
  /// cancelled. The first run consumes the subscription `init` made; each
  /// later run subscribes again, so the model survives a view's `.task`
  /// restarting. Run one at a time: concurrent runs are not supported.
  public func task() async {
    // Taken before the first suspension, so a restart misses nothing sent
    // after it begins.
    let events = subscribed ?? render.events
    subscribed = nil
    for await event in events {
      guard !Task.isCancelled else { return }
      if case let .invalidated(pages, _) = event {
        for page in pages { revisions[page, default: 0] &+= 1 }
      }
    }
  }

  /// `0` for a page never invalidated.
  public func revision(for page: PageRef) -> UInt64 { revisions[page, default: 0] }
}
