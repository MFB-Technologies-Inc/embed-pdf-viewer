package com.embedpdf

import kotlinx.coroutines.delay
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class ViewManagerTest {
    private suspend fun opened(): Triple<PdfViewer, DocumentInfo, ViewManagerCapability> {
        val viewer = started(ViewerConfig(viewManager = true))
        val manager = viewer.viewManager()
        val document = viewer.open(Fixture.bytes("report"))
        return Triple(viewer, document, manager)
    }

    @Test
    fun theFirstDocumentLandsInTheDefaultPane() = viewerTest {
        val (viewer, document, manager) = opened()
        assertEquals(listOf(PaneInfo("pane-1", listOf(document.id), document.id)), manager.panes())
        assertEquals("pane-1", manager.focusedPaneId())
        viewer.destroy()
    }

    @Test
    fun splittingIsObserved() = viewerTest {
        val (viewer, _, manager) = opened()
        val colors = viewer.open(Fixture.bytes("colors"))
        val events = subscribed(manager.observeEvents())
        val pane = manager.splitPane(document = colors)
        val moved = events.nextMapped { it as? ViewManagerEventDocumentMoved }
        assertEquals(DocumentMovedEvent(colors.id, "pane-1", pane), moved.event)
        assertEquals(pane, manager.pane(document = colors))
        viewer.destroy()
    }

    @Test
    fun closingADocumentTakesItOutOfItsPane() = viewerTest {
        val (viewer, document, manager) = opened()
        val colors = viewer.open(Fixture.bytes("colors"))
        manager.setActiveDocument(document, paneId = "pane-1")
        viewer.close(document)
        val pane = manager.pane("pane-1")
        assertEquals(listOf(colors.id), pane?.documentIds)
        assertEquals(colors.id, pane?.activeDocumentId)
        viewer.destroy()
    }

    @Test
    fun documentsMoveBetweenPanesAsDocumentInfos() = viewerTest {
        val (viewer, document, manager) = opened()
        val colors = viewer.open(Fixture.bytes("colors"))
        val pane = manager.createPane(listOf(colors))
        manager.addDocument(document, paneId = pane, index = 0)
        assertEquals(listOf(document.id, colors.id), manager.pane(pane)?.documentIds)
        manager.moveDocument(colors, paneId = pane, toIndex = 0)
        manager.setActiveDocument(colors, paneId = pane)
        val info = manager.pane(pane)
        assertEquals(listOf(colors.id, document.id), info?.documentIds)
        assertEquals(colors.id, info?.activeDocumentId)
        manager.moveDocument(document, fromPaneId = pane, toPaneId = "pane-1")
        assertEquals("pane-1", manager.pane(document = document))
        manager.removeDocument(colors, paneId = pane)
        assertNull(manager.pane(document = colors))
        viewer.destroy()
    }

    @Test
    fun anIndexPastTheEndAppends() = viewerTest {
        val (viewer, _, manager) = opened()
        val second = manager.createPane()
        manager.movePane("pane-1", toIndex = Int.MAX_VALUE)
        assertEquals(listOf(second, "pane-1"), manager.paneOrder())
        expectCode(PdfException.Code.INVALID_INPUT) { manager.movePane("pane-1", toIndex = -1) }
        viewer.destroy()
    }

    @Test
    fun panesAreRemovedAndFocused() = viewerTest {
        val (viewer, document, manager) = opened()
        val second = manager.createPane()
        manager.setFocusedPane("pane-1")
        assertEquals("pane-1", manager.focusedPaneId())
        manager.removePane("pane-1", movingDocumentsTo = second)
        assertEquals(second, manager.pane(document = document))
        viewer.destroy()
    }

    @Test
    fun droppingTheFlowUnsubscribes() = viewerTest {
        val (viewer, _, manager) = opened()
        val collector = collecting(manager.observeEvents())
        delay(20)
        collector.cancel()
        collector.join()
        manager.createPane()
        viewer.destroy()
    }

    @Test
    fun destroyingTheViewerCompletesTheEvents() = viewerTest {
        val (viewer, _, manager) = opened()
        val events = collecting(manager.observeEvents())
        delay(50)
        viewer.destroy()
        assertTrue(finishes(events))
    }

    @Test
    fun aDestroyedViewersCapabilityThrows() = viewerTest {
        val viewer = started(ViewerConfig(viewManager = true))
        val manager = viewer.viewManager()
        viewer.destroy()
        // `destroy` stops the kernel and then the thread, so the call answers
        // either the plugin's `instance-closed` or the viewer's.
        expectCode(PdfException.Code.INSTANCE_CLOSED) { manager.panes() }
    }
}
