package com.embedpdf.models

import com.embedpdf.DocumentInfo
import com.embedpdf.Fixture
import com.embedpdf.HistoryCapability
import com.embedpdf.HistoryChangeLabel
import com.embedpdf.HistoryEventUndoFailed
import com.embedpdf.HistoryEventUndone
import com.embedpdf.HistoryUndoFailure
import com.embedpdf.I18nConfig
import com.embedpdf.PageEditTurn
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import com.embedpdf.PluginErrorInfo
import com.embedpdf.ViewerConfig
import com.embedpdf.history
import com.embedpdf.i18n
import com.embedpdf.pageEdit
import com.embedpdf.started
import kotlinx.coroutines.launch
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

class HistoryModelTest {
    @get:Rule
    val main = MainThreadRule()

    private data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val history: HistoryCapability, val model: HistoryModel)

    private suspend fun opened(): Opened {
        val viewer = started(ViewerConfig(history = true, pageEdit = true))
        val document = viewer.open(Fixture.bytes("report"))
        val history = viewer.history(document)
        return Opened(viewer, document, history, HistoryModel(viewer, history))
    }

    @Test
    fun emptyHistoryNeverLeavesTheControlsSubmitting() = mainTest {
        val (viewer, _, _, model) = opened()
        model.refresh()
        assertFalse(model.canUndo.value)
        assertFalse(model.canRedo.value)
        model.undo()
        model.redo()
        assertFalse(model.submitting.value)
        assertNull(model.error.value)
        assertEquals("Undo", model.undoTitle.value)
        viewer.destroy()
    }

    /** A revision that leaves the history as it was must not notify; an undo still must. */
    @Test
    fun unchangedRevisionDoesNotNotify() = mainTest {
        val (viewer, document, history, model) = opened()
        val page = viewer.pages(document)[0].page
        viewer.pageEdit(document).rotate(listOf(page), PageEditTurn.CLOCKWISE)
        model.refresh()
        assertNotNull(model.undoLabel.value)
        val shown = listOf(model.canUndo, model.canRedo, model.undoLabel, model.redoLabel, model.undoTitle, model.redoTitle)
        assertFalse(anyChanges(shown) { model.refresh() })
        assertTrue(
            changes(model.canRedo) {
                history.undo()
                model.refresh()
            },
        )
        viewer.destroy()
    }

    @Test
    fun externalEditsAndUndoReachTheObservingModel() = mainTest {
        val (viewer, document, _, model) = opened()
        val observing = launch { model.run() }
        val page = viewer.pages(document)[0].page
        expectObserved(model.canUndo, true) { viewer.pageEdit(document).rotate(listOf(page), PageEditTurn.CLOCKWISE) }
        assertNotNull(model.undoLabel.value)
        expectObserved(model.canRedo, true) { model.undo() }
        assertFalse(model.submitting.value)
        observing.cancel()
        observing.join()
        viewer.destroy()
    }

    @Test
    fun closingADocumentStopsObservationAndDoesNotAffectItsReplacement() = mainTest {
        val (viewer, document, _, model) = opened()
        val observing = launch { model.run() }
        viewer.close(document)
        assertTrue(ends(observing))
        assertFalse(model.canUndo.value)
        assertEquals(PdfException.Code.INSTANCE_CLOSED, model.error.value?.code)
        val replacement = viewer.open(Fixture.bytes("report"))
        val next = HistoryModel(viewer, viewer.history(replacement))
        next.refresh()
        assertNull(next.error.value)
        assertFalse(next.canUndo.value)
        viewer.destroy()
    }

    @Test
    fun refusalAndSkippedPartsArePresentedWithoutWaitingForAnotherEvent() = mainTest {
        val (viewer, _, _, model) = opened()
        val label = HistoryChangeLabel(key = "test.edit", values = emptyMap())
        model.receive(
            HistoryEventUndoFailed(
                label, false, HistoryUndoFailure.REFUSED,
                PluginErrorInfo(code = "permission-denied", message = "Editing is not allowed", capability = "history", permission = null),
            ),
        )
        assertEquals(PdfException.Code.PERMISSION_DENIED, model.error.value?.code)
        model.receive(HistoryEventUndone(label, false, 2uL))
        assertEquals(HistoryNotice.SkippedUndo(2), model.notice.value)
        assertEquals("Some changes could not be undone.", model.noticeText.value)
        assertNull(model.error.value)
        model.receive(HistoryEventUndone(label, true, 1uL))
        assertEquals(HistoryNotice.SkippedRedo(1), model.notice.value)
        assertEquals("Some changes could not be redone.", model.noticeText.value)
        viewer.destroy()
    }

    /** Words another language, or resources, supply go into the titles and the notice. */
    private object French : HistoryStrings {
        override val undo: String = "Annuler"
        override val redo: String = "Refaire"
        override val change: String = "modification"

        override fun undoTitle(label: String): String = "Annuler : $label"

        override fun notice(notice: HistoryNotice): String = when (notice) {
            is HistoryNotice.SkippedUndo -> "${notice.count} annulations impossibles"
            is HistoryNotice.SkippedRedo -> "${notice.count} rétablissements impossibles"
        }
    }

    @Test
    fun givenStringsWordTheTitlesAndTheNotice() = mainTest {
        val viewer = started(ViewerConfig(history = true, pageEdit = true, i18n = I18nConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val history = viewer.history(document)
        val model = HistoryModel(viewer, history, viewer.i18n(), French)
        assertEquals("Annuler", model.undoTitle.value)
        assertEquals("Refaire", model.redoTitle.value)
        viewer.pageEdit(document).rotate(listOf(viewer.pages(document)[0].page), PageEditTurn.CLOCKWISE)
        model.refresh()
        // The translator has no text for the change, so the strings' own word stands in.
        assertEquals("Annuler : modification", model.undoTitle.value)
        model.receive(HistoryEventUndone(HistoryChangeLabel(key = "test.edit", values = emptyMap()), false, 3uL))
        assertEquals("3 annulations impossibles", model.noticeText.value)
        viewer.destroy()
    }

    @Test
    fun theEnglishStringsPutTheVerbBeforeTheChange() {
        assertEquals("Undo Rotate pages", HistoryStrings.English.undoTitle("Rotate pages"))
        assertEquals("Redo Rotate pages", HistoryStrings.English.redoTitle("Rotate pages"))
    }
}
