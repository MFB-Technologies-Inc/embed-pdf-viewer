using GF = uniffi.epdf_ffi;
using GS = uniffi.epdf_plugin_search;
using GM = uniffi.epdf_plugin_measurement;

namespace EmbedPDF;

internal static partial class Ffi
{
    // Wave D's measure records, which the annotation drafts also write as JSON.

    internal static PdfMeasurement ToPublic(GM.PdfMeasurement m) => new(m.Subtype)
    {
        Ratio = m.Ratio, X = Formats(m.X), Y = Formats(m.Y), Distance = Formats(m.Distance), Area = Formats(m.Area),
        Angle = Formats(m.Angle), Slope = Formats(m.Slope), Origin = m.Origin is { } o ? new MeasureOrigin(o.X, o.Y) : null, Cyx = m.Cyx,
    };

    internal static GM.PdfMeasurement ToFfi(PdfMeasurement m) => m is null ? throw new ArgumentException("A measurement is required.") : new(
        Subtype: m.Subtype ?? throw new ArgumentException("A measurement subtype is required."), Ratio: m.Ratio,
        X: Formats(m.X), Y: Formats(m.Y), Distance: Formats(m.Distance), Area: Formats(m.Area), Angle: Formats(m.Angle), Slope: Formats(m.Slope),
        Origin: m.Origin is { } o ? new GM.MeasureOrigin(Finite(o.X, "origin x"), Finite(o.Y, "origin y")) : null, Cyx: Finite(m.Cyx, "cyx"));

    static IReadOnlyList<PdfNumberFormat>? Formats(GM.PdfNumberFormat[]? formats) => formats is null ? null : [.. formats.Select(f => new PdfNumberFormat(f.Unit)
    {
        Conversion = f.Conversion, Fraction = f.Fraction is { } fr ? (MeasureFraction)(int)fr : null, Precision = f.Precision, Fixed = f.Fixed,
        Thousands = f.Thousands, Decimal = f.Decimal, PrefixSpacing = f.PrefixSpacing, SuffixSpacing = f.SuffixSpacing,
        LabelPosition = f.LabelPosition is { } lp ? (MeasureLabelPosition)(int)lp : null,
    })];

    static GM.PdfNumberFormat[]? Formats(IReadOnlyList<PdfNumberFormat>? formats) => formats is null ? null : [.. Arguments.NoNullElements(formats, "number formats").Select(f => new GM.PdfNumberFormat(
        Unit: f.Unit ?? throw new ArgumentException("A number format unit is required."), Conversion: Finite(f.Conversion, "conversion"),
        Fraction: f.Fraction is { } fr ? (GM.MeasureFraction)(int)Defined(fr, "fraction") : null, Precision: Finite(f.Precision, "precision"), Fixed: f.Fixed,
        Thousands: f.Thousands, Decimal: f.Decimal, PrefixSpacing: f.PrefixSpacing, SuffixSpacing: f.SuffixSpacing,
        LabelPosition: f.LabelPosition is { } lp ? (GM.MeasureLabelPosition)(int)Defined(lp, "label position") : null))];

    /// <summary><paramref name="value"/> when it is a finite number; <c>invalid-input</c> otherwise, which the engine's JSON can't hold.</summary>
    internal static double Finite(double value, string argument) =>
        double.IsFinite(value) ? value : throw new PdfException("RangeError", PdfErrorCode.InvalidInput, $"{argument} {value} is not a finite number");

    internal static double? Finite(double? value, string argument) => value is { } v ? Finite(v, argument) : null;

    internal static GM.CreateMeasurementInput ToFfi(CreateMeasurementInput r) => r is null ? throw new ArgumentException("A create measurement input is required.") : new(Kind: (GM.MeasurementKind)(int)Defined(r.Kind, "kind"), Page: ToFfi(r.Page), Points: [.. r.Points.Select(x0 => ToFfi(x0))], Tool: r.Tool);

    internal static GM.MeasurementCalibrateInput ToFfi(MeasurementCalibrateInput r) => r is null ? throw new ArgumentException("A measurement calibrate input is required.") : new(Page: ToFfi(r.Page), From: ToFfi(r.From), To: ToFfi(r.To), Value: r.Value, Unit: r.Unit ?? throw new ArgumentException("unit is required."));

    internal static GM.MeasurementCalibrateOptions ToFfi(MeasurementCalibrateOptions r) => r is null ? throw new ArgumentException("A measurement calibrate options is required.") : new(Recalculate: r.Recalculate, ApplyTo: r.ApplyTo is null ? null : ToFfi(r.ApplyTo));

    internal static MeasurementCalibrationRequest ToPublic(GM.MeasurementCalibrationRequest r) => new(ToPublic(r.Page), ToPublic(r.From), ToPublic(r.To), r.UserSpaceLength);

    internal static MeasurementErrorInfo ToPublic(GM.MeasurementErrorInfo r) => new(r.Code, r.Detail);

    internal static MeasurementPageScale ToPublic(GM.MeasurementPageScale r) => new(r.Measure is null ? null : ToPublic(r.Measure), (MeasurementScaleSource)(int)r.Source, r.Ready, r.Persistent, r.Error is null ? null : ToPublic(r.Error));

    internal static MeasurementReadout ToPublic(GM.MeasurementReadout r) => new((MeasurementReadoutKind)(int)r.Kind, r.Label, r.Value, r.Perimeter);

    internal static GM.MeasurementScaleChangeOptions ToFfi(MeasurementScaleChangeOptions r) => r is null ? throw new ArgumentException("A measurement scale change options is required.") : new(Recalculate: r.Recalculate);

    internal static MeasurementSettings ToPublic(GM.MeasurementSettings r) => new(ToPublic(r.DefaultScale), [.. r.Presets.Select(x0 => ToPublic(x0))]);

    internal static ScaleFailedAnnotation ToPublic(GM.ScaleFailedAnnotation r) => new(r.AnnotationJson, ToPublic(r.Error));

    internal static ScalePreset ToPublic(GM.ScalePreset r) => new(r.Id, r.Label, r.Paper, r.Real, r.Unit);

    internal static GM.ScalePreset ToFfi(ScalePreset r) => r is null ? throw new ArgumentException("A scale preset is required.") : new(Id: r.Id ?? throw new ArgumentException("id is required."), Label: r.Label ?? throw new ArgumentException("label is required."), Paper: r.Paper, Real: r.Real, Unit: r.Unit ?? throw new ArgumentException("unit is required."));

    internal static ScaleRecalibration ToPublic(GM.ScaleRecalibration r) => new(ToPublic(r.Page), r.Scale is null ? null : ToPublic(r.Scale), r.Error is null ? null : ToPublic(r.Error), [.. r.UpdatedJson.Select(x0 => x0)], [.. r.Skipped.Select(x0 => ToPublic(x0))], [.. r.Failed.Select(x0 => ToPublic(x0))]);

    internal static ScaleSkippedAnnotation ToPublic(GM.ScaleSkippedAnnotation r) => new(r.AnnotationJson, (ScaleSkipReason)(int)r.Reason);

    internal static MeasurementDefaultScale ToPublic(GM.MeasurementDefaultScale u) => u switch
    {
        GM.MeasurementDefaultScale.Metric => new MeasurementDefaultScale.Metric(),
        GM.MeasurementDefaultScale.Imperial => new MeasurementDefaultScale.Imperial(),
        GM.MeasurementDefaultScale.Measure c => new MeasurementDefaultScale.Measure(ToPublic(c.MeasureValue)),
        _ => throw Unknown(u),
    };

    internal static GM.MeasurementDefaultScale ToFfi(MeasurementDefaultScale u) => u switch
    {
        MeasurementDefaultScale.Metric => new GM.MeasurementDefaultScale.Metric(),
        MeasurementDefaultScale.Imperial => new GM.MeasurementDefaultScale.Imperial(),
        MeasurementDefaultScale.Measure c => new GM.MeasurementDefaultScale.Measure(ToFfi(c.MeasureValue)),
        null => throw new ArgumentException("A measurement default scale is required."),
        _ => throw Unknown(u),
    };

    internal static MeasurementEvent ToPublic(GM.MeasurementEvent u) => u switch
    {
        GM.MeasurementEvent.ScaleChanged c => new MeasurementEvent.ScaleChanged(ToPublic(c.Page), ToPublic(c.Report)),
        GM.MeasurementEvent.CalibrationRequested c => new MeasurementEvent.CalibrationRequested(ToPublic(c.Request)),
        GM.MeasurementEvent.CalibrationCompleted c => new MeasurementEvent.CalibrationCompleted(ToPublic(c.Page)),
        GM.MeasurementEvent.CalibrationDismissed => new MeasurementEvent.CalibrationDismissed(),
        _ => throw Unknown(u),
    };

    internal static GM.MeasurementPageTarget ToFfi(MeasurementPageTarget u) => u switch
    {
        MeasurementPageTarget.Page c => new GM.MeasurementPageTarget.Page(ToFfi(c.PageValue)),
        MeasurementPageTarget.Pages c => new GM.MeasurementPageTarget.Pages([.. c.PagesValue.Select(x0 => ToFfi(x0))]),
        MeasurementPageTarget.All => new GM.MeasurementPageTarget.All(),
        null => throw new ArgumentException("A measurement page target is required."),
        _ => throw Unknown(u),
    };

    internal static MeasurementReading ToPublic(GM.MeasurementReading u) => u switch
    {
        GM.MeasurementReading.Value c => new MeasurementReading.Value(ToPublic(c.Readout)),
        GM.MeasurementReading.Unavailable c => new MeasurementReading.Unavailable((MeasurementUnavailable)(int)c.Reason),
        _ => throw Unknown(u),
    };

    internal static ScaleChangeReport ToPublic(GM.ScaleChangeReport u) => u switch
    {
        GM.ScaleChangeReport.Recalibrated c => new ScaleChangeReport.Recalibrated(ToPublic(c.Report)),
        GM.ScaleChangeReport.ScaleFailed c => new ScaleChangeReport.ScaleFailed(ToPublic(c.Page), ToPublic(c.Error)),
        _ => throw Unknown(u),
    };
}
