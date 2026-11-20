package com.embedpdf

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/** Indexes the engine carries as `UInt` that no `Int` or `Long` argument may pass. */
private val outsideUInt = listOf(-1L, UInt.MAX_VALUE.toLong() + 1, Long.MAX_VALUE)

class IntegerSurfaceTest {
    @Test
    fun aValueOutsideTheWireTypeThrowsInvalidInput() {
        for (value in outsideUInt) {
            try {
                value.toWireUInt("index")
                throw AssertionError("$value converted")
            } catch (e: PdfException) {
                assertEquals(PdfException.Code.INVALID_INPUT, e.code)
                assertEquals("index $value is outside 0...4294967295", e.detail)
            }
        }
        assertEquals(0u, 0.toWireUInt("index"))
        assertEquals(Int.MAX_VALUE.toUInt(), Int.MAX_VALUE.toWireUInt("index"))
        assertEquals(UInt.MAX_VALUE, UInt.MAX_VALUE.toLong().toWireUInt("index"))
        try {
            (-1).toWireUInt("index")
            throw AssertionError("-1 converted")
        } catch (e: PdfException) {
            assertEquals(PdfException.Code.INVALID_INPUT, e.code)
        }
    }

    @Test
    fun aResultPastIntThrowsInvalidState() {
        for (convert in listOf({ ULong.MAX_VALUE.toIntResult("count") }, { Long.MAX_VALUE.toIntResult("count") }, { UInt.MAX_VALUE.toIntResult("count") })) {
            try {
                convert()
                throw AssertionError("converted")
            } catch (e: PdfException) {
                assertEquals(PdfException.Code.INVALID_STATE, e.code)
            }
        }
        assertEquals(Int.MAX_VALUE, Int.MAX_VALUE.toUInt().toIntResult("count"))
        assertEquals(-1, (-1L).toIntResult("index"))
    }

    @Test
    fun viewerPagesAndTabsTakeAndGiveInt() = viewerTest {
        val viewer = started()
        val first = viewer.open(Fixture.bytes("report"))
        val second = viewer.open(Fixture.bytes("colors"), activate = false)
        assertEquals(2, viewer.documentCount())
        // The engine's page index is a `Long`, so every `Int` reaches it: one
        // below zero or past the last page has no page.
        assertNull(viewer.page(at = -1, document = first))
        assertNull(viewer.page(at = Int.MAX_VALUE, document = first))
        assertNull(viewer.page(at = Int.MIN_VALUE, document = first))
        val page = viewer.page(at = 0, document = first)!!
        assertEquals(0, viewer.index(of = page.page, document = first))
        assertEquals(-1, viewer.index(of = page.page, document = second))
        // A tab position is clamped into the order at both ends.
        viewer.move(first, toIndex = Int.MAX_VALUE)
        assertEquals(listOf(second.id, first.id), viewer.documentOrder())
        viewer.move(first, toIndex = -1)
        assertEquals(listOf(first.id, second.id), viewer.documentOrder())
        viewer.move(first, toIndex = 0)
        assertEquals(listOf(first.id, second.id), viewer.documentOrder())
        viewer.destroy()
    }
}
