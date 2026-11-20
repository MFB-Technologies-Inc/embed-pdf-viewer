package com.embedpdf

import kotlinx.coroutines.flow.toList
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Test

class OutlineTest {
    @Test
    fun nestedOutlineEditsPersistAndPatchClearingIsExplicit() = viewerTest {
        val viewer = started(ViewerConfig(outline = true))
        val document = viewer.open(Fixture.bytes("report"))
        val outline = viewer.outline(document)
        outline.ensureLoaded()
        val parent = outline.create(OutlineItemInput(title = "Parent", clearTarget = true), OutlineCreateOptions(select = true))
        val child = outline.create(
            OutlineItemInput(title = "Child", parent = parent.itemRef, clearTarget = true, color = "#ff0000"),
        )
        outline.expand(parent.itemRef)
        assertEquals(1u, outline.rows().first { it.item.itemRef == child.itemRef }.depth)
        val cleared = outline.update(
            child.itemRef,
            OutlineItemPatch(title = "Renamed", clear = listOf(OutlinePatchKey.COLOR, OutlinePatchKey.TARGET)),
        )
        assertEquals("Renamed", cleared.title)
        assertNull(cleared.color)
        outline.move(listOf(child.itemRef), to = null, position = OutlinePositionStart)
        assertNull(outline.item(child.itemRef)!!.parent)
        expectCancelledBeforeItStarts { outline.refresh() }
        val bytes = viewer.save(document)
        val reopened = viewer.open(bytes)
        val saved = viewer.outline(reopened)
        saved.ensureLoaded()
        assertEquals("Renamed", saved.item(child.itemRef)!!.title)
        outline.delete(listOf(child.itemRef, parent.itemRef))
        assertNull(outline.item(parent.itemRef))
        viewer.close(document)
        assertEquals(emptyList<OutlineEvent>(), outline.observeEvents().toList())
        viewer.destroy()
    }

    @Test
    fun editingIsUnavailableWithRestrictedScope() = viewerTest {
        val viewer = started(ViewerConfig(scope = listOf("doc.open"), outline = true))
        val document = viewer.open(Fixture.bytes("report"))
        val outline = viewer.outline(document)
        assertFalse(outline.canEdit())
        expectCode(PdfException.Code.PERMISSION_DENIED) { outline.create(OutlineItemInput(title = "Denied")) }
        viewer.destroy()
    }
}
