import CustomDump
import EmbedPDF
import Foundation
import Testing

/// The 2026-11-27 catch-up's form surface (CU-C9): a field's purpose and
/// format, the document's groups and the design group, a turned widget's
/// frame, a group's accent on a fill item and on a lent widget, the
/// selection's fields, a widget's anchor and focus, and a focus target that
/// takes no cursor. The same cases as Kotlin's `FormGroupsTest` and .NET's
/// `FormGroupsTests`.
extension BaseSuite {
  @Suite
  struct FormGroupsTests {
    func opened() async throws -> (PDFViewer, DocumentInfo, FormCapability, PageRef) {
      let viewer = try PDFViewer(ViewerConfig(interaction: true, annotation: "", form: FormConfig()))
      try await viewer.start()
      let document = try await viewer.open(Fixture.data("report"))
      let form = try await viewer.form(for: document)
      try await form.refresh()
      let page = try #require(try await viewer.pages(of: document).first).page
      return (viewer, document, form, page)
    }

    func text(_ name: String, on page: PageRef, groupID: String? = nil) -> FormCreateFieldInput {
      FormCreateFieldInput(
        family: .text, name: name, page: page, bounds: Rect(x: 40, y: 60, width: 120, height: 24), groupId: groupID)
    }

    final class Target: FormFocusTarget, Sendable {
      let takes: Bool
      let took: Bool
      let focused = LockIsolated(0)
      init(takes: Bool, took: Bool) {
        self.takes = takes
        self.took = took
      }
      func takesCursor() -> Bool { takes }
      func focus(select: Bool) -> Bool {
        focused.withValue { $0 += 1 }
        return took
      }
    }

    /// A target that writes only `focus(select:)` takes the cursor: the
    /// facade's default answers `takesCursor()`.
    final class FocusOnly: FormFocusTarget, Sendable {
      func focus(select: Bool) -> Bool { true }
    }

    @Test
    func purposeFormatAndGroupAreWrittenReadAndSaved() async throws {
      let (viewer, document, form, page) = try await opened()
      var input = text("signed_on", on: page, groupID: "dana")
      input.purpose = "date-signed"
      input.format = .date(pattern: "yyyy-mm-dd")
      let field = try await form.create(input)
      expectNoDifference(field.purpose, "date-signed")
      expectNoDifference(field.format, .date(pattern: "yyyy-mm-dd"))
      expectNoDifference(field.groupId, "dana")
      await #expect {
        try await form.update(field.fieldRef, FormFieldPatch(format: .other))
      } throws: {
        ($0 as? PDFError)?.code == .invalidInput
      }
      let updated = try await form.update(
        field.fieldRef, FormFieldPatch(format: .time(pattern: "HH:MM"), clear: [.purpose]))
      #expect(updated.purpose == nil)
      expectNoDifference(updated.format, .time(pattern: "HH:MM"))
      let reopened = try await viewer.open(try await viewer.save(document))
      let again = try await viewer.form(for: reopened)
      try await again.refresh()
      let found = try #require(try await again.field(field.fieldRef))
      expectNoDifference(found.format, .time(pattern: "HH:MM"))
      expectNoDifference(found.groupId, "dana")
      let moved = try await again.moveToGroup([field.fieldRef], groupID: nil)
      try #require(moved.count == 1)
      #expect(moved[0].groupId == nil)
    }

    @Test
    func groupsAreNamedAnnouncedAndShowOnTheirFields() async throws {
      let (_, _, form, page) = try await opened()
      let field = try await form.create(text("dana_name", on: page, groupID: "dana"))
      let first = try await form.groups()
      expectNoDifference(first, [FormGroup(id: "dana", label: nil, color: "#ea580c")])
      let events = form.events
      try await form.setGroups([FormGroupInput(id: "ari", label: "Ari Cole"), FormGroupInput(id: "dana", color: "#0055aa")])
      let groups = [
        FormGroup(id: "ari", label: "Ari Cole", color: "#ea580c"), FormGroup(id: "dana", label: nil, color: "#0055aa"),
      ]
      let named = try await form.groups()
      expectNoDifference(named, groups)
      #expect(try await form.canSetGroup("ari"))
      try await form.setDesignGroup("ari")
      let design = try await form.designGroup()
      expectNoDifference(design, "ari")
      var announced: [FormGroup]?
      var designGroup: String??
      for await event in events {
        switch event {
        case let .groupsChanged(changed): announced = changed
        case let .designGroupChanged(groupID): designGroup = .some(groupID)
        default: break
        }
        if announced != nil, designGroup != nil { break }
      }
      expectNoDifference(announced, groups)
      expectNoDifference(designGroup, .some("ari"))
      let item = try #require(try await form.widgets(on: page).first { $0.fieldRef == field.fieldRef })
      expectNoDifference(item.groupId, "dana")
      #expect(item.canFill && item.empty)
      expectNoDifference(item.accent, RecordAccent(color: "#0055aa", strength: 0.16))
      try await form.updateSettings(FormSettingsPatch(fields: FormFieldSettingsPatch(tint: 0)))
      let tint = try await form.settings().fields.tint
      expectNoDifference(tint, 0)
      let untinted = try #require(try await form.widgets(on: page).first { $0.fieldRef == field.fieldRef })
      #expect(untinted.accent == nil)
    }

    @Test
    func aTurnedWidgetFillsWithItsFrameAndAnchorsWhereItStands() async throws {
      let (_, _, form, page) = try await opened()
      let field = try await form.create(text("turned", on: page))
      let widget = try #require(field.widgets.first?.annotationRef)
      try await form.updateWidget(widget, #"{"rotation":90}"#)
      let item = try #require(try await form.widgets(on: page).first { $0.fieldRef == field.fieldRef })
      expectNoDifference(item.rotation, 90)
      expectNoDifference([item.frame.width, item.frame.height], [120, 24])
      expectNoDifference([item.bounds.width, item.bounds.height], [24, 120])
      let anchor = try await form.widgetAnchor(widget)
      expectNoDifference(anchor, FormWidgetAnchor(page: page, bounds: item.bounds))
      let hit = try #require(
        try await form.widget(at: Point(x: item.bounds.x + 12, y: item.bounds.y + 60), on: page))
      expectNoDifference(hit.rotation, 90)
      expectNoDifference(hit.frame, item.frame)
    }

    @Test
    func aTargetThatTakesNoCursorIsPassedOverAndAFocusedOneSaysSo() async throws {
      let (_, _, form, page) = try await opened()
      let field = try await form.create(text("signer_only", on: page))
      let widget = try #require(field.widgets.first?.annotationRef)
      let declining = Target(takes: false, took: true)
      let registration = try await form.attachFocusTarget(widget, target: declining)
      #expect(try await !form.focus(field.fieldRef))
      expectNoDifference(declining.focused.value, 0)
      registration.unregister()
      let refusing = Target(takes: true, took: false)
      let refused = try await form.attachFocusTarget(widget, target: refusing)
      #expect(try await !form.focus(field.fieldRef))
      expectNoDifference(refusing.focused.value, 1)
      refused.unregister()
      _ = try await form.attachFocusTarget(widget, target: FocusOnly())
      #expect(try await form.focus(field.fieldRef))
      try await form.notifyWidgetEvent(field.fieldRef, widget: widget, event: .focus)
      #expect(try await form.isWidgetFocused(widget))
    }

    @Test
    func aLentWidgetShowsItsAccentSelectsAsItsFieldAndTurns() async throws {
      let (viewer, document, form, page) = try await opened()
      let field = try await form.create(text("lent", on: page, groupID: "dana"))
      let widget = try #require(field.widgets.first?.annotationRef)
      try await form.setGroups([FormGroupInput(id: "dana", color: "#0055aa")])
      let interaction = try await viewer.interaction(for: document)
      let annotations = try await viewer.annotations(for: document)
      try await interaction.activateTool(.formEdit)
      try await annotations.refresh()
      let lent: AnnotationInfo? = try await annotations.get(widget)
      expectNoDifference(lent?.accent, RecordAccent(color: "#0055aa", strength: 0.16))
      try await annotations.selection.select([widget])
      let selected = try await form.selectedFields()
      expectNoDifference(selected.map(\.fieldRef), [field.fieldRef])
      #expect(try await annotations.selection.props().properties.contains { $0.key == "orientation" })
      try await annotations.selection.rotate(by: 90)
      let turned = try #require(try await form.widgets(on: page).first { $0.fieldRef == field.fieldRef })
      expectNoDifference(turned.rotation, 90)
      try await annotations.selection.resetRotation()
      let reset = try #require(try await form.widgets(on: page).first { $0.fieldRef == field.fieldRef })
      expectNoDifference(reset.rotation, 0)
      try await annotations.drafts.previewPlacement(
        tool: ToolID("free-text"), from: Point(x: 100, y: 100), to: Point(x: 300, y: 160), on: page,
        displayRotation: 90)
      try await annotations.drafts.clearPlacementPreview()
    }
  }
}
