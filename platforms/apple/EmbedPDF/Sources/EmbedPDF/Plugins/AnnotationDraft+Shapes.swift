public import EmbedPDFBindings
import Foundation

// Typed drafts and patches for the shapes: square, circle, line, polygon
// and polyline. The rules are in `AnnotationDraft.swift`; each member's own
// rule is the engine's (`read_field` in
// `crates/epdf-engine/src/features/annotation/write/prepare.rs`).
//
// A stroked shape's `color` takes `.null`, which draws no stroke. A
// dimension intent's scale (`measure`) is left to the JSON-string
// overloads.

// ── square and circle ───────────────────────────────────────────────────────

/// A new square: a rectangle, `/Square`.
public struct SquareAnnotationDraft: AnnotationDraft, Hashable {
  /// The rectangle, page space, before rotation. The engine works out
  /// `/Rect` from it, with room for the stroke.
  public var box: Rect
  /// `/C`, the stroke. `.null` draws none.
  public var color: Nullable<AnnotationColor>?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  /// The line width, 0 or more.
  public var strokeWidth: Double?
  public var borderStyle: AnnotationBorderStyle?
  /// The dash lengths, each 0 or more, at least one. `.null` is none.
  public var dashArray: Nullable<[Double]>?
  /// `/IC`, the fill. `.null` is none.
  public var interiorColor: Nullable<AnnotationColor>?
  /// Degrees. `.null` is none.
  public var rotation: Nullable<Double>?
  /// `/BE`: a cloudy border's intensity, above 0. `.null` is a plain border.
  public var cloudyIntensity: Nullable<Double>?
  /// `/Contents`. `.null` is none.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The fields every annotation shares.
  public var common: AnnotationCommonFields

  public init(
    box: Rect, color: Nullable<AnnotationColor>? = nil, opacity: Double? = nil,
    strokeWidth: Double? = nil, borderStyle: AnnotationBorderStyle? = nil,
    dashArray: Nullable<[Double]>? = nil, interiorColor: Nullable<AnnotationColor>? = nil,
    rotation: Nullable<Double>? = nil, cloudyIntensity: Nullable<Double>? = nil,
    contents: Nullable<String>? = nil, blendMode: AnnotationBlendMode? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.box = box
    self.color = color
    self.opacity = opacity
    self.strokeWidth = strokeWidth
    self.borderStyle = borderStyle
    self.dashArray = dashArray
    self.interiorColor = interiorColor
    self.rotation = rotation
    self.cloudyIntensity = cloudyIntensity
    self.contents = contents
    self.blendMode = blendMode
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try ShapeFields(
      subtype: .square, box: box, color: color, opacity: opacity, strokeWidth: strokeWidth,
      borderStyle: borderStyle, dashArray: dashArray, interiorColor: interiorColor,
      rotation: rotation, cloudyIntensity: cloudyIntensity, contents: contents, blendMode: blendMode
    ).encode(to: encoder)
    try common.encode(to: encoder)
  }
}

/// A change to a square, or to a square tool's defaults. A member left
/// `nil` keeps its value.
public struct SquareAnnotationPatch: AnnotationPatch, Hashable {
  /// Moves or resizes the rectangle.
  public var box: Rect?
  /// `.null` removes the stroke.
  public var color: Nullable<AnnotationColor>?
  public var opacity: Double?
  public var strokeWidth: Double?
  public var borderStyle: AnnotationBorderStyle?
  /// `.null` removes the pattern.
  public var dashArray: Nullable<[Double]>?
  /// `.null` removes the fill.
  public var interiorColor: Nullable<AnnotationColor>?
  /// `.null` removes the rotation.
  public var rotation: Nullable<Double>?
  /// `.null` makes the border plain.
  public var cloudyIntensity: Nullable<Double>?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The fields every annotation shares. A patch leaves `nm` out.
  public var common: AnnotationCommonFields

  public init(
    box: Rect? = nil, color: Nullable<AnnotationColor>? = nil, opacity: Double? = nil,
    strokeWidth: Double? = nil, borderStyle: AnnotationBorderStyle? = nil,
    dashArray: Nullable<[Double]>? = nil, interiorColor: Nullable<AnnotationColor>? = nil,
    rotation: Nullable<Double>? = nil, cloudyIntensity: Nullable<Double>? = nil,
    contents: Nullable<String>? = nil, blendMode: AnnotationBlendMode? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.box = box
    self.color = color
    self.opacity = opacity
    self.strokeWidth = strokeWidth
    self.borderStyle = borderStyle
    self.dashArray = dashArray
    self.interiorColor = interiorColor
    self.rotation = rotation
    self.cloudyIntensity = cloudyIntensity
    self.contents = contents
    self.blendMode = blendMode
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try ShapeFields(
      subtype: nil, box: box, color: color, opacity: opacity, strokeWidth: strokeWidth,
      borderStyle: borderStyle, dashArray: dashArray, interiorColor: interiorColor,
      rotation: rotation, cloudyIntensity: cloudyIntensity, contents: contents, blendMode: blendMode
    ).encode(to: encoder)
    try common.encodeAsPatch(to: encoder)
  }
}

/// A new circle: an ellipse in its box, `/Circle`.
public struct CircleAnnotationDraft: AnnotationDraft, Hashable {
  /// The ellipse's box, page space, before rotation.
  public var box: Rect
  /// `/C`, the stroke. `.null` draws none.
  public var color: Nullable<AnnotationColor>?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  /// The line width, 0 or more.
  public var strokeWidth: Double?
  public var borderStyle: AnnotationBorderStyle?
  /// The dash lengths, each 0 or more, at least one. `.null` is none.
  public var dashArray: Nullable<[Double]>?
  /// `/IC`, the fill. `.null` is none.
  public var interiorColor: Nullable<AnnotationColor>?
  /// Degrees. `.null` is none.
  public var rotation: Nullable<Double>?
  /// `/BE`: a cloudy border's intensity, above 0. `.null` is a plain border.
  public var cloudyIntensity: Nullable<Double>?
  /// `/Contents`. `.null` is none.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The fields every annotation shares.
  public var common: AnnotationCommonFields

  public init(
    box: Rect, color: Nullable<AnnotationColor>? = nil, opacity: Double? = nil,
    strokeWidth: Double? = nil, borderStyle: AnnotationBorderStyle? = nil,
    dashArray: Nullable<[Double]>? = nil, interiorColor: Nullable<AnnotationColor>? = nil,
    rotation: Nullable<Double>? = nil, cloudyIntensity: Nullable<Double>? = nil,
    contents: Nullable<String>? = nil, blendMode: AnnotationBlendMode? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.box = box
    self.color = color
    self.opacity = opacity
    self.strokeWidth = strokeWidth
    self.borderStyle = borderStyle
    self.dashArray = dashArray
    self.interiorColor = interiorColor
    self.rotation = rotation
    self.cloudyIntensity = cloudyIntensity
    self.contents = contents
    self.blendMode = blendMode
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try ShapeFields(
      subtype: .circle, box: box, color: color, opacity: opacity, strokeWidth: strokeWidth,
      borderStyle: borderStyle, dashArray: dashArray, interiorColor: interiorColor,
      rotation: rotation, cloudyIntensity: cloudyIntensity, contents: contents, blendMode: blendMode
    ).encode(to: encoder)
    try common.encode(to: encoder)
  }
}

/// A change to a circle, or to a circle tool's defaults. A member left
/// `nil` keeps its value.
public struct CircleAnnotationPatch: AnnotationPatch, Hashable {
  /// Moves or resizes the ellipse's box.
  public var box: Rect?
  /// `.null` removes the stroke.
  public var color: Nullable<AnnotationColor>?
  public var opacity: Double?
  public var strokeWidth: Double?
  public var borderStyle: AnnotationBorderStyle?
  /// `.null` removes the pattern.
  public var dashArray: Nullable<[Double]>?
  /// `.null` removes the fill.
  public var interiorColor: Nullable<AnnotationColor>?
  /// `.null` removes the rotation.
  public var rotation: Nullable<Double>?
  /// `.null` makes the border plain.
  public var cloudyIntensity: Nullable<Double>?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The fields every annotation shares. A patch leaves `nm` out.
  public var common: AnnotationCommonFields

  public init(
    box: Rect? = nil, color: Nullable<AnnotationColor>? = nil, opacity: Double? = nil,
    strokeWidth: Double? = nil, borderStyle: AnnotationBorderStyle? = nil,
    dashArray: Nullable<[Double]>? = nil, interiorColor: Nullable<AnnotationColor>? = nil,
    rotation: Nullable<Double>? = nil, cloudyIntensity: Nullable<Double>? = nil,
    contents: Nullable<String>? = nil, blendMode: AnnotationBlendMode? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.box = box
    self.color = color
    self.opacity = opacity
    self.strokeWidth = strokeWidth
    self.borderStyle = borderStyle
    self.dashArray = dashArray
    self.interiorColor = interiorColor
    self.rotation = rotation
    self.cloudyIntensity = cloudyIntensity
    self.contents = contents
    self.blendMode = blendMode
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try ShapeFields(
      subtype: nil, box: box, color: color, opacity: opacity, strokeWidth: strokeWidth,
      borderStyle: borderStyle, dashArray: dashArray, interiorColor: interiorColor,
      rotation: rotation, cloudyIntensity: cloudyIntensity, contents: contents, blendMode: blendMode
    ).encode(to: encoder)
    try common.encodeAsPatch(to: encoder)
  }
}

// ── line ────────────────────────────────────────────────────────────────────

/// A new line between two points.
public struct LineAnnotationDraft: AnnotationDraft, Hashable {
  /// `/L`, page space.
  public var start: Point
  public var end: Point
  /// `/C`, the stroke. `.null` draws none.
  public var color: Nullable<AnnotationColor>?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  /// The line width, 0 or more.
  public var strokeWidth: Double?
  public var borderStyle: AnnotationBorderStyle?
  /// The dash lengths, each 0 or more, at least one. `.null` is none.
  public var dashArray: Nullable<[Double]>?
  /// `/IC`: what fills a closed line ending. `.null` is none.
  public var interiorColor: Nullable<AnnotationColor>?
  public var lineEndings: AnnotationLineEndings?
  /// Degrees. `.null` is none.
  public var rotation: Nullable<Double>?
  /// `.null` is a plain line.
  public var intent: Nullable<AnnotationLineIntent>?
  /// `/Cap`: whether `contents` shows as a caption on the line.
  public var captionEnabled: Bool?
  public var captionPosition: AnnotationCaptionPosition?
  /// `/CO`. `.null` is none.
  public var captionOffset: Nullable<AnnotationCaptionOffset>?
  /// `/LL`, `/LLE` and `/LLO`. `.null` is none.
  public var leader: Nullable<AnnotationLineLeader>?
  /// `/Contents`, and the caption when `captionEnabled`. `.null` is none.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The fields every annotation shares.
  public var common: AnnotationCommonFields

  public init(
    start: Point, end: Point, color: Nullable<AnnotationColor>? = nil, opacity: Double? = nil,
    strokeWidth: Double? = nil, borderStyle: AnnotationBorderStyle? = nil,
    dashArray: Nullable<[Double]>? = nil, interiorColor: Nullable<AnnotationColor>? = nil,
    lineEndings: AnnotationLineEndings? = nil, rotation: Nullable<Double>? = nil,
    intent: Nullable<AnnotationLineIntent>? = nil, captionEnabled: Bool? = nil,
    captionPosition: AnnotationCaptionPosition? = nil,
    captionOffset: Nullable<AnnotationCaptionOffset>? = nil,
    leader: Nullable<AnnotationLineLeader>? = nil, contents: Nullable<String>? = nil,
    blendMode: AnnotationBlendMode? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.start = start
    self.end = end
    self.color = color
    self.opacity = opacity
    self.strokeWidth = strokeWidth
    self.borderStyle = borderStyle
    self.dashArray = dashArray
    self.interiorColor = interiorColor
    self.lineEndings = lineEndings
    self.rotation = rotation
    self.intent = intent
    self.captionEnabled = captionEnabled
    self.captionPosition = captionPosition
    self.captionOffset = captionOffset
    self.leader = leader
    self.contents = contents
    self.blendMode = blendMode
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try LineFields(
      subtype: .line, linePoints: WireLinePoints(start: start, end: end), color: color,
      opacity: opacity, strokeWidth: strokeWidth, borderStyle: borderStyle, dashArray: dashArray,
      interiorColor: interiorColor, lineEndings: lineEndings, rotation: rotation, intent: intent,
      captionEnabled: captionEnabled, captionPosition: captionPosition,
      captionOffset: captionOffset, leader: leader, contents: contents, blendMode: blendMode
    ).encode(to: encoder)
    try common.encode(to: encoder)
  }
}

/// A change to a line, or to a line tool's defaults. A member left `nil`
/// keeps its value.
public struct LineAnnotationPatch: AnnotationPatch, Hashable {
  /// Moves the line. Set both or neither: `/L` is written whole.
  public var start: Point?
  public var end: Point?
  /// `.null` removes the stroke.
  public var color: Nullable<AnnotationColor>?
  public var opacity: Double?
  public var strokeWidth: Double?
  public var borderStyle: AnnotationBorderStyle?
  /// `.null` removes the pattern.
  public var dashArray: Nullable<[Double]>?
  /// `.null` removes the fill.
  public var interiorColor: Nullable<AnnotationColor>?
  public var lineEndings: AnnotationLineEndings?
  /// `.null` removes the rotation.
  public var rotation: Nullable<Double>?
  /// `.null` makes it a plain line.
  public var intent: Nullable<AnnotationLineIntent>?
  public var captionEnabled: Bool?
  public var captionPosition: AnnotationCaptionPosition?
  /// `.null` removes the offset.
  public var captionOffset: Nullable<AnnotationCaptionOffset>?
  /// `.null` removes the leader lines.
  public var leader: Nullable<AnnotationLineLeader>?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The fields every annotation shares. A patch leaves `nm` out.
  public var common: AnnotationCommonFields

  public init(
    start: Point? = nil, end: Point? = nil, color: Nullable<AnnotationColor>? = nil,
    opacity: Double? = nil, strokeWidth: Double? = nil, borderStyle: AnnotationBorderStyle? = nil,
    dashArray: Nullable<[Double]>? = nil, interiorColor: Nullable<AnnotationColor>? = nil,
    lineEndings: AnnotationLineEndings? = nil, rotation: Nullable<Double>? = nil,
    intent: Nullable<AnnotationLineIntent>? = nil, captionEnabled: Bool? = nil,
    captionPosition: AnnotationCaptionPosition? = nil,
    captionOffset: Nullable<AnnotationCaptionOffset>? = nil,
    leader: Nullable<AnnotationLineLeader>? = nil, contents: Nullable<String>? = nil,
    blendMode: AnnotationBlendMode? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.start = start
    self.end = end
    self.color = color
    self.opacity = opacity
    self.strokeWidth = strokeWidth
    self.borderStyle = borderStyle
    self.dashArray = dashArray
    self.interiorColor = interiorColor
    self.lineEndings = lineEndings
    self.rotation = rotation
    self.intent = intent
    self.captionEnabled = captionEnabled
    self.captionPosition = captionPosition
    self.captionOffset = captionOffset
    self.leader = leader
    self.contents = contents
    self.blendMode = blendMode
    self.common = common
  }

  /// Throws `invalid-input` when only one of `start` and `end` is set.
  public func encode(to encoder: any Encoder) throws {
    let linePoints: WireLinePoints?
    switch (start, end) {
    case let (start?, end?): linePoints = WireLinePoints(start: start, end: end)
    case (nil, nil): linePoints = nil
    default:
      throw EncodingError.invalidValue(
        self, .init(codingPath: encoder.codingPath, debugDescription: "a line patch sets both start and end, or neither"))
    }
    try LineFields(
      subtype: nil, linePoints: linePoints, color: color, opacity: opacity,
      strokeWidth: strokeWidth, borderStyle: borderStyle, dashArray: dashArray,
      interiorColor: interiorColor, lineEndings: lineEndings, rotation: rotation, intent: intent,
      captionEnabled: captionEnabled, captionPosition: captionPosition,
      captionOffset: captionOffset, leader: leader, contents: contents, blendMode: blendMode
    ).encode(to: encoder)
    try common.encodeAsPatch(to: encoder)
  }
}

private struct LineFields: Encodable {
  var subtype: AnnotationSubtype?
  var linePoints: WireLinePoints?
  var color: Nullable<AnnotationColor>?
  var opacity: Double?
  var strokeWidth: Double?
  var borderStyle: AnnotationBorderStyle?
  var dashArray: Nullable<[Double]>?
  var interiorColor: Nullable<AnnotationColor>?
  var lineEndings: AnnotationLineEndings?
  var rotation: Nullable<Double>?
  var intent: Nullable<AnnotationLineIntent>?
  var captionEnabled: Bool?
  var captionPosition: AnnotationCaptionPosition?
  var captionOffset: Nullable<AnnotationCaptionOffset>?
  var leader: Nullable<AnnotationLineLeader>?
  var contents: Nullable<String>?
  var blendMode: AnnotationBlendMode?

  private enum CodingKeys: String, CodingKey {
    case subtype, linePoints, color, opacity, strokeWidth, borderStyle, dashArray, interiorColor,
      lineEndings, rotation, intent, captionEnabled, captionPosition, captionOffset, leader,
      contents, blendMode
  }

  func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(subtype?.rawValue, forKey: .subtype)
    try container.encodeIfPresent(linePoints, forKey: .linePoints)
    try container.encodeIfPresent(color, forKey: .color)
    try container.encodeIfPresent(opacity, forKey: .opacity)
    try container.encodeIfPresent(strokeWidth, forKey: .strokeWidth)
    try container.encodeIfPresent(borderStyle, forKey: .borderStyle)
    try container.encodeIfPresent(dashArray, forKey: .dashArray)
    try container.encodeIfPresent(interiorColor, forKey: .interiorColor)
    try container.encodeIfPresent(lineEndings, forKey: .lineEndings)
    try container.encodeIfPresent(rotation, forKey: .rotation)
    try container.encodeIfPresent(intent, forKey: .intent)
    try container.encodeIfPresent(captionEnabled, forKey: .captionEnabled)
    try container.encodeIfPresent(captionPosition, forKey: .captionPosition)
    try container.encodeIfPresent(captionOffset, forKey: .captionOffset)
    try container.encodeIfPresent(leader, forKey: .leader)
    try container.encodeIfPresent(contents, forKey: .contents)
    try container.encodeIfPresent(blendMode, forKey: .blendMode)
  }
}

// ── polygon and polyline ────────────────────────────────────────────────────

/// A new polygon: a closed path through its vertices.
public struct PolygonAnnotationDraft: AnnotationDraft, Hashable {
  /// `/Vertices`, page space, at least three.
  public var vertices: [Point]
  /// `/C`, the stroke. `.null` draws none.
  public var color: Nullable<AnnotationColor>?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  /// The line width, 0 or more.
  public var strokeWidth: Double?
  public var borderStyle: AnnotationBorderStyle?
  /// The dash lengths, each 0 or more, at least one. `.null` is none.
  public var dashArray: Nullable<[Double]>?
  /// `/IC`, the fill. `.null` is none.
  public var interiorColor: Nullable<AnnotationColor>?
  /// Degrees. `.null` is none.
  public var rotation: Nullable<Double>?
  /// `.null` is a plain polygon.
  public var intent: Nullable<AnnotationPolygonIntent>?
  /// `/BE`: a cloudy border's intensity, above 0. `.null` is a plain border.
  public var cloudyIntensity: Nullable<Double>?
  /// Whether `contents` shows as a caption. `.null` is the engine's choice.
  public var captionEnabled: Nullable<Bool>?
  public var captionCenter: AnnotationCaptionCenter?
  /// `/Contents`, and the caption when `captionEnabled`. `.null` is none.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The fields every annotation shares.
  public var common: AnnotationCommonFields

  public init(
    vertices: [Point], color: Nullable<AnnotationColor>? = nil, opacity: Double? = nil,
    strokeWidth: Double? = nil, borderStyle: AnnotationBorderStyle? = nil,
    dashArray: Nullable<[Double]>? = nil, interiorColor: Nullable<AnnotationColor>? = nil,
    rotation: Nullable<Double>? = nil, intent: Nullable<AnnotationPolygonIntent>? = nil,
    cloudyIntensity: Nullable<Double>? = nil, captionEnabled: Nullable<Bool>? = nil,
    captionCenter: AnnotationCaptionCenter? = nil, contents: Nullable<String>? = nil,
    blendMode: AnnotationBlendMode? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.vertices = vertices
    self.color = color
    self.opacity = opacity
    self.strokeWidth = strokeWidth
    self.borderStyle = borderStyle
    self.dashArray = dashArray
    self.interiorColor = interiorColor
    self.rotation = rotation
    self.intent = intent
    self.cloudyIntensity = cloudyIntensity
    self.captionEnabled = captionEnabled
    self.captionCenter = captionCenter
    self.contents = contents
    self.blendMode = blendMode
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try PathFields(
      subtype: .polygon, vertices: vertices, color: color, opacity: opacity,
      strokeWidth: strokeWidth, borderStyle: borderStyle, dashArray: dashArray,
      interiorColor: interiorColor, rotation: rotation, intent: intent?.map(\.rawValue),
      cloudyIntensity: cloudyIntensity, lineEndings: nil, captionEnabled: captionEnabled,
      captionCenter: captionCenter, contents: contents, blendMode: blendMode
    ).encode(to: encoder)
    try common.encode(to: encoder)
  }
}

/// A change to a polygon, or to a polygon tool's defaults. A member left
/// `nil` keeps its value.
public struct PolygonAnnotationPatch: AnnotationPatch, Hashable {
  /// Replaces every vertex.
  public var vertices: [Point]?
  /// `.null` removes the stroke.
  public var color: Nullable<AnnotationColor>?
  public var opacity: Double?
  public var strokeWidth: Double?
  public var borderStyle: AnnotationBorderStyle?
  /// `.null` removes the pattern.
  public var dashArray: Nullable<[Double]>?
  /// `.null` removes the fill.
  public var interiorColor: Nullable<AnnotationColor>?
  /// `.null` removes the rotation.
  public var rotation: Nullable<Double>?
  /// `.null` makes it a plain polygon.
  public var intent: Nullable<AnnotationPolygonIntent>?
  /// `.null` makes the border plain.
  public var cloudyIntensity: Nullable<Double>?
  public var captionEnabled: Nullable<Bool>?
  public var captionCenter: AnnotationCaptionCenter?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The fields every annotation shares. A patch leaves `nm` out.
  public var common: AnnotationCommonFields

  public init(
    vertices: [Point]? = nil, color: Nullable<AnnotationColor>? = nil, opacity: Double? = nil,
    strokeWidth: Double? = nil, borderStyle: AnnotationBorderStyle? = nil,
    dashArray: Nullable<[Double]>? = nil, interiorColor: Nullable<AnnotationColor>? = nil,
    rotation: Nullable<Double>? = nil, intent: Nullable<AnnotationPolygonIntent>? = nil,
    cloudyIntensity: Nullable<Double>? = nil, captionEnabled: Nullable<Bool>? = nil,
    captionCenter: AnnotationCaptionCenter? = nil, contents: Nullable<String>? = nil,
    blendMode: AnnotationBlendMode? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.vertices = vertices
    self.color = color
    self.opacity = opacity
    self.strokeWidth = strokeWidth
    self.borderStyle = borderStyle
    self.dashArray = dashArray
    self.interiorColor = interiorColor
    self.rotation = rotation
    self.intent = intent
    self.cloudyIntensity = cloudyIntensity
    self.captionEnabled = captionEnabled
    self.captionCenter = captionCenter
    self.contents = contents
    self.blendMode = blendMode
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try PathFields(
      subtype: nil, vertices: vertices, color: color, opacity: opacity,
      strokeWidth: strokeWidth, borderStyle: borderStyle, dashArray: dashArray,
      interiorColor: interiorColor, rotation: rotation, intent: intent?.map(\.rawValue),
      cloudyIntensity: cloudyIntensity, lineEndings: nil, captionEnabled: captionEnabled,
      captionCenter: captionCenter, contents: contents, blendMode: blendMode
    ).encode(to: encoder)
    try common.encodeAsPatch(to: encoder)
  }
}

/// A new polyline: an open path through its vertices.
public struct PolylineAnnotationDraft: AnnotationDraft, Hashable {
  /// `/Vertices`, page space, at least two.
  public var vertices: [Point]
  /// `/C`, the stroke. `.null` draws none.
  public var color: Nullable<AnnotationColor>?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  /// The line width, 0 or more.
  public var strokeWidth: Double?
  public var borderStyle: AnnotationBorderStyle?
  /// The dash lengths, each 0 or more, at least one. `.null` is none.
  public var dashArray: Nullable<[Double]>?
  /// `/IC`: what fills a closed line ending. `.null` is none.
  public var interiorColor: Nullable<AnnotationColor>?
  public var lineEndings: AnnotationLineEndings?
  /// Degrees. `.null` is none.
  public var rotation: Nullable<Double>?
  /// `.null` is a plain polyline.
  public var intent: Nullable<AnnotationPolylineIntent>?
  /// Whether `contents` shows as a caption. `.null` is the engine's choice.
  public var captionEnabled: Nullable<Bool>?
  public var captionCenter: AnnotationCaptionCenter?
  /// `/Contents`, and the caption when `captionEnabled`. `.null` is none.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The fields every annotation shares.
  public var common: AnnotationCommonFields

  public init(
    vertices: [Point], color: Nullable<AnnotationColor>? = nil, opacity: Double? = nil,
    strokeWidth: Double? = nil, borderStyle: AnnotationBorderStyle? = nil,
    dashArray: Nullable<[Double]>? = nil, interiorColor: Nullable<AnnotationColor>? = nil,
    lineEndings: AnnotationLineEndings? = nil, rotation: Nullable<Double>? = nil,
    intent: Nullable<AnnotationPolylineIntent>? = nil, captionEnabled: Nullable<Bool>? = nil,
    captionCenter: AnnotationCaptionCenter? = nil, contents: Nullable<String>? = nil,
    blendMode: AnnotationBlendMode? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.vertices = vertices
    self.color = color
    self.opacity = opacity
    self.strokeWidth = strokeWidth
    self.borderStyle = borderStyle
    self.dashArray = dashArray
    self.interiorColor = interiorColor
    self.lineEndings = lineEndings
    self.rotation = rotation
    self.intent = intent
    self.captionEnabled = captionEnabled
    self.captionCenter = captionCenter
    self.contents = contents
    self.blendMode = blendMode
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try PathFields(
      subtype: .polyline, vertices: vertices, color: color, opacity: opacity,
      strokeWidth: strokeWidth, borderStyle: borderStyle, dashArray: dashArray,
      interiorColor: interiorColor, rotation: rotation, intent: intent?.map(\.rawValue),
      cloudyIntensity: nil, lineEndings: lineEndings, captionEnabled: captionEnabled,
      captionCenter: captionCenter, contents: contents, blendMode: blendMode
    ).encode(to: encoder)
    try common.encode(to: encoder)
  }
}

/// A change to a polyline, or to a polyline tool's defaults. A member left
/// `nil` keeps its value.
public struct PolylineAnnotationPatch: AnnotationPatch, Hashable {
  /// Replaces every vertex.
  public var vertices: [Point]?
  /// `.null` removes the stroke.
  public var color: Nullable<AnnotationColor>?
  public var opacity: Double?
  public var strokeWidth: Double?
  public var borderStyle: AnnotationBorderStyle?
  /// `.null` removes the pattern.
  public var dashArray: Nullable<[Double]>?
  /// `.null` removes the fill.
  public var interiorColor: Nullable<AnnotationColor>?
  public var lineEndings: AnnotationLineEndings?
  /// `.null` removes the rotation.
  public var rotation: Nullable<Double>?
  /// `.null` makes it a plain polyline.
  public var intent: Nullable<AnnotationPolylineIntent>?
  public var captionEnabled: Nullable<Bool>?
  public var captionCenter: AnnotationCaptionCenter?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The fields every annotation shares. A patch leaves `nm` out.
  public var common: AnnotationCommonFields

  public init(
    vertices: [Point]? = nil, color: Nullable<AnnotationColor>? = nil, opacity: Double? = nil,
    strokeWidth: Double? = nil, borderStyle: AnnotationBorderStyle? = nil,
    dashArray: Nullable<[Double]>? = nil, interiorColor: Nullable<AnnotationColor>? = nil,
    lineEndings: AnnotationLineEndings? = nil, rotation: Nullable<Double>? = nil,
    intent: Nullable<AnnotationPolylineIntent>? = nil, captionEnabled: Nullable<Bool>? = nil,
    captionCenter: AnnotationCaptionCenter? = nil, contents: Nullable<String>? = nil,
    blendMode: AnnotationBlendMode? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.vertices = vertices
    self.color = color
    self.opacity = opacity
    self.strokeWidth = strokeWidth
    self.borderStyle = borderStyle
    self.dashArray = dashArray
    self.interiorColor = interiorColor
    self.lineEndings = lineEndings
    self.rotation = rotation
    self.intent = intent
    self.captionEnabled = captionEnabled
    self.captionCenter = captionCenter
    self.contents = contents
    self.blendMode = blendMode
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try PathFields(
      subtype: nil, vertices: vertices, color: color, opacity: opacity,
      strokeWidth: strokeWidth, borderStyle: borderStyle, dashArray: dashArray,
      interiorColor: interiorColor, rotation: rotation, intent: intent?.map(\.rawValue),
      cloudyIntensity: nil, lineEndings: lineEndings, captionEnabled: captionEnabled,
      captionCenter: captionCenter, contents: contents, blendMode: blendMode
    ).encode(to: encoder)
    try common.encodeAsPatch(to: encoder)
  }
}

// ── shared field bags ───────────────────────────────────────────────────────

private struct ShapeFields: Encodable {
  var subtype: AnnotationSubtype?
  var box: Rect?
  var color: Nullable<AnnotationColor>?
  var opacity: Double?
  var strokeWidth: Double?
  var borderStyle: AnnotationBorderStyle?
  var dashArray: Nullable<[Double]>?
  var interiorColor: Nullable<AnnotationColor>?
  var rotation: Nullable<Double>?
  var cloudyIntensity: Nullable<Double>?
  var contents: Nullable<String>?
  var blendMode: AnnotationBlendMode?

  private enum CodingKeys: String, CodingKey {
    case subtype, box, color, opacity, strokeWidth, borderStyle, dashArray, interiorColor, rotation,
      cloudyIntensity, contents, blendMode
  }

  func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(subtype?.rawValue, forKey: .subtype)
    try container.encodeIfPresent(box.map(WireRect.init), forKey: .box)
    try container.encodeIfPresent(color, forKey: .color)
    try container.encodeIfPresent(opacity, forKey: .opacity)
    try container.encodeIfPresent(strokeWidth, forKey: .strokeWidth)
    try container.encodeIfPresent(borderStyle, forKey: .borderStyle)
    try container.encodeIfPresent(dashArray, forKey: .dashArray)
    try container.encodeIfPresent(interiorColor, forKey: .interiorColor)
    try container.encodeIfPresent(rotation, forKey: .rotation)
    try container.encodeIfPresent(cloudyIntensity, forKey: .cloudyIntensity)
    try container.encodeIfPresent(contents, forKey: .contents)
    try container.encodeIfPresent(blendMode, forKey: .blendMode)
  }
}

/// A polygon's or polyline's field bag; `intent` is already its wire text.
private struct PathFields: Encodable {
  var subtype: AnnotationSubtype?
  var vertices: [Point]?
  var color: Nullable<AnnotationColor>?
  var opacity: Double?
  var strokeWidth: Double?
  var borderStyle: AnnotationBorderStyle?
  var dashArray: Nullable<[Double]>?
  var interiorColor: Nullable<AnnotationColor>?
  var rotation: Nullable<Double>?
  var intent: Nullable<String>?
  var cloudyIntensity: Nullable<Double>?
  var lineEndings: AnnotationLineEndings?
  var captionEnabled: Nullable<Bool>?
  var captionCenter: AnnotationCaptionCenter?
  var contents: Nullable<String>?
  var blendMode: AnnotationBlendMode?

  private enum CodingKeys: String, CodingKey {
    case subtype, vertices, color, opacity, strokeWidth, borderStyle, dashArray, interiorColor,
      rotation, intent, cloudyIntensity, lineEndings, captionEnabled, captionCenter, contents,
      blendMode
  }

  func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(subtype?.rawValue, forKey: .subtype)
    try container.encodeIfPresent(vertices.map { $0.map(WirePoint.init) }, forKey: .vertices)
    try container.encodeIfPresent(color, forKey: .color)
    try container.encodeIfPresent(opacity, forKey: .opacity)
    try container.encodeIfPresent(strokeWidth, forKey: .strokeWidth)
    try container.encodeIfPresent(borderStyle, forKey: .borderStyle)
    try container.encodeIfPresent(dashArray, forKey: .dashArray)
    try container.encodeIfPresent(interiorColor, forKey: .interiorColor)
    try container.encodeIfPresent(rotation, forKey: .rotation)
    try container.encodeIfPresent(intent, forKey: .intent)
    try container.encodeIfPresent(cloudyIntensity, forKey: .cloudyIntensity)
    try container.encodeIfPresent(lineEndings, forKey: .lineEndings)
    try container.encodeIfPresent(captionEnabled, forKey: .captionEnabled)
    try container.encodeIfPresent(captionCenter, forKey: .captionCenter)
    try container.encodeIfPresent(contents, forKey: .contents)
    try container.encodeIfPresent(blendMode, forKey: .blendMode)
  }
}

extension Nullable {
  /// The value mapped, `.null` kept.
  func map<Other>(_ transform: (Value) -> Other) -> Nullable<Other> {
    switch self {
    case let .value(value): .value(transform(value))
    case .null: .null
    }
  }
}
