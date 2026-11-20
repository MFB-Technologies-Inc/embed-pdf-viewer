package com.embedpdf

import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.async
import kotlinx.coroutines.delay
import kotlinx.coroutines.withTimeout
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test
import java.net.URI
import java.util.concurrent.atomic.AtomicInteger
import kotlin.coroutines.cancellation.CancellationException

/** A fetcher written against `com.embedpdf` alone: it names no `uniffi` type. */
private class StubFetcher(private val body: ByteArray) : DocumentFetcher {
    override fun fetch(url: String, completion: DocumentFetchCompletion): DocumentFetchCancellation {
        if (url.endsWith("/report.pdf")) {
            completion.finish(200u, body)
        } else {
            completion.failWith(HostFailure(name = "StubFetcher", code = "not-found", detail = "no such document"))
        }
        return object : DocumentFetchCancellation {
            override fun cancel() {}
        }
    }
}

private class ThrowingFetcher : DocumentFetcher {
    val calls = AtomicInteger()

    override fun fetch(url: String, completion: DocumentFetchCompletion): DocumentFetchCancellation {
        calls.incrementAndGet()
        throw IllegalStateException("the transport broke")
    }
}

class FetcherTest {
    private suspend fun waitFor(what: String, condition: () -> Boolean) {
        withTimeout(5_000) {
            while (!condition()) delay(10)
        }
        assertTrue(what, condition())
    }

    @Test
    fun aServedPdfOpens() = viewerTest {
        LoopbackServer { _, reply -> reply.whole(200, Fixture.bytes("report")) }.use { server ->
            val viewer = PdfViewer(ViewerConfig())
            viewer.start()
            val document = viewer.open(URI(server.url("/report.pdf")).toURL())
            assertEquals(DocumentStatus.READY, document.status)
            assertEquals(14u, document.pageCount)
            viewer.destroy()
        }
    }

    @Test
    fun aNotFoundLeavesAFailedTabThatRetryRefetches() = viewerTest {
        val statuses = ArrayDeque(listOf(404, 200))
        LoopbackServer { _, reply ->
            val status = synchronized(statuses) { statuses.removeFirstOrNull() ?: 200 }
            reply.whole(status, if (status == 200) Fixture.bytes("report") else ByteArray(0))
        }.use { server ->
            val viewer = PdfViewer(ViewerConfig())
            viewer.start()
            expectCode(PdfException.Code.NOT_FOUND) { viewer.open(URI(server.url("/report.pdf")).toURL()) }
            val failed = viewer.documents().single()
            assertEquals(DocumentStatus.FAILED, failed.status)
            val retried = viewer.retry(failed)
            assertEquals(DocumentStatus.READY, retried.status)
            assertEquals(14u, retried.pageCount)
            assertEquals(1, viewer.documents().size)
            assertEquals(2, server.requests.get())
            viewer.destroy()
        }
    }

    @Test
    fun aBodyThatIsNotAPdfFailsTheOpen() = viewerTest {
        LoopbackServer { _, reply -> reply.whole(200, "<html>hello</html>".toByteArray()) }.use { server ->
            val viewer = PdfViewer(ViewerConfig())
            viewer.start()
            try {
                viewer.open(URI(server.url("/page.html")).toURL())
                fail("a body that is not a PDF opened")
            } catch (e: PdfException) {
                // The kernel's PDF header check refuses it.
            }
            assertEquals(DocumentStatus.FAILED, viewer.documents().single().status)
            viewer.destroy()
        }
    }

    @Test
    fun cancellingAStalledOpenClosesTheTabAndTheConnection() = viewerTest {
        LoopbackServer { _, reply -> reply.hold() }.use { server ->
            val viewer = PdfViewer(ViewerConfig())
            viewer.start()
            val opening = async(start = CoroutineStart.UNDISPATCHED) {
                viewer.open(URI(server.url("/held.pdf")).toURL())
            }
            waitFor("the request arrived") { server.requests.get() == 1 }
            assertEquals(DocumentStatus.LOADING, viewer.documents().single().status)
            opening.cancel()
            try {
                opening.await()
                fail("a cancelled open finished")
            } catch (e: CancellationException) {
                // The caller sees its own cancellation.
            }
            // The kernel answered before the cancellation propagated, so the tab is gone.
            assertEquals(emptyList<DocumentInfo>(), viewer.documents())
            waitFor("the connection closed") { server.stops.get() == 1 }
            viewer.destroy()
        }
    }

    @Test
    fun aFetcherThatThrowsIsReportedAndFailsOnlyItsOpen() = viewerTest {
        val fetcher = ThrowingFetcher()
        val viewer = PdfViewer(ViewerConfig(), fetcher)
        viewer.start()
        val reported = subscribed(viewer.observeReportedErrors())
        val error = expectCode(PdfException.Code.OPERATION_FAILED) {
            viewer.open(URI("https://embedpdf.test/report.pdf").toURL())
        }
        assertEquals("the transport broke", error.detail)
        val report = withTimeout(5_000) { reported.receive() }
        assertEquals("IllegalStateException", report.name)
        assertEquals("the transport broke", report.detail)
        assertEquals(1, fetcher.calls.get())
        // The kernel thread survived: the viewer still opens documents.
        assertEquals(14u, viewer.open(Fixture.bytes("report")).pageCount)
        viewer.destroy()
    }

    @Test
    fun aFetcherWrittenAgainstTheFacadeAloneServesAUrlDocument() = viewerTest {
        val viewer = PdfViewer(ViewerConfig(), StubFetcher(Fixture.bytes("report")))
        viewer.start()
        val document = viewer.open(URI("https://embedpdf.test/report.pdf").toURL())
        assertEquals(DocumentStatus.READY, document.status)
        assertEquals(14u, document.pageCount)
        expectCode(PdfException.Code.NOT_FOUND) { viewer.open(URI("https://embedpdf.test/missing.pdf").toURL()) }
        viewer.destroy()
    }

    @Test
    fun aFileUrlOpensThroughTheDefaultFetcher() = viewerTest {
        val viewer = PdfViewer(ViewerConfig())
        viewer.start()
        val document = viewer.open(Fixture.file("report").toURI().toURL())
        assertEquals(14u, document.pageCount)
        viewer.destroy()
    }

    @Test
    fun anErrorThrownWhileFetchingFailsTheOpen() = viewerTest {
        LoopbackServer { _, reply -> reply.whole(200, Fixture.bytes("report")) }.use { server ->
            // An `Error`, as an out-of-memory read would be: the open fails
            // instead of the fetch thread dying with the completion unanswered.
            val fetcher = HttpDocumentFetcher(configure = { throw OutOfMemoryError("too large") })
            val viewer = PdfViewer(ViewerConfig(), fetcher)
            viewer.start()
            val error = withTimeout(5_000) {
                try {
                    viewer.open(URI(server.url("/report.pdf")).toURL())
                    fail("expected the open to fail")
                    throw AssertionError()
                } catch (e: PdfException) {
                    e
                }
            }
            assertTrue(error.detail, error.detail.contains("too large"))
            viewer.destroy()
        }
    }
}
