namespace EmbedPDF;

/// <summary>
/// The public surface counts and indexes in <see cref="int"/>; the engine
/// takes and gives fixed-width integers (<see cref="uint"/> for a hit,
/// stage page, pane or tab index, <see cref="long"/> for the viewer's page
/// index and document-order position). These convert at that seam.
/// </summary>
internal static class WireInteger
{
    /// <summary>
    /// <paramref name="value"/> as the engine's <see cref="uint"/>. A negative
    /// value is the caller's mistake, so it throws <c>invalid-input</c>
    /// naming <paramref name="argument"/>.
    /// </summary>
    internal static uint ToUInt32(int value, string argument) =>
        value >= 0 ? (uint)value
            : throw new PdfException("RangeError", PdfErrorCode.InvalidInput,
                $"{argument} {value} is outside {uint.MinValue}...{uint.MaxValue}");

    /// <summary><paramref name="value"/> as the engine's <see cref="long"/>; every <see cref="int"/> fits.</summary>
    internal static long ToInt64(int value) => value;

    /// <summary>
    /// A count or index the engine answered, as <see cref="int"/>. One that
    /// doesn't fit didn't survive the boundary, so it throws <c>invalid-state</c>.
    /// </summary>
    internal static int ToInt(ulong value, string result) =>
        value <= int.MaxValue ? (int)value : throw Past(value.ToString(System.Globalization.CultureInfo.InvariantCulture), result);

    /// <inheritdoc cref="ToInt(ulong, string)"/>
    internal static int ToInt(uint value, string result) => ToInt((ulong)value, result);

    /// <inheritdoc cref="ToInt(ulong, string)"/>
    internal static int ToInt(long value, string result) =>
        value is >= int.MinValue and <= int.MaxValue ? (int)value
            : throw Past(value.ToString(System.Globalization.CultureInfo.InvariantCulture), result);

    /// <summary>A 64-bit count or version the engine answered, as <see cref="long"/>; past <see cref="long.MaxValue"/> throws <c>invalid-state</c>.</summary>
    internal static long ToLong(ulong value, string result) =>
        value <= long.MaxValue ? (long)value
            : throw new PdfException("RangeError", PdfErrorCode.InvalidState, $"{result} {value} is outside 0...{long.MaxValue}");

    /// <summary><paramref name="value"/> as the engine's <see cref="ulong"/>; a negative value throws <c>invalid-input</c>.</summary>
    internal static ulong ToUInt64(long value, string argument) =>
        value >= 0 ? (ulong)value
            : throw new PdfException("RangeError", PdfErrorCode.InvalidInput,
                $"{argument} {value} is outside {ulong.MinValue}...{ulong.MaxValue}");

    /// <summary>A caller's optional index for the engine; see <see cref="ToUInt32(int, string)"/>.</summary>
    internal static uint? ToUInt32(int? value, string argument) => value is { } v ? ToUInt32(v, argument) : null;

    /// <summary><paramref name="value"/> as the engine's <see cref="byte"/>, such as a permission level; outside 0 to 255 throws <c>invalid-input</c>.</summary>
    internal static byte ToByte(int value, string argument) =>
        value is >= byte.MinValue and <= byte.MaxValue ? (byte)value
            : throw new PdfException("RangeError", PdfErrorCode.InvalidInput,
                $"{argument} {value} is outside {byte.MinValue}...{byte.MaxValue}");

    static PdfException Past(string value, string result) =>
        new("RangeError", PdfErrorCode.InvalidState, $"{result} {value} is outside {int.MinValue}...{int.MaxValue}");
}
