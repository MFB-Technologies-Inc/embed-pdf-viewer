package com.embedpdf

import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.async
import kotlinx.coroutines.flow.count
import kotlinx.coroutines.flow.toList
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeout
import kotlinx.coroutines.yield
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test
import kotlin.coroutines.cancellation.CancellationException

class ViewerTest {
    // Bytes that are not a PDF fail the open; a wrong password parks the
    // document as a locked tab instead.
    private val notAPdf = "not a pdf".toByteArray()

    @Test
    fun aDocumentOpensWithItsPages() = viewerTest {
        val viewer = started()
        val document = viewer.open(Fixture.bytes("report"), name = "report.pdf")
        assertEquals(14u, document.pageCount)
        assertEquals("report.pdf", document.name)
        assertEquals(14, viewer.pages(document).size)
        viewer.destroy()
    }

    @Test
    fun aLockedDocumentOpensAsALockedTab() = viewerTest {
        val viewer = started()
        assertEquals(DocumentStatus.LOCKED, viewer.open(Fixture.bytes("encrypted-hello")).status)
        viewer.destroy()
    }

    @Test
    fun aFailedOpenListsItsTabAsFailed() = viewerTest {
        val viewer = started()
        expectCode(PdfException.Code.OPERATION_FAILED) { viewer.open(notAPdf) }
        val documents = viewer.documents()
        assertEquals(listOf(DocumentStatus.FAILED), documents.map { it.status })
        // The engine's words, with no code in front.
        assertEquals("failed to open base document", documents.first().error?.message)
        assertEquals("documents", documents.first().error?.capability)
        viewer.destroy()
    }

    @Test
    fun aFailedOpenAnnouncesTheTabsErrorWithItsCode() = viewerTest {
        val viewer = started()
        val events = subscribed(viewer.observeDocumentEvents())
        runCatching { viewer.open(notAPdf) }
        val tab = viewer.documents().first()
        val failed = events.nextMapped { it as? DocumentEventOpenFailed }
        assertEquals(tab.id, failed.documentId)
        assertEquals(tab.error, failed.error)
        assertEquals("operation-failed", failed.error.code)
        viewer.destroy()
    }

    @Test
    fun aWrongPasswordParksTheTabAndAWrongUnlockIsPermissionDenied() = viewerTest {
        val viewer = started()
        val events = subscribed(viewer.observeDocumentEvents())
        val parked = viewer.open(Fixture.bytes("encrypted-hello"), password = "nope")
        assertEquals(DocumentStatus.LOCKED, parked.status)
        val lockedEvent = events.nextMapped { it as? DocumentEventLocked }
        assertEquals(parked.id, lockedEvent.documentId)
        assertEquals(true, lockedEvent.passwordProvided)

        val locked = viewer.open(Fixture.bytes("encrypted-hello"))
        val error = expectCode(PdfException.Code.PERMISSION_DENIED) { viewer.unlock(locked, "nope") }
        assertEquals("incorrect document password", error.detail)
        assertEquals(DocumentStatus.LOCKED, viewer.documents().first { it.id == locked.id }.status)
        viewer.destroy()
    }

    @Test
    fun pagesOfAClosedDocumentAreNotFound() = viewerTest {
        val viewer = started()
        val document = viewer.open(Fixture.bytes("report"))
        viewer.close(document)
        expectCode(PdfException.Code.NOT_FOUND) { viewer.pages(document) }
        viewer.destroy()
    }

    @Test
    fun aPluginOfALockedDocumentIsNotReadyAndAMissingOneUnsupported() = viewerTest {
        val viewer = started(ViewerConfig(metadata = true))
        val locked = viewer.open(Fixture.bytes("encrypted-hello"))
        expectCode(PdfException.Code.NOT_READY) { viewer.call { viewer.host.metadata(locked.id) } }
        expectCode(PdfException.Code.UNSUPPORTED) { viewer.call { viewer.host.search(locked.id) } }
        viewer.destroy()
    }

    @Test
    fun aLockedOpenSaysNoPasswordWasGiven() = viewerTest {
        val viewer = started()
        val events = subscribed(viewer.observeDocumentEvents())
        val document = viewer.open(Fixture.bytes("encrypted-hello"))
        val locked = events.nextMapped { it as? DocumentEventLocked }
        assertEquals(document.id, locked.documentId)
        assertEquals(false, locked.passwordProvided)
        viewer.destroy()
    }

    @Test
    fun aSavedDocumentReopensInEitherMode() = viewerTest {
        val viewer = started()
        val original = Fixture.bytes("report")
        val document = viewer.open(original)
        val incremental = viewer.save(document)
        assertArrayEquals(original, incremental.copyOf(original.size))
        val rewritten = viewer.save(document, SaveMode.REWRITE)
        for (bytes in listOf(incremental, rewritten)) {
            assertEquals(14u, viewer.open(bytes).pageCount)
        }
        viewer.destroy()
    }

    @Test
    fun aSignedDocumentRefusesARewriteSave() = viewerTest {
        val viewer = started()
        val document = viewer.open(Fixture.bytes("signed_two_approvals"))
        expectCode(PdfException.Code.PERMISSION_DENIED) { viewer.save(document, SaveMode.REWRITE) }
        viewer.save(document, SaveMode.INCREMENTAL)
        viewer.destroy()
    }

    @Test
    fun saveCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val viewer = started()
        val document = viewer.open(Fixture.bytes("report"))
        val saving = async(start = CoroutineStart.UNDISPATCHED) { viewer.save(document, SaveMode.REWRITE) }
        saving.cancel()
        try {
            saving.await()
        } catch (e: CancellationException) {
            // A cancelled caller sees its own cancellation.
        }
        // The viewer keeps working after the cancelled call.
        viewer.save(document)
        viewer.destroy()
    }

    @Test
    fun aCallInACancelledCoroutineThrowsItsCancellation() = viewerTest {
        val viewer = started()
        val document = viewer.open(Fixture.bytes("report"))
        val call = async(start = CoroutineStart.UNDISPATCHED) {
            kotlinx.coroutines.currentCoroutineContext()[kotlinx.coroutines.Job]!!.cancel()
            viewer.save(document)
        }
        try {
            call.await()
            fail("a call made in a cancelled coroutine finished")
        } catch (e: CancellationException) {
            // Expected.
        }
        viewer.destroy()
    }

    @Test
    fun openingAndClosingAreObserved() = viewerTest {
        val viewer = started()
        val events = subscribed(viewer.observeDocumentEvents())
        val document = viewer.open(Fixture.bytes("report"))
        viewer.close(document)
        val seen = mutableListOf<String>()
        withTimeout(5_000) {
            while (seen.size < 2) {
                when (events.receive()) {
                    is DocumentEventOpened -> seen += "opened"
                    is DocumentEventClosed -> seen += "closed"
                    else -> {}
                }
            }
        }
        assertEquals(listOf("opened", "closed"), seen)
        viewer.destroy()
    }

    @Test
    fun destroyingCompletesTheFlows() = viewerTest {
        val viewer = started()
        val events = launch(start = CoroutineStart.UNDISPATCHED) { viewer.observeDocumentEvents().collect {} }
        val revisions = launch(start = CoroutineStart.UNDISPATCHED) { viewer.observeStateRevisions().collect {} }
        val errors = launch(start = CoroutineStart.UNDISPATCHED) { viewer.observeReportedErrors().collect {} }
        viewer.destroy()
        withTimeout(5_000) {
            events.join()
            revisions.join()
            errors.join()
        }
        assertTrue(events.isCompleted && !events.isCancelled)
        assertTrue(revisions.isCompleted && !revisions.isCancelled)
    }

    @Test
    fun aFlowCollectedAfterDestroyIsEmpty() = viewerTest {
        val viewer = started()
        viewer.destroy()
        assertEquals(0, viewer.observeDocumentEvents().count())
        assertEquals(emptyList<Long>(), viewer.observeStateRevisions().toList())
        assertEquals(0, viewer.observeReportedErrors().count())
    }

    @Test
    fun aCallAfterDestroyIsClosed() = viewerTest {
        val viewer = started()
        viewer.destroy()
        expectCode(PdfException.Code.INSTANCE_CLOSED) { viewer.documents() }
    }

    @Test
    fun destroyingTwiceIsClosed() = viewerTest {
        val viewer = started()
        viewer.destroy()
        expectCode(PdfException.Code.INSTANCE_CLOSED) { viewer.destroy() }
    }

    @Test
    fun closeReleasesTheViewerAndLaterCallsAreClosed() = viewerTest {
        val viewer = started()
        val revisions = launch(start = CoroutineStart.UNDISPATCHED) { viewer.observeStateRevisions().collect {} }
        viewer.use { yield() }
        withTimeout(5_000) { revisions.join() }
        viewer.close()
        expectCode(PdfException.Code.INSTANCE_CLOSED) { viewer.documents() }
        expectCode(PdfException.Code.INSTANCE_CLOSED) { viewer.save(DocumentInfo("x", null, 0u, DocumentStatus.READY, null)) }
    }

    @Test
    fun closeAfterDestroyIsSafe() = viewerTest {
        val viewer = started()
        viewer.destroy()
        viewer.close()
    }

    @Test
    fun theFeaturesFixtureIsFound() = viewerTest {
        val viewer = started()
        assertEquals(3u, viewer.open(Fixture.bytes("features")).pageCount)
        viewer.destroy()
    }
}
