package com.embedpdf

import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive

/**
 * An annotation as the engine reads it, in Kotlin types: timestamps are
 * epoch milliseconds, the subtype is an [AnnotationSubtype], and the blend
 * mode an [AnnotationBlendMode], and each subtype but popup and widget
 * carries its own members typed in [details]. [AnnotationCapability.get],
 * [AnnotationCapability.list], [AnnotationCapability.selected] and
 * [AnnotationCapability.hitTest] return it.
 *
 * Nothing the engine reported is lost. [fieldsJson] is the engine's whole
 * record, every member, byte for byte; each timestamp keeps the engine's text
 * next to its instant; and [record] gives back the exact [AnnotationRecord]
 * this was made from, for the calls that take one. Two are equal when their
 * records are.
 */
public class AnnotationInfo(
    /** The engine's record this was made from, member for member. */
    public val record: AnnotationRecord,
) {
    public val annotationRef: AnnotationRef get() = record.annotationRef

    public val page: PageRef get() = record.page

    public val subtype: AnnotationSubtype get() = AnnotationSubtype(record.subtype)

    /** The annotation's `/Rect`, page space. */
    public val rect: Rect get() = record.rect

    public val contents: String? get() = record.contents

    /** `/T`. */
    public val author: String? get() = record.author

    /**
     * `/CreationDate` as milliseconds since 1970-01-01T00:00:00Z. `null` when
     * the annotation has none, or when [rawCreatedAt] isn't a date and time
     * this reads.
     */
    public val createdAt: Long? = record.createdAt?.let(::epochMillis)

    /**
     * `/CreationDate` exactly as the engine reports it: ISO 8601 with whole
     * seconds, and an offset of `Z`, `±HH:mm` or none. A PDF date without an
     * offset reads without one, and [createdAt] takes it as UTC, as the
     * annotation plugin orders comment replies.
     */
    public val rawCreatedAt: String? get() = record.createdAt

    /** `/M` as milliseconds since the epoch, on the same terms as [createdAt]. */
    public val modifiedAt: Long? = record.modifiedAt?.let(::epochMillis)

    /** `/M` exactly as the engine reports it. */
    public val rawModifiedAt: String? get() = record.modifiedAt

    public val groupId: String? get() = record.groupId

    /** `reply.to`: the annotation this one answers. */
    public val inReplyTo: AnnotationRef? get() = record.inReplyTo

    public val canUpdate: Boolean get() = record.canUpdate

    public val canDelete: Boolean get() = record.canDelete

    /**
     * A lent record's accent from its lender: a form widget's group color
     * while the annotation plugin edits it, not part of the record itself.
     * `null` for every other record.
     */
    public val accent: RecordAccent? get() = record.accent

    /**
     * `/BM` on the appearance, or `null` when the record names none this
     * reads.
     */
    public val blendMode: AnnotationBlendMode?

    /**
     * The subtype's own members, typed for every subtype but popup and
     * widget. Each subtype also has its accessor ([ink], [line], …), `null`
     * for any other subtype.
     */
    public val details: Details

    init {
        val members = members(record.fieldsJson)
        blendMode = (members?.get("blendMode") as? JsonPrimitive)?.takeIf { it.isString }?.let { AnnotationBlendMode.of(it.content) }
        details = AnnotationDetails.read(subtype, members)
    }

    /**
     * The engine's record, every member, as the engine wrote it. The members
     * [details] doesn't type are read from here.
     */
    public val fieldsJson: String get() = record.fieldsJson

    override fun equals(other: Any?): Boolean = other is AnnotationInfo && record == other.record

    override fun hashCode(): Int = record.hashCode()

    override fun toString(): String = "AnnotationInfo($record)"

    /** `details` as ink, or `null` for any other subtype. */
    public val ink: Ink? get() = details as? Ink

    /** `details` as a highlight, or `null` for any other subtype. */
    public val highlight: Highlight? get() = details as? Highlight

    /** `details` as a note, or `null` for any other subtype. */
    public val note: Note? get() = details as? Note

    /** `details` as an underline, or `null` for any other subtype. */
    public val underline: Underline? get() = details as? Underline

    /** `details` as a squiggly underline, or `null` for any other subtype. */
    public val squiggly: Squiggly? get() = details as? Squiggly

    /** `details` as a strikeout, or `null` for any other subtype. */
    public val strikeout: Strikeout? get() = details as? Strikeout

    /** `details` as a square, or `null` for any other subtype. */
    public val square: Square? get() = details as? Square

    /** `details` as a circle, or `null` for any other subtype. */
    public val circle: Circle? get() = details as? Circle

    /** `details` as a line, or `null` for any other subtype. */
    public val line: Line? get() = details as? Line

    /** `details` as a polygon, or `null` for any other subtype. */
    public val polygon: Polygon? get() = details as? Polygon

    /** `details` as a polyline, or `null` for any other subtype. */
    public val polyline: Polyline? get() = details as? Polyline

    /** `details` as a free text, or `null` for any other subtype. */
    public val freeText: FreeText? get() = details as? FreeText

    /** `details` as a caret, or `null` for any other subtype. */
    public val caret: Caret? get() = details as? Caret

    /** `details` as a stamp, or `null` for any other subtype. */
    public val stamp: Stamp? get() = details as? Stamp

    /** `details` as a link, or `null` for any other subtype. */
    public val link: Link? get() = details as? Link

    /** `details` as a file attachment, or `null` for any other subtype. */
    public val fileAttachment: FileAttachment? get() = details as? FileAttachment

    /** `details` as a redaction, or `null` for any other subtype. */
    public val redaction: Redaction? get() = details as? Redaction

    // ── subtype members ──
    //
    // Each type is the engine's read record for its kind
    // (`crates/epdf-engine/src/features/annotation/read/kinds.rs`). A member
    // whose read vocabulary is the write vocabulary takes the drafts' type
    // (`AnnotationLineEndings`, `AnnotationTextAlignment`, …). `borderStyle`
    // and `intent` take the open read types instead ([BorderStyle],
    // [AnnotationIntent]): a read can name a border style a write refuses
    // (`beveled`, `inset`).

    /**
     * The members a subtype adds to the common ones: one type per subtype
     * but popup and widget, and [Untyped] for those, for a subtype a newer
     * engine adds, and for a record whose members don't have the shape this
     * reads.
     */
    public sealed interface Details {
        /** A subtype this layer doesn't type, or members of another shape. Read [fieldsJson]. */
        public data object Untyped : Details
    }

    /**
     * A border's style as a read reports it, in the engine's spelling. [value]
     * is the wire string.
     *
     * Open like [AnnotationIntent]. A read names more styles than a write
     * takes: [AnnotationBorderStyle], the drafts' enum, is `solid` and
     * `dashed` only.
     */
    @JvmInline
    public value class BorderStyle(public val value: String) {
        /** The wire string. */
        override fun toString(): String = value

        public companion object {
            public val SOLID: BorderStyle = BorderStyle("solid")
            public val DASHED: BorderStyle = BorderStyle("dashed")
            public val BEVELED: BorderStyle = BorderStyle("beveled")
            public val INSET: BorderStyle = BorderStyle("inset")
        }
    }

    /**
     * An ink annotation's members: freehand strokes.
     *
     * @property color `/C`, or `null` when the annotation has none or an empty
     *   one: no stroke colour.
     * @property opacity `/CA`, 0 to 1.
     * @property borderStyle `solid`, `dashed`, `beveled` or `inset`.
     * @property dashArray the dash pattern, or `null` when the border isn't
     *   dashed or names none.
     * @property inkList `/InkList`: one list of page-space points per stroke.
     * @property intent `/IT`. `null` for ordinary pen ink; `ink-highlight` for
     *   a freehand highlighter.
     * @property richText the comment as styled text, or `null` when it is
     *   plain. Its plain text is [AnnotationInfo.contents].
     */
    public data class Ink(
        val color: AnnotationColor?,
        val opacity: Double,
        val strokeWidth: Double,
        val borderStyle: BorderStyle,
        val dashArray: List<Double>?,
        val inkList: List<List<Point>>,
        val intent: AnnotationIntent?,
        val rotation: Double?,
        val richText: AnnotationRichText?,
    ) : Details

    /**
     * A highlight annotation's members.
     *
     * @property color `/C`.
     * @property opacity `/CA`, 0 to 1.
     * @property quadPoints `/QuadPoints`, page space: one quad per run of
     *   marked text.
     * @property richText the comment as styled text, or `null` when it is
     *   plain. Its plain text is [AnnotationInfo.contents].
     */
    public data class Highlight(
        val color: AnnotationColor,
        val opacity: Double,
        val quadPoints: List<Quad>,
        val richText: AnnotationRichText?,
    ) : Details

    /**
     * A note's members: a `text` annotation, a sticky note.
     *
     * @property color `/C`.
     * @property opacity `/CA`, 0 to 1.
     * @property icon `/Name`: `comment`, `key`, `note`, `help`,
     *   `new-paragraph`, `paragraph` or `insert`.
     * @property open `/Open`: whether the note shows open.
     * @property state `/State`, lowercased when it is one of the standard
     *   names and verbatim when it is not.
     * @property stateModel `/StateModel`, on the same terms.
     * @property richText the comment as styled text, or `null` when it is
     *   plain. Its plain text is [AnnotationInfo.contents].
     */
    public data class Note(
        val color: AnnotationColor,
        val opacity: Double,
        val icon: String,
        val open: Boolean,
        val state: String?,
        val stateModel: String?,
        val richText: AnnotationRichText?,
    ) : Details

    /** The members an underline and a squiggly underline share. */
    public sealed interface TextMarkup : Details {
        /** `/C`. */
        public val color: AnnotationColor

        /** `/CA`, 0 to 1. */
        public val opacity: Double

        /** `/QuadPoints`, page space: one quad per run of marked text. */
        public val quadPoints: List<Quad>

        /** The comment as styled text, or `null` when it is plain. */
        public val richText: AnnotationRichText?
    }

    /** An underline's members. */
    public data class Underline(
        override val color: AnnotationColor,
        override val opacity: Double,
        override val quadPoints: List<Quad>,
        override val richText: AnnotationRichText?,
    ) : TextMarkup

    /** A squiggly underline's members. */
    public data class Squiggly(
        override val color: AnnotationColor,
        override val opacity: Double,
        override val quadPoints: List<Quad>,
        override val richText: AnnotationRichText?,
    ) : TextMarkup

    /**
     * A strikeout's members.
     *
     * @property intent `/IT`: `strikeout-text-edit` for text marked for
     *   deletion, `null` for a plain strikeout.
     */
    public data class Strikeout(
        val color: AnnotationColor,
        val opacity: Double,
        val quadPoints: List<Quad>,
        val intent: AnnotationIntent?,
        val richText: AnnotationRichText?,
    ) : Details

    /** The members a square and a circle share. */
    public sealed interface Shape : Details {
        /** `/C`, the stroke, or `null` for none. */
        public val color: AnnotationColor?

        /** `/CA`, 0 to 1. */
        public val opacity: Double
        public val strokeWidth: Double

        /** `solid`, `dashed`, `beveled` or `inset`. */
        public val borderStyle: BorderStyle

        /** The dash pattern, or `null` when the border isn't dashed or names none. */
        public val dashArray: List<Double>?

        /** `/IC`, the fill, or `null` for none. */
        public val interiorColor: AnnotationColor?

        /** The shape's box, page space, before rotation. */
        public val box: Rect
        public val rotation: Double?

        /** `/BE`: the cloudy border's intensity, or `null` for a plain border. */
        public val cloudyIntensity: Double?

        /** The comment as styled text, or `null` when it is plain. */
        public val richText: AnnotationRichText?
    }

    /** A square's members. */
    public data class Square(
        override val color: AnnotationColor?,
        override val opacity: Double,
        override val strokeWidth: Double,
        override val borderStyle: BorderStyle,
        override val dashArray: List<Double>?,
        override val interiorColor: AnnotationColor?,
        override val box: Rect,
        override val rotation: Double?,
        override val cloudyIntensity: Double?,
        override val richText: AnnotationRichText?,
    ) : Shape

    /** A circle's members. */
    public data class Circle(
        override val color: AnnotationColor?,
        override val opacity: Double,
        override val strokeWidth: Double,
        override val borderStyle: BorderStyle,
        override val dashArray: List<Double>?,
        override val interiorColor: AnnotationColor?,
        override val box: Rect,
        override val rotation: Double?,
        override val cloudyIntensity: Double?,
        override val richText: AnnotationRichText?,
    ) : Shape

    /**
     * A line's members.
     *
     * @property color `/C`, the stroke, or `null` for none.
     * @property opacity `/CA`, 0 to 1.
     * @property borderStyle `solid`, `dashed`, `beveled` or `inset`.
     * @property interiorColor `/IC`: what fills a closed line ending.
     * @property start `/L`, page space.
     * @property intent `/IT`: `line-arrow`, `line-dimension`, or `null` for a
     *   plain line.
     * @property captionEnabled `/Cap`, `false` when the file has none.
     * @property leader `/LL`, `/LLE` and `/LLO`; the engine reads a missing
     *   `/LLE` or `/LLO` as 0.
     * @property richText the comment as styled text, or `null` when it is
     *   plain. Its plain text is [AnnotationInfo.contents].
     * @property measure `/Measure`: the scale a dimension measures in, or
     *   `null` for none. A scale the engine can't model has only its
     *   `subtype` (`geospatial`, `unknown`).
     */
    public data class Line(
        val color: AnnotationColor?,
        val opacity: Double,
        val strokeWidth: Double,
        val borderStyle: BorderStyle,
        val dashArray: List<Double>?,
        val interiorColor: AnnotationColor?,
        val start: Point,
        val end: Point,
        val lineEndings: AnnotationLineEndings,
        val rotation: Double?,
        val intent: AnnotationIntent?,
        val captionEnabled: Boolean,
        val captionPosition: AnnotationCaptionPosition,
        val captionOffset: AnnotationCaptionOffset?,
        val leader: AnnotationLineLeader?,
        val richText: AnnotationRichText?,
        val measure: PdfMeasurement?,
    ) : Details

    /**
     * A polygon's members.
     *
     * @property color `/C`, the stroke, or `null` for none.
     * @property opacity `/CA`, 0 to 1.
     * @property borderStyle `solid`, `dashed`, `beveled` or `inset`.
     * @property interiorColor `/IC`, the fill, or `null` for none.
     * @property vertices `/Vertices`, page space.
     * @property intent `/IT`: `polygon-cloud`, `polygon-dimension`, or `null`
     *   for a plain polygon.
     * @property cloudyIntensity `/BE`: the cloudy border's intensity, or
     *   `null` for a plain border.
     * @property captionCenter the caption's centre, page space, or `null` when
     *   the engine places it.
     * @property richText the comment as styled text, or `null` when it is
     *   plain. Its plain text is [AnnotationInfo.contents].
     * @property measure `/Measure`: the scale a dimension measures in, or
     *   `null` for none. A scale the engine can't model has only its
     *   `subtype` (`geospatial`, `unknown`).
     */
    public data class Polygon(
        val color: AnnotationColor?,
        val opacity: Double,
        val strokeWidth: Double,
        val borderStyle: BorderStyle,
        val dashArray: List<Double>?,
        val interiorColor: AnnotationColor?,
        val vertices: List<Point>,
        val rotation: Double?,
        val intent: AnnotationIntent?,
        val cloudyIntensity: Double?,
        val captionEnabled: Boolean?,
        val captionCenter: Point?,
        val richText: AnnotationRichText?,
        val measure: PdfMeasurement?,
    ) : Details

    /**
     * A polyline's members.
     *
     * @property color `/C`, the stroke, or `null` for none.
     * @property opacity `/CA`, 0 to 1.
     * @property borderStyle `solid`, `dashed`, `beveled` or `inset`.
     * @property interiorColor `/IC`: what fills a closed line ending.
     * @property vertices `/Vertices`, page space.
     * @property intent `/IT`: `polyline-dimension`, or `null` for a plain
     *   polyline.
     * @property captionCenter the caption's centre, page space, or `null` when
     *   the engine places it.
     * @property richText the comment as styled text, or `null` when it is
     *   plain. Its plain text is [AnnotationInfo.contents].
     * @property measure `/Measure`: the scale a dimension measures in, or
     *   `null` for none. A scale the engine can't model has only its
     *   `subtype` (`geospatial`, `unknown`).
     */
    public data class Polyline(
        val color: AnnotationColor?,
        val opacity: Double,
        val strokeWidth: Double,
        val borderStyle: BorderStyle,
        val dashArray: List<Double>?,
        val interiorColor: AnnotationColor?,
        val vertices: List<Point>,
        val lineEndings: AnnotationLineEndings,
        val rotation: Double?,
        val intent: AnnotationIntent?,
        val captionEnabled: Boolean?,
        val captionCenter: Point?,
        val richText: AnnotationRichText?,
        val measure: PdfMeasurement?,
    ) : Details

    /**
     * A free text's members. Its text is [AnnotationInfo.contents], plain;
     * the styled runs are [richText].
     *
     * @property box the text box, page space, before rotation.
     * @property intent `free-text`, `free-text-callout` or
     *   `free-text-typewriter`.
     * @property color the border.
     * @property interiorColor the fill, or `null` for none.
     * @property opacity `/CA`, 0 to 1.
     * @property borderStyle `solid`, `dashed`, `beveled` or `inset`.
     * @property richText the text as styled runs; its plain text is
     *   [AnnotationInfo.contents]. The engine reports one for every free text
     *   it reads, one unstyled run per line when the text was written plain.
     */
    public data class FreeText(
        val box: Rect,
        val intent: AnnotationIntent,
        val fontFamily: String,
        val fontSize: Double,
        val fontColor: AnnotationColor,
        val textAlign: AnnotationTextAlignment,
        val verticalAlign: AnnotationVerticalAlignment,
        val color: AnnotationColor,
        val interiorColor: AnnotationColor?,
        val opacity: Double,
        val strokeWidth: Double,
        val borderStyle: BorderStyle,
        val dashArray: List<Double>?,
        val cloudyIntensity: Double?,
        val rotation: Double?,
        val calloutLine: AnnotationCalloutLine?,
        val lineEnding: AnnotationLineEnding?,
        val richText: AnnotationRichText?,
    ) : Details

    /**
     * A caret's members.
     *
     * @property color `/C`.
     * @property opacity `/CA`, 0 to 1.
     * @property box the caret's box, page space, before rotation.
     * @property intent `/IT`: `replace`, or `null` for an insertion.
     * @property richText the comment as styled text, or `null` when it is
     *   plain. Its plain text is [AnnotationInfo.contents].
     */
    public data class Caret(
        val color: AnnotationColor,
        val opacity: Double,
        val box: Rect,
        val rotation: Double?,
        val intent: AnnotationIntent?,
        val richText: AnnotationRichText?,
    ) : Details

    /**
     * A stamp's members. Its image is the appearance, which the record doesn't
     * carry.
     *
     * @property name `/Name`, verbatim: a standard label (`Approved`) or a
     *   custom one.
     * @property opacity `/CA`, 0 to 1.
     * @property box the stamp's box, page space, before rotation.
     * @property richText the comment as styled text, or `null` when it is
     *   plain. Its plain text is [AnnotationInfo.contents].
     */
    public data class Stamp(
        val name: String?,
        val fit: AnnotationStampFit?,
        val opacity: Double,
        val box: Rect,
        val rotation: Double?,
        val richText: AnnotationRichText?,
    ) : Details

    /**
     * A link's members.
     *
     * @property target where it goes, or `null` when it goes nowhere or
     *   somewhere a link draft can't write (another file, a launch, a
     *   script): read those from [fieldsJson]'s `target` and `actions`.
     */
    public data class Link(val target: AnnotationLinkTarget?) : Details

    /**
     * A file attachment's members.
     *
     * @property color `/C`.
     * @property opacity `/CA`, 0 to 1.
     * @property file the attached file's description, or `null` when there is
     *   no readable `/FS`.
     * @property richText the comment as styled text, or `null` when it is
     *   plain. Its plain text is [AnnotationInfo.contents].
     */
    public data class FileAttachment(
        val icon: AnnotationFileAttachmentIcon,
        val color: AnnotationColor,
        val opacity: Double,
        val file: AttachedFile?,
        val richText: AnnotationRichText?,
    ) : Details

    /**
     * The file a file attachment carries, without its bytes.
     *
     * @property name `/UF`, empty when the file names none.
     * @property sizeBytes the decoded length in bytes.
     * @property md5 the MD5, lowercase hex.
     * @property rawCreatedAt `/CreationDate` as the engine reports it, ISO
     *   8601.
     * @property rawModifiedAt `/ModDate` as the engine reports it, ISO 8601.
     */
    public data class AttachedFile(
        val name: String,
        val mimeType: String?,
        val description: String?,
        val sizeBytes: Long?,
        val md5: String?,
        val rawCreatedAt: String?,
        val rawModifiedAt: String?,
    ) {
        /** [rawCreatedAt] as milliseconds since the epoch, on [AnnotationInfo.createdAt]'s terms. */
        val createdAt: Long? get() = rawCreatedAt?.let(::epochMillis)

        /** [rawModifiedAt] as milliseconds since the epoch. */
        val modifiedAt: Long? get() = rawModifiedAt?.let(::epochMillis)
    }

    /**
     * A redaction's members: what it marks, and what it draws once applied.
     *
     * @property quadPoints `/QuadPoints`, page space: the text runs it marks,
     *   empty for an area.
     * @property color `/C`: the outline drawn before it is applied.
     * @property opacity `/CA`, 0 to 1.
     * @property interiorColor `/IC`: the fill once applied, or `null` for none.
     * @property fontSize points; 0 fits the overlay text to the area.
     * @property richText the comment as styled text, or `null` when it is
     *   plain. Its plain text is [AnnotationInfo.contents].
     */
    public data class Redaction(
        val quadPoints: List<Quad>,
        val color: AnnotationColor,
        val opacity: Double,
        val interiorColor: AnnotationColor?,
        val overlayText: String?,
        val repeat: Boolean,
        val fontFamily: String,
        val fontSize: Double,
        val fontColor: AnnotationColor,
        val textAlign: AnnotationTextAlignment,
        val richText: AnnotationRichText?,
    ) : Details

    internal companion object {
        /** The record's members, or `null` when [fieldsJson] isn't a JSON object. */
        private fun members(fieldsJson: String): JsonObject? = try {
            AnnotationWire.json.parseToJsonElement(fieldsJson) as? JsonObject
        } catch (e: IllegalArgumentException) {
            null
        }

        /**
         * The instant an engine timestamp names, in milliseconds since the
         * epoch, or `null` when the text isn't one.
         *
         * The engine writes `YYYY-MM-DDTHH:mm:ss` and then `Z`, `±HH:mm` or
         * nothing (`pdf_date_to_iso` in `crates/epdf-engine/src/clock.rs`).
         * This also reads what the engine takes as a date and time on the way
         * in (`is_iso_date_time` there): seconds may be left out, a fraction
         * of a second may follow them, and an offset may drop its colon.
         * Without an offset the time is UTC, as the annotation plugin's
         * `date_parse` (`crates/plugins/annotation/src/engine.rs`) reads it.
         * The day must exist, and the hour, minute, second and offset must be
         * in range. A fraction finer than a millisecond is dropped.
         */
        fun epochMillis(text: String): Long? {
            var index = 0

            fun number(length: Int): Int? {
                if (index + length > text.length) return null
                var value = 0
                for (char in text.substring(index, index + length)) {
                    if (char !in '0'..'9') return null
                    value = value * 10 + (char - '0')
                }
                index += length
                return value
            }

            fun skip(char: Char): Boolean {
                if (index >= text.length || text[index] != char) return false
                index += 1
                return true
            }

            val year = number(4) ?: return null
            if (!skip('-')) return null
            val month = number(2) ?: return null
            if (!skip('-')) return null
            val day = number(2) ?: return null
            if (!skip('T')) return null
            val hour = number(2) ?: return null
            if (!skip(':')) return null
            val minute = number(2) ?: return null
            var second = 0
            var fractionMillis = 0L
            if (skip(':')) {
                second = number(2) ?: return null
                if (skip('.')) {
                    val start = index
                    while (index < text.length && text[index] in '0'..'9') index += 1
                    if (index == start) return null
                    fractionMillis = (text.substring(start, index).padEnd(3, '0').take(3)).toLong()
                }
            }
            var offsetMinutes = 0
            if (skip('Z')) {
                // UTC.
            } else if (index < text.length && (text[index] == '+' || text[index] == '-')) {
                val sign = if (text[index] == '-') -1 else 1
                index += 1
                val hours = number(2) ?: return null
                skip(':')
                val minutes = number(2) ?: return null
                if (hours > 23 || minutes > 59) return null
                offsetMinutes = sign * (hours * 60 + minutes)
            }
            if (index != text.length || month !in 1..12 || day !in 1..daysIn(month, year) || hour > 23 || minute > 59 || second > 59) {
                return null
            }
            val days = daysFromCivil(year.toLong(), month, day)
            val seconds = ((days * 24 + hour) * 60 + minute - offsetMinutes) * 60 + second
            return seconds * 1000 + fractionMillis
        }

        private fun daysIn(month: Int, year: Int): Int = when (month) {
            2 -> if (year % 4 == 0 && (year % 100 != 0 || year % 400 == 0)) 29 else 28
            4, 6, 9, 11 -> 30
            else -> 31
        }

        /**
         * Days from 1970-01-01 to a proleptic Gregorian date, the algorithm
         * `days_from_civil` in `crates/epdf-engine/src/clock.rs` uses.
         */
        private fun daysFromCivil(year: Long, month: Int, day: Int): Long {
            val shiftedYear = if (month <= 2) year - 1 else year
            val era = (if (shiftedYear >= 0) shiftedYear else shiftedYear - 399) / 400
            val yearOfEra = shiftedYear - era * 400
            val dayOfYear = (153 * (if (month > 2) month - 3 else month + 9) + 2) / 5 + day - 1
            val dayOfEra = yearOfEra * 365 + yearOfEra / 4 - yearOfEra / 100 + dayOfYear
            return era * 146_097 + dayOfEra - 719_468
        }
    }
}

/** The created or updated annotation, typed. */
public val AnnotationChangedEvent.annotationInfo: AnnotationInfo get() = AnnotationInfo(annotation)

/** The copied annotations, typed, in [AnnotationImportResult.annotations]' order. */
public val AnnotationImportResult.annotationInfos: List<AnnotationInfo> get() = annotations.map(::AnnotationInfo)
