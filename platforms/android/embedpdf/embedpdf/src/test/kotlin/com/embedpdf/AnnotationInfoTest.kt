package com.embedpdf

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * The common members of `AnnotationInfoTests`; the subtype members (`details`)
 * arrive with the typed kinds.
 */
class AnnotationInfoTest {
    companion object {
        val page = PageRef(1u)
        val ref = AnnotationRefObjectNumber(page, 12u)
        val parent = AnnotationRefObjectNumber(page, 7u)

        const val INK_JSON = """{"subtype":"ink","ref":{"kind":"objectNumber","page":{"kind":"objectNumber","objectNumber":1},"objectNumber":12},"rect":{"x":-1,"y":-1,"width":22,"height":7.000000000000001},"color":"#e5484d","opacity":0.5,"strokeWidth":2,"borderStyle":"dashed","dashArray":[3,1],"inkList":[[{"x":0,"y":0},{"x":10,"y":5}],[{"x":20,"y":0}]],"intent":"ink-highlight","rotation":30,"blendMode":"multiply","customMember":{"kept":true}}"""

        fun record(
            subtype: String = "ink",
            createdAt: String? = "2025-03-04T05:06:07Z",
            modifiedAt: String? = "2025-03-04T10:36:07+05:30",
            fieldsJson: String = INK_JSON,
        ): AnnotationRecord = AnnotationRecord(
            annotationRef = ref, page = page, subtype = subtype,
            rect = Rect(-1.0, -1.0, 22.0, 7.000000000000001), contents = "Ink", author = "Reviewer",
            createdAt = createdAt, modifiedAt = modifiedAt, groupId = "group-1", inReplyTo = parent,
            canUpdate = true, canDelete = false, fieldsJson = fieldsJson,
        )

        /** The instant [text] reads as through [AnnotationInfo]. */
        fun instant(text: String?): Long? = AnnotationInfo(record(createdAt = text)).createdAt
    }

    @Test
    fun everyCommonMemberMapsFromTheGeneratedRecord() {
        val info = AnnotationInfo(record())
        assertEquals(ref, info.annotationRef)
        assertEquals(page, info.page)
        assertEquals(AnnotationSubtype.INK, info.subtype)
        assertEquals(Rect(-1.0, -1.0, 22.0, 7.000000000000001), info.rect)
        assertEquals("Ink", info.contents)
        assertEquals("Reviewer", info.author)
        assertEquals(1_741_064_767_000L, info.createdAt)
        assertEquals("2025-03-04T05:06:07Z", info.rawCreatedAt)
        assertEquals(1_741_064_767_000L, info.modifiedAt)
        assertEquals("2025-03-04T10:36:07+05:30", info.rawModifiedAt)
        assertEquals("group-1", info.groupId)
        assertEquals(parent, info.inReplyTo)
        assertEquals(true, info.canUpdate)
        assertEquals(false, info.canDelete)
        assertEquals(AnnotationBlendMode.MULTIPLY, info.blendMode)
        assertEquals(INK_JSON, info.fieldsJson)
    }

    @Test
    fun aBlendModeThisDoesNotReadIsNull() {
        for (json in listOf("{}", """{"blendMode":"sparkle"}""", """{"blendMode":3}""", "", "not json")) {
            assertNull(json, AnnotationInfo(record(fieldsJson = json)).blendMode)
        }
    }

    @Test
    fun anUnknownSubtypeKeepsItsName() {
        val json = """{"subtype":"sparkle","glow":3}"""
        val info = AnnotationInfo(record(subtype = "sparkle", fieldsJson = json))
        assertEquals(AnnotationSubtype("sparkle"), info.subtype)
        assertEquals("sparkle", info.record.subtype)
        assertEquals(record(subtype = "sparkle", fieldsJson = json), info.record)
    }

    @Test
    fun theRoundTripLosesNothing() {
        val records = listOf(
            record(),
            record(createdAt = null, modifiedAt = null),
            record(createdAt = "not a date", modifiedAt = "2025-02-29T00:00:00Z"),
            record(subtype = "sparkle", fieldsJson = "{}"),
            AnnotationRecord(
                annotationRef = ref, page = page, subtype = "", rect = Rect(0.0, 0.0, 0.0, 0.0), contents = null,
                author = null, createdAt = null, modifiedAt = null, groupId = null, inReplyTo = null,
                canUpdate = false, canDelete = true, fieldsJson = "",
            ),
        )
        for (record in records) {
            val info = AnnotationInfo(record)
            assertEquals(record, info.record)
            assertEquals(info, AnnotationInfo(info.record))
            assertEquals(info.hashCode(), AnnotationInfo(info.record).hashCode())
        }
    }

    @Test
    fun theEnginesTimestampFormsRead() {
        val instant = 1_741_064_767_000L
        // What `pdf_date_to_iso` writes: `Z`, an offset, or none (UTC).
        assertEquals(instant, instant("2025-03-04T05:06:07Z"))
        assertEquals(instant, instant("2025-03-04T10:36:07+05:30"))
        assertEquals(instant, instant("2025-03-04T00:06:07-05:00"))
        assertEquals(instant, instant("2025-03-04T05:06:07"))
        // What the engine takes in: a fraction, no seconds, an offset without its colon.
        assertEquals(1_741_064_767_250L, instant("2025-03-04T05:06:07.25Z"))
        assertEquals(1_741_064_767_123L, instant("2025-03-04T05:06:07.123456Z"))
        assertEquals(1_741_064_760_000L, instant("2025-03-04T05:06Z"))
        assertEquals(instant, instant("2025-03-04T10:36:07+0530"))
        assertEquals(1_709_251_140_000L, instant("2024-02-29T23:59:00Z"))
        assertEquals(-3_600_000L, instant("1970-01-01T00:00:00+01:00"))
    }

    @Test
    fun aMalformedTimestampKeepsItsText() {
        for (text in listOf(
            "2025-02-29T00:00:00Z", "2025-03-04T24:00:00Z", "2025-13-01T00:00:00Z",
            "2025-03-04T05:06:07+24:00", "D:20250304050607Z", "2025-03-04T05:06:07z",
            "2025-03-04T05:06:07+5:30", "2025-03-04T05:06:07Z ", "2025-03-04", "yesterday", "",
            "2025-03-04T05:06:07.Z", "２０２５-03-04T05:06:07Z",
        )) {
            val info = AnnotationInfo(record(createdAt = text, modifiedAt = text))
            assertNull(text, info.createdAt)
            assertEquals(text, info.rawCreatedAt)
            assertNull(text, info.modifiedAt)
            assertEquals(text, info.rawModifiedAt)
            assertEquals(text, info.record.createdAt)
        }
    }

    @Test
    fun anAbsentTimestampIsNull() {
        val info = AnnotationInfo(record(createdAt = null, modifiedAt = null))
        assertNull(info.createdAt)
        assertNull(info.rawCreatedAt)
        assertNull(info.modifiedAt)
        assertNull(info.rawModifiedAt)
    }
}
