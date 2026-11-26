package com.embedpdf

import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.async
import kotlinx.coroutines.delay
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.coroutines.cancellation.CancellationException

class MetadataTest {
    private suspend fun opened(): Triple<PdfViewer, DocumentInfo, MetadataCapability> {
        val viewer = started(ViewerConfig(metadata = true))
        val document = viewer.open(Fixture.bytes("report"))
        return Triple(viewer, document, viewer.metadata(document))
    }

    @Test
    fun refreshThenUpdateIsObserved() = viewerTest {
        val (viewer, _, metadata) = opened()
        metadata.refresh()
        assertEquals(ResourceStatus.READY, metadata.status())
        val events = subscribed(metadata.observeEvents())
        val updated = metadata.update(MetadataPatch(title = "Renamed"))
        assertEquals("Renamed", updated.title)
        val event = events.nextMapped { it as? MetadataEventUpdated }
        assertEquals(listOf("title"), event.event.changedKeys)
        assertEquals("Renamed", metadata.snapshot()?.title)
        viewer.destroy()
    }

    @Test
    fun customKeysUpdateAndRemove() = viewerTest {
        val (viewer, _, metadata) = opened()
        metadata.ensureLoaded()
        assertEquals("v", metadata.updateCustom(mapOf("k" to "v"))["k"])
        assertEquals("v", metadata.customSnapshot()?.get("k"))
        assertEquals(null, metadata.updateCustom(mapOf("k" to null))["k"])
        metadata.refreshCustom()
        assertEquals(ResourceStatus.READY, metadata.customStatus())
        assertTrue(metadata.canUpdate())
        viewer.destroy()
    }

    @Test
    fun aClosedDocumentsCapabilityThrows() = viewerTest {
        val (viewer, document, metadata) = opened()
        viewer.close(document)
        val error = expectCode(PdfException.Code.INSTANCE_CLOSED) { metadata.snapshot() }
        // `PluginError`, not `Closed`: the viewer is still running.
        assertEquals("PluginError", error.name)
        viewer.destroy()
    }

    @Test
    fun droppingTheFlowUnsubscribes() = viewerTest {
        val (viewer, _, metadata) = opened()
        val collector = collecting(metadata.observeEvents())
        delay(20)
        collector.cancel()
        collector.join()
        metadata.refresh()
        viewer.destroy()
    }

    @Test
    fun cancellingARefreshDoesNotHang() = viewerTest {
        val (viewer, _, metadata) = opened()
        val refreshing = async(start = CoroutineStart.UNDISPATCHED) { metadata.refresh() }
        refreshing.cancel()
        try {
            refreshing.await()
        } catch (e: CancellationException) {
            // Cancelled, or finished first.
        }
        metadata.refresh()
        viewer.destroy()
    }

    @Test
    fun updateCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, _, metadata) = opened()
        metadata.refresh()
        expectCancelledBeforeItStarts { metadata.update(MetadataPatch(title = "Renamed")) }
        assertNotEquals("Renamed", metadata.snapshot()?.title)
        viewer.destroy()
    }

    @Test
    fun refreshCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (viewer, _, metadata) = opened()
        expectCancelledBeforeItStarts { metadata.refresh() }
        viewer.destroy()
    }

    @Test
    fun destroyingTheViewerCompletesTheEvents() = viewerTest {
        val (viewer, _, metadata) = opened()
        val events = collecting(metadata.observeEvents())
        delay(50)
        viewer.destroy()
        assertTrue(finishes(events))
    }

    @Test
    fun closingTheDocumentCompletesTheEvents() = viewerTest {
        val (viewer, document, metadata) = opened()
        val events = collecting(metadata.observeEvents())
        delay(50)
        viewer.close(document)
        assertTrue(finishes(events))
        // A flow collected after the close completes at once.
        assertTrue(completes(metadata.observeEvents()))
        viewer.destroy()
    }
}
