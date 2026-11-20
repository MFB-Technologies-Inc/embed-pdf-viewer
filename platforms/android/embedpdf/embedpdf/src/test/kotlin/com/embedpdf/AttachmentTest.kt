package com.embedpdf

import kotlinx.coroutines.flow.toList
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class AttachmentTest {
    @Test
    fun batchImportDownloadsAndReopensWithDistinctUnicodeNames() = viewerTest {
        val viewer = started(ViewerConfig(attachment = true))
        val document = viewer.open(Fixture.bytes("report"))
        val attachments = viewer.attachments(document)
        attachments.ensureLoaded()
        val data = byteArrayOf(0, 255.toByte(), 128.toByte(), 13, 10)
        val added = attachments.add(
            listOf(
                AttachmentFileSource(data = data, name = "Résumé.bin"),
                AttachmentFileSource(data = ByteArray(0), name = "Résumé.bin"),
            ),
        )
        assertEquals(listOf("Résumé.bin", "Résumé (2).bin"), added.map { it.name })
        assertNotEquals(added[1].id, added[0].id)
        val content = attachments.download(added[0])
        assertArrayEquals(data, content.data)
        assertNull(content.mimeType)
        attachments.select(added)
        assertEquals(added.map { it.id }, attachments.selectedFiles().map { it.id })
        assertEquals(emptyList<AttachedFile>(), attachments.add(emptyList()))
        expectCancelledBeforeItStarts { attachments.download(added[0]) }
        expectCancelledBeforeItStarts { attachments.add(listOf(AttachmentFileSource(data = data, name = "cancelled"))) }
        assertTrue(attachments.canAdd())
        assertTrue(attachments.canDownload())
        assertTrue(attachments.canRemove(added[0]))
        val bytes = viewer.save(document)
        val reopened = viewer.open(bytes)
        val saved = viewer.attachments(reopened)
        saved.ensureLoaded()
        val savedFile = saved.files().first { it.name == "Résumé.bin" }
        assertArrayEquals(data, saved.download(savedFile).data)
        attachments.remove(added[0])
        assertNull(attachments.file(added[0].id))
        viewer.close(document)
        assertEquals(emptyList<AttachmentEvent>(), attachments.observeEvents().toList())
        viewer.destroy()
    }

    // The Swift suite's page attachment case creates its attachment through
    // the annotation capability, which arrives with wave D.

    @Test
    fun aPageAttachmentDownloadsAndRevealsThroughTheAnnotationCapability() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, annotation = "", attachment = true))
        val document = viewer.open(Fixture.bytes("report"))
        val annotations = viewer.annotations(document)
        val page = viewer.pages(document)[0].page
        val data = "attached".toByteArray()
        val ref = annotations.drafts.createAttachment(AnnotationAttachmentFile(data, "page.txt"), Point(40.0, 40.0), page)
        assertArrayEquals(data, annotations.drafts.readAttachment(ref))
        val attachments = viewer.attachments(document)
        attachments.ensureLoaded()
        val file = attachments.files().first { it.home is AttachmentHomePage }
        assertArrayEquals(data, attachments.download(file).data)
        attachments.reveal(file)
        assertTrue(annotations.selected().any { it.annotationRef == ref })
        viewer.destroy()
    }
}
