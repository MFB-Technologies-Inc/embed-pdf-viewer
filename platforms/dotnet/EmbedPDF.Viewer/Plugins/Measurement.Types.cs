using System.Text.Json.Nodes;

namespace EmbedPDF;

/// <summary>A measure's origin, in page space.</summary>
/// <param name="X">Left.</param>
/// <param name="Y">Top.</param>
public readonly record struct MeasureOrigin(double X, double Y);

/// <summary>How a measurement writes one unit of its scale chain (<c>/NumberFormat</c>).</summary>
/// <param name="Unit">The unit label.</param>
public sealed record PdfNumberFormat(string Unit)
{
    /// <summary>The factor from the previous unit.</summary>
    public double? Conversion { get; init; }
    /// <summary>How fractions show.</summary>
    public MeasureFraction? Fraction { get; init; }
    /// <summary>Decimal places or the fraction's denominator.</summary>
    public double? Precision { get; init; }
    /// <summary>Whether trailing zeros stay.</summary>
    public bool? Fixed { get; init; }
    /// <summary>The thousands separator.</summary>
    public string? Thousands { get; init; }
    /// <summary>The decimal separator.</summary>
    public string? Decimal { get; init; }
    /// <summary>Text before the label.</summary>
    public string? PrefixSpacing { get; init; }
    /// <summary>Text after the label.</summary>
    public string? SuffixSpacing { get; init; }
    /// <summary>Where the label goes.</summary>
    public MeasureLabelPosition? LabelPosition { get; init; }
}

/// <summary>A measurement's scale (<c>/Measure</c>), as a dimension line, polygon or polyline carries it.</summary>
/// <param name="Subtype">The measure's subtype, <c>RL</c> for a rectilinear measure.</param>
public sealed record PdfMeasurement(string Subtype)
{
    /// <summary>The scale ratio text, such as <c>1 in = 1 ft</c>.</summary>
    public string? Ratio { get; init; }
    /// <summary>The x-axis units.</summary>
    public IReadOnlyList<PdfNumberFormat>? X { get; init; }
    /// <summary>The y-axis units.</summary>
    public IReadOnlyList<PdfNumberFormat>? Y { get; init; }
    /// <summary>The distance units.</summary>
    public IReadOnlyList<PdfNumberFormat>? Distance { get; init; }
    /// <summary>The area units.</summary>
    public IReadOnlyList<PdfNumberFormat>? Area { get; init; }
    /// <summary>The angle units.</summary>
    public IReadOnlyList<PdfNumberFormat>? Angle { get; init; }
    /// <summary>The slope units.</summary>
    public IReadOnlyList<PdfNumberFormat>? Slope { get; init; }
    /// <summary>The origin.</summary>
    public MeasureOrigin? Origin { get; init; }
    /// <summary>The y-to-x factor.</summary>
    public double? Cyx { get; init; }

    internal JsonNode ToNode(string key)
    {
        WireNumberFormat[]? Formats(IReadOnlyList<PdfNumberFormat>? list) => list?.Select(f => new WireNumberFormat(
            (f ?? throw new FieldBagException($"{key} holds a null number format")).Unit ?? throw new FieldBagException($"{key} has a format without a unit"),
            Finite(f.Conversion), f.Fraction is { } fr ? Wire<MeasureFraction>.Name(fr, key) : null, Finite(f.Precision), f.Fixed, f.Thousands,
            f.Decimal, f.PrefixSpacing, f.SuffixSpacing, f.LabelPosition is { } lp ? Wire<MeasureLabelPosition>.Name(lp, key) : null)).ToArray();
        double? Finite(double? v) => v is { } d && !double.IsFinite(d) ? throw new FieldBagException($"{key} holds {d}, which JSON can't hold") : v;
        return AnnotationWire.Serialize(new WireMeasurement(
            Subtype ?? throw new FieldBagException($"{key} has no subtype"), Ratio, Formats(X), Formats(Y), Formats(Distance), Formats(Area),
            Formats(Angle), Formats(Slope), Origin is { } o ? new WireOrigin(Finite(o.X)!.Value, Finite(o.Y)!.Value) : null, Finite(Cyx)));
    }

    internal static PdfMeasurement Read(JsonNode node, string key)
    {
        var w = AnnotationWire.ReadMeasurement(node, key);
        IReadOnlyList<PdfNumberFormat>? Formats(WireNumberFormat[]? list) => list?.Select(f => new PdfNumberFormat(f.Unit)
        {
            Conversion = f.Conversion,
            Fraction = f.Fraction is { } fr ? (Wire<MeasureFraction>.TryParse(fr, out var v) ? v : throw new FieldBagException($"{key} has fraction {fr}")) : null,
            Precision = f.Precision,
            Fixed = f.Fixed,
            Thousands = f.Thousands,
            Decimal = f.Decimal,
            PrefixSpacing = f.PrefixSpacing,
            SuffixSpacing = f.SuffixSpacing,
            LabelPosition = f.LabelPosition is { } lp ? (Wire<MeasureLabelPosition>.TryParse(lp, out var p) ? p : throw new FieldBagException($"{key} has label position {lp}")) : null,
        }).ToArray();
        return new(w.Subtype)
        {
            Ratio = w.Ratio,
            X = Formats(w.X),
            Y = Formats(w.Y),
            Distance = Formats(w.Distance),
            Area = Formats(w.Area),
            Angle = Formats(w.Angle),
            Slope = Formats(w.Slope),
            Origin = w.Origin is { } o ? new MeasureOrigin(o.X, o.Y) : null,
            Cyx = w.Cyx,
        };
    }
}
