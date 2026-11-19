import CoreGraphics
import EmbedPDF
import Foundation
import Observation

/// What a scrolling reader shows, and the gestures it forwards. The stage
/// lays out; this model only mirrors its visible pages and camera onto the
/// main actor and turns SwiftUI gesture values into stage calls.
///
/// Given an `InteractionCapability`, it also sends touches to the document's
/// pointer hub (``pointerDown(at:)`` and its siblings, ``tapped(at:)``), so
/// the active tool draws and a tap reaches whatever handles taps. Given a
/// `LinkCapability` too, a tap on a link activates it and the outcome
/// arrives on ``linkActivations``. Without an `InteractionCapability` the
/// pointer calls do nothing.
@MainActor
@Observable
public final class StageReaderModel {
  public private(set) var visiblePages: [StageVisiblePage] = []
  public private(set) var camera: Camera?
  /// The stage's current page as a zero-based display position.
  public private(set) var pageIndex = 0
  /// ``pageIndex`` as the engine's `UInt32`, which always holds it: the
  /// stage reports it as one.
  @available(*, deprecated, message: "Use pageIndex, an Int; this is the same position as a UInt32.")
  public var currentPageIndex: UInt32 { UInt32(exactly: pageIndex) ?? 0 }
  public private(set) var error: PDFError?
  /// Whether the zoom is changing: a pinch is under way, or the zoom
  /// animation a pinch's end or a double tap starts hasn't settled. Pages
  /// render at banded scales while it is, and at the stage's own scale
  /// otherwise (``renderScale(_:contentWidth:contentHeight:zooming:)``). A pan
  /// or a fling moves the camera without changing the scale, so it leaves
  /// this false and renders nothing new.
  public private(set) var isZooming = false
  /// The hub's active tool, read when ``task()`` starts and followed from
  /// the hub's `toolChanged` events while it runs. `nil` before the first
  /// read, after a failed one, and always without an
  /// `InteractionCapability`.
  public private(set) var activeToolID: ToolID?
  /// Whether a drag belongs to the active tool rather than to the pan: true
  /// when the model has an `InteractionCapability` and ``activeToolID`` is
  /// not the hub's `defaultToolID()`. Follows the hub as ``activeToolID``
  /// does, so it is false until ``task()`` has read the tool. The default
  /// tool is read again with each tool change; a settings change that names
  /// another default shows at the next one.
  ///
  /// `StageReaderView` reads it when a drag starts: true sends the drag to
  /// ``pointerDown(at:)``, ``pointerMoved(to:)`` and ``pointerUp(at:)``
  /// instead of ``dragChanged(translation:)``. It replaces only the pan.
  /// ``isPanEnabled`` does not gate it, and a page overlay's own gestures
  /// still take a touch before the reader's drag sees it.
  public private(set) var forwardsDrags = false
  /// Whether a drag pans the stage. Turn it off while a page overlay needs
  /// every touch. Turning it off mid-drag ends the open stage gesture without
  /// a fling, since SwiftUI cancels a disabled gesture without calling its
  /// end. It gates the pan only: while ``forwardsDrags`` is true a drag
  /// still goes to the active tool.
  public var isPanEnabled = true {
    didSet {
      // Joins the drag chain before returning, so a drag call made after
      // this setter runs after the end.
      if oldValue, !isPanEnabled { dragCalls.enqueue { await self.applyDragEnd(.zero) } }
    }
  }
  /// Per-page render revisions. The counter observes by itself, whether or
  /// not ``task()`` runs.
  public let revisions: RenderRevisions
  @ObservationIgnored public let stage: StageCapability
  @ObservationIgnored public let render: RenderCapability
  @ObservationIgnored private var lastTranslation = CGSize.zero
  @ObservationIgnored private var lastMagnification: CGFloat = 1
  @ObservationIgnored private(set) var gestureOpen = false
  /// Where a zoom stands. A pinch's end or a double tap may start a zoom
  /// animation; `settling` holds until a refresh finds the stage still. An
  /// open pinch outranks that animation: a double tap during a pinch leaves
  /// `pinching`, and the pinch's end starts the settling.
  private enum ZoomPhase { case idle, pinching, settling }
  @ObservationIgnored private var zoomPhase = ZoomPhase.idle
  /// Bumped when a zoom start begins and when its stage call returns. A
  /// refresh judges `settling` only if no start was in flight when it
  /// began and this is unchanged when its read lands. Each stage call hops
  /// off the main actor before it reaches the stage, so a refresh that began
  /// near a double tap can read `isMoving()` before `doubleTapZoom` runs and
  /// find the stage still.
  @ObservationIgnored private var zoomEpoch = 0
  /// Zoom starts whose stage call has not returned.
  @ObservationIgnored private var zoomStartsInFlight = 0
  /// Runs in a double tap after the start is marked and before the stage
  /// call. Tests hold it so a refresh can land first.
  @ObservationIgnored var zoomStartReached: (@MainActor () async -> Void)?
  /// Runs in a refresh after its `isMoving()` read returns and before the
  /// read is applied. Tests hold it so a double tap can run first.
  @ObservationIgnored var zoomReadReturned: (@MainActor () async -> Void)?
  /// The drag calls, each run after every earlier one. SwiftUI hands a
  /// gesture's change and end to separate tasks, and each call leaves the
  /// main actor while it reaches the stage, so without this an end could
  /// reach the stage before the begin it closes.
  @ObservationIgnored private let dragCalls = SerialCalls()
  @ObservationIgnored private let interaction: InteractionCapability?
  @ObservationIgnored private let link: LinkCapability?
  /// Sends the pointer calls to the hub. `nil` without an
  /// `InteractionCapability`.
  @ObservationIgnored private let pointer: StagePointerForwarder?
  @ObservationIgnored private let linkFeed = LinkActivationFeed()
  /// Runs as each pointer sample is handed to the hub. Tests record them.
  @ObservationIgnored var pointerSampleDispatched: (@MainActor (PointerSample) -> Void)? {
    didSet { pointer?.dispatched = pointerSampleDispatched }
  }

  /// The most pixels one page renders at: 4096 by 4096, 64 MB of RGBA.
  public nonisolated static let maxPagePixels: Double = 4096 * 4096

  /// The scale to render a page at: the stage's `renderScale`, capped so the
  /// whole page stays within `maxPagePixels`. The reader renders pages whole
  /// until it draws the tile plane, so past the cap a deep zoom is soft
  /// rather than hundreds of megabytes a page. Sizes in page points.
  ///
  /// While zooming the scale is first rounded up to a width band
  /// (``PageRenderResolution/bandedScale(_:pageWidth:)``), so a pinch
  /// restarts a page's render only when it crosses a band. Otherwise it is
  /// the stage's scale itself, so the page that stays on screen is drawn at
  /// the size it is shown, not resampled from a wider band.
  public nonisolated static func renderScale(
    _ scale: Double, contentWidth: Double, contentHeight: Double, zooming: Bool = false
  ) -> Double {
    let wanted = zooming ? PageRenderResolution.bandedScale(scale, pageWidth: contentWidth) : scale
    let area = contentWidth * contentHeight
    guard area > 0 else { return wanted }
    return min(wanted, (maxPagePixels / area).squareRoot())
  }

  /// Creates a reader model.
  ///
  /// - Parameters:
  ///   - stage: The stage that lays the pages out.
  ///   - render: The capability that renders them.
  ///   - interaction: The document's pointer hub. With it the model follows
  ///     the active tool and the pointer calls reach the hub; without it
  ///     they do nothing.
  ///   - link: The document's links. With it ``task()`` lets taps on the
  ///     hub follow links and ``linkActivations`` reports them.
  public init(
    stage: StageCapability, render: RenderCapability, interaction: InteractionCapability? = nil,
    link: LinkCapability? = nil
  ) {
    self.stage = stage
    self.render = render
    self.interaction = interaction
    self.link = link
    revisions = RenderRevisions(render: render)
    pointer = interaction.map { StagePointerForwarder(stage: stage, interaction: $0) }
    pointer?.failed = { [weak self] error in self?.error = error }
  }

  /// Run for the view's lifetime, projecting stage events. With an
  /// `InteractionCapability` it also follows the active tool, and with a
  /// `LinkCapability` as well it registers the link tap handler, once, and
  /// keeps the registration until it returns.
  public func task() async {
    let stageEvents = stage.events
    // Subscribed before the first read and before the handler registers, so
    // no tool change and no activation falls between.
    let toolEvents = interaction?.events
    let linkEvents = link?.events
    let linkTaps = await registerLinkTaps()
    defer { linkTaps?.unregister() }
    await withTaskGroup(of: Void.self) { group in
      group.addTask {
        for await event in stageEvents {
          guard !Task.isCancelled else { return }
          await self.receiveStage(event)
        }
      }
      if let toolEvents {
        group.addTask {
          await self.refreshTool()
          for await event in toolEvents {
            guard !Task.isCancelled else { return }
            if case .toolChanged = event { await self.refreshTool() }
          }
        }
      }
      if let linkEvents {
        group.addTask {
          for await event in linkEvents {
            guard !Task.isCancelled else { return }
            if case let .activated(event) = event { await self.linkFeed.yield(event.activation) }
          }
        }
      }
      await refresh()
      _ = await group.next()
      group.cancelAll()
    }
  }

  /// `nil` without a `LinkCapability`, or when registering fails; the
  /// failure is in ``error``.
  private func registerLinkTaps() async -> Registration? {
    guard let link else { return nil }
    do {
      return try await link.registerTapHandler()
    } catch {
      self.error = error
      return nil
    }
  }

  /// Reads the active and the default tool. A failed read leaves no tool
  /// and no forwarding.
  private func refreshTool() async {
    guard let interaction else { return }
    do {
      let active = try await interaction.activeToolID()
      let fallback = try await interaction.defaultToolID()
      if activeToolID != active { activeToolID = active }
      if forwardsDrags != (active != fallback) { forwardsDrags = active != fallback }
    } catch {
      if activeToolID != nil { activeToolID = nil }
      if forwardsDrags { forwardsDrags = false }
      self.error = error
    }
  }

  /// Link activations the document reports while ``task()`` runs: the
  /// outcome of a tap that lands on a link, and also of an activation the
  /// app makes itself through `LinkCapability`. A `.uri` is the app's to
  /// open. Never yields without a `LinkCapability`.
  ///
  /// Each read is a new stream, and every stream open when an activation
  /// arrives gets it, so a second reader takes nothing from the first. A
  /// stream gets no activation from before it was read. It finishes when
  /// the model is released.
  public var linkActivations: AsyncStream<LinkActivation> { linkFeed.stream() }

  /// Sends a pointer down at `location`, a point in the reader's viewport,
  /// to the hub, with the page the stage finds under it. Returns at once;
  /// the pointer calls reach the hub in the order they are made.
  public func pointerDown(at location: CGPoint) { pointer?.down(at: location) }

  /// Sends a pointer move. Moves that arrive while an earlier pointer call
  /// is still on its way collapse into the newest, so the hub sees fewer
  /// samples than a fast drag makes and always the latest position.
  public func pointerMoved(to location: CGPoint) { pointer?.move(to: location) }

  /// Sends a pointer up, after any move still waiting.
  public func pointerUp(at location: CGPoint) { pointer?.up(at: location) }

  /// Sends a pointer cancel at the last location, so the tool drops the
  /// gesture instead of committing it. A move still waiting is dropped.
  /// Call it when the system takes a drag away without ending it.
  public func pointerCancelled() { pointer?.cancel() }

  /// Sends a down and an up at `location`, each with a click count of 1:
  /// a tap for the active tool, and for a link under the default tool.
  public func tapped(at location: CGPoint) { pointer?.tap(at: location) }

  /// Returns once every pointer call made so far has been handed to the hub.
  func pointerCallsFinished() async { await pointer?.finished() }

  /// A presentation revision that changes when Rust invalidates this page.
  public func renderRevision(for page: PageRef) -> UInt64 { revisions.revision(for: page) }

  private func receiveStage(_ event: StageEvent) async {
    switch event {
    case let .cameraChanged(event):
      // The event carries the camera; only the pages it shows are read back.
      camera = event.camera
      do {
        visiblePages = try await stage.visiblePages()
      } catch {
        self.error = error
      }
    case .motionEnded, .viewportChanged, .zoomChanged, .settingsChanged:
      await refresh()
    case let .pageChanged(event):
      do {
        pageIndex = try event.pageIndex.toInt("page index")
      } catch {
        self.error = error
      }
    }
  }

  public func sizeChanged(_ size: CGSize, displayScale: CGFloat) async {
    do {
      try await stage.setDevicePixelRatio(Double(displayScale))
      try await stage.setViewportSize(Size(width: size.width, height: size.height))
      pageIndex = try await stage.currentPageIndex()
    } catch {
      self.error = error
    }
    await refresh()
  }

  /// Runs after every earlier drag call has finished. Does nothing to the
  /// stage while ``isPanEnabled`` is false.
  public func dragChanged(translation: CGSize) async {
    await dragCalls.enqueue { await self.applyDragChange(translation) }.value
  }

  /// `velocity` in points per second, as `DragGesture.Value.velocity`
  /// reports. Runs after every earlier drag call has finished. Skips the
  /// fling while ``isPanEnabled`` is false.
  public func dragEnded(velocity: CGSize) async {
    await dragCalls.enqueue { await self.applyDragEnd(velocity) }.value
  }

  private func applyDragChange(_ translation: CGSize) async {
    guard isPanEnabled else { return }
    do {
      if !gestureOpen {
        gestureOpen = true
        lastTranslation = .zero
        try await stage.beginGesture(elastic: true)
      }
      // `panBy` follows the finger: a finger moving up scrolls down.
      stage.panBy(
        dx: translation.width - lastTranslation.width,
        dy: translation.height - lastTranslation.height)
      lastTranslation = translation
    } catch {
      self.error = error
    }
    // The pan's `cameraChanged` event brings the camera and pages back.
  }

  private func applyDragEnd(_ velocity: CGSize) async {
    do {
      if gestureOpen {
        gestureOpen = false
        try await stage.endGesture()
      }
      if velocity != .zero, isPanEnabled {
        try await stage.fling(velocityX: velocity.width, velocityY: velocity.height)
      }
      pageIndex = try await stage.currentPageIndex()
    } catch {
      self.error = error
    }
    await refresh()
  }

  public func magnifyChanged(_ magnification: CGFloat, at location: CGPoint) {
    zoomPhase = .pinching
    setZooming(true)
    let factor = magnification / lastMagnification
    lastMagnification = magnification
    stage.zoomAround(Point(x: location.x, y: location.y), factor: Double(factor))
  }

  public func magnifyEnded() async {
    zoomPhase = .settling
    zoomEpoch &+= 1
    lastMagnification = 1
    await refresh()
  }

  public func doubleTapped(at location: CGPoint) async {
    if zoomPhase != .pinching { zoomPhase = .settling }
    zoomEpoch &+= 1
    zoomStartsInFlight += 1
    await zoomStartReached?()
    do {
      try await stage.doubleTapZoom(Point(x: location.x, y: location.y))
    } catch {
      self.error = error
    }
    zoomStartsInFlight -= 1
    zoomEpoch &+= 1
    await refresh()
  }

  /// Reads the pages, the camera and, after a pinch's end or a double tap,
  /// whether the zoom animation still runs: the stage's `motionEnded` lands
  /// here too. A failed read ends the animation, so ``isZooming`` can't stay
  /// set. Only a refresh that began after the last zoom start returned may
  /// end the animation (``zoomEpoch``); that start's own refresh always
  /// does, so the animation still ends.
  private func refresh() async {
    let epoch = zoomEpoch
    let mayJudge = zoomStartsInFlight == 0
    do {
      visiblePages = try await stage.visiblePages()
      camera = try await stage.camera()
      if zoomPhase == .settling, mayJudge {
        let moving = try await stage.isMoving()
        await zoomReadReturned?()
        // A pinch may have started while the read was out.
        if epoch == zoomEpoch, zoomPhase == .settling, !moving { zoomPhase = .idle }
      }
    } catch {
      if mayJudge, epoch == zoomEpoch, zoomPhase == .settling { zoomPhase = .idle }
      self.error = error
    }
    setZooming(zoomPhase != .idle)
  }

  private func setZooming(_ zooming: Bool) {
    if isZooming != zooming { isZooming = zooming }
  }
}

/// Hands each link activation to every stream open when it arrives.
private final class LinkActivationFeed: @unchecked Sendable {
  private let lock = NSLock()
  private var nextID: UInt64 = 0
  private var continuations: [UInt64: AsyncStream<LinkActivation>.Continuation] = [:]

  /// A new stream of the activations yielded from now on. Ending its
  /// iteration removes it.
  func stream() -> AsyncStream<LinkActivation> {
    let (stream, continuation) = AsyncStream<LinkActivation>.makeStream()
    let id = lock.withLock {
      nextID &+= 1
      continuations[nextID] = continuation
      return nextID
    }
    continuation.onTermination = { [weak self] _ in self?.remove(id) }
    return stream
  }

  func yield(_ activation: LinkActivation) {
    for continuation in lock.withLock({ Array(continuations.values) }) { continuation.yield(activation) }
  }

  private func remove(_ id: UInt64) {
    _ = lock.withLock { continuations.removeValue(forKey: id) }
  }

  deinit {
    for continuation in continuations.values { continuation.finish() }
  }
}
