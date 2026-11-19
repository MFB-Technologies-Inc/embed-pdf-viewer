import Foundation

/// Styled text: a free text's text, or another annotation's comment (`/RC`).
/// It is the engine's rich-text document
/// (`crates/epdf-engine/src/features/annotation/rich_text.rs`), the same
/// type on reads, drafts and patches, so what a read hands out can be
/// written back as it is.
///
/// The engine fixes only the outline: paragraphs of runs of text. The body
/// style, each run's style and each paragraph's properties are open maps it
/// passes through, held here as `JSONValue` with every key kept.
///
/// `contents` is always the rich text's plain text, `plainText`:
///
/// - A draft or patch that gives `richText` gets `contents` from it. Giving
///   both is allowed only when they agree, line breaks however spelled;
///   otherwise the call throws `invalid-input`.
/// - A free text always has rich text. One created or patched with only
///   `contents` reads back one unstyled run per line, in the body style.
/// - Any other annotation has rich text only when its comment is styled, and
///   reads `nil` for a plain comment. On a patch, `.null` makes a styled
///   comment plain and keeps `contents`; new `contents` on a styled comment
///   becomes one unstyled run per line over the same body; `contents: .null`
///   removes both.
public struct AnnotationRichText: Codable, Sendable, Hashable {
  /// The body style every run starts from: `family`, `weight`, `italic`,
  /// `size`, `color`, `decoration`, `script`, `letterSpacing`,
  /// `horizontalScale`, `align`, `dir`, and whatever else the engine
  /// reports. A read always has one.
  ///
  /// Left `nil` on a write, the annotation keeps its body. One that is given
  /// replaces the body whole: a key it leaves out takes the engine's
  /// default, not the current value. On a free text, the draft's or patch's
  /// own `fontFamily`, `fontSize`, `fontColor` and `textAlign` are then
  /// applied over it.
  public var body: [String: JSONValue]?
  public var paragraphs: [Paragraph]

  public init(body: [String: JSONValue]? = nil, paragraphs: [Paragraph]) {
    self.body = body
    self.paragraphs = paragraphs
  }

  /// Unstyled text: one paragraph per line of `text`, one run each. A line
  /// ends at `\r\n`, `\r` or `\n`.
  public init(plain text: String) {
    let lines = text.replacingOccurrences(of: "\r\n", with: "\n")
      .replacingOccurrences(of: "\r", with: "\n")
      .split(separator: "\n", omittingEmptySubsequences: false)
    self.init(paragraphs: lines.map { Paragraph(runs: [Run(text: String($0))]) })
  }

  /// The text without its styles: each paragraph's runs joined, one
  /// paragraph per line. This is what the engine writes to `contents`.
  public var plainText: String {
    paragraphs.map { $0.runs.map(\.text).joined() }.joined(separator: "\n")
  }

  /// One paragraph: its runs, and the paragraph properties it overrides.
  public struct Paragraph: Codable, Sendable, Hashable {
    public var runs: [Run]
    /// Every member of the paragraph's JSON object but `runs`.
    public var properties: [String: JSONValue]

    public init(runs: [Run], properties: [String: JSONValue] = [:]) {
      self.runs = runs
      self.properties = properties
    }

    private struct Key: CodingKey {
      static let runs = Key(stringValue: "runs")

      let stringValue: String
      var intValue: Int? { nil }

      init(stringValue: String) { self.stringValue = stringValue }
      init?(intValue: Int) { nil }
    }

    public init(from decoder: any Decoder) throws {
      let container = try decoder.container(keyedBy: Key.self)
      runs = try container.decode([Run].self, forKey: .runs)
      properties = [:]
      for key in container.allKeys where key.stringValue != Key.runs.stringValue {
        properties[key.stringValue] = try container.decode(JSONValue.self, forKey: key)
      }
    }

    /// Writes `properties` beside `runs`. A property named `runs` is left
    /// out: the runs are `runs`.
    public func encode(to encoder: any Encoder) throws {
      var container = encoder.container(keyedBy: Key.self)
      for (name, value) in properties where name != Key.runs.stringValue {
        try container.encode(value, forKey: Key(stringValue: name))
      }
      try container.encode(runs, forKey: .runs)
    }
  }

  /// A stretch of text in one style.
  public struct Run: Codable, Sendable, Hashable {
    /// May hold `\n`: a line break inside the paragraph.
    public var text: String
    /// What the run overrides of the body style, or `nil` for nothing.
    public var style: [String: JSONValue]?

    public init(text: String, style: [String: JSONValue]? = nil) {
      self.text = text
      self.style = style
    }
  }
}
