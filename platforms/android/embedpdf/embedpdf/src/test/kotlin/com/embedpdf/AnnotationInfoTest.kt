package com.embedpdf

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/** `AnnotationInfoTests`: the common members, the timestamps and [AnnotationInfo.details]. */
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

    // ── details ──

    @Test
    fun theInkMembersAreTyped() {
        val info = AnnotationInfo(record())
        assertNull(info.highlight)
        assertNull(info.note)
        val ink = info.ink!!
        assertEquals(AnnotationColor("#e5484d"), ink.color)
        assertEquals(0.5, ink.opacity, 0.0)
        assertEquals(2.0, ink.strokeWidth, 0.0)
        assertEquals(AnnotationInfo.BorderStyle.DASHED, ink.borderStyle)
        assertEquals(listOf(3.0, 1.0), ink.dashArray)
        assertEquals(listOf(listOf(Point(0.0, 0.0), Point(10.0, 5.0)), listOf(Point(20.0, 0.0))), ink.inkList)
        assertEquals(AnnotationIntent.INK_HIGHLIGHT, ink.intent)
        assertEquals(30.0, ink.rotation)
        assertEquals(ink, info.details)
    }

    @Test
    fun inkWithoutAColorIsStillInk() {
        // `/C` left out or empty reads as `"color": null`, and the other
        // optional members as `null` too.
        val json = """{"subtype":"ink","color":null,"opacity":1,"strokeWidth":1,"borderStyle":"solid","dashArray":null,"inkList":[[{"x":1,"y":2}]],"intent":null,"rotation":null}"""
        val record = record(fieldsJson = json)
        val info = AnnotationInfo(record)
        val ink = info.ink!!
        assertNull(ink.color)
        assertNull(ink.dashArray)
        assertNull(ink.intent)
        assertNull(ink.rotation)
        assertEquals(listOf(listOf(Point(1.0, 2.0))), ink.inkList)
        assertEquals(record, info.record)
        assertEquals(info, AnnotationInfo(info.record))
    }

    @Test
    fun aHighlightsQuadsAreTyped() {
        val json = """{"subtype":"highlight","color":"#ffcd45","opacity":1,"quadPoints":[{"upperLeft":{"x":10,"y":10},"upperRight":{"x":110,"y":10},"lowerLeft":{"x":10,"y":22},"lowerRight":{"x":110,"y":22}}]}"""
        val info = AnnotationInfo(record(subtype = "highlight", fieldsJson = json))
        assertEquals(AnnotationColor("#ffcd45"), info.highlight?.color)
        assertEquals(1.0, info.highlight?.opacity)
        assertEquals(listOf(Quad(Point(10.0, 10.0), Point(110.0, 10.0), Point(10.0, 22.0), Point(110.0, 22.0))), info.highlight?.quadPoints)
        assertNull(info.ink)
    }

    @Test
    fun aTextAnnotationIsANote() {
        val json = """{"subtype":"text","color":"#e5484d","opacity":1,"icon":"key","open":true,"state":"accepted","stateModel":"review"}"""
        val info = AnnotationInfo(record(subtype = "text", fieldsJson = json))
        assertEquals(AnnotationSubtype.TEXT, info.subtype)
        assertEquals(AnnotationInfo.Note(AnnotationColor("#e5484d"), 1.0, "key", true, "accepted", "review", null), info.note)
    }

    @Test
    fun membersOfAnotherShapeAreLeftInFieldsJson() {
        // `inkList` is not a list of strokes, so the ink members don't read.
        val json = """{"subtype":"ink","color":"#e5484d","opacity":1,"strokeWidth":2,"borderStyle":"solid","inkList":"none"}"""
        val info = AnnotationInfo(record(fieldsJson = json))
        assertEquals(AnnotationSubtype.INK, info.subtype)
        assertEquals(AnnotationInfo.Details.Untyped, info.details)
        assertEquals(json, info.fieldsJson)
        assertEquals(AnnotationInfo.Details.Untyped, AnnotationInfo(record(fieldsJson = "not json")).details)
    }

    @Test
    fun anIntentOrBorderStyleWithoutAConstantKeepsItsText() {
        val json = """{"subtype":"ink","color":null,"opacity":1,"strokeWidth":1,"borderStyle":"engraved","dashArray":null,"inkList":[[{"x":1,"y":2}]],"intent":"ink-signature","rotation":null}"""
        val ink = AnnotationInfo(record(fieldsJson = json)).ink!!
        assertEquals(AnnotationIntent("ink-signature"), ink.intent)
        assertEquals("ink-signature", ink.intent?.value)
        assertEquals(AnnotationInfo.BorderStyle("engraved"), ink.borderStyle)
        assertEquals("engraved", ink.borderStyle.toString())
    }

    @Test
    fun theConstantsAreTheWriteSideSpellings() {
        val intents = listOf(
            AnnotationIntent.INK_HIGHLIGHT, AnnotationIntent.STRIKEOUT_TEXT_EDIT, AnnotationIntent.LINE_ARROW,
            AnnotationIntent.LINE_DIMENSION, AnnotationIntent.POLYGON_CLOUD, AnnotationIntent.POLYGON_DIMENSION,
            AnnotationIntent.POLYLINE_DIMENSION, AnnotationIntent.FREE_TEXT, AnnotationIntent.FREE_TEXT_CALLOUT,
            AnnotationIntent.REPLACE,
        )
        val written = AnnotationInkIntent.entries.map { it.value } + AnnotationStrikeoutIntent.entries.map { it.value } +
            AnnotationLineIntent.entries.map { it.value } + AnnotationPolygonIntent.entries.map { it.value } +
            AnnotationPolylineIntent.entries.map { it.value } + AnnotationFreeTextIntent.entries.map { it.value } +
            AnnotationCaretIntent.entries.map { it.value }
        assertEquals(written, intents.map { it.value })
        // A read also names what no draft writes.
        assertEquals("free-text-typewriter", AnnotationIntent.FREE_TEXT_TYPEWRITER.value)
        val styles = listOf(
            AnnotationInfo.BorderStyle.SOLID, AnnotationInfo.BorderStyle.DASHED, AnnotationInfo.BorderStyle.BEVELED,
            AnnotationInfo.BorderStyle.INSET,
        )
        assertEquals(listOf("solid", "dashed", "beveled", "inset"), styles.map { it.value })
        assertEquals(AnnotationBorderStyle.entries.map { it.value }, styles.take(2).map { it.value })
    }

    @Test
    fun anUnknownSubtypeIsUntyped() {
        val info = AnnotationInfo(record(subtype = "sparkle", fieldsJson = """{"subtype":"sparkle","glow":3}"""))
        assertEquals(AnnotationInfo.Details.Untyped, info.details)
        assertNull(info.ink)
    }

    @Test
    fun everyKindReadsItsMembers() {
        val quad = """{"upperLeft":{"x":1,"y":2},"upperRight":{"x":3,"y":2},"lowerLeft":{"x":1,"y":4},"lowerRight":{"x":3,"y":4}}"""
        val q = Quad(Point(1.0, 2.0), Point(3.0, 2.0), Point(1.0, 4.0), Point(3.0, 4.0))
        val box = """{"x":1,"y":2,"width":3,"height":4}"""
        val r = Rect(1.0, 2.0, 3.0, 4.0)
        val red = AnnotationColor("#ff0000")
        fun details(subtype: String, json: String) = AnnotationInfo(record(subtype = subtype, fieldsJson = json)).details

        assertEquals(
            AnnotationInfo.Underline(red, 0.5, listOf(q), null),
            details("underline", """{"color":"#ff0000","opacity":0.5,"quadPoints":[$quad]}"""),
        )
        assertEquals(
            AnnotationInfo.Squiggly(red, 0.5, listOf(q), null),
            details("squiggly", """{"color":"#ff0000","opacity":0.5,"quadPoints":[$quad]}"""),
        )
        assertEquals(
            AnnotationInfo.Strikeout(red, 0.5, listOf(q), AnnotationIntent.STRIKEOUT_TEXT_EDIT, null),
            details("strikeout", """{"color":"#ff0000","opacity":0.5,"quadPoints":[$quad],"intent":"strikeout-text-edit"}"""),
        )
        val stroke = """"color":null,"opacity":1,"strokeWidth":2,"borderStyle":"beveled","dashArray":null,"interiorColor":"#ff0000","rotation":null"""
        assertEquals(
            AnnotationInfo.Square(null, 1.0, 2.0, AnnotationInfo.BorderStyle.BEVELED, null, red, r, null, 2.0, null),
            details("square", """{$stroke,"box":$box,"cloudyIntensity":2}"""),
        )
        val circle = AnnotationInfo(record(subtype = "circle", fieldsJson = """{$stroke,"box":$box}"""))
        assertEquals(AnnotationInfo.Circle(null, 1.0, 2.0, AnnotationInfo.BorderStyle.BEVELED, null, red, r, null, null, null), circle.circle)
        assertTrue(circle.details is AnnotationInfo.Shape)
        assertNull(circle.square)
        val line = details(
            "line",
            """{$stroke,"linePoints":{"start":{"x":1,"y":2},"end":{"x":3,"y":4}},"lineEndings":{"start":"none","end":"open-arrow"},"intent":"line-dimension","captionEnabled":false,"captionPosition":"inline","captionOffset":{"along":1,"perpendicular":2},"leader":{"length":5,"extension":0,"offset":0},"measure":{"subtype":"geospatial"}}""",
        )
        assertEquals(
            AnnotationInfo.Line(
                null, 1.0, 2.0, AnnotationInfo.BorderStyle.BEVELED, null, red, Point(1.0, 2.0), Point(3.0, 4.0),
                AnnotationLineEndings(AnnotationLineEnding.NONE, AnnotationLineEnding.OPEN_ARROW), null, AnnotationIntent.LINE_DIMENSION,
                false, AnnotationCaptionPosition.INLINE, AnnotationCaptionOffset(1.0, 2.0), AnnotationLineLeader(5.0, 0.0, 0.0), null,
                PdfMeasurement("geospatial"),
            ),
            line,
        )
        assertEquals(
            AnnotationInfo.Polygon(
                null, 1.0, 2.0, AnnotationInfo.BorderStyle.BEVELED, null, red, listOf(Point(1.0, 2.0)), null, null, null, true,
                Point(5.0, 6.0), null, null,
            ),
            details("polygon", """{$stroke,"vertices":[{"x":1,"y":2}],"captionEnabled":true,"captionCenter":{"x":5,"y":6}}"""),
        )
        assertEquals(
            AnnotationInfo.Polyline(
                null, 1.0, 2.0, AnnotationInfo.BorderStyle.BEVELED, null, red, listOf(Point(1.0, 2.0)),
                AnnotationLineEndings(AnnotationLineEnding.SLASH, AnnotationLineEnding.BUTT), null, null, null, null, null, null,
            ),
            details("polyline", """{$stroke,"vertices":[{"x":1,"y":2}],"lineEndings":{"start":"slash","end":"butt"},"captionCenter":null}"""),
        )
        val freeText = details(
            "free-text",
            """{"box":$box,"intent":"free-text-typewriter","fontFamily":"helvetica","fontSize":12,"fontColor":"#000000","textAlign":"left","verticalAlign":"top","color":"#ff0000","interiorColor":null,"opacity":1,"strokeWidth":1,"borderStyle":"solid","calloutLine":[{"x":1,"y":2},{"x":3,"y":4}],"lineEnding":"none","richText":{"body":{"size":12},"paragraphs":[{"runs":[{"text":"Hi"}]}]}}""",
        ) as AnnotationInfo.FreeText
        assertEquals(AnnotationIntent.FREE_TEXT_TYPEWRITER, freeText.intent)
        assertEquals(AnnotationCalloutLine.Straight(Point(1.0, 2.0), Point(3.0, 4.0)), freeText.calloutLine)
        assertEquals("Hi", freeText.richText?.plainText)
        assertEquals(
            AnnotationInfo.Caret(red, 1.0, r, 90.0, AnnotationIntent.REPLACE, null),
            details("caret", """{"color":"#ff0000","opacity":1,"box":$box,"rotation":90,"intent":"replace"}"""),
        )
        assertEquals(
            AnnotationInfo.Stamp("Approved", AnnotationStampFit.COVER, 1.0, r, null, null),
            details("stamp", """{"name":"Approved","fit":"cover","opacity":1,"box":$box}"""),
        )
        assertEquals(
            AnnotationInfo.Link(AnnotationLinkTarget.Uri("https://example.com")),
            details("link", """{"target":{"kind":"uri","uri":"https://example.com"}}"""),
        )
        // A target a draft can't write reads as `null`.
        assertEquals(AnnotationInfo.Link(null), details("link", """{"target":{"kind":"launch","file":"a.exe"}}"""))
        val attachment = details(
            "file-attachment",
            """{"icon":"paperclip","color":"#ff0000","opacity":1,"file":{"name":"a.txt","sizeBytes":12,"createdAt":"2025-03-04T05:06:07Z"}}""",
        ) as AnnotationInfo.FileAttachment
        assertEquals(AnnotationFileAttachmentIcon.PAPERCLIP, attachment.icon)
        assertEquals(12L, attachment.file?.sizeBytes)
        assertEquals(1_741_064_767_000L, attachment.file?.createdAt)
        assertNull(attachment.file?.modifiedAt)
        assertEquals(
            AnnotationInfo.Redaction(listOf(q), red, 1.0, null, "X", false, "helvetica", 0.0, red, AnnotationTextAlignment.LEFT, null),
            details(
                "redact",
                """{"quadPoints":[$quad],"color":"#ff0000","opacity":1,"overlayText":"X","repeat":false,"fontFamily":"helvetica","fontSize":0,"fontColor":"#ff0000","textAlign":"left"}""",
            ),
        )
        // A required member missing leaves the record untyped.
        assertEquals(AnnotationInfo.Details.Untyped, details("caret", """{"color":"#ff0000","opacity":1}"""))
    }

    @Test
    fun theFixturesIntentsAndBorderStylesReadAsConstants() = viewerTest {
        val (viewer, document, annotations) = AnnotationTest.opened()
        val infos = annotations.list()

        // The fixture's line names no `/IT`, so its intent is absent.
        val line = infos.firstNotNullOf { it.line }
        assertNull(line.intent)
        assertEquals(AnnotationInfo.BorderStyle.SOLID, line.borderStyle)

        assertEquals(listOf(AnnotationIntent.INK_HIGHLIGHT), infos.mapNotNull { it.ink?.intent })
        assertEquals(listOf(AnnotationIntent.STRIKEOUT_TEXT_EDIT), infos.mapNotNull { it.strikeout?.intent })
        assertEquals(listOf(AnnotationIntent.REPLACE), infos.mapNotNull { it.caret?.intent })
        assertTrue(infos.any { it.freeText?.intent == AnnotationIntent.FREE_TEXT_CALLOUT })
        val styles = infos.mapNotNull { it.square?.borderStyle ?: it.circle?.borderStyle ?: it.ink?.borderStyle ?: it.freeText?.borderStyle }.toSet()
        assertTrue(styles.toString(), styles.all { it in setOf(AnnotationInfo.BorderStyle.SOLID, AnnotationInfo.BorderStyle.DASHED, AnnotationInfo.BorderStyle.BEVELED, AnnotationInfo.BorderStyle.INSET) })
        assertTrue(AnnotationInfo.BorderStyle.DASHED in styles)

        // A created line reads its intent back as the constant.
        val page = viewer.pages(document)[0].page
        val arrow = annotations.create(
            LineAnnotationDraft(Point(100.0, 100.0), Point(200.0, 150.0), intent = value(AnnotationLineIntent.LINE_ARROW)).json(),
            page,
        )
        val created = checkNotNull(annotations.get(arrow)?.line)
        assertEquals(AnnotationIntent.LINE_ARROW, created.intent)
        viewer.destroy()
    }
}
