package com.embedpdf

import kotlinx.serialization.KSerializer
import kotlinx.serialization.Serializable
import kotlinx.serialization.SerializationException
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.encoding.Decoder
import kotlinx.serialization.encoding.Encoder
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonPrimitive

/**
 * Where [AnnotationCapability.reorder] puts annotations among their page's
 * annotations. A page's annotations paint in list order, later over earlier,
 * so [End] is on top and [Start] is underneath. A position names a neighbour
 * rather than an index, so it keeps its meaning while other annotations come
 * and go. These four are the engine's whole vocabulary (`ListPosition` in
 * `crates/epdf-engine/src/mutation/list_position.rs`).
 *
 * It encodes and decodes as the engine's `AnnotationPosition` JSON, [json].
 */
@Serializable(with = AnnotationPositionSerializer::class)
public sealed interface AnnotationPosition {
    /**
     * The engine's `AnnotationPosition` JSON: `"start"`, `"end"`,
     * `{"before":ref}` or `{"after":ref}`, where `ref` is the engine's
     * annotation ref (`{"kind":"objectNumber","page":…,"objectNumber":…}`).
     */
    public val json: String

    /** The start of the page's list: under every other annotation. */
    public data object Start : AnnotationPosition {
        override val json: String get() = "\"start\""
    }

    /** The end of the page's list: over every other annotation. */
    public data object End : AnnotationPosition {
        override val json: String get() = "\"end\""
    }

    /** Right before [annotation], so painted just under it. */
    public data class Before(val annotation: AnnotationRef) : AnnotationPosition {
        override val json: String get() = "{\"before\":${AnnotationWire.annotation(annotation)}}"
    }

    /** Right after [annotation], so painted just over it. */
    public data class After(val annotation: AnnotationRef) : AnnotationPosition {
        override val json: String get() = "{\"after\":${AnnotationWire.annotation(annotation)}}"
    }
}

internal object AnnotationPositionSerializer : KSerializer<AnnotationPosition> {
    override val descriptor: SerialDescriptor = JsonElement.serializer().descriptor

    override fun serialize(encoder: Encoder, value: AnnotationPosition) {
        encoder.json().encodeJsonElement(AnnotationWire.json.parseToJsonElement(value.json))
    }

    override fun deserialize(decoder: Decoder): AnnotationPosition {
        val element = decoder.json().decodeJsonElement()
        if (element is JsonPrimitive && element.isString) {
            return when (element.content) {
                "start" -> AnnotationPosition.Start
                "end" -> AnnotationPosition.End
                else -> throw SerializationException("not a position: ${element.content}")
            }
        }
        val neighbour = element.asObject("position")
        return when {
            "before" in neighbour -> AnnotationPosition.Before(AnnotationWire.annotation(neighbour.getValue("before")))
            "after" in neighbour -> AnnotationPosition.After(AnnotationWire.annotation(neighbour.getValue("after")))
            else -> throw SerializationException("not a position: $neighbour")
        }
    }
}
