package com.embedpdf

import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonPrimitive
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertThrows
import org.junit.Test

/**
 * The typed patches read back, as `AnnotationPatchDecodingTests`: a patch
 * reads from the field bag it writes, and
 * [AnnotationToolsCapability.defaults] reads a tool's defaults as one.
 */
class AnnotationPatchDecodingTest {
    companion object {
        val quad = AnnotationDraftTest.quad
        val comment = AnnotationRichText(
            body = mapOf("family" to JsonPrimitive("helvetica"), "size" to JsonPrimitive(12)),
            paragraphs = listOf(
                AnnotationRichText.Paragraph(
                    runs = listOf(
                        AnnotationRichText.Run("Styled "),
                        AnnotationRichText.Run("comment", mapOf("weight" to JsonPrimitive(700))),
                    ),
                    properties = mapOf("align" to JsonPrimitive("center")),
                ),
            ),
        )

        /** Every common member set, two of them to [Nullable.Clear]. */
        val fullCommon = AnnotationCommonFields(
            nm = value("patch-1"),
            subject = Nullable.Clear,
            reply = value(AnnotationReply(AnnotationRefObjectNumber(PageRef(2u), 9u), AnnotationReplyType.GROUP)),
            groupId = value("reviewers"),
            invisible = false, hidden = true, print = false, noZoom = true, noRotate = true, noView = false,
            readOnly = true, locked = true, toggleNoView = false, lockedContents = true,
        )
    }

    /**
     * [patch], whose `common` is [fullCommon], is itself after `json()` and a
     * read, but for `common.nm`: a patch leaves it out of its JSON, so it
     * comes back [Nullable.Unchanged]. [withoutName] is [patch] with that
     * name left out.
     */
    private fun <P : AnnotationPatch> expectRoundTrip(patch: P, withoutName: P, reader: AnnotationPatch.Reader<P>) {
        val read = reader.fromJson(patch.json())
        assertEquals(withoutName, read)
        assertEquals(patch.json(), read.json())
    }

    private val unnamed = fullCommon.copy(nm = Nullable.Unchanged)

    // ── every patch round-trips ──

    @Test
    fun inkHighlightAndNotePatchesRoundTrip() {
        val ink = InkAnnotationPatch(
            inkList = listOf(AnnotationDraftTest.stroke, listOf(Point(1.5, 2.0))), color = value(AnnotationColor("#1f6feb")),
            opacity = 0.75, strokeWidth = 3.0, borderStyle = AnnotationBorderStyle.DASHED, dashArray = Nullable.Clear,
            intent = value(AnnotationInkIntent.INK_HIGHLIGHT), rotation = value(30.0), contents = value("Ink text"),
            blendMode = AnnotationBlendMode.SCREEN, richText = value(comment), common = fullCommon,
        )
        expectRoundTrip(ink, ink.copy(common = unnamed), InkAnnotationPatch)
        val highlight = HighlightAnnotationPatch(
            quadPoints = listOf(quad), color = AnnotationColor("#ffcd45"), opacity = 0.5, contents = Nullable.Clear,
            blendMode = AnnotationBlendMode.MULTIPLY, richText = value(comment), common = fullCommon,
        )
        expectRoundTrip(highlight, highlight.copy(common = unnamed), HighlightAnnotationPatch)
        val note = NoteAnnotationPatch(
            rect = AnnotationDraftTest.noteRect, color = AnnotationColor("#e5484d"), opacity = 0.5,
            icon = AnnotationNoteIcon.NEW_PARAGRAPH, open = true, state = value("accepted"), stateModel = Nullable.Clear,
            contents = value("Typed note"), blendMode = AnnotationBlendMode.DARKEN, richText = Nullable.Clear,
            common = fullCommon,
        )
        expectRoundTrip(note, note.copy(common = unnamed), NoteAnnotationPatch)
    }

    @Test
    fun textMarkupAndRedactionPatchesRoundTrip() {
        val underline = UnderlineAnnotationPatch(
            quadPoints = listOf(quad), color = AnnotationColor("#0000ff"), opacity = 0.5, contents = Nullable.Clear,
            blendMode = AnnotationBlendMode.MULTIPLY, richText = value(comment), common = fullCommon,
        )
        expectRoundTrip(underline, underline.copy(common = unnamed), UnderlineAnnotationPatch)
        val squiggly = SquigglyAnnotationPatch(
            quadPoints = listOf(quad), color = AnnotationColor("#00ff00"), opacity = 0.75, contents = value("Squiggly text"),
            blendMode = AnnotationBlendMode.DARKEN, richText = Nullable.Clear, common = fullCommon,
        )
        expectRoundTrip(squiggly, squiggly.copy(common = unnamed), SquigglyAnnotationPatch)
        val strikeout = StrikeoutAnnotationPatch(
            quadPoints = listOf(quad), color = AnnotationColor("#ff0000"), opacity = 0.25,
            intent = value(AnnotationStrikeoutIntent.STRIKEOUT_TEXT_EDIT), contents = Nullable.Clear,
            blendMode = AnnotationBlendMode.SCREEN, richText = value(comment), common = fullCommon,
        )
        expectRoundTrip(strikeout, strikeout.copy(common = unnamed), StrikeoutAnnotationPatch)
        val redaction = RedactionAnnotationPatch(
            quadPoints = listOf(quad), rect = AnnotationKindDraftTest.box, color = AnnotationColor("#112233"), opacity = 0.5,
            interiorColor = Nullable.Clear, overlayText = value("REDACTED"), repeat = true, fontFamily = "courier",
            fontSize = 9.0, fontColor = AnnotationColor("#ffffff"), textAlign = AnnotationTextAlignment.CENTER,
            contents = value("Redaction text"), blendMode = AnnotationBlendMode.NORMAL, richText = value(comment),
            common = fullCommon,
        )
        expectRoundTrip(redaction, redaction.copy(common = unnamed), RedactionAnnotationPatch)
    }

    @Test
    fun boxPatchesRoundTrip() {
        val box = AnnotationKindDraftTest.box
        val freeText = FreeTextAnnotationPatch(
            box = box, contents = value("Styled comment"), fontFamily = "times-roman", fontSize = 14.0,
            fontColor = AnnotationColor("#112233"), textAlign = AnnotationTextAlignment.JUSTIFY,
            verticalAlign = AnnotationVerticalAlignment.MIDDLE, color = AnnotationColor("#445566"),
            interiorColor = value(AnnotationColor("#ffffcc")), opacity = 0.75, strokeWidth = 2.0,
            borderStyle = AnnotationBorderStyle.DASHED, dashArray = value(listOf(2.0, 1.0)), cloudyIntensity = value(1.0),
            rotation = Nullable.Clear, intent = value(AnnotationFreeTextIntent.FREE_TEXT_CALLOUT),
            calloutLine = value(AnnotationCalloutLine.Kneed(Point(20.0, 20.0), Point(60.0, 60.0), Point(100.0, 130.0))),
            lineEnding = value(AnnotationLineEnding.OPEN_ARROW), blendMode = AnnotationBlendMode.MULTIPLY, richText = comment,
            common = fullCommon,
        )
        expectRoundTrip(freeText, freeText.copy(common = unnamed), FreeTextAnnotationPatch)
        val caret = CaretAnnotationPatch(
            box = box, color = AnnotationColor("#ff0000"), opacity = 0.5, rotation = value(90.0),
            intent = value(AnnotationCaretIntent.REPLACE), contents = Nullable.Clear, blendMode = AnnotationBlendMode.MULTIPLY,
            richText = value(comment), common = fullCommon,
        )
        expectRoundTrip(caret, caret.copy(common = unnamed), CaretAnnotationPatch)
        val link = LinkAnnotationPatch(
            rect = box, target = value(AnnotationLinkTarget.Goto(PageDestinationXyz(PageRef(4u), 10.0, null, 2.0))),
            contents = Nullable.Clear, common = fullCommon,
        )
        expectRoundTrip(link, link.copy(common = unnamed), LinkAnnotationPatch)
        val stamp = StampAnnotationPatch(
            box = box, opacity = 0.5, rotation = value(90.0), name = value("Approved"), fit = Nullable.Clear,
            contents = value("Stamp text"), richText = value(comment), common = fullCommon,
        )
        expectRoundTrip(stamp, stamp.copy(common = unnamed), StampAnnotationPatch)
        val attachment = FileAttachmentAnnotationPatch(
            rect = box, icon = AnnotationFileAttachmentIcon.PUSH_PIN, color = AnnotationColor("#facc15"), opacity = 0.5,
            contents = Nullable.Clear, blendMode = AnnotationBlendMode.MULTIPLY, richText = value(comment), common = fullCommon,
        )
        expectRoundTrip(attachment, attachment.copy(common = unnamed), FileAttachmentAnnotationPatch)
    }

    /** A free text's rich text has no clearing: `null` reads as left out. */
    @Test
    fun aFreeTextsNullRichTextReadsAsLeftOut() {
        assertEquals(FreeTextAnnotationPatch(), FreeTextAnnotationPatch.fromJson("""{"richText":null}"""))
    }

    @Test
    fun shapePatchesRoundTrip() {
        val box = AnnotationKindDraftTest.box
        val square = SquareAnnotationPatch(
            box = box, color = Nullable.Clear, opacity = 0.5, strokeWidth = 3.0, borderStyle = AnnotationBorderStyle.DASHED,
            dashArray = value(listOf(4.0, 2.0)), interiorColor = value(AnnotationColor("#00ff00")), rotation = value(15.0),
            cloudyIntensity = value(2.0), contents = value("Square text"), blendMode = AnnotationBlendMode.MULTIPLY,
            richText = value(comment), common = fullCommon,
        )
        expectRoundTrip(square, square.copy(common = unnamed), SquareAnnotationPatch)
        val circle = CircleAnnotationPatch(
            box = box, color = value(AnnotationColor("#0000ff")), opacity = 0.75, strokeWidth = 2.0,
            borderStyle = AnnotationBorderStyle.SOLID, dashArray = value(listOf(1.0)), interiorColor = value(AnnotationColor("#ffff00")),
            rotation = Nullable.Clear, cloudyIntensity = Nullable.Clear, contents = value("Circle text"),
            blendMode = AnnotationBlendMode.OVERLAY, richText = value(comment), common = fullCommon,
        )
        expectRoundTrip(circle, circle.copy(common = unnamed), CircleAnnotationPatch)
        val scale = AnnotationKindDraftTest.scale.copy(origin = MeasureOrigin(3.0, 4.0))
        val line = LineAnnotationPatch(
            start = Point(10.0, 10.0), end = Point(100.0, 50.0), color = value(AnnotationColor("#ff00ff")), opacity = 0.5,
            strokeWidth = 2.0, borderStyle = AnnotationBorderStyle.DASHED, dashArray = value(listOf(3.0, 1.0)),
            interiorColor = Nullable.Clear,
            lineEndings = AnnotationLineEndings(AnnotationLineEnding.OPEN_ARROW, AnnotationLineEnding.REVERSED_CLOSED_ARROW),
            rotation = value(45.0), intent = value(AnnotationLineIntent.LINE_DIMENSION), captionEnabled = true,
            captionPosition = AnnotationCaptionPosition.TOP, captionOffset = value(AnnotationCaptionOffset(1.0, 2.0)),
            leader = value(AnnotationLineLeader(5.0, 1.0, 2.0)), contents = value("Line text"),
            blendMode = AnnotationBlendMode.HARD_LIGHT, richText = value(comment), measure = value(scale), common = fullCommon,
        )
        expectRoundTrip(line, line.copy(common = unnamed), LineAnnotationPatch)
        val polygon = PolygonAnnotationPatch(
            vertices = listOf(Point(10.0, 10.0), Point(100.0, 10.0), Point(50.0, 80.0)), color = value(AnnotationColor("#0000ff")),
            opacity = 0.5, strokeWidth = 2.0, borderStyle = AnnotationBorderStyle.DASHED, dashArray = value(listOf(2.0, 2.0)),
            interiorColor = value(AnnotationColor("#ffff00")), rotation = value(10.0), intent = value(AnnotationPolygonIntent.POLYGON_CLOUD),
            cloudyIntensity = value(2.0), captionEnabled = value(true), captionCenter = AnnotationCaptionCenter.At(Point(50.0, 30.0)),
            contents = value("Polygon text"), blendMode = AnnotationBlendMode.LIGHTEN, richText = value(comment),
            measure = Nullable.Clear, common = fullCommon,
        )
        expectRoundTrip(polygon, polygon.copy(common = unnamed), PolygonAnnotationPatch)
        val polyline = PolylineAnnotationPatch(
            vertices = listOf(Point(10.0, 10.0), Point(60.0, 80.0), Point(100.0, 10.0)), color = value(AnnotationColor("#00ffff")),
            opacity = 0.75, strokeWidth = 3.0, borderStyle = AnnotationBorderStyle.SOLID, dashArray = value(listOf(5.0)),
            interiorColor = value(AnnotationColor("#ff0000")),
            lineEndings = AnnotationLineEndings(AnnotationLineEnding.CIRCLE, AnnotationLineEnding.DIAMOND), rotation = value(20.0),
            intent = value(AnnotationPolylineIntent.POLYLINE_DIMENSION), captionEnabled = Nullable.Clear,
            captionCenter = AnnotationCaptionCenter.Automatic, contents = value("Polyline text"),
            blendMode = AnnotationBlendMode.DIFFERENCE, richText = value(comment), measure = value(scale), common = fullCommon,
        )
        expectRoundTrip(polyline, polyline.copy(common = unnamed), PolylineAnnotationPatch)
    }

    // ── the codec's rules ──

    @Test
    fun nullAndAbsentDecodeDifferently() {
        assertEquals(Nullable.Clear, InkAnnotationPatch.fromJson("""{"color":null}""").color)
        val absent = InkAnnotationPatch.fromJson("{}")
        assertEquals(Nullable.Unchanged, absent.color)
        assertEquals(InkAnnotationPatch(), absent)
    }

    @Test
    fun everyNullableMemberReadsNull() {
        val line = LineAnnotationPatch.fromJson(
            """{"captionOffset":null,"color":null,"contents":null,"dashArray":null,"intent":null,"interiorColor":null,"leader":null,"measure":null,"richText":null,"rotation":null,"subject":null}""",
        )
        assertEquals(
            LineAnnotationPatch(
                color = Nullable.Clear, dashArray = Nullable.Clear, interiorColor = Nullable.Clear, rotation = Nullable.Clear,
                intent = Nullable.Clear, captionOffset = Nullable.Clear, leader = Nullable.Clear, contents = Nullable.Clear,
                richText = Nullable.Clear, measure = Nullable.Clear, common = AnnotationCommonFields(subject = Nullable.Clear),
            ),
            line,
        )
        // A caption centre's `null` is a value, the engine's own placement.
        assertEquals(
            PolygonAnnotationPatch(captionCenter = AnnotationCaptionCenter.Automatic),
            PolygonAnnotationPatch.fromJson("""{"captionCenter":null}"""),
        )
    }

    @Test
    fun aLinePatchReadsBothPoints() {
        assertEquals(
            LineAnnotationPatch(start = Point(1.0, 2.0), end = Point(3.0, 4.0)),
            LineAnnotationPatch.fromJson("""{"linePoints":{"end":{"x":3,"y":4},"start":{"x":1,"y":2}}}"""),
        )
    }

    @Test
    fun aPatchIgnoresKeysItDoesNotDeclare() {
        assertEquals(
            InkAnnotationPatch(strokeWidth = 5.0),
            InkAnnotationPatch.fromJson("""{"icon":5,"strokeWidth":5,"subtype":"ink","target":"nowhere","zzz":[1]}"""),
        )
        // The kinds that share a field bag still ignore each other's members.
        assertEquals(UnderlineAnnotationPatch(opacity = 0.5), UnderlineAnnotationPatch.fromJson("""{"intent":7,"opacity":0.5}"""))
        assertEquals(PolygonAnnotationPatch(opacity = 0.5), PolygonAnnotationPatch.fromJson("""{"lineEndings":7,"opacity":0.5}"""))
        assertEquals(PolylineAnnotationPatch(opacity = 0.5), PolylineAnnotationPatch.fromJson("""{"cloudyIntensity":"x","opacity":0.5}"""))
        assertEquals(LinkAnnotationPatch(), LinkAnnotationPatch.fromJson("""{"blendMode":"normal","richText":7}"""))
    }

    @Test
    fun aDeclaredKeyOfTheWrongShapeThrows() {
        for (json in listOf("""{"strokeWidth":"wide"}""", """{"borderStyle":"beveled"}""", "[]", "not json")) {
            val error = assertThrows(json, PdfException::class.java) { InkAnnotationPatch.fromJson(json) }
            assertEquals(PdfException.Code.INVALID_INPUT, error.code)
        }
        val callout = assertThrows(PdfException::class.java) { FreeTextAnnotationPatch.fromJson("""{"calloutLine":[{"x":1,"y":2}]}""") }
        assertEquals(PdfException.Code.INVALID_INPUT, callout.code)
        val intent = assertThrows(PdfException::class.java) { PolygonAnnotationPatch.fromJson("""{"intent":"polyline-dimension"}""") }
        assertEquals(PdfException.Code.INVALID_INPUT, intent.code)
    }

    /** A patch never writes `nm`, but a field bag can hold one, and a read patch shows it. */
    @Test
    fun aDecodedPatchKeepsTheNameItsJsonCarries() {
        val note = NoteAnnotationPatch.fromJson("""{"nm":"note-1","open":true}""")
        assertEquals(value("note-1"), note.common.nm)
        assertEquals("""{"open":true}""", note.json())
    }

    // ── tool defaults ──

    private suspend fun tools(): Pair<PdfViewer, AnnotationToolsCapability> {
        val (viewer, _, annotations) = AnnotationTest.opened("report")
        return viewer to annotations.tools
    }

    @Test
    fun defaultsReadBackWhatUpdateDefaultsWrote() = viewerTest {
        val (viewer, tools) = tools()
        val common = AnnotationCommonFields(subject = value("Review"), groupId = value("reviewers"), locked = true)
        tools.updateDefaults(InkAnnotationPatch(strokeWidth = 5.0, common = common), ToolId.INK)
        val ink = tools.defaults(ToolId.INK, InkAnnotationPatch)
        assertEquals(5.0, ink.strokeWidth)
        assertEquals(value("Review"), ink.common.subject)
        assertEquals(value("reviewers"), ink.common.groupId)
        assertEquals(true, ink.common.locked)
        // The members the patch left alone are the tool's own.
        assertEquals(true, ink.common.print)
        assertEquals(AnnotationBorderStyle.SOLID, ink.borderStyle)
        viewer.destroy()
    }

    /** The engine reports a default set to nothing as `null` and leaves an unset one out; the typed read keeps them apart. */
    @Test
    fun aNullDefaultAndAnUnsetOneReadApart() = viewerTest {
        val (viewer, tools) = tools()
        val fresh = tools.defaults(ToolId.INK, InkAnnotationPatch)
        assertEquals(value(AnnotationColor("#ef4444")), fresh.color)
        assertEquals(Nullable.Unchanged, fresh.dashArray)
        assertEquals(Nullable.Unchanged, fresh.rotation)
        assertEquals(Nullable.Unchanged, fresh.contents)
        assertEquals(Nullable.Unchanged, fresh.common.subject)

        tools.updateDefaults(InkAnnotationPatch(color = Nullable.Clear, dashArray = Nullable.Clear), ToolId.INK)
        val raw = jsonObject(tools.defaults(ToolId.INK))
        assertEquals(JsonNull, raw["color"])
        assertEquals(JsonNull, raw["dashArray"])
        assertFalse("rotation" in raw)
        val ink = tools.defaults(ToolId.INK, InkAnnotationPatch)
        assertEquals(Nullable.Clear, ink.color)
        assertEquals(Nullable.Clear, ink.dashArray)
        assertEquals(Nullable.Unchanged, ink.rotation)
        viewer.destroy()
    }

    @Test
    fun everyToolsDefaultsReadAsItsPatch() = viewerTest {
        val (viewer, tools) = tools()
        suspend fun reads(reader: AnnotationPatch.Reader<*>, ids: List<ToolId>) {
            for (id in ids) tools.defaults(id, reader)
        }
        reads(InkAnnotationPatch, listOf(ToolId.INK, ToolId.INK_HIGHLIGHT))
        reads(HighlightAnnotationPatch, listOf(ToolId.HIGHLIGHT))
        reads(UnderlineAnnotationPatch, listOf(ToolId.UNDERLINE))
        reads(SquigglyAnnotationPatch, listOf(ToolId.SQUIGGLY))
        reads(StrikeoutAnnotationPatch, listOf(ToolId.STRIKEOUT))
        reads(RedactionAnnotationPatch, listOf(ToolId.REDACT))
        reads(SquareAnnotationPatch, listOf(ToolId.SQUARE))
        reads(CircleAnnotationPatch, listOf(ToolId.CIRCLE))
        reads(LineAnnotationPatch, listOf(ToolId.LINE, ToolId.DISTANCE, ToolId.CALIBRATE))
        reads(PolygonAnnotationPatch, listOf(ToolId.POLYGON, ToolId.AREA))
        reads(PolylineAnnotationPatch, listOf(ToolId.POLYLINE, ToolId.PERIMETER))
        reads(FreeTextAnnotationPatch, listOf(ToolId.FREE_TEXT, ToolId.FREE_TEXT_CALLOUT))
        reads(CaretAnnotationPatch, listOf(ToolId.INSERT_TEXT, ToolId.REPLACE_TEXT))
        reads(StampAnnotationPatch, listOf(ToolId.STAMP))
        reads(LinkAnnotationPatch, listOf(ToolId.LINK))
        reads(NoteAnnotationPatch, listOf(ToolId.NOTE))
        reads(FileAttachmentAnnotationPatch, listOf(ToolId.ATTACHMENT))

        val distance = tools.defaults(ToolId.DISTANCE, LineAnnotationPatch)
        assertEquals(value(AnnotationLineIntent.LINE_DIMENSION), distance.intent)
        assertEquals(value(AnnotationLineLeader(12.0, 5.0, 0.0)), distance.leader)
        assertEquals(AnnotationLineEndings(AnnotationLineEnding.CLOSED_ARROW, AnnotationLineEnding.CLOSED_ARROW), distance.lineEndings)
        viewer.destroy()
    }

    /** The defaults are an open field bag: the JSON overload stores a name, and a value no patch member holds. */
    @Test
    fun defaultsThatDoNotReadAsThePatchThrowInvalidInput() = viewerTest {
        val (viewer, tools) = tools()
        tools.updateDefaults("""{"nm":"fixed-name"}""", ToolId.INK)
        assertEquals(value("fixed-name"), tools.defaults(ToolId.INK, InkAnnotationPatch).common.nm)

        tools.updateDefaults("""{"strokeWidth":"wide"}""", ToolId.INK)
        expectCode(PdfException.Code.INVALID_INPUT) { tools.defaults(ToolId.INK, InkAnnotationPatch) }
        // Another kind's patch doesn't read `strokeWidth`, so it still reads.
        assertNull(tools.defaults(ToolId.INK, HighlightAnnotationPatch).quadPoints)
        viewer.destroy()
    }
}
