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
    fun emptyPatchesAreEmptyObjects() {
        val patches: List<AnnotationPatch> = listOf(
            UnderlineAnnotationPatch(), SquigglyAnnotationPatch(), StrikeoutAnnotationPatch(), RedactionAnnotationPatch(),
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
    }
}
