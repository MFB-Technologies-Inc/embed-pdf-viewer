package com.embedpdf.models

import com.embedpdf.AnnotationAttachmentFile
import com.embedpdf.AttachmentCapability
import com.embedpdf.AttachmentFileSource
import com.embedpdf.AttachmentHomePage
import com.embedpdf.DocumentInfo
import com.embedpdf.Fixture
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import com.embedpdf.Point
import com.embedpdf.ResourceStatus
import com.embedpdf.ViewerConfig
import com.embedpdf.annotations
import com.embedpdf.attachments
import com.embedpdf.started
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import java.io.File

class AttachmentModelTest {
    @get:Rule
    val main = MainThreadRule()

    private data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val attachments: AttachmentCapability, val model: AttachmentModel)

    private suspend fun opened(scope: List<String>? = null): Opened {
        val viewer = started(ViewerConfig(scope = scope, interaction = true, annotation = "", attachment = true))
        val document = viewer.open(Fixture.bytes("report"))
        val attachments = viewer.attachments(document)
        return Opened(viewer, document, attachments, AttachmentModel(viewer, attachments))
    }

    @Test
    fun cancellingInitialObservationDoesNotPoisonTheRetainedModel() = mainTest {
        val (viewer, _, _, model) = opened()
        val cancelled = launch(start = CoroutineStart.UNDISPATCHED) {
            coroutineContext[Job]!!.cancel()
            model.run()
        }
        cancelled.join()
        assertNull(model.error.value)
        val reopened = launch { model.run() }
        expectObserved(model.status, ResourceStatus.READY)
        assertNull(model.error.value)
        viewer.destroy()
        assertTrue(ends(reopened))
    }

    @Test
    fun startingObservationPreservesAFileOperationFailure() = mainTest {
        val (viewer, _, _, model) = opened()
        model.importFiles(listOf(File("/nonexistent/embedpdf-retained-model-file")))
        val failure = checkNotNull(model.error.value)
        val observing = launch { model.run() }
        expectObserved(model.status, ResourceStatus.READY)
        assertEquals(failure, model.error.value)
        viewer.destroy()
        assertTrue(ends(observing))
    }

    /** A revision that leaves the attachments as they were must not notify; a real change still must. */
    @Test
    fun unchangedRevisionDoesNotNotify() = mainTest {
        val (viewer, _, attachments, model) = opened()
        model.reload()
        model.add(listOf(AttachmentFileSource(data = byteArrayOf(1, 2, 3), name = "kept.bin")))
        val file = model.files.value.first { it.name == "kept.bin" }
        model.select(listOf(file))
        val shown = listOf(
            model.files, model.selectedFiles, model.status, model.canAdd, model.canDownload,
            model.canRemoveSelection, model.pendingIds,
        )
        assertFalse(anyChanges(shown) { model.refresh() })
        assertTrue(
            changes(model.files) {
                attachments.add(listOf(AttachmentFileSource(data = byteArrayOf(4), name = "added.bin")))
                model.refresh()
            },
        )
        assertNull(model.error.value)
        viewer.destroy()
    }

    @Test
    fun importDownloadAndBothHomesUseOwnedBytes() = mainTest {
        val (viewer, document, _, model) = opened()
        model.reload()
        val bytes = byteArrayOf(0, -1, 13, 10)
        model.add(
            listOf(
                AttachmentFileSource(data = bytes, name = "same.bin"),
                AttachmentFileSource(data = byteArrayOf(), name = "same.bin"),
            ),
        )
        assertEquals(2, model.files.value.size)
        assertNotEquals(model.files.value[0].id, model.files.value[1].id)
        val file = model.files.value.first { it.name == "same.bin" }
        model.select(listOf(file))
        assertEquals(listOf(file.id), model.selectedFiles.value.map { it.id })
        assertTrue(model.canRemoveSelection.value)
        val content = checkNotNull(model.download(file))
        assertArrayEquals(bytes, content.data)
        assertNull(content.mimeType)
        val temporary = File.createTempFile("embedpdf-import", ".bin")
        try {
            temporary.writeBytes(bytes)
            model.importFiles(listOf(temporary))
            assertNull(model.error.value)
            val imported = model.files.value.first { it.name == temporary.name }
            assertArrayEquals(bytes, checkNotNull(model.download(imported)).data)
        } finally {
            temporary.delete()
        }
        val page = viewer.pages(document)[0].page
        viewer.annotations(document).drafts.createAttachment(
            AnnotationAttachmentFile(bytes = bytes, name = "page.bin"),
            Point(40.0, 40.0),
            page,
        )
        model.refresh()
        val pageFile = model.files.value.first { it.home is AttachmentHomePage }
        model.reveal(pageFile)
        assertNull(model.error.value)
        model.remove(file)
        assertTrue(model.files.value.none { it.id == file.id })
        viewer.destroy()
    }

    @Test
    fun independentModelsObserveImportAndClose() = mainTest {
        val (viewer, document, attachments, first) = opened()
        val second = AttachmentModel(viewer, attachments)
        val one = launch { first.run() }
        val two = launch { second.run() }
        expectObserved(first.status, ResourceStatus.READY)
        expectObserved(second.status, ResourceStatus.READY)
        attachments.add(listOf(AttachmentFileSource(data = byteArrayOf(42), name = "External")))
        expectObserved({ first.files.value.size }, 1)
        expectObserved({ second.files.value.size }, 1)
        viewer.close(document)
        assertTrue(ends(one))
        assertTrue(ends(two))
        assertEquals(0, first.files.value.size)
        assertFalse(second.canDownload.value)
        val replacement = viewer.open(Fixture.bytes("report"))
        val next = AttachmentModel(viewer, viewer.attachments(replacement))
        next.reload()
        assertNull(next.error.value)
        assertEquals(0, next.files.value.size)
        viewer.destroy()
    }

    @Test
    fun fileErrorsCancellationAndPermissionsAreVisible() = mainTest {
        val (viewer, _, _, model) = opened()
        model.importFiles(listOf(File("/nonexistent/embedpdf-parity-file")))
        assertNotNull(model.error.value)
        assertFalse(model.performing.value)
        val cancelled = launch(start = CoroutineStart.UNDISPATCHED) {
            model.importFiles(listOf(Fixture.file("report")))
        }
        cancelled.cancel()
        cancelled.join()
        assertEquals(PdfException.Code.OPERATION_CANCELLED, model.error.value?.code)
        assertFalse(model.performing.value)
        viewer.destroy()
        val (restricted, _, _, denied) = opened(scope = listOf("doc.open"))
        denied.reload()
        assertFalse(denied.canAdd.value)
        denied.add(listOf(AttachmentFileSource(data = byteArrayOf(), name = "Denied")))
        assertEquals(PdfException.Code.PERMISSION_DENIED, denied.error.value?.code)
        restricted.destroy()
    }

    @Test
    fun aCancellationOfAFileOperationIsNotAFailure() = mainTest {
        val (viewer, _, _, model) = opened()
        model.fileOperationFailed(kotlinx.coroutines.CancellationException("picker closed"))
        assertNull(model.error.value)
        model.fileOperationFailed(java.io.IOException("disk full"))
        assertEquals(PdfException.Code.OPERATION_FAILED, model.error.value?.code)
        viewer.destroy()
    }
}
