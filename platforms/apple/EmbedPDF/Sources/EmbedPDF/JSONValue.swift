/// Any JSON value: what a member holds when the engine passes it through
/// without fixing its shape (`AnnotationRichText`'s body, run styles and
/// paragraph properties). Decoding and encoding again keeps every key and
/// value; a number is a `Double`, as JSON's is.
public enum JSONValue: Codable, Sendable, Hashable {
  case null
  case bool(Bool)
  case number(Double)
  case string(String)
  case array([JSONValue])
  case object([String: JSONValue])

  public init(from decoder: any Decoder) throws {
    let container = try decoder.singleValueContainer()
    if container.decodeNil() {
      self = .null
    } else if let bool = try? container.decode(Bool.self) {
      self = .bool(bool)
    } else if let number = try? container.decode(Double.self) {
      self = .number(number)
    } else if let string = try? container.decode(String.self) {
      self = .string(string)
    } else if let array = try? container.decode([JSONValue].self) {
      self = .array(array)
    } else {
      self = .object(try container.decode([String: JSONValue].self))
    }
  }

  public func encode(to encoder: any Encoder) throws {
    var container = encoder.singleValueContainer()
    switch self {
    case .null: try container.encodeNil()
    case let .bool(bool): try container.encode(bool)
    case let .number(number): try container.encode(number)
    case let .string(string): try container.encode(string)
    case let .array(array): try container.encode(array)
    case let .object(object): try container.encode(object)
    }
  }
}
