public import EmbedPDFBindings
import Foundation

// Typed drafts and patches for the text markups (underline, squiggly,
// strikeout) and redactions. The rules are in `AnnotationDraft.swift`; each
// member's own rule is the engine's (`read_field` in
// `crates/epdf-engine/src/features/annotation/write/prepare.rs`).

// ── underline ───────────────────────────────────────────────────────────────

/// A new underline over text quads.
public struct UnderlineAnnotationDraft: AnnotationDraft, Hashable {
  /// `/QuadPoints`, page space: one quad per run of marked text, at least
  /// one.
  public var quadPoints: [Quad]
  /// `/C`. A text markup always has one, so there is no `.null`.
  public var color: AnnotationColor?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  /// `/Contents`. `.null` is none.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The comment as styled text (`/RC`). It sets `contents` to its plain
  /// text, and a draft that gives both needs them to agree. `.null` is a
  /// plain comment.
  public var richText: Nullable<AnnotationRichText>?
  /// The fields every annotation shares.
  public var common: AnnotationCommonFields

  public init(
    quadPoints: [Quad], color: AnnotationColor? = nil, opacity: Double? = nil,
    contents: Nullable<String>? = nil, blendMode: AnnotationBlendMode? = nil,
    richText: Nullable<AnnotationRichText>? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.quadPoints = quadPoints
    self.color = color
    self.opacity = opacity
    self.contents = contents
    self.blendMode = blendMode
    self.richText = richText
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try TextMarkupFields(
      subtype: .underline, quadPoints: quadPoints, color: color, opacity: opacity, contents: contents,
      blendMode: blendMode
    ).encode(to: encoder)
    try AnnotationSharedMembers.encode(richText: richText, to: encoder)
    try common.encode(to: encoder)
  }
}

/// A change to an underline, or to an underline tool's defaults. A member
/// left `nil` keeps its value.
public struct UnderlineAnnotationPatch: AnnotationPatch, Hashable {
  /// Replaces every quad.
  public var quadPoints: [Quad]?
  public var color: AnnotationColor?
  public var opacity: Double?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The comment as styled text (`/RC`). It sets `contents` to its plain
  /// text, and a patch that gives both needs them to agree. `.null` makes
  /// the comment plain and keeps `contents`.
  public var richText: Nullable<AnnotationRichText>?
  /// The fields every annotation shares. A patch leaves `nm` out.
  public var common: AnnotationCommonFields

  public init(
    quadPoints: [Quad]? = nil, color: AnnotationColor? = nil, opacity: Double? = nil,
    contents: Nullable<String>? = nil, blendMode: AnnotationBlendMode? = nil,
    richText: Nullable<AnnotationRichText>? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.quadPoints = quadPoints
    self.color = color
    self.opacity = opacity
    self.contents = contents
    self.blendMode = blendMode
    self.richText = richText
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try TextMarkupFields(
      subtype: nil, quadPoints: quadPoints, color: color, opacity: opacity, contents: contents,
      blendMode: blendMode
    ).encode(to: encoder)
    try AnnotationSharedMembers.encode(richText: richText, to: encoder)
    try common.encodeAsPatch(to: encoder)
  }
}

// ── squiggly ────────────────────────────────────────────────────────────────

/// A new squiggly underline over text quads.
public struct SquigglyAnnotationDraft: AnnotationDraft, Hashable {
  /// `/QuadPoints`, page space: one quad per run of marked text, at least
  /// one.
  public var quadPoints: [Quad]
  /// `/C`. A text markup always has one, so there is no `.null`.
  public var color: AnnotationColor?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  /// `/Contents`. `.null` is none.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The comment as styled text (`/RC`). It sets `contents` to its plain
  /// text, and a draft that gives both needs them to agree. `.null` is a
  /// plain comment.
  public var richText: Nullable<AnnotationRichText>?
  /// The fields every annotation shares.
  public var common: AnnotationCommonFields

  public init(
    quadPoints: [Quad], color: AnnotationColor? = nil, opacity: Double? = nil,
    contents: Nullable<String>? = nil, blendMode: AnnotationBlendMode? = nil,
    richText: Nullable<AnnotationRichText>? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.quadPoints = quadPoints
    self.color = color
    self.opacity = opacity
    self.contents = contents
    self.blendMode = blendMode
    self.richText = richText
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try TextMarkupFields(
      subtype: .squiggly, quadPoints: quadPoints, color: color, opacity: opacity, contents: contents,
      blendMode: blendMode
    ).encode(to: encoder)
    try AnnotationSharedMembers.encode(richText: richText, to: encoder)
    try common.encode(to: encoder)
  }
}

/// A change to a squiggly underline, or to a squiggly tool's defaults. A
/// member left `nil` keeps its value.
public struct SquigglyAnnotationPatch: AnnotationPatch, Hashable {
  /// Replaces every quad.
  public var quadPoints: [Quad]?
  public var color: AnnotationColor?
  public var opacity: Double?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The comment as styled text (`/RC`). It sets `contents` to its plain
  /// text, and a patch that gives both needs them to agree. `.null` makes
  /// the comment plain and keeps `contents`.
  public var richText: Nullable<AnnotationRichText>?
  /// The fields every annotation shares. A patch leaves `nm` out.
  public var common: AnnotationCommonFields

  public init(
    quadPoints: [Quad]? = nil, color: AnnotationColor? = nil, opacity: Double? = nil,
    contents: Nullable<String>? = nil, blendMode: AnnotationBlendMode? = nil,
    richText: Nullable<AnnotationRichText>? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.quadPoints = quadPoints
    self.color = color
    self.opacity = opacity
    self.contents = contents
    self.blendMode = blendMode
    self.richText = richText
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try TextMarkupFields(
      subtype: nil, quadPoints: quadPoints, color: color, opacity: opacity, contents: contents,
      blendMode: blendMode
    ).encode(to: encoder)
    try AnnotationSharedMembers.encode(richText: richText, to: encoder)
    try common.encodeAsPatch(to: encoder)
  }
}

// ── strikeout ───────────────────────────────────────────────────────────────

/// A new strikeout over text quads.
public struct StrikeoutAnnotationDraft: AnnotationDraft, Hashable {
  /// `/QuadPoints`, page space: one quad per run of marked text, at least
  /// one.
  public var quadPoints: [Quad]
  /// `/C`. A text markup always has one, so there is no `.null`.
  public var color: AnnotationColor?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  /// `.null` is a plain strikeout.
  public var intent: Nullable<AnnotationStrikeoutIntent>?
  /// `/Contents`. `.null` is none.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The comment as styled text (`/RC`). It sets `contents` to its plain
  /// text, and a draft that gives both needs them to agree. `.null` is a
  /// plain comment.
  public var richText: Nullable<AnnotationRichText>?
  /// The fields every annotation shares.
  public var common: AnnotationCommonFields

  public init(
    quadPoints: [Quad], color: AnnotationColor? = nil, opacity: Double? = nil,
    intent: Nullable<AnnotationStrikeoutIntent>? = nil, contents: Nullable<String>? = nil,
    blendMode: AnnotationBlendMode? = nil,
    richText: Nullable<AnnotationRichText>? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.quadPoints = quadPoints
    self.color = color
    self.opacity = opacity
    self.intent = intent
    self.contents = contents
    self.blendMode = blendMode
    self.richText = richText
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try TextMarkupFields(
      subtype: .strikeout, quadPoints: quadPoints, color: color, opacity: opacity, contents: contents,
      blendMode: blendMode, intent: intent
    ).encode(to: encoder)
    try AnnotationSharedMembers.encode(richText: richText, to: encoder)
    try common.encode(to: encoder)
  }
}

/// A change to a strikeout, or to a strikeout tool's defaults. A member left
/// `nil` keeps its value.
public struct StrikeoutAnnotationPatch: AnnotationPatch, Hashable {
  /// Replaces every quad.
  public var quadPoints: [Quad]?
  public var color: AnnotationColor?
  public var opacity: Double?
  /// `.null` makes it a plain strikeout.
  public var intent: Nullable<AnnotationStrikeoutIntent>?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The comment as styled text (`/RC`). It sets `contents` to its plain
  /// text, and a patch that gives both needs them to agree. `.null` makes
  /// the comment plain and keeps `contents`.
  public var richText: Nullable<AnnotationRichText>?
  /// The fields every annotation shares. A patch leaves `nm` out.
  public var common: AnnotationCommonFields

  public init(
    quadPoints: [Quad]? = nil, color: AnnotationColor? = nil, opacity: Double? = nil,
    intent: Nullable<AnnotationStrikeoutIntent>? = nil, contents: Nullable<String>? = nil,
    blendMode: AnnotationBlendMode? = nil,
    richText: Nullable<AnnotationRichText>? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.quadPoints = quadPoints
    self.color = color
    self.opacity = opacity
    self.intent = intent
    self.contents = contents
    self.blendMode = blendMode
    self.richText = richText
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try TextMarkupFields(
      subtype: nil, quadPoints: quadPoints, color: color, opacity: opacity, contents: contents,
      blendMode: blendMode, intent: intent
    ).encode(to: encoder)
    try AnnotationSharedMembers.encode(richText: richText, to: encoder)
    try common.encodeAsPatch(to: encoder)
  }
}

/// The field bag the three text markups share; `subtype` is `nil` on a
/// patch.
private struct TextMarkupFields: Encodable {
  var subtype: AnnotationSubtype?
  var quadPoints: [Quad]?
  var color: AnnotationColor?
  var opacity: Double?
  var contents: Nullable<String>?
  var blendMode: AnnotationBlendMode?
  var intent: Nullable<AnnotationStrikeoutIntent>?

  private enum CodingKeys: String, CodingKey {
    case subtype, quadPoints, color, opacity, contents, blendMode, intent
  }

  func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(subtype?.rawValue, forKey: .subtype)
    try container.encodeIfPresent(quadPoints.map { $0.map(WireQuad.init) }, forKey: .quadPoints)
    try container.encodeIfPresent(color, forKey: .color)
    try container.encodeIfPresent(opacity, forKey: .opacity)
    try container.encodeIfPresent(contents, forKey: .contents)
    try container.encodeIfPresent(blendMode, forKey: .blendMode)
    try container.encodeIfPresent(intent, forKey: .intent)
  }
}

// ── redaction ───────────────────────────────────────────────────────────────

/// A new redaction: marked for removal, applied later through the redaction
/// plugin. It marks text quads, an area (`rect`), or both.
public struct RedactionAnnotationDraft: AnnotationDraft, Hashable {
  /// `/QuadPoints`, page space: the text runs to remove. May be empty for an
  /// area redaction.
  public var quadPoints: [Quad]?
  /// `/Rect`, page space: the area. Left out, it bounds `quadPoints`.
  public var rect: Rect?
  /// `/C`: the outline drawn before the redaction is applied.
  public var color: AnnotationColor?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  /// `/IC`: what fills the area once applied. `.null` is no fill.
  public var interiorColor: Nullable<AnnotationColor>?
  /// `/OverlayText`: the text drawn over the area once applied. `.null` is
  /// none.
  public var overlayText: Nullable<String>?
  /// `/Repeat`: whether the overlay text tiles the area.
  public var `repeat`: Bool?
  /// The overlay text's font: a standard font's name (`helvetica`) or a
  /// registered font's key.
  public var fontFamily: String?
  /// Points, 0 or more; 0 fits the text to the area.
  public var fontSize: Double?
  public var fontColor: AnnotationColor?
  /// `justify` is refused.
  public var textAlign: AnnotationTextAlignment?
  /// `/Contents`. `.null` is none.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The comment as styled text (`/RC`). It sets `contents` to its plain
  /// text, and a draft that gives both needs them to agree. `.null` is a
  /// plain comment.
  public var richText: Nullable<AnnotationRichText>?
  /// The fields every annotation shares.
  public var common: AnnotationCommonFields

  public init(
    quadPoints: [Quad]? = nil, rect: Rect? = nil, color: AnnotationColor? = nil,
    opacity: Double? = nil, interiorColor: Nullable<AnnotationColor>? = nil,
    overlayText: Nullable<String>? = nil, repeat: Bool? = nil, fontFamily: String? = nil,
    fontSize: Double? = nil, fontColor: AnnotationColor? = nil,
    textAlign: AnnotationTextAlignment? = nil, contents: Nullable<String>? = nil,
    blendMode: AnnotationBlendMode? = nil,
    richText: Nullable<AnnotationRichText>? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.quadPoints = quadPoints
    self.rect = rect
    self.color = color
    self.opacity = opacity
    self.interiorColor = interiorColor
    self.overlayText = overlayText
    self.repeat = `repeat`
    self.fontFamily = fontFamily
    self.fontSize = fontSize
    self.fontColor = fontColor
    self.textAlign = textAlign
    self.contents = contents
    self.blendMode = blendMode
    self.richText = richText
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try RedactionFields(
      subtype: .redact, quadPoints: quadPoints, rect: rect, color: color, opacity: opacity,
      interiorColor: interiorColor, overlayText: overlayText, repeat: `repeat`,
      fontFamily: fontFamily, fontSize: fontSize, fontColor: fontColor, textAlign: textAlign,
      contents: contents, blendMode: blendMode
    ).encode(to: encoder)
    try AnnotationSharedMembers.encode(richText: richText, to: encoder)
    try common.encode(to: encoder)
  }
}

/// A change to a redaction, or to the redaction tool's defaults. A member
/// left `nil` keeps its value.
public struct RedactionAnnotationPatch: AnnotationPatch, Hashable {
  /// Replaces every quad.
  public var quadPoints: [Quad]?
  public var rect: Rect?
  public var color: AnnotationColor?
  public var opacity: Double?
  /// `.null` removes the fill.
  public var interiorColor: Nullable<AnnotationColor>?
  /// `.null` removes the overlay text.
  public var overlayText: Nullable<String>?
  public var `repeat`: Bool?
  public var fontFamily: String?
  public var fontSize: Double?
  public var fontColor: AnnotationColor?
  public var textAlign: AnnotationTextAlignment?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The comment as styled text (`/RC`). It sets `contents` to its plain
  /// text, and a patch that gives both needs them to agree. `.null` makes
  /// the comment plain and keeps `contents`.
  public var richText: Nullable<AnnotationRichText>?
  /// The fields every annotation shares. A patch leaves `nm` out.
  public var common: AnnotationCommonFields

  public init(
    quadPoints: [Quad]? = nil, rect: Rect? = nil, color: AnnotationColor? = nil,
    opacity: Double? = nil, interiorColor: Nullable<AnnotationColor>? = nil,
    overlayText: Nullable<String>? = nil, repeat: Bool? = nil, fontFamily: String? = nil,
    fontSize: Double? = nil, fontColor: AnnotationColor? = nil,
    textAlign: AnnotationTextAlignment? = nil, contents: Nullable<String>? = nil,
    blendMode: AnnotationBlendMode? = nil,
    richText: Nullable<AnnotationRichText>? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.quadPoints = quadPoints
    self.rect = rect
    self.color = color
    self.opacity = opacity
    self.interiorColor = interiorColor
    self.overlayText = overlayText
    self.repeat = `repeat`
    self.fontFamily = fontFamily
    self.fontSize = fontSize
    self.fontColor = fontColor
    self.textAlign = textAlign
    self.contents = contents
    self.blendMode = blendMode
    self.richText = richText
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try RedactionFields(
      subtype: nil, quadPoints: quadPoints, rect: rect, color: color, opacity: opacity,
      interiorColor: interiorColor, overlayText: overlayText, repeat: `repeat`,
      fontFamily: fontFamily, fontSize: fontSize, fontColor: fontColor, textAlign: textAlign,
      contents: contents, blendMode: blendMode
    ).encode(to: encoder)
    try AnnotationSharedMembers.encode(richText: richText, to: encoder)
    try common.encodeAsPatch(to: encoder)
  }
}

private struct RedactionFields: Encodable {
  var subtype: AnnotationSubtype?
  var quadPoints: [Quad]?
  var rect: Rect?
  var color: AnnotationColor?
  var opacity: Double?
  var interiorColor: Nullable<AnnotationColor>?
  var overlayText: Nullable<String>?
  var `repeat`: Bool?
  var fontFamily: String?
  var fontSize: Double?
  var fontColor: AnnotationColor?
  var textAlign: AnnotationTextAlignment?
  var contents: Nullable<String>?
  var blendMode: AnnotationBlendMode?

  private enum CodingKeys: String, CodingKey {
    case subtype, quadPoints, rect, color, opacity, interiorColor, overlayText, `repeat`,
      fontFamily, fontSize, fontColor, textAlign, contents, blendMode
  }

  func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(subtype?.rawValue, forKey: .subtype)
    try container.encodeIfPresent(quadPoints.map { $0.map(WireQuad.init) }, forKey: .quadPoints)
    try container.encodeIfPresent(rect.map(WireRect.init), forKey: .rect)
    try container.encodeIfPresent(color, forKey: .color)
    try container.encodeIfPresent(opacity, forKey: .opacity)
    try container.encodeIfPresent(interiorColor, forKey: .interiorColor)
    try container.encodeIfPresent(overlayText, forKey: .overlayText)
    try container.encodeIfPresent(`repeat`, forKey: .repeat)
    try container.encodeIfPresent(fontFamily, forKey: .fontFamily)
    try container.encodeIfPresent(fontSize, forKey: .fontSize)
    try container.encodeIfPresent(fontColor, forKey: .fontColor)
    try container.encodeIfPresent(textAlign, forKey: .textAlign)
    try container.encodeIfPresent(contents, forKey: .contents)
    try container.encodeIfPresent(blendMode, forKey: .blendMode)
  }
}
