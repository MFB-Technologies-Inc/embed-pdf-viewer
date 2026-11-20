package com.embedpdf

import kotlinx.serialization.KSerializer
import kotlinx.serialization.Serializable
import kotlinx.serialization.SerializationException
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.encoding.Decoder
import kotlinx.serialization.encoding.Encoder
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive

/**
 * Styled text: a free text's text, or another annotation's comment (`/RC`).
 * It is the engine's rich-text document
 * (`crates/epdf-engine/src/features/annotation/rich_text.rs`), the same type
 * on reads, drafts and patches, so what a read hands out can be written back
 * as it is.
 *
 * The engine fixes only the outline: paragraphs of runs of text. The body
 * style, each run's style and each paragraph's properties are open maps it
 * passes through, held here as [JsonElement] with every key kept.
 *
 * `contents` is always the rich text's plain text: [plainText], with a `\r\n`
 * or `\r` inside a run's text spelled `\n`.
 *
 * - A draft or patch that gives `richText` gets `contents` from it. Giving
 *   both is allowed only when they agree, line breaks however spelled;
 *   otherwise the call throws `invalid-input`.
 * - A free text always has rich text. One created or patched with only
 *   `contents` reads back one unstyled run per line, in the body style.
 * - Any other annotation has rich text only when its comment is styled, and
 *   reads `null` for a plain comment. On a patch, [Nullable.Clear] makes a
 *   styled comment plain and keeps `contents`; new `contents` on a styled
 *   comment becomes one unstyled run per line over the same body;
 *   clearing `contents` removes both.
 *
 * @property body the body style every run starts from: `family`, `weight`,
 *   `italic`, `size`, `color`, `decoration`, `script`, `letterSpacing`,
 *   `horizontalScale`, `align`, `dir`, and whatever else the engine reports.
 *   A read always has one. Left `null` on a write, the annotation keeps its
 *   body. One that is given replaces the body whole: a key it leaves out takes
 *   the engine's default, not the current value. On a free text, the draft's
 *   or patch's own `fontFamily`, `fontSize`, `fontColor` and `textAlign` are
 *   then applied over it.
 */
@Serializable(with = AnnotationRichTextSerializer::class)
public data class AnnotationRichText(
    val body: Map<String, JsonElement>? = null,
    val paragraphs: List<Paragraph>,
) {
    /**
     * The text without its styles: each paragraph's runs joined, one
     * paragraph per line. The engine writes the same to `contents`, after
     * spelling a `\r\n` or `\r` inside a run's text as `\n`.
     */
    val plainText: String
        get() = paragraphs.joinToString("\n") { paragraph -> paragraph.runs.joinToString("") { it.text } }

    /**
     * One paragraph: its runs, and the paragraph properties it overrides.
     *
     * @property properties every member of the paragraph's JSON object but
     *   `runs`. A property named `runs` is not written: the runs are [runs].
     */
    public data class Paragraph(val runs: List<Run>, val properties: Map<String, JsonElement> = emptyMap())

    /**
     * A stretch of text in one style.
     *
     * @property text may hold `\n`: a line break inside the paragraph.
     * @property style what the run overrides of the body style, or `null` for
     *   nothing.
     */
    public data class Run(val text: String, val style: Map<String, JsonElement>? = null)

    public companion object {
        /**
         * Unstyled text: one paragraph per line of [text], one run each. A
         * line ends at `\r\n`, `\r` or `\n`.
         */
        public fun plain(text: String): AnnotationRichText = AnnotationRichText(
            paragraphs = text.replace("\r\n", "\n").replace('\r', '\n').split('\n').map { Paragraph(listOf(Run(it))) },
        )
    }
}

internal object AnnotationRichTextSerializer : KSerializer<AnnotationRichText> {
    override val descriptor: SerialDescriptor = JsonElement.serializer().descriptor

    override fun serialize(encoder: Encoder, value: AnnotationRichText) {
        encoder.json().encodeJsonElement(element(value))
    }

    override fun deserialize(decoder: Decoder): AnnotationRichText = read(decoder.json().decodeJsonElement())

    fun element(value: AnnotationRichText): JsonObject = JsonObject(
        buildMap {
            value.body?.let { put("body", JsonObject(it)) }
            put(
                "paragraphs",
                JsonArray(
                    value.paragraphs.map { paragraph ->
                        JsonObject(
                            buildMap {
                                putAll(paragraph.properties.filterKeys { it != "runs" })
                                put("runs", JsonArray(paragraph.runs.map(::run)))
                            },
                        )
                    },
                ),
            )
        },
    )

    private fun run(run: AnnotationRichText.Run): JsonObject = JsonObject(
        buildMap {
            put("text", JsonPrimitive(run.text))
            run.style?.let { put("style", JsonObject(it)) }
        },
    )

    fun read(element: JsonElement): AnnotationRichText {
        val document = element.asObject("rich text")
        val body = document["body"]?.takeIf { it !is JsonNull }?.asObject("body")
        val paragraphs = document.field("paragraphs") as? JsonArray ?: throw SerializationException("paragraphs is not an array")
        return AnnotationRichText(
            body = body,
            paragraphs = paragraphs.map { item ->
                val paragraph = item.asObject("paragraph")
                val runs = paragraph.field("runs") as? JsonArray ?: throw SerializationException("runs is not an array")
                AnnotationRichText.Paragraph(
                    runs = runs.map { runItem ->
                        val run = runItem.asObject("run")
                        AnnotationRichText.Run(
                            text = run.string("text"),
                            style = run["style"]?.takeIf { it !is JsonNull }?.asObject("style"),
                        )
                    },
                    properties = paragraph.filterKeys { it != "runs" },
                )
            },
        )
    }
}
