package com.embedpdf

import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive

/**
 * An annotation as the engine reads it, in Kotlin types: timestamps are
 * epoch milliseconds, the subtype is an [AnnotationSubtype], and the blend
 * mode an [AnnotationBlendMode]. [AnnotationCapability.get],
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
     * `/BM` on the appearance, or `null` when the record names none this
     * reads.
     */
    public val blendMode: AnnotationBlendMode? = blendMode(record.fieldsJson)

    /**
     * The engine's record, every member, as the engine wrote it. The members
     * this type doesn't read are read from here.
     */
    public val fieldsJson: String get() = record.fieldsJson

    override fun equals(other: Any?): Boolean = other is AnnotationInfo && record == other.record

    override fun hashCode(): Int = record.hashCode()

    override fun toString(): String = "AnnotationInfo($record)"

    internal companion object {
        private fun blendMode(fieldsJson: String): AnnotationBlendMode? = try {
            val mode = (AnnotationWire.json.parseToJsonElement(fieldsJson) as? JsonObject)?.get("blendMode") as? JsonPrimitive
            mode?.takeIf { it.isString }?.let { AnnotationBlendMode.of(it.content) }
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
