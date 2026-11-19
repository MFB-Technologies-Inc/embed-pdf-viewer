public import CoreGraphics
public import EmbedPDFBindings

/// Converts between a page's unrotated space and the page as it is shown,
/// turned clockwise by its rotation.
///
/// Engine geometry (annotation rects, text boxes, search hits) is in
/// unrotated page space. Both spaces have a top-left origin with y pointing
/// down, in PDF points. For a page of unrotated width `w` and height `h`,
/// ``toDisplay(_:)-(Point)`` maps `(x, y)` to `(h - y, x)` at 90 degrees,
/// `(w - x, h - y)` at 180 and `(y, w - x)` at 270, matching the engine's
/// render matrix. ``toPage(_:)-(Point)`` is its inverse.
public struct PageGeometry: Hashable, Sendable {
  /// Degrees clockwise: 0, 90, 180 or 270.
  public let rotation: Int
  /// The unrotated page size, as ``PageInfo`` reports it.
  public let pageSize: Size

  /// Creates the geometry for a page turned `rotation` degrees clockwise.
  ///
  /// - Parameters:
  ///   - rotation: Degrees clockwise. Any value is accepted; it reduces to
  ///     `0..<360` and rounds to the nearest quarter turn.
  ///   - pageSize: The unrotated page size.
  public init(rotation: Int, pageSize: Size) {
    var turn = rotation % 360
    if turn < 0 { turn += 360 }
    self.rotation = ((turn + 45) / 90 % 4) * 90
    self.pageSize = pageSize
  }

  /// The page size as shown: width and height swap at 90 and 270 degrees.
  public var displaySize: Size {
    isQuarterTurn ? Size(width: pageSize.height, height: pageSize.width) : pageSize
  }

  /// The rotation to pass to `RenderPageOptions` so a render matches the
  /// page as shown.
  public var renderRotation: RenderRotation {
    switch rotation {
    case 90: .clockwise90
    case 180: .clockwise180
    case 270: .clockwise270
    default: .none
    }
  }

  /// Converts a point in unrotated page space to the page as shown.
  public func toDisplay(_ point: Point) -> Point {
    let w = pageSize.width
    let h = pageSize.height
    switch rotation {
    case 90: return Point(x: h - point.y, y: point.x)
    case 180: return Point(x: w - point.x, y: h - point.y)
    case 270: return Point(x: point.y, y: w - point.x)
    default: return point
    }
  }

  /// Converts a point on the page as shown to unrotated page space.
  public func toPage(_ point: Point) -> Point {
    let w = pageSize.width
    let h = pageSize.height
    switch rotation {
    case 90: return Point(x: point.y, y: h - point.x)
    case 180: return Point(x: w - point.x, y: h - point.y)
    case 270: return Point(x: w - point.y, y: point.x)
    default: return point
    }
  }

  /// Converts a rect in unrotated page space to the page as shown: the
  /// bounding box of its four converted corners.
  public func toDisplay(_ rect: Rect) -> Rect {
    Self.bounds(of: rect, converting: toDisplay)
  }

  /// Converts a rect on the page as shown to unrotated page space: the
  /// bounding box of its four converted corners.
  public func toPage(_ rect: Rect) -> Rect {
    Self.bounds(of: rect, converting: toPage)
  }

  /// Turns an unrotated page render clockwise by ``rotation`` so it matches
  /// the page as shown.
  ///
  /// - Returns: The same instance at 0 degrees, and the source image when
  ///   no drawing context can be made for the turned size.
  public func toDisplay(_ image: CGImage) -> CGImage {
    guard rotation != 0 else { return image }
    let width = isQuarterTurn ? image.height : image.width
    let height = isQuarterTurn ? image.width : image.height
    let space =
      image.colorSpace.flatMap { $0.model == .rgb ? $0 : nil }
      ?? CGColorSpace(name: CGColorSpace.sRGB)
    guard let space,
      let context = CGContext(
        data: nil, width: width, height: height, bitsPerComponent: 8, bytesPerRow: 0,
        space: space, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
    else { return image }
    // CoreGraphics has y pointing up, so a clockwise turn is a negative angle.
    context.translateBy(x: CGFloat(width) / 2, y: CGFloat(height) / 2)
    context.rotate(by: -CGFloat(rotation) * .pi / 180)
    context.draw(
      image,
      in: CGRect(
        x: -CGFloat(image.width) / 2, y: -CGFloat(image.height) / 2,
        width: CGFloat(image.width), height: CGFloat(image.height)))
    return context.makeImage() ?? image
  }

  private var isQuarterTurn: Bool { rotation == 90 || rotation == 270 }

  private static func bounds(of rect: Rect, converting convert: (Point) -> Point) -> Rect {
    let corners = [
      Point(x: rect.x, y: rect.y),
      Point(x: rect.x + rect.width, y: rect.y),
      Point(x: rect.x, y: rect.y + rect.height),
      Point(x: rect.x + rect.width, y: rect.y + rect.height),
    ].map(convert)
    let xs = corners.map(\.x)
    let ys = corners.map(\.y)
    let minX = xs.min()!
    let minY = ys.min()!
    return Rect(x: minX, y: minY, width: xs.max()! - minX, height: ys.max()! - minY)
  }
}

extension PageInfo {
  /// Converts between this page's unrotated space and the page as shown.
  public var geometry: PageGeometry {
    PageGeometry(rotation: Int(rotation), pageSize: Size(width: width, height: height))
  }
}
