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
  /// overlay with its origin at the page's top left.
  public func pagePoint(_ local: CGPoint, info: PageInfo) -> Point {
    let geometry = geometry(for: info)
    let scale = pointsPerViewPoint(geometry)
    return geometry.toPage(Point(x: Double(local.x) * scale, y: Double(local.y) * scale))
  }

  /// Where `pageRect`, a rect in unrotated page space, sits in this page's
  /// overlay, with the origin at the page's top left.
  public func localRect(_ pageRect: Rect, info: PageInfo) -> CGRect {
    let geometry = geometry(for: info)
    let scale = pointsPerViewPoint(geometry)
    let shown = geometry.toDisplay(pageRect)
    return CGRect(
      x: shown.x / scale, y: shown.y / scale, width: shown.width / scale,
      height: shown.height / scale)
  }

  /// PDF points of the page as shown per view point of the overlay.
  private func pointsPerViewPoint(_ geometry: PageGeometry) -> Double {
    geometry.displaySize.width / transform.viewWidth
  }
}
