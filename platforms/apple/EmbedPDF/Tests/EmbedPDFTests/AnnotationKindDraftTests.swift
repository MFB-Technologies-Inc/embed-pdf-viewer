import CustomDump
import EmbedPDF
import Foundation
import Testing

extension BaseSuite {
  /// The typed drafts, patches and read members of every subtype besides
  /// ink, highlight and note (`AnnotationDraftTests` has those). The drift
  /// guard is the same: every key a full draft or patch writes must come
  /// back in the engine's read with the value written.
  @Suite struct AnnotationKindDraftTests {
    static let quad = AnnotationDraftTests.quad
    static let box = Rect(x: 100, y: 120, width: 80, height: 40)

    static let fullUnderline = UnderlineAnnotationDraft(
      quadPoints: [quad], color: "#0000ff", opacity: 0.5, contents: "Underline text",
      blendMode: .multiply)
    static let fullSquiggly = SquigglyAnnotationDraft(
      quadPoints: [quad], color: "#00ff00", opacity: 0.75, contents: "Squiggly text",
      blendMode: .darken)
    static let fullStrikeout = StrikeoutAnnotationDraft(
      quadPoints: [quad], color: "#ff0000", opacity: 0.25, intent: .value(.strikeoutTextEdit),
      contents: "Strikeout text", blendMode: .screen)
    static let fullRedaction = RedactionAnnotationDraft(
      quadPoints: [quad], color: "#112233", opacity: 0.5, interiorColor: "#000000",
      overlayText: "REDACTED", repeat: true, fontFamily: "courier", fontSize: 9,
      fontColor: "#ffffff", textAlign: .center, contents: "Redaction text", blendMode: .normal)
    static let fullSquare = SquareAnnotationDraft(
      box: box, color: "#ff0000", opacity: 0.5, strokeWidth: 3, borderStyle: .dashed,
      dashArray: [4, 2], interiorColor: "#00ff00", rotation: 15, cloudyIntensity: 2,
      contents: "Square text", blendMode: .multiply)
    static let fullCircle = CircleAnnotationDraft(
      box: box, color: "#0000ff", opacity: 0.75, strokeWidth: 2, borderStyle: .solid,
      interiorColor: "#ffff00", rotation: 30, contents: "Circle text", blendMode: .overlay)
    static let fullLine = LineAnnotationDraft(
      start: Point(x: 10, y: 10), end: Point(x: 100, y: 50), color: "#ff00ff", opacity: 0.5,
      strokeWidth: 2, borderStyle: .dashed, dashArray: [3, 1], interiorColor: "#00ff00",
      lineEndings: AnnotationLineEndings(start: .openArrow, end: .reversedClosedArrow),
      intent: .value(.lineArrow), captionEnabled: true, captionPosition: .top,
      captionOffset: .value(AnnotationCaptionOffset(along: 1, perpendicular: 2)),
      leader: .value(AnnotationLineLeader(length: 5, extension: 1, offset: 2)),
      contents: "Line text", blendMode: .hardLight)
    static let fullPolygon = PolygonAnnotationDraft(
      vertices: [Point(x: 10, y: 10), Point(x: 100, y: 10), Point(x: 50, y: 80)], color: "#0000ff",
      opacity: 0.5, strokeWidth: 2, borderStyle: .dashed, dashArray: [2, 2],
      interiorColor: "#ffff00", intent: .value(.polygonCloud), cloudyIntensity: 2,
      captionEnabled: true, captionCenter: .at(Point(x: 50, y: 30)), contents: "Polygon text",
      blendMode: .lighten)
    static let fullPolyline = PolylineAnnotationDraft(
      vertices: [Point(x: 10, y: 10), Point(x: 60, y: 80), Point(x: 100, y: 10)],
      color: "#00ffff", opacity: 0.75, strokeWidth: 3, borderStyle: .solid,
      interiorColor: "#ff0000", lineEndings: AnnotationLineEndings(start: .circle, end: .diamond),
      captionEnabled: true, captionCenter: .at(Point(x: 60, y: 40)), contents: "Polyline text",
      blendMode: .difference)
    static let fullFreeText = FreeTextAnnotationDraft(
      box: box, contents: "Free text", fontFamily: "times-roman", fontSize: 14,
      fontColor: "#112233", textAlign: .center, verticalAlign: .middle, color: "#445566",
      interiorColor: "#ffffcc", opacity: 0.75, strokeWidth: 2, borderStyle: .dashed,
      dashArray: [2, 1], cloudyIntensity: 1, intent: .value(.freeTextCallout),
      calloutLine: .value(.kneed(from: Point(x: 20, y: 20), knee: Point(x: 60, y: 60), to: Point(x: 100, y: 130))),
      lineEnding: .value(.openArrow), blendMode: .multiply)
    static let fullCaret = CaretAnnotationDraft(
      box: Rect(x: 72, y: 300, width: 12, height: 12), color: "#00ff00", opacity: 0.5,
      intent: .value(.replace), contents: "Inserted", blendMode: .exclusion)
    static let fullLink = LinkAnnotationDraft(
      rect: box, target: .uri("https://example.test/typed"), contents: "Link text")

    static var fullDrafts: [any AnnotationDraft] {
      [
        fullUnderline, fullSquiggly, fullStrikeout, fullRedaction, fullSquare, fullCircle, fullLine,
        fullPolygon, fullPolyline, fullFreeText, fullCaret, fullLink,
      ]
    }

    func opened(_ fixture: String = "report") async throws -> (PageRef, AnnotationCapability) {
      let viewer = try PDFViewer(ViewerConfig(interaction: true, annotation: ""))
      try await viewer.start()
      let document = try await viewer.open(Fixture.data(fixture))
      let page = try await viewer.pages(of: document)[0].page
      let annotations = try await viewer.annotations(for: document)
      try await annotations.refresh()
      return (page, annotations)
    }

    func object(_ json: String) throws -> [String: Any] {
      try #require(JSONSerialization.jsonObject(with: Data(json.utf8)) as? [String: Any])
    }

    /// Every key `json` writes is in `read` with the value written.
    func expectWritten(_ json: String, in read: String, _ what: String) throws {
      let written = try object(json)
      let read = try object(read)
      for (key, value) in written where key != "subtype" {
        let readValue = try #require(read[key], "the engine read back no '\(key)' for \(what)")
        #expect(sameJSON(value, readValue), "\(what) '\(key)': wrote \(value), read \(readValue)")
      }
    }

    // ── exact encoding ──────────────────────────────────────────────────

    @Test func aLineDraftEncodesItsStructuredMembers() throws {
      expectNoDifference(
        try Self.fullLine.json(),
        ##"{"blendMode":"hard-light","borderStyle":"dashed","captionEnabled":true,"captionOffset":{"along":1,"perpendicular":2},"captionPosition":"top","color":"#ff00ff","contents":"Line text","dashArray":[3,1],"intent":"line-arrow","interiorColor":"#00ff00","leader":{"extension":1,"length":5,"offset":2},"lineEndings":{"end":"r-closed-arrow","start":"open-arrow"},"linePoints":{"end":{"x":100,"y":50},"start":{"x":10,"y":10}},"opacity":0.5,"strokeWidth":2,"subtype":"line"}"##
      )
      expectNoDifference(
        try LineAnnotationDraft(start: Point(x: 0, y: 0), end: Point(x: 1, y: 1), leader: .null)
          .json(),
        ##"{"leader":null,"linePoints":{"end":{"x":1,"y":1},"start":{"x":0,"y":0}},"subtype":"line"}"##)
    }

    @Test func aLinePatchMovesBothEndsOrNeither() throws {
      expectNoDifference(
        try LineAnnotationPatch(start: Point(x: 1, y: 2), end: Point(x: 3, y: 4)).json(),
        ##"{"linePoints":{"end":{"x":3,"y":4},"start":{"x":1,"y":2}}}"##)
      #expect { try LineAnnotationPatch(start: Point(x: 1, y: 2)).json() } throws: {
        ($0 as? PDFError)?.code == .invalidInput
      }
    }

    @Test func aCaptionCenterIsAPointOrNull() throws {
      let vertices = [Point(x: 0, y: 0), Point(x: 1, y: 0), Point(x: 0, y: 1)]
      expectNoDifference(
        try PolygonAnnotationPatch(captionCenter: .automatic).json(), ##"{"captionCenter":null}"##)
      expectNoDifference(
        try PolygonAnnotationDraft(vertices: vertices, captionCenter: .at(Point(x: 2, y: 3))).json(),
        ##"{"captionCenter":{"x":2,"y":3},"subtype":"polygon","vertices":[{"x":0,"y":0},{"x":1,"y":0},{"x":0,"y":1}]}"##
      )
    }

    @Test func aCalloutLineIsTwoOrThreePoints() throws {
      let straight = FreeTextAnnotationPatch(
        calloutLine: .value(.straight(from: Point(x: 1, y: 2), to: Point(x: 3, y: 4))))
      expectNoDifference(
        try straight.json(), ##"{"calloutLine":[{"x":1,"y":2},{"x":3,"y":4}]}"##)
      expectNoDifference(
        try FreeTextAnnotationPatch(calloutLine: .null, lineEnding: .null).json(),
        ##"{"calloutLine":null,"lineEnding":null}"##)
    }

    @Test func aLinkTargetIsTaggedByKind() throws {
      let page = PageRef(pageObjectNumber: 7)
      expectNoDifference(
        try LinkAnnotationPatch(target: .value(.goto(.xyz(page: page, x: 10, y: nil, zoom: 2))))
          .json(),
        ##"{"target":{"destination":{"kind":"xyz","page":{"kind":"objectNumber","objectNumber":7},"x":10,"y":null,"zoom":2},"kind":"goto"}}"##
      )
      expectNoDifference(
        try LinkAnnotationPatch(target: .value(.named(.previousPage))).json(),
        ##"{"target":{"kind":"named","name":"PrevPage"}}"##)
      expectNoDifference(
        try LinkAnnotationPatch(target: .value(.goto(.fitR(page: page, x: 1, y: 2, width: 3, height: 4))))
          .json(),
        ##"{"target":{"destination":{"height":4,"kind":"fitR","page":{"kind":"objectNumber","objectNumber":7},"width":3,"x":1,"y":2},"kind":"goto"}}"##
      )
      expectNoDifference(try LinkAnnotationPatch(target: .null).json(), ##"{"target":null}"##)
    }

    @Test func emptyPatchesAreEmptyObjects() throws {
      let patches: [any AnnotationPatch] = [
        UnderlineAnnotationPatch(), SquigglyAnnotationPatch(), StrikeoutAnnotationPatch(),
        RedactionAnnotationPatch(), SquareAnnotationPatch(), CircleAnnotationPatch(),
        LineAnnotationPatch(), PolygonAnnotationPatch(), PolylineAnnotationPatch(),
        FreeTextAnnotationPatch(), CaretAnnotationPatch(), LinkAnnotationPatch(),
        StampAnnotationPatch(), FileAttachmentAnnotationPatch(),
      ]
      for patch in patches {
        expectNoDifference(try patch.json(), "{}", "\(type(of: patch))")
      }
    }

    @Test func eachDraftNamesItsSubtype() throws {
      var subtypes: [String] = []
      for draft in Self.fullDrafts {
        subtypes.append(try #require(object(try draft.json())["subtype"] as? String))
      }
      expectNoDifference(
        subtypes,
        [
          "underline", "squiggly", "strikeout", "redact", "square", "circle", "line", "polygon",
          "polyline", "free-text", "caret", "link",
        ])
    }

    @Test func theVocabularyIsTheEnginesSpelling() {
      expectNoDifference(
        AnnotationBlendMode.allCases.map(\.rawValue),
        [
          "normal", "multiply", "screen", "overlay", "darken", "lighten", "color-dodge", "color-burn",
          "hard-light", "soft-light", "difference", "exclusion", "hue", "saturation", "color",
          "luminosity",
        ])
      expectNoDifference(
        AnnotationLineEnding.allCases.map(\.rawValue),
        [
          "none", "square", "circle", "diamond", "open-arrow", "closed-arrow", "butt", "r-open-arrow",
          "r-closed-arrow", "slash",
        ])
      expectNoDifference(
        AnnotationFileAttachmentIcon.allCases.map(\.rawValue), ["graph", "push-pin", "paperclip", "tag"])
      expectNoDifference(
        AnnotationLinkVerb.allCases.map(\.rawValue), ["NextPage", "PrevPage", "FirstPage", "LastPage"])
      expectNoDifference(
        AnnotationTextAlignment.allCases.map(\.rawValue), ["left", "center", "right", "justify"])
      expectNoDifference(AnnotationVerticalAlignment.allCases.map(\.rawValue), ["top", "middle", "bottom"])
      expectNoDifference(AnnotationStampFit.allCases.map(\.rawValue), ["contain", "cover", "fill"])
      expectNoDifference(AnnotationCaptionPosition.allCases.map(\.rawValue), ["inline", "top"])
      expectNoDifference(AnnotationStrikeoutIntent.allCases.map(\.rawValue), ["strikeout-text-edit"])
      expectNoDifference(AnnotationLineIntent.allCases.map(\.rawValue), ["line-arrow", "line-dimension"])
      expectNoDifference(
        AnnotationPolygonIntent.allCases.map(\.rawValue), ["polygon-cloud", "polygon-dimension"])
      expectNoDifference(AnnotationPolylineIntent.allCases.map(\.rawValue), ["polyline-dimension"])
      expectNoDifference(
        AnnotationFreeTextIntent.allCases.map(\.rawValue), ["free-text", "free-text-callout"])
      expectNoDifference(AnnotationCaretIntent.allCases.map(\.rawValue), ["replace"])
    }

    // ── drift guard ─────────────────────────────────────────────────────

    /// Every key a full draft writes comes back in the engine's read of the
    /// annotation it created, with the value written.
    @Test func everyTypedDraftKeyRoundTripsThroughTheEngine() async throws {
      let (page, annotations) = try await opened()
      for draft in Self.fullDrafts {
        let json = try draft.json()
        let ref = try await annotations.create(draft, on: page)
        let read = try #require(try await annotations.get(ref))
        try expectWritten(json, in: read.fieldsJSON, "\(type(of: draft))")
        #expect(read.details != .untyped, "\(type(of: draft)) read back untyped")
      }
    }

    /// Every key a full patch writes comes back in the engine's read of the
    /// annotation it changed, and the typed read changes with it.
    @Test func everyTypedPatchKeyRoundTripsThroughTheEngine() async throws {
      let (page, annotations) = try await opened()
      let moved = Quad(
        upperLeft: Point(x: 80, y: 400), upperRight: Point(x: 160, y: 400),
        lowerLeft: Point(x: 80, y: 414), lowerRight: Point(x: 160, y: 414))
      let otherBox = Rect(x: 200, y: 220, width: 60, height: 30)
      let cases: [(any AnnotationDraft, any AnnotationPatch)] = [
        (
          Self.fullUnderline,
          UnderlineAnnotationPatch(
            quadPoints: [moved], color: "#123456", opacity: 1, contents: "Changed", blendMode: .normal)
        ),
        (
          Self.fullSquiggly,
          SquigglyAnnotationPatch(
            quadPoints: [moved], color: "#123456", opacity: 1, contents: "Changed", blendMode: .normal)
        ),
        (
          Self.fullStrikeout,
          StrikeoutAnnotationPatch(
            quadPoints: [moved], color: "#123456", opacity: 1, intent: .null, contents: "Changed",
            blendMode: .normal)
        ),
        (
          Self.fullRedaction,
          RedactionAnnotationPatch(
            quadPoints: [moved], color: "#654321", opacity: 1, interiorColor: .null,
            overlayText: .null, repeat: false, fontFamily: "helvetica", fontSize: 0,
            fontColor: "#000000", textAlign: .right, contents: "Changed", blendMode: .multiply)
        ),
        (
          Self.fullSquare,
          SquareAnnotationPatch(
            box: otherBox, color: .null, opacity: 1, strokeWidth: 0, borderStyle: .solid,
            dashArray: .null, interiorColor: .null, rotation: .null, cloudyIntensity: .null,
            contents: "Changed", blendMode: .normal)
        ),
        (
          Self.fullCircle,
          CircleAnnotationPatch(
            box: otherBox, color: "#000000", opacity: 0.25, strokeWidth: 4, borderStyle: .dashed,
            dashArray: [1, 1], interiorColor: "#ffffff", rotation: 45, cloudyIntensity: 1,
            contents: "Changed", blendMode: .normal)
        ),
        (
          Self.fullLine,
          LineAnnotationPatch(
            start: Point(x: 20, y: 30), end: Point(x: 120, y: 90), color: .null, opacity: 1,
            strokeWidth: 4, borderStyle: .solid, dashArray: .null, interiorColor: .null,
            lineEndings: AnnotationLineEndings(start: .noEnding, end: .slash), intent: .null,
            captionEnabled: false, captionPosition: .inline, captionOffset: .null, leader: .null,
            contents: "Changed", blendMode: .normal)
        ),
        (
          Self.fullPolygon,
          PolygonAnnotationPatch(
            vertices: [Point(x: 0, y: 0), Point(x: 40, y: 0), Point(x: 40, y: 40), Point(x: 0, y: 40)],
            color: "#000000", opacity: 1, strokeWidth: 1, borderStyle: .solid, dashArray: .null,
            interiorColor: .null, intent: .null, cloudyIntensity: .null, captionEnabled: false,
            contents: "Changed", blendMode: .normal)
        ),
        (
          Self.fullPolyline,
          PolylineAnnotationPatch(
            vertices: [Point(x: 0, y: 0), Point(x: 40, y: 40)], color: .null, opacity: 1,
            strokeWidth: 1, borderStyle: .dashed, dashArray: [5, 5], interiorColor: .null,
            lineEndings: AnnotationLineEndings(start: .butt, end: .closedArrow),
            captionEnabled: false, contents: "Changed", blendMode: .normal)
        ),
        (
          Self.fullFreeText,
          FreeTextAnnotationPatch(
            box: otherBox, contents: "Changed", fontFamily: "helvetica", fontSize: 10,
            fontColor: "#000000", textAlign: .justify, verticalAlign: .bottom, color: "#000000",
            interiorColor: .null, opacity: 1, strokeWidth: 0, borderStyle: .solid, dashArray: .null,
            cloudyIntensity: .null, rotation: 90, intent: .value(.freeText), calloutLine: .null,
            lineEnding: .null, blendMode: .normal)
        ),
        (
          Self.fullCaret,
          CaretAnnotationPatch(
            box: Rect(x: 90, y: 310, width: 10, height: 10), color: "#000000", opacity: 1,
            rotation: 45, intent: .null, contents: "Changed", blendMode: .normal)
        ),
        (
          Self.fullLink,
          LinkAnnotationPatch(
            rect: otherBox, target: .value(.named(.nextPage)), contents: "Changed")
        ),
      ]
      for (draft, patch) in cases {
        let ref = try await annotations.create(draft, on: page)
        try await annotations.update(ref, patch)
        let read = try #require(try await annotations.get(ref))
        try expectWritten(try patch.json(), in: read.fieldsJSON, "\(type(of: patch))")
        #expect(read.details != .untyped, "\(type(of: patch)) read back untyped")
      }
    }

    /// Stamps and file attachments have no draft: a placed stamp and the
    /// fixture's attachment take the patches.
    @Test func stampAndFileAttachmentPatchesRoundTrip() async throws {
      let (page, annotations) = try await opened()
      let stampRef = try await annotations.drafts.placeStamp(
        AnnotationStampSource(bytes: try AnnotationTests().tinyPng(), mimeType: "image/png"),
        at: AnnotationStampPlacement(page: page, center: Point(x: 200, y: 200)), targetWidth: 80)
      let stamp = try #require(await annotations.get(stampRef))
      let stampPatch = StampAnnotationPatch(
        box: Rect(x: 150, y: 310, width: 100, height: 40), opacity: 0.5, rotation: 90,
        name: "Draft", fit: .value(.cover), contents: "Changed")
      try await annotations.update(stamp.annotationRef, stampPatch)
      let stampRead = try #require(try await annotations.get(stamp.annotationRef))
      try expectWritten(try stampPatch.json(), in: stampRead.fieldsJSON, "StampAnnotationPatch")

      let (_, kinds) = try await opened("annotation_kinds")
      let attachment = try #require(try await kinds.list().first { $0.subtype == .fileAttachment })
      let attachmentPatch = FileAttachmentAnnotationPatch(
        rect: Rect(x: 470, y: 620, width: 24, height: 24), icon: .tag, color: "#00ff00",
        opacity: 0.5, contents: "Changed", blendMode: .multiply)
      try await kinds.update(attachment.annotationRef, attachmentPatch)
      let attachmentRead = try #require(try await kinds.get(attachment.annotationRef))
      try expectWritten(
        try attachmentPatch.json(), in: attachmentRead.fieldsJSON, "FileAttachmentAnnotationPatch")
      expectNoDifference(attachmentRead.fileAttachment?.icon, .tag)
      expectNoDifference(attachmentRead.fileAttachment?.file?.name, "notes.txt")
    }

    /// A patch that changes nothing but the blend mode still redraws the
    /// appearance, which is where `/BM` lives.
    @Test func aBlendModeAloneChanges() async throws {
      let (page, annotations) = try await opened()
      let highlight = try await annotations.create(AnnotationDraftTests.fullHighlight, on: page)
      try await annotations.update(highlight, HighlightAnnotationPatch(blendMode: .normal))
      let read = try await annotations.get(highlight)
      expectNoDifference(read?.blendMode, .normal)
      let square = try await annotations.create(Self.fullSquare, on: page)
      try await annotations.update(square, SquareAnnotationPatch(blendMode: .screen))
      let readSquare = try await annotations.get(square)
      expectNoDifference(readSquare?.blendMode, .screen)
    }

    // ── typed reads ─────────────────────────────────────────────────────

    @Test func createdDraftsReadBackTyped() async throws {
      let (page, annotations) = try await opened()
      func created(_ draft: some AnnotationDraft) async throws -> AnnotationInfo {
        try #require(try await annotations.get(annotations.create(draft, on: page)))
      }

      let underline = try await created(Self.fullUnderline)
      expectNoDifference(underline.blendMode, .multiply)
      expectNoDifference(underline.underline?.color, "#0000ff")
      expectNoDifference(underline.underline?.quadPoints, [Self.quad])
      let squiggly = try await created(Self.fullSquiggly)
      expectNoDifference(squiggly.squiggly?.opacity, 0.75)
      let strikeout = try #require(try await created(Self.fullStrikeout).strikeout)
      expectNoDifference(strikeout.intent, "strikeout-text-edit")

      let redaction = try #require(try await created(Self.fullRedaction).redaction)
      expectNoDifference(redaction.overlayText, "REDACTED")
      expectNoDifference(redaction.repeat, true)
      expectNoDifference(redaction.textAlign, .center)
      expectNoDifference(redaction.interiorColor, "#000000")
      expectNoDifference(redaction.fontColor, "#ffffff")

      let square = try #require(try await created(Self.fullSquare).square)
      expectNoDifference(square.box, Self.box)
      expectNoDifference(square.interiorColor, "#00ff00")
      expectNoDifference(square.cloudyIntensity, 2)
      expectNoDifference(square.rotation, 15)
      let circle = try await created(Self.fullCircle)
      expectNoDifference(circle.circle?.color, "#0000ff")
      expectNoDifference(circle.square, nil)

      let line = try #require(try await created(Self.fullLine).line)
      expectNoDifference(roundedPoint(line.start), Point(x: 10, y: 10))
      expectNoDifference(roundedPoint(line.end), Point(x: 100, y: 50))
      expectNoDifference(line.lineEndings, AnnotationLineEndings(start: .openArrow, end: .reversedClosedArrow))
      expectNoDifference(line.intent, "line-arrow")
      expectNoDifference(line.captionPosition, .top)
      expectNoDifference(line.captionOffset, AnnotationCaptionOffset(along: 1, perpendicular: 2))
      expectNoDifference(line.leader, AnnotationLineLeader(length: 5, extension: 1, offset: 2))

      let polygon = try #require(try await created(Self.fullPolygon).polygon)
      expectNoDifference(polygon.vertices.count, 3)
      expectNoDifference(polygon.intent, "polygon-cloud")
      expectNoDifference(polygon.captionCenter, Point(x: 50, y: 30))
      let polyline = try #require(try await created(Self.fullPolyline).polyline)
      expectNoDifference(polyline.lineEndings, AnnotationLineEndings(start: .circle, end: .diamond))

      let freeTextInfo = try await created(Self.fullFreeText)
      expectNoDifference(freeTextInfo.contents, "Free text")
      let freeText = try #require(freeTextInfo.freeText)
      expectNoDifference(roundedRect(freeText.box), Self.box)
      expectNoDifference(freeText.intent, "free-text-callout")
      expectNoDifference(freeText.fontFamily, "times-roman")
      expectNoDifference(freeText.textAlign, .center)
      expectNoDifference(freeText.verticalAlign, .middle)
      expectNoDifference(freeText.fontColor, "#112233")
      expectNoDifference(freeText.lineEnding, .openArrow)
      expectNoDifference(
        freeText.calloutLine?.points.map(roundedPoint),
        [Point(x: 20, y: 20), Point(x: 60, y: 60), Point(x: 100, y: 130)])

      let caret = try #require(try await created(Self.fullCaret).caret)
      expectNoDifference(caret.intent, "replace")
      let link = try #require(try await created(Self.fullLink).link)
      expectNoDifference(link.target, .uri("https://example.test/typed"))
    }

    @Test func aLinkReadsAPageTargetBack() async throws {
      let (page, annotations) = try await opened()
      let target = AnnotationLinkTarget.goto(.fit(page: page))
      let ref = try await annotations.create(LinkAnnotationDraft(rect: Self.box, target: target), on: page)
      let read = try await annotations.get(ref)
      expectNoDifference(read?.link?.target, target)
    }

    @Test func everyFixtureKindReadsTyped() async throws {
      let (_, annotations) = try await opened("annotation_kinds")
      var bySubtype: [AnnotationSubtype: AnnotationInfo] = [:]
      for info in try await annotations.list() where bySubtype[info.subtype] == nil {
        bySubtype[info.subtype] = info
      }
      for (subtype, info) in bySubtype where subtype != .popup && subtype != .widget {
        #expect(info.details != .untyped, "\(subtype) read untyped: \(info.fieldsJSON)")
        #expect(info.blendMode != nil, "\(subtype) has no blend mode")
      }
      expectNoDifference(bySubtype[.highlight]?.blendMode, .multiply)
      expectNoDifference(bySubtype[.popup]?.details, .untyped)
      let line = try #require(bySubtype[.line]?.line)
      expectNoDifference(line.lineEndings, AnnotationLineEndings(start: .circle, end: .closedArrow))
      expectNoDifference(line.captionEnabled, false)
      expectNoDifference(bySubtype[.polyline]?.polyline?.color, nil)
      expectNoDifference(bySubtype[.stamp]?.stamp?.name, "Approved")
      expectNoDifference(bySubtype[.caret]?.caret?.intent, "replace")
      expectNoDifference(bySubtype[.freeText]?.freeText?.textAlign, .right)
      expectNoDifference(bySubtype[.link]?.link?.target, .uri("https://example.test/linked"))
      let attachment = try #require(bySubtype[.fileAttachment]?.fileAttachment)
      expectNoDifference(attachment.icon, .paperclip)
      expectNoDifference(attachment.file?.name, "notes.txt")
      expectNoDifference(attachment.file?.mimeType, "text/plain")
      expectNoDifference(attachment.file?.sizeBytes, 15)
      expectNoDifference(attachment.file?.createdAt, Date(timeIntervalSince1970: 1_709_294_400))
      let redaction = try #require(bySubtype[.redact]?.redaction)
      expectNoDifference(redaction.overlayText, "REDACTED")
      expectNoDifference(redaction.textAlign, .center)
    }

    // ── tool defaults ───────────────────────────────────────────────────

    @Test func typedDefaultsReachTheTool() async throws {
      let (_, annotations) = try await opened()
      let cases: [(ToolID, any AnnotationPatch)] = [
        (.underline, UnderlineAnnotationPatch(color: "#123456", opacity: 0.5, blendMode: .multiply)),
        (.squiggly, SquigglyAnnotationPatch(color: "#123456", blendMode: .darken)),
        (.strikeout, StrikeoutAnnotationPatch(color: "#123456", blendMode: .screen)),
        (
          .square,
          SquareAnnotationPatch(color: .null, strokeWidth: 2, interiorColor: "#00ff00", blendMode: .multiply)
        ),
        (.circle, CircleAnnotationPatch(borderStyle: .dashed, dashArray: [2, 2], cloudyIntensity: 1)),
        (
          .line,
          LineAnnotationPatch(
            lineEndings: AnnotationLineEndings(start: .noEnding, end: .openArrow), captionEnabled: true)
        ),
        (.polygon, PolygonAnnotationPatch(interiorColor: "#ffff00", captionCenter: .automatic)),
        (
          .polyline,
          PolylineAnnotationPatch(lineEndings: AnnotationLineEndings(start: .circle, end: .circle))
        ),
        (
          .freeText,
          FreeTextAnnotationPatch(
            fontFamily: "courier", fontSize: 18, fontColor: "#ff0000", textAlign: .center,
            verticalAlign: .middle, interiorColor: "#ffffcc", strokeWidth: 0)
        ),
        (.redact, RedactionAnnotationPatch(overlayText: "X", repeat: true, textAlign: .right)),
      ]
      for (tool, patch) in cases {
        try await annotations.tools.updateDefaults(patch, for: tool)
        let defaults = try await annotations.tools.defaults(for: tool)
        try expectWritten(try patch.json(), in: defaults, "\(tool) defaults")
      }
    }

    @Test func aTypedDefaultReachesTheToolsNextAnnotation() async throws {
      let (page, annotations) = try await opened()
      try await annotations.tools.updateDefaults(
        SquareAnnotationPatch(color: "#00ff00", strokeWidth: 2, blendMode: .multiply), for: .square)
      let square = try #require(
        try await annotations.get(
          annotations.create(SquareAnnotationDraft(box: Self.box), on: page, tool: .square)))
      expectNoDifference(square.square?.color, "#00ff00")
      expectNoDifference(square.square?.strokeWidth, 2)
      expectNoDifference(square.blendMode, .multiply)
    }

    @Test func theStringOverloadsTakeTheTypedJSONAlike() async throws {
      let (page, annotations) = try await opened()
      for draft in Self.fullDrafts {
        let typed = try #require(try await annotations.get(annotations.create(draft, on: page)))
        let untyped = try #require(
          try await annotations.get(annotations.create(try draft.json(), on: page)))
        expectNoDifference(untyped.details, typed.details, "\(type(of: draft))")
        expectNoDifference(untyped.blendMode, typed.blendMode)
        expectNoDifference(untyped.contents, typed.contents)
      }
    }
  }
}

func roundedRect(_ rect: Rect) -> Rect {
  let round = { (value: Double) in (value * 1000).rounded() / 1000 }
  return Rect(x: round(rect.x), y: round(rect.y), width: round(rect.width), height: round(rect.height))
}
