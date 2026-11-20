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
/// <summary>A create measurement input.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Page">The page.</param>
/// <param name="Points">Page-space points: two for a distance, the vertices otherwise.</param>
public sealed record CreateMeasurementInput(MeasurementKind Kind, PageRef Page, IReadOnlyList<PdfPoint> Points)
{
    /// <summary>The authoring tool whose defaults apply. <see langword="null"/> is the kind's own.</summary>
    public string? Tool { get; init; }
}

/// <summary>Two page points and the real length between them, value in unit.</summary>
/// <param name="Page">The page.</param>
/// <param name="From">The from.</param>
/// <param name="To">The to.</param>
/// <param name="Value">The value.</param>
/// <param name="Unit">The unit.</param>
public sealed record MeasurementCalibrateInput(PageRef Page, PdfPoint From, PdfPoint To, double Value, string Unit);

/// <summary>A measurement calibrate options.</summary>
public sealed record MeasurementCalibrateOptions
{
    /// <summary>Re-measure the pages' measurement annotations. <see langword="null"/> is true.</summary>
    public bool? Recalculate { get; init; }
    /// <summary>Write the scale to these pages. <see langword="null"/> is the input's page.</summary>
    public MeasurementPageTarget? ApplyTo { get; init; }
}

/// <summary>Two points the calibrate tool captured, awaiting the real length. Page space.</summary>
/// <param name="Page">The page.</param>
/// <param name="From">The from.</param>
/// <param name="To">The to.</param>
/// <param name="UserSpaceLength">The captured length in PDF user space.</param>
public sealed record MeasurementCalibrationRequest(PageRef Page, PdfPoint From, PdfPoint To, double UserSpaceLength);

/// <summary>An engine error as the plugin keeps it (SerializedEngineError).</summary>
/// <param name="Code">The code.</param>
/// <param name="Detail">The detail.</param>
public sealed record MeasurementErrorInfo(string Code, string Detail);

/// <summary>A page's resolved scale.</summary>
/// <param name="Measure">The measure.</param>
/// <param name="Source">The source.</param>
/// <param name="Ready">The page's viewports are known: always, for a page of an open document (its layout carries them).</param>
/// <param name="Persistent">The engine can persist a scale into the document.</param>
/// <param name="Error">The error.</param>
public sealed record MeasurementPageScale(PdfMeasurement? Measure, MeasurementScaleSource Source, bool Ready, bool Persistent, MeasurementErrorInfo? Error);

/// <summary>A value in the scale's first format unit, and its label.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Label">The label.</param>
/// <param name="Value">The value.</param>
/// <param name="Perimeter">The perimeter.</param>
public sealed record MeasurementReadout(MeasurementReadoutKind Kind, string Label, double Value, string? Perimeter);

/// <summary>ScaleChangeOptions without the signal, which the handle's CancelToken carries.</summary>
public sealed record MeasurementScaleChangeOptions
{
    /// <summary>Re-measure the pages' measurement annotations. <see langword="null"/> is true.</summary>
    public bool? Recalculate { get; init; }
}

/// <summary>A measurement settings.</summary>
/// <param name="DefaultScale">The default scale.</param>
/// <param name="Presets">The presets.</param>
public sealed record MeasurementSettings(MeasurementDefaultScale DefaultScale, IReadOnlyList<ScalePreset> Presets);

/// <summary>A scale failed annotation.</summary>
/// <param name="AnnotationJson">The AnnotationRef record.</param>
/// <param name="Error">The error.</param>
public sealed record ScaleFailedAnnotation(string AnnotationJson, MeasurementErrorInfo Error);

/// <summary>A scale preset.</summary>
/// <param name="Id">The id.</param>
/// <param name="Label">The label.</param>
/// <param name="Paper">The paper.</param>
/// <param name="Real">The real.</param>
/// <param name="Unit">The unit.</param>
public sealed record ScalePreset(string Id, string Label, double Paper, double Real, string Unit);

/// <summary>One page's re-measurement after a scale change.</summary>
/// <param name="Page">The page.</param>
/// <param name="Scale">The scale.</param>
/// <param name="Error">The error.</param>
/// <param name="UpdatedJson">The AnnotationRef records re-measured.</param>
/// <param name="Skipped">The skipped.</param>
/// <param name="Failed">The failed.</param>
public sealed record ScaleRecalibration(PageRef Page, PdfMeasurement? Scale, MeasurementErrorInfo? Error, IReadOnlyList<string> UpdatedJson, IReadOnlyList<ScaleSkippedAnnotation> Skipped, IReadOnlyList<ScaleFailedAnnotation> Failed);

/// <summary>A scale skipped annotation.</summary>
/// <param name="AnnotationJson">The AnnotationRef record.</param>
/// <param name="Reason">The reason.</param>
public sealed record ScaleSkippedAnnotation(string AnnotationJson, ScaleSkipReason Reason);

/// <summary>The fallback scale for a page with no viewport.</summary>
public abstract record MeasurementDefaultScale
{
    private protected MeasurementDefaultScale() { }

    /// <summary>Metric.</summary>
    public sealed record Metric : MeasurementDefaultScale;

    /// <summary>Imperial.</summary>
    public sealed record Imperial : MeasurementDefaultScale;

    /// <summary>Measure.</summary>
    /// <param name="MeasureValue">The measure value.</param>
    public sealed record Measure(PdfMeasurement MeasureValue) : MeasurementDefaultScale;

}

/// <summary>A measurement event.</summary>
public abstract record MeasurementEvent
{
    private protected MeasurementEvent() { }

    /// <summary>Scale changed.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="Report">The report.</param>
    public sealed record ScaleChanged(PageRef Page, ScaleChangeReport Report) : MeasurementEvent;

    /// <summary>Calibration requested.</summary>
    /// <param name="Request">The request.</param>
    public sealed record CalibrationRequested(MeasurementCalibrationRequest Request) : MeasurementEvent;

    /// <summary>Calibration completed.</summary>
    /// <param name="Page">The page.</param>
    public sealed record CalibrationCompleted(PageRef Page) : MeasurementEvent;

    /// <summary>Calibration dismissed.</summary>
    public sealed record CalibrationDismissed : MeasurementEvent;

}

/// <summary>A measurement page target.</summary>
public abstract record MeasurementPageTarget
{
    private protected MeasurementPageTarget() { }

    /// <summary>Page.</summary>
    /// <param name="PageValue">The page value.</param>
    public sealed record Page(PageRef PageValue) : MeasurementPageTarget;

    /// <summary>Pages.</summary>
    /// <param name="PagesValue">The pages value.</param>
    public sealed record Pages(IReadOnlyList<PageRef> PagesValue) : MeasurementPageTarget;

    /// <summary>All.</summary>
    public sealed record All : MeasurementPageTarget;

}

/// <summary>A measurement reading.</summary>
public abstract record MeasurementReading
{
    private protected MeasurementReading() { }

    /// <summary>Value.</summary>
    /// <param name="Readout">The readout.</param>
    public sealed record Value(MeasurementReadout Readout) : MeasurementReading;

    /// <summary>Unavailable.</summary>
    /// <param name="Reason">The reason.</param>
    public sealed record Unavailable(MeasurementUnavailable Reason) : MeasurementReading;

}

/// <summary>A scale change report.</summary>
public abstract record ScaleChangeReport
{
    private protected ScaleChangeReport() { }

    /// <summary>Recalibrated.</summary>
    /// <param name="Report">The report.</param>
    public sealed record Recalibrated(ScaleRecalibration Report) : ScaleChangeReport;

    /// <summary>The page's viewport write failed; nothing on it was re-measured.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="Error">The error.</param>
    public sealed record ScaleFailed(PageRef Page, MeasurementErrorInfo Error) : ScaleChangeReport;

}

/// <summary>A measurement kind.</summary>
public enum MeasurementKind
{
    /// <summary>Distance.</summary>
    Distance,
    /// <summary>Perimeter.</summary>
    Perimeter,
    /// <summary>Area.</summary>
    Area,
}

/// <summary>A measurement readout kind.</summary>
public enum MeasurementReadoutKind
{
    /// <summary>Distance.</summary>
    Distance,
    /// <summary>Perimeter.</summary>
    Perimeter,
    /// <summary>Area.</summary>
    Area,
}

/// <summary>A measurement scale source.</summary>
public enum MeasurementScaleSource
{
    /// <summary>EmbedPDF's own viewport.</summary>
    Owned,
    /// <summary>Another producer's viewport.</summary>
    Foreign,
    /// <summary>No viewport: the configured default.</summary>
    Default,
}

/// <summary>A measurement unavailable.</summary>
public enum MeasurementUnavailable
{
    /// <summary>Not dimension.</summary>
    NotDimension,
    /// <summary>No measure.</summary>
    NoMeasure,
    /// <summary>Foreign measure.</summary>
    ForeignMeasure,
    /// <summary>No scale.</summary>
    NoScale,
    /// <summary>No distance format.</summary>
    NoDistanceFormat,
    /// <summary>No area format.</summary>
    NoAreaFormat,
    /// <summary>Invalid format.</summary>
    InvalidFormat,
    /// <summary>Invalid geometry.</summary>
    InvalidGeometry,
}

/// <summary>A scale skip reason.</summary>
public enum ScaleSkipReason
{
    /// <summary>No authority.</summary>
    NoAuthority,
    /// <summary>Locked.</summary>
    Locked,
    /// <summary>Foreign measure.</summary>
    ForeignMeasure,
    /// <summary>Unavailable.</summary>
    Unavailable,
}
