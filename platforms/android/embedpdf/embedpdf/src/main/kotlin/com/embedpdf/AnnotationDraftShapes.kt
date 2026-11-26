package com.embedpdf

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive

// Typed drafts and patches for the shapes: square, circle, line, polygon and
// polyline. The rules are in `AnnotationDraft.kt`; each member's own rule is
// the engine's (`read_field` in
// `crates/epdf-engine/src/features/annotation/write/prepare.rs`).
//
// A stroked shape's `color` takes `Nullable.Clear`, which draws no stroke.

/** The `/LE` pair a line or polyline draws at its two ends. */
public data class AnnotationLineEndings(val start: AnnotationLineEnding, val end: AnnotationLineEnding)

/** A line's `/IT`. A line without one is a plain line. */
@Serializable
public enum class AnnotationLineIntent(public val value: String) {
    @SerialName("line-arrow") LINE_ARROW("line-arrow"),

    /** `line-dimension`: a distance measurement. Its scale is `measure`. */
    @SerialName("line-dimension") LINE_DIMENSION("line-dimension"),
}

/** A polygon's `/IT`. A polygon without one is a plain polygon. */
@Serializable
public enum class AnnotationPolygonIntent(public val value: String) {
    @SerialName("polygon-cloud") POLYGON_CLOUD("polygon-cloud"),

    /** `polygon-dimension`: an area measurement. Its scale is `measure`. */
    @SerialName("polygon-dimension") POLYGON_DIMENSION("polygon-dimension"),
}

/** A polyline's `/IT`. A polyline without one is a plain polyline. */
@Serializable
public enum class AnnotationPolylineIntent(public val value: String) {
    /** `polyline-dimension`: a perimeter measurement. Its scale is `measure`. */
    @SerialName("polyline-dimension") POLYLINE_DIMENSION("polyline-dimension"),
}

/** A line's `/CP`: where its caption sits. */
@Serializable
public enum class AnnotationCaptionPosition(public val value: String) {
    /** In the line, which breaks around it. */
    @SerialName("inline") INLINE("inline"),

    /** Above the line. */
    @SerialName("top") TOP("top"),
}

/**
 * A line caption's displacement (`/CO`) in the line's own axes, in PDF units:
 * along the line from its midpoint, and away from it.
 */
public data class AnnotationCaptionOffset(val along: Double, val perpendicular: Double)

/**
 * A line's leader lines: `/LL`, `/LLE` and `/LLO`.
 *
 * @property length `/LL`: how far the leader lines reach from the line's
 *   ends, along the line's normal. Negative reaches the other way.
 * @property extension `/LLE`: how far the leader lines run past the line, 0
 *   or more.
 * @property offset `/LLO`: the gap between the points and the leader lines, 0
 *   or more.
 */
public data class AnnotationLineLeader(val length: Double, val extension: Double? = null, val offset: Double? = null)

/** Where a polygon's or polyline's caption sits. */
public sealed interface AnnotationCaptionCenter {
    /** Placed by the engine. Writes `null`. */
    public data object Automatic : AnnotationCaptionCenter

    /** Centred on this page-space point. */
    public data class At(val point: Point) : AnnotationCaptionCenter
}

// ── square and circle ───────────────────────────────────────────────────────

/**
 * A new square: a rectangle, `/Square`.
 *
 * @property box the rectangle, page space, before rotation. The engine works
 *   out `/Rect` from it, with room for the stroke.
 * @property color `/C`, the stroke. [Nullable.Clear] draws none.
 * @property opacity `/CA`, 0 to 1.
 * @property strokeWidth the line width, 0 or more.
 * @property dashArray the dash lengths, each 0 or more, at least one.
 *   [Nullable.Clear] is none.
 * @property interiorColor `/IC`, the fill. [Nullable.Clear] is none.
 * @property rotation degrees. [Nullable.Clear] is none.
 * @property cloudyIntensity `/BE`: a cloudy border's intensity, above 0.
 *   [Nullable.Clear] is a plain border.
 * @property contents `/Contents`. [Nullable.Clear] is none.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a draft that gives both needs them to agree.
 *   [Nullable.Clear] is a plain comment.
 * @property common the fields every annotation shares.
 */
public data class SquareAnnotationDraft(
    val box: Rect,
    val color: Nullable<AnnotationColor> = Nullable.Unchanged,
    val opacity: Double? = null,
    val strokeWidth: Double? = null,
    val borderStyle: AnnotationBorderStyle? = null,
    val dashArray: Nullable<List<Double>> = Nullable.Unchanged,
    val interiorColor: Nullable<AnnotationColor> = Nullable.Unchanged,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val cloudyIntensity: Nullable<Double> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationDraft {
    override fun json(): String = FieldBagWriter().run {
        put("subtype", AnnotationSubtype.SQUARE.value)
        shapeFields(box, color, opacity, strokeWidth, borderStyle, dashArray, interiorColor, rotation, cloudyIntensity, contents, blendMode)
        richText(richText)
        draftCommon(common)
    }
}

/**
 * A change to a square, or to a square tool's defaults. A member left out
 * keeps its value.
 *
 * @property box moves or resizes the rectangle.
 * @property color [Nullable.Clear] removes the stroke.
 * @property dashArray [Nullable.Clear] removes the pattern.
 * @property interiorColor [Nullable.Clear] removes the fill.
 * @property rotation [Nullable.Clear] removes the rotation.
 * @property cloudyIntensity [Nullable.Clear] makes the border plain.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree.
 *   [Nullable.Clear] makes the comment plain and keeps `contents`.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class SquareAnnotationPatch(
    val box: Rect? = null,
    val color: Nullable<AnnotationColor> = Nullable.Unchanged,
    val opacity: Double? = null,
    val strokeWidth: Double? = null,
    val borderStyle: AnnotationBorderStyle? = null,
    val dashArray: Nullable<List<Double>> = Nullable.Unchanged,
    val interiorColor: Nullable<AnnotationColor> = Nullable.Unchanged,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val cloudyIntensity: Nullable<Double> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    override fun json(): String = FieldBagWriter().run {
        shapeFields(box, color, opacity, strokeWidth, borderStyle, dashArray, interiorColor, rotation, cloudyIntensity, contents, blendMode)
        richText(richText)
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<SquareAnnotationPatch> {
        override fun fromJson(json: String): SquareAnnotationPatch = readPatch(json) {
            val shape = readShape()
            SquareAnnotationPatch(
                shape.box, shape.color, shape.opacity, shape.strokeWidth, shape.borderStyle, shape.dashArray,
                shape.interiorColor, shape.rotation, shape.cloudyIntensity, shape.contents, shape.blendMode, richText(), common(),
            )
        }
    }
}

/**
 * A new circle: an ellipse in its box, `/Circle`.
 *
 * @property box the ellipse's box, page space, before rotation.
 * @property color `/C`, the stroke. [Nullable.Clear] draws none.
 * @property opacity `/CA`, 0 to 1.
 * @property strokeWidth the line width, 0 or more.
 * @property dashArray the dash lengths, each 0 or more, at least one.
 *   [Nullable.Clear] is none.
 * @property interiorColor `/IC`, the fill. [Nullable.Clear] is none.
 * @property rotation degrees. [Nullable.Clear] is none.
 * @property cloudyIntensity `/BE`: a cloudy border's intensity, above 0.
 *   [Nullable.Clear] is a plain border.
 * @property contents `/Contents`. [Nullable.Clear] is none.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a draft that gives both needs them to agree.
 *   [Nullable.Clear] is a plain comment.
 * @property common the fields every annotation shares.
 */
public data class CircleAnnotationDraft(
    val box: Rect,
    val color: Nullable<AnnotationColor> = Nullable.Unchanged,
    val opacity: Double? = null,
    val strokeWidth: Double? = null,
    val borderStyle: AnnotationBorderStyle? = null,
    val dashArray: Nullable<List<Double>> = Nullable.Unchanged,
    val interiorColor: Nullable<AnnotationColor> = Nullable.Unchanged,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val cloudyIntensity: Nullable<Double> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationDraft {
    override fun json(): String = FieldBagWriter().run {
        put("subtype", AnnotationSubtype.CIRCLE.value)
        shapeFields(box, color, opacity, strokeWidth, borderStyle, dashArray, interiorColor, rotation, cloudyIntensity, contents, blendMode)
        richText(richText)
        draftCommon(common)
    }
}

/**
 * A change to a circle, or to a circle tool's defaults. A member left out
 * keeps its value.
 *
 * @property box moves or resizes the ellipse's box.
 * @property color [Nullable.Clear] removes the stroke.
 * @property dashArray [Nullable.Clear] removes the pattern.
 * @property interiorColor [Nullable.Clear] removes the fill.
 * @property rotation [Nullable.Clear] removes the rotation.
 * @property cloudyIntensity [Nullable.Clear] makes the border plain.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree.
 *   [Nullable.Clear] makes the comment plain and keeps `contents`.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class CircleAnnotationPatch(
    val box: Rect? = null,
    val color: Nullable<AnnotationColor> = Nullable.Unchanged,
    val opacity: Double? = null,
    val strokeWidth: Double? = null,
    val borderStyle: AnnotationBorderStyle? = null,
    val dashArray: Nullable<List<Double>> = Nullable.Unchanged,
    val interiorColor: Nullable<AnnotationColor> = Nullable.Unchanged,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val cloudyIntensity: Nullable<Double> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    override fun json(): String = FieldBagWriter().run {
        shapeFields(box, color, opacity, strokeWidth, borderStyle, dashArray, interiorColor, rotation, cloudyIntensity, contents, blendMode)
        richText(richText)
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<CircleAnnotationPatch> {
        override fun fromJson(json: String): CircleAnnotationPatch = readPatch(json) {
            val shape = readShape()
            CircleAnnotationPatch(
                shape.box, shape.color, shape.opacity, shape.strokeWidth, shape.borderStyle, shape.dashArray,
                shape.interiorColor, shape.rotation, shape.cloudyIntensity, shape.contents, shape.blendMode, richText(), common(),
            )
        }
    }
}

private fun FieldBagWriter.shapeFields(
    box: Rect?,
    color: Nullable<AnnotationColor>,
    opacity: Double?,
    strokeWidth: Double?,
    borderStyle: AnnotationBorderStyle?,
    dashArray: Nullable<List<Double>>,
    interiorColor: Nullable<AnnotationColor>,
    rotation: Nullable<Double>,
    cloudyIntensity: Nullable<Double>,
    contents: Nullable<String>,
    blendMode: AnnotationBlendMode?,
) {
    put("box", box?.let(FieldValues::rect))
    stroke(color, opacity, strokeWidth, borderStyle, dashArray, interiorColor)
    put("rotation", rotation, ::JsonPrimitive)
    put("cloudyIntensity", cloudyIntensity, ::JsonPrimitive)
    put("contents", contents, ::JsonPrimitive)
    put("blendMode", blendMode?.value)
}

/** The stroke and fill members every shape has. */
private fun FieldBagWriter.stroke(
    color: Nullable<AnnotationColor>,
    opacity: Double?,
    strokeWidth: Double?,
    borderStyle: AnnotationBorderStyle?,
    dashArray: Nullable<List<Double>>,
    interiorColor: Nullable<AnnotationColor>,
) {
    put("color", color, FieldValues::color)
    put("opacity", opacity)
    put("strokeWidth", strokeWidth)
    put("borderStyle", borderStyle?.value)
    put("dashArray", dashArray, FieldValues::doubles)
    put("interiorColor", interiorColor, FieldValues::color)
}

private class ShapeMembers(
    val box: Rect?,
    val color: Nullable<AnnotationColor>,
    val opacity: Double?,
    val strokeWidth: Double?,
    val borderStyle: AnnotationBorderStyle?,
    val dashArray: Nullable<List<Double>>,
    val interiorColor: Nullable<AnnotationColor>,
    val rotation: Nullable<Double>,
    val cloudyIntensity: Nullable<Double>,
    val contents: Nullable<String>,
    val blendMode: AnnotationBlendMode?,
)

private fun FieldBagReader.readShape(): ShapeMembers = ShapeMembers(
    box = optional("box", FieldValues::rect),
    color = nullable("color", FieldValues::color),
    opacity = optional("opacity", FieldValues::double),
    strokeWidth = optional("strokeWidth", FieldValues::double),
    borderStyle = optional("borderStyle", FieldValues::borderStyle),
    dashArray = nullable("dashArray", FieldValues::doubles),
    interiorColor = nullable("interiorColor", FieldValues::color),
    rotation = nullable("rotation", FieldValues::double),
    cloudyIntensity = nullable("cloudyIntensity", FieldValues::double),
    contents = nullable("contents", FieldValues::string),
    blendMode = blendMode(),
)

// ── line ────────────────────────────────────────────────────────────────────

/**
 * A new line between two points.
 *
 * @property start `/L`, page space.
 * @property color `/C`, the stroke. [Nullable.Clear] draws none.
 * @property opacity `/CA`, 0 to 1.
 * @property strokeWidth the line width, 0 or more.
 * @property dashArray the dash lengths, each 0 or more, at least one.
 *   [Nullable.Clear] is none.
 * @property interiorColor `/IC`: what fills a closed line ending.
 *   [Nullable.Clear] is none.
 * @property rotation degrees. [Nullable.Clear] is none.
 * @property intent [Nullable.Clear] is a plain line.
 * @property captionEnabled `/Cap`: whether `contents` shows as a caption on
 *   the line.
 * @property captionOffset `/CO`. [Nullable.Clear] is none.
 * @property leader `/LL`, `/LLE` and `/LLO`. [Nullable.Clear] is none.
 * @property contents `/Contents`, and the caption when [captionEnabled].
 *   [Nullable.Clear] is none.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a draft that gives both needs them to agree.
 *   [Nullable.Clear] is a plain comment.
 * @property measure `/Measure`: the scale a dimension intent measures in.
 *   With one, the engine sets `contents` to the measured value's label. Only
 *   a `rectilinear` scale that has its `x`, `distance` and `area` formats is
 *   written; any other throws `invalid-input`. [Nullable.Clear] is none.
 * @property common the fields every annotation shares.
 */
public data class LineAnnotationDraft(
    val start: Point,
    val end: Point,
    val color: Nullable<AnnotationColor> = Nullable.Unchanged,
    val opacity: Double? = null,
    val strokeWidth: Double? = null,
    val borderStyle: AnnotationBorderStyle? = null,
    val dashArray: Nullable<List<Double>> = Nullable.Unchanged,
    val interiorColor: Nullable<AnnotationColor> = Nullable.Unchanged,
    val lineEndings: AnnotationLineEndings? = null,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val intent: Nullable<AnnotationLineIntent> = Nullable.Unchanged,
    val captionEnabled: Boolean? = null,
    val captionPosition: AnnotationCaptionPosition? = null,
    val captionOffset: Nullable<AnnotationCaptionOffset> = Nullable.Unchanged,
    val leader: Nullable<AnnotationLineLeader> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val measure: Nullable<PdfMeasurement> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationDraft {
    override fun json(): String = FieldBagWriter().run {
        put("subtype", AnnotationSubtype.LINE.value)
        put("linePoints", FieldValues.linePoints(start, end))
        lineFields(
            color, opacity, strokeWidth, borderStyle, dashArray, interiorColor, lineEndings, rotation, intent,
            captionEnabled, captionPosition, captionOffset, leader, contents, blendMode,
        )
        richText(richText)
        measure(measure)
        draftCommon(common)
    }
}

/**
 * A change to a line, or to a line tool's defaults. A member left out keeps
 * its value.
 *
 * @property start moves the line. Set both [start] and [end] or neither:
 *   `/L` is written whole, and [json] throws `invalid-input` for one alone.
 * @property color [Nullable.Clear] removes the stroke.
 * @property dashArray [Nullable.Clear] removes the pattern.
 * @property interiorColor [Nullable.Clear] removes the fill.
 * @property rotation [Nullable.Clear] removes the rotation.
 * @property intent [Nullable.Clear] makes it a plain line.
 * @property captionOffset [Nullable.Clear] removes the offset.
 * @property leader [Nullable.Clear] removes the leader lines.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree.
 *   [Nullable.Clear] makes the comment plain and keeps `contents`.
 * @property measure replaces the scale, on the draft's rules.
 *   [Nullable.Clear] removes it. A scale read with another subtype
 *   (`geospatial`, `unknown`) may be sent back as it was read, which keeps it.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class LineAnnotationPatch(
    val start: Point? = null,
    val end: Point? = null,
    val color: Nullable<AnnotationColor> = Nullable.Unchanged,
    val opacity: Double? = null,
    val strokeWidth: Double? = null,
    val borderStyle: AnnotationBorderStyle? = null,
    val dashArray: Nullable<List<Double>> = Nullable.Unchanged,
    val interiorColor: Nullable<AnnotationColor> = Nullable.Unchanged,
    val lineEndings: AnnotationLineEndings? = null,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val intent: Nullable<AnnotationLineIntent> = Nullable.Unchanged,
    val captionEnabled: Boolean? = null,
    val captionPosition: AnnotationCaptionPosition? = null,
    val captionOffset: Nullable<AnnotationCaptionOffset> = Nullable.Unchanged,
    val leader: Nullable<AnnotationLineLeader> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val measure: Nullable<PdfMeasurement> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    /** Throws `invalid-input` when only one of [start] and [end] is set. */
    override fun json(): String = FieldBagWriter().run {
        when {
            start != null && end != null -> put("linePoints", FieldValues.linePoints(start, end))
            start != null || end != null -> throw PdfException(
                PdfException.Code.INVALID_INPUT,
                "the annotation fields don't encode as JSON: a line patch sets both start and end, or neither",
                "EncodingError",
            )
        }
        lineFields(
            color, opacity, strokeWidth, borderStyle, dashArray, interiorColor, lineEndings, rotation, intent,
            captionEnabled, captionPosition, captionOffset, leader, contents, blendMode,
        )
        richText(richText)
        measure(measure)
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<LineAnnotationPatch> {
        /** `linePoints` reads into [start] and [end]. */
        override fun fromJson(json: String): LineAnnotationPatch = readPatch(json) {
            val linePoints = optional("linePoints") { it.asObject("line points") }
            LineAnnotationPatch(
                start = linePoints?.let { FieldValues.point(it.field("start")) },
                end = linePoints?.let { FieldValues.point(it.field("end")) },
                color = nullable("color", FieldValues::color),
                opacity = optional("opacity", FieldValues::double),
                strokeWidth = optional("strokeWidth", FieldValues::double),
                borderStyle = optional("borderStyle", FieldValues::borderStyle),
                dashArray = nullable("dashArray", FieldValues::doubles),
                interiorColor = nullable("interiorColor", FieldValues::color),
                lineEndings = optional("lineEndings", FieldValues::lineEndings),
                rotation = nullable("rotation", FieldValues::double),
                intent = nullable("intent") { FieldValues.enum(it, AnnotationLineIntent.entries) { intent -> intent.value } },
                captionEnabled = optional("captionEnabled", FieldValues::boolean),
                captionPosition = optional("captionPosition", FieldValues::captionPosition),
                captionOffset = nullable("captionOffset", FieldValues::captionOffset),
                leader = nullable("leader", FieldValues::leader),
                contents = nullable("contents", FieldValues::string),
                blendMode = blendMode(),
                richText = richText(),
                measure = measure(),
                common = common(),
            )
        }
    }
}

private fun FieldBagWriter.lineFields(
    color: Nullable<AnnotationColor>,
    opacity: Double?,
    strokeWidth: Double?,
    borderStyle: AnnotationBorderStyle?,
    dashArray: Nullable<List<Double>>,
    interiorColor: Nullable<AnnotationColor>,
    lineEndings: AnnotationLineEndings?,
    rotation: Nullable<Double>,
    intent: Nullable<AnnotationLineIntent>,
    captionEnabled: Boolean?,
    captionPosition: AnnotationCaptionPosition?,
    captionOffset: Nullable<AnnotationCaptionOffset>,
    leader: Nullable<AnnotationLineLeader>,
    contents: Nullable<String>,
    blendMode: AnnotationBlendMode?,
) {
    stroke(color, opacity, strokeWidth, borderStyle, dashArray, interiorColor)
    put("lineEndings", lineEndings?.let(FieldValues::lineEndings))
    put("rotation", rotation, ::JsonPrimitive)
    put("intent", intent) { JsonPrimitive(it.value) }
    put("captionEnabled", captionEnabled?.let(::JsonPrimitive))
    put("captionPosition", captionPosition?.value)
    put("captionOffset", captionOffset, FieldValues::captionOffset)
    put("leader", leader, FieldValues::leader)
    put("contents", contents, ::JsonPrimitive)
    put("blendMode", blendMode?.value)
}

// ── polygon and polyline ────────────────────────────────────────────────────

/**
 * A new polygon: a closed path through its vertices.
 *
 * @property vertices `/Vertices`, page space, at least three.
 * @property color `/C`, the stroke. [Nullable.Clear] draws none.
 * @property opacity `/CA`, 0 to 1.
 * @property strokeWidth the line width, 0 or more.
 * @property dashArray the dash lengths, each 0 or more, at least one.
 *   [Nullable.Clear] is none.
 * @property interiorColor `/IC`, the fill. [Nullable.Clear] is none.
 * @property rotation degrees. [Nullable.Clear] is none.
 * @property intent [Nullable.Clear] is a plain polygon.
 * @property cloudyIntensity `/BE`: a cloudy border's intensity, above 0.
 *   [Nullable.Clear] is a plain border.
 * @property captionEnabled whether `contents` shows as a caption.
 *   [Nullable.Clear] is the engine's choice.
 * @property contents `/Contents`, and the caption when [captionEnabled].
 *   [Nullable.Clear] is none.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a draft that gives both needs them to agree.
 *   [Nullable.Clear] is a plain comment.
 * @property measure `/Measure`: the scale a dimension intent measures in.
 *   With one, the engine sets `contents` to the measured value's label. Only
 *   a `rectilinear` scale that has its `x`, `distance` and `area` formats is
 *   written; any other throws `invalid-input`. [Nullable.Clear] is none.
 * @property common the fields every annotation shares.
 */
public data class PolygonAnnotationDraft(
    val vertices: List<Point>,
    val color: Nullable<AnnotationColor> = Nullable.Unchanged,
    val opacity: Double? = null,
    val strokeWidth: Double? = null,
    val borderStyle: AnnotationBorderStyle? = null,
    val dashArray: Nullable<List<Double>> = Nullable.Unchanged,
    val interiorColor: Nullable<AnnotationColor> = Nullable.Unchanged,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val intent: Nullable<AnnotationPolygonIntent> = Nullable.Unchanged,
    val cloudyIntensity: Nullable<Double> = Nullable.Unchanged,
    val captionEnabled: Nullable<Boolean> = Nullable.Unchanged,
    val captionCenter: AnnotationCaptionCenter? = null,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val measure: Nullable<PdfMeasurement> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationDraft {
    override fun json(): String = FieldBagWriter().run {
        put("subtype", AnnotationSubtype.POLYGON.value)
        pathFields(
            vertices, color, opacity, strokeWidth, borderStyle, dashArray, interiorColor, rotation, intent.wire { it.value },
            captionEnabled, captionCenter, contents, blendMode,
        )
        put("cloudyIntensity", cloudyIntensity, ::JsonPrimitive)
        richText(richText)
        measure(measure)
        draftCommon(common)
    }
}

/**
 * A change to a polygon, or to a polygon tool's defaults. A member left out
 * keeps its value.
 *
 * @property vertices replaces every vertex.
 * @property color [Nullable.Clear] removes the stroke.
 * @property dashArray [Nullable.Clear] removes the pattern.
 * @property interiorColor [Nullable.Clear] removes the fill.
 * @property rotation [Nullable.Clear] removes the rotation.
 * @property intent [Nullable.Clear] makes it a plain polygon.
 * @property cloudyIntensity [Nullable.Clear] makes the border plain.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree.
 *   [Nullable.Clear] makes the comment plain and keeps `contents`.
 * @property measure replaces the scale, on the draft's rules.
 *   [Nullable.Clear] removes it. A scale read with another subtype
 *   (`geospatial`, `unknown`) may be sent back as it was read, which keeps it.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class PolygonAnnotationPatch(
    val vertices: List<Point>? = null,
    val color: Nullable<AnnotationColor> = Nullable.Unchanged,
    val opacity: Double? = null,
    val strokeWidth: Double? = null,
    val borderStyle: AnnotationBorderStyle? = null,
    val dashArray: Nullable<List<Double>> = Nullable.Unchanged,
    val interiorColor: Nullable<AnnotationColor> = Nullable.Unchanged,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val intent: Nullable<AnnotationPolygonIntent> = Nullable.Unchanged,
    val cloudyIntensity: Nullable<Double> = Nullable.Unchanged,
    val captionEnabled: Nullable<Boolean> = Nullable.Unchanged,
    val captionCenter: AnnotationCaptionCenter? = null,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val measure: Nullable<PdfMeasurement> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    override fun json(): String = FieldBagWriter().run {
        pathFields(
            vertices, color, opacity, strokeWidth, borderStyle, dashArray, interiorColor, rotation, intent.wire { it.value },
            captionEnabled, captionCenter, contents, blendMode,
        )
        put("cloudyIntensity", cloudyIntensity, ::JsonPrimitive)
        richText(richText)
        measure(measure)
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<PolygonAnnotationPatch> {
        /**
         * Only a polygon declares `cloudyIntensity` and only a polyline
         * `lineEndings`, so this leaves `lineEndings` unread.
         */
        override fun fromJson(json: String): PolygonAnnotationPatch = readPatch(json) {
            val path = readPath()
            PolygonAnnotationPatch(
                vertices = path.vertices,
                color = path.color,
                opacity = path.opacity,
                strokeWidth = path.strokeWidth,
                borderStyle = path.borderStyle,
                dashArray = path.dashArray,
                interiorColor = path.interiorColor,
                rotation = path.rotation,
                intent = nullable("intent") { FieldValues.enum(it, AnnotationPolygonIntent.entries) { intent -> intent.value } },
                cloudyIntensity = nullable("cloudyIntensity", FieldValues::double),
                captionEnabled = path.captionEnabled,
                captionCenter = path.captionCenter,
                contents = path.contents,
                blendMode = path.blendMode,
                richText = richText(),
                measure = measure(),
                common = common(),
            )
        }
    }
}

/**
 * A new polyline: an open path through its vertices.
 *
 * @property vertices `/Vertices`, page space, at least two.
 * @property color `/C`, the stroke. [Nullable.Clear] draws none.
 * @property opacity `/CA`, 0 to 1.
 * @property strokeWidth the line width, 0 or more.
 * @property dashArray the dash lengths, each 0 or more, at least one.
 *   [Nullable.Clear] is none.
 * @property interiorColor `/IC`: what fills a closed line ending.
 *   [Nullable.Clear] is none.
 * @property rotation degrees. [Nullable.Clear] is none.
 * @property intent [Nullable.Clear] is a plain polyline.
 * @property captionEnabled whether `contents` shows as a caption.
 *   [Nullable.Clear] is the engine's choice.
 * @property contents `/Contents`, and the caption when [captionEnabled].
 *   [Nullable.Clear] is none.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a draft that gives both needs them to agree.
 *   [Nullable.Clear] is a plain comment.
 * @property measure `/Measure`: the scale a dimension intent measures in.
 *   With one, the engine sets `contents` to the measured value's label. Only
 *   a `rectilinear` scale that has its `x`, `distance` and `area` formats is
 *   written; any other throws `invalid-input`. [Nullable.Clear] is none.
 * @property common the fields every annotation shares.
 */
public data class PolylineAnnotationDraft(
    val vertices: List<Point>,
    val color: Nullable<AnnotationColor> = Nullable.Unchanged,
    val opacity: Double? = null,
    val strokeWidth: Double? = null,
    val borderStyle: AnnotationBorderStyle? = null,
    val dashArray: Nullable<List<Double>> = Nullable.Unchanged,
    val interiorColor: Nullable<AnnotationColor> = Nullable.Unchanged,
    val lineEndings: AnnotationLineEndings? = null,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val intent: Nullable<AnnotationPolylineIntent> = Nullable.Unchanged,
    val captionEnabled: Nullable<Boolean> = Nullable.Unchanged,
    val captionCenter: AnnotationCaptionCenter? = null,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val measure: Nullable<PdfMeasurement> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationDraft {
    override fun json(): String = FieldBagWriter().run {
        put("subtype", AnnotationSubtype.POLYLINE.value)
        pathFields(
            vertices, color, opacity, strokeWidth, borderStyle, dashArray, interiorColor, rotation, intent.wire { it.value },
            captionEnabled, captionCenter, contents, blendMode,
        )
        put("lineEndings", lineEndings?.let(FieldValues::lineEndings))
        richText(richText)
        measure(measure)
        draftCommon(common)
    }
}

/**
 * A change to a polyline, or to a polyline tool's defaults. A member left out
 * keeps its value.
 *
 * @property vertices replaces every vertex.
 * @property color [Nullable.Clear] removes the stroke.
 * @property dashArray [Nullable.Clear] removes the pattern.
 * @property interiorColor [Nullable.Clear] removes the fill.
 * @property rotation [Nullable.Clear] removes the rotation.
 * @property intent [Nullable.Clear] makes it a plain polyline.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree.
 *   [Nullable.Clear] makes the comment plain and keeps `contents`.
 * @property measure replaces the scale, on the draft's rules.
 *   [Nullable.Clear] removes it. A scale read with another subtype
 *   (`geospatial`, `unknown`) may be sent back as it was read, which keeps it.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class PolylineAnnotationPatch(
    val vertices: List<Point>? = null,
    val color: Nullable<AnnotationColor> = Nullable.Unchanged,
    val opacity: Double? = null,
    val strokeWidth: Double? = null,
    val borderStyle: AnnotationBorderStyle? = null,
    val dashArray: Nullable<List<Double>> = Nullable.Unchanged,
    val interiorColor: Nullable<AnnotationColor> = Nullable.Unchanged,
    val lineEndings: AnnotationLineEndings? = null,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val intent: Nullable<AnnotationPolylineIntent> = Nullable.Unchanged,
    val captionEnabled: Nullable<Boolean> = Nullable.Unchanged,
    val captionCenter: AnnotationCaptionCenter? = null,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val measure: Nullable<PdfMeasurement> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    override fun json(): String = FieldBagWriter().run {
        pathFields(
            vertices, color, opacity, strokeWidth, borderStyle, dashArray, interiorColor, rotation, intent.wire { it.value },
            captionEnabled, captionCenter, contents, blendMode,
        )
        put("lineEndings", lineEndings?.let(FieldValues::lineEndings))
        richText(richText)
        measure(measure)
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<PolylineAnnotationPatch> {
        /** Only a polygon declares `cloudyIntensity`, so this leaves it unread. */
        override fun fromJson(json: String): PolylineAnnotationPatch = readPatch(json) {
            val path = readPath()
            PolylineAnnotationPatch(
                vertices = path.vertices,
                color = path.color,
                opacity = path.opacity,
                strokeWidth = path.strokeWidth,
                borderStyle = path.borderStyle,
                dashArray = path.dashArray,
                interiorColor = path.interiorColor,
                lineEndings = optional("lineEndings", FieldValues::lineEndings),
                rotation = path.rotation,
                intent = nullable("intent") { FieldValues.enum(it, AnnotationPolylineIntent.entries) { intent -> intent.value } },
                captionEnabled = path.captionEnabled,
                captionCenter = path.captionCenter,
                contents = path.contents,
                blendMode = path.blendMode,
                richText = richText(),
                measure = measure(),
                common = common(),
            )
        }
    }
}

/** A polygon's or polyline's shared members; `intent` is already its wire text. */
private fun FieldBagWriter.pathFields(
    vertices: List<Point>?,
    color: Nullable<AnnotationColor>,
    opacity: Double?,
    strokeWidth: Double?,
    borderStyle: AnnotationBorderStyle?,
    dashArray: Nullable<List<Double>>,
    interiorColor: Nullable<AnnotationColor>,
    rotation: Nullable<Double>,
    intent: Nullable<String>,
    captionEnabled: Nullable<Boolean>,
    captionCenter: AnnotationCaptionCenter?,
    contents: Nullable<String>,
    blendMode: AnnotationBlendMode?,
) {
    put("vertices", vertices?.let(FieldValues::points))
    stroke(color, opacity, strokeWidth, borderStyle, dashArray, interiorColor)
    put("rotation", rotation, ::JsonPrimitive)
    put("intent", intent, ::JsonPrimitive)
    put("captionEnabled", captionEnabled, ::JsonPrimitive)
    put("captionCenter", captionCenter?.let(FieldValues::captionCenter))
    put("contents", contents, ::JsonPrimitive)
    put("blendMode", blendMode?.value)
}

private class PathMembers(
    val vertices: List<Point>?,
    val color: Nullable<AnnotationColor>,
    val opacity: Double?,
    val strokeWidth: Double?,
    val borderStyle: AnnotationBorderStyle?,
    val dashArray: Nullable<List<Double>>,
    val interiorColor: Nullable<AnnotationColor>,
    val rotation: Nullable<Double>,
    val captionEnabled: Nullable<Boolean>,
    val captionCenter: AnnotationCaptionCenter?,
    val contents: Nullable<String>,
    val blendMode: AnnotationBlendMode?,
)

private fun FieldBagReader.readPath(): PathMembers = PathMembers(
    vertices = optional("vertices", FieldValues::points),
    color = nullable("color", FieldValues::color),
    opacity = optional("opacity", FieldValues::double),
    strokeWidth = optional("strokeWidth", FieldValues::double),
    borderStyle = optional("borderStyle", FieldValues::borderStyle),
    dashArray = nullable("dashArray", FieldValues::doubles),
    interiorColor = nullable("interiorColor", FieldValues::color),
    rotation = nullable("rotation", FieldValues::double),
    captionEnabled = nullable("captionEnabled", FieldValues::boolean),
    // A caption centre's `null` is a value: the engine places the caption.
    captionCenter = when (val center = members["captionCenter"]) {
        null -> null
        is JsonNull -> AnnotationCaptionCenter.Automatic
        else -> AnnotationCaptionCenter.At(FieldValues.point(center))
    },
    contents = nullable("contents", FieldValues::string),
    blendMode = blendMode(),
)

/** The value mapped, [Nullable.Unchanged] and [Nullable.Clear] kept. */
internal inline fun <T, R> Nullable<T>.wire(transform: (T) -> R): Nullable<R> = when (this) {
    Nullable.Unchanged -> Nullable.Unchanged
    Nullable.Clear -> Nullable.Clear
    is Nullable.Value -> Nullable.Value(transform(value))
}

// ── values ──────────────────────────────────────────────────────────────────

/** A line's two points, `{start, end}`. */
internal fun FieldValues.linePoints(start: Point, end: Point): JsonObject =
    JsonObject(linkedMapOf("start" to point(start), "end" to point(end)))

internal fun FieldValues.lineEndings(endings: AnnotationLineEndings): JsonObject =
    JsonObject(linkedMapOf("start" to JsonPrimitive(endings.start.value), "end" to JsonPrimitive(endings.end.value)))

internal fun FieldValues.lineEndings(element: JsonElement): AnnotationLineEndings {
    val endings = element.asObject("line endings")
    return AnnotationLineEndings(lineEnding(endings.field("start")), lineEnding(endings.field("end")))
}

internal fun FieldValues.captionOffset(offset: AnnotationCaptionOffset): JsonObject =
    JsonObject(linkedMapOf("along" to JsonPrimitive(offset.along), "perpendicular" to JsonPrimitive(offset.perpendicular)))

internal fun FieldValues.captionOffset(element: JsonElement): AnnotationCaptionOffset {
    val offset = element.asObject("caption offset")
    return AnnotationCaptionOffset(double(offset.field("along")), double(offset.field("perpendicular")))
}

/** `extension` and `offset` are left out when `null`. */
internal fun FieldValues.leader(leader: AnnotationLineLeader): JsonObject = JsonObject(
    buildMap {
        put("length", JsonPrimitive(leader.length))
        leader.extension?.let { put("extension", JsonPrimitive(it)) }
        leader.offset?.let { put("offset", JsonPrimitive(it)) }
    },
)

internal fun FieldValues.leader(element: JsonElement): AnnotationLineLeader = with(FieldBagReader(element.asObject("leader"))) {
    AnnotationLineLeader(
        length = required("length", FieldValues::double),
        extension = optional("extension", FieldValues::double),
        offset = optional("offset", FieldValues::double),
    )
}

internal fun FieldValues.captionPosition(element: JsonElement): AnnotationCaptionPosition =
    enum(element, AnnotationCaptionPosition.entries) { it.value }

internal fun FieldValues.captionCenter(center: AnnotationCaptionCenter): JsonElement = when (center) {
    AnnotationCaptionCenter.Automatic -> JsonNull
    is AnnotationCaptionCenter.At -> point(center.point)
}
