package com.embedpdf

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The 2026-11-27b catch-up's form surface (CU-C7): a field every signer
 * fills in ([FormField.forAnyone], #1163), and a push button's picture and
 * its widgets' layout (#1165), through the native engine.
 */
class PictureButtonsTest {
    data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val form: FormCapability, val page: PageRef)

    private suspend fun opened(): Opened {
        val viewer = started(ViewerConfig(interaction = true, annotation = "", form = FormConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val form = viewer.form(document)
        form.refresh()
        return Opened(viewer, document, form, viewer.pages(document)[0].page)
    }

    private fun field(family: FormFieldFamily, name: String, page: PageRef, y: Double) =
        FormCreateFieldInput(family, name, page, Rect(40.0, y, 90.0, 30.0))

    @Test
    fun forAnyoneIsCreatedPatchedAndSaved() = viewerTest {
        val (viewer, document, form, page) = opened()
        val initials = form.create(field(FormFieldFamily.TEXT, "initials_1", page, 60.0).copy(forAnyone = true))
        assertTrue(initials.forAnyone)
        // A field in a group is not also for anyone; a signature field never is.
        expectCode(PdfException.Code.INVALID_INPUT) {
            form.create(field(FormFieldFamily.TEXT, "grouped", page, 100.0).copy(forAnyone = true, groupId = "dana"))
        }
        expectCode(PdfException.Code.INVALID_INPUT) {
            form.create(field(FormFieldFamily.SIGNATURE, "sign_here", page, 140.0).copy(forAnyone = true))
        }
        val date = form.create(field(FormFieldFamily.TEXT, "date_1", page, 180.0))
        assertFalse(date.forAnyone)
        assertTrue(form.update(date.fieldRef, FormFieldPatch(forAnyone = true)).forAnyone)
        assertFalse(form.update(initials.fieldRef, FormFieldPatch(forAnyone = false)).forAnyone)
        val reopened = viewer.open(viewer.save(document))
        val again = viewer.form(reopened)
        again.refresh()
        assertEquals(true, again.field(date.fieldRef)?.forAnyone)
        assertEquals(false, again.field(initials.fieldRef)?.forAnyone)
        viewer.destroy()
    }

    @Test
    fun aPushButtonsWidgetsKeepTheirLayoutAndAPictureGoesInAndOut() = viewerTest {
        val (viewer, document, form, page) = opened()
        val button = form.create(field(FormFieldFamily.PUSHBUTTON, "stamp", page, 60.0).copy(layout = FormWidgetLayout.IMAGE))
        assertEquals(FormFieldDetailPushbutton(false), button.detail)
        val first = button.widgets[0].annotationRef!!
        assertEquals(FormWidgetLayout.IMAGE, form.widget(first)?.layout)
        // A layout goes on a push button only.
        expectCode(PdfException.Code.INVALID_INPUT) {
            form.create(field(FormFieldFamily.TEXT, "caption", page, 100.0).copy(layout = FormWidgetLayout.IMAGE))
        }
        val added = form.addWidget(
            button.fieldRef,
            FormWidgetPlacement(page = page, bounds = Rect(140.0, 60.0, 90.0, 30.0), layout = FormWidgetLayout.CAPTION_BELOW),
        )
        assertEquals(2, added.field.widgets.size)
        val second = form.widgets(button.fieldRef).single { it.annotationRef != first }
        assertEquals(FormWidgetLayout.CAPTION_BELOW, second.layout)
        val filled = form.setButtonImage(button.fieldRef, Fixture.bytes("tiny", "png"))
        assertEquals(FormFieldDetailPushbutton(true), filled.field.detail)
        assertEquals(
            setOf(FormWidgetLayout.IMAGE, FormWidgetLayout.CAPTION_BELOW),
            filled.widgets.map { it.layout }.toSet(),
        )
        // The form hears the engine's update.
        assertEquals(FormFieldDetailPushbutton(true), form.field(button.fieldRef)?.detail)
        assertEquals(FormWidgetLayout.CAPTION_OVER, form.updateWidget(second.annotationRef, """{"layout":"caption-over"}""").layout)
        val saved = viewer.save(document)
        assertEquals(FormFieldDetailPushbutton(false), form.setButtonImage(button.fieldRef, null).field.detail)
        val reopened = viewer.open(saved)
        val again = viewer.form(reopened)
        again.refresh()
        assertEquals(FormFieldDetailPushbutton(true), again.field(button.fieldRef)?.detail)
        assertEquals(FormWidgetLayout.CAPTION_OVER, again.widget(second.annotationRef)?.layout)
        viewer.destroy()
    }

    @Test
    fun aPictureForAFieldOfAnotherKindIsRefused() = viewerTest {
        val (viewer, _, form, page) = opened()
        val text = form.create(field(FormFieldFamily.TEXT, "name", page, 60.0))
        assertTrue(runCatching { form.setButtonImage(text.fieldRef, Fixture.bytes("tiny", "png")) }.exceptionOrNull() is PdfException)
        expectCode(PdfException.Code.NOT_FOUND) { form.setButtonImage(FormFieldRefFqn("nowhere"), null) }
        viewer.destroy()
    }
}
