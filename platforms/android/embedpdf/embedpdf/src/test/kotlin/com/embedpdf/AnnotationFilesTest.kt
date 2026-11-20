package com.embedpdf

import android.content.ContentResolver
import android.net.Uri
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RuntimeEnvironment
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config
import java.io.ByteArrayInputStream

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class AnnotationFilesTest {
    private val resolver: ContentResolver
        get() = RuntimeEnvironment.getApplication().contentResolver

    private val png: ByteArray = Fixture.bytes("tiny", "png")

    private fun registered(path: String): Uri {
        val uri = Uri.parse("content://com.embedpdf.test/files/$path")
        shadowOf(resolver).registerInputStream(uri, ByteArrayInputStream(png))
        return uri
    }

    @Test
    fun aContentUriBecomesEachFileRecord() = viewerTest {
        val attachment = AnnotationAttachmentFile(resolver, registered("tiny.png"), description = "a pixel")
        assertArrayEquals(png, attachment.bytes)
        assertEquals("tiny.png", attachment.name)
        assertEquals("a pixel", attachment.description)
        val stamp = AnnotationStampSource(resolver, registered("stamp.png"), subject = "Approved")
        assertArrayEquals(png, stamp.bytes)
        assertEquals("stamp.png", stamp.name)
        assertEquals("Approved", stamp.subject)
        val picked = AnnotationPickedFile(resolver, registered("picked.png"))
        assertArrayEquals(png, picked.bytes)
        assertEquals("picked.png", picked.name)
        // Robolectric's resolver has no provider to name a type.
        assertNull(picked.mimeType)
    }

    @Test
    fun aFileUriReadsThroughTheResolver() = viewerTest {
        val file = AnnotationAttachmentFile(resolver, Uri.fromFile(Fixture.file("tiny", "png")))
        assertArrayEquals(png, file.bytes)
        assertEquals("tiny.png", file.name)
    }

    @Test
    fun aUriTheResolverCannotOpenIsNotFound() = viewerTest {
        expectCode(PdfException.Code.NOT_FOUND) {
            AnnotationPickedFile(resolver, Uri.fromFile(Fixture.file("tiny", "png").resolveSibling("missing.png")))
        }
    }

    @Test
    fun aFileOverTheCapIsInvalidInput() = viewerTest {
        expectCode(PdfException.Code.INVALID_INPUT) {
            AnnotationPickedFile(resolver, registered("big.png"), maxBytes = png.size - 1L)
        }
        assertArrayEquals(png, AnnotationPickedFile(resolver, registered("exact.png"), maxBytes = png.size.toLong()).bytes)
    }

    @Test
    fun aRefusedOrFailedReadIsAPdfException() = viewerTest {
        val refused = Uri.parse("content://com.embedpdf.test/files/refused.png")
        shadowOf(resolver).registerInputStreamSupplier(refused) { throw SecurityException("no grant") }
        expectCode(PdfException.Code.PERMISSION_DENIED) { AnnotationPickedFile(resolver, refused) }
        val broken = Uri.parse("content://com.embedpdf.test/files/broken.png")
        shadowOf(resolver).registerInputStream(broken, object : java.io.InputStream() {
            override fun read(): Int = throw java.io.IOException("device gone")
            override fun read(b: ByteArray, off: Int, len: Int): Int = throw java.io.IOException("device gone")
        })
        expectCode(PdfException.Code.OPERATION_FAILED) { AnnotationPickedFile(resolver, broken) }
    }
}
