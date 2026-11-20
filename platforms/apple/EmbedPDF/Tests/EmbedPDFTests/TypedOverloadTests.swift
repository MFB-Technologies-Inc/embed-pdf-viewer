import CustomDump
import EmbedPDF
import Foundation
import Testing

extension BaseSuite {
  /// Each typed overload leaves the engine where its JSON twin does.
  @Suite struct TypedOverloadTests {
    static let box = Rect(x: 100, y: 120, width: 80, height: 40)
    static let uriJSON = #"{"kind":"uri","uri":"https://example.com"}"#

    struct StyleProps: Codable, Equatable, Sendable {
      var annotationId: String
      var page: Int
    }

    func opened(_ fixture: String = "report") async throws
      -> (PDFViewer, DocumentInfo, PageRef, AnnotationCapability)
    {
      let viewer = try PDFViewer(
        ViewerConfig(interaction: true, link: true, annotation: "", measurement: MeasurementConfig()))
      try await viewer.start()
      let document = try await viewer.open(Fixture.data(fixture))
      let page = try await viewer.pages(of: document)[0].page
      let annotations = try await viewer.annotations(for: document)
      try await annotations.refresh()
      return (viewer, document, page, annotations)
    }

    func shell() async throws -> ShellCapability {
      let viewer = try PDFViewer(ViewerConfig(shell: true))
      try await viewer.start()
      let document = try await viewer.open(Fixture.data("report"))
      return try await viewer.shell(for: document)
    }

    func object(_ json: String) throws -> [String: Any] {
      try #require(JSONSerialization.jsonObject(with: Data(json.utf8)) as? [String: Any])
    }

    @Test func aSelectionTakesATypedPatch() async throws {
      let (_, _, page, annotations) = try await opened("annotation_kinds")
      // The fixture has one highlight; the second is made here.
      let fixture = try #require(try await annotations.list().first { $0.subtype == .highlight })
      let created = try await annotations.create(AnnotationDraftTests.fullHighlight, on: page)
      let highlights = [fixture.annotationRef, created]
      try await annotations.selection.select(highlights)

      let typed = try await annotations.selection.update(HighlightAnnotationPatch(opacity: 0.4))
      expectNoDifference(Set(typed.applied), Set(highlights))
      expectNoDifference(typed.failed, [])
      for highlight in highlights {
        let opacity = try await annotations.get(highlight)?.highlight?.opacity
        expectNoDifference(opacity, 0.4)
      }

      let json = try await annotations.selection.update(#"{"opacity":0.4}"#)
      expectNoDifference(json, typed)
      for highlight in highlights {
        let opacity = try await annotations.get(highlight)?.highlight?.opacity
        expectNoDifference(opacity, 0.4)
      }
    }

    @Test func anAttachedLinkIsSetAndReadTyped() async throws {
      let (_, _, page, annotations) = try await opened()
      let square = try await annotations.create(SquareAnnotationDraft(box: Self.box), on: page)
      let none = try await annotations.threads.linkTarget(square)
      expectNoDifference(none, nil)

      try await annotations.threads.setLink(.uri("https://example.com"), on: square)
      let read = try await annotations.threads.linkTarget(square)
      expectNoDifference(read, .uri("https://example.com"))
      let typed = try await annotations.threads.link(square)

      try await annotations.threads.clearLink(square)
      try await annotations.threads.setLink(Self.uriJSON, on: square)
      let json = try await annotations.threads.link(square)
      expectNoDifference(json, typed)
    }

    @Test func anAttachedGotoKeepsItsCoordinates() async throws {
      let (_, _, page, annotations) = try await opened()
      let square = try await annotations.create(SquareAnnotationDraft(box: Self.box), on: page)
      let target = AnnotationLinkTarget.goto(.xyz(page: page, x: 30, y: 40, zoom: 2))
      try await annotations.threads.setLink(target, on: square)
      let read = try await annotations.threads.linkTarget(square)
      expectNoDifference(read, target)
    }

    @Test func aDraftedGotoKeepsItsCoordinates() async throws {
      let (_, _, page, annotations) = try await opened()
      let target = AnnotationLinkTarget.goto(.xyz(page: page, x: 30, y: 40, zoom: nil))
      let ref = try await annotations.create(LinkAnnotationDraft(rect: Self.box, target: target), on: page)
      let read = try await annotations.get(ref)?.link?.target
      expectNoDifference(read, target)
      let area = AnnotationLinkTarget.goto(.fitR(page: page, x: 10, y: 20, width: 100, height: 50))
      try await annotations.update(ref, LinkAnnotationPatch(target: .value(area)))
      let moved = try await annotations.get(ref)?.link?.target
      expectNoDifference(moved, area)
    }

    /// `linkTarget(_:)` decodes with this, so a target the type can't hold
    /// is an error and never a different target.
    @Test(arguments: [
      #"{"kind":"launch","path":"notes.txt"}"#,
      #"{"kind":"named","name":"Print"}"#,
      #"{"kind":"goto","destination":{"kind":"spiral","page":{"kind":"objectNumber","objectNumber":7}}}"#,
      #"{"kind":"goto","destination":{"kind":"fitR","page":{"kind":"objectNumber","objectNumber":7},"x":1,"y":2}}"#,
    ])
    func aTargetALinkTargetCannotHoldDoesNotDecode(json: String) {
      #expect(throws: DecodingError.self) {
        try JSONDecoder().decode(AnnotationLinkTarget.self, from: Data(json.utf8))
      }
    }

    @Test func aLinkTargetDecodesWhatItEncodes() throws {
      let page = PageRef(pageObjectNumber: 7)
      let targets: [AnnotationLinkTarget] = [
        .uri("https://example.com"), .named(.lastPage),
        .goto(.xyz(page: page, x: 30, y: nil, zoom: 2)), .goto(.fit(page: page)),
        .goto(.fitH(page: page, y: 40)), .goto(.fitV(page: page, x: nil)),
        .goto(.fitR(page: page, x: 1, y: 2, width: 3, height: 4)), .goto(.fitB(page: page)),
        .goto(.fitBh(page: page, y: nil)), .goto(.fitBv(page: page, x: 5)),
      ]
      for target in targets {
        let data = try JSONEncoder().encode(target)
        expectNoDifference(try JSONDecoder().decode(AnnotationLinkTarget.self, from: data), target)
      }
    }

    @Test func aLinkTargetResolvesActivatesAndLabelsTyped() async throws {
      let (viewer, document, page, _) = try await opened()
      let link = try await viewer.link(for: document)
      let uri = AnnotationLinkTarget.uri("https://example.com")

      let resolution = try await link.resolve(uri)
      expectNoDifference(resolution, .uri(uri: "https://example.com"))
      let resolutionFromJSON = try await link.resolve(targetJSON: Self.uriJSON)
      expectNoDifference(resolution, resolutionFromJSON)

      let activation = try await link.activate(uri)
      expectNoDifference(activation, .uri(uri: "https://example.com"))
      let activationFromJSON = try await link.activate(targetJSON: Self.uriJSON)
      expectNoDifference(activation, activationFromJSON)

      let label = try await link.label(for: uri)
      let labelFromJSON = try await link.label(forTargetJSON: Self.uriJSON)
      expectNoDifference(label, labelFromJSON)
      #expect(label.contains("https://example.com"))

      let place = try await link.resolve(.goto(.xyz(page: page, x: 30, y: 40, zoom: nil)))
      guard case let .destination(destinationJSON) = place else {
        Issue.record("expected a destination, got \(place)")
        return
      }
      let destination = try object(destinationJSON)
      expectNoDifference(destination["kind"] as? String, "xyz")
      expectNoDifference(destination["x"] as? Double, 30)
      expectNoDifference(destination["y"] as? Double, 40)
    }

    @Test func aMeasurementReadsOutFromItsAnnotation() async throws {
      let (viewer, document, page, annotations) = try await opened()
      let measurement = try await viewer.measurement(for: document)
      try await measurement.ensureLoaded(page)
      let ref = try await measurement.createMeasurement(
        CreateMeasurementInput(kind: .distance, page: page, points: [Point(x: 10, y: 10), Point(x: 82, y: 10)]))
      let objectNumber = try #require(try object(ref)["objectNumber"] as? UInt32)
      let line = try #require(
        try await annotations.get(.objectNumber(page: page, annotObjectNumber: objectNumber)))

      let typed = try await measurement.readout(line)
      guard case let .value(readout) = typed else {
        Issue.record("expected a readout, got \(typed)")
        return
      }
      expectNoDifference(readout.kind, .distance)
      let fromJSON = try await measurement.readout(annotationJSON: ref)
      expectNoDifference(typed, fromJSON)
    }

    /// The ref alone is enough: no read of the annotation comes first.
    @Test func aMeasurementReadsOutFromItsRef() async throws {
      let (viewer, document, page, _) = try await opened()
      let measurement = try await viewer.measurement(for: document)
      try await measurement.ensureLoaded(page)
      let json = try await measurement.createMeasurement(
        CreateMeasurementInput(kind: .distance, page: page, points: [Point(x: 10, y: 10), Point(x: 82, y: 10)]))
      let objectNumber = try #require(try object(json)["objectNumber"] as? UInt32)
      let ref = AnnotationRef.objectNumber(page: page, annotObjectNumber: objectNumber)

      let reading = try await measurement.readout(ref)
      guard case let .value(readout) = reading else {
        Issue.record("expected a readout, got \(reading)")
        return
      }
      expectNoDifference(readout.kind, .distance)
      // 72 points is one inch, in the page's default metric scale.
      #expect(abs(readout.value - 0.0254) < 1e-6)
      let fromJSON = try await measurement.readout(annotationJSON: json)
      expectNoDifference(reading, fromJSON)
    }

    @Test func aSurfaceOpensWithTypedProps() async throws {
      let shell = try await shell()
      let props = StyleProps(annotationId: "a1", page: 3)
      try await shell.open("typed", exclusive: "left", props: props)
      try await shell.open("json", exclusive: "right", propsJSON: #"{"annotationId":"a1","page":3}"#)
      let typed = try #require(try await shell.surface("typed"))
      let json = try #require(try await shell.surface("json"))
      expectNoDifference(typed.open, true)
      expectNoDifference(typed.exclusive, "left")
      expectNoDifference(try typed.props(as: StyleProps.self), props)
      expectNoDifference(try typed.props(as: StyleProps.self), try json.props(as: StyleProps.self))
    }

    @Test func aSurfaceTogglesWithTypedProps() async throws {
      let shell = try await shell()
      let props = StyleProps(annotationId: "a1", page: 3)
      try await shell.toggle("typed", exclusive: "left", props: props)
      try await shell.toggle("json", exclusive: "right", propsJSON: #"{"annotationId":"a1","page":3}"#)
      let typed = try #require(try await shell.surface("typed"))
      let json = try #require(try await shell.surface("json"))
      expectNoDifference(typed.open, true)
      expectNoDifference(typed.exclusive, "left")
      expectNoDifference(try typed.props(as: StyleProps.self), props)
      expectNoDifference(try typed.props(as: StyleProps.self), try json.props(as: StyleProps.self))

      try await shell.toggle("typed", exclusive: "left", props: props)
      let isOpen = try await shell.isOpen("typed")
      expectNoDifference(isOpen, false)
    }

    @Test func aSurfacesPropsUpdateTyped() async throws {
      let shell = try await shell()
      try await shell.open("typed", props: StyleProps(annotationId: "a1", page: 3))
      try await shell.open("json", props: StyleProps(annotationId: "a1", page: 3))
      let props = StyleProps(annotationId: "b2", page: 5)
      try await shell.updateProps(of: "typed", props: props)
      try await shell.updateProps(of: "json", propsJSON: #"{"annotationId":"b2","page":5}"#)
      let typed = try #require(try await shell.surface("typed"))
      let json = try #require(try await shell.surface("json"))
      expectNoDifference(try typed.props(as: StyleProps.self), props)
      expectNoDifference(try typed.props(as: StyleProps.self), try json.props(as: StyleProps.self))
    }
  }
}
