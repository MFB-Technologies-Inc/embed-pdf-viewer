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
/// The overlay takes every touch in its frame. Read
/// ``TextSelectionModel/error`` for a failure and
/// ``TextSelectionModel/isSelecting`` for whether the drag found text.
public struct TextSelectionOverlay: View {
  let model: TextSelectionModel
  let page: PageRef
  let geometry: PageGeometry
  let start: TextSelectionModel.Start
  let tint: Color
  /// Whether a drag has sent its first point and not yet ended.
  @State private var dragging = false
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
  public init(
    model: TextSelectionModel, page: PageRef, geometry: PageGeometry,
    start: TextSelectionModel.Start = .caret, tint: Color = .accentColor.opacity(0.3)
  ) {
    self.model = model
    self.page = page
    self.geometry = geometry
    self.start = start
    self.tint = tint
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
  public init(
    model: TextSelectionModel, visiblePage: StageVisiblePage, info: PageInfo,
    start: TextSelectionModel.Start = .caret, tint: Color = .accentColor.opacity(0.3)
  ) {
    self.init(
      model: model, page: visiblePage.page, geometry: visiblePage.geometry(for: info),
      start: start, tint: tint)
  }

  public var body: some View {
    GeometryReader { proxy in
      let size = proxy.size
      ZStack(alignment: .topLeading) {
        Color.clear.contentShape(Rectangle())
        ForEach(Array((model.rects[page] ?? []).enumerated()), id: \.offset) { _, rect in
          let local = Self.localRect(rect, in: size, geometry: geometry)
          Rectangle()
            .fill(tint)
            .frame(width: local.width, height: local.height)
            .offset(x: local.minX, y: local.minY)
        }
      }
      .gesture(
        DragGesture(minimumDistance: 0)
          .updating($touching) { _, touching, _ in touching = true }
          .onChanged { value in
            let point = Self.pagePoint(value.location, in: size, geometry: geometry)
            if dragging {
              model.dragChanged(to: point, on: page)
            } else {
              dragging = true
              model.dragBegan(at: point, on: page, start: start)
            }
          }
          .onEnded { _ in finish() }
      )
    }
    .onChange(of: touching) { _, touching in
      if !touching { finish() }
    }
  }

  /// Ends the open drag once, whether SwiftUI ended or cancelled it.
  private func finish() {
    guard dragging else { return }
    dragging = false
    Task { await model.dragEnded() }
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
