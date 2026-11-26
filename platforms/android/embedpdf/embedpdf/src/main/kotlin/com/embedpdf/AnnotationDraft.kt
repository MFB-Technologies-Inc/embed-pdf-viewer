package com.embedpdf

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.doubleOrNull

// Typed create drafts and patches, the interfaces they share and the
// vocabulary their members take. This file has ink, highlight and note;
// `AnnotationDraftMarkup.kt`, `AnnotationDraftShapes.kt` and
// `AnnotationDraftBoxes.kt` have the other subtypes.
//
// Each type encodes to the engine's field bag for its subtype, the JSON
// object `AnnotationCapability.create`, `update` and
// `AnnotationToolsCapability.updateDefaults` otherwise take as a string. The
// engine refuses a key its kind doesn't declare (`assert_declared_fields` in
// `crates/epdf-engine/src/features/annotation/write/prepare.rs` and
// `write/patch.rs`), so every key here is one the kind declares
// (`kind_fields` in `prepare.rs`), and each value has the shape that kind's
// `read_field` reads. Geometry is page space, as reads hand it out: points
// `{x, y}` and boxes `{x, y, width, height}` from the page's top-left; the
// plugin moves them into the file's coordinates (`convert` in
// `crates/plugins/annotation-core/src/engine/space.rs`).
//
// A member left `null` or `Nullable.Unchanged` is left out of the JSON: on a
// draft that is the engine's default, on a patch it keeps the current value.
// A member the engine can set to nothing is a `Nullable`, whose
// `Nullable.Clear` writes `null`.
//
// Every type carries its subtype's data fields (`kind_fields`) and the base
// fields: `contents` and `blendMode` as its own members, and the rest (`nm`,
// `subject`, the `/F` flags, `reply`, `groupId`) in `common`, an
// `AnnotationCommonFields` that writes flat beside them. A link and a stamp
// take no `blendMode`: the engine writes `/BM` while it draws an appearance,
// and it draws neither. `richText` and a dimension's `measure` write flat
// too (`AnnotationSharedMembers.kt`). The JSON-string overloads stay for the
// two subtypes the typed layer leaves out: popup and widget.

/**
 * A typed create draft: the engine's field bag for one subtype, with its
 * `subtype`. [AnnotationCapability.create] takes one.
 */
public interface AnnotationDraft {
    /**
     * The field bag as the engine reads it: keys sorted, so the same draft is
     * always the same bytes. Throws [PdfException] `invalid-input` for a
     * number JSON can't hold (NaN or an infinity).
     */
    public fun json(): String
}

/**
 * A typed patch: the members of one subtype to change.
 * [AnnotationCapability.update] and [AnnotationToolsCapability.updateDefaults]
 * take one, and [AnnotationToolsCapability.defaults] reads a tool's defaults
 * as one.
 *
 * A patch reads from the field bag it writes, one key list for both ways,
 * through its companion's [Reader.fromJson]. A key the patch doesn't declare
 * is ignored. A missing key leaves its member `null` or
 * [Nullable.Unchanged]. `null` reads as [Nullable.Clear] on a [Nullable]
 * member and as left out on any other. A declared key whose value the member
 * can't hold throws.
 *
 * One member doesn't come back as it went: no patch writes `common.nm`, so
 * it is [Nullable.Unchanged] after a write and a read. A field bag from
 * elsewhere can hold an `nm`, as a tool's defaults can, and a read patch's
 * `common.nm` is then that name.
 */
public interface AnnotationPatch {
    /**
     * The field bag as the engine reads it: keys sorted, unset members left
     * out. Throws [PdfException] `invalid-input` for a number JSON can't hold
     * (NaN or an infinity).
     */
    public fun json(): String

    /**
     * Reads a patch of one type from a field bag. Each patch's companion is
     * its reader: `InkAnnotationPatch.fromJson(json)`, or
     * `tools.defaults(ToolId.INK, InkAnnotationPatch)`.
     */
    public interface Reader<out P : AnnotationPatch> {
        /**
         * The patch [json] holds. Throws [PdfException] `invalid-input` when
         * it isn't a JSON object, or when a key the patch declares holds a
         * value of another shape.
         */
        public fun fromJson(json: String): P
    }
}

/**
 * The border styles the engine writes for an ink annotation and the stroked
 * kinds. It reads `beveled` and `inset` too, but refuses to write them on
 * anything but a widget (`read_field`, `"borderStyle"`, in
 * `crates/epdf-engine/src/features/annotation/write/prepare.rs`); a read
 * reports them as [AnnotationInfo.BorderStyle].
 */
@Serializable
public enum class AnnotationBorderStyle(public val value: String) {
    @SerialName("solid") SOLID("solid"),
    @SerialName("dashed") DASHED("dashed"),
}

/**
 * An ink annotation's `/IT`: the one intent the engine writes for ink
 * (`intents_of` in `prepare.rs`). An ink without one is pen ink.
 */
@Serializable
public enum class AnnotationInkIntent(public val value: String) {
    /** `ink-highlight`: a freehand highlighter. */
    @SerialName("ink-highlight") INK_HIGHLIGHT("ink-highlight"),
}

/**
 * A note's `/Name` icon, as the engine spells it (`NoteIcon` in
 * `crates/epdf-engine/src/features/annotation/vocabulary.rs`).
 */
@Serializable
public enum class AnnotationNoteIcon(public val value: String) {
    @SerialName("comment") COMMENT("comment"),
    @SerialName("key") KEY("key"),
    @SerialName("note") NOTE("note"),
    @SerialName("help") HELP("help"),
    @SerialName("new-paragraph") NEW_PARAGRAPH("new-paragraph"),
    @SerialName("paragraph") PARAGRAPH("paragraph"),
    @SerialName("insert") INSERT("insert"),
}

// ── ink ─────────────────────────────────────────────────────────────────────

/**
 * A new ink annotation: freehand strokes. Each member's rule is the engine's
 * (`read_field` in `prepare.rs`); a value outside it throws `invalid-input`
 * from the create.
 *
 * @property inkList `/InkList`, page space: one list of points per stroke. At
 *   least one stroke, and no stroke without points.
 * @property color `/C`. [Nullable.Clear] draws no stroke; left out takes the
 *   tool's or the engine's default.
 * @property opacity `/CA`, 0 to 1.
 * @property strokeWidth the line width, above 0.
 * @property dashArray the dash lengths, each 0 or more, at least one. A dashed
 *   border with this left out takes the engine's default pattern;
 *   [Nullable.Clear] is none.
 * @property intent [Nullable.Clear] is pen ink.
 * @property rotation degrees. [Nullable.Clear] is none.
 * @property contents `/Contents`. [Nullable.Clear] is none.
 * @property blendMode how the appearance blends with the page under it (`/BM`
 *   on `/AP /N`).
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a draft that gives both needs them to agree.
 *   [Nullable.Clear] is a plain comment.
 * @property common the fields every annotation shares.
 */
public data class InkAnnotationDraft(
    val inkList: List<List<Point>>,
    val color: Nullable<AnnotationColor> = Nullable.Unchanged,
    val opacity: Double? = null,
    val strokeWidth: Double? = null,
    val borderStyle: AnnotationBorderStyle? = null,
    val dashArray: Nullable<List<Double>> = Nullable.Unchanged,
    val intent: Nullable<AnnotationInkIntent> = Nullable.Unchanged,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationDraft {
    override fun json(): String = FieldBagWriter().run {
        put("subtype", AnnotationSubtype.INK.value)
        inkFields(inkList, color, opacity, strokeWidth, borderStyle, dashArray, intent, rotation, contents, blendMode)
        richText(richText)
        draftCommon(common)
    }
}

/**
 * A change to an ink annotation, or to an ink tool's defaults. A member left
 * out keeps its value; the rules are the draft's ([InkAnnotationDraft]).
 *
 * @property inkList replaces every stroke.
 * @property color [Nullable.Clear] removes the stroke colour: no stroke.
 * @property dashArray [Nullable.Clear] removes the pattern.
 * @property intent [Nullable.Clear] makes it pen ink.
 * @property rotation [Nullable.Clear] removes the rotation.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property blendMode how the appearance blends with the page under it (`/BM`
 *   on `/AP /N`).
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree.
 *   [Nullable.Clear] makes the comment plain and keeps `contents`.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class InkAnnotationPatch(
    val inkList: List<List<Point>>? = null,
    val color: Nullable<AnnotationColor> = Nullable.Unchanged,
    val opacity: Double? = null,
    val strokeWidth: Double? = null,
    val borderStyle: AnnotationBorderStyle? = null,
    val dashArray: Nullable<List<Double>> = Nullable.Unchanged,
    val intent: Nullable<AnnotationInkIntent> = Nullable.Unchanged,
    val rotation: Nullable<Double> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    override fun json(): String = FieldBagWriter().run {
        inkFields(inkList, color, opacity, strokeWidth, borderStyle, dashArray, intent, rotation, contents, blendMode)
        richText(richText)
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<InkAnnotationPatch> {
        override fun fromJson(json: String): InkAnnotationPatch = readPatch(json) {
            InkAnnotationPatch(
                inkList = optional("inkList", FieldValues::strokes),
                color = nullable("color", FieldValues::color),
                opacity = optional("opacity", FieldValues::double),
                strokeWidth = optional("strokeWidth", FieldValues::double),
                borderStyle = optional("borderStyle") { FieldValues.enum(it, AnnotationBorderStyle.entries) { style -> style.value } },
                dashArray = nullable("dashArray", FieldValues::doubles),
                intent = nullable("intent") { FieldValues.enum(it, AnnotationInkIntent.entries) { intent -> intent.value } },
                rotation = nullable("rotation", FieldValues::double),
                contents = nullable("contents", FieldValues::string),
                blendMode = blendMode(),
                richText = richText(),
                common = common(),
            )
        }
    }
}

private fun FieldBagWriter.inkFields(
    inkList: List<List<Point>>?,
    color: Nullable<AnnotationColor>,
    opacity: Double?,
    strokeWidth: Double?,
    borderStyle: AnnotationBorderStyle?,
    dashArray: Nullable<List<Double>>,
    intent: Nullable<AnnotationInkIntent>,
    rotation: Nullable<Double>,
    contents: Nullable<String>,
    blendMode: AnnotationBlendMode?,
) {
    put("inkList", inkList?.let(FieldValues::strokes))
    put("color", color, FieldValues::color)
    put("opacity", opacity)
    put("strokeWidth", strokeWidth)
    put("borderStyle", borderStyle?.value)
    put("dashArray", dashArray, FieldValues::doubles)
    put("intent", intent) { JsonPrimitive(it.value) }
    put("rotation", rotation, ::JsonPrimitive)
    put("contents", contents, ::JsonPrimitive)
    put("blendMode", blendMode?.value)
}

// ── highlight ───────────────────────────────────────────────────────────────

/**
 * A new highlight annotation over text quads.
 *
 * @property quadPoints `/QuadPoints`, page space: one quad per run of marked
 *   text, at least one.
 * @property color `/C`. A highlight always has one, so it takes no
 *   [Nullable.Clear].
 * @property opacity `/CA`, 0 to 1.
 * @property contents `/Contents`. [Nullable.Clear] is none.
 * @property blendMode how the appearance blends with the page under it (`/BM`
 *   on `/AP /N`).
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a draft that gives both needs them to agree.
 *   [Nullable.Clear] is a plain comment.
 * @property common the fields every annotation shares.
 */
public data class HighlightAnnotationDraft(
    val quadPoints: List<Quad>,
    val color: AnnotationColor? = null,
    val opacity: Double? = null,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationDraft {
    override fun json(): String = FieldBagWriter().run {
        put("subtype", AnnotationSubtype.HIGHLIGHT.value)
        textMarkupFields(quadPoints, color, opacity, contents, blendMode)
        richText(richText)
        draftCommon(common)
    }
}

/**
 * A change to a highlight annotation, or to a highlight tool's defaults. A
 * member left out keeps its value.
 *
 * @property quadPoints replaces every quad.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property blendMode how the appearance blends with the page under it (`/BM`
 *   on `/AP /N`).
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree.
 *   [Nullable.Clear] makes the comment plain and keeps `contents`.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class HighlightAnnotationPatch(
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

    public companion object : AnnotationPatch.Reader<HighlightAnnotationPatch> {
        override fun fromJson(json: String): HighlightAnnotationPatch = readPatch(json) {
            HighlightAnnotationPatch(
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

/** The members a highlight shares with the other text markups. */
internal fun FieldBagWriter.textMarkupFields(
    quadPoints: List<Quad>?,
    color: AnnotationColor?,
    opacity: Double?,
    contents: Nullable<String>,
    blendMode: AnnotationBlendMode?,
) {
    put("quadPoints", quadPoints?.let(FieldValues::quads))
    put("color", color?.let(FieldValues::color))
    put("opacity", opacity)
    put("contents", contents, ::JsonPrimitive)
    put("blendMode", blendMode?.value)
}

// ── note ────────────────────────────────────────────────────────────────────

/**
 * A new note: a `text` annotation, a sticky note.
 *
 * @property rect `/Rect`, page space: where the icon sits.
 * @property color `/C`. A note always has one, so it takes no
 *   [Nullable.Clear].
 * @property opacity `/CA`, 0 to 1.
 * @property open `/Open`: whether the note shows open.
 * @property state `/State`. A standard state (`accepted`, `marked`, …) brings
 *   its own `stateModel`; any other needs one. [Nullable.Clear] is none.
 * @property stateModel `/StateModel`: `review`, `marked`, or a custom model.
 *   [Nullable.Clear] is none.
 * @property contents `/Contents`: the note's text. [Nullable.Clear] is none.
 * @property blendMode how the appearance blends with the page under it (`/BM`
 *   on `/AP /N`).
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a draft that gives both needs them to agree.
 *   [Nullable.Clear] is a plain comment.
 * @property common the fields every annotation shares.
 */
public data class NoteAnnotationDraft(
    val rect: Rect,
    val color: AnnotationColor? = null,
    val opacity: Double? = null,
    val icon: AnnotationNoteIcon? = null,
    val open: Boolean? = null,
    val state: Nullable<String> = Nullable.Unchanged,
    val stateModel: Nullable<String> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationDraft {
    override fun json(): String = FieldBagWriter().run {
        put("subtype", AnnotationSubtype.TEXT.value)
        noteFields(rect, color, opacity, icon, open, state, stateModel, contents, blendMode)
        richText(richText)
        draftCommon(common)
    }
}

/**
 * A change to a note, or to the note tool's defaults. A member left out
 * keeps its value.
 *
 * @property rect moves or resizes the icon.
 * @property state a standard state sets its own `stateModel`; any other keeps
 *   the note's, and the note needs one. [Nullable.Clear] removes it.
 * @property stateModel [Nullable.Clear] removes it.
 * @property contents [Nullable.Clear] removes `/Contents`.
 * @property blendMode how the appearance blends with the page under it (`/BM`
 *   on `/AP /N`).
 * @property richText the comment as styled text (`/RC`). It sets `contents`
 *   to its plain text, and a patch that gives both needs them to agree.
 *   [Nullable.Clear] makes the comment plain and keeps `contents`.
 * @property common the fields every annotation shares. A patch leaves `nm`
 *   out.
 */
public data class NoteAnnotationPatch(
    val rect: Rect? = null,
    val color: AnnotationColor? = null,
    val opacity: Double? = null,
    val icon: AnnotationNoteIcon? = null,
    val open: Boolean? = null,
    val state: Nullable<String> = Nullable.Unchanged,
    val stateModel: Nullable<String> = Nullable.Unchanged,
    val contents: Nullable<String> = Nullable.Unchanged,
    val blendMode: AnnotationBlendMode? = null,
    val richText: Nullable<AnnotationRichText> = Nullable.Unchanged,
    val common: AnnotationCommonFields = AnnotationCommonFields(),
) : AnnotationPatch {
    override fun json(): String = FieldBagWriter().run {
        noteFields(rect, color, opacity, icon, open, state, stateModel, contents, blendMode)
        richText(richText)
        patchCommon(common)
    }

    public companion object : AnnotationPatch.Reader<NoteAnnotationPatch> {
        override fun fromJson(json: String): NoteAnnotationPatch = readPatch(json) {
            NoteAnnotationPatch(
                rect = optional("rect", FieldValues::rect),
                color = optional("color", FieldValues::color),
                opacity = optional("opacity", FieldValues::double),
                icon = optional("icon") { FieldValues.enum(it, AnnotationNoteIcon.entries) { icon -> icon.value } },
                open = optional("open", FieldValues::boolean),
                state = nullable("state", FieldValues::string),
                stateModel = nullable("stateModel", FieldValues::string),
                contents = nullable("contents", FieldValues::string),
                blendMode = blendMode(),
                richText = richText(),
                common = common(),
            )
        }
    }
}

private fun FieldBagWriter.noteFields(
    rect: Rect?,
    color: AnnotationColor?,
    opacity: Double?,
    icon: AnnotationNoteIcon?,
    open: Boolean?,
    state: Nullable<String>,
    stateModel: Nullable<String>,
    contents: Nullable<String>,
    blendMode: AnnotationBlendMode?,
) {
    put("rect", rect?.let(FieldValues::rect))
    put("color", color?.let(FieldValues::color))
    put("opacity", opacity)
    put("icon", icon?.value)
    put("open", open?.let(::JsonPrimitive))
    put("state", state, ::JsonPrimitive)
    put("stateModel", stateModel, ::JsonPrimitive)
    put("contents", contents, ::JsonPrimitive)
    put("blendMode", blendMode?.value)
}

// ── writing and reading ─────────────────────────────────────────────────────

/**
 * Builds a field bag. A member given `null` or [Nullable.Unchanged] is left
 * out; [Nullable.Clear] writes `null`. [run] a block on one and its last
 * expression, [json], is the bag.
 */
internal class FieldBagWriter {
    val members: MutableMap<String, JsonElement> = LinkedHashMap()

    fun put(key: String, value: JsonElement?) {
        if (value != null) members[key] = value
    }

    fun put(key: String, value: String?) = put(key, value?.let(::JsonPrimitive))

    fun put(key: String, value: Double?) = put(key, value?.let(::JsonPrimitive))

    fun <T> put(key: String, value: Nullable<T>, encode: (T) -> JsonElement) = value.writeTo(members, key, encode)

    /** A comment's rich text: left out when unchanged, `null` when cleared. */
    fun richText(value: Nullable<AnnotationRichText>) = put("richText", value, AnnotationRichTextSerializer::element)

    fun draftCommon(common: AnnotationCommonFields): String {
        common.writeTo(members)
        return json()
    }

    fun patchCommon(common: AnnotationCommonFields): String {
        common.writeTo(members, asPatch = true)
        return json()
    }

    fun json(): String = AnnotationFieldBag.json(JsonObject(members))
}

/**
 * Reads a patch's members out of a field bag. A missing key reads as `null`
 * or [Nullable.Unchanged]; `null` reads as `null` on a plain member and as
 * [Nullable.Clear] on a [Nullable] one. A value of another shape throws
 * [SerializationException].
 */
internal class FieldBagReader(val members: JsonObject) {
    inline fun <T> optional(key: String, decode: (JsonElement) -> T): T? {
        val value = members[key] ?: return null
        return if (value is JsonNull) null else decode(value)
    }

    inline fun <T> nullable(key: String, decode: (JsonElement) -> T): Nullable<T> = members.nullable(key, decode)

    inline fun <T> required(key: String, decode: (JsonElement) -> T): T =
        optional(key, decode) ?: throw SerializationException("no member $key")

    fun has(key: String): Boolean = key in members

    fun isNull(key: String): Boolean = members[key] is JsonNull

    fun blendMode(): AnnotationBlendMode? = optional("blendMode") { FieldValues.enum(it, AnnotationBlendMode.entries) { mode -> mode.value } }

    fun richText(): Nullable<AnnotationRichText> = nullable("richText", AnnotationRichTextSerializer::read)

    fun common(): AnnotationCommonFields = AnnotationCommonFields.read(members)
}

/**
 * The patch [json] holds, read by [read]. Throws `invalid-input` for JSON that
 * isn't an object, or for a declared member of another shape.
 */
internal inline fun <P> readPatch(json: String, read: FieldBagReader.() -> P): P = try {
    FieldBagReader(AnnotationWire.json.parseToJsonElement(json).asObject("a patch")).read()
} catch (e: IllegalArgumentException) {
    // SerializationException is one, and so is a malformed JSON text.
    throw PdfException(
        PdfException.Code.INVALID_INPUT,
        "the field bag doesn't read as this patch: ${e.message}",
        "DecodingError",
        e,
    )
}

/**
 * The values field bags hold, as the engine spells them: page-space points
 * `{x, y}`, boxes `{x, y, width, height}` and quads `upperLeft`…`lowerRight`
 * (`NamedPdfQuad` in `crates/epdf-engine/src/geom.rs`), colours `"#rrggbb"`,
 * and each vocabulary by its wire string. The readers throw
 * [SerializationException] for a value of another shape.
 */
internal object FieldValues {
    fun point(point: Point): JsonObject = JsonObject(linkedMapOf("x" to JsonPrimitive(point.x), "y" to JsonPrimitive(point.y)))

    fun points(points: List<Point>): JsonArray = JsonArray(points.map(::point))

    fun strokes(strokes: List<List<Point>>): JsonArray = JsonArray(strokes.map(::points))

    fun rect(rect: Rect): JsonObject = JsonObject(
        linkedMapOf(
            "x" to JsonPrimitive(rect.x), "y" to JsonPrimitive(rect.y),
            "width" to JsonPrimitive(rect.width), "height" to JsonPrimitive(rect.height),
        ),
    )

    fun quad(quad: Quad): JsonObject = JsonObject(
        linkedMapOf(
            "upperLeft" to point(quad.upperLeft), "upperRight" to point(quad.upperRight),
            "lowerLeft" to point(quad.lowerLeft), "lowerRight" to point(quad.lowerRight),
        ),
    )

    fun quads(quads: List<Quad>): JsonArray = JsonArray(quads.map(::quad))

    fun doubles(values: List<Double>): JsonArray = JsonArray(values.map(::JsonPrimitive))

    fun color(color: AnnotationColor): JsonPrimitive = JsonPrimitive(color.hex)

    // ── reading ──

    /** A finite number; one past a double's range (`1e400`) throws `invalid-input`. */
    fun double(element: JsonElement): Double {
        val value = element as? JsonPrimitive
        if (value == null || value.isString) throw SerializationException("$element is not a number")
        val number = value.doubleOrNull ?: throw SerializationException("$element is not a number")
        if (!number.isFinite()) {
            throw PdfException(PdfException.Code.INVALID_INPUT, "$element is not a finite number", "DecodingError")
        }
        return number
    }

    fun string(element: JsonElement): String {
        val value = element as? JsonPrimitive
        if (value == null || !value.isString) throw SerializationException("$element is not a string")
        return value.content
    }

    fun boolean(element: JsonElement): Boolean {
        val value = element as? JsonPrimitive
        if (value == null || value.isString) throw SerializationException("$element is not a boolean")
        return value.booleanOrNull ?: throw SerializationException("$element is not a boolean")
    }

    fun color(element: JsonElement): AnnotationColor {
        val text = string(element)
        return AnnotationColor.fromHex(text) ?: throw SerializationException("\"$text\" isn't a colour as \"#rrggbb\"")
    }

    inline fun <E> enum(element: JsonElement, entries: List<E>, wire: (E) -> String): E {
        val text = string(element)
        return entries.firstOrNull { wire(it) == text } ?: throw SerializationException("not a value this member takes: $text")
    }

    fun array(element: JsonElement): JsonArray = element as? JsonArray ?: throw SerializationException("$element is not an array")

    fun doubles(element: JsonElement): List<Double> = array(element).map(::double)

    fun point(element: JsonElement): Point {
        val point = element.asObject("point")
        return Point(double(point.field("x")), double(point.field("y")))
    }

    fun points(element: JsonElement): List<Point> = array(element).map(::point)

    fun strokes(element: JsonElement): List<List<Point>> = array(element).map(::points)

    fun rect(element: JsonElement): Rect {
        val rect = element.asObject("box")
        return Rect(double(rect.field("x")), double(rect.field("y")), double(rect.field("width")), double(rect.field("height")))
    }

    fun quad(element: JsonElement): Quad {
        val quad = element.asObject("quad")
        return Quad(point(quad.field("upperLeft")), point(quad.field("upperRight")), point(quad.field("lowerLeft")), point(quad.field("lowerRight")))
    }

    fun quads(element: JsonElement): List<Quad> = array(element).map(::quad)
}
