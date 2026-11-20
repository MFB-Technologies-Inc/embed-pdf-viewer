package com.embedpdf

import kotlinx.coroutines.withTimeout
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Test

class DocumentApiTest {
    @Test
    fun documentControlsUseTheCurrentKernelRegistry() = viewerTest {
        val viewer = started(ViewerConfig(metadata = true))
        val first = viewer.open(Fixture.bytes("report"))
        val second = viewer.open(Fixture.bytes("report"), activate = false)
        assertEquals(first.id, viewer.activeDocumentId())
        viewer.rename(first, "")
        assertEquals("", viewer.document(first.id)?.name)
        viewer.setActive(second)
        val page = viewer.page(at = 0)!!
        assertEquals(page, viewer.page(page.page))
        assertEquals(0, viewer.index(of = page.page))
        assertEquals(0L, viewer.pageRevision())
        viewer.move(second, toIndex = 0)
        assertEquals(listOf(second.id, first.id), viewer.documentOrder())
        viewer.swap(first, second)
        assertEquals(listOf(first.id, second.id), viewer.documentOrder())
        viewer.setDocumentOrder(listOf(second.id, first.id))

        viewer.metadata(first).update(MetadataPatch(title = "Saved layer title"))
        val bytes = viewer.saveLayer(first)
        val restored = viewer.open(base = Fixture.bytes("report"), layer = bytes)
        assertEquals(14u, restored.pageCount)
        val restoredMetadata = viewer.metadata(restored)
        restoredMetadata.ensureLoaded()
        assertEquals("Saved layer title", restoredMetadata.snapshot()?.title)

        viewer.close(second)
        assertNull(viewer.document(second.id))
        assertNull(viewer.page(page.page, second))
        assertEquals(-1L, viewer.pageRevision(second))
        viewer.closeAll()
        assertEquals(0, viewer.documentCount())
        assertNull(viewer.activeDocument())
        viewer.destroy()
    }

    @Test
    fun settingsPreserveOmissionsAndClearWithoutGrantingDownload() = viewerTest {
        val viewer = started(ViewerConfig(scope = listOf("doc.open")))
        val initial = viewer.settings()
        val revisions = subscribed(viewer.observeStateRevisions())
        viewer.updateSettings(ViewerSettingsPatch(accent = "#123456", pageBackground = "transparent"))
        assertNotNull(withTimeout(5_000) { revisions.receive() })
        viewer.updateSettings(ViewerSettingsPatch(clear = listOf(ViewerSettingsField.ACCENT)))
        val current = viewer.settings()
        assertNull(current.accent)
        assertEquals("transparent", current.pageBackground)
        assertEquals(initial.pageShadow, current.pageShadow)
        viewer.resetSettings()
        assertEquals(initial, viewer.settings())
        val document = viewer.open(Fixture.bytes("report"))
        assertFalse(viewer.canDownload(document))
        assertFalse(viewer.allows(DocumentPermission.DOWNLOAD, document))
        expectCode(PdfException.Code.PERMISSION_DENIED) { viewer.saveLayer(document) }
        viewer.destroy()
    }

    @Test
    fun documentPermissionNamesEveryConstantOnce() {
        val named = DocumentPermission.named
        assertEquals(29, named.size)
        assertEquals(named.size, named.toSet().size)
        assertEquals("doc.download", DocumentPermission.DOWNLOAD.value)
        assertEquals(DocumentPermission("protected-display.view").value, "protected-display.view")
    }
}
