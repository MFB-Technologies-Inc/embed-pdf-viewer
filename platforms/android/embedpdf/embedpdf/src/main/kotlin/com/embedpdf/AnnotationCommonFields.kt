package com.embedpdf

import kotlinx.serialization.KSerializer
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.SerializationException
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.encoding.Decoder
import kotlinx.serialization.encoding.Encoder
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull

// The data fields every annotation kind declares besides `contents` and
// `blendMode` (`BASE_FIELDS` in
// `crates/epdf-engine/src/features/annotation/write/prepare.rs`), as every
// typed draft and patch carries them in `common`. They write flat, beside the
// kind's own members, through `AnnotationCommonFields.writeTo`, whose `KEYS`
// is the one list of their names.

/**
 * How an annotation relates to the one it answers: `/RT`
 * (`AnnotationReplyType` in
 * `crates/epdf-engine/src/features/annotation/vocabulary.rs`).
 */
@Serializable
public enum class AnnotationReplyType(public val value: String) {
    /** A reply in the other annotation's thread. */
    @SerialName("reply") REPLY("reply"),

    /** Grouped with the other annotation: the two are treated as one. */
    @SerialName("group") GROUP("group"),
}

/**
 * `/IRT` and `/RT`: the annotation another one answers, and how. The engine
 * links only annotations of one page, and refuses a [to] that names another
 * page's.
 *
 * @property to the annotation answered.
 * @property type left `null`, the engine writes `reply`.
 */
@Serializable(with = AnnotationReplySerializer::class)
public data class AnnotationReply(val to: AnnotationRef, val type: AnnotationReplyType? = null)

internal object AnnotationReplySerializer : KSerializer<AnnotationReply> {
    override val descriptor: SerialDescriptor = JsonElement.serializer().descriptor

    override fun serialize(encoder: Encoder, value: AnnotationReply) {
        encoder.json().encodeJsonElement(element(value))
    }

    override fun deserialize(decoder: Decoder): AnnotationReply = read(decoder.json().decodeJsonElement())

    fun element(value: AnnotationReply): JsonObject = JsonObject(
        buildMap {
            put("to", AnnotationWire.annotation(value.to))
            value.type?.let { put("type", JsonPrimitive(it.value)) }
        },
    )

    fun read(element: JsonElement): AnnotationReply {
        val reply = element.asObject("reply")
        val type = reply["type"]?.takeIf { it !is JsonNull }?.let { type ->
            val text = (type as? JsonPrimitive)?.takeIf { it.isString }?.content
            AnnotationReplyType.entries.firstOrNull { it.value == text }
                ?: throw SerializationException("not a reply type: $type")
        }
        return AnnotationReply(AnnotationWire.annotation(reply.field("to")), type)
    }
}

/**
 * The fields every annotation shares, whatever its subtype. Every typed draft
 * and patch has one as `common`, and writes its members beside the subtype's
 * own.
 *
 * A member left [Nullable.Unchanged] or `null` is left out: on a draft that is
 * the engine's default, on a patch it keeps the current value. The engine's
 * defaults for a new annotation are a fresh `nm`, `print` on and every other
 * flag off.
 *
 * A patch given to [AnnotationToolsCapability.updateDefaults] stores these
 * with the tool's other defaults. A [reply] or [groupId] stored that way is
 * the default for an annotation created with that tool: it becomes a reply to
 * that annotation or a member of that group unless its draft sets its own
 * value. A create without `tool` takes no defaults.
 *
 * @property nm `/NM`: the annotation's name, unique on its page. A draft left
 *   without one gets a fresh name; [Nullable.Clear] or an empty string asks
 *   for none. The create throws `invalid-input` for a name the page already
 *   uses. Only a draft writes it: the engine refuses to change a name after
 *   create, so a patch leaves it out of its JSON.
 * @property subject `/Subj`. [Nullable.Clear] is none.
 * @property reply `/IRT` and `/RT`: the annotation this one answers.
 *   [Nullable.Clear] answers none.
 * @property groupId the group the annotation belongs to, at least one
 *   character. [Nullable.Clear] is none.
 * @property invisible `/F` bit 1: not shown when the viewer has no handler for
 *   the subtype.
 * @property hidden `/F` bit 2: neither shown nor printed.
 * @property print `/F` bit 3: printed with the page. On for a new annotation.
 * @property noZoom `/F` bit 4: the appearance keeps its size as the page zooms.
 * @property noRotate `/F` bit 5: the appearance stays upright as the page
 *   rotates.
 * @property noView `/F` bit 6: not shown on screen, though it may print.
 * @property readOnly `/F` bit 7: the user can't interact with it.
 * @property locked `/F` bit 8: it can't be moved, resized or deleted.
 * @property toggleNoView `/F` bit 9: [noView] is inverted for some events.
 * @property lockedContents `/F` bit 10: its contents can't be changed.
 */
public data class AnnotationCommonFields(
    val nm: Nullable<String> = Nullable.Unchanged,
    val subject: Nullable<String> = Nullable.Unchanged,
    val reply: Nullable<AnnotationReply> = Nullable.Unchanged,
    val groupId: Nullable<String> = Nullable.Unchanged,
    val invisible: Boolean? = null,
    val hidden: Boolean? = null,
    val print: Boolean? = null,
    val noZoom: Boolean? = null,
    val noRotate: Boolean? = null,
    val noView: Boolean? = null,
    val readOnly: Boolean? = null,
    val locked: Boolean? = null,
    val toggleNoView: Boolean? = null,
    val lockedContents: Boolean? = null,
) {
    /**
     * Writes the members that are set into [members], beside whatever else it
     * holds: what a draft writes. A patch ([asPatch]) leaves `nm` out, which
     * the engine refuses to change after create.
     */
    internal fun writeTo(members: MutableMap<String, JsonElement>, asPatch: Boolean = false) {
        if (!asPatch) nm.writeTo(members, "nm") { JsonPrimitive(it) }
        subject.writeTo(members, "subject") { JsonPrimitive(it) }
        reply.writeTo(members, "reply") { AnnotationReplySerializer.element(it) }
        groupId.writeTo(members, "groupId") { JsonPrimitive(it) }
        for ((key, flag) in flags()) flag?.let { members[key] = JsonPrimitive(it) }
    }

    private fun flags(): List<Pair<String, Boolean?>> = listOf(
        "invisible" to invisible, "hidden" to hidden, "print" to print, "noZoom" to noZoom,
        "noRotate" to noRotate, "noView" to noView, "readOnly" to readOnly, "locked" to locked,
        "toggleNoView" to toggleNoView, "lockedContents" to lockedContents,
    )

    internal companion object {
        /** The engine's names, one per member: the list a write writes and a read reads. */
        val KEYS: List<String> = listOf(
            "nm", "subject", "reply", "groupId", "invisible", "hidden", "print", "noZoom", "noRotate",
            "noView", "readOnly", "locked", "toggleNoView", "lockedContents",
        )

        /**
         * Reads the members from [members], beside whatever else it holds. A
         * member that is `null` reads as [Nullable.Clear], a missing one as
         * [Nullable.Unchanged]. Throws [SerializationException] for a member
         * of another shape.
         */
        fun read(members: JsonObject): AnnotationCommonFields {
            fun text(key: String): Nullable<String> = members.nullable(key) { value ->
                (value as? JsonPrimitive)?.takeIf { it.isString }?.content
                    ?: throw SerializationException("$key is not a string")
            }
            fun flag(key: String): Boolean? = members[key]?.let { value ->
                (value as? JsonPrimitive)?.takeIf { !it.isString }?.booleanOrNull
                    ?: throw SerializationException("$key is not a boolean")
            }
            return AnnotationCommonFields(
                nm = text("nm"),
                subject = text("subject"),
                reply = members.nullable("reply") { AnnotationReplySerializer.read(it) },
                groupId = text("groupId"),
                invisible = flag("invisible"),
                hidden = flag("hidden"),
                print = flag("print"),
                noZoom = flag("noZoom"),
                noRotate = flag("noRotate"),
                noView = flag("noView"),
                readOnly = flag("readOnly"),
                locked = flag("locked"),
                toggleNoView = flag("toggleNoView"),
                lockedContents = flag("lockedContents"),
            )
        }
    }
}

/** Writes this member under [key]: nothing when unchanged, `null` when cleared. */
internal inline fun <T> Nullable<T>.writeTo(
    members: MutableMap<String, JsonElement>,
    key: String,
    encode: (T) -> JsonElement,
) {
    when (this) {
        Nullable.Unchanged -> Unit
        Nullable.Clear -> members[key] = JsonNull
        is Nullable.Value -> members[key] = encode(value)
    }
}

/** A [Nullable] member: unchanged when [key] is missing, cleared when it holds `null`. */
internal inline fun <T> JsonObject.nullable(key: String, decode: (JsonElement) -> T): Nullable<T> {
    val value = this[key] ?: return Nullable.Unchanged
    return if (value is JsonNull) Nullable.Clear else Nullable.Value(decode(value))
}
