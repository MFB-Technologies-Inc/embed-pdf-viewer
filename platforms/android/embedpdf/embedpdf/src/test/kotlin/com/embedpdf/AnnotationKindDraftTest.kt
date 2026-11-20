package com.embedpdf

import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
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

    // ── drift guard ──

    /** Every key a full draft writes comes back in the engine's read, with the value written. */
    @Test
    fun everyTypedDraftKeyRoundTripsThroughTheEngine() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        for (draft in fullDrafts) {
            val read = checkNotNull(annotations.get(annotations.create(draft, page)))
            expectWritten(draft.json(), read.fieldsJson, draft.javaClass.simpleName)
            assertNotEquals("${draft.javaClass.simpleName} read back untyped", AnnotationInfo.Details.Untyped, read.details)
        }
        viewer.destroy()
    }

    /** Every key a full patch writes comes back in the engine's read, and the typed read changes with it. */
    @Test
    fun everyTypedPatchKeyRoundTripsThroughTheEngine() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val moved = Quad(Point(80.0, 400.0), Point(160.0, 400.0), Point(80.0, 414.0), Point(160.0, 414.0))
        val otherBox = Rect(200.0, 220.0, 60.0, 30.0)
        val other = AnnotationColor("#123456")
        val black = AnnotationColor("#000000")
        val cases: List<Pair<AnnotationDraft, AnnotationPatch>> = listOf(
            fullUnderline to UnderlineAnnotationPatch(listOf(moved), other, 1.0, value("Changed"), AnnotationBlendMode.NORMAL),
            fullSquiggly to SquigglyAnnotationPatch(listOf(moved), other, 1.0, value("Changed"), AnnotationBlendMode.NORMAL),
            fullStrikeout to StrikeoutAnnotationPatch(listOf(moved), other, 1.0, Nullable.Clear, value("Changed"), AnnotationBlendMode.NORMAL),
            fullRedaction to RedactionAnnotationPatch(
                quadPoints = listOf(moved), color = AnnotationColor("#654321"), opacity = 1.0, interiorColor = Nullable.Clear,
                overlayText = Nullable.Clear, repeat = false, fontFamily = "helvetica", fontSize = 0.0, fontColor = black,
                textAlign = AnnotationTextAlignment.RIGHT, contents = value("Changed"), blendMode = AnnotationBlendMode.MULTIPLY,
            ),
            fullSquare to SquareAnnotationPatch(
                otherBox, Nullable.Clear, 1.0, 0.0, AnnotationBorderStyle.SOLID, Nullable.Clear, Nullable.Clear, Nullable.Clear,
                Nullable.Clear, value("Changed"), AnnotationBlendMode.NORMAL,
            ),
            fullCircle to CircleAnnotationPatch(
                otherBox, value(black), 0.25, 4.0, AnnotationBorderStyle.DASHED, value(listOf(1.0, 1.0)),
                value(AnnotationColor("#ffffff")), value(45.0), value(1.0), value("Changed"), AnnotationBlendMode.NORMAL,
            ),
            fullLine to LineAnnotationPatch(
                start = Point(20.0, 30.0), end = Point(120.0, 90.0), color = Nullable.Clear, opacity = 1.0, strokeWidth = 4.0,
                borderStyle = AnnotationBorderStyle.SOLID, dashArray = Nullable.Clear, interiorColor = Nullable.Clear,
                lineEndings = AnnotationLineEndings(AnnotationLineEnding.NONE, AnnotationLineEnding.SLASH), intent = Nullable.Clear,
                captionEnabled = false, captionPosition = AnnotationCaptionPosition.INLINE, captionOffset = Nullable.Clear,
                leader = Nullable.Clear, contents = value("Changed"), blendMode = AnnotationBlendMode.NORMAL,
            ),
            fullPolygon to PolygonAnnotationPatch(
                vertices = listOf(Point(0.0, 0.0), Point(40.0, 0.0), Point(40.0, 40.0), Point(0.0, 40.0)), color = value(black),
                opacity = 1.0, strokeWidth = 1.0, borderStyle = AnnotationBorderStyle.SOLID, dashArray = Nullable.Clear,
                interiorColor = Nullable.Clear, intent = Nullable.Clear, cloudyIntensity = Nullable.Clear,
                captionEnabled = value(false), contents = value("Changed"), blendMode = AnnotationBlendMode.NORMAL,
            ),
            fullPolyline to PolylineAnnotationPatch(
                vertices = listOf(Point(0.0, 0.0), Point(40.0, 40.0)), color = Nullable.Clear, opacity = 1.0, strokeWidth = 1.0,
                borderStyle = AnnotationBorderStyle.DASHED, dashArray = value(listOf(5.0, 5.0)), interiorColor = Nullable.Clear,
                lineEndings = AnnotationLineEndings(AnnotationLineEnding.BUTT, AnnotationLineEnding.CLOSED_ARROW),
                captionEnabled = value(false), contents = value("Changed"), blendMode = AnnotationBlendMode.NORMAL,
            ),
            fullFreeText to FreeTextAnnotationPatch(
                box = otherBox, contents = value("Changed"), fontFamily = "helvetica", fontSize = 10.0, fontColor = black,
                textAlign = AnnotationTextAlignment.JUSTIFY, verticalAlign = AnnotationVerticalAlignment.BOTTOM, color = black,
                interiorColor = Nullable.Clear, opacity = 1.0, strokeWidth = 0.0, borderStyle = AnnotationBorderStyle.SOLID,
                dashArray = Nullable.Clear, cloudyIntensity = Nullable.Clear, rotation = value(90.0),
                intent = value(AnnotationFreeTextIntent.FREE_TEXT), calloutLine = Nullable.Clear, lineEnding = Nullable.Clear,
                blendMode = AnnotationBlendMode.NORMAL,
            ),
            fullCaret to CaretAnnotationPatch(
                Rect(90.0, 310.0, 10.0, 10.0), black, 1.0, value(45.0), Nullable.Clear, value("Changed"), AnnotationBlendMode.NORMAL,
            ),
            fullLink to LinkAnnotationPatch(otherBox, value(AnnotationLinkTarget.Named(AnnotationLinkVerb.NEXT_PAGE)), value("Changed")),
        )
        for ((draft, patch) in cases) {
            val ref = annotations.create(draft, page)
            annotations.update(ref, patch)
            val read = checkNotNull(annotations.get(ref))
            expectWritten(patch.json(), read.fieldsJson, patch.javaClass.simpleName)
            assertNotEquals("${patch.javaClass.simpleName} read back untyped", AnnotationInfo.Details.Untyped, read.details)
        }
        viewer.destroy()
    }

    /** Stamps and file attachments have no draft: a placed stamp and the fixture's attachment take the patches. */
    @Test
    fun stampAndFileAttachmentPatchesRoundTrip() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val stamp = annotations.drafts.placeStamp(
            AnnotationStampSource(AnnotationTest.tinyPng(), mimeType = "image/png"),
            AnnotationStampPlacement(page, Point(200.0, 200.0)),
            targetWidth = 80.0,
        )
        val stampPatch = StampAnnotationPatch(
            Rect(150.0, 310.0, 100.0, 40.0), 0.5, value(90.0), value("Draft"), value(AnnotationStampFit.COVER), value("Changed"),
            common = sharedFields,
        )
        annotations.update(stamp, stampPatch)
        expectWritten(stampPatch.json(), checkNotNull(annotations.get(stamp)).fieldsJson, "StampAnnotationPatch")

        val (kindsViewer, _, kinds) = openedPage("annotation_kinds")
        val attachment = kinds.list().first { it.subtype == AnnotationSubtype.FILE_ATTACHMENT }
        val attachmentPatch = FileAttachmentAnnotationPatch(
            Rect(470.0, 620.0, 24.0, 24.0), AnnotationFileAttachmentIcon.TAG, AnnotationColor("#00ff00"), 0.5, value("Changed"),
            AnnotationBlendMode.MULTIPLY, common = sharedFields,
        )
        kinds.update(attachment.annotationRef, attachmentPatch)
        val attachmentRead = checkNotNull(kinds.get(attachment.annotationRef))
        expectWritten(attachmentPatch.json(), attachmentRead.fieldsJson, "FileAttachmentAnnotationPatch")
        assertEquals(AnnotationFileAttachmentIcon.TAG, attachmentRead.fileAttachment?.icon)
        assertEquals("notes.txt", attachmentRead.fileAttachment?.file?.name)
        kindsViewer.destroy()
        viewer.destroy()
    }

    /** A patch that changes nothing but the blend mode still redraws the appearance, where `/BM` lives. */
    @Test
    fun aBlendModeAloneChanges() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val highlight = annotations.create(AnnotationDraftTest.fullHighlight, page)
        annotations.update(highlight, HighlightAnnotationPatch(blendMode = AnnotationBlendMode.NORMAL))
        assertEquals(AnnotationBlendMode.NORMAL, annotations.get(highlight)?.blendMode)
        val square = annotations.create(fullSquare, page)
        annotations.update(square, SquareAnnotationPatch(blendMode = AnnotationBlendMode.SCREEN))
        assertEquals(AnnotationBlendMode.SCREEN, annotations.get(square)?.blendMode)
        viewer.destroy()
    }

    // ── common fields ──

    /**
     * The fields every kind shares, each away from what a create defaults to.
     * No `nm`, which is one annotation's on its page, and no `reply`, which
     * needs an annotation to answer.
     */
    private val sharedFields = AnnotationCommonFields(subject = value("S"), groupId = value("reviewers"), print = true, locked = true)

    @Test
    fun commonFieldsEncodeFlatBesideTheKinds() {
        val common = AnnotationCommonFields(
            nm = value("note-1"), subject = Nullable.Clear, groupId = value("reviewers"),
            reply = value(AnnotationReply(AnnotationRefObjectNumber(PageRef(2u), 9u), AnnotationReplyType.GROUP)),
            invisible = false, hidden = true, print = false, noZoom = true, noRotate = true, noView = false, readOnly = true,
            locked = true, toggleNoView = false, lockedContents = true,
        )
        assertEquals(
            """{"groupId":"reviewers","hidden":true,"icon":"key","invisible":false,"locked":true,"lockedContents":true,"nm":"note-1","noRotate":true,"noView":false,"noZoom":true,"print":false,"readOnly":true,"rect":{"height":24,"width":24,"x":40,"y":40},"reply":{"to":{"kind":"objectNumber","objectNumber":9,"page":{"kind":"objectNumber","objectNumber":2}},"type":"group"},"subject":null,"subtype":"text","toggleNoView":false}""",
            NoteAnnotationDraft(AnnotationDraftTest.noteRect, icon = AnnotationNoteIcon.KEY, common = common).json(),
        )
        // A patch writes the same members but `nm`: a name can't change after create.
        assertEquals(
            """{"groupId":"reviewers","hidden":true,"icon":"key","invisible":false,"locked":true,"lockedContents":true,"noRotate":true,"noView":false,"noZoom":true,"print":false,"readOnly":true,"reply":{"to":{"kind":"objectNumber","objectNumber":9,"page":{"kind":"objectNumber","objectNumber":2}},"type":"group"},"subject":null,"toggleNoView":false}""",
            NoteAnnotationPatch(icon = AnnotationNoteIcon.KEY, common = common).json(),
        )
        assertEquals(
            """{"groupId":null,"reply":null}""",
            SquareAnnotationPatch(common = AnnotationCommonFields(reply = Nullable.Clear, groupId = Nullable.Clear)).json(),
        )
        assertEquals(listOf("reply", "group"), AnnotationReplyType.entries.map { it.value })
    }

    /** Every kind's draft writes the shared fields, and the engine reads them back as written. */
    @Test
    fun everyDraftWritesItsCommonFields() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val drafts: List<AnnotationDraft> = listOf(
            AnnotationDraftTest.fullInk.copy(common = sharedFields), AnnotationDraftTest.fullHighlight.copy(common = sharedFields),
            AnnotationDraftTest.fullNote.copy(common = sharedFields), fullUnderline.copy(common = sharedFields),
            fullSquiggly.copy(common = sharedFields), fullStrikeout.copy(common = sharedFields),
            fullRedaction.copy(common = sharedFields), fullSquare.copy(common = sharedFields), fullCircle.copy(common = sharedFields),
            fullLine.copy(common = sharedFields), fullPolygon.copy(common = sharedFields), fullPolyline.copy(common = sharedFields),
            fullFreeText.copy(common = sharedFields), fullCaret.copy(common = sharedFields), fullLink.copy(common = sharedFields),
        )
        for (draft in drafts) {
            val what = draft.javaClass.simpleName
            val read = checkNotNull(annotations.get(annotations.create(draft, page)))
            val fields = jsonObject(read.fieldsJson)
            assertEquals(what, JsonPrimitive("S"), fields["subject"])
            assertEquals(what, JsonPrimitive(true), fields["print"])
            assertEquals(what, JsonPrimitive(true), fields["locked"])
            assertEquals(what, "reviewers", read.groupId)
            expectWritten(draft.json(), read.fieldsJson, what)
        }
        viewer.destroy()
    }

    /** Every kind's patch writes the shared fields. Each annotation starts from a draft without them. */
    @Test
    fun everyPatchWritesItsCommonFields() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val common = sharedFields
        val cases: List<Pair<AnnotationDraft, AnnotationPatch>> = listOf(
            AnnotationDraftTest.fullInk to InkAnnotationPatch(common = common),
            AnnotationDraftTest.fullHighlight to HighlightAnnotationPatch(common = common),
            AnnotationDraftTest.fullNote to NoteAnnotationPatch(common = common),
            fullUnderline to UnderlineAnnotationPatch(common = common),
            fullSquiggly to SquigglyAnnotationPatch(common = common),
            fullStrikeout to StrikeoutAnnotationPatch(common = common),
            fullRedaction to RedactionAnnotationPatch(common = common),
            fullSquare to SquareAnnotationPatch(common = common),
            fullCircle to CircleAnnotationPatch(common = common),
            fullLine to LineAnnotationPatch(common = common),
            fullPolygon to PolygonAnnotationPatch(common = common),
            fullPolyline to PolylineAnnotationPatch(common = common),
            fullFreeText to FreeTextAnnotationPatch(common = common),
            fullCaret to CaretAnnotationPatch(common = common),
            fullLink to LinkAnnotationPatch(common = common),
        )
        for ((draft, patch) in cases) {
            val ref = annotations.create(draft, page)
            annotations.update(ref, patch)
            val read = checkNotNull(annotations.get(ref))
            assertEquals(patch.javaClass.simpleName, """{"groupId":"reviewers","locked":true,"print":true,"subject":"S"}""", patch.json())
            expectWritten(patch.json(), read.fieldsJson, patch.javaClass.simpleName)
        }
        viewer.destroy()
    }

    @Test
    fun aReplyLinksANoteToTheAnnotationItAnswers() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val other = annotations.create(NoteAnnotationDraft(box), page)
        var common = AnnotationCommonFields(reply = value(AnnotationReply(other)))
        val answer = annotations.create(NoteAnnotationDraft(AnnotationDraftTest.noteRect, common = common), page)
        val read = checkNotNull(annotations.get(answer))
        assertEquals(other, read.inReplyTo)
        assertEquals("reply", jsonObject(read.fieldsJson).getValue("reply").jsonObject.getValue("type").jsonPrimitive.content)

        common = AnnotationCommonFields(reply = value(AnnotationReply(other, AnnotationReplyType.GROUP)))
        annotations.update(answer, NoteAnnotationPatch(common = common))
        val grouped = checkNotNull(annotations.get(answer))
        assertEquals(other, grouped.inReplyTo)
        assertEquals("group", jsonObject(grouped.fieldsJson).getValue("reply").jsonObject.getValue("type").jsonPrimitive.content)

        annotations.update(answer, NoteAnnotationPatch(common = AnnotationCommonFields(reply = Nullable.Clear)))
        assertNull(checkNotNull(annotations.get(answer)).inReplyTo)
        viewer.destroy()
    }

    /** A draft names its annotation; a patch that carries a name leaves it out, where the engine would refuse it. */
    @Test
    fun aNameIsTheDraftsToSet() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val ref = annotations.create(SquareAnnotationDraft(box, common = AnnotationCommonFields(nm = value("typed-name"))), page)
        assertEquals(JsonPrimitive("typed-name"), jsonObject(checkNotNull(annotations.get(ref)).fieldsJson)["nm"])

        annotations.update(ref, SquareAnnotationPatch(common = AnnotationCommonFields(nm = value("another-name"), subject = value("Renamed"))))
        val fields = jsonObject(checkNotNull(annotations.get(ref)).fieldsJson)
        assertEquals(JsonPrimitive("typed-name"), fields["nm"])
        assertEquals(JsonPrimitive("Renamed"), fields["subject"])
        expectCode(PdfException.Code.INVALID_INPUT) { annotations.update(ref, """{"nm":"another-name"}""") }

        val unnamed = checkNotNull(
            annotations.get(annotations.create(SquareAnnotationDraft(box, common = AnnotationCommonFields(nm = Nullable.Clear)), page)),
        )
        assertEquals(JsonNull, jsonObject(unnamed.fieldsJson)["nm"])
        viewer.destroy()
    }

    // ── typed reads ──

    @Test
    fun createdDraftsReadBackTyped() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        suspend fun created(draft: AnnotationDraft): AnnotationInfo = checkNotNull(annotations.get(annotations.create(draft, page)))

        val underline = created(fullUnderline)
        assertEquals(AnnotationBlendMode.MULTIPLY, underline.blendMode)
        assertEquals(AnnotationColor("#0000ff"), underline.underline?.color)
        assertEquals(listOf(quad), underline.underline?.quadPoints)
        assertEquals(0.75, created(fullSquiggly).squiggly?.opacity)
        assertEquals(AnnotationIntent.STRIKEOUT_TEXT_EDIT, created(fullStrikeout).strikeout?.intent)

        val redaction = checkNotNull(created(fullRedaction).redaction)
        assertEquals("REDACTED", redaction.overlayText)
        assertEquals(true, redaction.repeat)
        assertEquals(AnnotationTextAlignment.CENTER, redaction.textAlign)
        assertEquals(AnnotationColor("#000000"), redaction.interiorColor)
        assertEquals(AnnotationColor("#ffffff"), redaction.fontColor)

        val square = checkNotNull(created(fullSquare).square)
        assertEquals(box, square.box)
        assertEquals(AnnotationColor("#00ff00"), square.interiorColor)
        assertEquals(2.0, square.cloudyIntensity)
        assertEquals(15.0, square.rotation)
        val circle = created(fullCircle)
        assertEquals(AnnotationColor("#0000ff"), circle.circle?.color)
        assertNull(circle.square)

        val line = checkNotNull(created(fullLine).line)
        assertEquals(Point(10.0, 10.0), rounded(line.start))
        assertEquals(Point(100.0, 50.0), rounded(line.end))
        assertEquals(AnnotationLineEndings(AnnotationLineEnding.OPEN_ARROW, AnnotationLineEnding.REVERSED_CLOSED_ARROW), line.lineEndings)
        assertEquals(AnnotationIntent.LINE_ARROW, line.intent)
        assertEquals(AnnotationCaptionPosition.TOP, line.captionPosition)
        assertEquals(AnnotationCaptionOffset(1.0, 2.0), line.captionOffset)
        assertEquals(AnnotationLineLeader(5.0, 1.0, 2.0), line.leader)

        val polygon = checkNotNull(created(fullPolygon).polygon)
        assertEquals(3, polygon.vertices.size)
        assertEquals(AnnotationIntent.POLYGON_CLOUD, polygon.intent)
        assertEquals(Point(50.0, 30.0), polygon.captionCenter)
        assertEquals(AnnotationLineEndings(AnnotationLineEnding.CIRCLE, AnnotationLineEnding.DIAMOND), created(fullPolyline).polyline?.lineEndings)

        val freeTextInfo = created(fullFreeText)
        assertEquals("Free text", freeTextInfo.contents)
        val freeText = checkNotNull(freeTextInfo.freeText)
        assertEquals(box, rounded(freeText.box))
        assertEquals(AnnotationIntent.FREE_TEXT_CALLOUT, freeText.intent)
        assertEquals("times-roman", freeText.fontFamily)
        assertEquals(AnnotationTextAlignment.CENTER, freeText.textAlign)
        assertEquals(AnnotationVerticalAlignment.MIDDLE, freeText.verticalAlign)
        assertEquals(AnnotationColor("#112233"), freeText.fontColor)
        assertEquals(AnnotationLineEnding.OPEN_ARROW, freeText.lineEnding)
        assertEquals(listOf(Point(20.0, 20.0), Point(60.0, 60.0), Point(100.0, 130.0)), freeText.calloutLine?.points?.map(::rounded))

        assertEquals(AnnotationIntent.REPLACE, created(fullCaret).caret?.intent)
        assertEquals(AnnotationLinkTarget.Uri("https://example.test/typed"), created(fullLink).link?.target)
        viewer.destroy()
    }

    @Test
    fun aLinkReadsAPageTargetBack() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val target = AnnotationLinkTarget.Goto(PageDestinationFit(page))
        val ref = annotations.create(LinkAnnotationDraft(box, target), page)
        assertEquals(target, annotations.get(ref)?.link?.target)
        viewer.destroy()
    }

    @Test
    fun everyFixtureKindReadsTyped() = viewerTest {
        val (viewer, _, annotations) = openedPage("annotation_kinds")
        val bySubtype = LinkedHashMap<AnnotationSubtype, AnnotationInfo>()
        for (info in annotations.list()) bySubtype.putIfAbsent(info.subtype, info)
        for ((subtype, info) in bySubtype) {
            if (subtype == AnnotationSubtype.POPUP || subtype == AnnotationSubtype.WIDGET) continue
            assertNotEquals("$subtype read untyped: ${info.fieldsJson}", AnnotationInfo.Details.Untyped, info.details)
            assertNotEquals("$subtype has no blend mode", null, info.blendMode)
        }
        assertEquals(AnnotationBlendMode.MULTIPLY, bySubtype[AnnotationSubtype.HIGHLIGHT]?.blendMode)
        assertEquals(AnnotationInfo.Details.Untyped, bySubtype[AnnotationSubtype.POPUP]?.details)
        val line = checkNotNull(bySubtype[AnnotationSubtype.LINE]?.line)
        assertEquals(AnnotationLineEndings(AnnotationLineEnding.CIRCLE, AnnotationLineEnding.CLOSED_ARROW), line.lineEndings)
        assertEquals(false, line.captionEnabled)
        assertNull(bySubtype[AnnotationSubtype.POLYLINE]?.polyline?.color)
        assertEquals("Approved", bySubtype[AnnotationSubtype.STAMP]?.stamp?.name)
        assertEquals(AnnotationIntent.REPLACE, bySubtype[AnnotationSubtype.CARET]?.caret?.intent)
        assertEquals(AnnotationTextAlignment.RIGHT, bySubtype[AnnotationSubtype.FREE_TEXT]?.freeText?.textAlign)
        assertEquals(AnnotationLinkTarget.Uri("https://example.test/linked"), bySubtype[AnnotationSubtype.LINK]?.link?.target)
        val attachment = checkNotNull(bySubtype[AnnotationSubtype.FILE_ATTACHMENT]?.fileAttachment)
        assertEquals(AnnotationFileAttachmentIcon.PAPERCLIP, attachment.icon)
        assertEquals("notes.txt", attachment.file?.name)
        assertEquals("text/plain", attachment.file?.mimeType)
        assertEquals(15L, attachment.file?.sizeBytes)
        assertEquals(1_709_294_400_000L, attachment.file?.createdAt)
        val redaction = checkNotNull(bySubtype[AnnotationSubtype.REDACT]?.redaction)
        assertEquals("REDACTED", redaction.overlayText)
        assertEquals(AnnotationTextAlignment.CENTER, redaction.textAlign)
        viewer.destroy()
    }

    // ── rich text ──

    private val styledText = AnnotationRichText(
        paragraphs = listOf(
            AnnotationRichText.Paragraph(
                listOf(AnnotationRichText.Run("Plain and "), AnnotationRichText.Run("bold", mapOf("weight" to JsonPrimitive(700)))),
            ),
            AnnotationRichText.Paragraph(listOf(AnnotationRichText.Run("Second line"))),
        ),
    )

    /** The text of each paragraph's runs, joined. */
    private fun texts(richText: AnnotationRichText?): List<String>? = richText?.paragraphs?.map { paragraph -> paragraph.runs.joinToString("") { it.text } }

    @Test
    fun richTextEncodesItsOpenMapsFlat() {
        val richText = AnnotationRichText(
            body = mapOf(
                "size" to JsonPrimitive(12), "decoration" to kotlinx.serialization.json.JsonArray(listOf(JsonPrimitive("underline"))),
                "margins" to JsonNull,
            ),
            paragraphs = listOf(
                AnnotationRichText.Paragraph(
                    listOf(AnnotationRichText.Run("Hi", mapOf("italic" to JsonPrimitive(true)))),
                    mapOf("align" to JsonPrimitive("center"), "spacing" to kotlinx.serialization.json.JsonObject(mapOf("before" to JsonPrimitive(2)))),
                ),
            ),
        )
        val json =
            """{"body":{"decoration":["underline"],"margins":null,"size":12},"paragraphs":[{"align":"center","runs":[{"style":{"italic":true},"text":"Hi"}],"spacing":{"before":2}}]}"""
        assertEquals("""{"richText":$json}""", FreeTextAnnotationPatch(richText = richText).json())
        assertEquals(richText, AnnotationRichTextSerializer.read(jsonObject(json)))
        assertEquals("""{"richText":null}""", SquareAnnotationPatch(richText = Nullable.Clear).json())
        assertEquals(
            AnnotationRichText(
                paragraphs = listOf("One", "Two", "Three").map { AnnotationRichText.Paragraph(listOf(AnnotationRichText.Run(it))) },
            ),
            AnnotationRichText.plain("One\r\nTwo\nThree"),
        )
        assertEquals("Plain and bold\nSecond line", styledText.plainText)
    }

    @Test
    fun aFreeTextReadsItsRichTextBack() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val ref = annotations.create(
            FreeTextAnnotationDraft(box, contents = value("Plain and bold\nSecond line"), fontSize = 14.0, richText = styledText),
            page,
        )
        val read = checkNotNull(annotations.get(ref))
        assertEquals("Plain and bold\nSecond line", read.contents)
        val richText = checkNotNull(read.freeText?.richText)
        assertEquals(styledText.paragraphs.map { it.runs }, richText.paragraphs.map { it.runs })
        // The body is the draft's text style, stated whole. The engine spells
        // the size `14.0`, and a JsonElement compares by its text.
        assertEquals(14.0, richText.body?.get("size")?.jsonPrimitive?.content?.toDouble())

        // What a read hands out, a patch takes back with one run changed, and
        // the body and the other runs are not lost.
        val edited = richText.copy(
            paragraphs = richText.paragraphs.toMutableList().also { paragraphs ->
                val second = paragraphs[1]
                paragraphs[1] = second.copy(runs = listOf(second.runs[0].copy(text = "Another line")) + second.runs.drop(1))
            },
        )
        annotations.update(ref, FreeTextAnnotationPatch(richText = edited))
        val again = checkNotNull(annotations.get(ref))
        val patched = checkNotNull(again.freeText?.richText)
        assertEquals(listOf("Another line"), patched.paragraphs[1].runs.map { it.text })
        assertEquals(richText.body, patched.body)
        assertEquals(richText.paragraphs[0], patched.paragraphs[0])
        assertEquals(edited, patched)
        assertEquals("Plain and bold\nAnother line", again.contents)

        // `contents` that isn't the rich text's plain text is refused, on a patch and on a draft.
        expectCode(PdfException.Code.INVALID_INPUT) {
            annotations.update(ref, FreeTextAnnotationPatch(contents = value("Something else"), richText = AnnotationRichText.plain("New text")))
        }
        expectCode(PdfException.Code.INVALID_INPUT) {
            annotations.create(FreeTextAnnotationDraft(box, contents = value("Something else"), richText = styledText), page)
        }
        viewer.destroy()
    }

    /** The engine keeps a free text's text as rich text, so plain `contents` reads back as one unstyled run per line. */
    @Test
    fun aPlainFreeTextReadsRichText() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val read = checkNotNull(annotations.get(annotations.create(FreeTextAnnotationDraft(box, contents = value("One\nTwo")), page)))
        assertEquals(AnnotationRichText.plain("One\nTwo").paragraphs, read.freeText?.richText?.paragraphs)
        viewer.destroy()
    }

    @Test
    fun aCommentsRichTextBringsItsContents() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val ref = annotations.create(SquareAnnotationDraft(box, richText = value(styledText)), page)
        val rich = checkNotNull(annotations.get(ref))
        assertEquals("Plain and bold\nSecond line", rich.contents)
        assertEquals(listOf("Plain and bold", "Second line"), texts(rich.square?.richText))

        // New plain contents on a rich comment become its paragraphs.
        annotations.update(ref, SquareAnnotationPatch(contents = value("Changed")))
        assertEquals(listOf("Changed"), texts(annotations.get(ref)?.square?.richText))

        // Clearing makes the comment plain and keeps `contents`.
        annotations.update(ref, SquareAnnotationPatch(richText = Nullable.Clear))
        val plain = checkNotNull(annotations.get(ref))
        assertNull(plain.square?.richText)
        assertEquals("Changed", plain.contents)

        // A comment that was never rich reads none.
        assertNull(annotations.get(annotations.create(fullSquare, page))?.square?.richText)
        viewer.destroy()
    }

    /** Every kind that declares `richText` writes it and reads it back. */
    @Test
    fun everyKindWritesItsRichText() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val comment = AnnotationRichText.plain("A comment")
        val c = value(comment)
        val cases: List<Triple<AnnotationDraft, AnnotationPatch, (AnnotationInfo) -> AnnotationRichText?>> = listOf(
            Triple(AnnotationDraftTest.fullInk, InkAnnotationPatch(richText = c)) { it.ink?.richText },
            Triple(AnnotationDraftTest.fullHighlight, HighlightAnnotationPatch(richText = c)) { it.highlight?.richText },
            Triple(AnnotationDraftTest.fullNote, NoteAnnotationPatch(richText = c)) { it.note?.richText },
            Triple(fullUnderline, UnderlineAnnotationPatch(richText = c)) { it.underline?.richText },
            Triple(fullSquiggly, SquigglyAnnotationPatch(richText = c)) { it.squiggly?.richText },
            Triple(fullStrikeout, StrikeoutAnnotationPatch(richText = c)) { it.strikeout?.richText },
            Triple(fullRedaction, RedactionAnnotationPatch(richText = c)) { it.redaction?.richText },
            Triple(fullSquare, SquareAnnotationPatch(richText = c)) { it.square?.richText },
            Triple(fullCircle, CircleAnnotationPatch(richText = c)) { it.circle?.richText },
            Triple(fullLine, LineAnnotationPatch(richText = c)) { it.line?.richText },
            Triple(fullPolygon, PolygonAnnotationPatch(richText = c)) { it.polygon?.richText },
            Triple(fullPolyline, PolylineAnnotationPatch(richText = c)) { it.polyline?.richText },
            Triple(fullFreeText, FreeTextAnnotationPatch(richText = comment)) { it.freeText?.richText },
            Triple(fullCaret, CaretAnnotationPatch(richText = c)) { it.caret?.richText },
        )
        for ((draft, patch, richText) in cases) {
            val ref = annotations.create(draft, page)
            annotations.update(ref, patch)
            val read = checkNotNull(annotations.get(ref))
            assertEquals(patch.javaClass.simpleName, listOf("A comment"), texts(richText(read)))
            assertEquals(patch.javaClass.simpleName, "A comment", read.contents)
        }

        val vertices = listOf(Point(10.0, 10.0), Point(100.0, 10.0), Point(50.0, 80.0))
        val drafts: List<Pair<AnnotationDraft, (AnnotationInfo) -> AnnotationRichText?>> = listOf(
            InkAnnotationDraft(listOf(AnnotationDraftTest.stroke), richText = c) to { it.ink?.richText },
            HighlightAnnotationDraft(listOf(quad), richText = c) to { it.highlight?.richText },
            NoteAnnotationDraft(AnnotationDraftTest.noteRect, richText = c) to { it.note?.richText },
            UnderlineAnnotationDraft(listOf(quad), richText = c) to { it.underline?.richText },
            SquigglyAnnotationDraft(listOf(quad), richText = c) to { it.squiggly?.richText },
            StrikeoutAnnotationDraft(listOf(quad), richText = c) to { it.strikeout?.richText },
            RedactionAnnotationDraft(listOf(quad), richText = c) to { it.redaction?.richText },
            CircleAnnotationDraft(box, richText = c) to { it.circle?.richText },
            LineAnnotationDraft(Point(10.0, 10.0), Point(100.0, 50.0), richText = c) to { it.line?.richText },
            PolygonAnnotationDraft(vertices, richText = c) to { it.polygon?.richText },
            PolylineAnnotationDraft(vertices.take(2), richText = c) to { it.polyline?.richText },
            CaretAnnotationDraft(Rect(72.0, 300.0, 12.0, 12.0), richText = c) to { it.caret?.richText },
        )
        for ((draft, richText) in drafts) {
            val read = checkNotNull(annotations.get(annotations.create(draft, page)))
            assertEquals(draft.javaClass.simpleName, listOf("A comment"), texts(richText(read)))
            assertEquals(draft.javaClass.simpleName, "A comment", read.contents)
        }
        viewer.destroy()
    }

    @Test
    fun stampAndFileAttachmentPatchesWriteRichText() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val comment = value(AnnotationRichText.plain("A comment"))
        val stamp = annotations.drafts.placeStamp(
            AnnotationStampSource(AnnotationTest.tinyPng(), mimeType = "image/png"),
            AnnotationStampPlacement(page, Point(200.0, 200.0)),
            targetWidth = 80.0,
        )
        annotations.update(stamp, StampAnnotationPatch(richText = comment))
        assertEquals(listOf("A comment"), texts(annotations.get(stamp)?.stamp?.richText))

        val (kindsViewer, _, kinds) = openedPage("annotation_kinds")
        val attachment = kinds.list().first { it.subtype == AnnotationSubtype.FILE_ATTACHMENT }
        kinds.update(attachment.annotationRef, FileAttachmentAnnotationPatch(richText = comment))
        assertEquals(listOf("A comment"), texts(kinds.get(attachment.annotationRef)?.fileAttachment?.richText))
        kindsViewer.destroy()
        viewer.destroy()
    }

    // ── measurement scales ──

    @Test
    fun aDimensionLineReadsItsMeasureBack() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val ref = annotations.create(
            LineAnnotationDraft(Point(72.0, 72.0), Point(216.0, 72.0), intent = value(AnnotationLineIntent.LINE_DIMENSION), measure = value(scale)),
            page,
        )
        val read = checkNotNull(annotations.get(ref))
        assertEquals(AnnotationIntent.LINE_DIMENSION, read.line?.intent)
        assertEquals(scale, read.line?.measure)
        // The engine works the label out from the points and the scale: 144
        // points at 0.125 ft a point.
        assertEquals("18 ft", read.contents)

        val metres = scale.copy(ratio = "1 in = 1 m")
        annotations.update(ref, LineAnnotationPatch(measure = value(metres)))
        assertEquals(metres, annotations.get(ref)?.line?.measure)

        annotations.update(ref, LineAnnotationPatch(measure = Nullable.Clear))
        val cleared = checkNotNull(annotations.get(ref))
        assertNull(cleared.line?.measure)
        assertNotEquals(AnnotationInfo.Details.Untyped, cleared.details)
        viewer.destroy()
    }

    @Test
    fun polygonsAndPolylinesReadTheirMeasureBack() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val vertices = listOf(Point(72.0, 72.0), Point(216.0, 72.0), Point(216.0, 216.0))
        val polygon = annotations.get(
            annotations.create(PolygonAnnotationDraft(vertices, intent = value(AnnotationPolygonIntent.POLYGON_DIMENSION), measure = value(scale)), page),
        )
        assertEquals(scale, polygon?.polygon?.measure)
        val polyline = annotations.get(
            annotations.create(PolylineAnnotationDraft(vertices, intent = value(AnnotationPolylineIntent.POLYLINE_DIMENSION), measure = value(scale)), page),
        )
        assertEquals(scale, polyline?.polyline?.measure)
        viewer.destroy()
    }

    /** A scale the engine can't model reads as its subtype alone, and a draft can't write one. */
    @Test
    fun aForeignMeasureReadsAsItsSubtypeAndIsRefusedOnADraft() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        val line = checkNotNull(annotations.get(annotations.create(LineAnnotationDraft(Point(72.0, 72.0), Point(216.0, 72.0)), page)))
        val fields = kotlinx.serialization.json.JsonObject(
            jsonObject(line.fieldsJson) + ("measure" to kotlinx.serialization.json.JsonObject(mapOf("subtype" to JsonPrimitive("geospatial")))),
        )
        val record = line.record.copy(fieldsJson = fields.toString())
        assertEquals(PdfMeasurement("geospatial"), AnnotationInfo(record).line?.measure)

        expectCode(PdfException.Code.INVALID_INPUT) {
            annotations.create(LineAnnotationDraft(Point(72.0, 72.0), Point(216.0, 72.0), measure = value(PdfMeasurement("geospatial"))), page)
        }
        viewer.destroy()
    }

    // ── tool defaults ──

    @Test
    fun typedDefaultsReachTheTool() = viewerTest {
        val (viewer, _, annotations) = openedPage()
        val other = AnnotationColor("#123456")
        val cases: List<Pair<ToolId, AnnotationPatch>> = listOf(
            ToolId.UNDERLINE to UnderlineAnnotationPatch(color = other, opacity = 0.5, blendMode = AnnotationBlendMode.MULTIPLY),
            ToolId.SQUIGGLY to SquigglyAnnotationPatch(color = other, blendMode = AnnotationBlendMode.DARKEN),
            ToolId.STRIKEOUT to StrikeoutAnnotationPatch(color = other, blendMode = AnnotationBlendMode.SCREEN),
            ToolId.SQUARE to SquareAnnotationPatch(
                color = Nullable.Clear, strokeWidth = 2.0, interiorColor = value(AnnotationColor("#00ff00")), blendMode = AnnotationBlendMode.MULTIPLY,
            ),
            ToolId.CIRCLE to CircleAnnotationPatch(borderStyle = AnnotationBorderStyle.DASHED, dashArray = value(listOf(2.0, 2.0)), cloudyIntensity = value(1.0)),
            ToolId.LINE to LineAnnotationPatch(
                lineEndings = AnnotationLineEndings(AnnotationLineEnding.NONE, AnnotationLineEnding.OPEN_ARROW), captionEnabled = true,
            ),
            ToolId.POLYGON to PolygonAnnotationPatch(interiorColor = value(AnnotationColor("#ffff00")), captionCenter = AnnotationCaptionCenter.Automatic),
            ToolId.POLYLINE to PolylineAnnotationPatch(lineEndings = AnnotationLineEndings(AnnotationLineEnding.CIRCLE, AnnotationLineEnding.CIRCLE)),
            ToolId.FREE_TEXT to FreeTextAnnotationPatch(
                fontFamily = "courier", fontSize = 18.0, fontColor = AnnotationColor("#ff0000"), textAlign = AnnotationTextAlignment.CENTER,
                verticalAlign = AnnotationVerticalAlignment.MIDDLE, interiorColor = value(AnnotationColor("#ffffcc")), strokeWidth = 0.0,
            ),
            ToolId.REDACT to RedactionAnnotationPatch(overlayText = value("X"), repeat = true, textAlign = AnnotationTextAlignment.RIGHT),
        )
        for ((tool, patch) in cases) {
            annotations.tools.updateDefaults(patch, tool)
            expectWritten(patch.json(), annotations.tools.defaults(tool), "$tool defaults")
        }
        viewer.destroy()
    }

    @Test
    fun aTypedDefaultReachesTheToolsNextAnnotation() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        annotations.tools.updateDefaults(
            SquareAnnotationPatch(color = value(AnnotationColor("#00ff00")), strokeWidth = 2.0, blendMode = AnnotationBlendMode.MULTIPLY),
            ToolId.SQUARE,
        )
        val square = checkNotNull(annotations.get(annotations.create(SquareAnnotationDraft(box), page, ToolId.SQUARE)))
        assertEquals(AnnotationColor("#00ff00"), square.square?.color)
        assertEquals(2.0, square.square?.strokeWidth)
        assertEquals(AnnotationBlendMode.MULTIPLY, square.blendMode)
        viewer.destroy()
    }

    @Test
    fun theStringOverloadsTakeTheTypedJsonAlike() = viewerTest {
        val (viewer, page, annotations) = openedPage()
        for (draft in fullDrafts) {
            val typed = checkNotNull(annotations.get(annotations.create(draft, page)))
            val untyped = checkNotNull(annotations.get(annotations.create(draft.json(), page)))
            assertEquals(draft.javaClass.simpleName, typed.details, untyped.details)
            assertEquals(typed.blendMode, untyped.blendMode)
            assertEquals(typed.contents, untyped.contents)
        }
        viewer.destroy()
    }
}
