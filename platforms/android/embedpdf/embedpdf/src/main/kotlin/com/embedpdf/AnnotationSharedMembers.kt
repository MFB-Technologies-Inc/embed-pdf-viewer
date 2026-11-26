package com.embedpdf

import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive

// A dimension's `measure` as the typed drafts, patches and reads carry it.
// It is a data field of line, polygon and polyline (`kind_fields` in
// `crates/epdf-engine/src/features/annotation/write/prepare.rs`), and each
// draft and patch writes it flat, beside its other members.
//
// A measure is a `PdfMeasurement`, the type the measurement capability hands
// out. That type is generated and has no serializer, so `AnnotationMeasure`
// spells it member for member: the annotation wire's measure (`read_measure`
// and `assert_writable_measure` in `crates/epdf-engine/src/features/measure.rs`)
// has no member `PdfMeasurement` lacks.

public typealias PdfMeasurement = uniffi.epdf_plugin_measurement.PdfMeasurement

public typealias PdfNumberFormat = uniffi.epdf_plugin_measurement.PdfNumberFormat

/** [PdfMeasurement.origin]: where the scale's coordinates start. */
public typealias MeasureOrigin = uniffi.epdf_plugin_measurement.MeasureOrigin

public typealias MeasureFraction = uniffi.epdf_plugin_measurement.MeasureFraction

public typealias MeasureLabelPosition = uniffi.epdf_plugin_measurement.MeasureLabelPosition

/**
 * [PdfMeasurement] as the engine's JSON spells it. A member that is `null` is
 * left out, which is how the engine reads and writes an absent one.
 */
internal object AnnotationMeasure {
    fun json(measure: PdfMeasurement): JsonObject {
        val members = LinkedHashMap<String, JsonElement>()
        members["subtype"] = JsonPrimitive(measure.subtype)
        measure.ratio?.let { members["ratio"] = JsonPrimitive(it) }
        fun formats(key: String, list: List<PdfNumberFormat>?) {
            list?.let { members[key] = kotlinx.serialization.json.JsonArray(it.map(::format)) }
        }
        formats("x", measure.x)
        formats("y", measure.y)
        formats("distance", measure.distance)
        formats("area", measure.area)
        formats("angle", measure.angle)
        formats("slope", measure.slope)
        measure.origin?.let { members["origin"] = JsonObject(linkedMapOf("x" to JsonPrimitive(it.x), "y" to JsonPrimitive(it.y))) }
        measure.cyx?.let { members["cyx"] = JsonPrimitive(it) }
        return JsonObject(members)
    }

    private fun format(format: PdfNumberFormat): JsonObject {
        val members = LinkedHashMap<String, JsonElement>()
        members["unit"] = JsonPrimitive(format.unit)
        format.conversion?.let { members["conversion"] = JsonPrimitive(it) }
        format.fraction?.let { members["fraction"] = JsonPrimitive(FRACTIONS.getValue(it)) }
        format.precision?.let { members["precision"] = JsonPrimitive(it) }
        format.fixed?.let { members["fixed"] = JsonPrimitive(it) }
        format.thousands?.let { members["thousands"] = JsonPrimitive(it) }
        format.decimal?.let { members["decimal"] = JsonPrimitive(it) }
        format.prefixSpacing?.let { members["prefixSpacing"] = JsonPrimitive(it) }
        format.suffixSpacing?.let { members["suffixSpacing"] = JsonPrimitive(it) }
        format.labelPosition?.let { members["labelPosition"] = JsonPrimitive(LABEL_POSITIONS.getValue(it)) }
        return JsonObject(members)
    }

    /** The measure [element] holds; a member of another shape throws [SerializationException]. */
    fun read(element: JsonElement): PdfMeasurement {
        val reader = FieldBagReader(element.asObject("measure"))
        return with(reader) {
            fun formats(key: String): List<PdfNumberFormat>? = optional(key) { FieldValues.array(it).map(::readFormat) }
            PdfMeasurement(
                subtype = required("subtype", FieldValues::string),
                ratio = optional("ratio", FieldValues::string),
                x = formats("x"),
                y = formats("y"),
                distance = formats("distance"),
                area = formats("area"),
                angle = formats("angle"),
                slope = formats("slope"),
                origin = optional("origin") {
                    val origin = it.asObject("origin")
                    MeasureOrigin(FieldValues.double(origin.field("x")), FieldValues.double(origin.field("y")))
                },
                cyx = optional("cyx", FieldValues::double),
            )
        }
    }

    private fun readFormat(element: JsonElement): PdfNumberFormat = with(FieldBagReader(element.asObject("number format"))) {
        PdfNumberFormat(
            unit = required("unit", FieldValues::string),
            conversion = optional("conversion", FieldValues::double),
            fraction = optional("fraction") { FieldValues.enum(it, MeasureFraction.entries) { fraction -> FRACTIONS.getValue(fraction) } },
            precision = optional("precision", FieldValues::double),
            fixed = optional("fixed", FieldValues::boolean),
            thousands = optional("thousands", FieldValues::string),
            decimal = optional("decimal", FieldValues::string),
            prefixSpacing = optional("prefixSpacing", FieldValues::string),
            suffixSpacing = optional("suffixSpacing", FieldValues::string),
            labelPosition = optional("labelPosition") {
                FieldValues.enum(it, MeasureLabelPosition.entries) { position -> LABEL_POSITIONS.getValue(position) }
            },
        )
    }

    private val FRACTIONS: Map<MeasureFraction, String> = mapOf(
        MeasureFraction.DECIMAL to "decimal",
        MeasureFraction.FRACTION to "fraction",
        MeasureFraction.ROUND to "round",
        MeasureFraction.TRUNCATE to "truncate",
    )

    private val LABEL_POSITIONS: Map<MeasureLabelPosition, String> = mapOf(
        MeasureLabelPosition.SUFFIX to "suffix",
        MeasureLabelPosition.PREFIX to "prefix",
    )
}

/** A dimension's scale: left out when unchanged, `null` when cleared. */
internal fun FieldBagWriter.measure(value: Nullable<PdfMeasurement>) = put("measure", value, AnnotationMeasure::json)

/** A patch's measure: unchanged when the key is missing, cleared for `null`. */
internal fun FieldBagReader.measure(): Nullable<PdfMeasurement> = nullable("measure", AnnotationMeasure::read)
