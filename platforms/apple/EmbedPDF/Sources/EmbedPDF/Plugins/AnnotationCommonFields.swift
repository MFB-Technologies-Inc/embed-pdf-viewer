public import EmbedPDFBindings
import Foundation

// The data fields every annotation kind declares besides `contents` and
// `blendMode` (`BASE_FIELDS` in
// `crates/epdf-engine/src/features/annotation/write/prepare.rs`), as every
// typed draft and patch carries them in `common`. They encode flat, beside
// the kind's own members, through `AnnotationCommonFields.encode(to:)` and
// `encodeAsPatch(to:)`: its `CodingKeys` is the one list of their names.

/// How an annotation relates to the one it answers: `/RT`
/// (`AnnotationReplyType` in
/// `crates/epdf-engine/src/features/annotation/vocabulary.rs`).
public enum AnnotationReplyType: String, Codable, Sendable, Hashable, CaseIterable {
  /// A reply in the other annotation's thread.
  case reply
  /// Grouped with the other annotation: the two are treated as one.
  case group
}

/// `/IRT` and `/RT`: the annotation another one answers, and how. The engine
/// links only annotations of one page, and refuses a `to` that names
/// another page's.
public struct AnnotationReply: Codable, Sendable, Hashable {
  /// The annotation answered.
  public var to: AnnotationRef
  /// Left `nil`, the engine writes `reply`.
  public var type: AnnotationReplyType?

  public init(to: AnnotationRef, type: AnnotationReplyType? = nil) {
    self.to = to
    self.type = type
  }

  private enum CodingKeys: String, CodingKey {
    case to, type
  }

  public init(from decoder: any Decoder) throws {
    let container = try decoder.container(keyedBy: CodingKeys.self)
    to = try AnnotationPosition.ref(
      container.nestedContainer(keyedBy: AnnotationPosition.RefKey.self, forKey: .to))
    type = try container.decodeIfPresent(AnnotationReplyType.self, forKey: .type)
  }

  public func encode(to encoder: any Encoder) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    try AnnotationPosition.encode(
      to, into: container.nestedContainer(keyedBy: AnnotationPosition.RefKey.self, forKey: .to))
    try container.encodeIfPresent(type, forKey: .type)
  }
}

/// The fields every annotation shares, whatever its subtype. Every typed
/// draft and patch has one as `common`, and writes its members beside the
/// subtype's own.
///
/// A member left `nil` is left out: on a draft that is the engine's default,
/// on a patch it keeps the current value. The engine's defaults for a new
/// annotation are a fresh `nm`, `print` on and every other flag off.
///
/// A patch given to `AnnotationToolsCapability.updateDefaults(_:for:)`
/// stores these with the tool's other defaults. A `reply` or `groupID`
/// stored that way makes every annotation the tool creates a reply to that
/// annotation or a member of that group.
public struct AnnotationCommonFields: Codable, Sendable, Hashable {
  /// `/NM`: the annotation's name, unique on its page. A draft left without
  /// one gets a fresh name; `.null` or an empty string asks for none. The
  /// create throws `invalid-input` for a name the page already uses.
  ///
  /// Only a draft writes it. The engine refuses to change a name after
  /// create, so a patch leaves this member out of its JSON.
  public var nm: Nullable<String>?
  /// `/Subj`. `.null` is none.
  public var subject: Nullable<String>?
  /// `/IRT` and `/RT`: the annotation this one answers. `.null` answers
  /// none.
  public var reply: Nullable<AnnotationReply>?
  /// The group the annotation belongs to, at least one character. `.null`
  /// is none.
  public var groupID: Nullable<String>?
  /// `/F` bit 1: not shown when the viewer has no handler for the subtype.
  public var invisible: Bool?
  /// `/F` bit 2: neither shown nor printed.
  public var hidden: Bool?
  /// `/F` bit 3: printed with the page. On for a new annotation.
  public var print: Bool?
  /// `/F` bit 4: the appearance keeps its size as the page zooms.
  public var noZoom: Bool?
  /// `/F` bit 5: the appearance stays upright as the page rotates.
  public var noRotate: Bool?
  /// `/F` bit 6: not shown on screen, though it may print.
  public var noView: Bool?
  /// `/F` bit 7: the user can't interact with it.
  public var readOnly: Bool?
  /// `/F` bit 8: it can't be moved, resized or deleted.
  public var locked: Bool?
  /// `/F` bit 9: `noView` is inverted for some events.
  public var toggleNoView: Bool?
  /// `/F` bit 10: its contents can't be changed.
  public var lockedContents: Bool?

  /// Every member left out.
  public init() {}

  /// The engine's names, one per member: the list a draft's and a patch's
  /// encoding write, and the list a decode reads.
  enum CodingKeys: String, CodingKey, CaseIterable {
    case nm, subject, reply
    case groupID = "groupId"
    case invisible, hidden, print, noZoom, noRotate, noView, readOnly, locked, toggleNoView,
      lockedContents
  }

  /// Reads the members from the object `decoder` holds, beside whatever else
  /// it holds. A member that is `null` reads as `.null`, a missing one as
  /// `nil`.
  public init(from decoder: any Decoder) throws {
    let container = try decoder.container(keyedBy: CodingKeys.self)
    nm = try container.decodeNullableIfPresent(String.self, forKey: .nm)
    subject = try container.decodeNullableIfPresent(String.self, forKey: .subject)
    reply = try container.decodeNullableIfPresent(AnnotationReply.self, forKey: .reply)
    groupID = try container.decodeNullableIfPresent(String.self, forKey: .groupID)
    invisible = try container.decodeIfPresent(Bool.self, forKey: .invisible)
    hidden = try container.decodeIfPresent(Bool.self, forKey: .hidden)
    print = try container.decodeIfPresent(Bool.self, forKey: .print)
    noZoom = try container.decodeIfPresent(Bool.self, forKey: .noZoom)
    noRotate = try container.decodeIfPresent(Bool.self, forKey: .noRotate)
    noView = try container.decodeIfPresent(Bool.self, forKey: .noView)
    readOnly = try container.decodeIfPresent(Bool.self, forKey: .readOnly)
    locked = try container.decodeIfPresent(Bool.self, forKey: .locked)
    toggleNoView = try container.decodeIfPresent(Bool.self, forKey: .toggleNoView)
    lockedContents = try container.decodeIfPresent(Bool.self, forKey: .lockedContents)
  }

  /// Writes the members that are set into the object `encoder` holds, beside
  /// whatever else it holds: what a draft writes.
  public func encode(to encoder: any Encoder) throws {
    try encode(to: encoder, leavingOut: [])
  }

  /// What a patch writes: every member that is set but `nm`, which the
  /// engine refuses to change after create.
  func encodeAsPatch(to encoder: any Encoder) throws {
    try encode(to: encoder, leavingOut: [.nm])
  }

  private func encode(to encoder: any Encoder, leavingOut: Set<CodingKeys>) throws {
    var container = encoder.container(keyedBy: CodingKeys.self)
    for key in CodingKeys.allCases where !leavingOut.contains(key) {
      switch key {
      case .nm: try container.encodeIfPresent(nm, forKey: key)
      case .subject: try container.encodeIfPresent(subject, forKey: key)
      case .reply: try container.encodeIfPresent(reply, forKey: key)
      case .groupID: try container.encodeIfPresent(groupID, forKey: key)
      case .invisible: try container.encodeIfPresent(invisible, forKey: key)
      case .hidden: try container.encodeIfPresent(hidden, forKey: key)
      case .print: try container.encodeIfPresent(print, forKey: key)
      case .noZoom: try container.encodeIfPresent(noZoom, forKey: key)
      case .noRotate: try container.encodeIfPresent(noRotate, forKey: key)
      case .noView: try container.encodeIfPresent(noView, forKey: key)
      case .readOnly: try container.encodeIfPresent(readOnly, forKey: key)
      case .locked: try container.encodeIfPresent(locked, forKey: key)
      case .toggleNoView: try container.encodeIfPresent(toggleNoView, forKey: key)
      case .lockedContents: try container.encodeIfPresent(lockedContents, forKey: key)
      }
    }
  }
}

extension KeyedDecodingContainer {
  /// A `Nullable` member: `nil` when the key is missing, `.null` when it
  /// holds `null`, else its value.
  func decodeNullableIfPresent<Value: Decodable>(
    _ type: Value.Type, forKey key: Key
  ) throws -> Nullable<Value>? {
    guard contains(key) else { return nil }
    return try decodeNil(forKey: key) ? .null : .value(try decode(type, forKey: key))
  }
}
