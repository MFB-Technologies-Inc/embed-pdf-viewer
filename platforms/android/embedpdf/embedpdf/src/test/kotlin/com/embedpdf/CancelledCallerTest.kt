package com.embedpdf

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.TimeoutCancellationException
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeout
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test
import java.net.URI
import kotlin.concurrent.thread
import kotlin.coroutines.cancellation.CancellationException

/** What a caller cancelled while the kernel works sees, and what is left behind. */
class CancelledCallerTest {
    @Test
    fun aValueThatArrivesAfterTheTimeoutIsDiscarded() = viewerTest {
        val discarded = CompletableDeferred<String>()
        try {
            withTimeout(50) {
                bridgedCancellable(onDiscard = { value: String -> discarded.complete(value) }) {
                    // The kernel ignores the cancel and answers late.
                    delay(300)
                    "made"
                }
            }
            fail("a timed-out call returned")
        } catch (e: TimeoutCancellationException) {
            // The caller's own cancellation.
        }
        assertEquals("made", discarded.await())
    }

    @Test
    fun aCancellationInsideTheCallIsOperationCancelledForAnActiveCaller() = viewerTest {
        expectCode(PdfException.Code.OPERATION_CANCELLED) {
            bridgedCancellable<Unit> { throw CancellationException("the call's own") }
        }
    }

    @Test
    fun keepResolvedHandsACancelledCallerTheResolvedValue() = viewerTest {
        val started = CompletableDeferred<Unit>()
        var value: List<String>? = null
        val calling = launch {
            value = bridgedCancellable(keepResolved = true) { token ->
                started.complete(Unit)
                // The kernel resolves an empty list once the cancel reaches it,
                // as `markMatches` does before its search finds a hit.
                while (!token.isCancelled()) delay(5)
                emptyList()
            }
        }
        started.await()
        calling.cancel()
        calling.join()
        assertEquals(emptyList<String>(), value)
    }

    @Test
    fun keepResolvedStillThrowsTheCallersCancellationForAnError() = viewerTest {
        val started = CompletableDeferred<Unit>()
        var thrown: Throwable? = null
        val calling = launch {
            try {
                bridgedCancellable<Unit>(keepResolved = true) { token ->
                    started.complete(Unit)
                    while (!token.isCancelled()) delay(5)
                    throw PdfException(PdfException.Code.OPERATION_CANCELLED, "operation cancelled", "AbortError", null)
                }
            } catch (e: Throwable) {
                thrown = e
                throw e
            }
        }
        started.await()
        calling.cancel()
        calling.join()
        assertTrue("$thrown", thrown is CancellationException && thrown !is PdfException)
    }

    @Test
    fun anOpenThatFinishesAfterTheTimeoutLeavesNoTab() = viewerTest {
        val viewer = PdfViewer(ViewerConfig(), LateFetcher(Fixture.bytes("report")))
        viewer.start()
        try {
            withTimeout(50) { viewer.open(URI("https://embedpdf.test/late.pdf").toURL()) }
            fail("a timed-out open returned")
        } catch (e: TimeoutCancellationException) {
            // Expected.
        }
        // The late answer has arrived by now; it must not have left a tab.
        delay(500)
        assertEquals(emptyList<DocumentInfo>(), viewer.documents())
        viewer.destroy()
    }

    @Test
    fun aCancelledDestroyStillTearsTheViewerDown() = viewerTest {
        val viewer = started()
        val revisions = collecting(viewer.observeStateRevisions())
        val destroying = launch(start = CoroutineStart.UNDISPATCHED) {
            coroutineContext[kotlinx.coroutines.Job]!!.cancel()
            viewer.destroy()
        }
        destroying.join()
        assertTrue(destroying.isCancelled)
        assertTrue("the flows completed", finishes(revisions))
        expectCode(PdfException.Code.INSTANCE_CLOSED) { viewer.documents() }
        viewer.close()
    }
}

/** Serves [bytes] 300 ms after each request, and ignores cancellation. */
class LateFetcher(private val bytes: ByteArray) : DocumentFetcher {
    override fun fetch(url: String, completion: DocumentFetchCompletion): DocumentFetchCancellation {
        thread(isDaemon = true) {
            Thread.sleep(300)
            completion.finish(200u, bytes)
        }
        return object : DocumentFetchCancellation {
            override fun cancel() {}
        }
    }
}
