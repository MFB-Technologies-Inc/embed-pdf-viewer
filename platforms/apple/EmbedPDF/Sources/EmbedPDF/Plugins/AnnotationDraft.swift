public import EmbedPDFBindings
import Foundation

// Typed create drafts and patches, the protocols they share and the
// vocabulary their members take. This file has ink, highlight and note;
// `AnnotationDraft+Markup.swift`, `+Shapes.swift` and `+Boxes.swift` have
// the other subtypes.
//
// Each type encodes to the engine's field bag for its subtype, the JSON
// object `AnnotationCapability.create(_:on:tool:select:)`,
// `update(_:_:)` and `AnnotationToolsCapability.updateDefaults(_:for:)`
// otherwise take as a string. The engine refuses a key its kind doesn't
// declare (`assert_declared_fields` in
// `crates/epdf-engine/src/features/annotation/write/prepare.rs` and
// `write/patch.rs`), so every key here is one the kind declares
// (`kind_fields` in `prepare.rs`), and each value has the shape that
// kind's `read_field` reads. Geometry is page space, as reads hand it out:
// points `{x, y}` and boxes `{x, y, width, height}` from the page's
// top-left; the plugin moves them into the file's coordinates
// (`convert` in `crates/plugins/annotation-core/src/engine/space.rs`).
//
// A member left `nil` is left out of the JSON: on a draft that is the
// engine's default, on a patch it keeps the current value. A member the
// engine can set to nothing is a `Nullable`, whose `.null`
// writes `null`.
//
// Every type carries its subtype's data fields (`kind_fields`) and two base
// fields, `contents` and `blendMode`. A link and a stamp take no
// `blendMode`: the engine writes `/BM` while it draws an appearance, and it
// draws neither. The JSON-string overloads stay for
// what they leave out: the other base fields (`nm`, `subject`, the `/F`
// flags, `reply`, `groupId`), a markup comment's `richText`, a measurement's
// `measure`, and the widget and popup subtypes.

/// A typed create draft: the engine's field bag for one subtype, with its
/// `subtype`. `AnnotationCapability.create(_:on:tool:select:)` takes one.
public protocol AnnotationDraft: Encodable, Sendable {}

/// A typed patch: the members of one subtype to change.
/// `AnnotationCapability.update(_:_:)` and
/// `AnnotationToolsCapability.updateDefaults(_:for:)` take one.
public protocol AnnotationPatch: Encodable, Sendable {}

extension AnnotationDraft {
  /// The field bag as the engine reads it: keys sorted, so the same draft is
  /// always the same bytes. Throws `invalid-input` for a number JSON can't
  /// hold (NaN or an infinity).
  public func json() throws(PDFError) -> String {
    try AnnotationFieldBag.json(of: self)
  }
}

extension AnnotationPatch {
  /// The field bag as the engine reads it: keys sorted, unset members left
  /// out. Throws `invalid-input` for a number JSON can't hold (NaN or an
  /// infinity).
  public func json() throws(PDFError) -> String {
    try AnnotationFieldBag.json(of: self)
  }
}

/// A value, or JSON `null`: a member a draft or patch can set to nothing.
/// `.null` writes `null`. Leaving the member itself `nil` writes nothing,
/// which on a draft is the engine's default and on a patch keeps the
/// current value (naming.md, "Nothing: left out or null").
///
/// A literal of `Value`'s own kind is `.value`: `color: "#ff0000"`,
/// `rotation: 90`, `dashArray: [3, 2]`, `captionEnabled: true`.
public enum Nullable<Value: Encodable & Sendable & Hashable>: Encodable, Sendable, Hashable {
  case value(Value)
  /// Nothing: no stroke colour, no dash pattern, no rotation.
  case null

  public func encode(to encoder: any Encoder) throws {
    var container = encoder.singleValueContainer()
    switch self {
    case let .value(value): try container.encode(value)
    case .null: try container.encodeNil()
    }
  }
}

extension Nullable: ExpressibleByUnicodeScalarLiteral
where Value: ExpressibleByStringLiteral, Value.StringLiteralType == String {
  public init(unicodeScalarLiteral value: String) { self = .value(Value(stringLiteral: value)) }
}

extension Nullable: ExpressibleByExtendedGraphemeClusterLiteral
where Value: ExpressibleByStringLiteral, Value.StringLiteralType == String {
  public init(extendedGraphemeClusterLiteral value: String) { self = .value(Value(stringLiteral: value)) }
}

extension Nullable: ExpressibleByStringLiteral
where Value: ExpressibleByStringLiteral, Value.StringLiteralType == String {
  public init(stringLiteral value: String) { self = .value(Value(stringLiteral: value)) }
}

extension Nullable: ExpressibleByFloatLiteral where Value == Double {
  public init(floatLiteral value: Double) { self = .value(value) }
}

extension Nullable: ExpressibleByIntegerLiteral where Value == Double {
  public init(integerLiteral value: Int) { self = .value(Double(value)) }
}

extension Nullable: ExpressibleByBooleanLiteral where Value == Bool {
  public init(booleanLiteral value: Bool) { self = .value(value) }
}

extension Nullable: ExpressibleByArrayLiteral where Value == [Double] {
  public init(arrayLiteral elements: Double...) { self = .value(elements) }
}

/// The border styles the engine writes for an ink annotation. It reads
/// `beveled` and `inset` too, but refuses to write them on anything but a
/// widget (`read_field`, `"borderStyle"`, in
/// `crates/epdf-engine/src/features/annotation/write/prepare.rs`).
public enum AnnotationBorderStyle: String, Encodable, Sendable, Hashable, CaseIterable {
  case solid
  case dashed
}

/// An ink annotation's `/IT`: the one intent the engine writes for ink
/// (`intents_of` in `prepare.rs`). An ink without one is pen ink.
public enum AnnotationInkIntent: String, Encodable, Sendable, Hashable, CaseIterable {
  /// `ink-highlight`: a freehand highlighter.
  case inkHighlight = "ink-highlight"
}

/// A note's `/Name` icon, as the engine spells it (`NoteIcon` in
/// `crates/epdf-engine/src/features/annotation/vocabulary.rs`).
public enum AnnotationNoteIcon: String, Encodable, Sendable, Hashable, CaseIterable {
  case comment
  case key
  case note
  case help
  case newParagraph = "new-paragraph"
  case paragraph
  case insert
}

/// `/BM` on an annotation's appearance: how it blends with the page under
/// it. Every subtype takes one (`BASE_FIELDS` in `prepare.rs`); the
/// spellings are `BlendMode` in
/// `crates/epdf-engine/src/features/annotation/vocabulary.rs`.
public enum AnnotationBlendMode: String, Codable, Sendable, Hashable, CaseIterable {
  case normal
  case multiply
  case screen
  case overlay
  case darken
  case lighten
  case colorDodge = "color-dodge"
  case colorBurn = "color-burn"
  case hardLight = "hard-light"
  case softLight = "soft-light"
  case difference
  case exclusion
  case hue
  case saturation
  case color
  case luminosity
}

/// A `/LE` line ending (`LineEnding` in `vocabulary.rs`).
public enum AnnotationLineEnding: String, Codable, Sendable, Hashable, CaseIterable {
  /// `none`: the line just stops. Not `none`, so it can't be mistaken for an
  /// optional's `.none`.
  case noEnding = "none"
  case square
  case circle
  case diamond
  case openArrow = "open-arrow"
  case closedArrow = "closed-arrow"
  case butt
  case reversedOpenArrow = "r-open-arrow"
  case reversedClosedArrow = "r-closed-arrow"
  case slash
}

/// The `/LE` pair a line or polyline draws at its two ends.
public struct AnnotationLineEndings: Codable, Sendable, Hashable {
  public var start: AnnotationLineEnding
  public var end: AnnotationLineEnding

  public init(start: AnnotationLineEnding, end: AnnotationLineEnding) {
    self.start = start
    self.end = end
  }
}

/// How text lines up across its box (`/Q`). Only free text takes
/// `justify`; a redaction's overlay text refuses it.
public enum AnnotationTextAlignment: String, Codable, Sendable, Hashable, CaseIterable {
  case left
  case center
  case right
  case justify
}

/// Where free text sits in its box, top to bottom.
public enum AnnotationVerticalAlignment: String, Codable, Sendable, Hashable, CaseIterable {
  case top
  case middle
  case bottom
}

/// A strikeout's `/IT` (`intents_of` in `prepare.rs`). A strikeout without
/// one is a plain strikeout.
public enum AnnotationStrikeoutIntent: String, Encodable, Sendable, Hashable, CaseIterable {
  /// `strikeout-text-edit`: text marked for deletion.
  case strikeoutTextEdit = "strikeout-text-edit"
}

/// A line's `/IT`. A line without one is a plain line.
public enum AnnotationLineIntent: String, Encodable, Sendable, Hashable, CaseIterable {
  case lineArrow = "line-arrow"
  /// `line-dimension`: a distance measurement. Its scale is `measure`, which
  /// only the JSON-string overloads write.
  case lineDimension = "line-dimension"
}

/// A polygon's `/IT`. A polygon without one is a plain polygon.
public enum AnnotationPolygonIntent: String, Encodable, Sendable, Hashable, CaseIterable {
  case polygonCloud = "polygon-cloud"
  /// `polygon-dimension`: an area measurement. Its scale is `measure`, which
  /// only the JSON-string overloads write.
  case polygonDimension = "polygon-dimension"
}

/// A polyline's `/IT`. A polyline without one is a plain polyline.
public enum AnnotationPolylineIntent: String, Encodable, Sendable, Hashable, CaseIterable {
  /// `polyline-dimension`: a perimeter measurement. Its scale is `measure`,
  /// which only the JSON-string overloads write.
  case polylineDimension = "polyline-dimension"
}

/// A free text's `/IT`.
public enum AnnotationFreeTextIntent: String, Encodable, Sendable, Hashable, CaseIterable {
  /// A text box.
  case freeText = "free-text"
  /// A text box with a leader line, `calloutLine`, to what it calls out.
  case freeTextCallout = "free-text-callout"
}

/// A caret's `/IT`. A caret without one marks an insertion.
public enum AnnotationCaretIntent: String, Encodable, Sendable, Hashable, CaseIterable {
  /// `replace`: the caret of a replacement, paired with a strikeout.
  case replace
}

/// A line's `/CP`: where its caption sits.
public enum AnnotationCaptionPosition: String, Codable, Sendable, Hashable, CaseIterable {
  /// In the line, which breaks around it.
  case inline
  /// Above the line.
  case top
}

/// A line caption's displacement (`/CO`) in the line's own axes, in PDF
/// units: along the line from its midpoint, and away from it.
public struct AnnotationCaptionOffset: Codable, Sendable, Hashable {
  public var along: Double
  public var perpendicular: Double

  public init(along: Double, perpendicular: Double) {
    self.along = along
    self.perpendicular = perpendicular
  }
}

/// A line's leader lines: `/LL`, `/LLE` and `/LLO`.
public struct AnnotationLineLeader: Codable, Sendable, Hashable {
  /// `/LL`: how far the leader lines reach from the line's ends, along the
  /// line's normal. Negative reaches the other way.
  public var length: Double
  /// `/LLE`: how far the leader lines run past the line, 0 or more.
  public var `extension`: Double?
  /// `/LLO`: the gap between the points and the leader lines, 0 or more.
  public var offset: Double?

  public init(length: Double, extension: Double? = nil, offset: Double? = nil) {
    self.length = length
    self.extension = `extension`
    self.offset = offset
  }

}

/// Where a polygon's or polyline's caption sits.
public enum AnnotationCaptionCenter: Sendable, Hashable, Encodable {
  /// Placed by the engine. Writes `null`.
  case automatic
  /// Centred on this page-space point.
  case at(Point)

  public func encode(to encoder: any Encoder) throws {
    var container = encoder.singleValueContainer()
    switch self {
    case .automatic: try container.encodeNil()
    case let .at(point): try container.encode(WirePoint(point))
    }
  }
}

/// A free-text callout's leader line (`/CL`), page space: from the point
/// called out to the text box, straight or with one knee.
public enum AnnotationCalloutLine: Sendable, Hashable, Encodable {
  case straight(from: Point, to: Point)
  case kneed(from: Point, knee: Point, to: Point)

  /// The two or three points, in order.
  public var points: [Point] {
    switch self {
    case let .straight(from, to): [from, to]
    case let .kneed(from, knee, to): [from, knee, to]
    }
  }

  public func encode(to encoder: any Encoder) throws {
    var container = encoder.singleValueContainer()
    try container.encode(points.map(WirePoint.init))
  }
}

/// A stamp's fit: how its image fills its box.
public enum AnnotationStampFit: String, Codable, Sendable, Hashable, CaseIterable {
  case contain
  case cover
  case fill
}

/// A file attachment's `/Name` icon (`FileAttachmentIcon` in
/// `vocabulary.rs`).
public enum AnnotationFileAttachmentIcon: String, Codable, Sendable, Hashable, CaseIterable {
  case graph
  case pushPin = "push-pin"
  case paperclip
  case tag
}

/// The page verbs a link may name (`read_target` in `prepare.rs`).
public enum AnnotationLinkVerb: String, Codable, Sendable, Hashable, CaseIterable {
  case nextPage = "NextPage"
  case previousPage = "PrevPage"
  case firstPage = "FirstPage"
  case lastPage = "LastPage"
}

/// Where a link goes, as a link draft or patch writes it: the targets the
/// engine writes (`LinkTargetWrite` in `prepare.rs`). A link read from a
/// file can point elsewhere too (another file, a launch, a script); read
/// those from `AnnotationInfo.fieldsJSON`.
public enum AnnotationLinkTarget: Sendable, Hashable, Encodable {
  /// A place in this document.
  case goto(PdfDestination)
  case uri(String)
  case named(AnnotationLinkVerb)

  private enum CodingKeys: String, CodingKey {
    case kind, destination, uri, name
  }

  public func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    switch self {
    case let .goto(destination):
      try container.encode("goto", forKey: .kind)
      try container.encode(WireDestination(destination), forKey: .destination)
    case let .uri(uri):
      try container.encode("uri", forKey: .kind)
      try container.encode(uri, forKey: .uri)
    case let .named(verb):
      try container.encode("named", forKey: .kind)
      try container.encode(verb, forKey: .name)
    }
  }
}

// ── ink ─────────────────────────────────────────────────────────────────────

/// A new ink annotation: freehand strokes. Each member's rule is the
/// engine's (`read_field` in `prepare.rs`); a value outside it throws
/// `invalid-input` from the create.
public struct InkAnnotationDraft: AnnotationDraft, Hashable {
  /// `/InkList`, page space: one array of points per stroke. At least one
  /// stroke, and no stroke without points.
  public var inkList: [[Point]]
  /// `/C`. `.null` draws no stroke; left out takes the tool's or the
  /// engine's default.
  public var color: Nullable<AnnotationColor>?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  /// The line width, above 0.
  public var strokeWidth: Double?
  public var borderStyle: AnnotationBorderStyle?
  /// The dash lengths, each 0 or more, at least one. A dashed border with
  /// this left out takes the engine's default pattern; `.null` is none.
  public var dashArray: Nullable<[Double]>?
  /// `.null` is pen ink.
  public var intent: Nullable<AnnotationInkIntent>?
  /// Degrees. `.null` is none.
  public var rotation: Nullable<Double>?
  /// `/Contents`. `.null` is none.
  public var contents: Nullable<String>?
  /// How the appearance blends with the page under it (`/BM` on `/AP /N`).
  public var blendMode: AnnotationBlendMode?

  public init(
    inkList: [[Point]], color: Nullable<AnnotationColor>? = nil, opacity: Double? = nil,
    strokeWidth: Double? = nil, borderStyle: AnnotationBorderStyle? = nil,
    dashArray: Nullable<[Double]>? = nil,
    intent: Nullable<AnnotationInkIntent>? = nil,
    rotation: Nullable<Double>? = nil, contents: Nullable<String>? = nil,
    blendMode: AnnotationBlendMode? = nil
  ) {
    self.inkList = inkList
    self.color = color
    self.opacity = opacity
    self.strokeWidth = strokeWidth
    self.borderStyle = borderStyle
    self.dashArray = dashArray
    self.intent = intent
    self.rotation = rotation
    self.contents = contents
    self.blendMode = blendMode
  }

  private enum CodingKeys: String, CodingKey {
    case subtype, inkList, color, opacity, strokeWidth, borderStyle, dashArray, intent, rotation,
      contents, blendMode
  }

  public func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encode(AnnotationSubtype.ink.rawValue, forKey: .subtype)
    try container.encode(inkList.map { $0.map(WirePoint.init) }, forKey: .inkList)
    try container.encodeIfPresent(color, forKey: .color)
    try container.encodeIfPresent(opacity, forKey: .opacity)
    try container.encodeIfPresent(strokeWidth, forKey: .strokeWidth)
    try container.encodeIfPresent(borderStyle, forKey: .borderStyle)
    try container.encodeIfPresent(dashArray, forKey: .dashArray)
    try container.encodeIfPresent(intent, forKey: .intent)
    try container.encodeIfPresent(rotation, forKey: .rotation)
    try container.encodeIfPresent(contents, forKey: .contents)
    try container.encodeIfPresent(blendMode, forKey: .blendMode)
  }
}

/// A change to an ink annotation, or to an ink tool's defaults. A member
/// left `nil` keeps its value; the rules are the draft's
/// (`InkAnnotationDraft`).
public struct InkAnnotationPatch: AnnotationPatch, Hashable {
  /// Replaces every stroke.
  public var inkList: [[Point]]?
  /// `.null` removes the stroke colour: no stroke.
  public var color: Nullable<AnnotationColor>?
  public var opacity: Double?
  public var strokeWidth: Double?
  public var borderStyle: AnnotationBorderStyle?
  /// `.null` removes the pattern.
  public var dashArray: Nullable<[Double]>?
  /// `.null` makes it pen ink.
  public var intent: Nullable<AnnotationInkIntent>?
  /// `.null` removes the rotation.
  public var rotation: Nullable<Double>?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  /// How the appearance blends with the page under it (`/BM` on `/AP /N`).
  public var blendMode: AnnotationBlendMode?

  public init(
    inkList: [[Point]]? = nil, color: Nullable<AnnotationColor>? = nil, opacity: Double? = nil,
    strokeWidth: Double? = nil, borderStyle: AnnotationBorderStyle? = nil,
    dashArray: Nullable<[Double]>? = nil,
    intent: Nullable<AnnotationInkIntent>? = nil,
    rotation: Nullable<Double>? = nil, contents: Nullable<String>? = nil,
    blendMode: AnnotationBlendMode? = nil
  ) {
    self.inkList = inkList
    self.color = color
    self.opacity = opacity
    self.strokeWidth = strokeWidth
    self.borderStyle = borderStyle
    self.dashArray = dashArray
    self.intent = intent
    self.rotation = rotation
    self.contents = contents
    self.blendMode = blendMode
  }

  private enum CodingKeys: String, CodingKey {
    case inkList, color, opacity, strokeWidth, borderStyle, dashArray, intent, rotation, contents,
      blendMode
  }

  public func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(inkList.map { $0.map { $0.map(WirePoint.init) } }, forKey: .inkList)
    try container.encodeIfPresent(color, forKey: .color)
    try container.encodeIfPresent(opacity, forKey: .opacity)
    try container.encodeIfPresent(strokeWidth, forKey: .strokeWidth)
    try container.encodeIfPresent(borderStyle, forKey: .borderStyle)
    try container.encodeIfPresent(dashArray, forKey: .dashArray)
    try container.encodeIfPresent(intent, forKey: .intent)
    try container.encodeIfPresent(rotation, forKey: .rotation)
    try container.encodeIfPresent(contents, forKey: .contents)
    try container.encodeIfPresent(blendMode, forKey: .blendMode)
  }
}

// ── highlight ───────────────────────────────────────────────────────────────

/// A new highlight annotation over text quads.
public struct HighlightAnnotationDraft: AnnotationDraft, Hashable {
  /// `/QuadPoints`, page space: one quad per run of marked text, at least
  /// one.
  public var quadPoints: [Quad]
  /// `/C`. A highlight always has one, so there is no `.null`.
  public var color: AnnotationColor?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  /// `/Contents`. `.null` is none.
  public var contents: Nullable<String>?
  /// How the appearance blends with the page under it (`/BM` on `/AP /N`).
  public var blendMode: AnnotationBlendMode?

  public init(
    quadPoints: [Quad], color: AnnotationColor? = nil, opacity: Double? = nil,
    contents: Nullable<String>? = nil, blendMode: AnnotationBlendMode? = nil
  ) {
    self.quadPoints = quadPoints
    self.color = color
    self.opacity = opacity
    self.contents = contents
    self.blendMode = blendMode
  }

  private enum CodingKeys: String, CodingKey {
    case subtype, quadPoints, color, opacity, contents, blendMode
  }

  public func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encode(AnnotationSubtype.highlight.rawValue, forKey: .subtype)
    try container.encode(quadPoints.map(WireQuad.init), forKey: .quadPoints)
    try container.encodeIfPresent(color, forKey: .color)
    try container.encodeIfPresent(opacity, forKey: .opacity)
    try container.encodeIfPresent(contents, forKey: .contents)
    try container.encodeIfPresent(blendMode, forKey: .blendMode)
  }
}

/// A change to a highlight annotation, or to a highlight tool's defaults.
/// A member left `nil` keeps its value.
public struct HighlightAnnotationPatch: AnnotationPatch, Hashable {
  /// Replaces every quad.
  public var quadPoints: [Quad]?
  public var color: AnnotationColor?
  public var opacity: Double?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  /// How the appearance blends with the page under it (`/BM` on `/AP /N`).
  public var blendMode: AnnotationBlendMode?

  public init(
    quadPoints: [Quad]? = nil, color: AnnotationColor? = nil, opacity: Double? = nil,
    contents: Nullable<String>? = nil, blendMode: AnnotationBlendMode? = nil
  ) {
    self.quadPoints = quadPoints
    self.color = color
    self.opacity = opacity
    self.contents = contents
    self.blendMode = blendMode
  }

  private enum CodingKeys: String, CodingKey {
    case quadPoints, color, opacity, contents, blendMode
  }

  public func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(quadPoints.map { $0.map(WireQuad.init) }, forKey: .quadPoints)
    try container.encodeIfPresent(color, forKey: .color)
    try container.encodeIfPresent(opacity, forKey: .opacity)
    try container.encodeIfPresent(contents, forKey: .contents)
    try container.encodeIfPresent(blendMode, forKey: .blendMode)
  }
}

// ── note ────────────────────────────────────────────────────────────────────

/// A new note: a `text` annotation, a sticky note.
public struct NoteAnnotationDraft: AnnotationDraft, Hashable {
  /// `/Rect`, page space: where the icon sits.
  public var rect: Rect
  /// `/C`. A note always has one, so there is no `.null`.
  public var color: AnnotationColor?
  /// `/CA`, 0 to 1.
  public var opacity: Double?
  public var icon: AnnotationNoteIcon?
  /// `/Open`: whether the note shows open.
  public var open: Bool?
  /// `/State`. A standard state (`accepted`, `marked`, …) brings its own
  /// `stateModel`; any other needs one. `.null` is none.
  public var state: Nullable<String>?
  /// `/StateModel`: `review`, `marked`, or a custom model. `.null` is none.
  public var stateModel: Nullable<String>?
  /// `/Contents`: the note's text. `.null` is none.
  public var contents: Nullable<String>?
  /// How the appearance blends with the page under it (`/BM` on `/AP /N`).
  public var blendMode: AnnotationBlendMode?

  public init(
    rect: Rect, color: AnnotationColor? = nil, opacity: Double? = nil, icon: AnnotationNoteIcon? = nil,
    open: Bool? = nil, state: Nullable<String>? = nil,
    stateModel: Nullable<String>? = nil, contents: Nullable<String>? = nil,
    blendMode: AnnotationBlendMode? = nil
  ) {
    self.rect = rect
    self.color = color
    self.opacity = opacity
    self.icon = icon
    self.open = open
    self.state = state
    self.stateModel = stateModel
    self.contents = contents
    self.blendMode = blendMode
  }

  private enum CodingKeys: String, CodingKey {
    case subtype, rect, color, opacity, icon, open, state, stateModel, contents, blendMode
  }

  public func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encode(AnnotationSubtype.text.rawValue, forKey: .subtype)
    try container.encode(WireRect(rect), forKey: .rect)
    try container.encodeIfPresent(color, forKey: .color)
    try container.encodeIfPresent(opacity, forKey: .opacity)
    try container.encodeIfPresent(icon, forKey: .icon)
    try container.encodeIfPresent(open, forKey: .open)
    try container.encodeIfPresent(state, forKey: .state)
    try container.encodeIfPresent(stateModel, forKey: .stateModel)
    try container.encodeIfPresent(contents, forKey: .contents)
    try container.encodeIfPresent(blendMode, forKey: .blendMode)
  }
}

/// A change to a note, or to the note tool's defaults. A member left `nil`
/// keeps its value.
public struct NoteAnnotationPatch: AnnotationPatch, Hashable {
  /// Moves or resizes the icon.
  public var rect: Rect?
  public var color: AnnotationColor?
  public var opacity: Double?
  public var icon: AnnotationNoteIcon?
  public var open: Bool?
  /// A standard state sets its own `stateModel`; any other keeps the
  /// note's, and the note needs one. `.null` removes it.
  public var state: Nullable<String>?
  /// `.null` removes it.
  public var stateModel: Nullable<String>?
  /// `.null` removes `/Contents`.
  public var contents: Nullable<String>?
  /// How the appearance blends with the page under it (`/BM` on `/AP /N`).
  public var blendMode: AnnotationBlendMode?

  public init(
    rect: Rect? = nil, color: AnnotationColor? = nil, opacity: Double? = nil,
    icon: AnnotationNoteIcon? = nil, open: Bool? = nil, state: Nullable<String>? = nil,
    stateModel: Nullable<String>? = nil, contents: Nullable<String>? = nil,
    blendMode: AnnotationBlendMode? = nil
  ) {
    self.rect = rect
    self.color = color
    self.opacity = opacity
    self.icon = icon
    self.open = open
    self.state = state
    self.stateModel = stateModel
    self.contents = contents
    self.blendMode = blendMode
  }

  private enum CodingKeys: String, CodingKey {
    case rect, color, opacity, icon, open, state, stateModel, contents, blendMode
  }

  public func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(rect.map(WireRect.init), forKey: .rect)
    try container.encodeIfPresent(color, forKey: .color)
    try container.encodeIfPresent(opacity, forKey: .opacity)
    try container.encodeIfPresent(icon, forKey: .icon)
    try container.encodeIfPresent(open, forKey: .open)
    try container.encodeIfPresent(state, forKey: .state)
    try container.encodeIfPresent(stateModel, forKey: .stateModel)
    try container.encodeIfPresent(contents, forKey: .contents)
    try container.encodeIfPresent(blendMode, forKey: .blendMode)
  }
}

// ── encoding ────────────────────────────────────────────────────────────────

enum AnnotationFieldBag {
  /// Sorted keys make the bytes stable, and unescaped slashes keep a
  /// string as the caller wrote it.
  static func json(of value: some Encodable) throws(PDFError) -> String {
    let encoder = JSONEncoder()
    encoder.outputFormatting = [.sortedKeys, .withoutEscapingSlashes]
    do {
      return String(decoding: try encoder.encode(value), as: UTF8.self)
    } catch {
      throw PDFError(
        name: "EncodingError", code: .invalidInput,
        detail: "the annotation fields don't encode as JSON: \(error)")
    }
  }
}

/// Page-space geometry as the plugin reads it: `{x, y}` points,
/// `upperLeft`…`lowerRight` quads (`NamedPdfQuad` in
/// `crates/epdf-engine/src/geom.rs`) and `{x, y, width, height}` boxes.
struct WirePoint: Encodable {
  let x: Double
  let y: Double

  init(_ point: Point) {
    (x, y) = (point.x, point.y)
  }
}

struct WireQuad: Encodable {
  let upperLeft: WirePoint
  let upperRight: WirePoint
  let lowerLeft: WirePoint
  let lowerRight: WirePoint

  init(_ quad: Quad) {
    upperLeft = WirePoint(quad.upperLeft)
    upperRight = WirePoint(quad.upperRight)
    lowerLeft = WirePoint(quad.lowerLeft)
    lowerRight = WirePoint(quad.lowerRight)
  }
}

struct WireRect: Encodable {
  let x: Double
  let y: Double
  let width: Double
  let height: Double

  init(_ rect: Rect) {
    (x, y, width, height) = (rect.x, rect.y, rect.width, rect.height)
  }
}

/// A line's two points, `{start, end}`.
struct WireLinePoints: Encodable {
  let start: WirePoint
  let end: WirePoint

  init(start: Point, end: Point) {
    self.start = WirePoint(start)
    self.end = WirePoint(end)
  }
}

/// A page as the engine's JSON names it: `{"kind": "objectNumber",
/// "objectNumber": …}` (`page_ref` in `crates/epdf-engine/src/identity.rs`).
struct WirePage: Codable {
  let kind: String
  let objectNumber: UInt32

  init(_ page: PageRef) {
    kind = "objectNumber"
    objectNumber = page.pageObjectNumber
  }

  var page: PageRef { PageRef(pageObjectNumber: objectNumber) }
}

/// A destination as the engine's JSON spells it (`PdfDestination` in
/// `crates/epdf-engine/src/features/actions/destination.rs`): tagged by
/// `kind`, its optional coordinates written as `null` when absent, which is
/// the PDF's "keep the current value".
struct WireDestination: Codable {
  let destination: PdfDestination

  init(_ destination: PdfDestination) {
    self.destination = destination
  }

  private enum CodingKeys: String, CodingKey {
    case kind, page, left, top, zoom, bottom, right
  }

  func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    func coordinate(_ value: Double?, _ key: CodingKeys) throws {
      if let value { try container.encode(value, forKey: key) } else { try container.encodeNil(forKey: key) }
    }
    switch destination {
    case let .xyz(page, left, top, zoom):
      try container.encode("xyz", forKey: .kind)
      try container.encode(WirePage(page), forKey: .page)
      try coordinate(left, .left)
      try coordinate(top, .top)
      try coordinate(zoom, .zoom)
    case let .fit(page):
      try container.encode("fit", forKey: .kind)
      try container.encode(WirePage(page), forKey: .page)
    case let .fitH(page, top):
      try container.encode("fitH", forKey: .kind)
      try container.encode(WirePage(page), forKey: .page)
      try coordinate(top, .top)
    case let .fitV(page, left):
      try container.encode("fitV", forKey: .kind)
      try container.encode(WirePage(page), forKey: .page)
      try coordinate(left, .left)
    case let .fitR(page, left, bottom, right, top):
      try container.encode("fitR", forKey: .kind)
      try container.encode(WirePage(page), forKey: .page)
      try container.encode(left, forKey: .left)
      try container.encode(bottom, forKey: .bottom)
      try container.encode(right, forKey: .right)
      try container.encode(top, forKey: .top)
    case let .fitB(page):
      try container.encode("fitB", forKey: .kind)
      try container.encode(WirePage(page), forKey: .page)
    case let .fitBh(page, top):
      try container.encode("fitBH", forKey: .kind)
      try container.encode(WirePage(page), forKey: .page)
      try coordinate(top, .top)
    case let .fitBv(page, left):
      try container.encode("fitBV", forKey: .kind)
      try container.encode(WirePage(page), forKey: .page)
      try coordinate(left, .left)
    }
  }

  init(from decoder: any Decoder) throws {
    let container = try decoder.container(keyedBy: CodingKeys.self)
    let page = try container.decode(WirePage.self, forKey: .page).page
    func coordinate(_ key: CodingKeys) throws -> Double? {
      try container.decodeIfPresent(Double.self, forKey: key)
    }
    switch try container.decode(String.self, forKey: .kind) {
    case "xyz":
      destination = .xyz(page: page, left: try coordinate(.left), top: try coordinate(.top), zoom: try coordinate(.zoom))
    case "fit": destination = .fit(page: page)
    case "fitH": destination = .fitH(page: page, top: try coordinate(.top))
    case "fitV": destination = .fitV(page: page, left: try coordinate(.left))
    case "fitR":
      destination = .fitR(
        page: page, left: try container.decode(Double.self, forKey: .left),
        bottom: try container.decode(Double.self, forKey: .bottom),
        right: try container.decode(Double.self, forKey: .right),
        top: try container.decode(Double.self, forKey: .top))
    case "fitB": destination = .fitB(page: page)
    case "fitBH": destination = .fitBh(page: page, top: try coordinate(.top))
    case "fitBV": destination = .fitBv(page: page, left: try coordinate(.left))
    case let kind:
      throw DecodingError.dataCorruptedError(
        forKey: .kind, in: container, debugDescription: "not a destination kind: \(kind)")
    }
  }
}
