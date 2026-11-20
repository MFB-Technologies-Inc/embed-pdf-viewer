package com.embedpdf

import android.content.ContentResolver
import android.net.Uri
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RuntimeEnvironment
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config
import java.io.ByteArrayInputStream
import java.util.concurrent.CopyOnWriteArrayList

private class RecordingFetcher : DocumentFetcher {
    val urls = CopyOnWriteArrayList<String>()

    override fun fetch(url: String, completion: DocumentFetchCompletion): DocumentFetchCancellation {
        urls += url
        completion.failWith(HostFailure(name = "RecordingFetcher", code = "not-found", detail = "recorded"))
        return EmptyCancellation
    }
}

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class ContentResolverFetcherTest {
    private val resolver: ContentResolver
        get() = RuntimeEnvironment.getApplication().contentResolver

    @Test
    fun aContentUriOpensThroughTheResolver() = viewerTest {
        val uri = Uri.parse("content://com.embedpdf.test/documents/report.pdf")
        shadowOf(resolver).registerInputStream(uri, ByteArrayInputStream(Fixture.bytes("report")))
        val viewer = PdfViewer(ViewerConfig(), ContentResolverDocumentFetcher(resolver, RecordingFetcher()))
        viewer.start()
        val document = viewer.open(uri, name = "report.pdf")
        assertEquals(DocumentStatus.READY, document.status)
        assertEquals(14u, document.pageCount)
        assertEquals("report.pdf", document.name)
        viewer.destroy()
    }

    @Test
    fun aFileUriOpensThroughTheResolver() = viewerTest {
        val viewer = PdfViewer(ViewerConfig(), ContentResolverDocumentFetcher(resolver, RecordingFetcher()))
        viewer.start()
        val document = viewer.open(Uri.fromFile(Fixture.file("report")))
        assertEquals(14u, document.pageCount)
        viewer.destroy()
    }

    @Test
    fun otherSchemesGoToTheFallback() = viewerTest {
        val fallback = RecordingFetcher()
        val viewer = PdfViewer(ViewerConfig(), ContentResolverDocumentFetcher(resolver, fallback))
        viewer.start()
        expectCode(PdfException.Code.NOT_FOUND) { viewer.open(Uri.parse("https://embedpdf.test/report.pdf")) }
        assertEquals(listOf("https://embedpdf.test/report.pdf"), fallback.urls.toList())
        viewer.destroy()
    }
}
