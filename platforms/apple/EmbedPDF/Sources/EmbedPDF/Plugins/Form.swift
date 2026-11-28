public import EmbedPDFBindings
public import Foundation

public typealias FormSettings = EmbedPDFBindings.FormSettings
public typealias FormSettingsPatch = EmbedPDFBindings.FormSettingsPatch
public typealias FormFocusSettings = EmbedPDFBindings.FormFocusSettings
public typealias FormFocusSettingsPatch = EmbedPDFBindings.FormFocusSettingsPatch
public typealias FormFieldSettings = EmbedPDFBindings.FormFieldSettings
public typealias FormFieldSettingsPatch = EmbedPDFBindings.FormFieldSettingsPatch
public typealias FormFillable = EmbedPDFBindings.FormFillable

public typealias FormFieldPanelSettings = EmbedPDFBindings.FormFieldPanelSettings
public typealias FormSettingProperty = EmbedPDFBindings.FormSettingProperty
public typealias FormSettingControl = EmbedPDFBindings.FormSettingControl
public typealias FormValidationResult = EmbedPDFBindings.FormValidationResult
public typealias FormFieldRef = EmbedPDFBindings.FormFieldRef
public typealias FormFieldFamily = EmbedPDFBindings.FormFieldFamily
public typealias FormConfig = EmbedPDFBindings.FormConfig
public typealias FormValidation = EmbedPDFBindings.FormValidation
public typealias FormKind = EmbedPDFBindings.FormKind
public typealias FormSnapshot = EmbedPDFBindings.FormSnapshot
public typealias FormField = EmbedPDFBindings.FormField
/// A text field's or a dropdown's format: a date or a time with its
/// pattern, or `.other` (read, never written).
public typealias FormFieldFormat = EmbedPDFBindings.FormFieldFormat
/// One of the document's groups as people see them: its id, label and the
/// CSS color its fields show in.
public typealias FormGroup = EmbedPDFBindings.FormGroup
/// One of the document's groups as the app names it with
/// `FormCapability.setGroups(_:)`.
public typealias FormGroupInput = EmbedPDFBindings.FormGroupInput
/// Where a radio button comes among its group's buttons in Tab's order.
public typealias FormGroupPosition = EmbedPDFBindings.FormGroupPosition
/// Where UI attaches to one box of a field: its page and where the box
/// stands.
public typealias FormWidgetAnchor = EmbedPDFBindings.FormWidgetAnchor
/// A record's group color and how strongly it shows; never written into
/// the PDF.
public typealias RecordAccent = EmbedPDFBindings.RecordAccent
public typealias FormFieldDetail = EmbedPDFBindings.FormFieldDetail
public typealias FormWidget = EmbedPDFBindings.FormWidget
public typealias FormWidgetRecord = EmbedPDFBindings.FormWidgetRecord
/// How a push button's widget lays out its caption and its picture
/// (`/MK /TP`): the seven layouts of ISO 32000-2 Table 192.
/// `FormWidgetRecord.layout` is `nil` for every other widget.
public typealias FormWidgetLayout = EmbedPDFBindings.FormWidgetLayout
/// What `FormCapability.setButtonImage(_:image:)` wrote: the field now, and
/// the widgets it drew again.
public typealias FormFieldUpdateResult = EmbedPDFBindings.FormFieldUpdateResult
public typealias FormCalculationPosition = EmbedPDFBindings.FormCalculationPosition
public typealias FormToggleWidget = EmbedPDFBindings.FormToggleWidget
public typealias FormChoiceOption = EmbedPDFBindings.FormChoiceOption
public typealias FormChoiceOptionInput = EmbedPDFBindings.FormChoiceOptionInput
public typealias FormValueInput = EmbedPDFBindings.FormValueInput
public typealias FormValueEntry = EmbedPDFBindings.FormValueEntry
/// One widget as a fill control draws it. `frame` is upstream's `box`, the
/// frame the contents are laid out in; `bounds` is where the widget stands
/// on its page, and `rotation` the quarter turn between them.
public typealias FormFillItem = EmbedPDFBindings.FormFillItem
public typealias FormFillControl = EmbedPDFBindings.FormFillControl
/// The widget under a point; `frame` and `rotation` as on `FormFillItem`.
public typealias FormWidgetHit = EmbedPDFBindings.FormWidgetHit
public typealias FormWidgetPlacement = EmbedPDFBindings.FormWidgetPlacement
public typealias FormWidgetAdded = EmbedPDFBindings.FormWidgetAdded
public typealias FormCommitStatus = EmbedPDFBindings.FormCommitStatus
public typealias FormCommitResult = EmbedPDFBindings.FormCommitResult
public typealias FormSetValueStatus = EmbedPDFBindings.FormSetValueStatus
public typealias FormSetValueResult = EmbedPDFBindings.FormSetValueResult
public typealias FormBatchResult = EmbedPDFBindings.FormBatchResult
public typealias FormBatchFailure = EmbedPDFBindings.FormBatchFailure
public typealias FormBatchSkip = EmbedPDFBindings.FormBatchSkip
public typealias FormFilter = EmbedPDFBindings.FormFilter
public typealias FormCreateFieldInput = EmbedPDFBindings.FormCreateFieldInput
public typealias FormFieldPatch = EmbedPDFBindings.FormFieldPatch
public typealias FormFieldPatchKey = EmbedPDFBindings.FormFieldPatchKey
public typealias FormImportResult = EmbedPDFBindings.FormImportResult
public typealias FormDuplicateOptions = EmbedPDFBindings.FormDuplicateOptions
public typealias FormDuplicateResult = EmbedPDFBindings.FormDuplicateResult
public typealias FormActivationResult = EmbedPDFBindings.FormActivationResult
public typealias FormEvent = EmbedPDFBindings.FormEvent
public typealias FormFocusOptions = EmbedPDFBindings.FormFocusOptions
public typealias FormFocusNextOptions = EmbedPDFBindings.FormFocusNextOptions
/// A mounted control's cursor target, lent to
/// `FormCapability.attachFocusTarget(_:target:)`. Both calls come on the
/// viewer's kernel thread: answer at once, and never wait there.
/// `takesCursor()` is asked each time the cursor is about to move; `false`,
/// for a box that holds nothing to focus (a signature field nobody here can
/// sign), makes focus and Tab pass over its field. It answers `true` unless
/// the target says otherwise. `focus(select:)` arranges the cursor move on
/// the main actor and answers `false` when nothing will take it, and the
/// cursor passes over the box.
public typealias FormFocusTarget = EmbedPDFBindings.FormFocusTarget

extension FormFocusTarget {
  /// Every box takes the cursor unless its target says otherwise.
  public func takesCursor() -> Bool { true }
}
public typealias FormWidgetEvent = EmbedPDFBindings.FormWidgetEvent

/// A document's AcroForm. Get one with `PDFViewer.form(for:)`; the viewer
/// needs `ViewerConfig(interaction: true, form: FormConfig())`. It keeps the
/// viewer alive. After its document closes, every call throws
/// `instance-closed`.
public struct FormCapability: Sendable {

  /// Current settings shared by every document using this plugin registration.
  public func settings() async throws(PDFError) -> FormSettings {
    try await bridged { try await handle.settings() }
  }

  /// Merge the supplied fields. Clear flags remove nullable values; omission preserves them.
  public func updateSettings(_ patch: FormSettingsPatch) async throws(PDFError) {
    try await bridged { try await handle.updateSettings(patch: patch) }
  }

  /// Restore the values supplied at registration, preserving registered host resources.
  public func resetSettings() async throws(PDFError) {
    try await bridged { try await handle.resetSettings() }
  }

  public func validate() async throws(PDFError) -> FormValidationResult {
    try await bridged { try await handle.validate() }
  }

  public func canRestoreAttribution() async throws(PDFError) -> Bool {
    try await bridged { try await handle.canRestoreAttribution() }
  }

  public func canWriteScripts() async throws(PDFError) -> Bool {
    try await bridged { try await handle.canWriteScripts() }
  }

  let handle: FormHandle
  let viewer: PDFViewer

  /// Reveal a fillable field and ask its mounted control for the cursor.
  public func focus(_ field: FormFieldRef, options: FormFocusOptions? = nil) async throws(PDFError) -> Bool {
    try await bridged { try await handle.focus(field: field, options: options) }
  }

  public func focusNext(_ options: FormFocusNextOptions? = nil) async throws(PDFError) -> Bool {
    try await bridged { try await handle.focusNext(options: options) }
  }

  public func focusedField() async throws(PDFError) -> FormField? {
    try await bridged { try await handle.focusedField() }
  }

  public func moveInGroup(_ widget: AnnotationRef, backward: Bool = false) async throws(PDFError) -> Bool {
    try await bridged { try await handle.moveInGroup(widget: widget, backward: backward) }
  }

  /// The target runs on the kernel thread. Arrange the control update without waiting there.
  public func attachFocusTarget(_ widget: AnnotationRef, target: any FormFocusTarget) async throws(PDFError) -> Registration {
    Registration(handle: try await bridged { try await handle.attachFocusTarget(widget: widget, target: target) })
  }

  public func notifyWidgetEvent(_ field: FormFieldRef, widget: AnnotationRef, event: FormWidgetEvent) async throws(PDFError) {
    try await bridged { try await handle.notifyWidgetEvent(field: field, widget: widget, event: event) }
  }

  public func snapshot() async throws(PDFError) -> FormSnapshot? {
    try await bridged { try await handle.snapshot() }
  }

  public func status() async throws(PDFError) -> ResourceStatus {
    try await bridged { try await handle.status() }
  }

  public func refresh() async throws(PDFError) {
    try await bridgedCancellable { token in try await handle.refresh(cancel: token) }
  }

  public func field(_ ref: FormFieldRef) async throws(PDFError) -> FormField? {
    try await bridged { try await handle.field(fieldRef: ref) }
  }

  public func field(forWidget widget: AnnotationRef) async throws(PDFError) -> FormField? {
    try await bridged { try await handle.fieldForWidget(widget: widget) }
  }

  public func fields(_ filter: FormFilter? = nil) async throws(PDFError) -> [FormField] {
    try await bridged { try await handle.fields(filter: filter) }
  }

  public func widget(_ ref: AnnotationRef) async throws(PDFError) -> FormWidgetRecord? {
    try await bridged { try await handle.widget(widget: ref) }
  }

  /// The field's widget annotation rows, in the field's order.
  public func widgets(of field: FormFieldRef) async throws(PDFError) -> [FormWidgetRecord] {
    try await bridged { try await handle.fieldWidgets(fieldRef: field) }
  }

  /// The field of the single selected widget in annotation design mode.
  public func selectedField() async throws(PDFError) -> FormField? {
    try await bridged { try await handle.selectedField() }
  }

  @discardableResult
  /// Cancellation ends the wait; Rust still reconciles a change already admitted by the kernel.
  public func reorderCalculations(_ fields: [FormFieldRef], to position: FormCalculationPosition) async throws(PDFError) -> [FormFieldRef?] {
    try await bridgedCancellable { token in try await handle.reorderCalculations(fields: fields, position: position, cancel: token) }
  }

  /// The engine's open FormWidgetPatch JSON payload.
  @discardableResult
  public func updateWidget(_ widget: AnnotationRef, _ patchJSON: String) async throws(PDFError) -> FormWidgetRecord {
    try await bridgedCancellable { token in try await handle.updateWidget(widget: widget, patchJson: patchJSON, cancel: token) }
  }

  /// Delete a widget from its page and field; a detached widget has no remaining field.
  @discardableResult
  public func deleteWidget(_ widget: AnnotationRef) async throws(PDFError) -> FormField? {
    try await bridgedCancellable { token in try await handle.deleteWidget(widget: widget, cancel: token) }
  }

  /// Puts `image` in every widget of a push button, or with `nil` takes it
  /// out (the engine's `doc.forms.setButtonImage`): a PNG, a JPEG or a
  /// one-page PDF, fitted whole inside each widget's border and shown as each
  /// widget's `FormWidgetRecord.layout` says. It goes straight to the engine
  /// as one change, outside the history's undo (`HistoryCapability.undo()`),
  /// so an undo after it steps back over the change before it, not the
  /// picture. The form hears it as an update. It is filling in, gated by the
  /// fill permission or the field's group. Throws `invalid-input` for a field
  /// of another family, `permission-denied` for one a signature locked or the
  /// session may not fill, `not-found` for one the form doesn't have.
  @discardableResult
  public func setButtonImage(_ field: FormFieldRef, image: Data?) async throws(PDFError) -> FormFieldUpdateResult {
    try await bridgedCancellable { token in
      try await handle.setButtonImage(fieldRef: field, image: image, cancel: token)
    }
  }

  public func value(of ref: FormFieldRef) async throws(PDFError) -> FormValueInput? {
    try await bridged { try await handle.value(fieldRef: ref) }
  }

  /// Every value by full name, as plain data, JSON (`exportValues()`).
  public func exportValues() async throws(PDFError) -> String {
    try await bridged { try await handle.exportValues() }
  }

  /// One control per widget on a page: what a native form overlay draws.
  public func widgets(on page: PageRef) async throws(PDFError) -> [FormFillItem] {
    try await bridged { try await handle.widgets(page: page) }
  }

  /// Every page's widgets come with the form: resolves once it is read.
  public func ensureLoaded(_ page: PageRef) async throws(PDFError) {
    try await bridgedCancellable { token in try await handle.ensureLoaded(page: page, cancel: token) }
  }

  public func widget(at point: Point, on page: PageRef) async throws(PDFError) -> FormWidgetHit? {
    try await bridged { try await handle.widgetAt(page: page, x: point.x, y: point.y) }
  }

  public func pageBox(_ page: PageRef) async throws(PDFError) -> Rect? {
    try await bridged { try await handle.pageBox(page: page) }
  }

  /// A text value (`.value`), a checkbox or radio (`.checked`, or `.value`
  /// with a radio's export value) or a choice (`.selectedValues`).
  public func setValue(_ value: FormValueInput, for ref: FormFieldRef) async throws(PDFError)
    -> FormSetValueResult
  {
    try await bridgedCancellable { token in try await handle.setValue(fieldRef: ref, value: value, cancel: token) }
  }

  public func setValues(_ entries: [FormValueEntry]) async throws(PDFError) -> FormBatchResult {
    try await bridgedCancellable { token in try await handle.setValues(entries: entries, cancel: token) }
  }

  /// Plain values by full name, as JSON (what `exportValues()` gives).
  public func importValues(_ valuesJSON: String) async throws(PDFError) -> FormBatchResult {
    try await bridgedCancellable { token in try await handle.importValues(valuesJson: valuesJSON, cancel: token) }
  }

  /// Each field's default, for the fields given, or every field you may
  /// fill in. Resolves the fields it changed.
  @discardableResult
  public func reset(_ fields: [FormFieldRef]? = nil) async throws(PDFError) -> [FormField] {
    try await bridgedCancellable { token in try await handle.reset(fields: fields, cancel: token) }
  }

  /// Family-specific panel descriptors and current values, resolved by Rust.
  public func fieldSettings(_ ref: FormFieldRef) async throws(PDFError) -> FormFieldPanelSettings? {
    try await bridged { try await handle.fieldSettings(fieldRef: ref) }
  }

  /// The engine's FormFieldDraft, including family options and actions/scripts.
  public func create(draftJSON: String) async throws(PDFError) -> FormField {
    try await bridgedCancellable { token in try await handle.createDraft(draftJson: draftJSON, cancel: token) }
  }

  /// The engine's FormFieldPatch. Omission preserves a value; null clears it.
  @discardableResult
  public func update(_ ref: FormFieldRef, patchJSON: String) async throws(PDFError) -> FormField {
    try await bridgedCancellable { token in try await handle.updatePatch(fieldRef: ref, patchJson: patchJSON, cancel: token) }
  }

  public func addWidget(to ref: FormFieldRef, placementJSON: String) async throws(PDFError) -> FormWidgetAdded {
    try await bridgedCancellable { token in try await handle.addWidgetPlacement(fieldRef: ref, placementJson: placementJSON, cancel: token) }
  }

  public func create(_ input: FormCreateFieldInput) async throws(PDFError) -> FormField {
    try await bridgedCancellable { token in try await handle.create(input: input, cancel: token) }
  }

  /// The field in one more place (`addWidget`).
  public func addWidget(to ref: FormFieldRef, _ placement: FormWidgetPlacement) async throws(PDFError)
    -> FormWidgetAdded
  {
    try await bridgedCancellable { token in
      try await handle.addWidget(fieldRef: ref, placement: placement, cancel: token)
    }
  }

  @discardableResult
  public func update(_ ref: FormFieldRef, _ patch: FormFieldPatch) async throws(PDFError) -> FormField {
    try await bridgedCancellable { token in try await handle.update(fieldRef: ref, patch: patch, cancel: token) }
  }

  public func delete(_ ref: FormFieldRef) async throws(PDFError) {
    try await bridgedCancellable { token in try await handle.delete(fieldRef: ref, cancel: token) }
  }

  /// One widget out of a field: it stays on its page, in no field.
  @discardableResult
  public func removeWidget(_ widget: AnnotationRef, from ref: FormFieldRef) async throws(PDFError)
    -> FormField
  {
    try await bridgedCancellable { token in
      try await handle.removeWidget(fieldRef: ref, widget: widget, cancel: token)
    }
  }

  /// Fields out as a bundle (`FormBundle`), JSON: every field, or the ones
  /// named.
  public func export(_ fields: [FormFieldRef]? = nil) async throws(PDFError) -> String {
    try await bridgedCancellable { token in try await handle.export(fields: fields, cancel: token) }
  }

  /// A bundle's fields copied into this document. `optionsJSON` is
  /// `FormImportOptions`.
  public func `import`(_ bundleJSON: String, optionsJSON: String = "{}") async throws(PDFError)
    -> FormImportResult
  {
    try await bridgedCancellable { token in
      try await handle.import(bundleJson: bundleJSON, optionsJson: optionsJSON, cancel: token)
    }
  }

  /// Copy whole fields as one change, one step to undo. Each copy is a new
  /// field named after its original (`email` becomes `email_2`), with its
  /// settings, scripts and widgets. `options` puts the copied widgets on
  /// another page, moves them by an offset in page points (`x` right, `y`
  /// down), or keeps the originals' values; a copy starts empty otherwise.
  ///
  /// Throws `permission-denied` when the session may not change the form,
  /// `not-found` for a field the form doesn't have, and `invalid-input` for
  /// an offset that is not finite. Cancelling the task throws
  /// `operation-cancelled`. A cancel that comes after the engine took the
  /// change still throws, but the copies stay in the form: read the fields
  /// again, or undo.
  public func duplicate(
    _ fields: [FormFieldRef], options: FormDuplicateOptions = FormDuplicateOptions()
  ) async throws(PDFError) -> FormDuplicateResult {
    try await bridgedCancellable { token in
      try await handle.duplicate(fields: fields, options: options, cancel: token)
    }
  }

  /// Copy widgets as a form designer's Duplicate does, as one step to undo:
  /// a radio button's copy joins its group, and any other widget's copy is a
  /// new field. The result lists the new widgets in the order of `widgets`.
  /// `options` is read as `duplicate(_:options:)` reads it.
  ///
  /// Throws `permission-denied` when the session may not change the form,
  /// `not-found` for a widget the form doesn't show, and `invalid-input` for
  /// an offset that is not finite. Cancelling the task throws
  /// `operation-cancelled`. A cancel that comes after the engine took the
  /// change still throws, but the copies stay in the form: read the fields
  /// again, or undo.
  public func duplicateWidgets(
    _ widgets: [AnnotationRef], options: FormDuplicateOptions = FormDuplicateOptions()
  ) async throws(PDFError) -> FormDuplicateResult {
    try await bridgedCancellable { token in
      try await handle.duplicateWidgets(widgets: widgets, options: options, cancel: token)
    }
  }

  /// Fix a form other apps read differently. Resolves the repair's result as
  /// JSON (`FormRepairResult`).
  @discardableResult
  public func repair(bakeAppearances: Bool = false) async throws(PDFError) -> String {
    try await bridgedCancellable { token in try await handle.repair(bakeAppearances: bakeAppearances, cancel: token) }
  }

  /// A tap on a widget: toggles a checkbox, runs a pushbutton's action.
  public func activate(_ widget: AnnotationRef) async throws(PDFError) -> FormActivationResult {
    try await bridgedCancellable { token in try await handle.activateWidget(widget: widget, cancel: token) }
  }

  /// The document's groups: the ones `setGroups(_:)` named, in that order,
  /// then the other groups the form's fields are in.
  public func groups() async throws(PDFError) -> [FormGroup] {
    try await bridged { try await handle.groups() }
  }

  /// Says who this document's groups are, the whole list; an empty list
  /// clears it. Emits `.groupsChanged`; the same list again changes
  /// nothing. Session state: nothing is written into the PDF.
  public func setGroups(_ groups: [FormGroupInput]) async throws(PDFError) {
    try await bridged { try await handle.setGroups(groups: groups) }
  }

  /// The group fields placed with a tool go in, or `nil` for none, where it
  /// starts.
  public func designGroup() async throws(PDFError) -> String? {
    try await bridged { try await handle.designGroup() }
  }

  /// Fields placed with a tool go into `groupID`; `nil` names none. Emits
  /// `.designGroupChanged`. Throws `permission-denied` for a group the
  /// session may not put a field in.
  public func setDesignGroup(_ groupID: String?) async throws(PDFError) {
    try await bridged { try await handle.setDesignGroup(groupId: groupID) }
  }

  /// Whether the session may design the form and put a field in `groupID`.
  public func canSetGroup(_ groupID: String) async throws(PDFError) -> Bool {
    try await bridged { try await handle.canSetGroup(groupId: groupID) }
  }

  /// Puts `fields` in a group, or with `nil` takes them out of theirs, as
  /// one step to undo; answers the fields as they are now, in the order
  /// given. Throws `permission-denied` without `doc.forms.modify` or for a
  /// group the session may not put a field in, `not-found` for a field the
  /// form doesn't have.
  @discardableResult
  public func moveToGroup(_ fields: [FormFieldRef], groupID: String?) async throws(PDFError) -> [FormField] {
    try await bridgedCancellable { token in
      try await handle.moveToGroup(fields: fields, groupId: groupID, cancel: token)
    }
  }

  /// Every field with a selected widget in design mode, in the selection's
  /// order, each once.
  public func selectedFields() async throws(PDFError) -> [FormField] {
    try await bridged { try await handle.selectedFields() }
  }

  /// Where UI attaches to one box of a field; `nil` for a box that isn't
  /// there or shows nowhere.
  public func widgetAnchor(_ widget: AnnotationRef) async throws(PDFError) -> FormWidgetAnchor? {
    try await bridged { try await handle.widgetAnchor(widget: widget) }
  }

  /// Whether the cursor is in this box.
  public func isWidgetFocused(_ widget: AnnotationRef) async throws(PDFError) -> Bool {
    try await bridged { try await handle.isWidgetFocused(widget: widget) }
  }

  public func canRead() async throws(PDFError) -> Bool {
    try await bridged { try await handle.canRead() }
  }

  public func canFill(_ ref: FormFieldRef) async throws(PDFError) -> Bool {
    try await bridged { try await handle.canFill(fieldRef: ref) }
  }

  public func canDesign() async throws(PDFError) -> Bool {
    try await bridged { try await handle.canDesign() }
  }

  /// Confirmed changes. Finishes when the document closes (at once if it
  /// already has) or the viewer is destroyed. Each read is a new subscription.
  public var events: AsyncStream<FormEvent> {
    viewer.registry.stream { handle.subscribe(listener: $0) }
  }
}

extension EventRelay: FormEventListener where Event == FormEvent {
  func onEvent(event: FormEvent) {
    continuation.yield(event)
  }

  func onEnd() {
    continuation.finish()
  }
}

extension PDFViewer {
  public func form(for document: DocumentInfo) async throws(PDFError) -> FormCapability {
    let handle = try await bridged { try await host.form(documentId: document.id) }
    return FormCapability(handle: handle, viewer: self)
  }
}
