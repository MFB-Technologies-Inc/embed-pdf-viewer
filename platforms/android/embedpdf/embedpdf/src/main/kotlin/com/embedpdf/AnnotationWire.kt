package com.embedpdf

import kotlinx.serialization.KSerializer
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.SerializationException
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.encoding.Decoder
import kotlinx.serialization.encoding.Encoder
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonDecoder
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonEncoder
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.doubleOrNull
import kotlinx.serialization.json.longOrNull

// The values the annotation field bags carry, and the JSON they cross as.
// The typed drafts and patches build on these (`AnnotationDraft.swift` holds
// the Swift ones).

/**
 * A member a patch can leave alone, set, or clear: [Unchanged] writes
 * nothing, which on a draft is the engine's default and on a patch keeps the
 * current value; [Value] writes the value; [Clear] writes `null`
 * (`docs/conventions/naming.md`, "Nothing: left out or null"). Swift spells
 * this `Nullable<Value>?`.
 */
public sealed interface Nullable<out T> {
    /** Left out: the engine's default on a draft, the current value on a patch. */
    public data object Unchanged : Nullable<Nothing>

    public data class Value<out T>(val value: T) : Nullable<T>

    /** Nothing: no stroke colour, no dash pattern, no rotation. Writes `null`. */
    public data object Clear : Nullable<Nothing>
}

/**
 * `/BM` on an annotation's appearance: how it blends with the page under it.
 * Every subtype takes one (`BASE_FIELDS` in `prepare.rs`); the spellings are
 * `BlendMode` in `crates/epdf-engine/src/features/annotation/vocabulary.rs`.
 */
@Serializable
public enum class AnnotationBlendMode(public val value: String) {
    @SerialName("normal") NORMAL("normal"),
    @SerialName("multiply") MULTIPLY("multiply"),
    @SerialName("screen") SCREEN("screen"),
    @SerialName("overlay") OVERLAY("overlay"),
    @SerialName("darken") DARKEN("darken"),
    @SerialName("lighten") LIGHTEN("lighten"),
    @SerialName("color-dodge") COLOR_DODGE("color-dodge"),
    @SerialName("color-burn") COLOR_BURN("color-burn"),
    @SerialName("hard-light") HARD_LIGHT("hard-light"),
    @SerialName("soft-light") SOFT_LIGHT("soft-light"),
    @SerialName("difference") DIFFERENCE("difference"),
    @SerialName("exclusion") EXCLUSION("exclusion"),
    @SerialName("hue") HUE("hue"),
    @SerialName("saturation") SATURATION("saturation"),
    @SerialName("color") COLOR("color"),
    @SerialName("luminosity") LUMINOSITY("luminosity"),
    ;

    internal companion object {
        fun of(value: String): AnnotationBlendMode? = entries.firstOrNull { it.value == value }
    }
}

/** The page verbs a link may name (`read_target` in `prepare.rs`). */
@Serializable
public enum class AnnotationLinkVerb(public val value: String) {
    @SerialName("NextPage") NEXT_PAGE("NextPage"),
    @SerialName("PrevPage") PREVIOUS_PAGE("PrevPage"),
    @SerialName("FirstPage") FIRST_PAGE("FirstPage"),
    @SerialName("LastPage") LAST_PAGE("LastPage"),
}

/**
 * Where a link goes, as a link draft or patch writes it: the targets the
 * engine writes (`LinkTargetWrite` in `prepare.rs`). A link read from a file
 * can point elsewhere too (another file, a launch, a script); read those as
 * JSON ([AnnotationThreadsCapability.link], [AnnotationInfo.fieldsJson]).
 *
 * It encodes and decodes as the plugins' `LinkTarget` record. Decoding throws
 * for a target outside these three, and for a `named` verb outside
 * [AnnotationLinkVerb].
 */
@Serializable(with = AnnotationLinkTargetSerializer::class)
public sealed interface AnnotationLinkTarget {
    /**
     * A place in this document, in page space: measured from the top-left of
     * the visible box of the page it goes to, y down.
     */
    public data class Goto(val destination: PageDestination) : AnnotationLinkTarget

    public data class Uri(val uri: String) : AnnotationLinkTarget

    public data class Named(val verb: AnnotationLinkVerb) : AnnotationLinkTarget

    public companion object {
        /**
         * The target in a `LinkTarget` record the engine returned. Throws
         * `operation-failed` for a record this type can't hold, so a target
         * is never read as a different one.
         */
        internal fun fromJson(json: String): AnnotationLinkTarget = try {
            AnnotationWire.json.decodeFromString(AnnotationLinkTargetSerializer, json)
        } catch (e: IllegalArgumentException) {
            // SerializationException is one.
            throw PdfException(
                PdfException.Code.OPERATION_FAILED,
                "the link target doesn't read as an AnnotationLinkTarget: ${e.message}",
                "DecodingError",
                e,
            )
        }
    }
}

/** The `LinkTarget` record the JSON entry points take, keys sorted. */
internal fun AnnotationLinkTarget.json(): String = AnnotationFieldBag.json(AnnotationWire.linkTarget(this))

internal object AnnotationLinkTargetSerializer : KSerializer<AnnotationLinkTarget> {
    override val descriptor: SerialDescriptor = JsonElement.serializer().descriptor

    override fun serialize(encoder: Encoder, value: AnnotationLinkTarget) {
        encoder.json().encodeJsonElement(AnnotationWire.linkTarget(value))
    }

    override fun deserialize(decoder: Decoder): AnnotationLinkTarget {
        val target = decoder.json().decodeJsonElement().asObject("link target")
        return when (val kind = target.string("kind")) {
            "goto" -> AnnotationLinkTarget.Goto(AnnotationWire.destination(target.field("destination")))
            "uri" -> AnnotationLinkTarget.Uri(target.string("uri"))
            "named" -> {
                val name = target.string("name")
                AnnotationLinkTarget.Named(
                    AnnotationLinkVerb.entries.firstOrNull { it.value == name }
                        ?: throw SerializationException("not a link verb: $name"),
                )
            }
            else -> throw SerializationException("not a link target a draft writes: $kind")
        }
    }
}

/**
 * The JSON spellings of the engine's identities and geometry, the way the
 * plugins' field bags carry them.
 */
internal object AnnotationWire {
    val json: kotlinx.serialization.json.Json = kotlinx.serialization.json.Json

    /** `{"kind": "objectNumber", "objectNumber": …}` (`page_ref` in `crates/epdf-engine/src/identity.rs`). */
    fun page(page: PageRef): JsonObject = JsonObject(
        linkedMapOf("kind" to JsonPrimitive("objectNumber"), "objectNumber" to JsonPrimitive(page.pageObjectNumber.toLong())),
    )

    fun page(element: JsonElement): PageRef {
        val page = element.asObject("page")
        val kind = page.string("kind")
        if (kind != "objectNumber") throw SerializationException("not a page ref kind: $kind")
        return PageRef(page.unsigned("objectNumber"))
    }

    /** The engine's `AnnotationRef` record, in the member order Swift writes. */
    fun annotation(annotation: AnnotationRef): JsonObject = when (annotation) {
        is AnnotationRefObjectNumber -> JsonObject(
            linkedMapOf(
                "kind" to JsonPrimitive("objectNumber"),
                "page" to page(annotation.page),
                "objectNumber" to JsonPrimitive(annotation.annotObjectNumber.toLong()),
            ),
        )
        is AnnotationRefIndex -> JsonObject(
            linkedMapOf(
                "kind" to JsonPrimitive("baseIndex"),
                "page" to page(annotation.page),
                "baseIndex" to JsonPrimitive(annotation.index.toLong()),
            ),
        )
    }

    fun annotation(element: JsonElement): AnnotationRef {
        val ref = element.asObject("annotation ref")
        val page = page(ref.field("page"))
        return when (val kind = ref.string("kind")) {
            "objectNumber" -> AnnotationRefObjectNumber(page, ref.unsigned("objectNumber"))
            "baseIndex" -> AnnotationRefIndex(page, ref.unsigned("baseIndex"))
            else -> throw SerializationException("not an annotation ref kind: $kind")
        }
    }

    /**
     * A page-space destination as the plugins' JSON spells it (`PageDestination`
     * in `crates/epdf-engine/src/page_space/destinations.rs`): tagged by `kind`.
     * An optional coordinate that is absent is written as `null`, and reads
     * back from `null` or a missing member: the PDF's "keep the current value".
     */
    fun destination(destination: PageDestination): JsonObject {
        val members = linkedMapOf<String, JsonElement>()
        fun place(kind: String, at: PageRef) {
            members["kind"] = JsonPrimitive(kind)
            members["page"] = page(at)
        }
        fun coordinate(key: String, value: Double?) {
            members[key] = value?.let(::JsonPrimitive) ?: JsonNull
        }
        when (destination) {
            is PageDestinationXyz -> {
                place("xyz", destination.page)
                coordinate("x", destination.x)
                coordinate("y", destination.y)
                coordinate("zoom", destination.zoom)
            }
            is PageDestinationFit -> place("fit", destination.page)
            is PageDestinationFitH -> {
                place("fitH", destination.page)
                coordinate("y", destination.y)
            }
            is PageDestinationFitV -> {
                place("fitV", destination.page)
                coordinate("x", destination.x)
            }
            is PageDestinationFitR -> {
                place("fitR", destination.page)
                coordinate("x", destination.x)
                coordinate("y", destination.y)
                coordinate("width", destination.width)
                coordinate("height", destination.height)
            }
            is PageDestinationFitB -> place("fitB", destination.page)
            is PageDestinationFitBh -> {
                place("fitBH", destination.page)
                coordinate("y", destination.y)
            }
            is PageDestinationFitBv -> {
                place("fitBV", destination.page)
                coordinate("x", destination.x)
            }
        }
        return JsonObject(members)
    }

    fun destination(element: JsonElement): PageDestination {
        val members = element.asObject("destination")
        val page = page(members.field("page"))
        fun coordinate(key: String): Double? = members.optionalDouble(key)
        fun required(key: String): Double = coordinate(key) ?: throw SerializationException("destination has no $key")
        return when (val kind = members.string("kind")) {
            "xyz" -> PageDestinationXyz(page, coordinate("x"), coordinate("y"), coordinate("zoom"))
            "fit" -> PageDestinationFit(page)
            "fitH" -> PageDestinationFitH(page, coordinate("y"))
            "fitV" -> PageDestinationFitV(page, coordinate("x"))
            "fitR" -> PageDestinationFitR(page, required("x"), required("y"), required("width"), required("height"))
            "fitB" -> PageDestinationFitB(page)
            "fitBH" -> PageDestinationFitBh(page, coordinate("y"))
            "fitBV" -> PageDestinationFitBv(page, coordinate("x"))
            else -> throw SerializationException("not a destination kind: $kind")
        }
    }

    fun linkTarget(target: AnnotationLinkTarget): JsonObject = when (target) {
        is AnnotationLinkTarget.Goto -> JsonObject(
            linkedMapOf("kind" to JsonPrimitive("goto"), "destination" to destination(target.destination)),
        )
        is AnnotationLinkTarget.Uri -> JsonObject(linkedMapOf("kind" to JsonPrimitive("uri"), "uri" to JsonPrimitive(target.uri)))
        is AnnotationLinkTarget.Named -> JsonObject(
            linkedMapOf("kind" to JsonPrimitive("named"), "name" to JsonPrimitive(target.verb.value)),
        )
    }
}

/**
 * Field bags as the engine reads them: keys sorted as Foundation's
 * `.sortedKeys` sorts them ([KEY_ORDER]), so the same value is always the
 * same bytes, and numbers spelled as Swift's `JSONEncoder` spells
 * them (`72`, not `72.0`), so a Kotlin bag and a Swift bag compare byte for
 * byte. Throws `invalid-input` for a number JSON can't hold (NaN or an
 * infinity).
 */
internal object AnnotationFieldBag {
    fun json(element: JsonElement): String = StringBuilder().also { write(element, it) }.toString()

    private fun write(element: JsonElement, out: StringBuilder) {
        when (element) {
            is JsonNull -> out.append("null")
            is JsonObject -> {
                out.append('{')
                element.keys.sortedWith(KEY_ORDER).forEachIndexed { index, key ->
                    if (index > 0) out.append(',')
                    out.append(JsonPrimitive(key).toString()).append(':')
                    write(element.getValue(key), out)
                }
                out.append('}')
            }
            is JsonArray -> {
                out.append('[')
                element.forEachIndexed { index, value ->
                    if (index > 0) out.append(',')
                    write(value, out)
                }
                out.append(']')
            }
            is JsonPrimitive -> out.append(primitive(element))
        }
    }

    private fun primitive(value: JsonPrimitive): String {
        if (value.isString || value.booleanOrNull != null) return value.toString()
        value.longOrNull?.let { return it.toString() }
        val number = value.doubleOrNull ?: notFinite(value.content)
        if (!number.isFinite()) notFinite(value.content)
        return number(number)
    }

    /**
     * Foundation's `.sortedKeys` order: case-insensitive, runs of digits
     * compared by value, and ties broken by the keys' own characters
     * (`String.CompareOptions` `.caseInsensitive`, `.numeric` and
     * `.forcedOrdering`).
     */
    val KEY_ORDER: Comparator<String> = Comparator { left, right ->
        var i = 0
        var j = 0
        while (i < left.length && j < right.length) {
            if (left[i].isAsciiDigit() && right[j].isAsciiDigit()) {
                val leftEnd = digitsEnd(left, i)
                val rightEnd = digitsEnd(right, j)
                val leftRun = left.substring(i, leftEnd).trimStart('0')
                val rightRun = right.substring(j, rightEnd).trimStart('0')
                val order = if (leftRun.length != rightRun.length) leftRun.length - rightRun.length else leftRun.compareTo(rightRun)
                if (order != 0) return@Comparator order
                i = leftEnd
                j = rightEnd
            } else {
                val order = left[i].lowercaseChar().compareTo(right[j].lowercaseChar())
                if (order != 0) return@Comparator order
                i += 1
                j += 1
            }
        }
        val order = (left.length - i).compareTo(right.length - j)
        if (order != 0) order else left.compareTo(right)
    }

    private fun Char.isAsciiDigit(): Boolean = this in '0'..'9'

    private fun digitsEnd(text: String, start: Int): Int {
        var end = start
        while (end < text.length && text[end].isAsciiDigit()) end += 1
        return end
    }

    /**
     * A finite double as Swift's `JSONEncoder` writes it: `Double.description`
     * with a trailing `.0` dropped. That is the shortest digits that read
     * back as the same double, in plain notation for a decimal exponent from
     * -4 up to 15 (`0.0005`, `12345678.5`, `72`) and otherwise as `1e-07` or
     * `1e+16`: lowercase `e`, a sign and at least two exponent digits.
     * Negative zero is `-0`.
     */
    fun number(number: Double): String {
        if (number == 0.0) return if (1.0 / number < 0) "-0" else "0"
        // `Double.toString` gives the shortest round-trip digits; only its
        // notation differs from Swift's.
        val text = Math.abs(number).toString()
        val mantissa = text.substringBefore('E')
        val exponent = text.substringAfter('E', "0").toInt()
        val whole = mantissa.substringBefore('.')
        var digits = whole + mantissa.substringAfter('.', "")
        // Where the decimal point falls, counted in `digits`.
        var point = whole.length + exponent
        val leadingZeros = digits.indexOfFirst { it != '0' }
        digits = digits.substring(leadingZeros).trimEnd('0')
        point -= leadingZeros
        val decimalExponent = point - 1
        val body = when {
            decimalExponent !in -4 until 16 -> {
                val significand = if (digits.length == 1) digits else digits[0] + "." + digits.substring(1)
                val sign = if (decimalExponent < 0) '-' else '+'
                significand + "e" + sign + Math.abs(decimalExponent).toString().padStart(2, '0')
            }
            point <= 0 -> "0." + "0".repeat(-point) + digits
            point >= digits.length -> digits + "0".repeat(point - digits.length)
            else -> digits.substring(0, point) + "." + digits.substring(point)
        }
        return if (number < 0) "-$body" else body
    }

    private fun notFinite(content: String): Nothing = throw PdfException(
        PdfException.Code.INVALID_INPUT,
        "the annotation fields don't encode as JSON: $content is not a JSON number",
        "EncodingError",
    )
}

// ── reading JSON elements ──────────────────────────────────────────────────

internal fun Encoder.json(): JsonEncoder =
    this as? JsonEncoder ?: throw SerializationException("this type encodes only as JSON")

internal fun Decoder.json(): JsonDecoder =
    this as? JsonDecoder ?: throw SerializationException("this type decodes only from JSON")

internal fun JsonElement.asObject(what: String): JsonObject =
    this as? JsonObject ?: throw SerializationException("$what is not a JSON object")

internal fun JsonObject.field(key: String): JsonElement = this[key] ?: throw SerializationException("no member $key")

internal fun JsonObject.string(key: String): String {
    val value = field(key) as? JsonPrimitive
    if (value == null || !value.isString) throw SerializationException("$key is not a string")
    return value.content
}

internal fun JsonObject.unsigned(key: String): UInt {
    val value = (field(key) as? JsonPrimitive)?.takeIf { !it.isString }?.longOrNull
    if (value == null || value < 0 || value > UInt.MAX_VALUE.toLong()) throw SerializationException("$key is not a UInt32")
    return value.toUInt()
}

/** A number, or `null` when the key is missing or `null`. */
internal fun JsonObject.optionalDouble(key: String): Double? {
    val value = this[key] ?: return null
    if (value is JsonNull) return null
    val primitive = value as? JsonPrimitive
    if (primitive == null || primitive.isString) throw SerializationException("$key is not a number")
    return primitive.doubleOrNull ?: throw SerializationException("$key is not a number")
}
