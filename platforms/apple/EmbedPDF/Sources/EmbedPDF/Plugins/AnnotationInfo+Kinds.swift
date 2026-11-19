public import EmbedPDFBindings
public import Foundation

// The typed members of every subtype `AnnotationInfo.Details` reads besides
// ink, highlight and note (`AnnotationInfo.swift`). Each struct is the
// engine's read record for its kind (`crates/epdf-engine/src/features/
// annotation/read/kinds.rs`).
//
// A member whose read vocabulary is the write vocabulary takes the drafts'
// enum (`AnnotationLineEndings`, `AnnotationTextAlignment`, …).
// `borderStyle` and `intent` take the open read types instead
// (`AnnotationInfo.BorderStyle`, `AnnotationIntent`): a read can name a
// border style a write refuses (`beveled`, `inset`).

extension AnnotationInfo {
  /// An underline's or squiggly underline's members.
  public struct TextMarkup: Sendable, Hashable {
    /// `/C`.
    public let color: AnnotationColor
    /// `/CA`, 0 to 1.
    public let opacity: Double
    /// `/QuadPoints`, page space: one quad per run of marked text.
    public let quadPoints: [Quad]
    /// The comment as styled text, or `nil` when it is plain. Its plain
    /// text is `AnnotationInfo.contents`.
    public let richText: AnnotationRichText?
  }

  /// A strikeout's members.
  public struct Strikeout: Sendable, Hashable {
    /// `/C`.
    public let color: AnnotationColor
    /// `/CA`, 0 to 1.
    public let opacity: Double
    /// `/QuadPoints`, page space: one quad per run of marked text.
    public let quadPoints: [Quad]
    /// `/IT`: `strikeout-text-edit` for text marked for deletion, `nil` for
    /// a plain strikeout.
    public let intent: AnnotationIntent?
    /// The comment as styled text, or `nil` when it is plain. Its plain
    /// text is `AnnotationInfo.contents`.
    public let richText: AnnotationRichText?
  }

  /// A square's or circle's members.
  public struct Shape: Sendable, Hashable {
    /// `/C`, the stroke, or `nil` for none.
    public let color: AnnotationColor?
    /// `/CA`, 0 to 1.
    public let opacity: Double
    public let strokeWidth: Double
    /// `solid`, `dashed`, `beveled` or `inset`.
    public let borderStyle: BorderStyle
    /// The dash pattern, or `nil` when the border isn't dashed or names none.
    public let dashArray: [Double]?
    /// `/IC`, the fill, or `nil` for none.
    public let interiorColor: AnnotationColor?
    /// The shape's box, page space, before rotation.
    public let box: Rect
    public let rotation: Double?
    /// `/BE`: the cloudy border's intensity, or `nil` for a plain border.
    public let cloudyIntensity: Double?
    /// The comment as styled text, or `nil` when it is plain. Its plain
    /// text is `AnnotationInfo.contents`.
    public let richText: AnnotationRichText?
  }

  /// A line's members.
  public struct Line: Sendable, Hashable {
    /// `/C`, the stroke, or `nil` for none.
    public let color: AnnotationColor?
    /// `/CA`, 0 to 1.
    public let opacity: Double
    public let strokeWidth: Double
    /// `solid`, `dashed`, `beveled` or `inset`.
    public let borderStyle: BorderStyle
    public let dashArray: [Double]?
    /// `/IC`: what fills a closed line ending.
    public let interiorColor: AnnotationColor?
    /// `/L`, page space.
    public let start: Point
    public let end: Point
    public let lineEndings: AnnotationLineEndings
    public let rotation: Double?
    /// `/IT`: `line-arrow`, `line-dimension`, or `nil` for a plain line.
    public let intent: AnnotationIntent?
    /// `/Cap`, `false` when the file has none.
    public let captionEnabled: Bool
    public let captionPosition: AnnotationCaptionPosition
    public let captionOffset: AnnotationCaptionOffset?
    /// `/LL`, `/LLE` and `/LLO`; the engine reads a missing `/LLE` or `/LLO`
    /// as 0.
    public let leader: AnnotationLineLeader?
    /// The comment as styled text, or `nil` when it is plain. Its plain
    /// text is `AnnotationInfo.contents`.
    public let richText: AnnotationRichText?
    /// `/Measure`: the scale a dimension measures in, or `nil` for none. A
    /// scale the engine can't model has only its `subtype` (`geospatial`,
    /// `unknown`).
    public let measure: PdfMeasurement?
  }

  /// A polygon's members.
  public struct Polygon: Sendable, Hashable {
    /// `/C`, the stroke, or `nil` for none.
    public let color: AnnotationColor?
    /// `/CA`, 0 to 1.
    public let opacity: Double
    public let strokeWidth: Double
    /// `solid`, `dashed`, `beveled` or `inset`.
    public let borderStyle: BorderStyle
    public let dashArray: [Double]?
    /// `/IC`, the fill, or `nil` for none.
    public let interiorColor: AnnotationColor?
    /// `/Vertices`, page space.
    public let vertices: [Point]
    public let rotation: Double?
    /// `/IT`: `polygon-cloud`, `polygon-dimension`, or `nil` for a plain
    /// polygon.
    public let intent: AnnotationIntent?
    /// `/BE`: the cloudy border's intensity, or `nil` for a plain border.
    public let cloudyIntensity: Double?
    public let captionEnabled: Bool?
    /// The caption's centre, page space, or `nil` when the engine places it.
    public let captionCenter: Point?
    /// The comment as styled text, or `nil` when it is plain. Its plain
    /// text is `AnnotationInfo.contents`.
    public let richText: AnnotationRichText?
    /// `/Measure`: the scale a dimension measures in, or `nil` for none. A
    /// scale the engine can't model has only its `subtype` (`geospatial`,
    /// `unknown`).
    public let measure: PdfMeasurement?
  }

  /// A polyline's members.
  public struct Polyline: Sendable, Hashable {
    /// `/C`, the stroke, or `nil` for none.
    public let color: AnnotationColor?
    /// `/CA`, 0 to 1.
    public let opacity: Double
    public let strokeWidth: Double
    /// `solid`, `dashed`, `beveled` or `inset`.
    public let borderStyle: BorderStyle
    public let dashArray: [Double]?
    /// `/IC`: what fills a closed line ending.
    public let interiorColor: AnnotationColor?
    /// `/Vertices`, page space.
    public let vertices: [Point]
    public let lineEndings: AnnotationLineEndings
    public let rotation: Double?
    /// `/IT`: `polyline-dimension`, or `nil` for a plain polyline.
    public let intent: AnnotationIntent?
    public let captionEnabled: Bool?
    /// The caption's centre, page space, or `nil` when the engine places it.
    public let captionCenter: Point?
    /// The comment as styled text, or `nil` when it is plain. Its plain
    /// text is `AnnotationInfo.contents`.
    public let richText: AnnotationRichText?
    /// `/Measure`: the scale a dimension measures in, or `nil` for none. A
    /// scale the engine can't model has only its `subtype` (`geospatial`,
    /// `unknown`).
    public let measure: PdfMeasurement?
  }

  /// A free text's members. Its text is `AnnotationInfo.contents`, plain;
  /// the styled runs are `richText`.
  public struct FreeText: Sendable, Hashable {
    /// The text box, page space, before rotation.
    public let box: Rect
    /// `free-text`, `free-text-callout` or `free-text-typewriter`.
    public let intent: AnnotationIntent
    public let fontFamily: String
    public let fontSize: Double
    public let fontColor: AnnotationColor
    public let textAlign: AnnotationTextAlignment
    public let verticalAlign: AnnotationVerticalAlignment
    /// The border.
    public let color: AnnotationColor
    /// The fill, or `nil` for none.
    public let interiorColor: AnnotationColor?
    /// `/CA`, 0 to 1.
    public let opacity: Double
    public let strokeWidth: Double
    /// `solid`, `dashed`, `beveled` or `inset`.
    public let borderStyle: BorderStyle
    public let dashArray: [Double]?
    public let cloudyIntensity: Double?
    public let rotation: Double?
    public let calloutLine: AnnotationCalloutLine?
    public let lineEnding: AnnotationLineEnding?
    /// The text as styled runs; its plain text is `AnnotationInfo.contents`.
    /// The engine reports one for every free text it reads, one unstyled run
    /// per line when the text was written plain.
    public let richText: AnnotationRichText?
  }

  /// A caret's members.
  public struct Caret: Sendable, Hashable {
    /// `/C`.
    public let color: AnnotationColor
    /// `/CA`, 0 to 1.
    public let opacity: Double
    /// The caret's box, page space, before rotation.
    public let box: Rect
    public let rotation: Double?
    /// `/IT`: `replace`, or `nil` for an insertion.
    public let intent: AnnotationIntent?
    /// The comment as styled text, or `nil` when it is plain. Its plain
    /// text is `AnnotationInfo.contents`.
    public let richText: AnnotationRichText?
  }

  /// A stamp's members. Its image is the appearance, which the record
  /// doesn't carry.
  public struct Stamp: Sendable, Hashable {
    /// `/Name`, verbatim: a standard label (`Approved`) or a custom one.
    public let name: String?
    public let fit: AnnotationStampFit?
    /// `/CA`, 0 to 1.
    public let opacity: Double
    /// The stamp's box, page space, before rotation.
    public let box: Rect
    public let rotation: Double?
    /// The comment as styled text, or `nil` when it is plain. Its plain
    /// text is `AnnotationInfo.contents`.
    public let richText: AnnotationRichText?
  }

  /// A link's members.
  public struct Link: Sendable, Hashable {
    /// Where it goes, or `nil` when it goes nowhere or somewhere a link
    /// draft can't write (another file, a launch, a script): read those from
    /// `fieldsJSON`'s `target` and `actions`.
    public let target: AnnotationLinkTarget?
  }

  /// A file attachment's members.
  public struct FileAttachment: Sendable, Hashable {
    public let icon: AnnotationFileAttachmentIcon
    /// `/C`.
    public let color: AnnotationColor
    /// `/CA`, 0 to 1.
    public let opacity: Double
    /// The attached file's description, or `nil` when there is no readable
    /// `/FS`.
    public let file: AttachedFile?
    /// The comment as styled text, or `nil` when it is plain. Its plain
    /// text is `AnnotationInfo.contents`.
    public let richText: AnnotationRichText?
  }

  /// The file a file attachment carries, without its bytes.
  public struct AttachedFile: Sendable, Hashable {
    /// `/UF`, empty when the file names none.
    public let name: String
    public let mimeType: String?
    public let description: String?
    /// The decoded length in bytes.
    public let sizeBytes: Int?
    /// The MD5, lowercase hex.
    public let md5: String?
    /// `/CreationDate` as the engine reports it, ISO 8601.
    public let rawCreatedAt: String?
    /// `/ModDate` as the engine reports it, ISO 8601.
    public let rawModifiedAt: String?

    /// `rawCreatedAt` as an instant, on `AnnotationInfo.createdAt`'s terms.
    public var createdAt: Date? { rawCreatedAt.flatMap(AnnotationInfo.instant(of:)) }
    /// `rawModifiedAt` as an instant.
    public var modifiedAt: Date? { rawModifiedAt.flatMap(AnnotationInfo.instant(of:)) }
  }

  /// A redaction's members: what it marks, and what it draws once applied.
  public struct Redaction: Sendable, Hashable {
    /// `/QuadPoints`, page space: the text runs it marks, empty for an area.
    public let quadPoints: [Quad]
    /// `/C`: the outline drawn before it is applied.
    public let color: AnnotationColor
    /// `/CA`, 0 to 1.
    public let opacity: Double
    /// `/IC`: the fill once applied, or `nil` for none.
    public let interiorColor: AnnotationColor?
    public let overlayText: String?
    public let `repeat`: Bool
    public let fontFamily: String
    /// Points; 0 fits the overlay text to the area.
    public let fontSize: Double
    public let fontColor: AnnotationColor
    public let textAlign: AnnotationTextAlignment
    /// The comment as styled text, or `nil` when it is plain. Its plain
    /// text is `AnnotationInfo.contents`.
    public let richText: AnnotationRichText?
  }
}

// ── decoding ────────────────────────────────────────────────────────────────

private enum MemberKey: String, CodingKey {
  case color, opacity, quadPoints, intent, strokeWidth, borderStyle, dashArray, interiorColor,
    box, rotation, cloudyIntensity, linePoints, lineEndings, captionEnabled, captionPosition,
    captionOffset, leader, vertices, captionCenter, fontFamily, fontSize, fontColor, textAlign,
    verticalAlign, calloutLine, lineEnding, name, fit, target, icon, file, overlayText, `repeat`,
    richText, measure
}

extension KeyedDecodingContainer where Key == MemberKey {
  fileprivate func quads() throws -> [Quad] {
    try decode([ReadQuad].self, forKey: .quadPoints).map(\.quad)
  }

  fileprivate func points(_ key: MemberKey) throws -> [Point] {
    try decode([ReadPoint].self, forKey: key).map(\.point)
  }

  fileprivate func box() throws -> Rect {
    try decode(ReadRect.self, forKey: .box).rect
  }
}

extension AnnotationInfo.TextMarkup: Decodable {
  public init(from decoder: any Decoder) throws {
    let members = try decoder.container(keyedBy: MemberKey.self)
    color = try members.decode(AnnotationColor.self, forKey: .color)
    opacity = try members.decode(Double.self, forKey: .opacity)
    quadPoints = try members.quads()
    richText = try members.decodeIfPresent(AnnotationRichText.self, forKey: .richText)
  }
}

extension AnnotationInfo.Strikeout: Decodable {
  public init(from decoder: any Decoder) throws {
    let members = try decoder.container(keyedBy: MemberKey.self)
    color = try members.decode(AnnotationColor.self, forKey: .color)
    opacity = try members.decode(Double.self, forKey: .opacity)
    quadPoints = try members.quads()
    intent = try members.decodeIfPresent(AnnotationIntent.self, forKey: .intent)
    richText = try members.decodeIfPresent(AnnotationRichText.self, forKey: .richText)
  }
}

extension AnnotationInfo.Shape: Decodable {
  public init(from decoder: any Decoder) throws {
    let members = try decoder.container(keyedBy: MemberKey.self)
    color = try members.decodeIfPresent(AnnotationColor.self, forKey: .color)
    opacity = try members.decode(Double.self, forKey: .opacity)
    strokeWidth = try members.decode(Double.self, forKey: .strokeWidth)
    borderStyle = try members.decode(AnnotationInfo.BorderStyle.self, forKey: .borderStyle)
    dashArray = try members.decodeIfPresent([Double].self, forKey: .dashArray)
    interiorColor = try members.decodeIfPresent(AnnotationColor.self, forKey: .interiorColor)
    box = try members.box()
    rotation = try members.decodeIfPresent(Double.self, forKey: .rotation)
    cloudyIntensity = try members.decodeIfPresent(Double.self, forKey: .cloudyIntensity)
    richText = try members.decodeIfPresent(AnnotationRichText.self, forKey: .richText)
  }
}

struct ReadLinePoints: Decodable {
  let start: ReadPoint
  let end: ReadPoint
}

extension AnnotationInfo.Line: Decodable {
  public init(from decoder: any Decoder) throws {
    let members = try decoder.container(keyedBy: MemberKey.self)
    color = try members.decodeIfPresent(AnnotationColor.self, forKey: .color)
    opacity = try members.decode(Double.self, forKey: .opacity)
    strokeWidth = try members.decode(Double.self, forKey: .strokeWidth)
    borderStyle = try members.decode(AnnotationInfo.BorderStyle.self, forKey: .borderStyle)
    dashArray = try members.decodeIfPresent([Double].self, forKey: .dashArray)
    interiorColor = try members.decodeIfPresent(AnnotationColor.self, forKey: .interiorColor)
    let linePoints = try members.decode(ReadLinePoints.self, forKey: .linePoints)
    start = linePoints.start.point
    end = linePoints.end.point
    lineEndings = try members.decode(AnnotationLineEndings.self, forKey: .lineEndings)
    rotation = try members.decodeIfPresent(Double.self, forKey: .rotation)
    intent = try members.decodeIfPresent(AnnotationIntent.self, forKey: .intent)
    captionEnabled = try members.decode(Bool.self, forKey: .captionEnabled)
    captionPosition = try members.decode(AnnotationCaptionPosition.self, forKey: .captionPosition)
    captionOffset = try members.decodeIfPresent(AnnotationCaptionOffset.self, forKey: .captionOffset)
    leader = try members.decodeIfPresent(AnnotationLineLeader.self, forKey: .leader)
    richText = try members.decodeIfPresent(AnnotationRichText.self, forKey: .richText)
    measure = try members.decodeMeasureIfPresent(forKey: .measure)
  }
}

extension AnnotationInfo.Polygon: Decodable {
  public init(from decoder: any Decoder) throws {
    let members = try decoder.container(keyedBy: MemberKey.self)
    color = try members.decodeIfPresent(AnnotationColor.self, forKey: .color)
    opacity = try members.decode(Double.self, forKey: .opacity)
    strokeWidth = try members.decode(Double.self, forKey: .strokeWidth)
    borderStyle = try members.decode(AnnotationInfo.BorderStyle.self, forKey: .borderStyle)
    dashArray = try members.decodeIfPresent([Double].self, forKey: .dashArray)
    interiorColor = try members.decodeIfPresent(AnnotationColor.self, forKey: .interiorColor)
    vertices = try members.points(.vertices)
    rotation = try members.decodeIfPresent(Double.self, forKey: .rotation)
    intent = try members.decodeIfPresent(AnnotationIntent.self, forKey: .intent)
    cloudyIntensity = try members.decodeIfPresent(Double.self, forKey: .cloudyIntensity)
    captionEnabled = try members.decodeIfPresent(Bool.self, forKey: .captionEnabled)
    captionCenter = try members.decodeIfPresent(ReadPoint.self, forKey: .captionCenter)?.point
    richText = try members.decodeIfPresent(AnnotationRichText.self, forKey: .richText)
    measure = try members.decodeMeasureIfPresent(forKey: .measure)
  }
}

extension AnnotationInfo.Polyline: Decodable {
  public init(from decoder: any Decoder) throws {
    let members = try decoder.container(keyedBy: MemberKey.self)
    color = try members.decodeIfPresent(AnnotationColor.self, forKey: .color)
    opacity = try members.decode(Double.self, forKey: .opacity)
    strokeWidth = try members.decode(Double.self, forKey: .strokeWidth)
    borderStyle = try members.decode(AnnotationInfo.BorderStyle.self, forKey: .borderStyle)
    dashArray = try members.decodeIfPresent([Double].self, forKey: .dashArray)
    interiorColor = try members.decodeIfPresent(AnnotationColor.self, forKey: .interiorColor)
    vertices = try members.points(.vertices)
    lineEndings = try members.decode(AnnotationLineEndings.self, forKey: .lineEndings)
    rotation = try members.decodeIfPresent(Double.self, forKey: .rotation)
    intent = try members.decodeIfPresent(AnnotationIntent.self, forKey: .intent)
    captionEnabled = try members.decodeIfPresent(Bool.self, forKey: .captionEnabled)
    captionCenter = try members.decodeIfPresent(ReadPoint.self, forKey: .captionCenter)?.point
    richText = try members.decodeIfPresent(AnnotationRichText.self, forKey: .richText)
    measure = try members.decodeMeasureIfPresent(forKey: .measure)
  }
}

extension AnnotationInfo.FreeText: Decodable {
  public init(from decoder: any Decoder) throws {
    let members = try decoder.container(keyedBy: MemberKey.self)
    box = try members.box()
    intent = try members.decode(AnnotationIntent.self, forKey: .intent)
    fontFamily = try members.decode(String.self, forKey: .fontFamily)
    fontSize = try members.decode(Double.self, forKey: .fontSize)
    fontColor = try members.decode(AnnotationColor.self, forKey: .fontColor)
    textAlign = try members.decode(AnnotationTextAlignment.self, forKey: .textAlign)
    verticalAlign = try members.decode(AnnotationVerticalAlignment.self, forKey: .verticalAlign)
    color = try members.decode(AnnotationColor.self, forKey: .color)
    interiorColor = try members.decodeIfPresent(AnnotationColor.self, forKey: .interiorColor)
    opacity = try members.decode(Double.self, forKey: .opacity)
    strokeWidth = try members.decode(Double.self, forKey: .strokeWidth)
    borderStyle = try members.decode(AnnotationInfo.BorderStyle.self, forKey: .borderStyle)
    dashArray = try members.decodeIfPresent([Double].self, forKey: .dashArray)
    cloudyIntensity = try members.decodeIfPresent(Double.self, forKey: .cloudyIntensity)
    rotation = try members.decodeIfPresent(Double.self, forKey: .rotation)
    calloutLine = try members.decodeIfPresent([ReadPoint].self, forKey: .calloutLine).map {
      points throws in
      switch points.count {
      case 2: .straight(from: points[0].point, to: points[1].point)
      case 3: .kneed(from: points[0].point, knee: points[1].point, to: points[2].point)
      default:
        throw DecodingError.dataCorruptedError(
          forKey: .calloutLine, in: members, debugDescription: "a callout line has two or three points")
      }
    }
    lineEnding = try members.decodeIfPresent(AnnotationLineEnding.self, forKey: .lineEnding)
    richText = try members.decodeIfPresent(AnnotationRichText.self, forKey: .richText)
  }
}

extension AnnotationInfo.Caret: Decodable {
  public init(from decoder: any Decoder) throws {
    let members = try decoder.container(keyedBy: MemberKey.self)
    color = try members.decode(AnnotationColor.self, forKey: .color)
    opacity = try members.decode(Double.self, forKey: .opacity)
    box = try members.box()
    rotation = try members.decodeIfPresent(Double.self, forKey: .rotation)
    intent = try members.decodeIfPresent(AnnotationIntent.self, forKey: .intent)
    richText = try members.decodeIfPresent(AnnotationRichText.self, forKey: .richText)
  }
}

extension AnnotationInfo.Stamp: Decodable {
  public init(from decoder: any Decoder) throws {
    let members = try decoder.container(keyedBy: MemberKey.self)
    name = try members.decodeIfPresent(String.self, forKey: .name)
    fit = try members.decodeIfPresent(AnnotationStampFit.self, forKey: .fit)
    opacity = try members.decode(Double.self, forKey: .opacity)
    box = try members.box()
    rotation = try members.decodeIfPresent(Double.self, forKey: .rotation)
    richText = try members.decodeIfPresent(AnnotationRichText.self, forKey: .richText)
  }
}

extension AnnotationInfo.Link: Decodable {
  public init(from decoder: any Decoder) throws {
    let members = try decoder.container(keyedBy: MemberKey.self)
    // A target a draft can't write doesn't decode, and reads as `nil`.
    target = (try? members.decodeIfPresent(AnnotationLinkTarget.self, forKey: .target)) ?? nil
  }
}

extension AnnotationInfo.AttachedFile: Decodable {
  private enum Key: String, CodingKey {
    case name, mimeType, description, sizeBytes, md5, createdAt, modifiedAt
  }

  public init(from decoder: any Decoder) throws {
    let container = try decoder.container(keyedBy: Key.self)
    name = try container.decode(String.self, forKey: .name)
    mimeType = try container.decodeIfPresent(String.self, forKey: .mimeType)
    description = try container.decodeIfPresent(String.self, forKey: .description)
    sizeBytes = try container.decodeIfPresent(Int.self, forKey: .sizeBytes)
    md5 = try container.decodeIfPresent(String.self, forKey: .md5)
    rawCreatedAt = try container.decodeIfPresent(String.self, forKey: .createdAt)
    rawModifiedAt = try container.decodeIfPresent(String.self, forKey: .modifiedAt)
  }
}

extension AnnotationInfo.FileAttachment: Decodable {
  public init(from decoder: any Decoder) throws {
    let members = try decoder.container(keyedBy: MemberKey.self)
    icon = try members.decode(AnnotationFileAttachmentIcon.self, forKey: .icon)
    color = try members.decode(AnnotationColor.self, forKey: .color)
    opacity = try members.decode(Double.self, forKey: .opacity)
    file = try members.decodeIfPresent(AnnotationInfo.AttachedFile.self, forKey: .file)
    richText = try members.decodeIfPresent(AnnotationRichText.self, forKey: .richText)
  }
}

extension AnnotationInfo.Redaction: Decodable {
  public init(from decoder: any Decoder) throws {
    let members = try decoder.container(keyedBy: MemberKey.self)
    quadPoints = try members.quads()
    color = try members.decode(AnnotationColor.self, forKey: .color)
    opacity = try members.decode(Double.self, forKey: .opacity)
    interiorColor = try members.decodeIfPresent(AnnotationColor.self, forKey: .interiorColor)
    overlayText = try members.decodeIfPresent(String.self, forKey: .overlayText)
    `repeat` = try members.decode(Bool.self, forKey: .repeat)
    fontFamily = try members.decode(String.self, forKey: .fontFamily)
    fontSize = try members.decode(Double.self, forKey: .fontSize)
    fontColor = try members.decode(AnnotationColor.self, forKey: .fontColor)
    textAlign = try members.decode(AnnotationTextAlignment.self, forKey: .textAlign)
    richText = try members.decodeIfPresent(AnnotationRichText.self, forKey: .richText)
  }
}
