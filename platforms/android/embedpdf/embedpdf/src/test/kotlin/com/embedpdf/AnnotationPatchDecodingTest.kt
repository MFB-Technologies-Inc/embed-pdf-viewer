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
    }

    @Test
    fun aDeclaredKeyOfTheWrongShapeThrows() {
        for (json in listOf("""{"strokeWidth":"wide"}""", """{"borderStyle":"beveled"}""", "[]", "not json")) {
            val error = assertThrows(json, PdfException::class.java) { InkAnnotationPatch.fromJson(json) }
            assertEquals(PdfException.Code.INVALID_INPUT, error.code)
        }
    }

    /** A patch never writes `nm`, but a field bag can hold one, and a read patch shows it. */
    @Test
    fun aDecodedPatchKeepsTheNameItsJsonCarries() {
        val note = NoteAnnotationPatch.fromJson("""{"nm":"note-1","open":true}""")
        assertEquals(value("note-1"), note.common.nm)
        assertEquals("""{"open":true}""", note.json())
    }
}
