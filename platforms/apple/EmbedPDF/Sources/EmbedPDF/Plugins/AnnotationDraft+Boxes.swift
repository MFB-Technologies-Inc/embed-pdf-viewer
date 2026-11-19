public import EmbedPDFBindings
import Foundation

// Typed drafts and patches for free text, carets and links, and patches for
// stamps and file attachments. The rules are in `AnnotationDraft.swift`;
// each member's own rule is the engine's (`read_field` in
// `crates/epdf-engine/src/features/annotation/write/prepare.rs`).
//
// Stamps and file attachments have no draft: a create needs their image or
// file bytes beside the field bag (`resource_roles` in `prepare.rs`), which
// `AnnotationCapability.create` doesn't carry. Place a stamp with
// `AnnotationDraftsCapability.armStamp(_:targetWidth:)`.

// ── free text ───────────────────────────────────────────────────────────────

/// A new free text: a text box drawn on the page.
public struct FreeTextAnnotationDraft: AnnotationDraft, Hashable {
  /// The text box, page space, before rotation.
  public var box: Rect
  /// `/Contents`: the text, plain. The engine lays it out in the body
  /// style below. Styled runs are `richText`, which only the JSON-string
  /// overloads write; a draft that sets both needs `contents` to be
  /// `richText`'s plain text.
  public var contents: Nullable<String>?
  /// The body font: a standard font's name (`helvetica`) or a registered
  /// font's key.
  public var fontFamily: String?
  /// Points, above 0.
  public var fontSize: Double?
  public var fontColor: AnnotationColor?
  public var textAlign: AnnotationTextAlignment?
  public var verticalAlign: AnnotationVerticalAlignment?
  /// `/C`: the border.
  public var color: AnnotationColor?
  /// `/IC`, the fill. `.null` is none.
  public var interiorColor: Nullable<AnnotationColor>?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  /// The border width, 0 or more; 0 draws no border.
  public var strokeWidth: Double?
  public var borderStyle: AnnotationBorderStyle?
  /// The dash lengths, each 0 or more, at least one. `.null` is none.
  public var dashArray: Nullable<[Double]>?
  /// `/BE`: a cloudy border's intensity, above 0. `.null` is a plain border.
  public var cloudyIntensity: Nullable<Double>?
  /// Degrees. `.null` is none.
  public var rotation: Nullable<Double>?
  /// `.null` is the engine's default, a plain text box.
  public var intent: Nullable<AnnotationFreeTextIntent>?
  /// A callout's leader line. `.null` is none.
  public var calloutLine: Nullable<AnnotationCalloutLine>?
  /// The ending at the called-out end of `calloutLine`. `.null` is none.
  public var lineEnding: Nullable<AnnotationLineEnding>?
  public var blendMode: AnnotationBlendMode?
  /// The fields every annotation shares.
  public var common: AnnotationCommonFields

  public init(
    box: Rect, contents: Nullable<String>? = nil, fontFamily: String? = nil,
    fontSize: Double? = nil, fontColor: AnnotationColor? = nil,
    textAlign: AnnotationTextAlignment? = nil, verticalAlign: AnnotationVerticalAlignment? = nil,
    color: AnnotationColor? = nil, interiorColor: Nullable<AnnotationColor>? = nil,
    opacity: Double? = nil, strokeWidth: Double? = nil, borderStyle: AnnotationBorderStyle? = nil,
    dashArray: Nullable<[Double]>? = nil, cloudyIntensity: Nullable<Double>? = nil,
    rotation: Nullable<Double>? = nil, intent: Nullable<AnnotationFreeTextIntent>? = nil,
    calloutLine: Nullable<AnnotationCalloutLine>? = nil,
    lineEnding: Nullable<AnnotationLineEnding>? = nil, blendMode: AnnotationBlendMode? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.box = box
    self.contents = contents
    self.fontFamily = fontFamily
    self.fontSize = fontSize
    self.fontColor = fontColor
    self.textAlign = textAlign
    self.verticalAlign = verticalAlign
    self.color = color
    self.interiorColor = interiorColor
    self.opacity = opacity
    self.strokeWidth = strokeWidth
    self.borderStyle = borderStyle
    self.dashArray = dashArray
    self.cloudyIntensity = cloudyIntensity
    self.rotation = rotation
    self.intent = intent
    self.calloutLine = calloutLine
    self.lineEnding = lineEnding
    self.blendMode = blendMode
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try FreeTextFields(
      subtype: .freeText, box: box, contents: contents, fontFamily: fontFamily, fontSize: fontSize,
      fontColor: fontColor, textAlign: textAlign, verticalAlign: verticalAlign, color: color,
      interiorColor: interiorColor, opacity: opacity, strokeWidth: strokeWidth,
      borderStyle: borderStyle, dashArray: dashArray, cloudyIntensity: cloudyIntensity,
      rotation: rotation, intent: intent, calloutLine: calloutLine, lineEnding: lineEnding,
      blendMode: blendMode
    ).encode(to: encoder)
    try common.encode(to: encoder)
  }
}

/// A change to a free text, or to a free-text tool's defaults. A member
/// left `nil` keeps its value.
public struct FreeTextAnnotationPatch: AnnotationPatch, Hashable {
  /// Moves or resizes the text box.
  public var box: Rect?
  /// Replaces the text, plain. `.null` removes it.
  public var contents: Nullable<String>?
  public var fontFamily: String?
  public var fontSize: Double?
  public var fontColor: AnnotationColor?
  public var textAlign: AnnotationTextAlignment?
  public var verticalAlign: AnnotationVerticalAlignment?
  public var color: AnnotationColor?
  /// `.null` removes the fill.
  public var interiorColor: Nullable<AnnotationColor>?
  public var opacity: Double?
  public var strokeWidth: Double?
  public var borderStyle: AnnotationBorderStyle?
  /// `.null` removes the pattern.
  public var dashArray: Nullable<[Double]>?
  /// `.null` makes the border plain.
  public var cloudyIntensity: Nullable<Double>?
  /// `.null` removes the rotation.
  public var rotation: Nullable<Double>?
  /// `.null` removes the intent.
  public var intent: Nullable<AnnotationFreeTextIntent>?
  /// `.null` removes the leader line.
  public var calloutLine: Nullable<AnnotationCalloutLine>?
  /// `.null` removes the ending.
  public var lineEnding: Nullable<AnnotationLineEnding>?
  public var blendMode: AnnotationBlendMode?
  /// The fields every annotation shares. A patch leaves `nm` out.
  public var common: AnnotationCommonFields

  public init(
    box: Rect? = nil, contents: Nullable<String>? = nil, fontFamily: String? = nil,
    fontSize: Double? = nil, fontColor: AnnotationColor? = nil,
    textAlign: AnnotationTextAlignment? = nil, verticalAlign: AnnotationVerticalAlignment? = nil,
    color: AnnotationColor? = nil, interiorColor: Nullable<AnnotationColor>? = nil,
    opacity: Double? = nil, strokeWidth: Double? = nil, borderStyle: AnnotationBorderStyle? = nil,
    dashArray: Nullable<[Double]>? = nil, cloudyIntensity: Nullable<Double>? = nil,
    rotation: Nullable<Double>? = nil, intent: Nullable<AnnotationFreeTextIntent>? = nil,
    calloutLine: Nullable<AnnotationCalloutLine>? = nil,
    lineEnding: Nullable<AnnotationLineEnding>? = nil, blendMode: AnnotationBlendMode? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.box = box
    self.contents = contents
    self.fontFamily = fontFamily
    self.fontSize = fontSize
    self.fontColor = fontColor
    self.textAlign = textAlign
    self.verticalAlign = verticalAlign
    self.color = color
    self.interiorColor = interiorColor
    self.opacity = opacity
    self.strokeWidth = strokeWidth
    self.borderStyle = borderStyle
    self.dashArray = dashArray
    self.cloudyIntensity = cloudyIntensity
    self.rotation = rotation
    self.intent = intent
    self.calloutLine = calloutLine
    self.lineEnding = lineEnding
    self.blendMode = blendMode
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try FreeTextFields(
      subtype: nil, box: box, contents: contents, fontFamily: fontFamily, fontSize: fontSize,
      fontColor: fontColor, textAlign: textAlign, verticalAlign: verticalAlign, color: color,
      interiorColor: interiorColor, opacity: opacity, strokeWidth: strokeWidth,
      borderStyle: borderStyle, dashArray: dashArray, cloudyIntensity: cloudyIntensity,
      rotation: rotation, intent: intent, calloutLine: calloutLine, lineEnding: lineEnding,
      blendMode: blendMode
    ).encode(to: encoder)
    try common.encodeAsPatch(to: encoder)
  }
}

private struct FreeTextFields: Encodable {
  var subtype: AnnotationSubtype?
  var box: Rect?
  var contents: Nullable<String>?
  var fontFamily: String?
  var fontSize: Double?
  var fontColor: AnnotationColor?
  var textAlign: AnnotationTextAlignment?
  var verticalAlign: AnnotationVerticalAlignment?
  var color: AnnotationColor?
  var interiorColor: Nullable<AnnotationColor>?
  var opacity: Double?
  var strokeWidth: Double?
  var borderStyle: AnnotationBorderStyle?
  var dashArray: Nullable<[Double]>?
  var cloudyIntensity: Nullable<Double>?
  var rotation: Nullable<Double>?
  var intent: Nullable<AnnotationFreeTextIntent>?
  var calloutLine: Nullable<AnnotationCalloutLine>?
  var lineEnding: Nullable<AnnotationLineEnding>?
  var blendMode: AnnotationBlendMode?

  private enum CodingKeys: String, CodingKey {
    case subtype, box, contents, fontFamily, fontSize, fontColor, textAlign, verticalAlign, color,
      interiorColor, opacity, strokeWidth, borderStyle, dashArray, cloudyIntensity, rotation,
      intent, calloutLine, lineEnding, blendMode
  }

  func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(subtype?.rawValue, forKey: .subtype)
    try container.encodeIfPresent(box.map(WireRect.init), forKey: .box)
    try container.encodeIfPresent(contents, forKey: .contents)
    try container.encodeIfPresent(fontFamily, forKey: .fontFamily)
    try container.encodeIfPresent(fontSize, forKey: .fontSize)
    try container.encodeIfPresent(fontColor, forKey: .fontColor)
    try container.encodeIfPresent(textAlign, forKey: .textAlign)
    try container.encodeIfPresent(verticalAlign, forKey: .verticalAlign)
    try container.encodeIfPresent(color, forKey: .color)
    try container.encodeIfPresent(interiorColor, forKey: .interiorColor)
    try container.encodeIfPresent(opacity, forKey: .opacity)
    try container.encodeIfPresent(strokeWidth, forKey: .strokeWidth)
    try container.encodeIfPresent(borderStyle, forKey: .borderStyle)
    try container.encodeIfPresent(dashArray, forKey: .dashArray)
    try container.encodeIfPresent(cloudyIntensity, forKey: .cloudyIntensity)
    try container.encodeIfPresent(rotation, forKey: .rotation)
    try container.encodeIfPresent(intent, forKey: .intent)
    try container.encodeIfPresent(calloutLine, forKey: .calloutLine)
    try container.encodeIfPresent(lineEnding, forKey: .lineEnding)
    try container.encodeIfPresent(blendMode, forKey: .blendMode)
  }
}

// ── caret ───────────────────────────────────────────────────────────────────

/// A new caret: an insertion mark.
public struct CaretAnnotationDraft: AnnotationDraft, Hashable {
  /// The caret's box, page space, before rotation.
  public var box: Rect
  /// `/C`. A caret always has one, so there is no `.null`.
  public var color: AnnotationColor?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  /// Degrees. `.null` is none.
  public var rotation: Nullable<Double>?
  /// `.null` is an insertion.
  public var intent: Nullable<AnnotationCaretIntent>?
  /// `/Contents`: the text to insert. `.null` is none.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The fields every annotation shares.
  public var common: AnnotationCommonFields

  public init(
    box: Rect, color: AnnotationColor? = nil, opacity: Double? = nil,
    rotation: Nullable<Double>? = nil, intent: Nullable<AnnotationCaretIntent>? = nil,
    contents: Nullable<String>? = nil, blendMode: AnnotationBlendMode? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.box = box
    self.color = color
    self.opacity = opacity
    self.rotation = rotation
    self.intent = intent
    self.contents = contents
    self.blendMode = blendMode
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try CaretFields(
      subtype: .caret, box: box, color: color, opacity: opacity, rotation: rotation, intent: intent,
      contents: contents, blendMode: blendMode
    ).encode(to: encoder)
    try common.encode(to: encoder)
  }
}

/// A change to a caret, or to a caret tool's defaults. A member left `nil`
/// keeps its value.
public struct CaretAnnotationPatch: AnnotationPatch, Hashable {
  /// Moves or resizes the caret.
  public var box: Rect?
  public var color: AnnotationColor?
  public var opacity: Double?
  /// `.null` removes the rotation.
  public var rotation: Nullable<Double>?
  /// `.null` makes it an insertion.
  public var intent: Nullable<AnnotationCaretIntent>?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The fields every annotation shares. A patch leaves `nm` out.
  public var common: AnnotationCommonFields

  public init(
    box: Rect? = nil, color: AnnotationColor? = nil, opacity: Double? = nil,
    rotation: Nullable<Double>? = nil, intent: Nullable<AnnotationCaretIntent>? = nil,
    contents: Nullable<String>? = nil, blendMode: AnnotationBlendMode? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.box = box
    self.color = color
    self.opacity = opacity
    self.rotation = rotation
    self.intent = intent
    self.contents = contents
    self.blendMode = blendMode
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try CaretFields(
      subtype: nil, box: box, color: color, opacity: opacity, rotation: rotation, intent: intent,
      contents: contents, blendMode: blendMode
    ).encode(to: encoder)
    try common.encodeAsPatch(to: encoder)
  }
}

private struct CaretFields: Encodable {
  var subtype: AnnotationSubtype?
  var box: Rect?
  var color: AnnotationColor?
  var opacity: Double?
  var rotation: Nullable<Double>?
  var intent: Nullable<AnnotationCaretIntent>?
  var contents: Nullable<String>?
  var blendMode: AnnotationBlendMode?

  private enum CodingKeys: String, CodingKey {
    case subtype, box, color, opacity, rotation, intent, contents, blendMode
  }

  func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(subtype?.rawValue, forKey: .subtype)
    try container.encodeIfPresent(box.map(WireRect.init), forKey: .box)
    try container.encodeIfPresent(color, forKey: .color)
    try container.encodeIfPresent(opacity, forKey: .opacity)
    try container.encodeIfPresent(rotation, forKey: .rotation)
    try container.encodeIfPresent(intent, forKey: .intent)
    try container.encodeIfPresent(contents, forKey: .contents)
    try container.encodeIfPresent(blendMode, forKey: .blendMode)
  }
}

// ── link ────────────────────────────────────────────────────────────────────

/// A new link: an area that goes somewhere when activated. A link draws
/// nothing, so it takes no `blendMode`.
public struct LinkAnnotationDraft: AnnotationDraft, Hashable {
  /// `/Rect`, page space: the area that activates.
  public var rect: Rect
  public var target: AnnotationLinkTarget
  /// `/Contents`. `.null` is none.
  public var contents: Nullable<String>?
  /// The fields every annotation shares.
  public var common: AnnotationCommonFields

  public init(
    rect: Rect, target: AnnotationLinkTarget, contents: Nullable<String>? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.rect = rect
    self.target = target
    self.contents = contents
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try LinkFields(subtype: .link, rect: rect, target: .value(target), contents: contents)
      .encode(to: encoder)
    try common.encode(to: encoder)
  }
}

/// A change to a link. A member left `nil` keeps its value.
public struct LinkAnnotationPatch: AnnotationPatch, Hashable {
  /// Moves or resizes the area.
  public var rect: Rect?
  /// `.null` removes the target: the link goes nowhere.
  public var target: Nullable<AnnotationLinkTarget>?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  /// The fields every annotation shares. A patch leaves `nm` out.
  public var common: AnnotationCommonFields

  public init(
    rect: Rect? = nil, target: Nullable<AnnotationLinkTarget>? = nil,
    contents: Nullable<String>? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.rect = rect
    self.target = target
    self.contents = contents
    self.common = common
  }

  public func encode(to encoder: any Encoder) throws {
    try LinkFields(subtype: nil, rect: rect, target: target, contents: contents).encode(to: encoder)
    try common.encodeAsPatch(to: encoder)
  }
}

private struct LinkFields: Encodable {
  var subtype: AnnotationSubtype?
  var rect: Rect?
  var target: Nullable<AnnotationLinkTarget>?
  var contents: Nullable<String>?

  private enum CodingKeys: String, CodingKey {
    case subtype, rect, target, contents
  }

  func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(subtype?.rawValue, forKey: .subtype)
    try container.encodeIfPresent(rect.map(WireRect.init), forKey: .rect)
    try container.encodeIfPresent(target, forKey: .target)
    try container.encodeIfPresent(contents, forKey: .contents)
  }
}

// ── stamp ───────────────────────────────────────────────────────────────────

/// A change to a stamp. A member left `nil` keeps its value.
///
/// There is no `blendMode`: the engine sets `/BM` while it draws an
/// appearance, and it keeps a stamp's image as placed instead of drawing
/// one (`write/update.rs` in `crates/epdf-engine/src/features/annotation`),
/// so the field would be accepted and dropped.
public struct StampAnnotationPatch: AnnotationPatch, Hashable {
  /// Moves or resizes the stamp's box, page space, before rotation.
  public var box: Rect?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  /// Degrees. `.null` removes the rotation.
  public var rotation: Nullable<Double>?
  /// `/Name`: the stamp's name (`Approved`, or a custom one). `.null`
  /// removes it.
  public var name: Nullable<String>?
  /// How the image fills the box. `.null` removes it.
  public var fit: Nullable<AnnotationStampFit>?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  /// The fields every annotation shares. A patch leaves `nm` out.
  public var common: AnnotationCommonFields

  public init(
    box: Rect? = nil, opacity: Double? = nil, rotation: Nullable<Double>? = nil,
    name: Nullable<String>? = nil, fit: Nullable<AnnotationStampFit>? = nil,
    contents: Nullable<String>? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.box = box
    self.opacity = opacity
    self.rotation = rotation
    self.name = name
    self.fit = fit
    self.contents = contents
    self.common = common
  }

  private enum CodingKeys: String, CodingKey {
    case box, opacity, rotation, name, fit, contents
  }

  public func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(box.map(WireRect.init), forKey: .box)
    try container.encodeIfPresent(opacity, forKey: .opacity)
    try container.encodeIfPresent(rotation, forKey: .rotation)
    try container.encodeIfPresent(name, forKey: .name)
    try container.encodeIfPresent(fit, forKey: .fit)
    try container.encodeIfPresent(contents, forKey: .contents)
    try common.encodeAsPatch(to: encoder)
  }
}

// ── file attachment ─────────────────────────────────────────────────────────

/// A change to a file attachment's icon. A member left `nil` keeps its
/// value. The file itself is the attachment plugin's.
public struct FileAttachmentAnnotationPatch: AnnotationPatch, Hashable {
  /// Moves or resizes the icon.
  public var rect: Rect?
  public var icon: AnnotationFileAttachmentIcon?
  /// `/C`.
  public var color: AnnotationColor?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  public var blendMode: AnnotationBlendMode?
  /// The fields every annotation shares. A patch leaves `nm` out.
  public var common: AnnotationCommonFields

  public init(
    rect: Rect? = nil, icon: AnnotationFileAttachmentIcon? = nil, color: AnnotationColor? = nil,
    opacity: Double? = nil, contents: Nullable<String>? = nil, blendMode: AnnotationBlendMode? = nil,
    common: AnnotationCommonFields = AnnotationCommonFields()
  ) {
    self.rect = rect
    self.icon = icon
    self.color = color
    self.opacity = opacity
    self.contents = contents
    self.blendMode = blendMode
    self.common = common
  }

  private enum CodingKeys: String, CodingKey {
    case rect, icon, color, opacity, contents, blendMode
  }

  public func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(rect.map(WireRect.init), forKey: .rect)
    try container.encodeIfPresent(icon, forKey: .icon)
    try container.encodeIfPresent(color, forKey: .color)
    try container.encodeIfPresent(opacity, forKey: .opacity)
    try container.encodeIfPresent(contents, forKey: .contents)
    try container.encodeIfPresent(blendMode, forKey: .blendMode)
    try common.encodeAsPatch(to: encoder)
  }
}
