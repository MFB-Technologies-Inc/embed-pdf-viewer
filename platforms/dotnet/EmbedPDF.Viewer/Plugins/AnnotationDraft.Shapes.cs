namespace EmbedPDF;

// Square, circle, line, polygon and polyline drafts and patches
// (AnnotationDraft.cs has the conventions).

/// <summary>A new square (a rectangle).</summary>
/// <param name="Box">Its box, page space.</param>
public sealed record SquareAnnotationDraft(PdfRect Box) : AnnotationDraft
{
    /// <summary><c>/C</c>. <see cref="JsonNullable{T}.Null"/> draws no border.</summary>
    public JsonNullable<AnnotationColor>? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The border width, 0 or more.</summary>
    public double? StrokeWidth { get; init; }
    /// <summary>The border style.</summary>
    public AnnotationBorderStyle? BorderStyle { get; init; }
    /// <summary>The dash lengths. <see cref="JsonNullable{T}.Null"/> is none.</summary>
    public JsonNullable<IReadOnlyList<double>>? DashArray { get; init; }
    /// <summary><c>/IC</c>, the fill. <see cref="JsonNullable{T}.Null"/> is none.</summary>
    public JsonNullable<AnnotationColor>? InteriorColor { get; init; }
    /// <summary>Degrees. <see cref="JsonNullable{T}.Null"/> is none.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary>The cloudy border's intensity, 0 to 2. <see cref="JsonNullable{T}.Null"/> is a plain border.</summary>
    public JsonNullable<double>? CloudyIntensity { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag) =>
        Shape.Write(bag, "square", Box, Color, Opacity, StrokeWidth, BorderStyle, DashArray, InteriorColor, Rotation, CloudyIntensity, BlendMode, RichText);
}

/// <summary>A change to a square, or to a square tool's defaults.</summary>
public sealed record SquareAnnotationPatch : AnnotationPatch, IAnnotationPatch<SquareAnnotationPatch>
{
    /// <summary>Its box.</summary>
    public PdfRect? Box { get; init; }
    /// <summary><c>/C</c>.</summary>
    public JsonNullable<AnnotationColor>? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The border width.</summary>
    public double? StrokeWidth { get; init; }
    /// <summary>The border style.</summary>
    public AnnotationBorderStyle? BorderStyle { get; init; }
    /// <summary>The dash lengths.</summary>
    public JsonNullable<IReadOnlyList<double>>? DashArray { get; init; }
    /// <summary>The fill.</summary>
    public JsonNullable<AnnotationColor>? InteriorColor { get; init; }
    /// <summary>Degrees.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary>The cloudy border's intensity.</summary>
    public JsonNullable<double>? CloudyIntensity { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag) =>
        Shape.Write(bag, null, Box, Color, Opacity, StrokeWidth, BorderStyle, DashArray, InteriorColor, Rotation, CloudyIntensity, BlendMode, RichText);

    /// <inheritdoc/>
    public static SquareAnnotationPatch FromJson(string json) => Decode(json, r => new SquareAnnotationPatch
    {
        Box = r.Rect("box"),
        Color = r.NullableColor("color"),
        Opacity = r.Double("opacity"),
        StrokeWidth = r.Double("strokeWidth"),
        BorderStyle = r.Enum<AnnotationBorderStyle>("borderStyle"),
        DashArray = r.NullableNumbers("dashArray"),
        InteriorColor = r.NullableColor("interiorColor"),
        Rotation = r.NullableDouble("rotation"),
        CloudyIntensity = r.NullableDouble("cloudyIntensity"),
        BlendMode = r.Enum<AnnotationBlendMode>("blendMode"),
        RichText = r.RichText(),
    });
}

/// <summary>A new circle (an ellipse in its box).</summary>
/// <param name="Box">Its box, page space.</param>
public sealed record CircleAnnotationDraft(PdfRect Box) : AnnotationDraft
{
    /// <summary><c>/C</c>. <see cref="JsonNullable{T}.Null"/> draws no border.</summary>
    public JsonNullable<AnnotationColor>? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The border width, 0 or more.</summary>
    public double? StrokeWidth { get; init; }
    /// <summary>The border style.</summary>
    public AnnotationBorderStyle? BorderStyle { get; init; }
    /// <summary>The dash lengths.</summary>
    public JsonNullable<IReadOnlyList<double>>? DashArray { get; init; }
    /// <summary><c>/IC</c>, the fill.</summary>
    public JsonNullable<AnnotationColor>? InteriorColor { get; init; }
    /// <summary>Degrees.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary>The cloudy border's intensity.</summary>
    public JsonNullable<double>? CloudyIntensity { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag) =>
        Shape.Write(bag, "circle", Box, Color, Opacity, StrokeWidth, BorderStyle, DashArray, InteriorColor, Rotation, CloudyIntensity, BlendMode, RichText);
}

/// <summary>A change to a circle, or to a circle tool's defaults.</summary>
public sealed record CircleAnnotationPatch : AnnotationPatch, IAnnotationPatch<CircleAnnotationPatch>
{
    /// <summary>Its box.</summary>
    public PdfRect? Box { get; init; }
    /// <summary><c>/C</c>.</summary>
    public JsonNullable<AnnotationColor>? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The border width.</summary>
    public double? StrokeWidth { get; init; }
    /// <summary>The border style.</summary>
    public AnnotationBorderStyle? BorderStyle { get; init; }
    /// <summary>The dash lengths.</summary>
    public JsonNullable<IReadOnlyList<double>>? DashArray { get; init; }
    /// <summary>The fill.</summary>
    public JsonNullable<AnnotationColor>? InteriorColor { get; init; }
    /// <summary>Degrees.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary>The cloudy border's intensity.</summary>
    public JsonNullable<double>? CloudyIntensity { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag) =>
        Shape.Write(bag, null, Box, Color, Opacity, StrokeWidth, BorderStyle, DashArray, InteriorColor, Rotation, CloudyIntensity, BlendMode, RichText);

    /// <inheritdoc/>
    public static CircleAnnotationPatch FromJson(string json) => Decode(json, r => new CircleAnnotationPatch
    {
        Box = r.Rect("box"),
        Color = r.NullableColor("color"),
        Opacity = r.Double("opacity"),
        StrokeWidth = r.Double("strokeWidth"),
        BorderStyle = r.Enum<AnnotationBorderStyle>("borderStyle"),
        DashArray = r.NullableNumbers("dashArray"),
        InteriorColor = r.NullableColor("interiorColor"),
        Rotation = r.NullableDouble("rotation"),
        CloudyIntensity = r.NullableDouble("cloudyIntensity"),
        BlendMode = r.Enum<AnnotationBlendMode>("blendMode"),
        RichText = r.RichText(),
    });
}

/// <summary>A new line between two points.</summary>
/// <param name="Start">The first point, page space.</param>
/// <param name="End">The second point, page space.</param>
public sealed record LineAnnotationDraft(PdfPoint Start, PdfPoint End) : AnnotationDraft
{
    /// <summary><c>/C</c>. <see cref="JsonNullable{T}.Null"/> draws no stroke.</summary>
    public JsonNullable<AnnotationColor>? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The line width.</summary>
    public double? StrokeWidth { get; init; }
    /// <summary>The border style.</summary>
    public AnnotationBorderStyle? BorderStyle { get; init; }
    /// <summary>The dash lengths.</summary>
    public JsonNullable<IReadOnlyList<double>>? DashArray { get; init; }
    /// <summary><c>/IC</c>, the fill of closed line endings.</summary>
    public JsonNullable<AnnotationColor>? InteriorColor { get; init; }
    /// <summary>The endings at the two points.</summary>
    public AnnotationLineEndings? LineEndings { get; init; }
    /// <summary>Degrees.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary><see cref="JsonNullable{T}.Null"/> is a plain line.</summary>
    public JsonNullable<AnnotationLineIntent>? Intent { get; init; }
    /// <summary>Whether <c>contents</c> shows as a caption.</summary>
    public bool? CaptionEnabled { get; init; }
    /// <summary>Where the caption sits.</summary>
    public AnnotationCaptionPosition? CaptionPosition { get; init; }
    /// <summary>The caption's displacement.</summary>
    public JsonNullable<AnnotationCaptionOffset>? CaptionOffset { get; init; }
    /// <summary>The leader lines.</summary>
    public JsonNullable<AnnotationLineLeader>? Leader { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }
    /// <summary>The scale of a <see cref="AnnotationLineIntent.LineDimension"/> line.</summary>
    public JsonNullable<PdfMeasurement>? Measure { get; init; }

    internal override void Write(FieldBag bag)
    {
        bag.Put("subtype", "line");
        bag.Set("linePoints", AnnotationWire.LinePoints(Start, End, "linePoints"));
        Line.Write(bag, Color, Opacity, StrokeWidth, BorderStyle, DashArray, InteriorColor, LineEndings, Rotation, Intent, CaptionEnabled, CaptionPosition, CaptionOffset, Leader, BlendMode, RichText, Measure);
    }
}

/// <summary>A change to a line, or to a line tool's defaults.</summary>
public sealed record LineAnnotationPatch : AnnotationPatch, IAnnotationPatch<LineAnnotationPatch>
{
    /// <summary>Moves the first point. A patch sets both points or neither; one alone is <c>invalid-input</c>.</summary>
    public PdfPoint? Start { get; init; }
    /// <summary>Moves the second point.</summary>
    public PdfPoint? End { get; init; }
    /// <summary><c>/C</c>.</summary>
    public JsonNullable<AnnotationColor>? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The line width.</summary>
    public double? StrokeWidth { get; init; }
    /// <summary>The border style.</summary>
    public AnnotationBorderStyle? BorderStyle { get; init; }
    /// <summary>The dash lengths.</summary>
    public JsonNullable<IReadOnlyList<double>>? DashArray { get; init; }
    /// <summary>The fill of closed line endings.</summary>
    public JsonNullable<AnnotationColor>? InteriorColor { get; init; }
    /// <summary>The endings.</summary>
    public AnnotationLineEndings? LineEndings { get; init; }
    /// <summary>Degrees.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary>The intent.</summary>
    public JsonNullable<AnnotationLineIntent>? Intent { get; init; }
    /// <summary>Whether <c>contents</c> shows as a caption.</summary>
    public bool? CaptionEnabled { get; init; }
    /// <summary>Where the caption sits.</summary>
    public AnnotationCaptionPosition? CaptionPosition { get; init; }
    /// <summary>The caption's displacement.</summary>
    public JsonNullable<AnnotationCaptionOffset>? CaptionOffset { get; init; }
    /// <summary>The leader lines.</summary>
    public JsonNullable<AnnotationLineLeader>? Leader { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }
    /// <summary>The scale.</summary>
    public JsonNullable<PdfMeasurement>? Measure { get; init; }

    internal override void Write(FieldBag bag)
    {
        switch (Start, End)
        {
            case ({ } start, { } end): bag.Set("linePoints", AnnotationWire.LinePoints(start, end, "linePoints")); break;
            case (null, null): break;
            default: throw new FieldBagException("a line patch sets both start and end, or neither");
        }
        Line.Write(bag, Color, Opacity, StrokeWidth, BorderStyle, DashArray, InteriorColor, LineEndings, Rotation, Intent, CaptionEnabled, CaptionPosition, CaptionOffset, Leader, BlendMode, RichText, Measure);
    }

    /// <inheritdoc/>
    public static LineAnnotationPatch FromJson(string json) => Decode(json, r =>
    {
        var points = r.Node("linePoints") is { } node ? AnnotationWire.ReadLinePoints(node, "linePoints") : ((PdfPoint, PdfPoint)?)null;
        return new LineAnnotationPatch
        {
            Start = points?.Item1,
            End = points?.Item2,
            Color = r.NullableColor("color"),
            Opacity = r.Double("opacity"),
            StrokeWidth = r.Double("strokeWidth"),
            BorderStyle = r.Enum<AnnotationBorderStyle>("borderStyle"),
            DashArray = r.NullableNumbers("dashArray"),
            InteriorColor = r.NullableColor("interiorColor"),
            LineEndings = r.LineEndings(),
            Rotation = r.NullableDouble("rotation"),
            Intent = r.NullableEnum<AnnotationLineIntent>("intent"),
            CaptionEnabled = r.Bool("captionEnabled"),
            CaptionPosition = r.Enum<AnnotationCaptionPosition>("captionPosition"),
            CaptionOffset = r.Nullable("captionOffset", Line.ReadCaptionOffset),
            Leader = r.Nullable("leader", Line.ReadLeader),
            BlendMode = r.Enum<AnnotationBlendMode>("blendMode"),
            RichText = r.RichText(),
            Measure = r.Measure(),
        };
    });
}

/// <summary>A new polygon: a closed path.</summary>
/// <param name="Vertices">Its corners, page space, at least two.</param>
public sealed record PolygonAnnotationDraft(IReadOnlyList<PdfPoint> Vertices) : AnnotationDraft
{
    /// <summary><c>/C</c>. <see cref="JsonNullable{T}.Null"/> draws no stroke.</summary>
    public JsonNullable<AnnotationColor>? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The line width.</summary>
    public double? StrokeWidth { get; init; }
    /// <summary>The border style.</summary>
    public AnnotationBorderStyle? BorderStyle { get; init; }
    /// <summary>The dash lengths.</summary>
    public JsonNullable<IReadOnlyList<double>>? DashArray { get; init; }
    /// <summary><c>/IC</c>, the fill.</summary>
    public JsonNullable<AnnotationColor>? InteriorColor { get; init; }
    /// <summary>Degrees.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary><see cref="JsonNullable{T}.Null"/> is a plain polygon.</summary>
    public JsonNullable<AnnotationPolygonIntent>? Intent { get; init; }
    /// <summary>The cloudy border's intensity.</summary>
    public JsonNullable<double>? CloudyIntensity { get; init; }
    /// <summary>Whether the measurement shows as a caption.</summary>
    public JsonNullable<bool>? CaptionEnabled { get; init; }
    /// <summary>Where the caption sits.</summary>
    public AnnotationCaptionCenter? CaptionCenter { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }
    /// <summary>The scale of a <see cref="AnnotationPolygonIntent.PolygonDimension"/> polygon.</summary>
    public JsonNullable<PdfMeasurement>? Measure { get; init; }

    internal override void Write(FieldBag bag)
    {
        bag.Put("subtype", "polygon");
        bag.PutPoints("vertices", AnnotationMembers.Required(Vertices, "vertices"));
        Path.Write(bag, Color, Opacity, StrokeWidth, BorderStyle, DashArray, InteriorColor, Rotation, CaptionEnabled, CaptionCenter, BlendMode, RichText, Measure);
        bag.PutEnum("intent", Intent);
        bag.Put("cloudyIntensity", CloudyIntensity);
    }
}

/// <summary>A change to a polygon, or to a polygon tool's defaults.</summary>
public sealed record PolygonAnnotationPatch : AnnotationPatch, IAnnotationPatch<PolygonAnnotationPatch>
{
    /// <summary>Replaces every corner.</summary>
    public IReadOnlyList<PdfPoint>? Vertices { get; init; }
    /// <summary><c>/C</c>.</summary>
    public JsonNullable<AnnotationColor>? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The line width.</summary>
    public double? StrokeWidth { get; init; }
    /// <summary>The border style.</summary>
    public AnnotationBorderStyle? BorderStyle { get; init; }
    /// <summary>The dash lengths.</summary>
    public JsonNullable<IReadOnlyList<double>>? DashArray { get; init; }
    /// <summary>The fill.</summary>
    public JsonNullable<AnnotationColor>? InteriorColor { get; init; }
    /// <summary>Degrees.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary>The intent.</summary>
    public JsonNullable<AnnotationPolygonIntent>? Intent { get; init; }
    /// <summary>The cloudy border's intensity.</summary>
    public JsonNullable<double>? CloudyIntensity { get; init; }
    /// <summary>Whether the measurement shows as a caption.</summary>
    public JsonNullable<bool>? CaptionEnabled { get; init; }
    /// <summary>Where the caption sits.</summary>
    public AnnotationCaptionCenter? CaptionCenter { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }
    /// <summary>The scale.</summary>
    public JsonNullable<PdfMeasurement>? Measure { get; init; }

    internal override void Write(FieldBag bag)
    {
        bag.PutPoints("vertices", Vertices);
        Path.Write(bag, Color, Opacity, StrokeWidth, BorderStyle, DashArray, InteriorColor, Rotation, CaptionEnabled, CaptionCenter, BlendMode, RichText, Measure);
        bag.PutEnum("intent", Intent);
        bag.Put("cloudyIntensity", CloudyIntensity);
    }

    /// <inheritdoc/>
    public static PolygonAnnotationPatch FromJson(string json) => Decode(json, r => new PolygonAnnotationPatch
    {
        Vertices = r.Points("vertices"),
        Color = r.NullableColor("color"),
        Opacity = r.Double("opacity"),
        StrokeWidth = r.Double("strokeWidth"),
        BorderStyle = r.Enum<AnnotationBorderStyle>("borderStyle"),
        DashArray = r.NullableNumbers("dashArray"),
        InteriorColor = r.NullableColor("interiorColor"),
        Rotation = r.NullableDouble("rotation"),
        Intent = r.NullableEnum<AnnotationPolygonIntent>("intent"),
        CloudyIntensity = r.NullableDouble("cloudyIntensity"),
        CaptionEnabled = r.NullableBool("captionEnabled"),
        CaptionCenter = r.CaptionCenter(),
        BlendMode = r.Enum<AnnotationBlendMode>("blendMode"),
        RichText = r.RichText(),
        Measure = r.Measure(),
    });
}

/// <summary>A new polyline: an open path.</summary>
/// <param name="Vertices">Its points, page space, at least two.</param>
public sealed record PolylineAnnotationDraft(IReadOnlyList<PdfPoint> Vertices) : AnnotationDraft
{
    /// <summary><c>/C</c>. <see cref="JsonNullable{T}.Null"/> draws no stroke.</summary>
    public JsonNullable<AnnotationColor>? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The line width.</summary>
    public double? StrokeWidth { get; init; }
    /// <summary>The border style.</summary>
    public AnnotationBorderStyle? BorderStyle { get; init; }
    /// <summary>The dash lengths.</summary>
    public JsonNullable<IReadOnlyList<double>>? DashArray { get; init; }
    /// <summary><c>/IC</c>, the fill of closed line endings.</summary>
    public JsonNullable<AnnotationColor>? InteriorColor { get; init; }
    /// <summary>The endings at the two ends.</summary>
    public AnnotationLineEndings? LineEndings { get; init; }
    /// <summary>Degrees.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary><see cref="JsonNullable{T}.Null"/> is a plain polyline.</summary>
    public JsonNullable<AnnotationPolylineIntent>? Intent { get; init; }
    /// <summary>Whether the measurement shows as a caption.</summary>
    public JsonNullable<bool>? CaptionEnabled { get; init; }
    /// <summary>Where the caption sits.</summary>
    public AnnotationCaptionCenter? CaptionCenter { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }
    /// <summary>The scale of a <see cref="AnnotationPolylineIntent.PolylineDimension"/> polyline.</summary>
    public JsonNullable<PdfMeasurement>? Measure { get; init; }

    internal override void Write(FieldBag bag)
    {
        bag.Put("subtype", "polyline");
        bag.PutPoints("vertices", AnnotationMembers.Required(Vertices, "vertices"));
        Path.Write(bag, Color, Opacity, StrokeWidth, BorderStyle, DashArray, InteriorColor, Rotation, CaptionEnabled, CaptionCenter, BlendMode, RichText, Measure);
        bag.PutEnum("intent", Intent);
        bag.PutLineEndings(LineEndings);
    }
}

/// <summary>A change to a polyline, or to a polyline tool's defaults.</summary>
public sealed record PolylineAnnotationPatch : AnnotationPatch, IAnnotationPatch<PolylineAnnotationPatch>
{
    /// <summary>Replaces every point.</summary>
    public IReadOnlyList<PdfPoint>? Vertices { get; init; }
    /// <summary><c>/C</c>.</summary>
    public JsonNullable<AnnotationColor>? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The line width.</summary>
    public double? StrokeWidth { get; init; }
    /// <summary>The border style.</summary>
    public AnnotationBorderStyle? BorderStyle { get; init; }
    /// <summary>The dash lengths.</summary>
    public JsonNullable<IReadOnlyList<double>>? DashArray { get; init; }
    /// <summary>The fill of closed line endings.</summary>
    public JsonNullable<AnnotationColor>? InteriorColor { get; init; }
    /// <summary>The endings.</summary>
    public AnnotationLineEndings? LineEndings { get; init; }
    /// <summary>Degrees.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary>The intent.</summary>
    public JsonNullable<AnnotationPolylineIntent>? Intent { get; init; }
    /// <summary>Whether the measurement shows as a caption.</summary>
    public JsonNullable<bool>? CaptionEnabled { get; init; }
    /// <summary>Where the caption sits.</summary>
    public AnnotationCaptionCenter? CaptionCenter { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }
    /// <summary>The scale.</summary>
    public JsonNullable<PdfMeasurement>? Measure { get; init; }

    internal override void Write(FieldBag bag)
    {
        bag.PutPoints("vertices", Vertices);
        Path.Write(bag, Color, Opacity, StrokeWidth, BorderStyle, DashArray, InteriorColor, Rotation, CaptionEnabled, CaptionCenter, BlendMode, RichText, Measure);
        bag.PutEnum("intent", Intent);
        bag.PutLineEndings(LineEndings);
    }

    /// <inheritdoc/>
    public static PolylineAnnotationPatch FromJson(string json) => Decode(json, r => new PolylineAnnotationPatch
    {
        Vertices = r.Points("vertices"),
        Color = r.NullableColor("color"),
        Opacity = r.Double("opacity"),
        StrokeWidth = r.Double("strokeWidth"),
        BorderStyle = r.Enum<AnnotationBorderStyle>("borderStyle"),
        DashArray = r.NullableNumbers("dashArray"),
        InteriorColor = r.NullableColor("interiorColor"),
        LineEndings = r.LineEndings(),
        Rotation = r.NullableDouble("rotation"),
        Intent = r.NullableEnum<AnnotationPolylineIntent>("intent"),
        CaptionEnabled = r.NullableBool("captionEnabled"),
        CaptionCenter = r.CaptionCenter(),
        BlendMode = r.Enum<AnnotationBlendMode>("blendMode"),
        RichText = r.RichText(),
        Measure = r.Measure(),
    });
}

file static class Shape
{
    internal static void Write(
        FieldBag bag, string? subtype, PdfRect? box, JsonNullable<AnnotationColor>? color, double? opacity, double? strokeWidth,
        AnnotationBorderStyle? borderStyle, JsonNullable<IReadOnlyList<double>>? dashArray, JsonNullable<AnnotationColor>? interiorColor,
        JsonNullable<double>? rotation, JsonNullable<double>? cloudyIntensity, AnnotationBlendMode? blendMode, JsonNullable<AnnotationRichText>? richText)
    {
        bag.Put("subtype", subtype);
        bag.Put("box", box);
        bag.Put("color", color);
        bag.Put("opacity", opacity);
        bag.Put("strokeWidth", strokeWidth);
        bag.Put("borderStyle", borderStyle);
        bag.PutNumbers("dashArray", dashArray);
        bag.Put("interiorColor", interiorColor);
        bag.Put("rotation", rotation);
        bag.Put("cloudyIntensity", cloudyIntensity);
        bag.Put("blendMode", blendMode);
        bag.PutRichText(richText);
    }
}

file static class Line
{
    internal static void Write(
        FieldBag bag, JsonNullable<AnnotationColor>? color, double? opacity, double? strokeWidth, AnnotationBorderStyle? borderStyle,
        JsonNullable<IReadOnlyList<double>>? dashArray, JsonNullable<AnnotationColor>? interiorColor, AnnotationLineEndings? lineEndings,
        JsonNullable<double>? rotation, JsonNullable<AnnotationLineIntent>? intent, bool? captionEnabled, AnnotationCaptionPosition? captionPosition,
        JsonNullable<AnnotationCaptionOffset>? captionOffset, JsonNullable<AnnotationLineLeader>? leader, AnnotationBlendMode? blendMode,
        JsonNullable<AnnotationRichText>? richText, JsonNullable<PdfMeasurement>? measure)
    {
        bag.Put("color", color);
        bag.Put("opacity", opacity);
        bag.Put("strokeWidth", strokeWidth);
        bag.Put("borderStyle", borderStyle);
        bag.PutNumbers("dashArray", dashArray);
        bag.Put("interiorColor", interiorColor);
        bag.PutLineEndings(lineEndings);
        bag.Put("rotation", rotation);
        bag.PutEnum("intent", intent);
        bag.Put("captionEnabled", captionEnabled);
        bag.Put("captionPosition", captionPosition);
        bag.Put("captionOffset", captionOffset, o => AnnotationWire.Serialize(new WireCaptionOffset(
            Finite(o.Along, "captionOffset"), Finite(o.Perpendicular, "captionOffset"))));
        bag.Put("leader", leader, l => AnnotationWire.Serialize(new WireLineLeader(
            Finite(l.Length, "leader"), l.Extension is { } e ? Finite(e, "leader") : null, l.Offset is { } f ? Finite(f, "leader") : null)));
        bag.Put("blendMode", blendMode);
        bag.PutRichText(richText);
        bag.PutMeasure(measure);
    }

    static double Finite(double value, string key) =>
        double.IsFinite(value) ? value : throw new FieldBagException($"{key} holds {value}, which JSON can't hold");

    internal static AnnotationCaptionOffset ReadCaptionOffset(System.Text.Json.Nodes.JsonNode node, string key)
    {
        var o = AnnotationWire.ReadCaptionOffset(node, key);
        return new(o.Along, o.Perpendicular);
    }

    internal static AnnotationLineLeader ReadLeader(System.Text.Json.Nodes.JsonNode node, string key)
    {
        var l = AnnotationWire.ReadLineLeader(node, key);
        return new(l.Length) { Extension = l.Extension, Offset = l.Offset };
    }
}

file static class Path
{
    internal static void Write(
        FieldBag bag, JsonNullable<AnnotationColor>? color, double? opacity, double? strokeWidth, AnnotationBorderStyle? borderStyle,
        JsonNullable<IReadOnlyList<double>>? dashArray, JsonNullable<AnnotationColor>? interiorColor, JsonNullable<double>? rotation,
        JsonNullable<bool>? captionEnabled, AnnotationCaptionCenter? captionCenter, AnnotationBlendMode? blendMode,
        JsonNullable<AnnotationRichText>? richText, JsonNullable<PdfMeasurement>? measure)
    {
        bag.Put("color", color);
        bag.Put("opacity", opacity);
        bag.Put("strokeWidth", strokeWidth);
        bag.Put("borderStyle", borderStyle);
        bag.PutNumbers("dashArray", dashArray);
        bag.Put("interiorColor", interiorColor);
        bag.Put("rotation", rotation);
        bag.Put("captionEnabled", captionEnabled);
        bag.PutCaptionCenter(captionCenter);
        bag.Put("blendMode", blendMode);
        bag.PutRichText(richText);
        bag.PutMeasure(measure);
    }
}
