package com.embedpdf.models

import com.embedpdf.AttachmentFileSource
import com.embedpdf.Fixture
import com.embedpdf.HistoryEventUndone
import com.embedpdf.Nullable
import com.embedpdf.NoteAnnotationDraft
import com.embedpdf.OutlineItemInput
import com.embedpdf.Rect
import com.embedpdf.ResourceStatus
import com.embedpdf.ViewerConfig
import com.embedpdf.annotations
import com.embedpdf.attachments
import com.embedpdf.history
import com.embedpdf.nextMapped
import com.embedpdf.outline
import com.embedpdf.started
import com.embedpdf.subscribed
import kotlinx.coroutines.launch
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

class PluginIntegrationTest {
    @get:Rule
    val main = MainThreadRule()

    @Test
    fun threeModelsEditObserveSaveAndCloseTogether() = mainTest {
        val viewer = started(
            ViewerConfig(interaction = true, annotation = "", history = true, outline = true, attachment = true),
        )
        val document = viewer.open(Fixture.bytes("report"))
        val history = viewer.history(document)
        val outline = viewer.outline(document)
        val attachments = viewer.attachments(document)
        val annotations = viewer.annotations(document)
        val historyModel = HistoryModel(viewer, history)
        val outlineModel = OutlineModel(viewer, outline)
        val attachmentModel = AttachmentModel(viewer, attachments)
        val runs = listOf(
            launch { historyModel.run() },
            launch { outlineModel.run() },
            launch { attachmentModel.run() },
        )
        expectObserved(outlineModel.status, ResourceStatus.READY)
        expectObserved(attachmentModel.status, ResourceStatus.READY)
        outline.create(OutlineItemInput(title = "Saved bookmark", clearTarget = true))
        expectObserved({ outlineModel.rows.value.any { it.item.title == "Saved bookmark" } }, true)
        expectObserved(historyModel.canUndo, true)
        val bytes = byteArrayOf(0, -1, 42)
        expectObserved({ attachmentModel.files.value.size }, 1) {
            attachments.add(listOf(AttachmentFileSource(data = bytes, name = "Saved.bin")))
        }
        val page = viewer.pages(document)[0].page
        val note = annotations.create(
            NoteAnnotationDraft(rect = Rect(40.0, 40.0, 24.0, 24.0), contents = Nullable.Value("Saved note")),
            page,
        )
        val undoEvents = subscribed(history.observeEvents())
        historyModel.undo()
        undoEvents.nextMapped { it as? HistoryEventUndone }
        assertNull(annotations.get(note))
        historyModel.redo()
        undoEvents.nextMapped { it as? HistoryEventUndone }
        assertEquals("Saved note", annotations.get(note)?.contents)
        val reopened = viewer.open(viewer.save(document))
        val savedOutline = viewer.outline(reopened)
        val savedFiles = viewer.attachments(reopened)
        savedOutline.ensureLoaded()
        savedFiles.ensureLoaded()
        assertNotNull(savedOutline.list().firstOrNull { it.title == "Saved bookmark" })
        val file = savedFiles.files().first { it.name == "Saved.bin" }
        assertArrayEquals(bytes, savedFiles.download(file).data)
        viewer.close(document)
        runs.forEach { assertTrue(ends(it)) }
        assertFalse(historyModel.canUndo.value)
        assertFalse(outlineModel.canEdit.value)
        assertFalse(attachmentModel.canAdd.value)
        viewer.destroy()
    }
}
