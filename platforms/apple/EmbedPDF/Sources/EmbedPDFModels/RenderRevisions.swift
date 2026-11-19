import EmbedPDF
import Observation

/// A per-page counter that rises each time the engine invalidates a page's
/// render. Views key their page images on it, so an edit redraws only the
/// pages it touched.
///
/// The model observes by itself from `init` until the document closes or the
/// model is released; there is no task to run. Releasing it ends its event
/// subscription, so nothing buffers for a model no one holds.
@MainActor
@Observable
public final class RenderRevisions {
  private var revisions: [PageRef: UInt64] = [:]
  /// The task counting invalidations, cancelled when the model is released.
  @ObservationIgnored private var observation: CancelOnRelease?
  /// The counting task, which finishes when the document closes or the model
  /// is released. Tests await it.
  var observationTask: Task<Void, Never>? { observation?.task }

  /// Subscribes at once, so a page invalidated right after `init` counts.
  public init(render: RenderCapability) {
    let events = render.events
    // Holds the model weakly, and only while it counts one event, so the
    // task never keeps the model alive.
    observation = CancelOnRelease(
      Task { [weak self] in
        for await event in events { self?.count(event) }
      })
  }

  private func count(_ event: RenderEvent) {
    if case let .invalidated(pages, _) = event {
      for page in pages { revisions[page, default: 0] &+= 1 }
    }
  }

  /// `0` for a page never invalidated.
  public func revision(for page: PageRef) -> UInt64 { revisions[page, default: 0] }
}
