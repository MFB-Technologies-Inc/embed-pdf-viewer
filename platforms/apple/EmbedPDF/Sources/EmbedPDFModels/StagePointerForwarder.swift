import CoreGraphics
import EmbedPDF

/// Turns a reader's touches into pointer samples for the interaction hub.
/// Each sample carries the touch's viewport point and the page the stage
/// finds under it, and reaches the hub after every sample sent before it.
/// Moves that arrive while another call is in flight collapse into the
/// newest one, and a collapsed move stays with its own gesture: the hub gets
/// a gesture's down, its newest move, then its up or cancel, with no sample
/// of another gesture between them.
@MainActor
final class StagePointerForwarder {
  private let stage: StageCapability
  private let interaction: InteractionCapability
  /// Orders the samples. Each one leaves the main actor while the stage
  /// finds its page, so without this an up could reach the hub before the
  /// down it closes.
  private let calls = SerialCalls()
  /// A move waiting in the chain. The call that sends it reads `point` when
  /// it runs.
  private final class WaitingMove {
    var point: CGPoint?
    init(_ point: CGPoint) { self.point = point }
  }
  /// The waiting move later moves still collapse into. A down, an up, a
  /// cancel or a tap lets go of it, so a move after one of them queues
  /// behind that call instead of joining a move queued ahead of it.
  private var waitingMove: WaitingMove?
  /// Where the newest touch was. A cancel has no location of its own.
  private var lastLocation = CGPoint.zero
  /// Whether a down has been asked for with no up or cancel after it.
  private var gestureOpen = false
  /// Whether ``interrupt()`` closed the gesture. Until the next down or
  /// tap, a move, an up and a cancel send nothing.
  private var interrupted = false
  /// Receives a failed page lookup. The sample still goes out, with no page.
  var failed: (@MainActor (PDFError) -> Void)?
  /// Runs as each sample is handed to the hub. Tests record them.
  var dispatched: (@MainActor (PointerSample) -> Void)?

  init(stage: StageCapability, interaction: InteractionCapability) {
    self.stage = stage
    self.interaction = interaction
  }

  func down(at location: CGPoint) {
    // An earlier gesture's waiting move stays queued ahead of its up.
    waitingMove = nil
    interrupted = false
    gestureOpen = true
    lastLocation = location
    calls.enqueue { await self.send(.down, at: location) }
  }

  func move(to location: CGPoint) {
    guard !interrupted else { return }
    lastLocation = location
    if let waitingMove {
      waitingMove.point = location
      return
    }
    let move = WaitingMove(location)
    waitingMove = move
    calls.enqueue {
      // Let go before the send suspends: a move that arrives during it
      // queues a call of its own.
      if self.waitingMove === move { self.waitingMove = nil }
      guard let location = move.point else { return }
      await self.send(.move, at: location)
    }
  }

  /// A move still waiting is dispatched first, so the gesture ends where the
  /// touch last was.
  func up(at location: CGPoint) {
    guard !interrupted else { return }
    waitingMove = nil
    gestureOpen = false
    lastLocation = location
    calls.enqueue { await self.send(.up, at: location) }
  }

  /// Drops this gesture's move if one still waits and sends a cancel at the
  /// last location.
  func cancel() {
    guard !interrupted else { return }
    waitingMove?.point = nil
    waitingMove = nil
    gestureOpen = false
    let location = lastLocation
    calls.enqueue { await self.send(.cancel, at: location) }
  }

  /// Cancels the open gesture, if there is one, and mutes the rest of its
  /// touch: until the next down or tap, a move, an up and a cancel send
  /// nothing. Does nothing with no gesture open.
  func interrupt() {
    guard gestureOpen else { return }
    cancel()
    interrupted = true
  }

  /// A down and an up at one point, both with a click count of 1.
  func tap(at location: CGPoint) {
    waitingMove = nil
    interrupted = false
    gestureOpen = false
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
      // Performance: every sample awaits this lookup before its dispatch, so
      // a sample costs a round trip to the stage on top of the dispatch, and
      // a move waits a full lookup behind the sample before it. The page
      // under a drag rarely changes between moves; mapping the point through
      // the last hit's page and transform while the camera stands still
      // saves the lookup.
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
