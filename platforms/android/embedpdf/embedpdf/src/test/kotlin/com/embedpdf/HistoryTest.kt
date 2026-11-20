package com.embedpdf

import kotlinx.coroutines.flow.toList
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class HistoryTest {
    @Test
    fun undoRedoTracksPageEditsAndDocumentsIndependently() = viewerTest {
        val viewer = started(ViewerConfig(history = true, pageEdit = true))
        val document = viewer.open(Fixture.bytes("report"))
        val other = viewer.open(Fixture.bytes("report"))
        val history = viewer.history(document)
        val otherHistory = viewer.history(other)
        val edit = viewer.pageEdit(document)
        val first = viewer.pages(document)[0].page
        val events = subscribed(history.observeEvents())
        edit.rotate(listOf(first), by = PageEditTurn.CLOCKWISE)
        assertTrue(history.canUndo())
        assertFalse(otherHistory.canUndo())
        val label = history.undoLabel()!!
        assertTrue(label.key.isNotEmpty())
        history.undo()
        events.nextMapped { it as? HistoryEventUndone }
        assertEquals(0u, viewer.pages(document)[0].rotation)
        assertTrue(history.canRedo())
        assertTrue(history.redoLabel() != null)
        history.redo()
        events.nextMapped { it as? HistoryEventUndone }
        assertEquals(90u, viewer.pages(document)[0].rotation)
        viewer.destroy()
    }

    @Test
    fun settingsAndEmptyHistoryDoNotRequireCompletionEvents() = viewerTest {
        val viewer = started(ViewerConfig(history = true))
        val document = viewer.open(Fixture.bytes("report"))
        val history = viewer.history(document)
        history.undo()
        history.redo()
        history.updateSettings(HistorySettingsPatch(limit = 5.0))
        assertEquals(5.0, history.settings().limit, 0.0)
        history.resetSettings()
        assertEquals(100.0, history.settings().limit, 0.0)
        history.clear()
        viewer.close(document)
        expectCode(PdfException.Code.INSTANCE_CLOSED) { history.canUndo() }
        assertEquals(emptyList<HistoryEvent>(), history.observeEvents().toList())
        viewer.destroy()
    }

    @Test
    fun absentHistoryIsUnsupported() = viewerTest {
        val viewer = started()
        val document = viewer.open(Fixture.bytes("report"))
        expectCode(PdfException.Code.UNSUPPORTED) { viewer.history(document) }
        viewer.destroy()
    }
}
