public import CoreGraphics
public import EmbedPDFBindings

extension Point {
  /// The same point, from a CoreGraphics value.
  public init(_ point: CGPoint) {
    self.init(x: Double(point.x), y: Double(point.y))
  }
}

extension CGPoint {
  /// The same point, as a CoreGraphics value.
  public init(_ point: Point) {
    self.init(x: point.x, y: point.y)
  }
}

extension Size {
  /// The same size, from a CoreGraphics value.
  public init(_ size: CGSize) {
    self.init(width: Double(size.width), height: Double(size.height))
  }
}

extension CGSize {
  /// The same size, as a CoreGraphics value.
  public init(_ size: Size) {
    self.init(width: size.width, height: size.height)
  }
}

extension Rect {
  /// The same rect, from a CoreGraphics value. A rect with a negative width
  /// or height is standardized: the origin is its minimum corner.
  /// `CGRect.null` becomes an empty rect with its origin at positive
  /// infinity on both axes.
  public init(_ rect: CGRect) {
    self.init(
      x: Double(rect.minX), y: Double(rect.minY), width: Double(rect.width),
      height: Double(rect.height))
  }
}

extension CGRect {
  /// The same rect, as a CoreGraphics value.
  public init(_ rect: Rect) {
    self.init(x: rect.x, y: rect.y, width: rect.width, height: rect.height)
  }
}

extension Quad {
  /// The four corners of `rect`, with y pointing down: `upperLeft` is the
  /// rect's origin.
  public init(_ rect: Rect) {
    self.init(
      upperLeft: Point(x: rect.x, y: rect.y),
      upperRight: Point(x: rect.x + rect.width, y: rect.y),
      lowerLeft: Point(x: rect.x, y: rect.y + rect.height),
      lowerRight: Point(x: rect.x + rect.width, y: rect.y + rect.height))
  }
}
