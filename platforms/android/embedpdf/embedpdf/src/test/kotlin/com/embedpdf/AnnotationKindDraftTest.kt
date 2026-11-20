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
        val fullSquare = SquareAnnotationDraft(
            box, color = value(AnnotationColor("#ff0000")), opacity = 0.5, strokeWidth = 3.0, borderStyle = AnnotationBorderStyle.DASHED,
            dashArray = value(listOf(4.0, 2.0)), interiorColor = value(AnnotationColor("#00ff00")), rotation = value(15.0),
            cloudyIntensity = value(2.0), contents = value("Square text"), blendMode = AnnotationBlendMode.MULTIPLY,
        )
        val fullCircle = CircleAnnotationDraft(
            box, color = value(AnnotationColor("#0000ff")), opacity = 0.75, strokeWidth = 2.0, borderStyle = AnnotationBorderStyle.SOLID,
            interiorColor = value(AnnotationColor("#ffff00")), rotation = value(30.0), contents = value("Circle text"),
            blendMode = AnnotationBlendMode.OVERLAY,
        )
        val fullLine = LineAnnotationDraft(
            Point(10.0, 10.0), Point(100.0, 50.0), color = value(AnnotationColor("#ff00ff")), opacity = 0.5, strokeWidth = 2.0,
            borderStyle = AnnotationBorderStyle.DASHED, dashArray = value(listOf(3.0, 1.0)), interiorColor = value(AnnotationColor("#00ff00")),
            lineEndings = AnnotationLineEndings(AnnotationLineEnding.OPEN_ARROW, AnnotationLineEnding.REVERSED_CLOSED_ARROW),
            intent = value(AnnotationLineIntent.LINE_ARROW), captionEnabled = true, captionPosition = AnnotationCaptionPosition.TOP,
            captionOffset = value(AnnotationCaptionOffset(1.0, 2.0)), leader = value(AnnotationLineLeader(5.0, 1.0, 2.0)),
            contents = value("Line text"), blendMode = AnnotationBlendMode.HARD_LIGHT,
        )
        val fullPolygon = PolygonAnnotationDraft(
            listOf(Point(10.0, 10.0), Point(100.0, 10.0), Point(50.0, 80.0)), color = value(AnnotationColor("#0000ff")), opacity = 0.5,
            strokeWidth = 2.0, borderStyle = AnnotationBorderStyle.DASHED, dashArray = value(listOf(2.0, 2.0)),
            interiorColor = value(AnnotationColor("#ffff00")), intent = value(AnnotationPolygonIntent.POLYGON_CLOUD),
            cloudyIntensity = value(2.0), captionEnabled = value(true), captionCenter = AnnotationCaptionCenter.At(Point(50.0, 30.0)),
            contents = value("Polygon text"), blendMode = AnnotationBlendMode.LIGHTEN,
        )
        val fullPolyline = PolylineAnnotationDraft(
            listOf(Point(10.0, 10.0), Point(60.0, 80.0), Point(100.0, 10.0)), color = value(AnnotationColor("#00ffff")), opacity = 0.75,
            strokeWidth = 3.0, borderStyle = AnnotationBorderStyle.SOLID, interiorColor = value(AnnotationColor("#ff0000")),
            lineEndings = AnnotationLineEndings(AnnotationLineEnding.CIRCLE, AnnotationLineEnding.DIAMOND),
            captionEnabled = value(true), captionCenter = AnnotationCaptionCenter.At(Point(60.0, 40.0)),
            contents = value("Polyline text"), blendMode = AnnotationBlendMode.DIFFERENCE,
        )
        val fullLink = LinkAnnotationDraft(box, AnnotationLinkTarget.Uri("https://example.test/typed"), contents = value("Link text"))

        val fullDrafts: List<AnnotationDraft>
            get() = listOf(
                fullUnderline, fullSquiggly, fullStrikeout, fullRedaction, fullSquare, fullCircle, fullLine, fullPolygon,
                fullPolyline, fullFreeText, fullCaret, fullLink,
            )

        /** One inch on the page is one foot: every member a scale carries. */
        val scale = PdfMeasurement(
            subtype = "rectilinear", ratio = "1 in = 1 ft",
            x = listOf(
                PdfNumberFormat(
                    "ft", conversion = 0.125, fraction = MeasureFraction.DECIMAL, precision = 100.0, fixed = true,
                    thousands = ",", decimal = ".", prefixSpacing = "", suffixSpacing = " ", labelPosition = MeasureLabelPosition.SUFFIX,
                ),
            ),
            y = listOf(PdfNumberFormat("ft", conversion = 0.25, fraction = MeasureFraction.ROUND, precision = 1.0)),
            distance = listOf(
                PdfNumberFormat("ft", conversion = 1.0, fraction = MeasureFraction.FRACTION, precision = 16.0),
                PdfNumberFormat("in", conversion = 12.0, fraction = MeasureFraction.TRUNCATE, precision = 1.0, labelPosition = MeasureLabelPosition.PREFIX),
            ),
            area = listOf(PdfNumberFormat("sq ft", conversion = 1.0, fraction = MeasureFraction.DECIMAL, precision = 10.0)),
            angle = listOf(PdfNumberFormat("deg", conversion = 1.0, fraction = MeasureFraction.DECIMAL, precision = 1.0)),
            slope = listOf(PdfNumberFormat("%", conversion = 100.0, fraction = MeasureFraction.DECIMAL, precision = 1.0)),
            cyx = 2.0,
        )
    }

    // ── exact encoding ──

    @Test
    fun aLineDraftEncodesItsStructuredMembers() {
        assertEquals(
            """{"blendMode":"hard-light","borderStyle":"dashed","captionEnabled":true,"captionOffset":{"along":1,"perpendicular":2},"captionPosition":"top","color":"#ff00ff","contents":"Line text","dashArray":[3,1],"intent":"line-arrow","interiorColor":"#00ff00","leader":{"extension":1,"length":5,"offset":2},"lineEndings":{"end":"r-closed-arrow","start":"open-arrow"},"linePoints":{"end":{"x":100,"y":50},"start":{"x":10,"y":10}},"opacity":0.5,"strokeWidth":2,"subtype":"line"}""",
            fullLine.json(),
        )
        assertEquals(
            """{"leader":null,"linePoints":{"end":{"x":1,"y":1},"start":{"x":0,"y":0}},"subtype":"line"}""",
            LineAnnotationDraft(Point(0.0, 0.0), Point(1.0, 1.0), leader = Nullable.Clear).json(),
        )
    }

    @Test
    fun aLinePatchMovesBothEndsOrNeither() = viewerTest {
        assertEquals(
            """{"linePoints":{"end":{"x":3,"y":4},"start":{"x":1,"y":2}}}""",
            LineAnnotationPatch(start = Point(1.0, 2.0), end = Point(3.0, 4.0)).json(),
        )
        expectCode(PdfException.Code.INVALID_INPUT) { LineAnnotationPatch(start = Point(1.0, 2.0)).json() }
    }

    @Test
    fun aCaptionCenterIsAPointOrNull() {
        val vertices = listOf(Point(0.0, 0.0), Point(1.0, 0.0), Point(0.0, 1.0))
        assertEquals("""{"captionCenter":null}""", PolygonAnnotationPatch(captionCenter = AnnotationCaptionCenter.Automatic).json())
        assertEquals(
            """{"captionCenter":{"x":2,"y":3},"subtype":"polygon","vertices":[{"x":0,"y":0},{"x":1,"y":0},{"x":0,"y":1}]}""",
            PolygonAnnotationDraft(vertices, captionCenter = AnnotationCaptionCenter.At(Point(2.0, 3.0))).json(),
        )
    }

    @Test
    fun shapeDraftsEncodeEveryMember() {
        assertEquals(
            """{"blendMode":"multiply","borderStyle":"dashed","box":{"height":40,"width":80,"x":100,"y":120},"cloudyIntensity":2,"color":"#ff0000","contents":"Square text","dashArray":[4,2],"interiorColor":"#00ff00","opacity":0.5,"rotation":15,"strokeWidth":3,"subtype":"square"}""",
            fullSquare.json(),
        )
        assertEquals(
            """{"blendMode":"overlay","borderStyle":"solid","box":{"height":40,"width":80,"x":100,"y":120},"color":"#0000ff","contents":"Circle text","interiorColor":"#ffff00","opacity":0.75,"rotation":30,"strokeWidth":2,"subtype":"circle"}""",
            fullCircle.json(),
        )
        assertEquals(
            """{"blendMode":"lighten","borderStyle":"dashed","captionCenter":{"x":50,"y":30},"captionEnabled":true,"cloudyIntensity":2,"color":"#0000ff","contents":"Polygon text","dashArray":[2,2],"intent":"polygon-cloud","interiorColor":"#ffff00","opacity":0.5,"strokeWidth":2,"subtype":"polygon","vertices":[{"x":10,"y":10},{"x":100,"y":10},{"x":50,"y":80}]}""",
            fullPolygon.json(),
        )
        assertEquals(
            """{"blendMode":"difference","borderStyle":"solid","captionCenter":{"x":60,"y":40},"captionEnabled":true,"color":"#00ffff","contents":"Polyline text","interiorColor":"#ff0000","lineEndings":{"end":"diamond","start":"circle"},"opacity":0.75,"strokeWidth":3,"subtype":"polyline","vertices":[{"x":10,"y":10},{"x":60,"y":80},{"x":100,"y":10}]}""",
            fullPolyline.json(),
        )
        assertEquals("""{"box":{"height":40,"width":80,"x":100,"y":120},"color":null,"subtype":"square"}""", SquareAnnotationDraft(box, color = Nullable.Clear).json())
    }

    @Test
    fun aMeasureEncodesEveryMember() {
        val json =
            """{"angle":[{"conversion":1,"fraction":"decimal","precision":1,"unit":"deg"}],"area":[{"conversion":1,"fraction":"decimal","precision":10,"unit":"sq ft"}],"cyx":2,"distance":[{"conversion":1,"fraction":"fraction","precision":16,"unit":"ft"},{"conversion":12,"fraction":"truncate","labelPosition":"prefix","precision":1,"unit":"in"}],"origin":{"x":3,"y":4},"ratio":"1 in = 1 ft","slope":[{"conversion":100,"fraction":"decimal","precision":1,"unit":"%"}],"subtype":"rectilinear","x":[{"conversion":0.125,"decimal":".","fixed":true,"fraction":"decimal","labelPosition":"suffix","precision":100,"prefixSpacing":"","suffixSpacing":" ","thousands":",","unit":"ft"}],"y":[{"conversion":0.25,"fraction":"round","precision":1,"unit":"ft"}]}"""
        assertEquals("""{"measure":$json}""", PolygonAnnotationPatch(measure = value(scale.copy(origin = MeasureOrigin(3.0, 4.0)))).json())
        assertEquals("""{"measure":null}""", PolylineAnnotationPatch(measure = Nullable.Clear).json())
    }

    @Test
    fun eachDraftNamesItsSubtype() {
        assertEquals(
            listOf(
                "underline", "squiggly", "strikeout", "redact", "square", "circle", "line", "polygon", "polyline", "free-text",
                "caret", "link",
            ),
            fullDrafts.map { kotlinx.serialization.json.Json.parseToJsonElement(it.json()).let { json -> (json as kotlinx.serialization.json.JsonObject).string("subtype") } },
        )
    }

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
            FileAttachmentAnnotationPatch(), SquareAnnotationPatch(), CircleAnnotationPatch(), LineAnnotationPatch(),
            PolygonAnnotationPatch(), PolylineAnnotationPatch(),
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
        assertEquals(listOf("inline", "top"), AnnotationCaptionPosition.entries.map { it.value })
        assertEquals(listOf("line-arrow", "line-dimension"), AnnotationLineIntent.entries.map { it.value })
        assertEquals(listOf("polygon-cloud", "polygon-dimension"), AnnotationPolygonIntent.entries.map { it.value })
        assertEquals(listOf("polyline-dimension"), AnnotationPolylineIntent.entries.map { it.value })
    }
}
