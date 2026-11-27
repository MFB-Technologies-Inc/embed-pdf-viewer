using System.Text.Json;
using System.Text.Json.Nodes;

namespace EmbedPDF;

// The typed members of each subtype AnnotationInfo reads: the engine's read
// record for the kind (`crates/epdf-engine/src/features/annotation/read/
// kinds.rs`). A member whose read vocabulary is the write vocabulary takes the
// drafts' enum; border styles and intents take the open read types, since a
// read can name a value a write refuses.

/// <summary>A subtype's typed members, as <see cref="AnnotationInfo.Details"/> holds them.</summary>
public abstract record AnnotationDetails
{
    private protected AnnotationDetails() { }

    internal static AnnotationDetails? Read(AnnotationSubtype subtype, FieldReader r)
    {
        try
        {
            return subtype.Value switch
            {
                "ink" => AnnotationInkDetails.Read(r),
                "highlight" => AnnotationHighlightDetails.Read(r),
                "text" => AnnotationNoteDetails.Read(r),
                "underline" or "squiggly" => AnnotationTextMarkupDetails.Read(r),
                "strikeout" => AnnotationStrikeoutDetails.Read(r),
                "square" or "circle" => AnnotationShapeDetails.Read(r),
                "line" => AnnotationLineDetails.Read(r),
                "polygon" => AnnotationPolygonDetails.Read(r),
                "polyline" => AnnotationPolylineDetails.Read(r),
                "free-text" => AnnotationFreeTextDetails.Read(r),
                "caret" => AnnotationCaretDetails.Read(r),
                "stamp" => AnnotationStampDetails.Read(r),
                "link" => AnnotationLinkDetails.Read(r),
                "file-attachment" => AnnotationFileAttachmentDetails.Read(r),
                "redact" => AnnotationRedactionDetails.Read(r),
                _ => null,
            };
        }
        catch (Exception e) when (e is FieldBagException or JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            return null;
        }
    }
}

internal static class DetailReads
{
    static FieldBagException Missing(string key) => new($"{key} is required");

    internal static T Req<T>(T? value, string key) where T : struct => value ?? throw Missing(key);

    internal static T Req<T>(T? value, string key) where T : class => value ?? throw Missing(key);

    internal static AnnotationBorderStyleName BorderStyle(this FieldReader r) => new(Req(r.String("borderStyle"), "borderStyle"));

    internal static AnnotationIntent? Intent(this FieldReader r) => r.String("intent") is { } s ? new AnnotationIntent(s) : (AnnotationIntent?)null;

    internal static AnnotationRichText? ReadRichText(this FieldReader r) => r.Value("richText", AnnotationRichText.Read);

    internal static IReadOnlyList<double>? Dashes(this FieldReader r) => r.Value("dashArray", AnnotationWire.ReadNumbers);

    internal static PdfMeasurement? ReadMeasure(this FieldReader r) => r.Value("measure", PdfMeasurement.Read);

    internal static PdfPoint? Point(this FieldReader r, string key) => r.Struct(key, AnnotationWire.ReadPoint);
}

/// <summary>An ink annotation's members.</summary>
public sealed record AnnotationInkDetails(
    AnnotationColor? Color, double Opacity, double StrokeWidth, AnnotationBorderStyleName BorderStyle, IReadOnlyList<double>? DashArray,
    IReadOnlyList<IReadOnlyList<PdfPoint>> InkList, AnnotationIntent? Intent, double? Rotation, AnnotationRichText? RichText) : AnnotationDetails
{
    internal static AnnotationInkDetails Read(FieldReader r) => new(
        r.Color("color"), DetailReads.Req(r.Double("opacity"), "opacity"), DetailReads.Req(r.Double("strokeWidth"), "strokeWidth"), r.BorderStyle(),
        r.Dashes(), DetailReads.Req(r.Value("inkList", AnnotationWire.ReadPointLists), "inkList"), r.Intent(), r.Double("rotation"), r.ReadRichText());
}

/// <summary>A highlight's members.</summary>
public sealed record AnnotationHighlightDetails(AnnotationColor Color, double Opacity, IReadOnlyList<PdfQuad> QuadPoints, AnnotationRichText? RichText) : AnnotationDetails
{
    internal static AnnotationHighlightDetails Read(FieldReader r) => new(
        DetailReads.Req(r.Color("color"), "color"), DetailReads.Req(r.Double("opacity"), "opacity"), DetailReads.Req(r.Quads(), "quadPoints"), r.ReadRichText());
}

/// <summary>A note's members.</summary>
public sealed record AnnotationNoteDetails(
    AnnotationColor Color, double Opacity, string Icon, bool Open, string? State, string? StateModel, AnnotationRichText? RichText) : AnnotationDetails
{
    internal static AnnotationNoteDetails Read(FieldReader r) => new(
        DetailReads.Req(r.Color("color"), "color"), DetailReads.Req(r.Double("opacity"), "opacity"), DetailReads.Req(r.String("icon"), "icon"),
        DetailReads.Req(r.Bool("open"), "open"), r.String("state"), r.String("stateModel"), r.ReadRichText());
}

/// <summary>An underline's or squiggly underline's members.</summary>
public sealed record AnnotationTextMarkupDetails(AnnotationColor Color, double Opacity, IReadOnlyList<PdfQuad> QuadPoints, AnnotationRichText? RichText) : AnnotationDetails
{
    internal static AnnotationTextMarkupDetails Read(FieldReader r) => new(
        DetailReads.Req(r.Color("color"), "color"), DetailReads.Req(r.Double("opacity"), "opacity"), DetailReads.Req(r.Quads(), "quadPoints"), r.ReadRichText());
}

/// <summary>A strikeout's members.</summary>
public sealed record AnnotationStrikeoutDetails(
    AnnotationColor Color, double Opacity, IReadOnlyList<PdfQuad> QuadPoints, AnnotationIntent? Intent, AnnotationRichText? RichText) : AnnotationDetails
{
    internal static AnnotationStrikeoutDetails Read(FieldReader r) => new(
        DetailReads.Req(r.Color("color"), "color"), DetailReads.Req(r.Double("opacity"), "opacity"), DetailReads.Req(r.Quads(), "quadPoints"), r.Intent(),
        r.ReadRichText());
}

/// <summary>A square's or circle's members.</summary>
public sealed record AnnotationShapeDetails(
    AnnotationColor? Color, double Opacity, double StrokeWidth, AnnotationBorderStyleName BorderStyle, IReadOnlyList<double>? DashArray,
    AnnotationColor? InteriorColor, PdfRect Box, double? Rotation, double? CloudyIntensity, AnnotationRichText? RichText) : AnnotationDetails
{
    internal static AnnotationShapeDetails Read(FieldReader r) => new(
        r.Color("color"), DetailReads.Req(r.Double("opacity"), "opacity"), DetailReads.Req(r.Double("strokeWidth"), "strokeWidth"), r.BorderStyle(),
        r.Dashes(), r.Color("interiorColor"), DetailReads.Req(r.Rect("box"), "box"), r.Double("rotation"), r.Double("cloudyIntensity"), r.ReadRichText());
}

/// <summary>A line's members.</summary>
public sealed record AnnotationLineDetails(
    AnnotationColor? Color, double Opacity, double StrokeWidth, AnnotationBorderStyleName BorderStyle, IReadOnlyList<double>? DashArray,
    AnnotationColor? InteriorColor, PdfPoint Start, PdfPoint End, AnnotationLineEndings LineEndings, double? Rotation, AnnotationIntent? Intent,
    bool CaptionEnabled, AnnotationCaptionPosition CaptionPosition, AnnotationCaptionOffset? CaptionOffset, AnnotationLineLeader? Leader,
    AnnotationRichText? RichText, PdfMeasurement? Measure) : AnnotationDetails
{
    internal static AnnotationLineDetails Read(FieldReader r)
    {
        var (start, end) = AnnotationWire.ReadLinePoints(r.Node("linePoints") ?? throw new FieldBagException("linePoints is required"), "linePoints");
        return new(
            r.Color("color"), DetailReads.Req(r.Double("opacity"), "opacity"), DetailReads.Req(r.Double("strokeWidth"), "strokeWidth"), r.BorderStyle(),
            r.Dashes(), r.Color("interiorColor"), start, end, DetailReads.Req(r.LineEndings(), "lineEndings"), r.Double("rotation"), r.Intent(),
            DetailReads.Req(r.Bool("captionEnabled"), "captionEnabled"), DetailReads.Req(r.Enum<AnnotationCaptionPosition>("captionPosition"), "captionPosition"),
            r.Node("captionOffset") is { } o ? new AnnotationCaptionOffset(AnnotationWire.ReadCaptionOffset(o, "captionOffset").Along, AnnotationWire.ReadCaptionOffset(o, "captionOffset").Perpendicular) : null,
            r.Node("leader") is { } l ? ToLeader(AnnotationWire.ReadLineLeader(l, "leader")) : null,
            r.ReadRichText(), r.ReadMeasure());
    }

    static AnnotationLineLeader ToLeader(WireLineLeader l) => new(l.Length) { Extension = l.Extension, Offset = l.Offset };
}

/// <summary>A polygon's members.</summary>
public sealed record AnnotationPolygonDetails(
    AnnotationColor? Color, double Opacity, double StrokeWidth, AnnotationBorderStyleName BorderStyle, IReadOnlyList<double>? DashArray,
    AnnotationColor? InteriorColor, IReadOnlyList<PdfPoint> Vertices, double? Rotation, AnnotationIntent? Intent, double? CloudyIntensity,
    bool? CaptionEnabled, PdfPoint? CaptionCenter, AnnotationRichText? RichText, PdfMeasurement? Measure) : AnnotationDetails
{
    internal static AnnotationPolygonDetails Read(FieldReader r) => new(
        r.Color("color"), DetailReads.Req(r.Double("opacity"), "opacity"), DetailReads.Req(r.Double("strokeWidth"), "strokeWidth"), r.BorderStyle(),
        r.Dashes(), r.Color("interiorColor"), DetailReads.Req(r.Points("vertices"), "vertices"), r.Double("rotation"), r.Intent(), r.Double("cloudyIntensity"),
        r.Bool("captionEnabled"), r.Point("captionCenter"), r.ReadRichText(), r.ReadMeasure());
}

/// <summary>A polyline's members.</summary>
public sealed record AnnotationPolylineDetails(
    AnnotationColor? Color, double Opacity, double StrokeWidth, AnnotationBorderStyleName BorderStyle, IReadOnlyList<double>? DashArray,
    AnnotationColor? InteriorColor, IReadOnlyList<PdfPoint> Vertices, AnnotationLineEndings LineEndings, double? Rotation, AnnotationIntent? Intent,
    bool? CaptionEnabled, PdfPoint? CaptionCenter, AnnotationRichText? RichText, PdfMeasurement? Measure) : AnnotationDetails
{
    internal static AnnotationPolylineDetails Read(FieldReader r) => new(
        r.Color("color"), DetailReads.Req(r.Double("opacity"), "opacity"), DetailReads.Req(r.Double("strokeWidth"), "strokeWidth"), r.BorderStyle(),
        r.Dashes(), r.Color("interiorColor"), DetailReads.Req(r.Points("vertices"), "vertices"), DetailReads.Req(r.LineEndings(), "lineEndings"),
        r.Double("rotation"), r.Intent(), r.Bool("captionEnabled"), r.Point("captionCenter"), r.ReadRichText(), r.ReadMeasure());
}

/// <summary>A free text's members.</summary>
public sealed record AnnotationFreeTextDetails(
    PdfRect Box, AnnotationIntent Intent, string FontFamily, double FontSize, AnnotationColor FontColor, AnnotationTextAlignment TextAlign,
    AnnotationVerticalAlignment VerticalAlign, AnnotationColor Color, AnnotationColor? InteriorColor, double Opacity, double StrokeWidth,
    AnnotationBorderStyleName BorderStyle, IReadOnlyList<double>? DashArray, double? CloudyIntensity, double? Rotation,
    AnnotationCalloutLine? CalloutLine, AnnotationLineEnding? LineEnding, AnnotationRichText? RichText) : AnnotationDetails
{
    internal static AnnotationFreeTextDetails Read(FieldReader r) => new(
        DetailReads.Req(r.Rect("box"), "box"), DetailReads.Req(r.Intent(), "intent"), DetailReads.Req(r.String("fontFamily"), "fontFamily"),
        DetailReads.Req(r.Double("fontSize"), "fontSize"), DetailReads.Req(r.Color("fontColor"), "fontColor"),
        DetailReads.Req(r.Enum<AnnotationTextAlignment>("textAlign"), "textAlign"), DetailReads.Req(r.Enum<AnnotationVerticalAlignment>("verticalAlign"), "verticalAlign"),
        DetailReads.Req(r.Color("color"), "color"), r.Color("interiorColor"), DetailReads.Req(r.Double("opacity"), "opacity"),
        DetailReads.Req(r.Double("strokeWidth"), "strokeWidth"), r.BorderStyle(), r.Dashes(), r.Double("cloudyIntensity"), r.Double("rotation"),
        r.Points("calloutLine") is { } points ? AnnotationCalloutLine.FromPoints(points) ?? throw new FieldBagException("a callout line has two or three points") : null,
        r.Enum<AnnotationLineEnding>("lineEnding"), r.ReadRichText());
}

/// <summary>A caret's members.</summary>
public sealed record AnnotationCaretDetails(
    AnnotationColor Color, double Opacity, PdfRect Box, double? Rotation, AnnotationIntent? Intent, AnnotationRichText? RichText) : AnnotationDetails
{
    internal static AnnotationCaretDetails Read(FieldReader r) => new(
        DetailReads.Req(r.Color("color"), "color"), DetailReads.Req(r.Double("opacity"), "opacity"), DetailReads.Req(r.Rect("box"), "box"),
        r.Double("rotation"), r.Intent(), r.ReadRichText());
}

/// <summary>A stamp's members.</summary>
public sealed record AnnotationStampDetails(
    string? Name, AnnotationStampFit? Fit, double Opacity, PdfRect Box, double? Rotation, AnnotationRichText? RichText) : AnnotationDetails
{
    internal static AnnotationStampDetails Read(FieldReader r) => new(
        r.String("name"), r.Enum<AnnotationStampFit>("fit"), DetailReads.Req(r.Double("opacity"), "opacity"), DetailReads.Req(r.Rect("box"), "box"),
        r.Double("rotation"), r.ReadRichText());
}

/// <summary>A link's members. A target a draft can't write (another file, a launch, a script) is <see langword="null"/>; read it from <see cref="AnnotationInfo.FieldsJson"/>.</summary>
public sealed record AnnotationLinkDetails(AnnotationLinkTarget? Target) : AnnotationDetails
{
    internal static AnnotationLinkDetails Read(FieldReader r)
    {
        AnnotationLinkTarget? target = null;
        try
        {
            target = r.Value("target", AnnotationLinkTarget.Read);
        }
        catch (Exception e) when (e is FieldBagException or JsonException or InvalidOperationException or OverflowException)
        {
        }
        return new(target);
    }
}

/// <summary>The file a file attachment carries.</summary>
public sealed record AnnotationAttachedFile(
    string Name, string? MimeType, string? Description, long? SizeBytes, string? Md5, string? RawCreatedAt, string? RawModifiedAt)
{
    /// <summary>When the file was created, if <see cref="RawCreatedAt"/> reads as a date.</summary>
    public DateTimeOffset? CreatedAt => RawCreatedAt is null ? null : AnnotationInfo.Instant(RawCreatedAt);

    /// <summary>When the file last changed, if <see cref="RawModifiedAt"/> reads as a date.</summary>
    public DateTimeOffset? ModifiedAt => RawModifiedAt is null ? null : AnnotationInfo.Instant(RawModifiedAt);

    internal static AnnotationAttachedFile Read(JsonNode node, string key)
    {
        var f = AnnotationWire.ReadAttachedFile(node, key);
        return new(f.Name, f.MimeType, f.Description, f.SizeBytes, f.Md5, f.CreatedAt, f.ModifiedAt);
    }
}

/// <summary>A file attachment's members.</summary>
public sealed record AnnotationFileAttachmentDetails(
    AnnotationFileAttachmentIcon Icon, AnnotationColor Color, double Opacity, AnnotationAttachedFile? File, AnnotationRichText? RichText) : AnnotationDetails
{
    internal static AnnotationFileAttachmentDetails Read(FieldReader r) => new(
        DetailReads.Req(r.Enum<AnnotationFileAttachmentIcon>("icon"), "icon"), DetailReads.Req(r.Color("color"), "color"),
        DetailReads.Req(r.Double("opacity"), "opacity"), r.Value("file", AnnotationAttachedFile.Read), r.ReadRichText());
}

/// <summary>A redaction mark's members.</summary>
public sealed record AnnotationRedactionDetails(
    IReadOnlyList<PdfQuad> QuadPoints, AnnotationColor Color, double Opacity, AnnotationColor? InteriorColor, string? OverlayText, bool Repeat,
    string FontFamily, double FontSize, AnnotationColor FontColor, AnnotationTextAlignment TextAlign, AnnotationRichText? RichText) : AnnotationDetails
{
    internal static AnnotationRedactionDetails Read(FieldReader r) => new(
        DetailReads.Req(r.Quads(), "quadPoints"), DetailReads.Req(r.Color("color"), "color"), DetailReads.Req(r.Double("opacity"), "opacity"),
        r.Color("interiorColor"), r.String("overlayText"), DetailReads.Req(r.Bool("repeat"), "repeat"), DetailReads.Req(r.String("fontFamily"), "fontFamily"),
        DetailReads.Req(r.Double("fontSize"), "fontSize"), DetailReads.Req(r.Color("fontColor"), "fontColor"),
        DetailReads.Req(r.Enum<AnnotationTextAlignment>("textAlign"), "textAlign"), r.ReadRichText());
}
