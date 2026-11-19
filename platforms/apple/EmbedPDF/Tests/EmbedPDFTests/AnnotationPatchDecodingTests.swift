import CustomDump
import EmbedPDF
import Foundation
import Testing

extension BaseSuite {
  /// The typed patches read back: a patch decodes from the field bag it
  /// encodes to, and `AnnotationToolsCapability.defaults(for:as:)` reads a
  /// tool's defaults as one.
  @Suite struct AnnotationPatchDecodingTests {
    static let quad = AnnotationDraftTests.quad
    static let box = AnnotationKindDraftTests.box
    static let comment = AnnotationRichText(
      body: ["family": .string("helvetica"), "size": .number(12)],
      paragraphs: [
        .init(
          runs: [.init(text: "Styled "), .init(text: "comment", style: ["weight": .number(700)])],
          properties: ["align": .string("center")])
      ])

    /// Every common member set, two of them to `.null`.
    static var fullCommon: AnnotationCommonFields {
      var common = AnnotationCommonFields()
      common.nm = "patch-1"
      common.subject = .null
      common.reply = .value(
        AnnotationReply(
          to: .objectNumber(page: PageRef(pageObjectNumber: 2), annotObjectNumber: 9), type: .group))
      common.groupID = "reviewers"
      common.invisible = false
      common.hidden = true
      common.print = false
      common.noZoom = true
      common.noRotate = true
      common.noView = false
      common.readOnly = true
      common.locked = true
      common.toggleNoView = false
      common.lockedContents = true
      return common
    }

    func opened() async throws -> AnnotationToolsCapability {
      let viewer = try PDFViewer(ViewerConfig(interaction: true, annotation: ""))
      try await viewer.start()
      let document = try await viewer.open(Fixture.data("report"))
      let annotations = try await viewer.annotations(for: document)
      try await annotations.refresh()
      return annotations.tools
    }

    func decoded<Patch: AnnotationPatch>(_ json: String) throws -> Patch {
      try JSONDecoder().decode(Patch.self, from: Data(json.utf8))
    }

    /// `patch`, with `fullCommon`, is itself after `json()` and a decode, but
    /// for `common.nm`: a patch leaves it out of its JSON, so it comes back
    /// `nil`.
    func expectRoundTrip<Patch: AnnotationPatch & Equatable>(
      _ patch: Patch, _ common: WritableKeyPath<Patch, AnnotationCommonFields>,
      sourceLocation: SourceLocation = #_sourceLocation
    ) throws {
      var written = patch
      written[keyPath: common] = Self.fullCommon
      #expect(written[keyPath: common].nm == "patch-1", sourceLocation: sourceLocation)
      var expected = written
      expected[keyPath: common].nm = nil
      let read: Patch = try decoded(try written.json())
      #expect(read == expected, sourceLocation: sourceLocation)
      expectNoDifference(try read.json(), try written.json())
    }

    // ── every patch round-trips ─────────────────────────────────────────

    @Test func inkHighlightAndNotePatchesRoundTrip() throws {
      try expectRoundTrip(
        InkAnnotationPatch(
          inkList: [AnnotationDraftTests.stroke, [Point(x: 1.5, y: 2)]], color: "#1f6feb",
          opacity: 0.75, strokeWidth: 3, borderStyle: .dashed, dashArray: .null,
          intent: .value(.inkHighlight), rotation: 30, contents: "Ink text", blendMode: .screen,
          richText: .value(Self.comment)),
        \.common)
      try expectRoundTrip(
        HighlightAnnotationPatch(
          quadPoints: [Self.quad], color: "#ffcd45", opacity: 0.5, contents: .null,
          blendMode: .multiply, richText: .value(Self.comment)),
        \.common)
      try expectRoundTrip(
        NoteAnnotationPatch(
          rect: AnnotationDraftTests.noteRect, color: "#e5484d", opacity: 0.5, icon: .newParagraph,
          open: true, state: "accepted", stateModel: .null, contents: "Typed note",
          blendMode: .darken, richText: .null),
        \.common)
    }

    @Test func textMarkupAndRedactionPatchesRoundTrip() throws {
      try expectRoundTrip(
        UnderlineAnnotationPatch(
          quadPoints: [Self.quad], color: "#0000ff", opacity: 0.5, contents: .null,
          blendMode: .multiply, richText: .value(Self.comment)),
        \.common)
      try expectRoundTrip(
        SquigglyAnnotationPatch(
          quadPoints: [Self.quad], color: "#00ff00", opacity: 0.75, contents: "Squiggly text",
          blendMode: .darken, richText: .null),
        \.common)
      try expectRoundTrip(
        StrikeoutAnnotationPatch(
          quadPoints: [Self.quad], color: "#ff0000", opacity: 0.25,
          intent: .value(.strikeoutTextEdit), contents: .null, blendMode: .screen,
          richText: .value(Self.comment)),
        \.common)
      try expectRoundTrip(
        RedactionAnnotationPatch(
          quadPoints: [Self.quad], rect: Self.box, color: "#112233", opacity: 0.5,
          interiorColor: .null, overlayText: "REDACTED", repeat: true, fontFamily: "courier",
          fontSize: 9, fontColor: "#ffffff", textAlign: .center, contents: "Redaction text",
          blendMode: .normal, richText: .value(Self.comment)),
        \.common)
    }

    @Test func shapePatchesRoundTrip() throws {
      try expectRoundTrip(
        SquareAnnotationPatch(
          box: Self.box, color: .null, opacity: 0.5, strokeWidth: 3, borderStyle: .dashed,
          dashArray: [4, 2], interiorColor: "#00ff00", rotation: 15, cloudyIntensity: 2,
          contents: "Square text", blendMode: .multiply, richText: .value(Self.comment)),
        \.common)
      try expectRoundTrip(
        CircleAnnotationPatch(
          box: Self.box, color: "#0000ff", opacity: 0.75, strokeWidth: 2, borderStyle: .solid,
          dashArray: [1], interiorColor: "#ffff00", rotation: .null, cloudyIntensity: .null,
          contents: "Circle text", blendMode: .overlay, richText: .value(Self.comment)),
        \.common)
      var scale = AnnotationKindDraftTests.scale
      scale.origin = MeasureOrigin(x: 3, y: 4)
      try expectRoundTrip(
        LineAnnotationPatch(
          start: Point(x: 10, y: 10), end: Point(x: 100, y: 50), color: "#ff00ff", opacity: 0.5,
          strokeWidth: 2, borderStyle: .dashed, dashArray: [3, 1], interiorColor: .null,
          lineEndings: AnnotationLineEndings(start: .openArrow, end: .reversedClosedArrow),
          rotation: 45, intent: .value(.lineDimension), captionEnabled: true, captionPosition: .top,
          captionOffset: .value(AnnotationCaptionOffset(along: 1, perpendicular: 2)),
          leader: .value(AnnotationLineLeader(length: 5, extension: 1, offset: 2)),
          contents: "Line text", blendMode: .hardLight, richText: .value(Self.comment),
          measure: .value(scale)),
        \.common)
      try expectRoundTrip(
        PolygonAnnotationPatch(
          vertices: [Point(x: 10, y: 10), Point(x: 100, y: 10), Point(x: 50, y: 80)],
          color: "#0000ff", opacity: 0.5, strokeWidth: 2, borderStyle: .dashed, dashArray: [2, 2],
          interiorColor: "#ffff00", rotation: 10, intent: .value(.polygonCloud), cloudyIntensity: 2,
          captionEnabled: true, captionCenter: .at(Point(x: 50, y: 30)), contents: "Polygon text",
          blendMode: .lighten, richText: .value(Self.comment), measure: .null),
        \.common)
      try expectRoundTrip(
        PolylineAnnotationPatch(
          vertices: [Point(x: 10, y: 10), Point(x: 60, y: 80), Point(x: 100, y: 10)],
          color: "#00ffff", opacity: 0.75, strokeWidth: 3, borderStyle: .solid, dashArray: [5],
          interiorColor: "#ff0000", lineEndings: AnnotationLineEndings(start: .circle, end: .diamond),
          rotation: 20, intent: .value(.polylineDimension), captionEnabled: .null,
          captionCenter: .automatic, contents: "Polyline text", blendMode: .difference,
          richText: .value(Self.comment), measure: .value(scale)),
        \.common)
    }

    @Test func boxPatchesRoundTrip() throws {
      try expectRoundTrip(
        FreeTextAnnotationPatch(
          box: Self.box, contents: "Styled comment", fontFamily: "times-roman", fontSize: 14,
          fontColor: "#112233", textAlign: .justify, verticalAlign: .middle, color: "#445566",
          interiorColor: "#ffffcc", opacity: 0.75, strokeWidth: 2, borderStyle: .dashed,
          dashArray: [2, 1], cloudyIntensity: 1, rotation: .null,
          intent: .value(.freeTextCallout),
          calloutLine: .value(
            .kneed(from: Point(x: 20, y: 20), knee: Point(x: 60, y: 60), to: Point(x: 100, y: 130))),
          lineEnding: .value(.openArrow), blendMode: .multiply, richText: Self.comment),
        \.common)
      try expectRoundTrip(
        CaretAnnotationPatch(
          box: Self.box, color: "#ff0000", opacity: 0.5, rotation: 90, intent: .value(.replace),
          contents: .null, blendMode: .multiply, richText: .value(Self.comment)),
        \.common)
      try expectRoundTrip(
        LinkAnnotationPatch(
          rect: Self.box,
          target: .value(.goto(.xyz(page: PageRef(pageObjectNumber: 4), x: 10, y: nil, zoom: 2))),
          contents: .null),
        \.common)
      try expectRoundTrip(
        StampAnnotationPatch(
          box: Self.box, opacity: 0.5, rotation: 90, name: "Approved", fit: .null,
          contents: "Stamp text", richText: .value(Self.comment)),
        \.common)
      try expectRoundTrip(
        FileAttachmentAnnotationPatch(
          rect: Self.box, icon: .pushPin, color: "#facc15", opacity: 0.5, contents: .null,
          blendMode: .multiply, richText: .value(Self.comment)),
        \.common)
    }

    // ── the codec's rules ───────────────────────────────────────────────

    @Test func nullAndAbsentDecodeDifferently() throws {
      let null: InkAnnotationPatch = try decoded(#"{"color":null}"#)
      #expect(null.color == .null)
      let absent: InkAnnotationPatch = try decoded("{}")
      #expect(absent.color == nil)
      expectNoDifference(absent, InkAnnotationPatch())
    }

    @Test func aNullableDecodesNullAndItsValue() throws {
      let values = try JSONDecoder().decode([Nullable<Double>].self, from: Data("[null, 2.5]".utf8))
      expectNoDifference(values, [.null, 2.5])
    }

    @Test func everyNullableMemberReadsNull() throws {
      let line: LineAnnotationPatch = try decoded(
        #"{"captionOffset":null,"color":null,"contents":null,"dashArray":null,"intent":null,"interiorColor":null,"leader":null,"measure":null,"richText":null,"rotation":null,"subject":null}"#
      )
      var common = AnnotationCommonFields()
      common.subject = .null
      expectNoDifference(
        line,
        LineAnnotationPatch(
          color: .null, dashArray: .null, interiorColor: .null, rotation: .null, intent: .null,
          captionOffset: .null, leader: .null, contents: .null, richText: .null, measure: .null,
          common: common))
      // A caption centre's `null` is a value, the engine's own placement.
      let polygon: PolygonAnnotationPatch = try decoded(#"{"captionCenter":null}"#)
      expectNoDifference(polygon, PolygonAnnotationPatch(captionCenter: .automatic))
      // A free text's rich text has no `.null`: `null` reads as left out.
      let freeText: FreeTextAnnotationPatch = try decoded(#"{"richText":null}"#)
      expectNoDifference(freeText, FreeTextAnnotationPatch())
    }

    @Test func aPatchIgnoresKeysItDoesNotDeclare() throws {
      let ink: InkAnnotationPatch = try decoded(
        #"{"icon":5,"strokeWidth":5,"subtype":"ink","target":"nowhere","zzz":[1]}"#)
      expectNoDifference(ink, InkAnnotationPatch(strokeWidth: 5))
      // The kinds that share a field bag still ignore each other's members.
      let underline: UnderlineAnnotationPatch = try decoded(#"{"intent":7,"opacity":0.5}"#)
      expectNoDifference(underline, UnderlineAnnotationPatch(opacity: 0.5))
      let polygon: PolygonAnnotationPatch = try decoded(#"{"lineEndings":7,"opacity":0.5}"#)
      expectNoDifference(polygon, PolygonAnnotationPatch(opacity: 0.5))
      let polyline: PolylineAnnotationPatch = try decoded(#"{"cloudyIntensity":"x","opacity":0.5}"#)
      expectNoDifference(polyline, PolylineAnnotationPatch(opacity: 0.5))
      let link: LinkAnnotationPatch = try decoded(#"{"blendMode":"normal","richText":7}"#)
      expectNoDifference(link, LinkAnnotationPatch())
    }

    @Test func aDeclaredKeyOfTheWrongShapeThrows() {
      #expect(throws: DecodingError.self) {
        let _: InkAnnotationPatch = try decoded(#"{"strokeWidth":"wide"}"#)
      }
      #expect(throws: DecodingError.self) {
        let _: InkAnnotationPatch = try decoded(#"{"borderStyle":"beveled"}"#)
      }
      #expect(throws: DecodingError.self) {
        let _: PolygonAnnotationPatch = try decoded(#"{"intent":"polyline-dimension"}"#)
      }
      #expect(throws: DecodingError.self) {
        let _: FreeTextAnnotationPatch = try decoded(#"{"calloutLine":[{"x":1,"y":2}]}"#)
      }
    }

    /// A patch never writes `nm`, but a field bag can hold one, and a decoded
    /// patch shows it.
    @Test func aDecodedPatchKeepsTheNameItsJSONCarries() throws {
      let note: NoteAnnotationPatch = try decoded(#"{"nm":"note-1","open":true}"#)
      #expect(note.common.nm == "note-1")
      expectNoDifference(try note.json(), #"{"open":true}"#)
    }

    @Test func aLinePatchReadsBothPoints() throws {
      let line: LineAnnotationPatch = try decoded(
        #"{"linePoints":{"end":{"x":3,"y":4},"start":{"x":1,"y":2}}}"#)
      expectNoDifference(line, LineAnnotationPatch(start: Point(x: 1, y: 2), end: Point(x: 3, y: 4)))
    }

    // ── tool defaults ───────────────────────────────────────────────────

    @Test func defaultsReadBackWhatUpdateDefaultsWrote() async throws {
      let tools = try await opened()
      var common = AnnotationCommonFields()
      common.subject = "Review"
      common.groupID = "reviewers"
      common.locked = true
      try await tools.updateDefaults(InkAnnotationPatch(strokeWidth: 5, common: common), for: .ink)
      let ink = try await tools.defaults(for: .ink, as: InkAnnotationPatch.self)
      #expect(ink.strokeWidth == 5)
      expectNoDifference(ink.common.subject, "Review")
      expectNoDifference(ink.common.groupID, "reviewers")
      expectNoDifference(ink.common.locked, true)
      // The members the patch left alone are the tool's own.
      expectNoDifference(ink.common.print, true)
      expectNoDifference(ink.borderStyle, .solid)
    }

    /// The engine reports a default set to nothing as `null` and leaves an
    /// unset one out, and the typed read keeps the two apart.
    @Test func aNullDefaultAndAnUnsetOneReadApart() async throws {
      let tools = try await opened()
      let fresh = try await tools.defaults(for: .ink, as: InkAnnotationPatch.self)
      expectNoDifference(fresh.color, "#ef4444")
      #expect(fresh.dashArray == nil)
      #expect(fresh.rotation == nil)
      #expect(fresh.contents == nil)
      #expect(fresh.common.subject == nil)

      try await tools.updateDefaults(InkAnnotationPatch(color: .null, dashArray: .null), for: .ink)
      let raw = try #require(
        JSONSerialization.jsonObject(with: Data(try await tools.defaults(for: .ink).utf8))
          as? [String: Any])
      #expect(raw["color"] is NSNull)
      #expect(raw["dashArray"] is NSNull)
      #expect(raw["rotation"] == nil)
      let ink = try await tools.defaults(for: .ink, as: InkAnnotationPatch.self)
      #expect(ink.color == .null)
      #expect(ink.dashArray == .null)
      #expect(ink.rotation == nil)
    }

    @Test func everyToolsDefaultsReadAsItsPatch() async throws {
      let tools = try await opened()
      func reads<Patch: AnnotationPatch>(_ type: Patch.Type, _ ids: ToolID...) async throws {
        for id in ids {
          _ = try await tools.defaults(for: id, as: type)
        }
      }
      try await reads(InkAnnotationPatch.self, .ink, .inkHighlight)
      try await reads(HighlightAnnotationPatch.self, .highlight)
      try await reads(UnderlineAnnotationPatch.self, .underline)
      try await reads(SquigglyAnnotationPatch.self, .squiggly)
      try await reads(StrikeoutAnnotationPatch.self, .strikeout)
      try await reads(RedactionAnnotationPatch.self, .redact)
      try await reads(SquareAnnotationPatch.self, .square)
      try await reads(CircleAnnotationPatch.self, .circle)
      try await reads(LineAnnotationPatch.self, .line, .distance, .calibrate)
      try await reads(PolygonAnnotationPatch.self, .polygon, .area)
      try await reads(PolylineAnnotationPatch.self, .polyline, .perimeter)
      try await reads(FreeTextAnnotationPatch.self, .freeText, .freeTextCallout)
      try await reads(CaretAnnotationPatch.self, .insertText, .replaceText)
      try await reads(StampAnnotationPatch.self, .stamp)
      try await reads(LinkAnnotationPatch.self, .link)
      try await reads(NoteAnnotationPatch.self, .note)
      try await reads(FileAttachmentAnnotationPatch.self, .attachment)

      let distance = try await tools.defaults(for: .distance, as: LineAnnotationPatch.self)
      expectNoDifference(distance.intent, .value(.lineDimension))
      expectNoDifference(distance.leader, .value(AnnotationLineLeader(length: 12, extension: 5, offset: 0)))
      expectNoDifference(
        distance.lineEndings, AnnotationLineEndings(start: .closedArrow, end: .closedArrow))
    }

    /// The defaults are an open field bag: the JSON overload stores a name,
    /// and a value no patch member holds.
    @Test func defaultsThatDoNotReadAsThePatchThrowInvalidInput() async throws {
      let tools = try await opened()
      try await tools.updateDefaults(#"{"nm":"fixed-name"}"#, for: .ink)
      let named = try await tools.defaults(for: .ink, as: InkAnnotationPatch.self)
      #expect(named.common.nm == "fixed-name")

      try await tools.updateDefaults(#"{"strokeWidth":"wide"}"#, for: .ink)
      await #expect {
        _ = try await tools.defaults(for: .ink, as: InkAnnotationPatch.self)
      } throws: {
        ($0 as? PDFError)?.code == .invalidInput
      }
      // Another kind's patch doesn't read `strokeWidth`, so it still decodes.
      _ = try await tools.defaults(for: .ink, as: HighlightAnnotationPatch.self)
    }
  }
}
