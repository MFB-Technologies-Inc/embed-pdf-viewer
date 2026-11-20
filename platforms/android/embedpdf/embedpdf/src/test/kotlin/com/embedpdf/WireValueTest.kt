package com.embedpdf

import kotlinx.serialization.KSerializer
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.jsonObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test

/** Encode with kotlinx.serialization, decode the text back, and return both. */
private fun <T> roundTrip(serializer: KSerializer<T>, value: T): Pair<String, T> {
    val json = Json.encodeToString(serializer, value)
    return json to Json.decodeFromString(serializer, json)
}

class WireValueTest {
    // ── open string sets ──

    private val subtypes = listOf(
        AnnotationSubtype.TEXT to "text", AnnotationSubtype.LINK to "link", AnnotationSubtype.FREE_TEXT to "free-text",
        AnnotationSubtype.LINE to "line", AnnotationSubtype.SQUARE to "square", AnnotationSubtype.CIRCLE to "circle",
        AnnotationSubtype.POLYGON to "polygon", AnnotationSubtype.POLYLINE to "polyline",
        AnnotationSubtype.HIGHLIGHT to "highlight", AnnotationSubtype.UNDERLINE to "underline",
        AnnotationSubtype.SQUIGGLY to "squiggly", AnnotationSubtype.STRIKEOUT to "strikeout",
        AnnotationSubtype.STAMP to "stamp", AnnotationSubtype.CARET to "caret", AnnotationSubtype.INK to "ink",
        AnnotationSubtype.POPUP to "popup", AnnotationSubtype.FILE_ATTACHMENT to "file-attachment",
        AnnotationSubtype.REDACT to "redact", AnnotationSubtype.WIDGET to "widget",
        AnnotationSubtype.UNSUPPORTED to "unsupported",
    )

    @Test
    fun everyNamedSubtypeIsListed() {
        assertEquals(subtypes.map { it.second }, AnnotationSubtype.named.map { it.value })
    }

    @Test
    fun eachSubtypeRoundTripsItsWireString() {
        for ((subtype, wire) in subtypes) {
            assertEquals(wire, subtype.value)
            assertEquals(subtype, AnnotationSubtype(wire))
            assertNotEquals(subtype, AnnotationSubtype("$wire-x"))
            val (json, decoded) = roundTrip(AnnotationSubtype.serializer(), subtype)
            assertEquals("\"$wire\"", json)
            assertEquals(subtype, decoded)
        }
    }

    @Test
    fun anUnnamedStringRoundTripsAsItself() {
        val subtype = AnnotationSubtype("sound")
        assertEquals("sound", subtype.value)
        assertFalse(subtype in AnnotationSubtype.named)
        assertEquals(subtype, roundTrip(AnnotationSubtype.serializer(), subtype).second)
        val intent = AnnotationIntent("cloudy")
        assertEquals(intent, roundTrip(AnnotationIntent.serializer(), intent).second)
    }

    @Test
    fun aWhenMatchesTheNamedConstantsAndFallsBackForTheRest() {
        fun kind(subtype: AnnotationSubtype): String = when (subtype) {
            AnnotationSubtype.SQUARE, AnnotationSubtype.CIRCLE -> "shape"
            AnnotationSubtype.INK -> "ink"
            else -> "other"
        }
        assertEquals(listOf("shape", "shape", "ink", "other"), listOf("square", "circle", "ink", "sound").map { kind(AnnotationSubtype(it)) })
    }

    @Test
    fun toStringIsTheWireString() {
        assertEquals("file-attachment", "${AnnotationSubtype.FILE_ATTACHMENT}")
        assertEquals("free-text-callout", "${AnnotationIntent.FREE_TEXT_CALLOUT}")
    }

    // ── AnnotationPosition ──

    private val page = PageRef(3u)
    private val positions = listOf(
        AnnotationPosition.Start to "\"start\"",
        AnnotationPosition.End to "\"end\"",
        AnnotationPosition.Before(AnnotationRefObjectNumber(page, 7u)) to
            """{"before":{"kind":"objectNumber","page":{"kind":"objectNumber","objectNumber":3},"objectNumber":7}}""",
        AnnotationPosition.After(AnnotationRefObjectNumber(page, 7u)) to
            """{"after":{"kind":"objectNumber","page":{"kind":"objectNumber","objectNumber":3},"objectNumber":7}}""",
        AnnotationPosition.Before(AnnotationRefIndex(page, 0u)) to
            """{"before":{"kind":"baseIndex","page":{"kind":"objectNumber","objectNumber":3},"baseIndex":0}}""",
        AnnotationPosition.After(AnnotationRefIndex(page, 4294967295u)) to
            """{"after":{"kind":"baseIndex","page":{"kind":"objectNumber","objectNumber":3},"baseIndex":4294967295}}""",
    )

    @Test
    fun aPositionIsTheEnginesJsonByteForByte() {
        for ((position, json) in positions) {
            assertEquals(json, position.json)
            assertEquals(position, Json.decodeFromString(AnnotationPosition.serializer(), json))
            val (encoded, decoded) = roundTrip(AnnotationPosition.serializer(), position)
            assertEquals(json, encoded)
            assertEquals(position, decoded)
        }
    }

    @Test
    fun aPositionThatIsNotOneRefusesToDecode() {
        for (json in listOf(
            "\"front\"",
            "\"above\"",
            """{"above":{"kind":"objectNumber","page":{"kind":"objectNumber","objectNumber":3},"objectNumber":7}}""",
            """{"before":{"kind":"spiral","page":{"kind":"objectNumber","objectNumber":3},"objectNumber":7}}""",
        )) {
            assertThrows(json, SerializationException::class.java) { Json.decodeFromString(AnnotationPosition.serializer(), json) }
        }
    }

    // ── AnnotationLinkTarget ──

    /** `linkTarget` decodes with this, so a target the type can't hold is an error and never a different target. */
    @Test
    fun aTargetALinkTargetCannotHoldDoesNotDecode() {
        for (json in listOf(
            """{"kind":"launch","path":"notes.txt"}""",
            """{"kind":"named","name":"Print"}""",
            """{"kind":"goto","destination":{"kind":"spiral","page":{"kind":"objectNumber","objectNumber":7}}}""",
            """{"kind":"goto","destination":{"kind":"fitR","page":{"kind":"objectNumber","objectNumber":7},"x":1,"y":2}}""",
        )) {
            assertThrows(json, SerializationException::class.java) { Json.decodeFromString(AnnotationLinkTarget.serializer(), json) }
            val error = assertThrows(json, PdfException::class.java) { AnnotationLinkTarget.fromJson(json) }
            assertEquals(PdfException.Code.OPERATION_FAILED, error.code)
        }
    }

    @Test
    fun aLinkTargetDecodesWhatItEncodes() {
        val page = PageRef(7u)
        val targets = listOf(
            AnnotationLinkTarget.Uri("https://example.com"),
            AnnotationLinkTarget.Named(AnnotationLinkVerb.LAST_PAGE),
            AnnotationLinkTarget.Goto(PageDestinationXyz(page, 30.0, null, 2.0)),
            AnnotationLinkTarget.Goto(PageDestinationFit(page)),
            AnnotationLinkTarget.Goto(PageDestinationFitH(page, 40.0)),
            AnnotationLinkTarget.Goto(PageDestinationFitV(page, null)),
            AnnotationLinkTarget.Goto(PageDestinationFitR(page, 1.0, 2.0, 3.0, 4.0)),
            AnnotationLinkTarget.Goto(PageDestinationFitB(page)),
            AnnotationLinkTarget.Goto(PageDestinationFitBh(page, null)),
            AnnotationLinkTarget.Goto(PageDestinationFitBv(page, 5.0)),
        )
        for (target in targets) {
            assertEquals(target, roundTrip(AnnotationLinkTarget.serializer(), target).second)
            assertEquals(target, AnnotationLinkTarget.fromJson(target.json()))
        }
    }

    /** The record the JSON entry points take: keys sorted, absent coordinates `null`, integral numbers without a fraction. */
    @Test
    fun aLinkTargetsJsonIsTheSortedRecord() {
        val target = AnnotationLinkTarget.Goto(PageDestinationXyz(PageRef(7u), 30.0, null, 1.5))
        assertEquals(
            """{"destination":{"kind":"xyz","page":{"kind":"objectNumber","objectNumber":7},"x":30,"y":null,"zoom":1.5},"kind":"goto"}""",
            target.json(),
        )
        assertEquals("""{"kind":"uri","uri":"https://example.com/a/b"}""", AnnotationLinkTarget.Uri("https://example.com/a/b").json())
        assertEquals("""{"kind":"named","name":"PrevPage"}""", AnnotationLinkTarget.Named(AnnotationLinkVerb.PREVIOUS_PAGE).json())
    }

    @Test
    fun aFieldBagRefusesANumberJsonCannotHold() {
        for (number in listOf(Double.NaN, Double.POSITIVE_INFINITY, Double.NEGATIVE_INFINITY)) {
            val error = assertThrows(PdfException::class.java) {
                AnnotationFieldBag.json(JsonObject(mapOf("opacity" to JsonPrimitive(number))))
            }
            assertEquals(PdfException.Code.INVALID_INPUT, error.code)
        }
        assertEquals("""{"a":[1,0.25,-3],"b":true}""", AnnotationFieldBag.json(Json.parseToJsonElement("""{"b":true,"a":[1.0,0.25,-3]}""")))
    }

    // ── common fields and rich text ──

    private val parent = AnnotationRefObjectNumber(PageRef(1u), 7u)

    @Test
    fun commonFieldsWriteOnlyWhatIsSet() {
        val common = AnnotationCommonFields(
            nm = Nullable.Value("n1"),
            subject = Nullable.Clear,
            reply = Nullable.Value(AnnotationReply(parent, AnnotationReplyType.GROUP)),
            hidden = true,
            print = false,
        )
        val members = mutableMapOf<String, JsonElement>()
        common.writeTo(members)
        assertEquals(
            """{"hidden":true,"nm":"n1","print":false,"reply":{"to":{"kind":"objectNumber","objectNumber":7,""" +
                """"page":{"kind":"objectNumber","objectNumber":1}},"type":"group"},"subject":null}""",
            AnnotationFieldBag.json(JsonObject(members)),
        )
        assertEquals(common, AnnotationCommonFields.read(JsonObject(members)))

        val patch = mutableMapOf<String, JsonElement>()
        common.writeTo(patch, asPatch = true)
        assertFalse("nm" in patch)
        assertEquals(AnnotationCommonFields(), AnnotationCommonFields.read(JsonObject(emptyMap())))
    }

    @Test
    fun commonFieldsOfAnotherShapeRefuseToRead() {
        for (json in listOf("""{"hidden":"yes"}""", """{"nm":3}""", """{"reply":{"to":7}}""", """{"reply":{"to":{"kind":"objectNumber","page":{"kind":"objectNumber","objectNumber":1},"objectNumber":7},"type":"thread"}}""")) {
            assertThrows(json, SerializationException::class.java) { AnnotationCommonFields.read(Json.parseToJsonElement(json).jsonObject) }
        }
    }

    @Test
    fun richTextKeepsEveryStyleKeyAndReadsItsPlainText() {
        val json = """{"body":{"family":"Helvetica","size":12},"paragraphs":[{"align":"center","runs":[{"text":"Bold","style":{"weight":700}},{"text":" plain"}]},{"runs":[{"text":"two"}]}]}"""
        val text = Json.decodeFromString(AnnotationRichText.serializer(), json)
        assertEquals("Bold plain\ntwo", text.plainText)
        assertEquals(JsonPrimitive("center"), text.paragraphs[0].properties["align"])
        assertEquals(JsonPrimitive(700), text.paragraphs[0].runs[0].style?.get("weight"))
        val (encoded, decoded) = roundTrip(AnnotationRichText.serializer(), text)
        assertEquals(text, decoded)
        assertEquals(Json.parseToJsonElement(json), Json.parseToJsonElement(encoded))
    }

    @Test
    fun plainRichTextIsOneRunPerLine() {
        val text = AnnotationRichText.plain("one\r\ntwo\rthree\n")
        assertEquals(listOf("one", "two", "three", ""), text.paragraphs.map { it.runs.single().text })
        assertEquals(null, text.body)
        assertEquals("one\ntwo\nthree\n", text.plainText)
    }

    @Test
    fun aParagraphPropertyNamedRunsIsNotWritten() {
        val text = AnnotationRichText(
            paragraphs = listOf(AnnotationRichText.Paragraph(listOf(AnnotationRichText.Run("x")), mapOf("runs" to JsonPrimitive(1)))),
        )
        assertEquals("""{"paragraphs":[{"runs":[{"text":"x"}]}]}""", Json.encodeToString(AnnotationRichText.serializer(), text))
    }

    // ── against the engine ──

    /** A fresh copy of the fixture, and the refs on the page with the most annotations, in paint order. */
    private suspend fun crowdedPage(): Pair<AnnotationCapability, List<AnnotationRef>> {
        val annotations = AnnotationTest.opened().annotations
        val page = annotations.list().groupBy { it.page }.maxBy { it.value.size }.key
        val refs = order(annotations, page)
        assertTrue(refs.size >= 3)
        return annotations to refs
    }

    private suspend fun order(annotations: AnnotationCapability, page: PageRef): List<AnnotationRef> =
        annotations.list(AnnotationFilter(pages = listOf(page))).map { it.annotationRef }

    private fun page(ref: AnnotationRef): PageRef = when (ref) {
        is AnnotationRefObjectNumber -> ref.page
        is AnnotationRefIndex -> ref.page
    }

    @Test
    fun eachTypedPositionMovesTheAnnotationWhereItSays() = viewerTest {
        val (annotations, original) = crowdedPage()
        val page = page(original[0])
        val (first, second, last) = Triple(original[0], original[1], original.last())

        annotations.reorder(listOf(first), AnnotationPosition.End)
        assertEquals(first, order(annotations, page).last())

        annotations.reorder(listOf(first), AnnotationPosition.Start)
        assertEquals(first, order(annotations, page).first())

        annotations.reorder(listOf(first), AnnotationPosition.After(last))
        assertEquals(first, order(annotations, page).last())

        annotations.reorder(listOf(first), AnnotationPosition.Before(second))
        val beforeSecond = order(annotations, page)
        assertEquals(first, beforeSecond[beforeSecond.indexOf(second) - 1])
        annotations.viewer.destroy()
    }

    @Test
    fun aNeighbourOnAnotherPageOrMissingIsRefused() = viewerTest {
        val (annotations, original) = crowdedPage()
        val missing = AnnotationRefObjectNumber(page(original[0]), 999_999u)
        expectCode(PdfException.Code.NOT_FOUND) { annotations.reorder(listOf(original[0]), AnnotationPosition.Before(missing)) }
        assertEquals(original, order(annotations, page(original[0])))
        annotations.viewer.destroy()
    }

    @Test
    fun everyAnnotationToolIsANamedToolId() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, annotation = ""))
        val document = viewer.open(Fixture.bytes("report"))
        val annotationTools = viewer.annotations(document).tools.tools()
        assertEquals(emptyList<ToolId>(), annotationTools.map { it.toolId }.filter { it !in ToolId.named })
        assertTrue(annotationTools.any { it.toolId == ToolId.HIGHLIGHT })
        viewer.destroy()
    }

    @Test
    fun aTypedToolReachesTheAnnotationTools() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, annotation = ""))
        val document = viewer.open(Fixture.bytes("report"))
        val tools = viewer.annotations(document).tools
        assertEquals("highlight", tools.tool(ToolId.HIGHLIGHT)?.id)
        tools.updateDefaults("""{"color": "#00ff00"}""", ToolId.HIGHLIGHT)
        assertTrue(tools.defaults(ToolId.HIGHLIGHT).contains("#00ff00"))
        tools.properties(ToolId.HIGHLIGHT)
        assertEquals(null, tools.tool(ToolId("laser")))
        viewer.destroy()
    }

    private val quads = """[{"upperLeft":{"x":72,"y":72},"upperRight":{"x":172,"y":72},"lowerLeft":{"x":72,"y":92},"lowerRight":{"x":172,"y":92}}]"""
    private val anchor = AnnotationTextEndAnchor(
        """{"glyphQuad":{"upperLeft":{"x":172,"y":72},"upperRight":{"x":178,"y":72},"lowerLeft":{"x":172,"y":92},"lowerRight":{"x":178,"y":92}},"advance":1}""",
    )
    private val square = """{"subtype": "square", "box": {"x": 200, "y": 200, "width": 10, "height": 10}}"""

    @Test
    fun typedSubtypesAndToolsReachTheDrafts() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, selection = true, annotation = ""))
        val document = viewer.open(Fixture.bytes("report"))
        val page = viewer.pages(document)[0].page
        val annotations = viewer.annotations(document)
        annotations.refresh()
        val selection = viewer.selection(document)
        selection.ensureLoaded(page)
        val drafts = annotations.drafts
        annotations.create(square, page, tool = ToolId.SQUARE)
        drafts.createMarkup(AnnotationSubtype.UNDERLINE, quads, page, preset = ToolId.UNDERLINE)
        drafts.createReplaceText(quads, anchor, page, preset = ToolId.REPLACE_TEXT)
        drafts.previewMarkup(AnnotationSubtype.HIGHLIGHT, listOf(AnnotationPageQuads(page, quads)), preset = ToolId.HIGHLIGHT)
        drafts.clearMarkupPreview()
        drafts.hoverGhost(ToolId.NOTE, Point(300.0, 300.0), page)
        drafts.clearGhost()
        drafts.previewPlacement(ToolId.SQUARE, Point(300.0, 300.0), Point(340.0, 340.0), page)
        drafts.clearPlacementPreview()
        selection.selectPage(page)
        assertTrue(drafts.createFromSelection(ToolId.HIGHLIGHT).isNotEmpty())
        assertEquals(
            setOf(AnnotationSubtype.SQUARE, AnnotationSubtype.UNDERLINE, AnnotationSubtype.STRIKEOUT, AnnotationSubtype.CARET, AnnotationSubtype.HIGHLIGHT),
            annotations.list().map { it.subtype }.toSet(),
        )
        viewer.destroy()
    }
}
