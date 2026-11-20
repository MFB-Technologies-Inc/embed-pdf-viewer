/// An annotation's `/IT` as a read reports it, in the engine's spelling.
/// `rawValue` is the wire string.
///
/// The set is open, so this is a struct rather than an enum: the constants
/// are the intents the engine names (`crates/epdf-engine/src/features/
/// annotation/intent.rs`), and a newer engine can name more without
/// breaking a caller's `switch`. Any other intent is a string literal or
/// `AnnotationIntent("…")`. Each kind reads only its own intents; a draft or
/// patch writes one through its kind's enum (`AnnotationLineIntent`, …).
public struct AnnotationIntent: RawRepresentable, Hashable, Sendable, Codable,
  ExpressibleByStringLiteral, CustomStringConvertible
{
  public let rawValue: String

  public init(rawValue: String) { self.rawValue = rawValue }

  public init(_ rawValue: String) { self.rawValue = rawValue }

  public init(stringLiteral rawValue: String) { self.rawValue = rawValue }

  public var description: String { rawValue }

  /// An ink's: a freehand highlighter.
  public static let inkHighlight = AnnotationIntent("ink-highlight")
  /// A strikeout's: text marked for deletion.
  public static let strikeoutTextEdit = AnnotationIntent("strikeout-text-edit")
  /// A line's.
  public static let lineArrow = AnnotationIntent("line-arrow")
  /// A line's: a distance measurement.
  public static let lineDimension = AnnotationIntent("line-dimension")
  /// A polygon's.
  public static let polygonCloud = AnnotationIntent("polygon-cloud")
  /// A polygon's: an area measurement.
  public static let polygonDimension = AnnotationIntent("polygon-dimension")
  /// A polyline's: a perimeter measurement.
  public static let polylineDimension = AnnotationIntent("polyline-dimension")
  /// A free text's: a text box.
  public static let freeText = AnnotationIntent("free-text")
  /// A free text's: a text box with a leader line to what it calls out.
  public static let freeTextCallout = AnnotationIntent("free-text-callout")
  /// A free text's: typewriter text. A read reports it; no draft writes it.
  public static let freeTextTypewriter = AnnotationIntent("free-text-typewriter")
  /// A caret's: the caret of a replacement, paired with a strikeout.
  public static let replace = AnnotationIntent("replace")
}

extension AnnotationInfo {
  /// A border's style as a read reports it, in the engine's spelling.
  /// `rawValue` is the wire string.
  ///
  /// Open like `AnnotationIntent`. A read names more styles than a write
  /// takes: `AnnotationBorderStyle`, the drafts' enum, is `solid` and
  /// `dashed` only.
  public struct BorderStyle: RawRepresentable, Hashable, Sendable, Codable,
    ExpressibleByStringLiteral, CustomStringConvertible
  {
    public let rawValue: String

    public init(rawValue: String) { self.rawValue = rawValue }

    public init(_ rawValue: String) { self.rawValue = rawValue }

    public init(stringLiteral rawValue: String) { self.rawValue = rawValue }

    public var description: String { rawValue }

    public static let solid = BorderStyle("solid")
    public static let dashed = BorderStyle("dashed")
    public static let beveled = BorderStyle("beveled")
    public static let inset = BorderStyle("inset")
  }
}
