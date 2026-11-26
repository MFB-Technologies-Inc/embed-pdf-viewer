package com.embedpdf

import androidx.annotation.ColorInt
import kotlinx.serialization.KSerializer
import kotlinx.serialization.Serializable
import kotlinx.serialization.SerializationException
import kotlinx.serialization.descriptors.PrimitiveKind
import kotlinx.serialization.descriptors.PrimitiveSerialDescriptor
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.encoding.Decoder
import kotlinx.serialization.encoding.Encoder

/**
 * An annotation colour: `/C` and the colours drafts and patches write.
 *
 * The engine reads and writes colours as `"#rrggbb"` (`Rgb` in
 * `crates/epdf-engine/src/features/annotation/write/prepare.rs`): six hex
 * digits in either case on the way in, lowercase on the way out. This type
 * holds the three channels, so a misspelled colour is caught where it is
 * written instead of as `invalid-input` from the engine.
 *
 * `AnnotationColor("#ff0000")` throws [IllegalArgumentException] for text that
 * isn't `#rrggbb`, as a malformed constant is a typo in the source; for text
 * from elsewhere, use [fromHex], which returns `null`. It encodes as its
 * [hex] string.
 */
@Serializable(with = AnnotationColorSerializer::class)
public class AnnotationColor(red: Int, green: Int, blue: Int) {
    /** 0 to 255. */
    public val red: Int = channel(red, "red")

    /** 0 to 255. */
    public val green: Int = channel(green, "green")

    /** 0 to 255. */
    public val blue: Int = channel(blue, "blue")

    /** The colour `"#rrggbb"` names; throws [IllegalArgumentException] for any other text. */
    public constructor(hex: String) : this(parse(hex) ?: throw IllegalArgumentException("\"$hex\" isn't an annotation colour: write \"#rrggbb\""))

    private constructor(channels: Triple<Int, Int, Int>) : this(channels.first, channels.second, channels.third)

    /** `"#rrggbb"`, lowercase, as the engine writes it. */
    public val hex: String
        get() = buildString(7) {
            append('#')
            for (channel in intArrayOf(red, green, blue)) {
                append(DIGITS[channel shr 4])
                append(DIGITS[channel and 0x0f])
            }
        }

    /**
     * The colour as an Android colour int, opaque. An annotation's opacity is
     * its own member.
     */
    @ColorInt
    public fun toColorInt(): Int = (0xff shl 24) or (red shl 16) or (green shl 8) or blue

    override fun equals(other: Any?): Boolean =
        other is AnnotationColor && red == other.red && green == other.green && blue == other.blue

    override fun hashCode(): Int = (red shl 16) or (green shl 8) or blue

    /** [hex]. */
    override fun toString(): String = hex

    public companion object {
        private const val DIGITS = "0123456789abcdef"

        /**
         * The colour `"#rrggbb"` names, in either case, or `null` for any other
         * text: the engine's own rule.
         */
        public fun fromHex(hex: String): AnnotationColor? = parse(hex)?.let(::AnnotationColor)

        /**
         * The red, green and blue of an Android colour int. Its alpha is
         * dropped: an annotation's opacity is its own member.
         */
        public fun fromColorInt(@ColorInt color: Int): AnnotationColor =
            AnnotationColor((color shr 16) and 0xff, (color shr 8) and 0xff, color and 0xff)

        private fun parse(hex: String): Triple<Int, Int, Int>? {
            if (hex.length != 7 || hex[0] != '#') return null
            fun channel(offset: Int): Int? {
                val high = nibble(hex[offset]) ?: return null
                val low = nibble(hex[offset + 1]) ?: return null
                return (high shl 4) or low
            }
            return Triple(channel(1) ?: return null, channel(3) ?: return null, channel(5) ?: return null)
        }

        // ASCII only: `Char.digitToIntOrNull` takes full-width digits too.
        private fun nibble(char: Char): Int? = when (char) {
            in '0'..'9' -> char - '0'
            in 'a'..'f' -> char - 'a' + 10
            in 'A'..'F' -> char - 'A' + 10
            else -> null
        }

        private fun channel(value: Int, name: String): Int {
            require(value in 0..255) { "$name $value is outside 0...255" }
            return value
        }
    }
}

internal object AnnotationColorSerializer : KSerializer<AnnotationColor> {
    override val descriptor: SerialDescriptor = PrimitiveSerialDescriptor("com.embedpdf.AnnotationColor", PrimitiveKind.STRING)

    override fun serialize(encoder: Encoder, value: AnnotationColor) {
        encoder.encodeString(value.hex)
    }

    override fun deserialize(decoder: Decoder): AnnotationColor {
        val text = decoder.decodeString()
        return AnnotationColor.fromHex(text) ?: throw SerializationException("\"$text\" isn't a colour as \"#rrggbb\"")
    }
}
