package com.embedpdf

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonPrimitive

// Typed drafts and patches for free text, carets and links, and patches for
// stamps and file attachments. The rules are in `AnnotationDraft.kt`; each
// member's own rule is the engine's (`read_field` in
// `crates/epdf-engine/src/features/annotation/write/prepare.rs`).
//
// Stamps and file attachments have no draft: a create needs their image or
// file bytes beside the field bag (`resource_roles` in `prepare.rs`), which
// `AnnotationCapability.create` doesn't carry. Place a stamp with
// `AnnotationDraftsCapability.armStamp` or `placeStamp`.

/** Where free text sits in its box, top to bottom. */
@Serializable
public enum class AnnotationVerticalAlignment(public val value: String) {
    @SerialName("top") TOP("top"),
    @SerialName("middle") MIDDLE("middle"),
    @SerialName("bottom") BOTTOM("bottom"),
}

/** A `/LE` line ending (`LineEnding` in `vocabulary.rs`). */
@Serializable
public enum class AnnotationLineEnding(public val value: String) {
    /** `none`: the line just stops. */
    @SerialName("none") NONE("none"),
    @SerialName("square") SQUARE("square"),
    @SerialName("circle") CIRCLE("circle"),
    @SerialName("diamond") DIAMOND("diamond"),
    @SerialName("open-arrow") OPEN_ARROW("open-arrow"),
    @SerialName("closed-arrow") CLOSED_ARROW("closed-arrow"),
    @SerialName("butt") BUTT("butt"),
    @SerialName("r-open-arrow") REVERSED_OPEN_ARROW("r-open-arrow"),
    @SerialName("r-closed-arrow") REVERSED_CLOSED_ARROW("r-closed-arrow"),
    @SerialName("slash") SLASH("slash"),
}

/** A free text's `/IT`. */
@Serializable
public enum class AnnotationFreeTextIntent(public val value: String) {
    /** A text box. */
    @SerialName("free-text") FREE_TEXT("free-text"),

    /** A text box with a leader line, `calloutLine`, to what it calls out. */
    @SerialName("free-text-callout") FREE_TEXT_CALLOUT("free-text-callout"),
}

/** A caret's `/IT`. A caret without one marks an insertion. */
@Serializable
public enum class AnnotationCaretIntent(public val value: String) {
    /** `replace`: the caret of a replacement, paired with a strikeout. */
    @SerialName("replace") REPLACE("replace"),
}

/** A stamp's fit: how its image fills its box. */
@Serializable
public enum class AnnotationStampFit(public val value: String) {
    @SerialName("contain") CONTAIN("contain"),
    @SerialName("cover") COVER("cover"),
    @SerialName("fill") FILL("fill"),
}

/** A file attachment's `/Name` icon (`FileAttachmentIcon` in `vocabulary.rs`). */
@Serializable
public enum class AnnotationFileAttachmentIcon(public val value: String) {
    @SerialName("graph") GRAPH("graph"),
    @SerialName("push-pin") PUSH_PIN("push-pin"),
    @SerialName("paperclip") PAPERCLIP("paperclip"),
    @SerialName("tag") TAG("tag"),
}

/**
 * A free-text callout's leader line (`/CL`), page space: from the point
 * called out to the text box, straight or with one knee. It writes as its
 * two or three [points].
 */
public sealed interface AnnotationCalloutLine {
    /** The two or three points, in order. */
    public val points: List<Point>

    public data class Straight(val from: Point, val to: Point) : AnnotationCalloutLine {
        override val points: List<Point> get() = listOf(from, to)
    }

    public data class Kneed(val from: Point, val knee: Point, val to: Point) : AnnotationCalloutLine {
        override val points: List<Point> get() = listOf(from, knee, to)
    }

    public companion object {
        /** The line through two or three points, or `null` for any other count. */
        public fun of(points: List<Point>): AnnotationCalloutLine? = when (points.size) {
            2 -> Straight(points[0], points[1])
            3 -> Kneed(points[0], points[1], points[2])
            else -> null
        }
    }
}

// ── free text ───────────────────────────────────────────────────────────────

/**
 * A new free text: a text box drawn on the page.
 *
 * @property box the text box, page space, before rotation.
 * @property contents `/Contents`: the text, plain. The engine lays it out in
 *   the body style below. Styled runs are [richText]; a draft that sets both
 *   needs `contents` to be `richText`'s plain text.
 * @property fontFamily the body font: a standard font's name (`helvetica`) or
 *   a registered font's key.
 * @property fontSize points, above 0.
 * @property color `/C`: the border.
 * @property interiorColor `/IC`, the fill. [Nullable.Clear] is none.
 * @property opacity `/CA`, 0 to 1.
 * @property strokeWidth the border width, 0 or more; 0 draws no border.
 * @property dashArray the dash lengths, each 0 or more, at least one.
 *   [Nullable.Clear] is none.
 * @property cloudyIntensity `/BE`: a cloudy border's intensity, above 0.
 *   [Nullable.Clear] is a plain border.
 * @property rotation degrees. [Nullable.Clear] is none.
 * @property intent [Nullable.Clear] is the engine's default, a plain text box.
 * @property calloutLine a callout's leader line. [Nullable.Clear] is none.
 * @property lineEnding the ending at the called-out end of [calloutLine].
 *   [Nullable.Clear] is none.
 * @property richText the text as styled runs. It sets `contents` to its plain
 *   text, and a draft that gives both needs them to agree. Left out, the
 *   engine makes one unstyled run per line of `contents`. There is no
 *   clearing it: a free text always has rich text.
 * @property common the fields every annotation shares.
 */
public data class FreeTextAnnotationDraft(
    val box: Rect,
    val contents: Nullable<String> = Nullable.Unchanged,
    val fontFamily: String? = null,
    val fontSize: Double? = null,
    val fontColor: AnnotationColor? = null,
    val textAlign: AnnotationTextAlignment? = null,
    val verticalAlign: AnnotationVerticalAlignment? = null,
    val color: AnnotationColor? = null,
    val interiorColor: Nullable<AnnotationColor> = Nullable.Unchanged,
    val opacity: Double? = null,
    val strokeWidth: Double? = null,
    val borderStyle: AnnotationBorderStyle? = null,
    val dashArray: Nullable<List<Double>> = Nullable.Unchanged,
    val cloudyIntensity: Nullable<Double> = Nullable.Unchanged,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val intent: Nullable<AnnotationFreeTextIntent> = Nullable.Unchanged,
    val calloutLine: Nullable<AnnotationCalloutLine> = Nullable.Unchanged,
    val lineEnding: Nullable<AnnotationLineEnding> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: AnnotationRichText? = null,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationDraft {
    override fun json(): String = FieldBagWriter().run {
        put("subtype", AnnotationSubtype.FREE_TEXT.value)
        freeTextFields(
            box, contents, fontFamily, fontSize, fontColor, textAlign, verticalAlign, color, interiorColor, opacity,
            strokeWidth, borderStyle, dashArray, cloudyIntensity, rotation, intent, calloutLine, lineEnding, blendMode,
        )
        put("richText", richText?.let(AnnotationRichTextSerializer::element))
        draftCommon(common)
    }
}

/**
 * A change to a free text, or to a free-text tool's defaults. A member left
 * out keeps its value.
 *
 * @property box moves or resizes the text box.
 * @property contents replaces the text, plain. [Nullable.Clear] removes it.
 * @property interiorColor [Nullable.Clear] removes the fill.
 * @property dashArray [Nullable.Clear] removes the pattern.
 * @property cloudyIntensity [Nullable.Clear] makes the border plain.
 * @property rotation [Nullable.Clear] removes the rotation.
 * @property intent [Nullable.Clear] removes the intent.
 * @property calloutLine [Nullable.Clear] removes the leader line.
 * @property lineEnding [Nullable.Clear] removes the ending.
 * @property richText replaces the text with styled runs. It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree. A
 *   `body` it gives replaces the body style whole. There is no clearing it.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class FreeTextAnnotationPatch(
    val box: Rect? = null,
    val contents: Nullable<String> = Nullable.Unchanged,
    val fontFamily: String? = null,
    val fontSize: Double? = null,
    val fontColor: AnnotationColor? = null,
    val textAlign: AnnotationTextAlignment? = null,
    val verticalAlign: AnnotationVerticalAlignment? = null,
    val color: AnnotationColor? = null,
    val interiorColor: Nullable<AnnotationColor> = Nullable.Unchanged,
    val opacity: Double? = null,
    val strokeWidth: Double? = null,
    val borderStyle: AnnotationBorderStyle? = null,
    val dashArray: Nullable<List<Double>> = Nullable.Unchanged,
    val cloudyIntensity: Nullable<Double> = Nullable.Unchanged,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val intent: Nullable<AnnotationFreeTextIntent> = Nullable.Unchanged,
    val calloutLine: Nullable<AnnotationCalloutLine> = Nullable.Unchanged,
    val lineEnding: Nullable<AnnotationLineEnding> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: AnnotationRichText? = null,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    override fun json(): String = FieldBagWriter().run {
        freeTextFields(
            box, contents, fontFamily, fontSize, fontColor, textAlign, verticalAlign, color, interiorColor, opacity,
            strokeWidth, borderStyle, dashArray, cloudyIntensity, rotation, intent, calloutLine, lineEnding, blendMode,
        )
        put("richText", richText?.let(AnnotationRichTextSerializer::element))
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<FreeTextAnnotationPatch> {
        /** A free text's rich text has no clearing: `null` reads as left out. */
        override fun fromJson(json: String): FreeTextAnnotationPatch = readPatch(json) {
            FreeTextAnnotationPatch(
                box = optional("box", FieldValues::rect),
                contents = nullable("contents", FieldValues::string),
                fontFamily = optional("fontFamily", FieldValues::string),
                fontSize = optional("fontSize", FieldValues::double),
                fontColor = optional("fontColor", FieldValues::color),
                textAlign = optional("textAlign") { FieldValues.enum(it, AnnotationTextAlignment.entries) { align -> align.value } },
                verticalAlign = optional("verticalAlign") {
                    FieldValues.enum(it, AnnotationVerticalAlignment.entries) { align -> align.value }
                },
                color = optional("color", FieldValues::color),
                interiorColor = nullable("interiorColor", FieldValues::color),
                opacity = optional("opacity", FieldValues::double),
                strokeWidth = optional("strokeWidth", FieldValues::double),
                borderStyle = optional("borderStyle", FieldValues::borderStyle),
                dashArray = nullable("dashArray", FieldValues::doubles),
                cloudyIntensity = nullable("cloudyIntensity", FieldValues::double),
                rotation = nullable("rotation", FieldValues::double),
                intent = nullable("intent") { FieldValues.enum(it, AnnotationFreeTextIntent.entries) { intent -> intent.value } },
                calloutLine = nullable("calloutLine", FieldValues::calloutLine),
                lineEnding = nullable("lineEnding", FieldValues::lineEnding),
                blendMode = blendMode(),
                richText = optional("richText", AnnotationRichTextSerializer::read),
                common = common(),
            )
        }
    }
}

private fun FieldBagWriter.freeTextFields(
    box: Rect?,
    contents: Nullable<String>,
    fontFamily: String?,
    fontSize: Double?,
    fontColor: AnnotationColor?,
    textAlign: AnnotationTextAlignment?,
    verticalAlign: AnnotationVerticalAlignment?,
    color: AnnotationColor?,
    interiorColor: Nullable<AnnotationColor>,
    opacity: Double?,
    strokeWidth: Double?,
    borderStyle: AnnotationBorderStyle?,
    dashArray: Nullable<List<Double>>,
    cloudyIntensity: Nullable<Double>,
    rotation: Nullable<Double>,
    intent: Nullable<AnnotationFreeTextIntent>,
    calloutLine: Nullable<AnnotationCalloutLine>,
    lineEnding: Nullable<AnnotationLineEnding>,
    blendMode: AnnotationBlendMode?,
) {
    put("box", box?.let(FieldValues::rect))
    put("contents", contents, ::JsonPrimitive)
    put("fontFamily", fontFamily)
    put("fontSize", fontSize)
    put("fontColor", fontColor?.let(FieldValues::color))
    put("textAlign", textAlign?.value)
    put("verticalAlign", verticalAlign?.value)
    put("color", color?.let(FieldValues::color))
    put("interiorColor", interiorColor, FieldValues::color)
    put("opacity", opacity)
    put("strokeWidth", strokeWidth)
    put("borderStyle", borderStyle?.value)
    put("dashArray", dashArray, FieldValues::doubles)
    put("cloudyIntensity", cloudyIntensity, ::JsonPrimitive)
    put("rotation", rotation, ::JsonPrimitive)
    put("intent", intent) { JsonPrimitive(it.value) }
    put("calloutLine", calloutLine) { FieldValues.points(it.points) }
    put("lineEnding", lineEnding) { JsonPrimitive(it.value) }
    put("blendMode", blendMode?.value)
}

// ── caret ───────────────────────────────────────────────────────────────────

/**
 * A new caret: an insertion mark.
 *
 * @property box the caret's box, page space, before rotation.
 * @property color `/C`. A caret always has one, so it takes no
 *   [Nullable.Clear].
 * @property opacity `/CA`, 0 to 1.
 * @property rotation degrees. [Nullable.Clear] is none.
 * @property intent [Nullable.Clear] is an insertion.
 * @property contents `/Contents`: the text to insert. [Nullable.Clear] is none.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a draft that gives both needs them to agree.
 *   [Nullable.Clear] is a plain comment.
 * @property common the fields every annotation shares.
 */
public data class CaretAnnotationDraft(
    val box: Rect,
    val color: AnnotationColor? = null,
    val opacity: Double? = null,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val intent: Nullable<AnnotationCaretIntent> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationDraft {
    override fun json(): String = FieldBagWriter().run {
        put("subtype", AnnotationSubtype.CARET.value)
        caretFields(box, color, opacity, rotation, intent, contents, blendMode)
        richText(richText)
        draftCommon(common)
    }
}

/**
 * A change to a caret, or to a caret tool's defaults. A member left out
 * keeps its value.
 *
 * @property box moves or resizes the caret.
 * @property rotation [Nullable.Clear] removes the rotation.
 * @property intent [Nullable.Clear] makes it an insertion.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree.
 *   [Nullable.Clear] makes the comment plain and keeps `contents`.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class CaretAnnotationPatch(
    val box: Rect? = null,
    val color: AnnotationColor? = null,
    val opacity: Double? = null,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val intent: Nullable<AnnotationCaretIntent> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    override fun json(): String = FieldBagWriter().run {
        caretFields(box, color, opacity, rotation, intent, contents, blendMode)
        richText(richText)
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<CaretAnnotationPatch> {
        override fun fromJson(json: String): CaretAnnotationPatch = readPatch(json) {
            CaretAnnotationPatch(
                box = optional("box", FieldValues::rect),
                color = optional("color", FieldValues::color),
                opacity = optional("opacity", FieldValues::double),
                rotation = nullable("rotation", FieldValues::double),
                intent = nullable("intent") { FieldValues.enum(it, AnnotationCaretIntent.entries) { intent -> intent.value } },
                contents = nullable("contents", FieldValues::string),
                blendMode = blendMode(),
                richText = richText(),
                common = common(),
            )
        }
    }
}

private fun FieldBagWriter.caretFields(
    box: Rect?,
    color: AnnotationColor?,
    opacity: Double?,
    rotation: Nullable<Double>,
    intent: Nullable<AnnotationCaretIntent>,
    contents: Nullable<String>,
    blendMode: AnnotationBlendMode?,
) {
    put("box", box?.let(FieldValues::rect))
    put("color", color?.let(FieldValues::color))
    put("opacity", opacity)
    put("rotation", rotation, ::JsonPrimitive)
    put("intent", intent) { JsonPrimitive(it.value) }
    put("contents", contents, ::JsonPrimitive)
    put("blendMode", blendMode?.value)
}

// ── link ────────────────────────────────────────────────────────────────────

/**
 * A new link: an area that goes somewhere when activated. A link draws
 * nothing, so it takes no `blendMode`.
 *
 * @property rect `/Rect`, page space: the area that activates.
 * @property contents `/Contents`. [Nullable.Clear] is none.
 * @property common the fields every annotation shares.
 */
public data class LinkAnnotationDraft(
    val rect: Rect,
    val target: AnnotationLinkTarget,
    val contents: Nullable<String> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationDraft {
    override fun json(): String = FieldBagWriter().run {
        put("subtype", AnnotationSubtype.LINK.value)
        linkFields(rect, Nullable.Value(target), contents)
        draftCommon(common)
    }
}

/**
 * A change to a link. A member left out keeps its value.
 *
 * @property rect moves or resizes the area.
 * @property target [Nullable.Clear] removes the target: the link goes nowhere.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class LinkAnnotationPatch(
    val rect: Rect? = null,
    val target: Nullable<AnnotationLinkTarget> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    override fun json(): String = FieldBagWriter().run {
        linkFields(rect, target, contents)
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<LinkAnnotationPatch> {
        /** Throws for a `target` outside the three [AnnotationLinkTarget] holds. */
        override fun fromJson(json: String): LinkAnnotationPatch = readPatch(json) {
            LinkAnnotationPatch(
                rect = optional("rect", FieldValues::rect),
                target = nullable("target", FieldValues::linkTarget),
                contents = nullable("contents", FieldValues::string),
                common = common(),
            )
        }
    }
}

private fun FieldBagWriter.linkFields(rect: Rect?, target: Nullable<AnnotationLinkTarget>, contents: Nullable<String>) {
    put("rect", rect?.let(FieldValues::rect))
    put("target", target, AnnotationWire::linkTarget)
    put("contents", contents, ::JsonPrimitive)
}

// ── stamp ───────────────────────────────────────────────────────────────────

/**
 * A change to a stamp. A member left out keeps its value.
 *
 * There is no `blendMode`: the engine sets `/BM` while it draws an
 * appearance, and it keeps a stamp's image as placed instead of drawing one
 * (`write/update.rs` in `crates/epdf-engine/src/features/annotation`), so the
 * field would be accepted and dropped.
 *
 * @property box moves or resizes the stamp's box, page space, before
 *   rotation.
 * @property opacity `/CA`, 0 to 1.
 * @property rotation degrees. [Nullable.Clear] removes the rotation.
 * @property name `/Name`: the stamp's name (`Approved`, or a custom one).
 *   [Nullable.Clear] removes it.
 * @property fit how the image fills the box. [Nullable.Clear] removes it.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree.
 *   [Nullable.Clear] makes the comment plain and keeps `contents`.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class StampAnnotationPatch(
    val box: Rect? = null,
    val opacity: Double? = null,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val name: Nullable<String> = Nullable.Unchanged,
    val fit: Nullable<AnnotationStampFit> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    override fun json(): String = FieldBagWriter().run {
        put("box", box?.let(FieldValues::rect))
        put("opacity", opacity)
        put("rotation", rotation, ::JsonPrimitive)
        put("name", name, ::JsonPrimitive)
        put("fit", fit) { JsonPrimitive(it.value) }
        put("contents", contents, ::JsonPrimitive)
        richText(richText)
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<StampAnnotationPatch> {
        override fun fromJson(json: String): StampAnnotationPatch = readPatch(json) {
            StampAnnotationPatch(
                box = optional("box", FieldValues::rect),
                opacity = optional("opacity", FieldValues::double),
                rotation = nullable("rotation", FieldValues::double),
                name = nullable("name", FieldValues::string),
                fit = nullable("fit") { FieldValues.enum(it, AnnotationStampFit.entries) { fit -> fit.value } },
                contents = nullable("contents", FieldValues::string),
                richText = richText(),
                common = common(),
            )
        }
    }
}

// ── file attachment ─────────────────────────────────────────────────────────

/**
 * A change to a file attachment's icon. A member left out keeps its value.
 * The file itself is the attachment plugin's.
 *
 * @property rect moves or resizes the icon.
 * @property color `/C`.
 * @property opacity `/CA`, 0 to 1.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree.
 *   [Nullable.Clear] makes the comment plain and keeps `contents`.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class FileAttachmentAnnotationPatch(
    val rect: Rect? = null,
    val icon: AnnotationFileAttachmentIcon? = null,
    val color: AnnotationColor? = null,
    val opacity: Double? = null,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    override fun json(): String = FieldBagWriter().run {
        put("rect", rect?.let(FieldValues::rect))
        put("icon", icon?.value)
        put("color", color?.let(FieldValues::color))
        put("opacity", opacity)
        put("contents", contents, ::JsonPrimitive)
        put("blendMode", blendMode?.value)
        richText(richText)
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<FileAttachmentAnnotationPatch> {
        override fun fromJson(json: String): FileAttachmentAnnotationPatch = readPatch(json) {
            FileAttachmentAnnotationPatch(
                rect = optional("rect", FieldValues::rect),
                icon = optional("icon", FieldValues::fileAttachmentIcon),
                color = optional("color", FieldValues::color),
                opacity = optional("opacity", FieldValues::double),
                contents = nullable("contents", FieldValues::string),
                blendMode = blendMode(),
                richText = richText(),
                common = common(),
            )
        }
    }
}

// ── reading ─────────────────────────────────────────────────────────────────

internal fun FieldValues.borderStyle(element: JsonElement): AnnotationBorderStyle =
    enum(element, AnnotationBorderStyle.entries) { it.value }

internal fun FieldValues.lineEnding(element: JsonElement): AnnotationLineEnding =
    enum(element, AnnotationLineEnding.entries) { it.value }

internal fun FieldValues.fileAttachmentIcon(element: JsonElement): AnnotationFileAttachmentIcon =
    enum(element, AnnotationFileAttachmentIcon.entries) { it.value }

/** Two points are a straight line, three a kneed one; any other count throws. */
internal fun FieldValues.calloutLine(element: JsonElement): AnnotationCalloutLine =
    AnnotationCalloutLine.of(points(element)) ?: throw SerializationException("a callout line has two or three points")

internal fun FieldValues.linkTarget(element: JsonElement): AnnotationLinkTarget =
    AnnotationWire.json.decodeFromJsonElement(AnnotationLinkTargetSerializer, element)
