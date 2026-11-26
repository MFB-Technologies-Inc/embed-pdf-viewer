package com.embedpdf

import kotlinx.coroutines.delay
import kotlinx.serialization.Serializable
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class ShellTest {
    @Serializable
    data class StyleProps(val annotationId: String, val page: Int)

    private suspend fun opened(): Triple<PdfViewer, DocumentInfo, ShellCapability> {
        val viewer = started(ViewerConfig(shell = true))
        val document = viewer.open(Fixture.bytes("report"))
        return Triple(viewer, document, viewer.shell(document))
    }

    @Test
    fun typedPropsRoundTrip() = viewerTest {
        val (viewer, _, shell) = opened()
        shell.open("style", props = StyleProps(annotationId = "a1", page = 3))
        val surface = shell.surface("style")!!
        assertTrue(surface.open)
        assertEquals(StyleProps("a1", 3), surface.props<StyleProps>())
        shell.updateProps("style", props = StyleProps(annotationId = "a2", page = 4))
        assertEquals(StyleProps("a2", 4), shell.surface("style")!!.props<StyleProps>())
        viewer.destroy()
    }

    @Test
    fun aNeverOpenedSurfaceIsNull() = viewerTest {
        val (viewer, _, shell) = opened()
        assertNull(shell.surface("thumbnails"))
        viewer.destroy()
    }

    /**
     * `surfaces[id]` on the TypeScript plugin's plain object finds an
     * `Object.prototype` member, so these ids are found, closed and bare.
     */
    @Test
    fun anObjectPrototypeNameIsFoundClosed() = viewerTest {
        val (viewer, _, shell) = opened()
        for (id in listOf("constructor", "toString")) {
            assertEquals(ShellSurface(id, false, null, null, 0.0), shell.surface(id))
            assertFalse(shell.isOpen(id))
        }
        viewer.destroy()
    }

    @Test
    fun propsThatAreNotAnObjectThrow() = viewerTest {
        val (viewer, _, shell) = opened()
        expectCode(PdfException.Code.INVALID_INPUT) { shell.open("style", props = listOf(1, 2, 3)) }
        assertFalse(shell.isOpen("style"))
        viewer.destroy()
    }

    @Test
    fun anExclusiveOpenIsObserved() = viewerTest {
        val (viewer, _, shell) = opened()
        shell.open("thumbnails", exclusive = "left")
        val events = subscribed(shell.observeEvents())
        shell.open("search", exclusive = "left")
        val seen = listOf(events.nextMapped { it }, events.nextMapped { it })
        assertEquals(
            listOf<ShellEvent>(
                ShellEventSurfaceClosed(ShellSurfaceClosedEvent("thumbnails")),
                ShellEventSurfaceOpened(ShellSurfaceOpenedEvent("search", null)),
            ),
            seen,
        )
        viewer.destroy()
    }

    @Test
    fun togglingAndMenusTrackOpenState() = viewerTest {
        val (viewer, _, shell) = opened()
        shell.toggle("sidebar")
        assertEquals(listOf("sidebar"), shell.openSurfaces().map { it.id })
        shell.toggle("sidebar")
        assertFalse(shell.isOpen("sidebar"))
        shell.openMenu("file")
        shell.toggleMenu("edit")
        assertEquals(listOf("file", "edit"), shell.openMenus())
        shell.closeMenu("file")
        assertTrue(shell.isMenuOpen("edit"))
        shell.open("sidebar")
        shell.close("sidebar")
        assertFalse(shell.isOpen("sidebar"))
        viewer.destroy()
    }

    @Test
    fun aSnapshotRestores() = viewerTest {
        val (viewer, _, shell) = opened()
        shell.open("sidebar", exclusive = "left")
        shell.openMenu("file")
        val snapshot = shell.snapshot()
        shell.closeAll()
        shell.closeAllMenus()
        shell.applySnapshot(snapshot)
        assertEquals(snapshot, shell.snapshot())
        viewer.destroy()
    }

    @Test
    fun eachDocumentKeepsItsOwnSurfaces() = viewerTest {
        val (viewer, _, first) = opened()
        val colors = viewer.open(Fixture.bytes("colors"))
        val second = viewer.shell(colors)
        first.open("sidebar")
        assertTrue(first.isOpen("sidebar"))
        assertFalse(second.isOpen("sidebar"))
        viewer.destroy()
    }

    @Test
    fun droppingTheFlowUnsubscribes() = viewerTest {
        val (viewer, _, shell) = opened()
        val collector = collecting(shell.observeEvents())
        delay(20)
        collector.cancel()
        collector.join()
        shell.open("sidebar")
        viewer.destroy()
    }

    @Test
    fun closingTheDocumentCompletesTheEvents() = viewerTest {
        val (viewer, document, shell) = opened()
        val events = collecting(shell.observeEvents())
        delay(50)
        viewer.close(document)
        assertTrue(finishes(events))
        viewer.destroy()
    }

    @Test
    fun aClosedDocumentsCapabilityThrows() = viewerTest {
        val (viewer, document, shell) = opened()
        viewer.close(document)
        val error = expectCode(PdfException.Code.INSTANCE_CLOSED) { shell.isOpen("sidebar") }
        assertEquals("PluginError", error.name)
        viewer.destroy()
    }
}
