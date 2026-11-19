import EmbedPDFBindings
import Foundation

// `richText` and `measure` as the typed drafts, patches and reads carry
// them. Both are data fields of more than one kind (`kind_fields` in
// `crates/epdf-engine/src/features/annotation/write/prepare.rs`): every
// kind but link and widget declares `richText`, and line, polygon and
// polyline declare `measure`. Each draft and patch writes them flat, beside
// its other members, through `AnnotationSharedMembers`, whose `CodingKeys`
// is the one list of the two names.
//
// A measure is a `PdfMeasurement`, the type `MeasurementCapability` already
// hands out. That type is generated and has no `Codable`, so
// `WireMeasurement` mirrors it member for member: the annotation wire's
// measure (`read_measure` and `assert_writable_measure` in
// `crates/epdf-engine/src/features/measure.rs`) has no member
// `PdfMeasurement` lacks.

enum AnnotationSharedMembers {
  enum CodingKeys: String, CodingKey {
    case richText, measure
  }

  /// A comment's rich text: left out when `nil`, `null` for `.null`.
  static func encode(richText: Nullable<AnnotationRichText>?, to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(richText, forKey: .richText)
  }

  /// A free text's rich text, which has no `null`.
  static func encode(richText: AnnotationRichText?, to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(richText, forKey: .richText)
  }

  static func encode(measure: Nullable<PdfMeasurement>?, to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try container.encodeIfPresent(measure?.map(WireMeasurement.init), forKey: .measure)
  }
}

extension KeyedDecodingContainer {
  /// A read's `measure`: `nil` when the key is missing or `null`.
  func decodeMeasureIfPresent(forKey key: Key) throws -> PdfMeasurement? {
    try decodeIfPresent(WireMeasurement.self, forKey: key)?.measurement
  }
}

/// `PdfMeasurement` as the engine's JSON spells it. A member that is `nil`
/// is left out, which is how the engine reads and writes an absent one.
struct WireMeasurement: Codable, Sendable, Hashable {
  var subtype: String
  var ratio: String?
  var x: [WireNumberFormat]?
  var y: [WireNumberFormat]?
  var distance: [WireNumberFormat]?
  var area: [WireNumberFormat]?
  var angle: [WireNumberFormat]?
  var slope: [WireNumberFormat]?
  var origin: WireOrigin?
  var cyx: Double?

  struct WireOrigin: Codable, Sendable, Hashable {
    var x: Double
    var y: Double
  }

  init(_ measurement: PdfMeasurement) {
    func formats(_ list: [PdfNumberFormat]?) -> [WireNumberFormat]? {
      list.map { $0.map(WireNumberFormat.init) }
    }
    subtype = measurement.subtype
    ratio = measurement.ratio
    x = formats(measurement.x)
    y = formats(measurement.y)
    distance = formats(measurement.distance)
    area = formats(measurement.area)
    angle = formats(measurement.angle)
    slope = formats(measurement.slope)
    origin = measurement.origin.map { WireOrigin(x: $0.x, y: $0.y) }
    cyx = measurement.cyx
  }

  var measurement: PdfMeasurement {
    func formats(_ list: [WireNumberFormat]?) -> [PdfNumberFormat]? {
      list.map { $0.map(\.format) }
    }
    return PdfMeasurement(
      subtype: subtype, ratio: ratio, x: formats(x), y: formats(y), distance: formats(distance),
      area: formats(area), angle: formats(angle), slope: formats(slope),
      origin: origin.map { MeasureOrigin(x: $0.x, y: $0.y) }, cyx: cyx)
  }
}

/// `PdfNumberFormat` as the engine's JSON spells it.
struct WireNumberFormat: Codable, Sendable, Hashable {
  var unit: String
  var conversion: Double?
  var fraction: Fraction?
  var precision: Double?
  var fixed: Bool?
  var thousands: String?
  var decimal: String?
  var prefixSpacing: String?
  var suffixSpacing: String?
  var labelPosition: LabelPosition?

  enum Fraction: String, Codable, Sendable, Hashable {
    case decimal, fraction, round, truncate
  }

  enum LabelPosition: String, Codable, Sendable, Hashable {
    case suffix, prefix
  }

  init(_ format: PdfNumberFormat) {
    unit = format.unit
    conversion = format.conversion
    fraction = format.fraction.map {
      switch $0 {
      case .decimal: .decimal
      case .fraction: .fraction
      case .round: .round
      case .truncate: .truncate
      }
    }
    precision = format.precision
    fixed = format.fixed
    thousands = format.thousands
    decimal = format.decimal
    prefixSpacing = format.prefixSpacing
    suffixSpacing = format.suffixSpacing
    labelPosition = format.labelPosition.map {
      switch $0 {
      case .suffix: .suffix
      case .prefix: .prefix
      }
    }
  }

  var format: PdfNumberFormat {
    PdfNumberFormat(
      unit: unit, conversion: conversion,
      fraction: fraction.map {
        switch $0 {
        case .decimal: .decimal
        case .fraction: .fraction
        case .round: .round
        case .truncate: .truncate
        }
      },
      precision: precision, fixed: fixed, thousands: thousands, decimal: decimal,
      prefixSpacing: prefixSpacing, suffixSpacing: suffixSpacing,
      labelPosition: labelPosition.map {
        switch $0 {
        case .suffix: .suffix
        case .prefix: .prefix
        }
      })
  }
}
