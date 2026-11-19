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
  /// The clockwise turn of the page as shown. Pass it to `RenderPageOptions`
  /// so a render matches the page as shown.
  public let turn: RenderRotation
  /// The unrotated page size, as ``PageInfo`` reports it.
  public let pageSize: Size

  /// Creates the geometry for a page shown at `turn`.
  public init(turn: RenderRotation, pageSize: Size) {
    self.turn = turn
    self.pageSize = pageSize
  }

  /// Creates the geometry for a page turned `rotation` degrees clockwise.
  ///
  /// - Parameters:
  ///   - rotation: Degrees clockwise. Any value is accepted; it reduces to
  ///     `0..<360` and rounds to the nearest quarter turn.
  ///   - pageSize: The unrotated page size.
  public init(rotation: Int, pageSize: Size) {
    self.init(turn: RenderRotation(degrees: rotation), pageSize: pageSize)
  }

  /// Degrees clockwise: 0, 90, 180 or 270.
  public var rotation: Int { turn.degrees }

  /// The page size as shown: width and height swap at 90 and 270 degrees.
  public var displaySize: Size {
    turn.isQuarterTurn ? Size(width: pageSize.height, height: pageSize.width) : pageSize
  }

  /// ``displaySize`` as a CoreGraphics value.
  public var displayCGSize: CGSize { CGSize(displaySize) }

  /// Converts a point in unrotated page space to the page as shown.
  public func toDisplay(_ point: Point) -> Point {
    let w = pageSize.width
    let h = pageSize.height
    switch turn {
    case .none: return point
    case .clockwise90: return Point(x: h - point.y, y: point.x)
    case .clockwise180: return Point(x: w - point.x, y: h - point.y)
    case .clockwise270: return Point(x: point.y, y: w - point.x)
    }
  }

  /// Converts a point on the page as shown to unrotated page space.
  public func toPage(_ point: Point) -> Point {
    let w = pageSize.width
    let h = pageSize.height
    switch turn {
    case .none: return point
    case .clockwise90: return Point(x: point.y, y: h - point.x)
    case .clockwise180: return Point(x: w - point.x, y: h - point.y)
    case .clockwise270: return Point(x: w - point.y, y: point.x)
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

  /// ``toDisplay(_:)-(Point)`` for a CoreGraphics point.
  public func toDisplay(_ point: CGPoint) -> CGPoint {
    CGPoint(toDisplay(Point(point)))
  }

  /// ``toPage(_:)-(Point)`` for a CoreGraphics point.
  public func toPage(_ point: CGPoint) -> CGPoint {
    CGPoint(toPage(Point(point)))
  }

  /// ``toDisplay(_:)-(Rect)`` for a CoreGraphics rect.
  public func toDisplay(_ rect: CGRect) -> CGRect {
    CGRect(toDisplay(Rect(rect)))
  }

  /// ``toPage(_:)-(Rect)`` for a CoreGraphics rect.
  public func toPage(_ rect: CGRect) -> CGRect {
    CGRect(toPage(Rect(rect)))
  }

  /// Turns an unrotated page render clockwise by ``turn`` so it matches the
  /// page as shown.
  ///
  /// - Returns: The same instance when ``turn`` is `.none`.
  /// - Throws: ``PDFError/Code/operationFailed`` when no drawing context can
  ///   be made for the turned size.
  public func toDisplay(_ image: CGImage) throws(PDFError) -> CGImage {
    let angle: CGFloat
    switch turn {
    case .none: return image
    // CoreGraphics has y pointing up, so a clockwise turn is a negative angle.
    case .clockwise90: angle = -.pi / 2
    case .clockwise180: angle = -.pi
    case .clockwise270: angle = -.pi * 3 / 2
    }
    let width = turn.isQuarterTurn ? image.height : image.width
    let height = turn.isQuarterTurn ? image.width : image.height
    let space =
      image.colorSpace.flatMap { $0.model == .rgb ? $0 : nil }
      ?? CGColorSpace(name: CGColorSpace.sRGB)
    guard let space,
      let context = CGContext(
        data: nil, width: width, height: height, bitsPerComponent: 8, bytesPerRow: 0,
        space: space, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
    else {
      throw PDFError(
        .operationFailed, detail: "No drawing context for a \(width) by \(height) image.")
    }
    context.translateBy(x: CGFloat(width) / 2, y: CGFloat(height) / 2)
    context.rotate(by: angle)
    context.draw(
      image,
      in: CGRect(
        x: -CGFloat(image.width) / 2, y: -CGFloat(image.height) / 2,
        width: CGFloat(image.width), height: CGFloat(image.height)))
    guard let turned = context.makeImage() else {
      throw PDFError(
        .operationFailed, detail: "No image from the \(width) by \(height) drawing context.")
    }
    return turned
  }

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

extension RenderRotation {
  /// The quarter turn nearest `degrees` clockwise. Any value is accepted; it
  /// reduces to `0..<360` first, and a value halfway between two turns takes
  /// the larger.
  public init(degrees: Int) {
    var reduced = degrees % 360
    if reduced < 0 { reduced += 360 }
    switch (reduced + 45) / 90 % 4 {
    case 1: self = .clockwise90
    case 2: self = .clockwise180
    case 3: self = .clockwise270
    default: self = .none
    }
  }

  /// The quarter turn nearest `degrees` clockwise, as ``init(degrees:)-(Int)``
  /// rounds it. A value that is not finite is `.none`.
  public init(degrees: Double) {
    guard degrees.isFinite else {
      self = .none
      return
    }
    // The remainder is within one full turn, so the quarter count fits an Int.
    let quarters = (degrees.truncatingRemainder(dividingBy: 360) / 90 + 0.5).rounded(.down)
    self.init(degrees: Int(quarters) * 90)
  }

  /// Degrees clockwise: 0, 90, 180 or 270.
  public var degrees: Int {
    switch self {
    case .none: 0
    case .clockwise90: 90
    case .clockwise180: 180
    case .clockwise270: 270
    }
  }

  /// Whether the turn swaps width and height: 90 or 270 degrees.
  public var isQuarterTurn: Bool {
    switch self {
    case .none, .clockwise180: false
    case .clockwise90, .clockwise270: true
    }
  }
}

extension PageInfo {
  /// Converts between this page's unrotated space and the page as shown.
  public var geometry: PageGeometry {
    PageGeometry(
      turn: RenderRotation(degrees: Int(rotation)), pageSize: Size(width: width, height: height))
  }
}
