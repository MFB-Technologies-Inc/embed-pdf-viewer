import CoreGraphics
import EmbedPDF

extension StageVisiblePage {
  /// The viewport point at `local`, a point in this page's overlay with its
  /// origin at the page's top left. Pass it to
  /// `StageCapability.viewportToPage(_:_:)` for the page point.
  public func viewportPoint(_ local: CGPoint) -> Point {
    Point(x: screenX + local.x, y: screenY + local.y)
  }

  /// The geometry of the page as the stage shows it. Its turn comes from
  /// `transform.rotation`, which includes the stage's view rotation;
  /// `PageInfo.geometry` has only the page's own rotation.
  ///
  /// - Parameter info: This page's `PageInfo`, for its unrotated size.
  public func geometry(for info: PageInfo) -> PageGeometry {
    PageGeometry(
      turn: RenderRotation(degrees: transform.rotation),
      pageSize: Size(width: info.width, height: info.height))
  }

  /// The point in unrotated page space at `local`, a point in this page's
  /// overlay with its origin at the page's top left. Both axes scale by the
  /// page's width as shown over `transform.viewWidth`.
  ///
  /// When `transform.viewWidth` is not greater than zero, every `local`
  /// maps to the page point at the top left of the page as shown, so the
  /// result is finite for a page the stage has not sized.
  public func pagePoint(_ local: CGPoint, info: PageInfo) -> Point {
    let geometry = geometry(for: info)
    let scale = Self.ratio(geometry.displaySize.width, over: transform.viewWidth)
    return geometry.toPage(Point(x: Double(local.x) * scale, y: Double(local.y) * scale))
  }

  /// Where `pageRect`, a rect in unrotated page space, sits in this page's
  /// overlay, with the origin at the page's top left. The inverse of
  /// ``pagePoint(_:info:)``.
  ///
  /// When the page's width as shown is not greater than zero, the result
  /// is `CGRect.zero`, and it is too when `transform.viewWidth` is zero.
  public func localRect(_ pageRect: Rect, info: PageInfo) -> CGRect {
    let geometry = geometry(for: info)
    let scale = Self.ratio(transform.viewWidth, over: geometry.displaySize.width)
    let shown = geometry.toDisplay(pageRect)
    return CGRect(
      x: shown.x * scale, y: shown.y * scale, width: shown.width * scale,
      height: shown.height * scale)
  }

  /// Where `point`, a point in unrotated page space, sits in this page's
  /// overlay, with the origin at the page's top left. The inverse of
  /// ``pagePoint(_:info:)``, scaled as ``localRect(_:info:)`` scales.
  ///
  /// When the page's width as shown is not greater than zero, the result
  /// is `CGPoint.zero`, and it is too when `transform.viewWidth` is zero.
  public func localPoint(_ point: Point, info: PageInfo) -> CGPoint {
    let geometry = geometry(for: info)
    let scale = Self.ratio(transform.viewWidth, over: geometry.displaySize.width)
    let shown = geometry.toDisplay(point)
    return CGPoint(x: shown.x * scale, y: shown.y * scale)
  }

  /// `length / divisor`, or 0 when `divisor` is not greater than zero.
  private static func ratio(_ length: Double, over divisor: Double) -> Double {
    divisor > 0 ? length / divisor : 0
  }
}
