package com.embedpdf

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.JsonPrimitive

// Typed drafts and patches for the text markups (underline, squiggly,
// strikeout) and redactions. The rules are in `AnnotationDraft.kt`; each
// member's own rule is the engine's (`read_field` in
// `crates/epdf-engine/src/features/annotation/write/prepare.rs`).

/**
 * How text lines up across its box (`/Q`). Only free text takes [JUSTIFY]; a
 * redaction's overlay text refuses it.
 */
@Serializable
public enum class AnnotationTextAlignment(public val value: String) {
    @SerialName("left") LEFT("left"),
    @SerialName("center") CENTER("center"),
    @SerialName("right") RIGHT("right"),
    @SerialName("justify") JUSTIFY("justify"),
}

/**
 * A strikeout's `/IT` (`intents_of` in `prepare.rs`). A strikeout without one
 * is a plain strikeout.
 */
@Serializable
public enum class AnnotationStrikeoutIntent(public val value: String) {
    /** `strikeout-text-edit`: text marked for deletion. */
    @SerialName("strikeout-text-edit") STRIKEOUT_TEXT_EDIT("strikeout-text-edit"),
}

// ── underline ───────────────────────────────────────────────────────────────

/**
 * A new underline over text quads.
 *
 * @property quadPoints `/QuadPoints`, page space: one quad per run of marked
 *   text, at least one.
 * @property color `/C`. A text markup always has one, so it takes no
 *   [Nullable.Clear].
 * @property opacity `/CA`, 0 to 1.
 * @property contents `/Contents`. [Nullable.Clear] is none.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a draft that gives both needs them to agree.
 *   [Nullable.Clear] is a plain comment.
 * @property common the fields every annotation shares.
 */
public data class UnderlineAnnotationDraft(
    val quadPoints: List<Quad>,
    val color: AnnotationColor? = null,
    val opacity: Double? = null,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationDraft {
    override fun json(): String = FieldBagWriter().run {
        put("subtype", AnnotationSubtype.UNDERLINE.value)
        textMarkupFields(quadPoints, color, opacity, contents, blendMode)
        richText(richText)
        draftCommon(common)
    }
}

/**
 * A change to an underline, or to an underline tool's defaults. A member left
 * out keeps its value.
 *
 * @property quadPoints replaces every quad.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree.
 *   [Nullable.Clear] makes the comment plain and keeps `contents`.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class UnderlineAnnotationPatch(
    val quadPoints: List<Quad>? = null,
    val color: AnnotationColor? = null,
    val opacity: Double? = null,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    override fun json(): String = FieldBagWriter().run {
        textMarkupFields(quadPoints, color, opacity, contents, blendMode)
        richText(richText)
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<UnderlineAnnotationPatch> {
        override fun fromJson(json: String): UnderlineAnnotationPatch = readPatch(json) {
            UnderlineAnnotationPatch(
                quadPoints = optional("quadPoints", FieldValues::quads),
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

// ── squiggly ────────────────────────────────────────────────────────────────

/**
 * A new squiggly underline over text quads.
 *
 * @property quadPoints `/QuadPoints`, page space: one quad per run of marked
 *   text, at least one.
 * @property color `/C`. A text markup always has one, so it takes no
 *   [Nullable.Clear].
 * @property opacity `/CA`, 0 to 1.
 * @property contents `/Contents`. [Nullable.Clear] is none.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a draft that gives both needs them to agree.
 *   [Nullable.Clear] is a plain comment.
 * @property common the fields every annotation shares.
 */
public data class SquigglyAnnotationDraft(
    val quadPoints: List<Quad>,
    val color: AnnotationColor? = null,
    val opacity: Double? = null,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationDraft {
    override fun json(): String = FieldBagWriter().run {
        put("subtype", AnnotationSubtype.SQUIGGLY.value)
        textMarkupFields(quadPoints, color, opacity, contents, blendMode)
        richText(richText)
        draftCommon(common)
    }
}

/**
 * A change to a squiggly underline, or to a squiggly tool's defaults. A
 * member left out keeps its value.
 *
 * @property quadPoints replaces every quad.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree.
 *   [Nullable.Clear] makes the comment plain and keeps `contents`.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class SquigglyAnnotationPatch(
    val quadPoints: List<Quad>? = null,
    val color: AnnotationColor? = null,
    val opacity: Double? = null,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    override fun json(): String = FieldBagWriter().run {
        textMarkupFields(quadPoints, color, opacity, contents, blendMode)
        richText(richText)
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<SquigglyAnnotationPatch> {
        override fun fromJson(json: String): SquigglyAnnotationPatch = readPatch(json) {
            SquigglyAnnotationPatch(
                quadPoints = optional("quadPoints", FieldValues::quads),
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

// ── strikeout ───────────────────────────────────────────────────────────────

/**
 * A new strikeout over text quads.
 *
 * @property quadPoints `/QuadPoints`, page space: one quad per run of marked
 *   text, at least one.
 * @property color `/C`. A text markup always has one, so it takes no
 *   [Nullable.Clear].
 * @property opacity `/CA`, 0 to 1.
 * @property intent [Nullable.Clear] is a plain strikeout.
 * @property contents `/Contents`. [Nullable.Clear] is none.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a draft that gives both needs them to agree.
 *   [Nullable.Clear] is a plain comment.
 * @property common the fields every annotation shares.
 */
public data class StrikeoutAnnotationDraft(
    val quadPoints: List<Quad>,
    val color: AnnotationColor? = null,
    val opacity: Double? = null,
    val intent: Nullable<AnnotationStrikeoutIntent> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationDraft {
    override fun json(): String = FieldBagWriter().run {
        put("subtype", AnnotationSubtype.STRIKEOUT.value)
        textMarkupFields(quadPoints, color, opacity, contents, blendMode)
        put("intent", intent) { JsonPrimitive(it.value) }
        richText(richText)
        draftCommon(common)
    }
}

/**
 * A change to a strikeout, or to a strikeout tool's defaults. A member left
 * out keeps its value.
 *
 * @property quadPoints replaces every quad.
 * @property intent [Nullable.Clear] makes it a plain strikeout.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree.
 *   [Nullable.Clear] makes the comment plain and keeps `contents`.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class StrikeoutAnnotationPatch(
    val quadPoints: List<Quad>? = null,
    val color: AnnotationColor? = null,
    val opacity: Double? = null,
    val intent: Nullable<AnnotationStrikeoutIntent> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    override fun json(): String = FieldBagWriter().run {
        textMarkupFields(quadPoints, color, opacity, contents, blendMode)
        put("intent", intent) { JsonPrimitive(it.value) }
        richText(richText)
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<StrikeoutAnnotationPatch> {
        override fun fromJson(json: String): StrikeoutAnnotationPatch = readPatch(json) {
            StrikeoutAnnotationPatch(
                quadPoints = optional("quadPoints", FieldValues::quads),
                color = optional("color", FieldValues::color),
                opacity = optional("opacity", FieldValues::double),
                intent = nullable("intent") { FieldValues.enum(it, AnnotationStrikeoutIntent.entries) { intent -> intent.value } },
                contents = nullable("contents", FieldValues::string),
                blendMode = blendMode(),
                richText = richText(),
                common = common(),
            )
        }
    }
}

// ── redaction ───────────────────────────────────────────────────────────────

/**
 * A new redaction: marked for removal, applied later through the redaction
 * plugin. It marks text quads, an area ([rect]), or both.
 *
 * @property quadPoints `/QuadPoints`, page space: the text runs to remove. May
 *   be empty for an area redaction.
 * @property rect `/Rect`, page space: the area. Left out, it bounds
 *   [quadPoints].
 * @property color `/C`: the outline drawn before the redaction is applied.
 * @property opacity `/CA`, 0 to 1.
 * @property interiorColor `/IC`: what fills the area once applied.
 *   [Nullable.Clear] is no fill.
 * @property overlayText `/OverlayText`: the text drawn over the area once
 *   applied. [Nullable.Clear] is none.
 * @property repeat `/Repeat`: whether the overlay text tiles the area.
 * @property fontFamily the overlay text's font: a standard font's name
 *   (`helvetica`) or a registered font's key.
 * @property fontSize points, 0 or more; 0 fits the text to the area.
 * @property textAlign [AnnotationTextAlignment.JUSTIFY] is refused.
 * @property contents `/Contents`. [Nullable.Clear] is none.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a draft that gives both needs them to agree.
 *   [Nullable.Clear] is a plain comment.
 * @property common the fields every annotation shares.
 */
public data class RedactionAnnotationDraft(
    val quadPoints: List<Quad>? = null,
    val rect: Rect? = null,
    val color: AnnotationColor? = null,
    val opacity: Double? = null,
    val interiorColor: Nullable<AnnotationColor> = Nullable.Unchanged,
    val overlayText: Nullable<String> = Nullable.Unchanged,
    val repeat: Boolean? = null,
    val fontFamily: String? = null,
    val fontSize: Double? = null,
    val fontColor: AnnotationColor? = null,
    val textAlign: AnnotationTextAlignment? = null,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationDraft {
    override fun json(): String = FieldBagWriter().run {
        put("subtype", AnnotationSubtype.REDACT.value)
        redactionFields(
            quadPoints, rect, color, opacity, interiorColor, overlayText, repeat, fontFamily, fontSize, fontColor,
            textAlign, contents, blendMode,
        )
        richText(richText)
        draftCommon(common)
    }
}

/**
 * A change to a redaction, or to the redaction tool's defaults. A member left
 * out keeps its value.
 *
 * @property quadPoints replaces every quad.
 * @property interiorColor [Nullable.Clear] removes the fill.
 * @property overlayText [Nullable.Clear] removes the overlay text.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree.
 *   [Nullable.Clear] makes the comment plain and keeps `contents`.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class RedactionAnnotationPatch(
    val quadPoints: List<Quad>? = null,
    val rect: Rect? = null,
    val color: AnnotationColor? = null,
    val opacity: Double? = null,
    val interiorColor: Nullable<AnnotationColor> = Nullable.Unchanged,
    val overlayText: Nullable<String> = Nullable.Unchanged,
    val repeat: Boolean? = null,
    val fontFamily: String? = null,
    val fontSize: Double? = null,
    val fontColor: AnnotationColor? = null,
    val textAlign: AnnotationTextAlignment? = null,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    override fun json(): String = FieldBagWriter().run {
        redactionFields(
            quadPoints, rect, color, opacity, interiorColor, overlayText, repeat, fontFamily, fontSize, fontColor,
            textAlign, contents, blendMode,
        )
        richText(richText)
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<RedactionAnnotationPatch> {
        override fun fromJson(json: String): RedactionAnnotationPatch = readPatch(json) {
            RedactionAnnotationPatch(
                quadPoints = optional("quadPoints", FieldValues::quads),
                rect = optional("rect", FieldValues::rect),
                color = optional("color", FieldValues::color),
                opacity = optional("opacity", FieldValues::double),
                interiorColor = nullable("interiorColor", FieldValues::color),
                overlayText = nullable("overlayText", FieldValues::string),
                repeat = optional("repeat", FieldValues::boolean),
                fontFamily = optional("fontFamily", FieldValues::string),
                fontSize = optional("fontSize", FieldValues::double),
                fontColor = optional("fontColor", FieldValues::color),
                textAlign = optional("textAlign") { FieldValues.enum(it, AnnotationTextAlignment.entries) { align -> align.value } },
                contents = nullable("contents", FieldValues::string),
                blendMode = blendMode(),
                richText = richText(),
                common = common(),
            )
        }
    }
}

private fun FieldBagWriter.redactionFields(
    quadPoints: List<Quad>?,
    rect: Rect?,
    color: AnnotationColor?,
    opacity: Double?,
    interiorColor: Nullable<AnnotationColor>,
    overlayText: Nullable<String>,
    repeat: Boolean?,
    fontFamily: String?,
    fontSize: Double?,
    fontColor: AnnotationColor?,
    textAlign: AnnotationTextAlignment?,
    contents: Nullable<String>,
    blendMode: AnnotationBlendMode?,
) {
    put("quadPoints", quadPoints?.let(FieldValues::quads))
    put("rect", rect?.let(FieldValues::rect))
    put("color", color?.let(FieldValues::color))
    put("opacity", opacity)
    put("interiorColor", interiorColor, FieldValues::color)
    put("overlayText", overlayText, ::JsonPrimitive)
    put("repeat", repeat?.let(::JsonPrimitive))
    put("fontFamily", fontFamily)
    put("fontSize", fontSize)
    put("fontColor", fontColor?.let(FieldValues::color))
    put("textAlign", textAlign?.value)
    put("contents", contents, ::JsonPrimitive)
    put("blendMode", blendMode?.value)
}
