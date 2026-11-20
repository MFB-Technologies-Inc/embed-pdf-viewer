package com.embedpdf

import android.graphics.Bitmap
import android.graphics.PointF
import android.graphics.RectF
import android.util.SizeF
import org.junit.Assert.assertEquals
import org.junit.Assert.assertSame
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class GeometryBridgesTest {
    @Test
    fun pointRoundTrips() {
        val point = Point(10.5, -20.25)
        assertEquals(PointF(10.5f, -20.25f), point.toPointF())
        assertEquals(point, point.toPointF().toPoint())
    }

    @Test
    fun sizeRoundTrips() {
        val size = Size(612.0, 792.5)
        assertEquals(SizeF(612f, 792.5f), size.toSizeF())
        assertEquals(size, size.toSizeF().toSize())
    }

    @Test
    fun rectRoundTrips() {
        val rect = Rect(10.0, 20.0, 100.0, 12.5)
        assertEquals(RectF(10f, 20f, 110f, 32.5f), rect.toRectF())
        assertEquals(rect, rect.toRectF().toRect())
    }

    @Test
    fun rectFromAndroidIsStandardized() {
        assertEquals(Rect(10.0, 20.0, 100.0, 12.0), RectF(110f, 32f, 10f, 20f).toRect())
    }

    @Test
    fun quadTakesTheCornersOfARect() {
        val quad = Rect(10.0, 20.0, 100.0, 12.0).toQuad()
        assertEquals(Point(10.0, 20.0), quad.upperLeft)
        assertEquals(Point(110.0, 20.0), quad.upperRight)
        assertEquals(Point(10.0, 32.0), quad.lowerLeft)
        assertEquals(Point(110.0, 32.0), quad.lowerRight)
    }

    @Test
    fun androidValuesMatchPageValues() {
        for (turn in RenderRotation.entries) {
            val geometry = PageGeometry(turn, Size(612.0, 792.0))
            val point = Point(30.0, 20.0)
            val rect = Rect(10.0, 20.0, 100.0, 12.0)
            assertEquals(geometry.toDisplay(point), geometry.toDisplay(point.toPointF()).toPoint())
            assertEquals(geometry.toPage(point), geometry.toPage(point.toPointF()).toPoint())
            assertEquals(geometry.toDisplay(rect), geometry.toDisplay(rect.toRectF()).toRect())
            assertEquals(geometry.toPage(rect), geometry.toPage(rect.toRectF()).toRect())
            assertEquals(geometry.displaySize, geometry.displaySizeF.toSize())
        }
    }

    @Test
    fun aBitmapSwapsItsSidesOnAQuarterTurn() {
        val bitmap = Bitmap.createBitmap(4, 2, Bitmap.Config.ARGB_8888)
        assertSame(bitmap, PageGeometry(RenderRotation.NONE, Size(4.0, 2.0)).toDisplay(bitmap))
        val turned = PageGeometry(RenderRotation.CLOCKWISE90, Size(4.0, 2.0)).toDisplay(bitmap)
        assertEquals(listOf(2, 4), listOf(turned.width, turned.height))
    }
}
