package com.embedpdf

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.doubleOrNull
import kotlinx.serialization.json.jsonObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.math.abs
import kotlin.math.roundToLong

/** [Nullable.Value]: a test's short spelling of a value a member sets. */
fun <T> value(value: T): Nullable<T> = Nullable.Value(value)

/** A JSON text's object. */
fun jsonObject(json: String): JsonObject = Json.parseToJsonElement(json).jsonObject

/** JSON values equal, numbers within a thousandth (see [rounded]). */
fun sameJson(left: JsonElement, right: JsonElement): Boolean = when {
    left is JsonObject && right is JsonObject ->
        left.size == right.size && left.all { (key, value) -> right[key]?.let { sameJson(value, it) } ?: false }
    left is JsonArray && right is JsonArray -> left.size == right.size && left.zip(right).all { (a, b) -> sameJson(a, b) }
    left is JsonPrimitive && right is JsonPrimitive && !left.isString && !right.isString &&
        left !is JsonNull && right !is JsonNull && left.booleanOrNull == null && right.booleanOrNull == null ->
        abs(left.doubleOrNull!! - right.doubleOrNull!!) < 0.001
    else -> left == right
}

/** Every key [json] writes but `subtype` is in [read] with the value written. */
fun expectWritten(json: String, read: String, what: String) {
    val readMembers = jsonObject(read)
    for ((key, value) in jsonObject(json)) {
        if (key == "subtype") continue
        val readValue = readMembers[key] ?: throw AssertionError("the engine read back no '$key' for $what")
        assertTrue("$what '$key': wrote $value, read $readValue", sameJson(value, readValue))
    }
}

/**
 * The point rounded to a thousandth of a point: a rotated ink's strokes come
 * back off by about 1e-5, the engine turning them into its unrotated frame
 * and back.
 */
fun rounded(point: Point): Point = Point(rounded(point.x), rounded(point.y))

fun rounded(rect: Rect): Rect = Rect(rounded(rect.x), rounded(rect.y), rounded(rect.width), rounded(rect.height))

private fun rounded(value: Double): Double = (value * 1000).roundToLong() / 1000.0

/** A viewer on [fixture], its first page and its annotations. */
data class OpenedPage(val viewer: PdfViewer, val page: PageRef, val annotations: AnnotationCapability)

suspend fun openedPage(fixture: String = "report"): OpenedPage {
    val (viewer, document, annotations) = AnnotationTest.opened(fixture)
    return OpenedPage(viewer, viewer.pages(document)[0].page, annotations)
}

/** An [AnnotationInfo.Ink]'s members a test compares, the points [rounded]. */
fun inkSeen(ink: AnnotationInfo.Ink): AnnotationInfo.Ink = ink.copy(inkList = ink.inkList.map { it.map(::rounded) }, richText = null)

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

    // ── the engine takes every typed member ──

    @Test
    fun createdDraftsReadBackEveryMember() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val ink = checkNotNull(annotations.get(annotations.create(fullInk, page)))
        assertEquals(AnnotationSubtype.INK, ink.subtype)
        assertEquals("Ink text", ink.contents)
        assertEquals(AnnotationBlendMode.SCREEN, ink.blendMode)
        assertEquals(
            AnnotationInfo.Ink(
                AnnotationColor("#1f6feb"), 0.75, 3.0, AnnotationInfo.BorderStyle.DASHED, listOf(4.0, 2.0), listOf(stroke),
                AnnotationIntent.INK_HIGHLIGHT, 30.0, null,
            ),
            ink.ink?.let(::inkSeen),
        )

        val highlight = checkNotNull(annotations.get(annotations.create(fullHighlight, page)))
        assertEquals(AnnotationSubtype.HIGHLIGHT, highlight.subtype)
        assertEquals("Highlight text", highlight.contents)
        assertEquals(AnnotationBlendMode.MULTIPLY, highlight.blendMode)
        assertEquals(AnnotationInfo.Highlight(AnnotationColor("#ffcd45"), 0.5, listOf(quad), null), highlight.highlight)

        val note = checkNotNull(annotations.get(annotations.create(fullNote, page)))
        assertEquals(AnnotationSubtype.TEXT, note.subtype)
        assertEquals(noteRect, note.rect)
        assertEquals("Typed note", note.contents)
        assertEquals(AnnotationBlendMode.DARKEN, note.blendMode)
        assertEquals(AnnotationInfo.Note(AnnotationColor("#e5484d"), 0.5, "key", true, "accepted", "review", null), note.note)
        viewer.destroy()
    }

    @Test
    fun aNullColorDrawsAnInkWithNoStroke() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val ref = annotations.create(InkAnnotationDraft(listOf(stroke), color = Nullable.Clear), page)
        assertNull(checkNotNull(annotations.get(ref)?.ink).color)
        viewer.destroy()
    }

    @Test
    fun updatesChangeEveryMember() = viewerTest {
        val (viewer, page, annotations) = openedPage()

        val inkRef = annotations.create(fullInk, page)
        val strokes = listOf(listOf(Point(50.0, 60.0), Point(70.0, 80.0)), listOf(Point(90.0, 100.0)))
        annotations.update(
            inkRef,
            InkAnnotationPatch(
                inkList = strokes, color = value(AnnotationColor("#00ff00")), opacity = 0.25, strokeWidth = 5.0,
                borderStyle = AnnotationBorderStyle.DASHED, dashArray = value(listOf(1.0, 1.0)), rotation = value(45.0),
                contents = value("Changed ink"),
            ),
        )
        val ink = checkNotNull(annotations.get(inkRef))
        assertEquals("Changed ink", ink.contents)
        assertEquals(
            AnnotationInfo.Ink(
                AnnotationColor("#00ff00"), 0.25, 5.0, AnnotationInfo.BorderStyle.DASHED, listOf(1.0, 1.0), strokes,
                AnnotationIntent.INK_HIGHLIGHT, 45.0, null,
            ),
            ink.ink?.let(::inkSeen),
        )

        val highlightRef = annotations.create(fullHighlight, page)
        val moved = Quad(Point(80.0, 400.0), Point(160.0, 400.0), Point(80.0, 414.0), Point(160.0, 414.0))
        annotations.update(
            highlightRef,
            HighlightAnnotationPatch(listOf(moved), AnnotationColor("#00ff00"), 1.0, value("Changed highlight")),
        )
        val highlight = checkNotNull(annotations.get(highlightRef))
        assertEquals("Changed highlight", highlight.contents)
        assertEquals(AnnotationInfo.Highlight(AnnotationColor("#00ff00"), 1.0, listOf(moved), null), highlight.highlight)

        val noteRef = annotations.create(fullNote, page)
        val rect = Rect(100.0, 120.0, 20.0, 20.0)
        annotations.update(
            noteRef,
            NoteAnnotationPatch(
                rect, AnnotationColor("#00ff00"), 1.0, AnnotationNoteIcon.NEW_PARAGRAPH, false, value("completed"),
                value("review"), value("Changed note"),
            ),
        )
        val note = checkNotNull(annotations.get(noteRef))
        assertEquals(rect, note.rect)
        assertEquals("Changed note", note.contents)
        assertEquals(
            AnnotationInfo.Note(AnnotationColor("#00ff00"), 1.0, "new-paragraph", false, "completed", "review", null),
            note.note,
        )
        viewer.destroy()
    }

    @Test
    fun aPatchLeavesWhatItLeavesOutAndClearsWhatItNulls() = viewerTest {
        val (viewer, page, annotations) = openedPage()

        val inkRef = annotations.create(fullInk, page)
        annotations.update(inkRef, InkAnnotationPatch(opacity = 0.5))
        val kept = checkNotNull(annotations.get(inkRef))
        assertEquals(0.5, kept.ink?.opacity)
        assertEquals(AnnotationColor("#1f6feb"), kept.ink?.color)
        assertEquals(listOf(4.0, 2.0), kept.ink?.dashArray)
        assertEquals("Ink text", kept.contents)

        annotations.update(
            inkRef,
            InkAnnotationPatch(
                color = Nullable.Clear, borderStyle = AnnotationBorderStyle.SOLID, dashArray = Nullable.Clear,
                intent = Nullable.Clear, rotation = Nullable.Clear, contents = Nullable.Clear,
            ),
        )
        val cleared = checkNotNull(annotations.get(inkRef))
        val ink = checkNotNull(cleared.ink)
        assertNull(ink.color)
        assertNull(ink.dashArray)
        assertNull(ink.intent)
        assertNull(ink.rotation)
        assertNull(cleared.contents)

        val noteRef = annotations.create(fullNote, page)
        annotations.update(noteRef, NoteAnnotationPatch(state = Nullable.Clear, stateModel = Nullable.Clear, contents = Nullable.Clear))
        val note = checkNotNull(annotations.get(noteRef))
        assertNull(note.note?.state)
        assertNull(note.note?.stateModel)
        assertNull(note.contents)
        assertEquals("key", note.note?.icon)

        val highlightRef = annotations.create(fullHighlight, page)
        annotations.update(highlightRef, HighlightAnnotationPatch(contents = Nullable.Clear))
        val highlight = checkNotNull(annotations.get(highlightRef))
        assertNull(highlight.contents)
        assertEquals(AnnotationColor("#ffcd45"), highlight.highlight?.color)
        viewer.destroy()
    }

    @Test
    fun aPatchOfAnotherSubtypesFieldsIsRefused() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val highlight = annotations.create(fullHighlight, page)
        expectCode(PdfException.Code.INVALID_INPUT) { annotations.update(highlight, InkAnnotationPatch(strokeWidth = 2.0)) }
        viewer.destroy()
    }

    @Test
    fun defaultsReachTheToolsNextAnnotation() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val tools = annotations.tools

        tools.updateDefaults(
            InkAnnotationPatch(
                color = value(AnnotationColor("#00ff00")), opacity = 0.5, strokeWidth = 4.0,
                borderStyle = AnnotationBorderStyle.DASHED, dashArray = value(listOf(2.0, 3.0)), rotation = value(15.0),
                contents = value("Default ink"),
            ),
            ToolId.INK,
        )
        val ink = checkNotNull(annotations.get(annotations.create(InkAnnotationDraft(listOf(stroke)), page, ToolId.INK)))
        assertEquals("Default ink", ink.contents)
        assertEquals(
            AnnotationInfo.Ink(
                AnnotationColor("#00ff00"), 0.5, 4.0, AnnotationInfo.BorderStyle.DASHED, listOf(2.0, 3.0), listOf(stroke), null,
                15.0, null,
            ),
            ink.ink?.let(::inkSeen),
        )

        tools.updateDefaults(
            HighlightAnnotationPatch(color = AnnotationColor("#00ff00"), opacity = 0.5, contents = value("Default highlight")),
            ToolId.HIGHLIGHT,
        )
        val highlight = checkNotNull(annotations.get(annotations.create(HighlightAnnotationDraft(listOf(quad)), page, ToolId.HIGHLIGHT)))
        assertEquals("Default highlight", highlight.contents)
        assertEquals(AnnotationInfo.Highlight(AnnotationColor("#00ff00"), 0.5, listOf(quad), null), highlight.highlight)

        tools.updateDefaults(
            NoteAnnotationPatch(
                color = AnnotationColor("#00ff00"), opacity = 0.5, icon = AnnotationNoteIcon.HELP, open = true,
                state = value("marked"), stateModel = value("marked"), contents = value("Default note"),
            ),
            ToolId.NOTE,
        )
        val note = checkNotNull(annotations.get(annotations.create(NoteAnnotationDraft(noteRect), page, ToolId.NOTE)))
        assertEquals("Default note", note.contents)
        assertEquals(AnnotationInfo.Note(AnnotationColor("#00ff00"), 0.5, "help", true, "marked", "marked", null), note.note)
        viewer.destroy()
    }

    @Test
    fun aNullDefaultReachesTheToolsNextAnnotation() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        annotations.tools.updateDefaults(InkAnnotationPatch(color = Nullable.Clear), ToolId.INK)
        assertEquals(JsonNull, jsonObject(annotations.tools.defaults(ToolId.INK))["color"])
        val ink = checkNotNull(annotations.get(annotations.create(InkAnnotationDraft(listOf(stroke)), page, ToolId.INK)))
        assertNull(ink.ink?.color)
        viewer.destroy()
    }

    // ── drift guard ──

    /**
     * The engine refuses a key its kind doesn't declare
     * (`assert_declared_fields`), so a typed key the engine renamed or
     * dropped makes the creates above throw. This pins that refusal.
     */
    @Test
    fun theEngineRefusesAKeyItsKindDoesntDeclare() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        for (json in listOf(
            """{"subtype":"ink","inkList":[[{"x":1,"y":2}]],"colour":"#ff0000"}""",
            """{"subtype":"highlight","quadPoints":[{"upperLeft":{"x":72,"y":300},"upperRight":{"x":200,"y":300},"lowerLeft":{"x":72,"y":312},"lowerRight":{"x":200,"y":312}}],"strokeWidth":2}""",
            """{"subtype":"text","rect":{"x":40,"y":40,"width":24,"height":24},"stateModell":"review"}""",
        )) {
            expectCode(PdfException.Code.INVALID_INPUT, json) { annotations.create(json, page) }
        }
        val ink = annotations.create(InkAnnotationDraft(listOf(stroke)), page)
        expectCode(PdfException.Code.INVALID_INPUT) { annotations.update(ink, """{"strokeWidht": 2}""") }
        viewer.destroy()
    }

    /**
     * Every key a full typed draft writes comes back in the engine's read of
     * the annotation it created, with the value written.
     */
    @Test
    fun everyTypedKeyRoundTripsThroughTheEngine() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        for (draft in listOf(fullInk, fullHighlight, fullNote)) {
            val ref = annotations.create(draft, page)
            expectWritten(draft.json(), checkNotNull(annotations.get(ref)).fieldsJson, draft.javaClass.simpleName)
        }
        viewer.destroy()
    }

    // ── the JSON-string overloads ──

    @Test
    fun theStringOverloadsTakeTheTypedJsonAlike() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        for (draft in listOf(fullInk, fullHighlight, fullNote)) {
            val typed = checkNotNull(annotations.get(annotations.create(draft, page)))
            val untyped = checkNotNull(annotations.get(annotations.create(draft.json(), page)))
            assertEquals(typed.details, untyped.details)
            assertEquals(typed.rect, untyped.rect)
            assertEquals(typed.contents, untyped.contents)
            assertNotEquals(AnnotationInfo.Details.Untyped, typed.details)
        }

        val patch = InkAnnotationPatch(color = value(AnnotationColor("#00ff00")), strokeWidth = 6.0, rotation = Nullable.Clear)
        val typed = annotations.create(fullInk, page)
        val untyped = annotations.create(fullInk, page)
        annotations.update(typed, patch)
        annotations.update(untyped, patch.json())
        assertEquals(annotations.get(typed)?.details, annotations.get(untyped)?.details)

        annotations.tools.updateDefaults(patch.json(), ToolId.INK)
        val viaString = annotations.tools.defaults(ToolId.INK)
        annotations.tools.updateDefaults(InkAnnotationPatch(strokeWidth = 1.0), ToolId.INK)
        annotations.tools.updateDefaults(patch, ToolId.INK)
        assertEquals(viaString, annotations.tools.defaults(ToolId.INK))
        viewer.destroy()
    }
}
