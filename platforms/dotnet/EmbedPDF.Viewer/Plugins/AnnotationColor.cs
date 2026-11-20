using System.Diagnostics.CodeAnalysis;

namespace EmbedPDF;

/// <summary>
/// An annotation colour as the engine writes it: <c>"#rrggbb"</c>, opaque
/// sRGB. Opacity is the annotation's own <c>Opacity</c> member.
/// </summary>
/// <param name="Red">Red, 0 to 255.</param>
/// <param name="Green">Green, 0 to 255.</param>
/// <param name="Blue">Blue, 0 to 255.</param>
public readonly record struct AnnotationColor(byte Red, byte Green, byte Blue)
{
    /// <summary>The colour as <c>"#rrggbb"</c>, lowercase.</summary>
    public string Hex => $"#{Red:x2}{Green:x2}{Blue:x2}";

    /// <summary>A colour from <c>"#rrggbb"</c>, digits in either case. Throws <see cref="FormatException"/> for anything else.</summary>
    public static AnnotationColor Parse(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        return TryParse(hex, out var color) ? color : throw new FormatException($"\"{hex}\" isn't an annotation colour: write \"#rrggbb\"");
    }

    /// <summary>A colour from <c>"#rrggbb"</c>: a <c>#</c> and six hex digits, in either case, and nothing else.</summary>
    public static bool TryParse([NotNullWhen(true)] string? hex, out AnnotationColor color)
    {
        color = default;
        if (hex is not { Length: 7 } || hex[0] != '#') return false;
        Span<byte> channels = stackalloc byte[3];
        for (var i = 0; i < 3; i++)
        {
            if (Nibble(hex[1 + 2 * i]) is not { } high || Nibble(hex[2 + 2 * i]) is not { } low) return false;
            channels[i] = (byte)(high << 4 | low);
        }
        color = new(channels[0], channels[1], channels[2]);
        return true;
    }

    /// <summary>The colour as <c>"#rrggbb"</c>.</summary>
    public override string ToString() => Hex;

    static int? Nibble(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => null,
    };
}
