package com.embedpdf

import org.junit.Assert.assertEquals
import org.junit.Test

/** [Nullable.Value]: a test's short spelling of a value a member sets. */
fun <T> value(value: T): Nullable<T> = Nullable.Value(value)

/**
 * The typed drafts and patches of `AnnotationDraftTests`. Encoding tests
 * compare exact bytes with the Swift test's expected JSON: the field bag
 * sorts keys, so a draft is always the same string.
 */
class AnnotationDraftTest {
    companion object {
        val stroke = listOf(Point(100.0, 100.0), Point(140.0, 120.0), Point(180.0, 100.0))
        val quad = Quad(Point(72.0, 300.0), Point(200.0, 300.0), Point(72.0, 312.0), Point(200.0, 312.0))
        val noteRect = Rect(40.0, 40.0, 24.0, 24.0)

        /** Every ink member set, none to its default. */
        val fullInk = InkAnnotationDraft(
            inkList = listOf(stroke), color = value(AnnotationColor("#1f6feb")), opacity = 0.75, strokeWidth = 3.0,
            borderStyle = AnnotationBorderStyle.DASHED, dashArray = value(listOf(4.0, 2.0)),
            intent = value(AnnotationInkIntent.INK_HIGHLIGHT), rotation = value(30.0), contents = value("Ink text"),
            blendMode = AnnotationBlendMode.SCREEN,
        )

        /** Every highlight member set, none to its default. */
        val fullHighlight = HighlightAnnotationDraft(
            quadPoints = listOf(quad), color = AnnotationColor("#ffcd45"), opacity = 0.5,
            contents = value("Highlight text"), blendMode = AnnotationBlendMode.MULTIPLY,
        )

        /** Every note member set, none to its default. */
        val fullNote = NoteAnnotationDraft(
            rect = noteRect, color = AnnotationColor("#e5484d"), opacity = 0.5, icon = AnnotationNoteIcon.KEY, open = true,
            state = value("accepted"), stateModel = value("review"), contents = value("Typed note"),
            blendMode = AnnotationBlendMode.DARKEN,
        )
    }

    // ── exact encoding ──

    @Test
    fun anInkDraftEncodesEveryMember() {
        assertEquals(
            """{"blendMode":"screen","borderStyle":"dashed","color":"#1f6feb","contents":"Ink text","dashArray":[4,2],"inkList":[[{"x":100,"y":100},{"x":140,"y":120},{"x":180,"y":100}]],"intent":"ink-highlight","opacity":0.75,"rotation":30,"strokeWidth":3,"subtype":"ink"}""",
            fullInk.json(),
        )
    }

    @Test
    fun aDraftLeavesUnsetMembersOut() {
        assertEquals("""{"inkList":[[{"x":1.5,"y":2}]],"subtype":"ink"}""", InkAnnotationDraft(listOf(listOf(Point(1.5, 2.0)))).json())
        assertEquals(
            """{"quadPoints":[{"lowerLeft":{"x":72,"y":312},"lowerRight":{"x":200,"y":312},"upperLeft":{"x":72,"y":300},"upperRight":{"x":200,"y":300}}],"subtype":"highlight"}""",
            HighlightAnnotationDraft(listOf(quad)).json(),
        )
        assertEquals("""{"rect":{"height":24,"width":24,"x":40,"y":40},"subtype":"text"}""", NoteAnnotationDraft(noteRect).json())
    }

    @Test
    fun anInkDraftWritesNullForNothing() {
        val draft = InkAnnotationDraft(
            listOf(listOf(Point(0.0, 0.0))), color = Nullable.Clear, dashArray = Nullable.Clear, intent = Nullable.Clear,
            rotation = Nullable.Clear, contents = Nullable.Clear,
        )
        assertEquals(
            """{"color":null,"contents":null,"dashArray":null,"inkList":[[{"x":0,"y":0}]],"intent":null,"rotation":null,"subtype":"ink"}""",
            draft.json(),
        )
    }

    @Test
    fun aHighlightDraftEncodesEveryMember() {
        assertEquals(
            """{"blendMode":"multiply","color":"#ffcd45","contents":"Highlight text","opacity":0.5,"quadPoints":[{"lowerLeft":{"x":72,"y":312},"lowerRight":{"x":200,"y":312},"upperLeft":{"x":72,"y":300},"upperRight":{"x":200,"y":300}}],"subtype":"highlight"}""",
            fullHighlight.json(),
        )
    }

    @Test
    fun aNoteDraftEncodesEveryMember() {
        assertEquals(
            """{"blendMode":"darken","color":"#e5484d","contents":"Typed note","icon":"key","opacity":0.5,"open":true,"rect":{"height":24,"width":24,"x":40,"y":40},"state":"accepted","stateModel":"review","subtype":"text"}""",
            fullNote.json(),
        )
        assertEquals(
            """{"contents":null,"rect":{"height":24,"width":24,"x":40,"y":40},"state":null,"stateModel":null,"subtype":"text"}""",
            NoteAnnotationDraft(noteRect, state = Nullable.Clear, stateModel = Nullable.Clear, contents = Nullable.Clear).json(),
        )
    }

    @Test
    fun noteIconsAreTheEnginesNames() {
        assertEquals(
            listOf("comment", "key", "note", "help", "new-paragraph", "paragraph", "insert"),
            AnnotationNoteIcon.entries.map { it.value },
        )
        assertEquals(listOf("solid", "dashed"), AnnotationBorderStyle.entries.map { it.value })
        assertEquals(listOf("ink-highlight"), AnnotationInkIntent.entries.map { it.value })
    }

    @Test
    fun anEmptyPatchIsAnEmptyObject() {
        assertEquals("{}", InkAnnotationPatch().json())
        assertEquals("{}", HighlightAnnotationPatch().json())
        assertEquals("{}", NoteAnnotationPatch().json())
    }

    @Test
    fun anInkPatchEncodesEveryMemberAndNoSubtype() {
        val patch = InkAnnotationPatch(
            inkList = listOf(listOf(Point(1.0, 2.0)), listOf(Point(3.0, 4.0))), color = value(AnnotationColor("#00ff00")),
            opacity = 0.25, strokeWidth = 5.0, borderStyle = AnnotationBorderStyle.SOLID, dashArray = value(listOf(1.0)),
            intent = value(AnnotationInkIntent.INK_HIGHLIGHT), rotation = value(90.0), contents = value("Changed"),
            blendMode = AnnotationBlendMode.NORMAL,
        )
        assertEquals(
            """{"blendMode":"normal","borderStyle":"solid","color":"#00ff00","contents":"Changed","dashArray":[1],"inkList":[[{"x":1,"y":2}],[{"x":3,"y":4}]],"intent":"ink-highlight","opacity":0.25,"rotation":90,"strokeWidth":5}""",
            patch.json(),
        )
    }

    @Test
    fun aPatchTellsClearingFromLeavingAlone() {
        assertEquals(
            """{"color":null,"contents":null,"dashArray":null,"intent":null,"rotation":null}""",
            InkAnnotationPatch(
                color = Nullable.Clear, dashArray = Nullable.Clear, intent = Nullable.Clear, rotation = Nullable.Clear,
                contents = Nullable.Clear,
            ).json(),
        )
        assertEquals(
            """{"contents":null,"state":null,"stateModel":null}""",
            NoteAnnotationPatch(state = Nullable.Clear, stateModel = Nullable.Clear, contents = Nullable.Clear).json(),
        )
        assertEquals("""{"contents":null}""", HighlightAnnotationPatch(contents = Nullable.Clear).json())
    }

    @Test
    fun aHighlightPatchEncodesEveryMember() {
        val patch = HighlightAnnotationPatch(
            quadPoints = listOf(quad), color = AnnotationColor("#00ff00"), opacity = 1.0, contents = value("Changed"),
            blendMode = AnnotationBlendMode.NORMAL,
        )
        assertEquals(
            """{"blendMode":"normal","color":"#00ff00","contents":"Changed","opacity":1,"quadPoints":[{"lowerLeft":{"x":72,"y":312},"lowerRight":{"x":200,"y":312},"upperLeft":{"x":72,"y":300},"upperRight":{"x":200,"y":300}}]}""",
            patch.json(),
        )
    }

    @Test
    fun aNotePatchEncodesEveryMember() {
        val patch = NoteAnnotationPatch(
            rect = Rect(1.0, 2.0, 3.0, 4.0), color = AnnotationColor("#00ff00"), opacity = 0.5,
            icon = AnnotationNoteIcon.NEW_PARAGRAPH, open = false, state = value("rejected"), stateModel = value("review"),
            contents = value("Changed"),
        )
        assertEquals(
            """{"color":"#00ff00","contents":"Changed","icon":"new-paragraph","opacity":0.5,"open":false,"rect":{"height":4,"width":3,"x":1,"y":2},"state":"rejected","stateModel":"review"}""",
            patch.json(),
        )
    }

    @Test
    fun aStringIsWrittenAsGiven() {
        assertEquals("""{"contents":"a/b \"c\" é"}""", HighlightAnnotationPatch(contents = value("a/b \"c\" é")).json())
    }

    @Test
    fun aNumberJsonCantHoldIsInvalidInput() = viewerTest {
        expectCode(PdfException.Code.INVALID_INPUT) { InkAnnotationPatch(opacity = Double.NaN).json() }
        expectCode(PdfException.Code.INVALID_INPUT) {
            InkAnnotationDraft(listOf(listOf(Point(Double.POSITIVE_INFINITY, 0.0)))).json()
        }
    }
}
