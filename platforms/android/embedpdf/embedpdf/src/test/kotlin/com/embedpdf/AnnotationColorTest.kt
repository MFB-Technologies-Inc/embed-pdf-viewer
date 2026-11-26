package com.embedpdf

import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertThrows
import org.junit.Test

class AnnotationColorTest {
    @Test
    fun hexReadsEitherCaseAndWritesLowercase() {
        val color = AnnotationColor.fromHex("#1F6fEb")
        assertEquals(AnnotationColor(0x1f, 0x6f, 0xeb), color)
        assertEquals("#1f6feb", color?.hex)
        assertEquals("#000aff", AnnotationColor(0, 10, 255).hex)
    }

    @Test
    fun anythingButHashAndSixHexDigitsIsRefused() {
        for (text in listOf("", "#", "1f6feb", "#1f6fe", "#1f6feb0", "#1f6fez", "#ffffff ", " #ffffff", "#ｆｆｆｆｆｆ", "#١٢٣٤٥٦")) {
            assertNull(text, AnnotationColor.fromHex(text))
            assertThrows(text, IllegalArgumentException::class.java) { AnnotationColor(text) }
        }
    }

    @Test
    fun aHexConstantIsTheColourItNames() {
        val color = AnnotationColor("#E5484D")
        assertEquals(AnnotationColor(0xe5, 0x48, 0x4d), color)
        assertEquals("#e5484d", "$color")
        assertEquals(color.hashCode(), AnnotationColor(0xe5, 0x48, 0x4d).hashCode())
    }

    @Test
    fun aChannelOutsideAByteIsRefused() {
        assertThrows(IllegalArgumentException::class.java) { AnnotationColor(256, 0, 0) }
        assertThrows(IllegalArgumentException::class.java) { AnnotationColor(0, -1, 0) }
    }

    @Test
    fun itCodesAsItsHexString() {
        assertEquals("\"#ff0000\"", Json.encodeToString(AnnotationColor.serializer(), AnnotationColor(255, 0, 0)))
        assertEquals(AnnotationColor("#00ff00"), Json.decodeFromString(AnnotationColor.serializer(), "\"#00FF00\""))
        assertThrows(SerializationException::class.java) { Json.decodeFromString(AnnotationColor.serializer(), "\"green\"") }
    }

    /** Swift's `cgColorIsOpaqueSRGB`, for Android's colour ints. */
    @Test
    fun aColorIntIsOpaqueAndDropsAlphaOnTheWayIn() {
        assertEquals(0xffff0033.toInt(), AnnotationColor(255, 0, 51).toColorInt())
        assertEquals(AnnotationColor(0x12, 0x34, 0x56), AnnotationColor.fromColorInt(0x80123456.toInt()))
        val color = AnnotationColor("#abcdef")
        assertEquals(color, AnnotationColor.fromColorInt(color.toColorInt()))
    }
}
