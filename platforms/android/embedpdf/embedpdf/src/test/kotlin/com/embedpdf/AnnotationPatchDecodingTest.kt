package com.embedpdf

import kotlinx.serialization.json.JsonPrimitive
import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Test

/**
 * The typed patches read back, as `AnnotationPatchDecodingTests`: a patch
 * reads from the field bag it writes.
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

    // ── the codec's rules ──

    @Test
    fun nullAndAbsentDecodeDifferently() {
        assertEquals(Nullable.Clear, InkAnnotationPatch.fromJson("""{"color":null}""").color)
        val absent = InkAnnotationPatch.fromJson("{}")
        assertEquals(Nullable.Unchanged, absent.color)
        assertEquals(InkAnnotationPatch(), absent)
    }

    @Test
    fun aPatchIgnoresKeysItDoesNotDeclare() {
        assertEquals(
            InkAnnotationPatch(strokeWidth = 5.0),
            InkAnnotationPatch.fromJson("""{"icon":5,"strokeWidth":5,"subtype":"ink","target":"nowhere","zzz":[1]}"""),
        )
        // The kinds that share a field bag still ignore each other's members.
        assertEquals(UnderlineAnnotationPatch(opacity = 0.5), UnderlineAnnotationPatch.fromJson("""{"intent":7,"opacity":0.5}"""))
        assertEquals(LinkAnnotationPatch(), LinkAnnotationPatch.fromJson("""{"blendMode":"normal","richText":7}"""))
    }

    @Test
    fun aDeclaredKeyOfTheWrongShapeThrows() {
        for (json in listOf("""{"strokeWidth":"wide"}""", """{"borderStyle":"beveled"}""", "[]", "not json")) {
            val error = assertThrows(json, PdfException::class.java) { InkAnnotationPatch.fromJson(json) }
            assertEquals(PdfException.Code.INVALID_INPUT, error.code)
        }
        val error = assertThrows(PdfException::class.java) { FreeTextAnnotationPatch.fromJson("""{"calloutLine":[{"x":1,"y":2}]}""") }
        assertEquals(PdfException.Code.INVALID_INPUT, error.code)
    }

    /** A patch never writes `nm`, but a field bag can hold one, and a read patch shows it. */
    @Test
    fun aDecodedPatchKeepsTheNameItsJsonCarries() {
        val note = NoteAnnotationPatch.fromJson("""{"nm":"note-1","open":true}""")
        assertEquals(value("note-1"), note.common.nm)
        assertEquals("""{"open":true}""", note.json())
    }
}
