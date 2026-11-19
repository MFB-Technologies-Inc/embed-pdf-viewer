import EmbedPDF
import SwiftUI

/// Selects text on one page by dragging, and draws the page's selected rects.
///
/// The overlay fills its frame and maps the whole frame onto the page as
/// shown, so size it to the page: place it over the page's image, or return
/// it from a stage reader's page overlay. One drag is one selection gesture:
/// its first point begins the selection, later points extend it, and lifting
/// ends it. A drag the system cancels ends the selection the same way.
///
/// With ``Activation/drag``, the default, the overlay takes every touch in
/// its frame. With ``Activation/longPress`` it takes a touch only once a long
/// press has succeeded, so a plain drag is left to a gesture behind it, such
/// as a stage reader's pan. A drag that is open when the overlay leaves the
/// hierarchy ends the selection too. Read ``TextSelectionModel/error`` for a
/// failure and ``TextSelectionModel/isSelecting`` for whether the drag found
/// text.
public struct TextSelectionOverlay: View {
  /// What starts a selection drag.
  public enum Activation: Sendable {
    /// A touch begins the selection where it lands, and the overlay takes
    /// every touch in its frame.
    case drag
    /// A long press, of the system's default duration, arms the selection.
    /// It begins where the finger is when it next moves, and extends while
    /// the finger drags. A touch that moves before the press completes is
    /// not the overlay's.
    case longPress
  }

  let model: TextSelectionModel
  let page: PageRef
  let geometry: PageGeometry
  let start: TextSelectionModel.Start
  let tint: Color
  let activation: Activation
  /// The open drag: which of its points began it, and whether it has ended.
  @State private var drag = TextSelectionDrag()
  /// True while a drag is down. SwiftUI resets it when it cancels the
  /// gesture without calling `onEnded`.
  @GestureState private var touching = false

  /// Creates an overlay for `page` as `geometry` shows it.
  ///
  /// - Parameters:
  ///   - model: The model the drag drives and whose rects are drawn.
  ///   - page: The page under the overlay.
  ///   - geometry: The page's turn and unrotated size. The frame covers the
  ///     page turned this way.
  ///   - start: What the drag's first point selects; defaults to `.caret`.
  ///   - tint: The fill of the selected rects.
  ///   - activation: What starts a selection drag; defaults to `.drag`.
  public init(
    model: TextSelectionModel, page: PageRef, geometry: PageGeometry,
    start: TextSelectionModel.Start = .caret, tint: Color = .accentColor.opacity(0.3),
    activation: Activation = .drag
  ) {
    self.model = model
    self.page = page
    self.geometry = geometry
    self.start = start
    self.tint = tint
    self.activation = activation
  }

  /// Creates an overlay for a page a stage shows, turned as the stage turns
  /// it, view rotation included.
  ///
  /// - Parameters:
  ///   - model: The model the drag drives and whose rects are drawn.
  ///   - visiblePage: The page as the stage reports it.
  ///   - info: The same page's `PageInfo`, for its unrotated size.
  ///   - start: What the drag's first point selects; defaults to `.caret`.
  ///   - tint: The fill of the selected rects.
  ///   - activation: What starts a selection drag; defaults to `.drag`.
  public init(
    model: TextSelectionModel, visiblePage: StageVisiblePage, info: PageInfo,
    start: TextSelectionModel.Start = .caret, tint: Color = .accentColor.opacity(0.3),
    activation: Activation = .drag
  ) {
    self.init(
      model: model, page: visiblePage.page, geometry: visiblePage.geometry(for: info),
      start: start, tint: tint, activation: activation)
  }

  public var body: some View {
    GeometryReader { proxy in
      let size = proxy.size
      gestured(in: size) {
        Color.clear.contentShape(Rectangle())
        ForEach(Array((model.rects[page] ?? []).enumerated()), id: \.offset) { _, rect in
          let local = Self.localRect(rect, in: size, geometry: geometry)
          Rectangle()
            .fill(tint)
            .frame(width: local.width, height: local.height)
            .offset(x: local.minX, y: local.minY)
        }
      }
    }
    .onChange(of: touching) { _, touching in
      if !touching { handle(.cancelled) }
    }
    .onDisappear { handle(.disappeared) }
  }

  /// The rects' stack with the activation's gesture on it.
  @ViewBuilder
  private func gestured(in size: CGSize, @ViewBuilder content: () -> some View) -> some View {
    let stack = ZStack(alignment: .topLeading, content: content)
    switch activation {
    case .drag: stack.gesture(dragGesture(in: size))
    case .longPress: stack.gesture(longPressGesture(in: size))
    }
  }

  /// A drag that selects from its first point.
  private func dragGesture(in size: CGSize) -> some Gesture {
    DragGesture(minimumDistance: 0)
      .updating($touching) { _, touching, _ in touching = true }
      .onChanged { value in handle(.changed, at: value.location, in: size) }
      .onEnded { _ in handle(.ended) }
  }

  /// A long press, then a drag that selects. Until the press succeeds the
  /// sequence has recognized nothing, so a touch that moves first fails it
  /// and goes to a gesture behind the overlay. The drag reports no point
  /// until the finger moves after the press, which is where the selection
  /// begins.
  private func longPressGesture(in size: CGSize) -> some Gesture {
    LongPressGesture()
      .sequenced(before: DragGesture(minimumDistance: 0))
      .updating($touching) { value, touching, _ in
        if case .second = value { touching = true }
      }
      .onChanged { value in
        if case let .second(_, drag?) = value { handle(.changed, at: drag.location, in: size) }
      }
      .onEnded { _ in handle(.ended) }
  }

  /// Sends the model what `event` calls for: a begin or an extension at
  /// `location`, or the drag's one end.
  private func handle(_ event: TextSelectionDrag.Event, at location: CGPoint = .zero, in size: CGSize = .zero) {
    switch drag.handle(event) {
    case .begin:
      model.dragBegan(at: Self.pagePoint(location, in: size, geometry: geometry), on: page, start: start)
    case .extend:
      model.dragChanged(to: Self.pagePoint(location, in: size, geometry: geometry), on: page)
    case .end:
      Task { await model.dragEnded() }
    case nil:
      break
    }
  }

  /// The point in unrotated page space at `local`, a point in a frame of
  /// `size` that covers the page as shown. Each axis scales by its own
  /// length.
  ///
  /// An axis on which `size` is not greater than zero maps to coordinate 0
  /// of the page as shown, so the result is finite for an empty frame.
  nonisolated static func pagePoint(
    _ local: CGPoint, in size: CGSize, geometry: PageGeometry
  ) -> Point {
    let shown = geometry.displaySize
    return geometry.toPage(
      Point(
        x: Double(local.x) * ratio(shown.width, over: Double(size.width)),
        y: Double(local.y) * ratio(shown.height, over: Double(size.height))))
  }

  /// Where `rect`, a rect in unrotated page space, sits in a frame of `size`
  /// that covers the page as shown. The inverse of
  /// ``pagePoint(_:in:geometry:)``.
  ///
  /// An axis on which the page as shown is not greater than zero gets
  /// offset 0 and length 0, so the result is finite for an empty page.
  nonisolated static func localRect(
    _ rect: Rect, in size: CGSize, geometry: PageGeometry
  ) -> CGRect {
    let shown = geometry.displaySize
    let turned = geometry.toDisplay(rect)
    let x = ratio(Double(size.width), over: shown.width)
    let y = ratio(Double(size.height), over: shown.height)
    return CGRect(
      x: turned.x * x, y: turned.y * y, width: turned.width * x, height: turned.height * y)
  }

  /// `length / divisor`, or 0 when `divisor` is not greater than zero.
  private nonisolated static func ratio(_ length: Double, over divisor: Double) -> Double {
    divisor > 0 ? length / divisor : 0
  }
}
