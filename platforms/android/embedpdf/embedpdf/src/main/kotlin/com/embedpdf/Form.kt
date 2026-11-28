package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_form.FormEventListener
import uniffi.epdf_plugin_form.FormHandle

public typealias FormSettings = uniffi.epdf_plugin_form.FormSettings
public typealias FormSettingsPatch = uniffi.epdf_plugin_form.FormSettingsPatch
public typealias FormFocusSettings = uniffi.epdf_plugin_form.FormFocusSettings
public typealias FormFocusSettingsPatch = uniffi.epdf_plugin_form.FormFocusSettingsPatch
public typealias FormFieldSettings = uniffi.epdf_plugin_form.FormFieldSettings
public typealias FormFieldSettingsPatch = uniffi.epdf_plugin_form.FormFieldSettingsPatch
public typealias FormFillable = uniffi.epdf_plugin_form.FormFillable
public typealias FormFillableAll = uniffi.epdf_plugin_form.FormFillable.All
public typealias FormFillableNone = uniffi.epdf_plugin_form.FormFillable.None
public typealias FormFillableKinds = uniffi.epdf_plugin_form.FormFillable.Kinds

public typealias FormFieldPanelSettings = uniffi.epdf_plugin_form.FormFieldPanelSettings
public typealias FormSettingProperty = uniffi.epdf_plugin_form.FormSettingProperty
public typealias FormSettingControl = uniffi.epdf_plugin_form.FormSettingControl
public typealias FormValidationResult = uniffi.epdf_plugin_form.FormValidationResult
public typealias FormFieldRef = uniffi.epdf_ffi.FormFieldRef
public typealias FormFieldRefObjectNumber = uniffi.epdf_ffi.FormFieldRef.ObjectNumber
public typealias FormFieldRefFqn = uniffi.epdf_ffi.FormFieldRef.Fqn
public typealias FormFieldFamily = uniffi.epdf_ffi.FormFieldFamily
public typealias FormConfig = uniffi.epdf_plugin_form.FormConfig
public typealias FormValidation = uniffi.epdf_plugin_form.FormValidation
public typealias FormKind = uniffi.epdf_plugin_form.FormKind
public typealias FormSnapshot = uniffi.epdf_plugin_form.FormSnapshot
public typealias FormField = uniffi.epdf_plugin_form.FormField

/** A text field's or a dropdown's format: a date or a time with its pattern, or [FormFieldFormatOther] (read, never written). */
public typealias FormFieldFormat = uniffi.epdf_plugin_form.FormFieldFormat
public typealias FormFieldFormatDate = uniffi.epdf_plugin_form.FormFieldFormat.Date
public typealias FormFieldFormatTime = uniffi.epdf_plugin_form.FormFieldFormat.Time
public typealias FormFieldFormatOther = uniffi.epdf_plugin_form.FormFieldFormat.Other

/** One of the document's groups as people see them: its id, label and the CSS color its fields show in. */
public typealias FormGroup = uniffi.epdf_plugin_form.FormGroup

/** One of the document's groups as the app names it with [FormCapability.setGroups]. */
public typealias FormGroupInput = uniffi.epdf_plugin_form.FormGroupInput

/** Where a radio button comes among its group's buttons in Tab's order. */
public typealias FormGroupPosition = uniffi.epdf_plugin_form.FormGroupPosition

/** Where UI attaches to one box of a field: its page and where the box stands. */
public typealias FormWidgetAnchor = uniffi.epdf_plugin_form.FormWidgetAnchor

/** A record's group color and how strongly it shows; never written into the PDF. */
public typealias RecordAccent = uniffi.epdf_ffi.RecordAccent
public typealias FormFieldDetail = uniffi.epdf_plugin_form.FormFieldDetail
public typealias FormFieldDetailText = uniffi.epdf_plugin_form.FormFieldDetail.Text
public typealias FormFieldDetailCheckbox = uniffi.epdf_plugin_form.FormFieldDetail.Checkbox
public typealias FormFieldDetailRadio = uniffi.epdf_plugin_form.FormFieldDetail.Radio
public typealias FormFieldDetailCombobox = uniffi.epdf_plugin_form.FormFieldDetail.Combobox
public typealias FormFieldDetailListbox = uniffi.epdf_plugin_form.FormFieldDetail.Listbox
public typealias FormFieldDetailPushbutton = uniffi.epdf_plugin_form.FormFieldDetail.Pushbutton
public typealias FormFieldDetailSignature = uniffi.epdf_plugin_form.FormFieldDetail.Signature
public typealias FormFieldDetailUnknown = uniffi.epdf_plugin_form.FormFieldDetail.Unknown
public typealias FormWidget = uniffi.epdf_plugin_form.FormWidget
public typealias FormWidgetRecord = uniffi.epdf_plugin_form.FormWidgetRecord

/**
 * How a push button's widget lays out its caption and its picture (`/MK /TP`): the seven layouts
 * of ISO 32000-2 Table 192. [FormWidgetRecord.layout] is `null` for every other widget.
 */
public typealias FormWidgetLayout = uniffi.epdf_plugin_form.FormWidgetLayout

/** What [FormCapability.setButtonImage] wrote: the field now, and the widgets it drew again. */
public typealias FormFieldUpdateResult = uniffi.epdf_plugin_form.FormFieldUpdateResult
public typealias FormCalculationPosition = uniffi.epdf_plugin_form.FormCalculationPosition
public typealias FormCalculationPositionStart = uniffi.epdf_plugin_form.FormCalculationPosition.Start
public typealias FormCalculationPositionEnd = uniffi.epdf_plugin_form.FormCalculationPosition.End
public typealias FormCalculationPositionBefore = uniffi.epdf_plugin_form.FormCalculationPosition.Before
public typealias FormCalculationPositionAfter = uniffi.epdf_plugin_form.FormCalculationPosition.After
public typealias FormToggleWidget = uniffi.epdf_plugin_form.FormToggleWidget
public typealias FormChoiceOption = uniffi.epdf_plugin_form.FormChoiceOption
public typealias FormChoiceOptionInput = uniffi.epdf_plugin_form.FormChoiceOptionInput
public typealias FormValueInput = uniffi.epdf_plugin_form.FormValueInput
public typealias FormValueInputValue = uniffi.epdf_plugin_form.FormValueInput.Value
public typealias FormValueInputChecked = uniffi.epdf_plugin_form.FormValueInput.Checked
public typealias FormValueInputSelectedValues = uniffi.epdf_plugin_form.FormValueInput.SelectedValues
public typealias FormValueEntry = uniffi.epdf_plugin_form.FormValueEntry
public typealias FormFillItem = uniffi.epdf_plugin_form.FormFillItem
public typealias FormFillControl = uniffi.epdf_plugin_form.FormFillControl
public typealias FormFillControlText = uniffi.epdf_plugin_form.FormFillControl.Text
public typealias FormFillControlToggle = uniffi.epdf_plugin_form.FormFillControl.Toggle
public typealias FormFillControlChoice = uniffi.epdf_plugin_form.FormFillControl.Choice
public typealias FormFillControlButton = uniffi.epdf_plugin_form.FormFillControl.Button
public typealias FormFillControlSignature = uniffi.epdf_plugin_form.FormFillControl.Signature
public typealias FormWidgetHit = uniffi.epdf_plugin_form.FormWidgetHit
public typealias FormWidgetPlacement = uniffi.epdf_plugin_form.FormWidgetPlacement
public typealias FormWidgetAdded = uniffi.epdf_plugin_form.FormWidgetAdded
public typealias FormCommitStatus = uniffi.epdf_plugin_form.FormCommitStatus
public typealias FormCommitResult = uniffi.epdf_plugin_form.FormCommitResult
public typealias FormSetValueStatus = uniffi.epdf_plugin_form.FormSetValueStatus
public typealias FormSetValueResult = uniffi.epdf_plugin_form.FormSetValueResult
public typealias FormBatchResult = uniffi.epdf_plugin_form.FormBatchResult
public typealias FormBatchFailure = uniffi.epdf_plugin_form.FormBatchFailure
public typealias FormBatchSkip = uniffi.epdf_plugin_form.FormBatchSkip
public typealias FormFilter = uniffi.epdf_plugin_form.FormFilter
public typealias FormCreateFieldInput = uniffi.epdf_plugin_form.FormCreateFieldInput
public typealias FormFieldPatch = uniffi.epdf_plugin_form.FormFieldPatch
public typealias FormFieldPatchKey = uniffi.epdf_plugin_form.FormFieldPatchKey
public typealias FormImportResult = uniffi.epdf_plugin_form.FormImportResult
public typealias FormDuplicateOptions = uniffi.epdf_plugin_form.FormDuplicateOptions
public typealias FormDuplicateResult = uniffi.epdf_plugin_form.FormDuplicateResult
public typealias FormActivationResult = uniffi.epdf_plugin_form.FormActivationResult
public typealias FormActivationResultForm = uniffi.epdf_plugin_form.FormActivationResult.Form
public typealias FormActivationResultDispatched = uniffi.epdf_plugin_form.FormActivationResult.Dispatched
public typealias FormEvent = uniffi.epdf_plugin_form.FormEvent
public typealias FormEventFocusChanged = uniffi.epdf_plugin_form.FormEvent.FocusChanged
public typealias FormEventValueChanged = uniffi.epdf_plugin_form.FormEvent.ValueChanged
public typealias FormEventFieldCreated = uniffi.epdf_plugin_form.FormEvent.FieldCreated
public typealias FormEventFieldUpdated = uniffi.epdf_plugin_form.FormEvent.FieldUpdated
public typealias FormEventFieldDeleted = uniffi.epdf_plugin_form.FormEvent.FieldDeleted
public typealias FormEventValidationRejected = uniffi.epdf_plugin_form.FormEvent.ValidationRejected
public typealias FormEventResynced = uniffi.epdf_plugin_form.FormEvent.Resynced
public typealias FormEventDesignGroupChanged = uniffi.epdf_plugin_form.FormEvent.DesignGroupChanged
public typealias FormEventGroupsChanged = uniffi.epdf_plugin_form.FormEvent.GroupsChanged
public typealias FormFocusOptions = uniffi.epdf_plugin_form.FormFocusOptions
public typealias FormFocusNextOptions = uniffi.epdf_plugin_form.FormFocusNextOptions

/**
 * A mounted control's cursor target, lent to [FormCapability.attachFocusTarget].
 * Both calls come on the viewer's kernel thread: answer at once, and never
 * wait there. `takesCursor()` is asked each time the cursor is about to
 * move; `false`, for a box that holds nothing to focus (a signature field
 * nobody here can sign), makes focus and Tab pass over its field.
 * `focus(select)` posts to the main thread to move the cursor and answers
 * `false` when nothing will take it, and the cursor passes over the box.
 */
public typealias FormFocusTarget = uniffi.epdf_plugin_form.FormFocusTarget
public typealias FormWidgetEvent = uniffi.epdf_plugin_form.FormWidgetEvent

/**
 * A document's AcroForm. Get one with [PdfViewer.form]; the viewer needs
 * `ViewerConfig(interaction = true, form = FormConfig())`. It keeps the
 * viewer alive. After its document closes, every call throws
 * `instance-closed`.
 *
 * Each change that started (a value, a field, a widget, an import, a repair)
 * lands even when the caller is cancelled, and the caller then sees its
 * cancellation rather than the result (see the README's Cancellation
 * section).
 */
public class FormCapability internal constructor(
    private val handle: FormHandle,
    internal val viewer: PdfViewer,
) {
    /** Current settings shared by every document using this plugin registration. */
    public suspend fun settings(): FormSettings = viewer.call { handle.settings() }

    /** Merges the supplied fields. Clear flags remove nullable values; omission preserves them. */
    public suspend fun updateSettings(patch: FormSettingsPatch) {
        viewer.call { handle.updateSettings(patch) }
    }

    /** Restores the values supplied at registration, preserving registered host resources. */
    public suspend fun resetSettings() {
        viewer.call { handle.resetSettings() }
    }

    public suspend fun validate(): FormValidationResult = viewer.call { handle.validate() }

    public suspend fun canRestoreAttribution(): Boolean = viewer.call { handle.canRestoreAttribution() }

    public suspend fun canWriteScripts(): Boolean = viewer.call { handle.canWriteScripts() }

    /** Reveals a fillable field and asks its mounted control for the cursor. */
    public suspend fun focus(field: FormFieldRef, options: FormFocusOptions? = null): Boolean =
        viewer.call { handle.focus(field, options) }

    public suspend fun focusNext(options: FormFocusNextOptions? = null): Boolean = viewer.call { handle.focusNext(options) }

    public suspend fun focusedField(): FormField? = viewer.call { handle.focusedField() }

    public suspend fun moveInGroup(widget: AnnotationRef, backward: Boolean = false): Boolean =
        viewer.call { handle.moveInGroup(widget, backward) }

    /**
     * Lends [widget]'s mounted control to the form, which calls [target] when
     * the field takes focus. [target] runs on the kernel thread: arrange the
     * control update without waiting there. The form keeps it until the
     * registration is unregistered or the document closes.
     */
    public suspend fun attachFocusTarget(widget: AnnotationRef, target: FormFocusTarget): Registration =
        Registration(viewer.call { handle.attachFocusTarget(widget, target) })

    public suspend fun notifyWidgetEvent(field: FormFieldRef, widget: AnnotationRef, event: FormWidgetEvent) {
        viewer.call { handle.notifyWidgetEvent(field, widget, event) }
    }

    public suspend fun snapshot(): FormSnapshot? = viewer.call { handle.snapshot() }

    public suspend fun status(): ResourceStatus = viewer.call { handle.status() }

    public suspend fun refresh() {
        viewer.callCancellable { token -> handle.refresh(token) }
    }

    public suspend fun field(ref: FormFieldRef): FormField? = viewer.call { handle.field(ref) }

    /** The field [widget] belongs to. */
    public suspend fun field(widget: AnnotationRef): FormField? = viewer.call { handle.fieldForWidget(widget) }

    public suspend fun fields(filter: FormFilter? = null): List<FormField> = viewer.call { handle.fields(filter) }

    public suspend fun widget(ref: AnnotationRef): FormWidgetRecord? = viewer.call { handle.widget(ref) }

    /** The field's widget annotation rows, in the field's order. */
    public suspend fun widgets(field: FormFieldRef): List<FormWidgetRecord> = viewer.call { handle.fieldWidgets(field) }

    /** The field of the single selected widget in annotation design mode. */
    public suspend fun selectedField(): FormField? = viewer.call { handle.selectedField() }

    /**
     * Moves [fields] to [position] in the calculation order and answers the
     * new order.
     */
    public suspend fun reorderCalculations(fields: List<FormFieldRef>, position: FormCalculationPosition): List<FormFieldRef?> =
        viewer.callCancellable { token -> handle.reorderCalculations(fields, position, token) }

    /** [patchJson] is the engine's open `FormWidgetPatch` JSON payload. */
    public suspend fun updateWidget(widget: AnnotationRef, patchJson: String): FormWidgetRecord =
        viewer.callCancellable { token -> handle.updateWidget(widget, patchJson, token) }

    /**
     * Deletes a widget from its page and field; a detached widget has no
     * remaining field.
     */
    public suspend fun deleteWidget(widget: AnnotationRef): FormField? =
        viewer.callCancellable { token -> handle.deleteWidget(widget, token) }

    /**
     * Puts [image] in every widget of a push button, or with `null` takes it out (the engine's
     * `doc.forms.setButtonImage`): a PNG, a JPEG or a one-page PDF, fitted whole inside each
     * widget's border and shown as each widget's [FormWidgetRecord.layout] says. It goes straight
     * to the engine as one change, outside the history's undo ([HistoryCapability.undo]), so an
     * undo after it steps back over the change before it, not the picture. The form hears it as
     * an update. It is filling in, gated by the fill permission or the field's group. Refused with
     * `invalid-input` for a field of another family, `permission-denied` for one a signature locked
     * or the session may not fill, `not-found` for one the form doesn't have.
     */
    public suspend fun setButtonImage(field: FormFieldRef, image: ByteArray?): FormFieldUpdateResult =
        viewer.callCancellable { token -> handle.setButtonImage(field, image, token) }

    public suspend fun value(ref: FormFieldRef): FormValueInput? = viewer.call { handle.value(ref) }

    /** Every value by full name, as plain data, JSON (`exportValues()`). */
    public suspend fun exportValues(): String = viewer.call { handle.exportValues() }

    /** One control per widget on a page: what a native form overlay draws. */
    public suspend fun widgets(page: PageRef): List<FormFillItem> = viewer.call { handle.widgets(page) }

    /** Every page's widgets come with the form: resolves once it is read. */
    public suspend fun ensureLoaded(page: PageRef) {
        viewer.callCancellable { token -> handle.ensureLoaded(page, token) }
    }

    public suspend fun widget(point: Point, page: PageRef): FormWidgetHit? = viewer.call { handle.widgetAt(page, point.x, point.y) }

    public suspend fun pageBox(page: PageRef): Rect? = viewer.call { handle.pageBox(page) }

    /**
     * A text value ([FormValueInputValue]), a checkbox or radio
     * ([FormValueInputChecked], or [FormValueInputValue] with a radio's export
     * value) or a choice ([FormValueInputSelectedValues]).
     */
    public suspend fun setValue(value: FormValueInput, ref: FormFieldRef): FormSetValueResult =
        viewer.callCancellable { token -> handle.setValue(ref, value, token) }

    public suspend fun setValues(entries: List<FormValueEntry>): FormBatchResult =
        viewer.callCancellable { token -> handle.setValues(entries, token) }

    /** Plain values by full name, as JSON (what [exportValues] gives). */
    public suspend fun importValues(valuesJson: String): FormBatchResult =
        viewer.callCancellable { token -> handle.importValues(valuesJson, token) }

    /**
     * Each field's default, for the [fields] given, or every field you may
     * fill in. Resolves the fields it changed.
     */
    public suspend fun reset(fields: List<FormFieldRef>? = null): List<FormField> =
        viewer.callCancellable { token -> handle.reset(fields, token) }

    /** Family-specific panel descriptors and current values, resolved by Rust. */
    public suspend fun fieldSettings(ref: FormFieldRef): FormFieldPanelSettings? = viewer.call { handle.fieldSettings(ref) }

    /**
     * [draftJson] is the engine's `FormFieldDraft`, including family options
     * and actions or scripts: the way to set what [FormCreateFieldInput]
     * doesn't cover.
     */
    public suspend fun create(draftJson: String): FormField =
        viewer.callCancellable { token -> handle.createDraft(draftJson, token) }

    /**
     * [patchJson] is the engine's `FormFieldPatch`. Omission preserves a
     * value; `null` clears it.
     */
    public suspend fun update(ref: FormFieldRef, patchJson: String): FormField =
        viewer.callCancellable { token -> handle.updatePatch(ref, patchJson, token) }

    /** [placementJson] is the engine's widget placement, with its widget options and actions. */
    public suspend fun addWidget(field: FormFieldRef, placementJson: String): FormWidgetAdded =
        viewer.callCancellable { token -> handle.addWidgetPlacement(field, placementJson, token) }

    public suspend fun create(input: FormCreateFieldInput): FormField =
        viewer.callCancellable { token -> handle.create(input, token) }

    /** The field in one more place (`addWidget`). */
    public suspend fun addWidget(field: FormFieldRef, placement: FormWidgetPlacement): FormWidgetAdded =
        viewer.callCancellable { token -> handle.addWidget(field, placement, token) }

    public suspend fun update(ref: FormFieldRef, patch: FormFieldPatch): FormField =
        viewer.callCancellable { token -> handle.update(ref, patch, token) }

    public suspend fun delete(ref: FormFieldRef) {
        viewer.callCancellable { token -> handle.delete(ref, token) }
    }

    /** One widget out of a field: it stays on its page, in no field. */
    public suspend fun removeWidget(widget: AnnotationRef, field: FormFieldRef): FormField =
        viewer.callCancellable { token -> handle.removeWidget(field, widget, token) }

    /** Fields out as a bundle (`FormBundle`), JSON: every field, or the ones named. */
    public suspend fun export(fields: List<FormFieldRef>? = null): String =
        viewer.callCancellable { token -> handle.export(fields, token) }

    /** A bundle's fields copied into this document. [optionsJson] is `FormImportOptions`. */
    public suspend fun import(bundleJson: String, optionsJson: String = "{}"): FormImportResult =
        viewer.callCancellable { token -> handle.import(bundleJson, optionsJson, token) }

    /**
     * Copies whole fields, each a new field with its original's settings,
     * scripts, default value and widgets, named after it with the next free
     * number (`email` becomes `email_2`). A copy has no value unless
     * [FormDuplicateOptions.values] is set, and a signature never comes
     * along. [FormDuplicateOptions.offset] moves each copied widget, in page
     * points (`x` to the right, `y` down). One step to undo.
     *
     * Throws `permission-denied` without `doc.forms.modify`, `not-found` for
     * a field the form doesn't have, and `invalid-input` for an offset that
     * is NaN or an infinity. A copy that started lands even when the caller
     * is cancelled, as every change here does.
     */
    public suspend fun duplicate(fields: List<FormFieldRef>, options: FormDuplicateOptions = FormDuplicateOptions()): FormDuplicateResult =
        viewer.callCancellable { token -> handle.duplicate(fields, options, token) }

    /**
     * Copies widgets as Duplicate does in a form designer. A radio button's
     * copy is another button of its group; any other widget's copy is a new
     * field, and widgets of one field copied together become one new field.
     * The result's widgets are in the order of [widgets]. One step to undo.
     *
     * Throws `permission-denied` without `doc.forms.modify`, `not-found` for
     * a widget the form doesn't show, and `invalid-input` for an offset that
     * is NaN or an infinity. A copy that started lands even when the caller
     * is cancelled, as every change here does.
     */
    public suspend fun duplicateWidgets(
        widgets: List<AnnotationRef>,
        options: FormDuplicateOptions = FormDuplicateOptions(),
    ): FormDuplicateResult = viewer.callCancellable { token -> handle.duplicateWidgets(widgets, options, token) }

    /**
     * Fixes a form other apps read differently. Resolves the repair's result
     * as JSON (`FormRepairResult`).
     */
    public suspend fun repair(bakeAppearances: Boolean = false): String =
        viewer.callCancellable { token -> handle.repair(bakeAppearances, token) }

    /** A tap on a widget: toggles a checkbox, runs a pushbutton's action. */
    public suspend fun activate(widget: AnnotationRef): FormActivationResult =
        viewer.callCancellable { token -> handle.activateWidget(widget, token) }

    /**
     * The document's groups: the ones [setGroups] named, in that order, then
     * the other groups the form's fields are in.
     */
    public suspend fun groups(): List<FormGroup> = viewer.call { handle.groups() }

    /**
     * Says who this document's groups are, the whole list; an empty list
     * clears it. Emits [FormEventGroupsChanged]; the same list again changes
     * nothing. Session state: nothing is written into the PDF.
     */
    public suspend fun setGroups(groups: List<FormGroupInput>) {
        viewer.call { handle.setGroups(groups) }
    }

    /** The group fields placed with a tool go in, or `null` for none, where it starts. */
    public suspend fun designGroup(): String? = viewer.call { handle.designGroup() }

    /**
     * Fields placed with a tool go into [groupId]; `null` names none. Emits
     * [FormEventDesignGroupChanged]. Throws `permission-denied` for a group
     * the session may not put a field in.
     */
    public suspend fun setDesignGroup(groupId: String?) {
        viewer.call { handle.setDesignGroup(groupId) }
    }

    /** Whether the session may design the form and put a field in [groupId]. */
    public suspend fun canSetGroup(groupId: String): Boolean = viewer.call { handle.canSetGroup(groupId) }

    /**
     * Puts [fields] in a group, or with `null` takes them out of theirs, as
     * one step to undo; answers the fields as they are now, in the order
     * given. Throws `permission-denied` without `doc.forms.modify` or for a
     * group the session may not put a field in, `not-found` for a field the
     * form doesn't have.
     */
    public suspend fun moveToGroup(fields: List<FormFieldRef>, groupId: String?): List<FormField> =
        viewer.callCancellable { token -> handle.moveToGroup(fields, groupId, token) }

    /** Every field with a selected widget in design mode, in the selection's order, each once. */
    public suspend fun selectedFields(): List<FormField> = viewer.call { handle.selectedFields() }

    /** Where UI attaches to one box of a field; `null` for a box that isn't there or shows nowhere. */
    public suspend fun widgetAnchor(widget: AnnotationRef): FormWidgetAnchor? = viewer.call { handle.widgetAnchor(widget) }

    /** Whether the cursor is in this box. */
    public suspend fun isWidgetFocused(widget: AnnotationRef): Boolean = viewer.call { handle.isWidgetFocused(widget) }

    public suspend fun canRead(): Boolean = viewer.call { handle.canRead() }

    public suspend fun canFill(ref: FormFieldRef): Boolean = viewer.call { handle.canFill(ref) }

    public suspend fun canDesign(): Boolean = viewer.call { handle.canDesign() }

    /**
     * Confirmed changes. Completes when the document closes (at once if it
     * already has) or the viewer is destroyed. Each collection is a new
     * subscription.
     */
    public fun observeEvents(): Flow<FormEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : FormEventListener {
                override fun onEvent(event: FormEvent) {
                    channel.trySend(event)
                }

                override fun onEnd() {
                    channel.close()
                }
            },
        )
    }
}

/**
 * The form capability for [document]. Throws `unsupported` when the viewer was
 * built without the form plugin, whatever the document's state.
 */
public suspend fun PdfViewer.form(document: DocumentInfo): FormCapability =
    FormCapability(call { host.form(document.id) }, this)
