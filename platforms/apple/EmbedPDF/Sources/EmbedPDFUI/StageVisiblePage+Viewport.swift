import CoreGraphics
import EmbedPDF

extension StageVisiblePage {
  /// The viewport point at `local`, a point in this page's overlay with its
  /// origin at the page's top left. Pass it to
  /// `StageCapability.viewportToPage(_:_:)` for the page point.
  public func viewportPoint(_ local: CGPoint) -> Point {
    Point(x: screenX + local.x, y: screenY + local.y)
  }
}
