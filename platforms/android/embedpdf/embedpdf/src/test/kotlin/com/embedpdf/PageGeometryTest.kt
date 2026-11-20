package com.embedpdf

import org.junit.Assert.assertEquals
import org.junit.Assert.assertSame
import org.junit.Assert.assertTrue
import org.junit.Test
import uniffi.epdf_plugin_render.RenderPixelFormat
import uniffi.epdf_plugin_render.RenderedImage

class PageGeometryTest {
    private val size = Size(200.0, 100.0)
    private val turns = listOf(
        RenderRotation.NONE, RenderRotation.CLOCKWISE90, RenderRotation.CLOCKWISE180, RenderRotation.CLOCKWISE270,
    )

    @Test
    fun normalizesRotationToAQuarterTurn() {
        assertEquals(270, PageGeometry(rotation = -90, pageSize = size).rotation)
        assertEquals(90, PageGeometry(rotation = 450, pageSize = size).rotation)
        assertEquals(0, PageGeometry(rotation = 359, pageSize = size).rotation)
        PageGeometry(rotation = Int.MAX_VALUE, pageSize = size)
        PageGeometry(rotation = Int.MIN_VALUE, pageSize = size)
    }

    @Test
    fun degreesReduceToAQuarterTurn() {
        assertEquals(RenderRotation.CLOCKWISE270, RenderRotation.fromDegrees(-90))
        assertEquals(RenderRotation.CLOCKWISE90, RenderRotation.fromDegrees(450))
        assertEquals(RenderRotation.CLOCKWISE90, RenderRotation.fromDegrees(89.6))
        assertEquals(RenderRotation.CLOCKWISE270, RenderRotation.fromDegrees(-90.4))
        // `Int.MAX_VALUE` is 127 modulo 360, nearest 90, and `Int.MIN_VALUE`
        // is -128, which reduces to 232, nearest 270. (Swift's 64-bit `Int`
        // extremes land on 0 instead.)
        assertEquals(RenderRotation.CLOCKWISE90, RenderRotation.fromDegrees(Int.MAX_VALUE))
        assertEquals(RenderRotation.CLOCKWISE270, RenderRotation.fromDegrees(Int.MIN_VALUE))
        assertEquals(RenderRotation.NONE, RenderRotation.fromDegrees(Double.NaN))
        assertEquals(RenderRotation.NONE, RenderRotation.fromDegrees(Double.POSITIVE_INFINITY))
        assertEquals(RenderRotation.NONE, RenderRotation.fromDegrees(Double.NEGATIVE_INFINITY))
        // 1e12 is 280 modulo 360.
        assertEquals(RenderRotation.CLOCKWISE270, RenderRotation.fromDegrees(1e12))
    }

    @Test
    fun turnReportsItsDegrees() {
        assertEquals(listOf(0, 90, 180, 270), turns.map { it.degrees })
        assertEquals(listOf(false, true, false, true), turns.map { it.isQuarterTurn })
        val geometry = PageGeometry(RenderRotation.CLOCKWISE270, size)
        assertEquals(270, geometry.rotation)
        assertEquals(PageGeometry(rotation = 270, pageSize = size), geometry)
    }

    @Test
    fun pointsRoundTrip() {
        for (rotation in listOf(0, 90, 180, 270)) {
            val geometry = PageGeometry(rotation = rotation, pageSize = size)
            val point = Point(30.0, 20.0)
            assertEquals(point, geometry.toPage(geometry.toDisplay(point)))
        }
    }

    @Test
    fun rectsRoundTrip() {
        for (turn in turns) {
            val geometry = PageGeometry(turn, size)
            val rect = Rect(10.0, 20.0, 30.0, 40.0)
            assertEquals(rect, geometry.toPage(geometry.toDisplay(rect)))
        }
    }

    @Test
    fun quarterTurnMapsTopLeftToTopRight() {
        val geometry = PageGeometry(rotation = 90, pageSize = size)
        assertEquals(Size(100.0, 200.0), geometry.displaySize)
        assertEquals(Point(100.0, 0.0), geometry.toDisplay(Point(0.0, 0.0)))
        assertEquals(Rect(40.0, 10.0, 40.0, 30.0), geometry.toDisplay(Rect(10.0, 20.0, 30.0, 40.0)))
        assertEquals(RenderRotation.CLOCKWISE90, geometry.turn)
    }

    private fun image(width: Int, height: Int, vararg rgba: Int): PageImage {
        val bytes = ByteArray(rgba.size) { rgba[it].toByte() }
        return PageImage.from(RenderedImage(width.toUInt(), height.toUInt(), (width * 4).toUInt(), 1.0, RenderPixelFormat.RGBA8, bytes))
    }

    @Test
    fun imageSwapsItsSidesOnAQuarterTurn() {
        val turned = PageGeometry(RenderRotation.CLOCKWISE90, size).toDisplay(image(4, 2, *IntArray(32)))
        assertEquals(listOf(2, 4), listOf(turned.width, turned.height))
    }

    @Test
    fun imageTurnsClockwiseWithThePage() {
        // A 2x1 image: left pixel red, right pixel blue. A clockwise quarter
        // turn makes it 1x2 with red on top.
        val red = 0xffff0000.toInt()
        val blue = 0xff0000ff.toInt()
        val source = image(2, 1, 255, 0, 0, 255, 0, 0, 255, 255)
        assertSame(source, PageGeometry(rotation = 0, pageSize = size).toDisplay(source))
        val turned = PageGeometry(rotation = 90, pageSize = size).toDisplay(source)
        assertEquals(listOf(1, 2), listOf(turned.width, turned.height))
        assertEquals(red, turned.pixel(0, 0))
        assertEquals(blue, turned.pixel(0, 1))
        val half = PageGeometry(rotation = 180, pageSize = size).toDisplay(source)
        assertEquals(listOf(blue, red), listOf(half.pixel(0, 0), half.pixel(1, 0)))
        val back = PageGeometry(rotation = 270, pageSize = size).toDisplay(source)
        assertEquals(listOf(blue, red), listOf(back.pixel(0, 0), back.pixel(0, 1)))
    }

    @Test
    fun rotatedPageReportsItsTurnAndRendersToMatch() = viewerTest {
        val viewer = started(ViewerConfig(render = RenderConfig(), pageEdit = true))
        val document = viewer.open(Fixture.bytes("report"))
        val edit = viewer.pageEdit(document)
        val before = viewer.pages(document)[0]
        edit.setRotation(PageEditRotation.CLOCKWISE90, of = listOf(before.page))
        val page = viewer.pages(document)[0]
        val geometry = page.geometry
        assertEquals(90, geometry.rotation)
        assertEquals(Size(before.height, before.width), geometry.displaySize)
        val corner = Point(10.0, 20.0)
        assertEquals(corner, geometry.toPage(geometry.toDisplay(corner)))
        val render = viewer.render(document)
        // report.pdf is portrait (612 by 792); turned, it renders landscape.
        val rendered = render.renderPage(page.page, RenderPageOptions(rotation = geometry.turn, width = 200.0))
        assertTrue(rendered.image.width > rendered.image.height)
        val thumbnail = render.renderThumbnail(page.page)
        assertTrue(thumbnail.image.height > thumbnail.image.width)
        val turned = geometry.toDisplay(thumbnail.image)
        assertEquals(thumbnail.image.height, turned.width)
        assertEquals(thumbnail.image.width, turned.height)
        viewer.destroy()
    }
}
