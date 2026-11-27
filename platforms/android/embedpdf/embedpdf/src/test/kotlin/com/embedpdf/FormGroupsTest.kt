package com.embedpdf

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.concurrent.atomic.AtomicInteger

/**
 * The 2026-11-27 catch-up's form surface (CU-C8): a field's purpose and
 * format, the document's groups and the design group, a turned widget's
 * frame, a group's accent on a fill item and on a lent widget, the
 * selection's fields, a widget's anchor and focus, and a focus target that
 * takes no cursor.
 */
class FormGroupsTest {
    data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val form: FormCapability, val page: PageRef)

    private suspend fun opened(): Opened {
        val viewer = started(ViewerConfig(interaction = true, annotation = "", form = FormConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val form = viewer.form(document)
        form.refresh()
        return Opened(viewer, document, form, viewer.pages(document)[0].page)
    }

    private fun text(name: String, page: PageRef, groupId: String? = null) =
        FormCreateFieldInput(FormFieldFamily.TEXT, name, page, Rect(40.0, 60.0, 120.0, 24.0), groupId = groupId)

    class Target(private val takes: Boolean, private val took: Boolean) : FormFocusTarget {
        val focused = AtomicInteger()
        override fun takesCursor(): Boolean = takes
        override fun focus(select: Boolean): Boolean {
            focused.incrementAndGet()
            return took
        }
    }

    @Test
    fun purposeFormatAndGroupAreWrittenReadAndSaved() = viewerTest {
        val (viewer, document, form, page) = opened()
        val field = form.create(
            text("signed_on", page, "dana").copy(purpose = "date-signed", format = FormFieldFormatDate("yyyy-mm-dd")),
        )
        assertEquals("date-signed", field.purpose)
        assertEquals(FormFieldFormatDate("yyyy-mm-dd"), field.format)
        assertEquals("dana", field.groupId)
        expectCode(PdfException.Code.INVALID_INPUT) { form.update(field.fieldRef, FormFieldPatch(format = FormFieldFormatOther)) }
        val updated = form.update(
            field.fieldRef,
            FormFieldPatch(format = FormFieldFormatTime("HH:MM"), clear = listOf(FormFieldPatchKey.PURPOSE)),
        )
        assertNull(updated.purpose)
        assertEquals(FormFieldFormatTime("HH:MM"), updated.format)
        val reopened = viewer.open(viewer.save(document))
        val again = viewer.form(reopened)
        again.refresh()
        val found = again.field(field.fieldRef)!!
        assertEquals(FormFieldFormatTime("HH:MM"), found.format)
        assertEquals("dana", found.groupId)
        assertNull(again.moveToGroup(listOf(field.fieldRef), null).single().groupId)
        viewer.destroy()
    }

    @Test
    fun groupsAreNamedAnnouncedAndShowOnTheirFields() = viewerTest {
        val (viewer, _, form, page) = opened()
        val field = form.create(text("dana_name", page, "dana"))
        assertEquals(listOf(FormGroup("dana", null, "#ea580c")), form.groups())
        val events = subscribed(form.observeEvents())
        form.setGroups(listOf(FormGroupInput("ari", label = "Ari Cole"), FormGroupInput("dana", color = "#0055aa")))
        val groups = listOf(FormGroup("ari", "Ari Cole", "#ea580c"), FormGroup("dana", null, "#0055aa"))
        assertEquals(groups, form.groups())
        assertTrue(form.canSetGroup("ari"))
        form.setDesignGroup("ari")
        assertEquals("ari", form.designGroup())
        assertEquals(groups, events.nextMapped { it as? FormEventGroupsChanged }.groups)
        assertEquals("ari", events.nextMapped { it as? FormEventDesignGroupChanged }.groupId)
        val item = form.widgets(page).single { it.fieldRef == field.fieldRef }
        assertEquals("dana", item.groupId)
        assertTrue(item.canFill && item.empty)
        assertEquals(RecordAccent("#0055aa", 0.16), item.accent)
        form.updateSettings(FormSettingsPatch(fields = FormFieldSettingsPatch(tint = 0.0)))
        assertEquals(0.0, form.settings().fields.tint, 0.0)
        assertNull(form.widgets(page).single { it.fieldRef == field.fieldRef }.accent)
        viewer.destroy()
    }

    @Test
    fun aTurnedWidgetFillsWithItsFrameAndAnchorsWhereItStands() = viewerTest {
        val (viewer, _, form, page) = opened()
        val field = form.create(text("turned", page))
        val widget = field.widgets[0].annotationRef!!
        form.updateWidget(widget, """{"rotation":90}""")
        val item = form.widgets(page).single { it.fieldRef == field.fieldRef }
        assertEquals(90.0, item.rotation, 0.0)
        assertEquals(Pair(120.0, 24.0), Pair(item.frame.width, item.frame.height))
        assertEquals(Pair(24.0, 120.0), Pair(item.bounds.width, item.bounds.height))
        assertEquals(item.bounds, form.widgetAnchor(widget)?.bounds)
        val hit = form.widget(Point(item.bounds.x + 12.0, item.bounds.y + 60.0), page)!!
        assertEquals(Pair(90.0, item.frame), Pair(hit.rotation, hit.frame))
        viewer.destroy()
    }

    @Test
    fun aTargetThatTakesNoCursorIsPassedOverAndAFocusedOneSaysSo() = viewerTest {
        val (viewer, _, form, page) = opened()
        val field = form.create(text("signer_only", page))
        val widget = field.widgets[0].annotationRef!!
        val declining = Target(takes = false, took = true)
        val registration = form.attachFocusTarget(widget, declining)
        assertFalse(form.focus(field.fieldRef))
        assertEquals(0, declining.focused.get())
        registration.unregister()
        form.attachFocusTarget(widget, Target(takes = true, took = true))
        assertTrue(form.focus(field.fieldRef))
        form.notifyWidgetEvent(field.fieldRef, widget, FormWidgetEvent.FOCUS)
        assertTrue(form.isWidgetFocused(widget))
        viewer.destroy()
    }

    @Test
    fun aLentWidgetShowsItsAccentSelectsAsItsFieldAndTurns() = viewerTest {
        val (viewer, document, form, page) = opened()
        val field = form.create(text("lent", page, "dana"))
        val widget = field.widgets[0].annotationRef!!
        form.setGroups(listOf(FormGroupInput("dana", color = "#0055aa")))
        val interaction = viewer.interaction(document)
        val annotations = viewer.annotations(document)
        interaction.activateTool(ToolId.FORM_EDIT)
        annotations.refresh()
        assertEquals(RecordAccent("#0055aa", 0.16), annotations.get(widget)?.accent)
        annotations.selection.select(listOf(widget))
        assertEquals(listOf(field.fieldRef), form.selectedFields().map { it.fieldRef })
        assertTrue(annotations.selection.props().properties.any { it.key == "orientation" })
        annotations.selection.rotate(90.0)
        assertEquals(90.0, form.widgets(page).single { it.fieldRef == field.fieldRef }.rotation, 0.0)
        annotations.selection.resetRotation()
        assertEquals(0.0, form.widgets(page).single { it.fieldRef == field.fieldRef }.rotation, 0.0)
        annotations.drafts.previewPlacement(ToolId("free-text"), Point(100.0, 100.0), Point(300.0, 160.0), page, displayRotation = 90.0)
        annotations.drafts.clearPlacementPreview()
        viewer.destroy()
    }
}
