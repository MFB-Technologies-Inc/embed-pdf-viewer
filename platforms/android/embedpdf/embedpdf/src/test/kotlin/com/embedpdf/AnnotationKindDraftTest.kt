package com.embedpdf

import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The typed drafts and patches of every subtype besides ink, highlight and
 * note, as `AnnotationKindDraftTests`. Where the Swift test has no expected
 * JSON for a full draft, the expectation is the bytes Swift's `Encodable`
 * writes for it.
 */
class AnnotationKindDraftTest {
    companion object {
        val quad = AnnotationDraftTest.quad
        val box = Rect(100.0, 120.0, 80.0, 40.0)
        const val QUAD_JSON =
            """[{"lowerLeft":{"x":72,"y":312},"lowerRight":{"x":200,"y":312},"upperLeft":{"x":72,"y":300},"upperRight":{"x":200,"y":300}}]"""

        val fullUnderline = UnderlineAnnotationDraft(
            listOf(quad), color = AnnotationColor("#0000ff"), opacity = 0.5, contents = value("Underline text"),
            blendMode = AnnotationBlendMode.MULTIPLY,
        )
        val fullSquiggly = SquigglyAnnotationDraft(
            listOf(quad), color = AnnotationColor("#00ff00"), opacity = 0.75, contents = value("Squiggly text"),
            blendMode = AnnotationBlendMode.DARKEN,
        )
        val fullStrikeout = StrikeoutAnnotationDraft(
            listOf(quad), color = AnnotationColor("#ff0000"), opacity = 0.25,
            intent = value(AnnotationStrikeoutIntent.STRIKEOUT_TEXT_EDIT), contents = value("Strikeout text"),
            blendMode = AnnotationBlendMode.SCREEN,
        )
        val fullRedaction = RedactionAnnotationDraft(
            quadPoints = listOf(quad), color = AnnotationColor("#112233"), opacity = 0.5,
            interiorColor = value(AnnotationColor("#000000")), overlayText = value("REDACTED"), repeat = true,
            fontFamily = "courier", fontSize = 9.0, fontColor = AnnotationColor("#ffffff"),
            textAlign = AnnotationTextAlignment.CENTER, contents = value("Redaction text"),
            blendMode = AnnotationBlendMode.NORMAL,
        )
        val fullFreeText = FreeTextAnnotationDraft(
            box, contents = value("Free text"), fontFamily = "times-roman", fontSize = 14.0,
            fontColor = AnnotationColor("#112233"), textAlign = AnnotationTextAlignment.CENTER,
            verticalAlign = AnnotationVerticalAlignment.MIDDLE, color = AnnotationColor("#445566"),
            interiorColor = value(AnnotationColor("#ffffcc")), opacity = 0.75, strokeWidth = 2.0,
            borderStyle = AnnotationBorderStyle.DASHED, dashArray = value(listOf(2.0, 1.0)), cloudyIntensity = value(1.0),
            intent = value(AnnotationFreeTextIntent.FREE_TEXT_CALLOUT),
            calloutLine = value(AnnotationCalloutLine.Kneed(Point(20.0, 20.0), Point(60.0, 60.0), Point(100.0, 130.0))),
            lineEnding = value(AnnotationLineEnding.OPEN_ARROW), blendMode = AnnotationBlendMode.MULTIPLY,
        )
        val fullCaret = CaretAnnotationDraft(
            Rect(72.0, 300.0, 12.0, 12.0), color = AnnotationColor("#00ff00"), opacity = 0.5,
            intent = value(AnnotationCaretIntent.REPLACE), contents = value("Inserted"), blendMode = AnnotationBlendMode.EXCLUSION,
        )
        val fullLink = LinkAnnotationDraft(box, AnnotationLinkTarget.Uri("https://example.test/typed"), contents = value("Link text"))
    }

    // ── exact encoding ──

    @Test
    fun textMarkupDraftsEncodeEveryMember() {
        assertEquals(
            """{"blendMode":"multiply","color":"#0000ff","contents":"Underline text","opacity":0.5,"quadPoints":$QUAD_JSON,"subtype":"underline"}""",
            fullUnderline.json(),
        )
        assertEquals(
            """{"blendMode":"darken","color":"#00ff00","contents":"Squiggly text","opacity":0.75,"quadPoints":$QUAD_JSON,"subtype":"squiggly"}""",
            fullSquiggly.json(),
        )
        assertEquals(
            """{"blendMode":"screen","color":"#ff0000","contents":"Strikeout text","intent":"strikeout-text-edit","opacity":0.25,"quadPoints":$QUAD_JSON,"subtype":"strikeout"}""",
            fullStrikeout.json(),
        )
        assertEquals(
            """{"intent":null,"quadPoints":$QUAD_JSON,"subtype":"strikeout"}""",
            StrikeoutAnnotationDraft(listOf(quad), intent = Nullable.Clear).json(),
        )
    }

    @Test
    fun aRedactionDraftEncodesEveryMember() {
        assertEquals(
            """{"blendMode":"normal","color":"#112233","contents":"Redaction text","fontColor":"#ffffff","fontFamily":"courier","fontSize":9,"interiorColor":"#000000","opacity":0.5,"overlayText":"REDACTED","quadPoints":$QUAD_JSON,"repeat":true,"subtype":"redact","textAlign":"center"}""",
            fullRedaction.json(),
        )
        assertEquals(
            """{"interiorColor":null,"overlayText":null,"rect":{"height":40,"width":80,"x":100,"y":120},"subtype":"redact"}""",
            RedactionAnnotationDraft(rect = box, interiorColor = Nullable.Clear, overlayText = Nullable.Clear).json(),
        )
    }

    @Test
    fun boxDraftsEncodeEveryMember() {
        assertEquals(
            """{"blendMode":"multiply","borderStyle":"dashed","box":{"height":40,"width":80,"x":100,"y":120},"calloutLine":[{"x":20,"y":20},{"x":60,"y":60},{"x":100,"y":130}],"cloudyIntensity":1,"color":"#445566","contents":"Free text","dashArray":[2,1],"fontColor":"#112233","fontFamily":"times-roman","fontSize":14,"intent":"free-text-callout","interiorColor":"#ffffcc","lineEnding":"open-arrow","opacity":0.75,"strokeWidth":2,"subtype":"free-text","textAlign":"center","verticalAlign":"middle"}""",
            fullFreeText.json(),
        )
        assertEquals(
            """{"blendMode":"exclusion","box":{"height":12,"width":12,"x":72,"y":300},"color":"#00ff00","contents":"Inserted","intent":"replace","opacity":0.5,"subtype":"caret"}""",
            fullCaret.json(),
        )
        assertEquals(
            """{"contents":"Link text","rect":{"height":40,"width":80,"x":100,"y":120},"subtype":"link","target":{"kind":"uri","uri":"https://example.test/typed"}}""",
            fullLink.json(),
        )
        assertEquals(
            """{"box":{"height":40,"width":80,"x":100,"y":120},"contents":"Stamp text","fit":null,"name":"Approved","opacity":0.5,"rotation":90}""",
            StampAnnotationPatch(box, 0.5, value(90.0), value("Approved"), Nullable.Clear, value("Stamp text")).json(),
        )
        assertEquals(
            """{"blendMode":"multiply","color":"#facc15","contents":null,"icon":"push-pin","opacity":0.5,"rect":{"height":40,"width":80,"x":100,"y":120}}""",
            FileAttachmentAnnotationPatch(
                box, AnnotationFileAttachmentIcon.PUSH_PIN, AnnotationColor("#facc15"), 0.5, Nullable.Clear, AnnotationBlendMode.MULTIPLY,
            ).json(),
        )
    }

    @Test
    fun aCalloutLineIsTwoOrThreePoints() {
        val straight = FreeTextAnnotationPatch(calloutLine = value(AnnotationCalloutLine.Straight(Point(1.0, 2.0), Point(3.0, 4.0))))
        assertEquals("""{"calloutLine":[{"x":1,"y":2},{"x":3,"y":4}]}""", straight.json())
        assertEquals(
            """{"calloutLine":null,"lineEnding":null}""",
            FreeTextAnnotationPatch(calloutLine = Nullable.Clear, lineEnding = Nullable.Clear).json(),
        )
    }

    @Test
    fun aLinkTargetIsTaggedByKind() {
        val page = PageRef(7u)
        assertEquals(
            """{"target":{"destination":{"kind":"xyz","page":{"kind":"objectNumber","objectNumber":7},"x":10,"y":null,"zoom":2},"kind":"goto"}}""",
            LinkAnnotationPatch(target = value(AnnotationLinkTarget.Goto(PageDestinationXyz(page, 10.0, null, 2.0)))).json(),
        )
        assertEquals(
            """{"target":{"kind":"named","name":"PrevPage"}}""",
            LinkAnnotationPatch(target = value(AnnotationLinkTarget.Named(AnnotationLinkVerb.PREVIOUS_PAGE))).json(),
        )
        assertEquals(
            """{"target":{"destination":{"height":4,"kind":"fitR","page":{"kind":"objectNumber","objectNumber":7},"width":3,"x":1,"y":2},"kind":"goto"}}""",
            LinkAnnotationPatch(target = value(AnnotationLinkTarget.Goto(PageDestinationFitR(page, 1.0, 2.0, 3.0, 4.0)))).json(),
        )
        assertEquals("""{"target":null}""", LinkAnnotationPatch(target = Nullable.Clear).json())
    }

    @Test
    fun emptyPatchesAreEmptyObjects() {
        val patches: List<AnnotationPatch> = listOf(
            UnderlineAnnotationPatch(), SquigglyAnnotationPatch(), StrikeoutAnnotationPatch(), RedactionAnnotationPatch(),
            FreeTextAnnotationPatch(), CaretAnnotationPatch(), LinkAnnotationPatch(), StampAnnotationPatch(),
            FileAttachmentAnnotationPatch(),
        )
        for (patch in patches) assertEquals(patch.javaClass.simpleName, "{}", patch.json())
    }

    @Test
    fun theVocabularyIsTheEnginesSpelling() {
        assertEquals(
            listOf(
                "normal", "multiply", "screen", "overlay", "darken", "lighten", "color-dodge", "color-burn",
                "hard-light", "soft-light", "difference", "exclusion", "hue", "saturation", "color", "luminosity",
            ),
            AnnotationBlendMode.entries.map { it.value },
        )
        assertEquals(listOf("NextPage", "PrevPage", "FirstPage", "LastPage"), AnnotationLinkVerb.entries.map { it.value })
        assertEquals(listOf("left", "center", "right", "justify"), AnnotationTextAlignment.entries.map { it.value })
        assertEquals(listOf("strikeout-text-edit"), AnnotationStrikeoutIntent.entries.map { it.value })
        assertEquals(
            listOf("none", "square", "circle", "diamond", "open-arrow", "closed-arrow", "butt", "r-open-arrow", "r-closed-arrow", "slash"),
            AnnotationLineEnding.entries.map { it.value },
        )
        assertEquals(listOf("graph", "push-pin", "paperclip", "tag"), AnnotationFileAttachmentIcon.entries.map { it.value })
        assertEquals(listOf("top", "middle", "bottom"), AnnotationVerticalAlignment.entries.map { it.value })
        assertEquals(listOf("contain", "cover", "fill"), AnnotationStampFit.entries.map { it.value })
        assertEquals(listOf("free-text", "free-text-callout"), AnnotationFreeTextIntent.entries.map { it.value })
        assertEquals(listOf("replace"), AnnotationCaretIntent.entries.map { it.value })
    }
}
