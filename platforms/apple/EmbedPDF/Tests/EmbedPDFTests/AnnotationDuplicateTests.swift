import CustomDump
import EmbedPDF
import Foundation
import Testing

extension BaseSuite {
  /// Duplication, movability and remeasuring, against the engine.
  @Suite
  struct AnnotationDuplicateTests {
    static let square =
      #"{"subtype": "square", "box": {"x": 72, "y": 72, "width": 100, "height": 50}, "contents": "Original"}"#

    func opened(history: Bool = false) async throws
      -> (PDFViewer, DocumentInfo, AnnotationCapability, PageRef)
    {
      let viewer = try PDFViewer(ViewerConfig(interaction: true, annotation: "", history: history))
      try await viewer.start()
      let document = try await viewer.open(Fixture.data("report"))
      let annotations = try await viewer.annotations(for: document)
      try await annotations.refresh()
      return (viewer, document, annotations, try await viewer.pages(of: document)[0].page)
    }

    static func missing(on page: PageRef) -> AnnotationRef {
      .objectNumber(page: page, annotObjectNumber: 99999)
    }

    @Test
    func duplicateCopiesFieldsDrawingFileAndLinkWithNewRefsAsOneUndoStep() async throws {
      let (viewer, document, annotations, page) = try await opened(history: true)
      let square = try await annotations.create(Self.square, on: page)
      try await annotations.threads.setLink(.uri("https://example.com/a"), on: square)
      let ink = try await annotations.create(
        InkAnnotationDraft(inkList: [
          [Point(x: 100, y: 300), Point(x: 150, y: 320), Point(x: 200, y: 300)]
        ]), on: page)
      let bytes = Data([1, 2, 3, 4, 5])
      let file = try await annotations.drafts.createAttachment(
        AnnotationAttachmentFile(bytes: bytes, name: "data.bin"), at: Point(x: 300, y: 300), on: page)
      let history = try await viewer.history(for: document)
      let originals = [square, ink, file]

      let result = try await annotations.duplicate(
        originals, options: AnnotationDuplicateOptions(offset: Point(x: 20, y: 10)))

      expectNoDifference(result.annotations.count, 3)
      expectNoDifference(result.droppedJson, "[]")
      let copies = result.annotations.map(\.annotationRef)
      #expect(Set(copies).isDisjoint(with: originals))
      expectNoDifference(Set(copies).count, 3)
      for (original, copy) in zip(originals, result.annotations) {
        let read = try #require(try await annotations.get(original))
        expectNoDifference(read.record.subtype, copy.subtype)
      }
      let squareCopy = result.annotations[0]
      let squareRect = try #require(try await annotations.get(square)).rect
      expectNoDifference(squareCopy.contents, "Original")
      #expect(abs(squareCopy.rect.x - (squareRect.x + 20)) < 1e-3)
      let link = try await annotations.threads.linkTarget(squareCopy.annotationRef)
      expectNoDifference(link, .uri("https://example.com/a"))
      let copiedFile = try await annotations.drafts.readAttachment(copies[2])
      expectNoDifference(copiedFile, bytes)

      try await history.undo()
      for copy in copies {
        let read = try await annotations.get(copy)
        #expect(read == nil)
      }
      for original in originals {
        let read = try await annotations.get(original)
        #expect(read != nil)
      }
    }

    @Test
    func duplicatingNothingCopiesNothing() async throws {
      let (_, _, annotations, _) = try await opened()
      let result = try await annotations.duplicate([])
      expectNoDifference(result.annotations, [])
    }

    @Test
    func duplicatingAMissingAnnotationIsNotFound() async throws {
      let (_, _, annotations, page) = try await opened()
      let before = try await annotations.list().count
      await #expect {
        _ = try await annotations.duplicate([Self.missing(on: page)])
      } throws: {
        ($0 as? PDFError)?.code == .notFound
      }
      let after = try await annotations.list().count
      expectNoDifference(after, before)
    }

    @Test
    func aLockedAnnotationCannotBeDuplicatedByHand() async throws {
      let (_, _, annotations, page) = try await opened()
      let locked = try await annotations.create(Self.square, on: page)
      try await annotations.update(locked, #"{"locked": true}"#)
      #expect(try await !annotations.canDuplicate([locked]))
      try await annotations.selection.select([locked])
      #expect(try await !annotations.selection.canDuplicate())
    }

    @Test
    func canDuplicateTruthTable() async throws {
      let (_, _, annotations, page) = try await opened()
      let square = try await annotations.create(Self.square, on: page)
      #expect(try await !annotations.canDuplicate([]))
      #expect(try await annotations.canDuplicate([square]))
      #expect(try await !annotations.canDuplicate([square, Self.missing(on: page)]))
      #expect(try await !annotations.canDuplicate([Self.missing(on: page)]))
    }

    @Test
    func aSessionThatMayNotCreateCannotDuplicate() async throws {
      let viewer = try PDFViewer(ViewerConfig(scope: ["doc.open"], interaction: true, annotation: ""))
      try await viewer.start()
      let document = try await viewer.open(Fixture.data("annotation_kinds"))
      let annotations = try await viewer.annotations(for: document)
      let page = try await viewer.pages(of: document)[0].page
      let any = AnnotationRef.objectNumber(page: page, annotObjectNumber: 1)
      #expect(try await !annotations.canDuplicate([any]))
      await #expect {
        _ = try await annotations.duplicate([any])
      } throws: {
        ($0 as? PDFError)?.code == .permissionDenied
      }
      #expect(try await !annotations.selection.canDuplicate())
    }

    @Test
    func selectionDuplicateCopiesTheSelectionAndSelectsTheCopies() async throws {
      let (_, _, annotations, page) = try await opened()
      let square = try await annotations.create(Self.square, on: page)
      let selection = annotations.selection
      try await selection.select([square])
      #expect(try await selection.canDuplicate())
      let result = try await selection.duplicate(offset: Point(x: 0, y: 40))
      expectNoDifference(result.annotations.count, 1)
      let copy = try #require(result.annotations.first)
      #expect(copy.annotationRef != square)
      let squareRect = try #require(try await annotations.get(square)).rect
      #expect(abs(copy.rect.y - (squareRect.y + 40)) < 1e-3)
      let selected = try await selection.refs()
      expectNoDifference(selected, [copy.annotationRef])
    }

    @Test
    func selectionDuplicateWithNothingSelectedIsEmpty() async throws {
      let (_, _, annotations, _) = try await opened()
      let selection = annotations.selection
      #expect(try await !selection.canDuplicate())
      let result = try await selection.duplicate()
      expectNoDifference(result.annotations, [])
    }

    @Test
    func aHighlightIsNotMovableAndASquareIs() async throws {
      let (_, _, annotations, page) = try await opened()
      let square = try await annotations.create(Self.square, on: page)
      let quad = Quad(
        upperLeft: Point(x: 72, y: 100), upperRight: Point(x: 172, y: 100),
        lowerLeft: Point(x: 72, y: 120), lowerRight: Point(x: 172, y: 120))
      let highlight = try await annotations.create(HighlightAnnotationDraft(quadPoints: [quad]), on: page)
      #expect(try await annotations.isMovable(square))
      #expect(try await !annotations.isMovable(highlight))
      #expect(try await !annotations.isMovable(Self.missing(on: page)))
    }

    static func feetPerInch(_ feet: Double) -> PdfMeasurement {
      PdfMeasurement(
        subtype: "rectilinear", ratio: "1 in = \(Int(feet)) ft",
        x: [PdfNumberFormat(unit: "ft", conversion: feet / 72, precision: 100)],
        distance: [PdfNumberFormat(unit: "ft", conversion: 1, precision: 100)],
        area: [PdfNumberFormat(unit: "sq ft", conversion: 1, precision: 100)])
    }

    @Test
    func remeasuringAPageRelabelsItsMeasurements() async throws {
      let (viewer, document, measurement, page) = try await MeasurementTests().opened()
      try await measurement.ensureLoaded(page)
      let json = try await measurement.createMeasurement(
        CreateMeasurementInput(kind: .distance, page: page, points: [Point(x: 10, y: 10), Point(x: 82, y: 10)]))
      let record = try #require(try JSONSerialization.jsonObject(with: Data(json.utf8)) as? [String: Any])
      let line = AnnotationRef.objectNumber(
        page: page, annotObjectNumber: try #require(record["objectNumber"] as? UInt32))
      let annotations = try await viewer.annotations(for: document)
      guard case let .value(before) = try await measurement.readout(line) else {
        Issue.record("expected a readout before the remeasure")
        return
      }

      try await annotations.whenRemeasurable()
      let report = try await annotations.stageRemeasure(on: page, to: Self.feetPerInch(2))

      expectNoDifference(report.error, nil)
      expectNoDifference(report.page, page)
      #expect(report.updated.contains(line))
      guard case let .value(after) = try await measurement.readout(line) else {
        Issue.record("expected a readout after the remeasure")
        return
      }
      #expect(before.label != after.label)
      #expect(after.label.contains("ft"))
      #expect(abs(after.value - 2) < 1e-3)
    }

    /// The scale is encoded before the engine sees it, as a draft's
    /// `measure` is, so a value JSON can't hold throws the encoder's
    /// `invalid-input`.
    @Test
    func aRemeasureScaleJSONCannotHoldIsInvalidInput() async throws {
      let (_, _, annotations, page) = try await opened()
      await #expect {
        _ = try await annotations.stageRemeasure(
          on: page, to: PdfMeasurement(subtype: "rectilinear", cyx: .nan))
      } throws: {
        let error = $0 as? PDFError
        return error?.code == .invalidInput && error?.name == "EncodingError"
      }
    }
  }
}
