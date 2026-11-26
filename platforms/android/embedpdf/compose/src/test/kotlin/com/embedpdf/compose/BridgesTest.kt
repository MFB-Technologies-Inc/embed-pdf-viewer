package com.embedpdf.compose

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asAndroidBitmap
import com.embedpdf.AnnotationColor
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.RenderConfig
import com.embedpdf.RenderPageOptions
import com.embedpdf.ViewerConfig
import com.embedpdf.render
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config
import org.robolectric.annotation.GraphicsMode

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
@GraphicsMode(GraphicsMode.Mode.NATIVE)
class BridgesTest {
    @Test
    fun aPageImageBecomesAnImageBitmapWithTheSamePixels() {
        val o = opened(ViewerConfig(render = RenderConfig()))
        try {
            val image = engine { o.viewer.render(o.document).renderPage(o.pages[0].page, RenderPageOptions(scale = 0.25)) }.image
            val bitmap = image.toImageBitmap()
            assertEquals(image.width, bitmap.width)
            assertEquals(image.height, bitmap.height)
            val android = bitmap.asAndroidBitmap()
            for ((x, y) in listOf(0 to 0, image.width / 2 to image.height / 3, image.width - 1 to image.height - 1)) {
                assertEquals("($x, $y)", image.pixel(x, y), android.getPixel(x, y))
            }
        } finally {
            o.destroy()
        }
    }

    @Test
    fun anAnnotationColorBecomesAnOpaqueColorAndComesBack() {
        val color = AnnotationColor("#ff8000")
        assertEquals(Color(0xffff8000), color.toColor())
        assertEquals(color, color.toColor().toAnnotationColor())
    }

    @Test
    fun aColorDropsItsAlphaOnTheWayToAnAnnotationColor() {
        assertEquals(AnnotationColor("#336699"), Color(0x80336699).toAnnotationColor())
    }
}
