namespace EmbedPDF;

/// <summary>A point in PDF points, top-left origin, y pointing down.</summary>
/// <param name="X">Horizontal position.</param>
/// <param name="Y">Vertical position.</param>
public readonly record struct PdfPoint(double X, double Y);

/// <summary>A size in PDF points.</summary>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
public readonly record struct PdfSize(double Width, double Height);

/// <summary>A rectangle in PDF points, top-left origin, y pointing down.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
public readonly record struct PdfRect(double X, double Y, double Width, double Height);

/// <summary>A clockwise quarter turn.</summary>
public enum RenderRotation
{
    /// <summary>0 degrees.</summary>
    None,
    /// <summary>90 degrees clockwise.</summary>
    Clockwise90,
    /// <summary>180 degrees.</summary>
    Clockwise180,
    /// <summary>270 degrees clockwise.</summary>
    Clockwise270,
}

/// <summary>Conversions between <see cref="RenderRotation"/> and degrees.</summary>
public static class RenderRotationExtensions
{
    /// <summary>
    /// The quarter turn nearest <paramref name="degrees"/> clockwise. Any value
    /// is accepted; it reduces to 0 to 359 first, and a value halfway between
    /// two turns takes the larger.
    /// </summary>
    public static RenderRotation FromDegrees(int degrees)
    {
        var reduced = degrees % 360;
        if (reduced < 0) reduced += 360;
        return ((reduced + 45) / 90 % 4) switch
        {
            1 => RenderRotation.Clockwise90,
            2 => RenderRotation.Clockwise180,
            3 => RenderRotation.Clockwise270,
            _ => RenderRotation.None,
        };
    }

    /// <summary>
    /// The quarter turn nearest <paramref name="degrees"/> clockwise, rounded as
    /// <see cref="FromDegrees(int)"/> rounds. A value that is not finite is
    /// <see cref="RenderRotation.None"/>.
    /// </summary>
    public static RenderRotation FromDegrees(double degrees)
    {
        if (!double.IsFinite(degrees)) return RenderRotation.None;
        // The remainder is within one full turn, so the quarter count fits an int.
        var quarters = Math.Floor(degrees % 360 / 90 + 0.5);
        return FromDegrees((int)quarters * 90);
    }

    /// <summary>Degrees clockwise: 0, 90, 180 or 270.</summary>
    public static int Degrees(this RenderRotation rotation) => rotation switch
    {
        RenderRotation.Clockwise90 => 90,
        RenderRotation.Clockwise180 => 180,
        RenderRotation.Clockwise270 => 270,
        _ => 0,
    };

    /// <summary>Whether the turn swaps width and height: 90 or 270 degrees.</summary>
    public static bool IsQuarterTurn(this RenderRotation rotation) =>
        rotation is RenderRotation.Clockwise90 or RenderRotation.Clockwise270;
}
