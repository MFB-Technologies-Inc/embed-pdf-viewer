package com.embedpdf

import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonObject

// Reading `AnnotationInfo.details` out of the engine's record. A required
// member that is missing, or a member of another shape, fails the read, and
// the details fall back to `Untyped`.

internal object AnnotationDetails {
    /** The typed members of [subtype] in [members], or `Untyped`. */
    fun read(subtype: AnnotationSubtype, members: JsonObject?): AnnotationInfo.Details {
        if (members == null) return AnnotationInfo.Details.Untyped
        val reader = FieldBagReader(members)
        return try {
            when (subtype) {
                AnnotationSubtype.INK -> reader.ink()
                AnnotationSubtype.HIGHLIGHT -> reader.highlight()
                AnnotationSubtype.TEXT -> reader.note()
                AnnotationSubtype.UNDERLINE -> reader.run {
                    AnnotationInfo.Underline(color(), opacity(), quads(), comment())
                }
                AnnotationSubtype.SQUIGGLY -> reader.run {
                    AnnotationInfo.Squiggly(color(), opacity(), quads(), comment())
                }
                AnnotationSubtype.STRIKEOUT -> reader.run {
                    AnnotationInfo.Strikeout(color(), opacity(), quads(), intent(), comment())
                }
                AnnotationSubtype.SQUARE -> reader.run {
                    AnnotationInfo.Square(
                        optional("color", FieldValues::color), opacity(), strokeWidth(), borderStyle(), dashArray(),
                        interiorColor(), box(), rotation(), optional("cloudyIntensity", FieldValues::double), comment(),
                    )
                }
                AnnotationSubtype.CIRCLE -> reader.run {
                    AnnotationInfo.Circle(
                        optional("color", FieldValues::color), opacity(), strokeWidth(), borderStyle(), dashArray(),
                        interiorColor(), box(), rotation(), optional("cloudyIntensity", FieldValues::double), comment(),
                    )
                }
                AnnotationSubtype.LINE -> reader.line()
                AnnotationSubtype.POLYGON -> reader.polygon()
                AnnotationSubtype.POLYLINE -> reader.polyline()
                AnnotationSubtype.FREE_TEXT -> reader.freeText()
                AnnotationSubtype.CARET -> reader.run {
                    AnnotationInfo.Caret(color(), opacity(), box(), rotation(), intent(), comment())
                }
                AnnotationSubtype.STAMP -> reader.run {
                    AnnotationInfo.Stamp(
                        optional("name", FieldValues::string),
                        optional("fit") { FieldValues.enum(it, AnnotationStampFit.entries) { fit -> fit.value } },
                        opacity(), box(), rotation(), comment(),
                    )
                }
                // A target a draft can't write doesn't read, and is `null`.
                AnnotationSubtype.LINK -> AnnotationInfo.Link(
                    try {
                        reader.optional("target", FieldValues::linkTarget)
                    } catch (e: IllegalArgumentException) {
                        null
                    },
                )
                AnnotationSubtype.FILE_ATTACHMENT -> reader.run {
                    AnnotationInfo.FileAttachment(
                        required("icon", FieldValues::fileAttachmentIcon), color(), opacity(),
                        optional("file", ::attachedFile), comment(),
                    )
                }
                AnnotationSubtype.REDACT -> reader.redaction()
                else -> AnnotationInfo.Details.Untyped
            }
        } catch (e: IllegalArgumentException) {
            // SerializationException is one.
            AnnotationInfo.Details.Untyped
        }
    }

    private fun FieldBagReader.color(): AnnotationColor = required("color", FieldValues::color)

    private fun FieldBagReader.opacity(): Double = required("opacity", FieldValues::double)

    private fun FieldBagReader.strokeWidth(): Double = required("strokeWidth", FieldValues::double)

    private fun FieldBagReader.borderStyle(): AnnotationInfo.BorderStyle =
        required("borderStyle") { AnnotationInfo.BorderStyle(FieldValues.string(it)) }

    private fun FieldBagReader.dashArray(): List<Double>? = optional("dashArray", FieldValues::doubles)

    private fun FieldBagReader.interiorColor(): AnnotationColor? = optional("interiorColor", FieldValues::color)

    private fun FieldBagReader.quads(): List<Quad> = required("quadPoints", FieldValues::quads)

    private fun FieldBagReader.box(): Rect = required("box", FieldValues::rect)

    private fun FieldBagReader.rotation(): Double? = optional("rotation", FieldValues::double)

    private fun FieldBagReader.intent(): AnnotationIntent? = optional("intent") { AnnotationIntent(FieldValues.string(it)) }

    private fun FieldBagReader.comment(): AnnotationRichText? = optional("richText", AnnotationRichTextSerializer::read)

    private fun FieldBagReader.scale(): PdfMeasurement? = optional("measure", AnnotationMeasure::read)

    private fun FieldBagReader.ink(): AnnotationInfo.Ink = AnnotationInfo.Ink(
        optional("color", FieldValues::color), opacity(), strokeWidth(), borderStyle(), dashArray(),
        required("inkList", FieldValues::strokes), intent(), rotation(), comment(),
    )

    private fun FieldBagReader.highlight(): AnnotationInfo.Highlight =
        AnnotationInfo.Highlight(color(), opacity(), quads(), comment())

    private fun FieldBagReader.note(): AnnotationInfo.Note = AnnotationInfo.Note(
        color(), opacity(), required("icon", FieldValues::string), required("open", FieldValues::boolean),
        optional("state", FieldValues::string), optional("stateModel", FieldValues::string), comment(),
    )

    private fun FieldBagReader.line(): AnnotationInfo.Line {
        val linePoints = required("linePoints") { it.asObject("line points") }
        return AnnotationInfo.Line(
            optional("color", FieldValues::color), opacity(), strokeWidth(), borderStyle(), dashArray(), interiorColor(),
            FieldValues.point(linePoints.field("start")), FieldValues.point(linePoints.field("end")),
            required("lineEndings", FieldValues::lineEndings), rotation(), intent(),
            required("captionEnabled", FieldValues::boolean), required("captionPosition", FieldValues::captionPosition),
            optional("captionOffset", FieldValues::captionOffset), optional("leader", FieldValues::leader), comment(), scale(),
        )
    }

    private fun FieldBagReader.polygon(): AnnotationInfo.Polygon = AnnotationInfo.Polygon(
        optional("color", FieldValues::color), opacity(), strokeWidth(), borderStyle(), dashArray(), interiorColor(),
        required("vertices", FieldValues::points), rotation(), intent(), optional("cloudyIntensity", FieldValues::double),
        optional("captionEnabled", FieldValues::boolean), optional("captionCenter", FieldValues::point), comment(), scale(),
    )

    private fun FieldBagReader.polyline(): AnnotationInfo.Polyline = AnnotationInfo.Polyline(
        optional("color", FieldValues::color), opacity(), strokeWidth(), borderStyle(), dashArray(), interiorColor(),
        required("vertices", FieldValues::points), required("lineEndings", FieldValues::lineEndings), rotation(), intent(),
        optional("captionEnabled", FieldValues::boolean), optional("captionCenter", FieldValues::point), comment(), scale(),
    )

    private fun FieldBagReader.freeText(): AnnotationInfo.FreeText = AnnotationInfo.FreeText(
        box = box(),
        intent = required("intent") { AnnotationIntent(FieldValues.string(it)) },
        fontFamily = required("fontFamily", FieldValues::string),
        fontSize = required("fontSize", FieldValues::double),
        fontColor = required("fontColor", FieldValues::color),
        textAlign = required("textAlign") { FieldValues.enum(it, AnnotationTextAlignment.entries) { align -> align.value } },
        verticalAlign = required("verticalAlign") { FieldValues.enum(it, AnnotationVerticalAlignment.entries) { align -> align.value } },
        color = color(),
        interiorColor = interiorColor(),
        opacity = opacity(),
        strokeWidth = strokeWidth(),
        borderStyle = borderStyle(),
        dashArray = dashArray(),
        cloudyIntensity = optional("cloudyIntensity", FieldValues::double),
        rotation = rotation(),
        calloutLine = optional("calloutLine", FieldValues::calloutLine),
        lineEnding = optional("lineEnding", FieldValues::lineEnding),
        richText = comment(),
    )

    private fun FieldBagReader.redaction(): AnnotationInfo.Redaction = AnnotationInfo.Redaction(
        quadPoints = quads(),
        color = color(),
        opacity = opacity(),
        interiorColor = interiorColor(),
        overlayText = optional("overlayText", FieldValues::string),
        repeat = required("repeat", FieldValues::boolean),
        fontFamily = required("fontFamily", FieldValues::string),
        fontSize = required("fontSize", FieldValues::double),
        fontColor = required("fontColor", FieldValues::color),
        textAlign = required("textAlign") { FieldValues.enum(it, AnnotationTextAlignment.entries) { align -> align.value } },
        richText = comment(),
    )

    private fun attachedFile(element: JsonElement): AnnotationInfo.AttachedFile = with(FieldBagReader(element.asObject("file"))) {
        AnnotationInfo.AttachedFile(
            name = required("name", FieldValues::string),
            mimeType = optional("mimeType", FieldValues::string),
            description = optional("description", FieldValues::string),
            sizeBytes = optional("sizeBytes") { value ->
                val number = FieldValues.double(value)
                if (number != Math.rint(number) || number < Long.MIN_VALUE.toDouble() || number > Long.MAX_VALUE.toDouble()) {
                    throw SerializationException("sizeBytes is not an integer")
                }
                number.toLong()
            },
            md5 = optional("md5", FieldValues::string),
            rawCreatedAt = optional("createdAt", FieldValues::string),
            rawModifiedAt = optional("modifiedAt", FieldValues::string),
        )
    }
}
