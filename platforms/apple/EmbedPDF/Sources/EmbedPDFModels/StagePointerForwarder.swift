import CoreGraphics
import EmbedPDF

/// Turns a reader's touches into pointer samples for the interaction hub.
/// Each sample carries the touch's viewport point and the page the stage
/// finds under it, and reaches the hub after every sample sent before it.
/// Moves that arrive while another call is in flight collapse into the
/// newest one.
@MainActor
final class StagePointerForwarder {
  private let stage: StageCapability
  private let interaction: InteractionCapability
  /// Orders the samples. Each one leaves the main actor while the stage
  /// finds its page, so without this an up could reach the hub before the
  /// down it closes.
  private let calls = SerialCalls()
  /// The newest move no call has dispatched yet.
  private var pendingMove: CGPoint?
  /// Whether a move waits in the chain; it dispatches whatever
  /// `pendingMove` holds when it runs.
  private var moveQueued = false
  /// Where the newest touch was. A cancel has no location of its own.
  private var lastLocation = CGPoint.zero
  /// Receives a failed page lookup. The sample still goes out, with no page.
  var failed: (@MainActor (PDFError) -> Void)?
  /// Runs as each sample is handed to the hub. Tests record them.
  var dispatched: (@MainActor (PointerSample) -> Void)?

  init(stage: StageCapability, interaction: InteractionCapability) {
    self.stage = stage
    self.interaction = interaction
  }

  func down(at location: CGPoint) {
    // A move left by a gesture that never ended belongs to that gesture.
    pendingMove = nil
    lastLocation = location
    calls.enqueue { await self.send(.down, at: location) }
  }

  func move(to location: CGPoint) {
    pendingMove = location
    lastLocation = location
    guard !moveQueued else { return }
    moveQueued = true
    calls.enqueue {
      self.moveQueued = false
      guard let location = self.pendingMove else { return }
      self.pendingMove = nil
      await self.send(.move, at: location)
    }
  }

  /// A move still waiting is dispatched first, so the gesture ends where the
  /// touch last was.
  func up(at location: CGPoint) {
    lastLocation = location
    calls.enqueue { await self.send(.up, at: location) }
  }

  /// Drops a move still waiting and sends a cancel at the last location.
  func cancel() {
    pendingMove = nil
    let location = lastLocation
    calls.enqueue { await self.send(.cancel, at: location) }
  }

  /// A down and an up at one point, both with a click count of 1.
  func tap(at location: CGPoint) {
    pendingMove = nil
    lastLocation = location
    calls.enqueue {
      let page = await self.pageHit(at: location)
      self.dispatch(self.sample(.down, at: location, page: page, clickCount: 1))
      self.dispatch(self.sample(.up, at: location, page: page, clickCount: 1))
    }
  }

  /// Returns once every sample asked for so far has been handed to the hub.
  func finished() async {
    await calls.enqueue {}.value
  }

  private func send(_ phase: PointerPhase, at location: CGPoint) async {
    dispatch(sample(phase, at: location, page: await pageHit(at: location), clickCount: nil))
  }

  private func pageHit(at location: CGPoint) async -> PointerPageHit? {
    do {
      guard let hit = try await stage.page(at: Point(x: location.x, y: location.y)) else { return nil }
      return PointerPageHit(
        page: hit.page, point: hit.point, scale: hit.scale, rotation: hit.rotation, zoom: hit.zoom)
    } catch {
      failed?(error)
      return nil
    }
  }

  private func sample(
    _ phase: PointerPhase, at location: CGPoint, page: PointerPageHit?, clickCount: UInt32?
  ) -> PointerSample {
    PointerSample(
      phase: phase, viewport: Point(x: location.x, y: location.y), page: page,
      modifiers: Modifiers(), clickCount: clickCount, pointerType: .touch)
  }

  private func dispatch(_ sample: PointerSample) {
    interaction.dispatchPointer(sample)
    dispatched?(sample)
  }
}
