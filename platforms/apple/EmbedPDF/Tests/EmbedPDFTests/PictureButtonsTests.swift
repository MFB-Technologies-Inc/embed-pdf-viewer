import CustomDump
import EmbedPDF
import Foundation
import Testing

/// The 2026-11-27b catch-up's form surface (CU-C8): a field every signer
/// fills in (`FormField.forAnyone`, #1163), and a push button's picture and
/// its widgets' layout (#1165), through the native engine. The same cases as
/// Kotlin's `PictureButtonsTest` and .NET's `PictureButtonsTests`.
extension BaseSuite {
  @Suite
  struct PictureButtonsTests {
    func opened() async throws -> (PDFViewer, DocumentInfo, FormCapability, PageRef) {
      let viewer = try PDFViewer(ViewerConfig(interaction: true, annotation: "", form: FormConfig()))
      try await viewer.start()
      let document = try await viewer.open(Fixture.data("report"))
      let form = try await viewer.form(for: document)
      try await form.refresh()
      let page = try #require(try await viewer.pages(of: document).first).page
      return (viewer, document, form, page)
    }

    func field(_ family: FormFieldFamily, _ name: String, on page: PageRef, y: Double) -> FormCreateFieldInput {
      FormCreateFieldInput(family: family, name: name, page: page, bounds: Rect(x: 40, y: y, width: 90, height: 30))
    }

    func tinyPng() throws -> Data {
      let url = try #require(Bundle.module.url(forResource: "tiny", withExtension: "png", subdirectory: "Fixtures"))
      return try Data(contentsOf: url)
    }

    func expectCode(
      _ code: PDFError.Code, sourceLocation: SourceLocation = #_sourceLocation,
      _ body: @Sendable () async throws -> Void
    ) async {
      await #expect(sourceLocation: sourceLocation) {
        try await body()
      } throws: {
        ($0 as? PDFError)?.code == code
      }
    }

    @Test
    func forAnyoneIsCreatedPatchedAndSaved() async throws {
      let (viewer, document, form, page) = try await opened()
      var input = field(.text, "initials_1", on: page, y: 60)
      input.forAnyone = true
      let initials = try await form.create(input)
      #expect(initials.forAnyone)
      // A field in a group is not also for anyone; a signature field never is.
      var grouped = field(.text, "grouped", on: page, y: 100)
      grouped.forAnyone = true
      grouped.groupId = "dana"
      await expectCode(.invalidInput) { [grouped] in _ = try await form.create(grouped) }
      var signature = field(.signature, "sign_here", on: page, y: 140)
      signature.forAnyone = true
      await expectCode(.invalidInput) { [signature] in _ = try await form.create(signature) }
      let date = try await form.create(field(.text, "date_1", on: page, y: 180))
      #expect(!date.forAnyone)
      let patched = try await form.update(date.fieldRef, FormFieldPatch(forAnyone: true))
      #expect(patched.forAnyone)
      let unpatched = try await form.update(initials.fieldRef, FormFieldPatch(forAnyone: false))
      #expect(!unpatched.forAnyone)
      let reopened = try await viewer.open(try await viewer.save(document))
      let again = try await viewer.form(for: reopened)
      try await again.refresh()
      let dateAgain = try await again.field(date.fieldRef)
      let initialsAgain = try await again.field(initials.fieldRef)
      #expect(dateAgain?.forAnyone == true)
      #expect(initialsAgain?.forAnyone == false)
    }

    @Test
    func aPushButtonsWidgetsKeepTheirLayoutAndAPictureGoesInAndOut() async throws {
      let (viewer, document, form, page) = try await opened()
      var input = field(.pushbutton, "stamp", on: page, y: 60)
      input.layout = .image
      let button = try await form.create(input)
      expectNoDifference(button.detail, .pushbutton(hasImage: false))
      let first = try #require(button.widgets.first?.annotationRef)
      let firstRecord = try await form.widget(first)
      expectNoDifference(firstRecord?.layout, .image)
      // A layout goes on a push button only.
      var caption = field(.text, "caption", on: page, y: 100)
      caption.layout = .image
      await expectCode(.invalidInput) { [caption] in _ = try await form.create(caption) }
      let added = try await form.addWidget(
        to: button.fieldRef,
        FormWidgetPlacement(page: page, bounds: Rect(x: 140, y: 60, width: 90, height: 30), layout: .captionBelow))
      expectNoDifference(added.field.widgets.count, 2)
      let widgets = try await form.widgets(of: button.fieldRef)
      let second = try #require(widgets.first { $0.annotationRef != first })
      expectNoDifference(second.layout, .captionBelow)
      let filled = try await form.setButtonImage(button.fieldRef, image: try tinyPng())
      expectNoDifference(filled.field.detail, .pushbutton(hasImage: true))
      expectNoDifference(
        Set(filled.widgets.compactMap(\.layout)), Set<FormWidgetLayout>([.image, .captionBelow]))
      // The form hears the engine's update.
      let heard = try await form.field(button.fieldRef)
      expectNoDifference(heard?.detail, .pushbutton(hasImage: true))
      let relaid = try await form.updateWidget(second.annotationRef, #"{"layout":"caption-over"}"#)
      expectNoDifference(relaid.layout, .captionOver)
      let saved = try await viewer.save(document)
      let emptied = try await form.setButtonImage(button.fieldRef, image: nil)
      expectNoDifference(emptied.field.detail, .pushbutton(hasImage: false))
      let reopened = try await viewer.open(saved)
      let again = try await viewer.form(for: reopened)
      try await again.refresh()
      let buttonAgain = try await again.field(button.fieldRef)
      expectNoDifference(buttonAgain?.detail, .pushbutton(hasImage: true))
      let secondAgain = try await again.widget(second.annotationRef)
      expectNoDifference(secondAgain?.layout, .captionOver)
    }

    @Test
    func aPictureForAFieldOfAnotherKindIsRefused() async throws {
      let (_, _, form, page) = try await opened()
      let text = try await form.create(field(.text, "name", on: page, y: 60))
      let png = try tinyPng()
      await #expect(throws: PDFError.self) { try await form.setButtonImage(text.fieldRef, image: png) }
      await expectCode(.notFound) { _ = try await form.setButtonImage(.fqn(name: "nowhere"), image: nil) }
    }

    @Test
    func setButtonImageCancelsWhenTheTaskIsCancelled() async throws {
      let (_, _, form, page) = try await opened()
      let button = try await form.create(field(.pushbutton, "stamp", on: page, y: 60))
      let png = try tinyPng()
      await expectCancelledBeforeItStarts { try await form.setButtonImage(button.fieldRef, image: png) }
      let kept = try await form.field(button.fieldRef)
      expectNoDifference(kept?.detail, .pushbutton(hasImage: false))
    }
  }
}
