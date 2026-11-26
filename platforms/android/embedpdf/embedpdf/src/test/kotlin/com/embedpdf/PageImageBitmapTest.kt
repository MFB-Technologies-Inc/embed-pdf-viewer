package com.embedpdf

import android.graphics.Bitmap
import android.graphics.Color
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config
import org.robolectric.annotation.GraphicsMode
import uniffi.epdf_plugin_render.RenderPixelFormat
import uniffi.epdf_plugin_render.RenderedImage

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
// Native graphics, so the bitmap really premultiplies and the round trip is tested.
@GraphicsMode(GraphicsMode.Mode.NATIVE)
class PageImageBitmapTest {
    private fun image(width: Int, height: Int, stride: Int, pixels: ByteArray) =
        PageImage.from(RenderedImage(width.toUInt(), height.toUInt(), stride.toUInt(), 1.0, RenderPixelFormat.RGBA8, pixels))

    @Test
    fun aBitmapHasTheImagesColors() {
        // Two opaque pixels and a translucent one, in a row padded to 16 bytes.
        val bytes = byteArrayOf(
            255.toByte(), 0, 0, 255.toByte(),
            0, 0, 255.toByte(), 255.toByte(),
            51, 102, 153.toByte(), 128.toByte(),
            9, 9, 9, 9,
        )
        val bitmap = image(3, 1, 16, bytes).toBitmap()
        assertEquals(3, bitmap.width)
        assertEquals(1, bitmap.height)
        assertEquals(Bitmap.Config.ARGB_8888, bitmap.config)
        assertEquals(Color.RED, bitmap.getPixel(0, 0))
        assertEquals(Color.BLUE, bitmap.getPixel(1, 0))
        // Straight alpha in, straight alpha out: Android premultiplies inside the
        // bitmap and gives the color back, give or take rounding.
        val translucent = bitmap.getPixel(2, 0)
        assertEquals(128, Color.alpha(translucent))
        assertEquals(51.0, Color.red(translucent).toDouble(), 1.0)
        assertEquals(102.0, Color.green(translucent).toDouble(), 1.0)
        assertEquals(153.0, Color.blue(translucent).toDouble(), 1.0)
    }

    @Test
    fun aShortBufferIsInvalidState() {
        try {
            image(2, 2, 8, ByteArray(8))
            throw AssertionError("a short buffer was accepted")
        } catch (e: PdfException) {
            assertEquals(PdfException.Code.INVALID_STATE, e.code)
        }
    }

    @Test
    fun anEmptyRasterHasNoBitmap() {
        for ((width, height) in listOf(0 to 0, 0 to 3, 3 to 0)) {
            try {
                image(width, height, width * 4, ByteArray(0)).toBitmap()
                throw AssertionError("a ${width}x$height raster made a bitmap")
            } catch (e: PdfException) {
                assertEquals(PdfException.Code.INVALID_INPUT, e.code)
            }
        }
    }
}
