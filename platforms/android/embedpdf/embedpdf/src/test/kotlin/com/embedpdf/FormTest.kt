package com.embedpdf

import kotlinx.coroutines.delay
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.concurrent.CopyOnWriteArrayList

class FormTest {
    data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val form: FormCapability)

    private suspend fun opened(fixture: String = "form_fields"): Opened {
        val viewer = started(ViewerConfig(interaction = true, form = FormConfig(validation = FormValidation.NONE)))
        val document = viewer.open(Fixture.bytes(fixture))
        val form = viewer.form(document)
        form.refresh()
        return Opened(viewer, document, form)
    }

    private suspend fun writableText(form: FormCapability): FormField =
        form.fields().first { it.detail is FormFieldDetailText && !it.readOnly }

    class CursorTarget : FormFocusTarget {
        val selections = CopyOnWriteArrayList<Boolean>()

        override fun takesCursor(): Boolean = true

        override fun focus(select: Boolean): Boolean {
            selections.add(select)
            return true
        }
    }

    @Test
    fun aMountedControlReceivesFocusAndUnregisters() = viewerTest {
        val (viewer, _, form) = opened()
        val field = writableText(form)
        val widget = field.widgets.first { it.annotationRef != null }.annotationRef!!
        val target = CursorTarget()
        val registration = form.attachFocusTarget(widget, target)
        val events = subscribed(form.observeEvents())
        assertTrue(form.focus(field.fieldRef, FormFocusOptions(select = true)))
        assertEquals(listOf(true), target.selections.toList())
        form.notifyWidgetEvent(field.fieldRef, widget, FormWidgetEvent.FOCUS)
        assertEquals(field.fieldRef, form.focusedField()?.fieldRef)
        val changed = events.nextMapped { it as? FormEventFocusChanged }
        assertEquals(field.fieldRef, changed.field?.fieldRef)
        assertEquals(widget, changed.widget)
        registration.unregister()
        assertFalse(form.focus(field.fieldRef))
        viewer.destroy()
    }

    @Test
    fun fieldsListWithTypedDetail() = viewerTest {
        val (viewer, _, form) = opened()
        assertEquals(ResourceStatus.READY, form.status())
        assertTrue(form.fields().isNotEmpty())
        assertNotNull(form.snapshot())
        viewer.destroy()
    }

    @Test
    fun aTextFieldFillsAndIsObserved() = viewerTest {
        val (viewer, _, form) = opened()
        val field = writableText(form)
        val events = subscribed(form.observeEvents())
        val result = form.setValue(FormValueInputValue("From Kotlin"), field.fieldRef)
        assertEquals(FormSetValueStatus.APPLIED, result.status)
        val changed = events.nextMapped { it as? FormEventValueChanged }
        assertEquals(field.fieldRef, changed.field.fieldRef)
        assertEquals(FormValueInputValue("From Kotlin"), form.value(field.fieldRef))
        viewer.destroy()
    }

    @Test
    fun aCheckboxIsCheckedByItsExportValue() = viewerTest {
        val (viewer, _, form) = opened()
        val box = form.fields().first { it.detail is FormFieldDetailCheckbox && !it.readOnly }
        val exportValue = box.widgets.first().toggle!!.exportValue
        // form_fields.pdf's box starts checked; clear it so checking is a change.
        assertEquals(FormSetValueStatus.APPLIED, form.setValue(FormValueInputValue(null), box.fieldRef).status)
        assertEquals(FormSetValueStatus.APPLIED, form.setValue(FormValueInputValue(exportValue), box.fieldRef).status)
        val read = form.field(box.fieldRef)!!
        assertTrue((read.detail as FormFieldDetailCheckbox).checked)
        viewer.destroy()
    }

    @Test
    fun exportedValuesRoundTrip() = viewerTest {
        val (viewer, _, form) = opened()
        val field = writableText(form)
        form.setValue(FormValueInputValue("Round trip"), field.fieldRef)
        val all = Json.parseToJsonElement(form.exportValues()).jsonObject
        // The whole export also names `inline.direct`, a field with no object
        // number, whose write is refused `not-found` and fails the batch, as
        // the TypeScript engine refuses it.
        assertEquals("Round trip", all.getValue(field.name).jsonPrimitive.content)
        val values = JsonObject(mapOf(field.name to all.getValue(field.name))).toString()
        val second = viewer.open(Fixture.bytes("form_fields"))
        val fresh = viewer.form(second)
        fresh.refresh()
        val imported = fresh.importValues(values)
        assertTrue(imported.applied.contains(field.fieldRef))
        assertEquals(emptyList<FormBatchFailure>(), imported.failed)
        assertEquals("Round trip", (fresh.field(field.fieldRef)!!.detail as FormFieldDetailText).value)
        viewer.destroy()
    }

    @Test
    fun aWidgetIsFoundByItsFieldAndPage() = viewerTest {
        val (viewer, _, form) = opened()
        val field = writableText(form)
        val widget = field.widgets.first { it.annotationRef != null && it.page != null }
        val page = widget.page!!
        form.ensureLoaded(page)
        assertEquals(field.fieldRef, form.field(widget.annotationRef!!)?.fieldRef)
        assertEquals(widget.annotationRef, form.widget(widget.annotationRef!!)?.annotationRef)
        assertTrue(form.widgets(field.fieldRef).any { it.annotationRef == widget.annotationRef })
        assertTrue(form.widgets(page).isNotEmpty())
        assertNotNull(form.pageBox(page))
        assertTrue(form.canRead())
        assertTrue(form.canFill(field.fieldRef))
        viewer.destroy()
    }

    @Test
    fun aClosedDocumentsCapabilityThrows() = viewerTest {
        val (viewer, document, form) = opened()
        viewer.close(document)
        val error = expectCode(PdfException.Code.INSTANCE_CLOSED) { form.fields() }
        assertEquals("PluginError", error.name)
        viewer.destroy()
    }

    @Test
    fun droppingTheFlowUnsubscribes() = viewerTest {
        val (viewer, _, form) = opened()
        val collector = collecting(form.observeEvents())
        delay(20)
        collector.cancel()
        collector.join()
        form.refresh()
        viewer.destroy()
    }

    @Test
    fun theFlowCompletesWhenTheDocumentCloses() = viewerTest {
        val (viewer, document, form) = opened()
        val collector = collecting(form.observeEvents())
        viewer.close(document)
        assertTrue(finishes(collector))
        viewer.destroy()
    }

    // One per member that takes a `CancelToken`. Each is called in an
    // already-cancelled coroutine.

    private data class Cancellable(val form: FormCapability, val field: FormField, val widget: AnnotationRef, val page: PageRef)

    /** A writable text field, its first widget, and that widget's page. */
    private suspend fun cancellable(): Cancellable {
        val form = opened().form
        val field = writableText(form)
        val widget = field.widgets.first()
        return Cancellable(form, field, widget.annotationRef!!, widget.page!!)
    }

    @Test
    fun refreshAndExportCancelWhenTheirCoroutineIsCancelled() = viewerTest {
        val (form, _, _, _) = cancellable()
        expectCancelledBeforeItStarts { form.refresh() }
        expectCancelledBeforeItStarts { form.export() }
        form.export()
        form.viewer.destroy()
    }

    @Test
    fun setValueCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (form, field, _, _) = cancellable()
        expectCancelledBeforeItStarts { form.setValue(FormValueInputValue("x"), field.fieldRef) }
        assertNotEquals(FormValueInputValue("x"), form.value(field.fieldRef))
        expectCancelledBeforeItStarts { form.setValue(FormValueInputValue(null), field.fieldRef) }
        expectCancelledBeforeItStarts { form.setValue(FormValueInputChecked(false), field.fieldRef) }
        expectCancelledBeforeItStarts { form.setValue(FormValueInputSelectedValues(listOf("a")), field.fieldRef) }
        expectCancelledBeforeItStarts { form.setValues(listOf(FormValueEntry(field.fieldRef, FormValueInputValue("x")))) }
        expectCancelledBeforeItStarts { form.importValues(JsonObject(mapOf(field.name to JsonPrimitive("x"))).toString()) }
        assertNotEquals(FormValueInputValue("x"), form.value(field.fieldRef))
        form.viewer.destroy()
    }

    @Test
    fun resetCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (form, field, _, _) = cancellable()
        expectCancelledBeforeItStarts { form.reset(listOf(field.fieldRef)) }
        expectCancelledBeforeItStarts { form.reset() }
        form.viewer.destroy()
    }

    @Test
    fun fieldChangesCancelWhenTheirCoroutineIsCancelled() = viewerTest {
        val (form, field, widget, page) = cancellable()
        val before = form.fields().size
        expectCancelledBeforeItStarts {
            form.create(FormCreateFieldInput(FormFieldFamily.TEXT, "fromKotlin", page, Rect(10.0, 10.0, 80.0, 20.0)))
        }
        assertEquals(before, form.fields().size)
        expectCancelledBeforeItStarts { form.update(field.fieldRef, FormFieldPatch(readOnly = true)) }
        expectCancelledBeforeItStarts { form.delete(field.fieldRef) }
        assertNotNull(form.field(field.fieldRef))
        expectCancelledBeforeItStarts { form.removeWidget(widget, field.fieldRef) }
        expectCancelledBeforeItStarts { form.addWidget(field.fieldRef, FormWidgetPlacement(page, Rect(10.0, 10.0, 80.0, 20.0))) }
        assertEquals(field.widgets.size, form.field(field.fieldRef)!!.widgets.size)
        assertFalse(form.field(field.fieldRef)!!.readOnly)
        form.viewer.destroy()
    }

    @Test
    fun bundleRepairAndActivationCancelWhenTheirCoroutineIsCancelled() = viewerTest {
        val (form, _, widget, _) = cancellable()
        val bundle = form.export()
        expectCancelledBeforeItStarts { form.import(bundle) }
        expectCancelledBeforeItStarts { form.repair() }
        expectCancelledBeforeItStarts { form.activate(widget) }
        form.viewer.destroy()
    }

    @Test
    fun formFieldDuplicationMakesANamedCopy() = viewerTest {
        val (viewer, _, form) = opened()
        val original = writableText(form)
        val before = form.fields().size
        val result = form.duplicate(listOf(original.fieldRef), FormDuplicateOptions(offset = Point(0.0, 30.0)))
        val copy = result.fields.single()
        assertNotEquals(original.fieldRef, copy.fieldRef)
        assertTrue(copy.name.startsWith(original.name))
        assertNotEquals(original.name, copy.name)
        assertEquals(original.widgets.size, result.widgets.size)
        assertEquals(before + 1, form.fields().size)
        assertEquals("[]", result.droppedJson)
        viewer.destroy()
    }

    @Test
    fun formWidgetDuplicationMakesANewField() = viewerTest {
        val (viewer, _, form) = opened()
        val original = writableText(form)
        val widget = original.widgets.first { it.annotationRef != null }.annotationRef!!
        val result = form.duplicateWidgets(listOf(widget))
        val copy = result.widgets.single()
        assertNotEquals(widget, copy)
        val field = result.fields.single()
        assertNotEquals(original.fieldRef, field.fieldRef)
        assertTrue(field.widgets.any { it.annotationRef == copy })
        viewer.destroy()
    }

    @Test
    fun duplicatingAFieldTheFormDoesNotHaveIsNotFound() = viewerTest {
        val (viewer, _, form) = opened()
        expectCode(PdfException.Code.NOT_FOUND) { form.duplicate(listOf(FormFieldRefFqn("no such field"))) }
        viewer.destroy()
    }

    @Test
    fun aDuplicateOffsetThatIsNotFiniteIsInvalidInput() = viewerTest {
        val (viewer, _, form) = opened()
        val original = writableText(form)
        val before = form.fields().size
        val options = FormDuplicateOptions(offset = Point(Double.NEGATIVE_INFINITY, 0.0))
        expectCode(PdfException.Code.INVALID_INPUT) { form.duplicate(listOf(original.fieldRef), options) }
        val widget = original.widgets.first { it.annotationRef != null }.annotationRef!!
        expectCode(PdfException.Code.INVALID_INPUT) { form.duplicateWidgets(listOf(widget), options) }
        assertEquals(before, form.fields().size)
        viewer.destroy()
    }

    // Cross-plugin: a SubmitForm node reaches the Kotlin submit handler
    // through the form plugin's resolver. Swift's JavaScriptCore case is the
    // javascript module's, where Robolectric has no sandbox.

    class RecordingSubmit : ActionSubmitHandler {
        val urls = CopyOnWriteArrayList<String?>()

        override fun submit(request: ActionSubmitRequest) {
            urls.add(request.url)
        }
    }

    @Test
    fun aSubmitReachesTheKotlinHandler() = viewerTest {
        val viewer = started(
            ViewerConfig(interaction = true, form = FormConfig(), actions = ActionsConfig.forCurrentTimeZone(openSequence = OpenSequence.OFF)),
        )
        val document = viewer.open(Fixture.bytes("form_fields"))
        val actions = viewer.actions(document)
        val submit = RecordingSubmit()
        actions.setSubmitHandler(submit)
        val tree = PdfActionTree.decode(
            """{"root":{"type":"submit-form","subtype":"SubmitForm","payload":{"url":"https://home.test/submit","fields":null,"flags":{"raw":4,"exclude":false,"includeNoValueFields":false,"format":"html","method":"post","submitCoordinates":false,"includeAppendSaves":false,"includeAnnotations":false,"canonicalFormat":false,"exclNonUserAnnots":false,"exclFKey":false,"embedForm":false}},"next":[]},"incomplete":false,"warningFlags":0,"warnings":[]}""",
        )
        actions.execute(tree, ActionContext(origin = ActionOrigin.USER, source = ActionSourceApi, event = ActionTriggerEventActivate))
        assertEquals(listOf<String?>("https://home.test/submit"), submit.urls.toList())
        viewer.destroy()
    }
}
