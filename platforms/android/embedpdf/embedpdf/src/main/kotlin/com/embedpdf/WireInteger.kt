package com.embedpdf

// The public surface counts and indexes in `Int`; the engine takes and gives
// fixed-width integers (`UInt` for a hit, stage page, pane or tab index, `Long`
// for the viewer's page index and document-order position, `ULong` for a
// count). These convert at that seam without wrapping.

/**
 * This value as the engine's `UInt`. A value below zero is the caller's
 * mistake, so it throws `invalid-input` naming [argument].
 */
internal fun Int.toWireUInt(argument: String): UInt = toLong().toWireUInt(argument)

/**
 * This value as the engine's `UInt`. A value outside `0..4294967295` is the
 * caller's mistake, so it throws `invalid-input` naming [argument].
 */
internal fun Long.toWireUInt(argument: String): UInt {
    if (this < 0 || this > UInt.MAX_VALUE.toLong()) {
        throw PdfException(
            PdfException.Code.INVALID_INPUT,
            "$argument $this is outside 0...${UInt.MAX_VALUE}",
            name = "RangeError",
        )
    }
    return toUInt()
}

/**
 * A count or index the engine answered, as `Int`. One past `Int`'s range is a
 * value that didn't survive the boundary, so it throws `invalid-state` naming
 * [result].
 */
internal fun Long.toIntResult(result: String): Int {
    if (this < Int.MIN_VALUE || this > Int.MAX_VALUE) throw outsideInt(result, toString())
    return toInt()
}

/** [Long.toIntResult] for an unsigned answer. */
internal fun ULong.toIntResult(result: String): Int {
    if (this > Int.MAX_VALUE.toULong()) throw outsideInt(result, toString())
    return toInt()
}

/** [Long.toIntResult] for an unsigned answer. */
internal fun UInt.toIntResult(result: String): Int = toULong().toIntResult(result)

private fun outsideInt(result: String, value: String) = PdfException(
    PdfException.Code.INVALID_STATE,
    "$result $value is outside ${Int.MIN_VALUE}...${Int.MAX_VALUE}",
    name = "RangeError",
)
