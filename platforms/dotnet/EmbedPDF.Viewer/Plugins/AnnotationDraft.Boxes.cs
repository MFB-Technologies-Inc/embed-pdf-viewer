using System.Text.Json.Nodes;

namespace EmbedPDF;

// Free text, caret, link, stamp and file attachment drafts and patches
// (AnnotationDraft.cs has the conventions). Stamps and file attachments are
// created by placing them (AnnotationDraftsCapability), so they have patches
// only. A link and a stamp take no blend mode: the engine draws neither.

/// <summary>A new free text: a text box on the page.</summary>
/// <param name="Box">Its box, page space.</param>
public sealed record FreeTextAnnotationDraft(PdfRect Box) : AnnotationDraft
{
    /// <summary>The font family.</summary>
    public string? FontFamily { get; init; }
    /// <summary>The font size, above 0.</summary>
    public double? FontSize { get; init; }
    /// <summary>The text colour.</summary>
    public AnnotationColor? FontColor { get; init; }
    /// <summary>How lines align across the box.</summary>
    public AnnotationTextAlignment? TextAlign { get; init; }
    /// <summary>Where the text sits, top to bottom.</summary>
    public AnnotationVerticalAlignment? VerticalAlign { get; init; }
    /// <summary><c>/C</c>, the border colour.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary>The fill. <see cref="JsonNullable{T}.Null"/> is none.</summary>
    public JsonNullable<AnnotationColor>? InteriorColor { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The border width.</summary>
    public double? StrokeWidth { get; init; }
    /// <summary>The border style.</summary>
    public AnnotationBorderStyle? BorderStyle { get; init; }
    /// <summary>The dash lengths.</summary>
    public JsonNullable<IReadOnlyList<double>>? DashArray { get; init; }
    /// <summary>The cloudy border's intensity.</summary>
    public JsonNullable<double>? CloudyIntensity { get; init; }
    /// <summary>Degrees.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary>A box or a callout.</summary>
    public JsonNullable<AnnotationFreeTextIntent>? Intent { get; init; }
    /// <summary>A callout's leader line.</summary>
    public JsonNullable<AnnotationCalloutLine>? CalloutLine { get; init; }
    /// <summary>The ending at the leader line's called-out end.</summary>
    public JsonNullable<AnnotationLineEnding>? LineEnding { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The text styled (<c>/RC</c>); it sets <c>contents</c>. Free text always has text, so there is no null.</summary>
    public AnnotationRichText? RichText { get; init; }

    internal override void Write(FieldBag bag) => FreeText.Write(
        bag, "free-text", Box, FontFamily, FontSize, FontColor, TextAlign, VerticalAlign, Color, InteriorColor, Opacity, StrokeWidth, BorderStyle,
        DashArray, CloudyIntensity, Rotation, Intent, CalloutLine, LineEnding, BlendMode, RichText);
}

/// <summary>A change to a free text, or to a free text tool's defaults.</summary>
public sealed record FreeTextAnnotationPatch : AnnotationPatch, IAnnotationPatch<FreeTextAnnotationPatch>
{
    /// <summary>Its box.</summary>
    public PdfRect? Box { get; init; }
    /// <summary>The font family.</summary>
    public string? FontFamily { get; init; }
    /// <summary>The font size.</summary>
    public double? FontSize { get; init; }
    /// <summary>The text colour.</summary>
    public AnnotationColor? FontColor { get; init; }
    /// <summary>How lines align.</summary>
    public AnnotationTextAlignment? TextAlign { get; init; }
    /// <summary>Where the text sits.</summary>
    public AnnotationVerticalAlignment? VerticalAlign { get; init; }
    /// <summary>The border colour.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary>The fill.</summary>
    public JsonNullable<AnnotationColor>? InteriorColor { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The border width.</summary>
    public double? StrokeWidth { get; init; }
    /// <summary>The border style.</summary>
    public AnnotationBorderStyle? BorderStyle { get; init; }
    /// <summary>The dash lengths.</summary>
    public JsonNullable<IReadOnlyList<double>>? DashArray { get; init; }
    /// <summary>The cloudy border's intensity.</summary>
    public JsonNullable<double>? CloudyIntensity { get; init; }
    /// <summary>Degrees.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary>A box or a callout.</summary>
    public JsonNullable<AnnotationFreeTextIntent>? Intent { get; init; }
    /// <summary>A callout's leader line.</summary>
    public JsonNullable<AnnotationCalloutLine>? CalloutLine { get; init; }
    /// <summary>The leader line's ending.</summary>
    public JsonNullable<AnnotationLineEnding>? LineEnding { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The text styled.</summary>
    public AnnotationRichText? RichText { get; init; }

    internal override void Write(FieldBag bag) => FreeText.Write(
        bag, null, Box, FontFamily, FontSize, FontColor, TextAlign, VerticalAlign, Color, InteriorColor, Opacity, StrokeWidth, BorderStyle,
        DashArray, CloudyIntensity, Rotation, Intent, CalloutLine, LineEnding, BlendMode, RichText);

    /// <inheritdoc/>
    public static FreeTextAnnotationPatch FromJson(string json) => Decode(json, r => new FreeTextAnnotationPatch
    {
        Box = r.Rect("box"),
        FontFamily = r.String("fontFamily"),
        FontSize = r.Double("fontSize"),
        FontColor = r.Color("fontColor"),
        TextAlign = r.Enum<AnnotationTextAlignment>("textAlign"),
        VerticalAlign = r.Enum<AnnotationVerticalAlignment>("verticalAlign"),
        Color = r.Color("color"),
        InteriorColor = r.NullableColor("interiorColor"),
        Opacity = r.Double("opacity"),
        StrokeWidth = r.Double("strokeWidth"),
        BorderStyle = r.Enum<AnnotationBorderStyle>("borderStyle"),
        DashArray = r.NullableNumbers("dashArray"),
        CloudyIntensity = r.NullableDouble("cloudyIntensity"),
        Rotation = r.NullableDouble("rotation"),
        Intent = r.NullableEnum<AnnotationFreeTextIntent>("intent"),
        CalloutLine = r.Nullable("calloutLine", FreeText.ReadCallout),
        LineEnding = r.NullableEnum<AnnotationLineEnding>("lineEnding"),
        BlendMode = r.Enum<AnnotationBlendMode>("blendMode"),
        RichText = r.Value("richText", AnnotationRichText.Read),
    });
}

/// <summary>A new caret: an insertion mark.</summary>
/// <param name="Box">Its box, page space.</param>
public sealed record CaretAnnotationDraft(PdfRect Box) : AnnotationDraft
{
    /// <summary><c>/C</c>.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>Degrees.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary><see cref="JsonNullable{T}.Null"/> marks an insertion.</summary>
    public JsonNullable<AnnotationCaretIntent>? Intent { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag) => Caret.Write(bag, "caret", Box, Color, Opacity, Rotation, Intent, BlendMode, RichText);
}

/// <summary>A change to a caret, or to a caret tool's defaults.</summary>
public sealed record CaretAnnotationPatch : AnnotationPatch, IAnnotationPatch<CaretAnnotationPatch>
{
    /// <summary>Its box.</summary>
    public PdfRect? Box { get; init; }
    /// <summary><c>/C</c>.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>Degrees.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary>The intent.</summary>
    public JsonNullable<AnnotationCaretIntent>? Intent { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag) => Caret.Write(bag, null, Box, Color, Opacity, Rotation, Intent, BlendMode, RichText);

    /// <inheritdoc/>
    public static CaretAnnotationPatch FromJson(string json) => Decode(json, r => new CaretAnnotationPatch
    {
        Box = r.Rect("box"),
        Color = r.Color("color"),
        Opacity = r.Double("opacity"),
        Rotation = r.NullableDouble("rotation"),
        Intent = r.NullableEnum<AnnotationCaretIntent>("intent"),
        BlendMode = r.Enum<AnnotationBlendMode>("blendMode"),
        RichText = r.RichText(),
    });
}

/// <summary>A new link annotation.</summary>
/// <param name="Rect">Its active area, page space.</param>
/// <param name="Target">Where it goes.</param>
public sealed record LinkAnnotationDraft(PdfRect Rect, AnnotationLinkTarget Target) : AnnotationDraft
{
    internal override void Write(FieldBag bag)
    {
        bag.Put("subtype", "link");
        bag.Put("rect", Rect);
        bag.Set("target", AnnotationMembers.Required(Target, "target").ToNode("target"));
    }
}

/// <summary>A change to a link annotation.</summary>
public sealed record LinkAnnotationPatch : AnnotationPatch, IAnnotationPatch<LinkAnnotationPatch>
{
    /// <summary>Its active area.</summary>
    public PdfRect? Rect { get; init; }
    /// <summary>Where it goes. <see cref="JsonNullable{T}.Null"/> removes the target.</summary>
    public JsonNullable<AnnotationLinkTarget>? Target { get; init; }

    internal override void Write(FieldBag bag)
    {
        bag.Put("rect", Rect);
        bag.Put("target", Target, t => t.ToNode("target"));
    }

    /// <inheritdoc/>
    public static LinkAnnotationPatch FromJson(string json) => Decode(json, r => new LinkAnnotationPatch
    {
        Rect = r.Rect("rect"),
        Target = r.Nullable("target", AnnotationLinkTarget.Read),
    });
}

/// <summary>A change to a stamp, or to a stamp tool's defaults. Place a stamp with the drafts capability.</summary>
public sealed record StampAnnotationPatch : AnnotationPatch, IAnnotationPatch<StampAnnotationPatch>
{
    /// <summary>Its box.</summary>
    public PdfRect? Box { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>Degrees.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary><c>/Name</c>, the stamp's name.</summary>
    public JsonNullable<string>? Name { get; init; }
    /// <summary>How its image fills its box.</summary>
    public JsonNullable<AnnotationStampFit>? Fit { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag)
    {
        bag.Put("box", Box);
        bag.Put("opacity", Opacity);
        bag.Put("rotation", Rotation);
        bag.Put("name", Name);
        bag.PutEnum("fit", Fit);
        bag.PutRichText(RichText);
    }

    /// <inheritdoc/>
    public static StampAnnotationPatch FromJson(string json) => Decode(json, r => new StampAnnotationPatch
    {
        Box = r.Rect("box"),
        Opacity = r.Double("opacity"),
        Rotation = r.NullableDouble("rotation"),
        Name = r.NullableString("name"),
        Fit = r.NullableEnum<AnnotationStampFit>("fit"),
        RichText = r.RichText(),
    });
}

/// <summary>A change to a file attachment annotation. Create one with the drafts capability.</summary>
public sealed record FileAttachmentAnnotationPatch : AnnotationPatch, IAnnotationPatch<FileAttachmentAnnotationPatch>
{
    /// <summary>Its icon's area.</summary>
    public PdfRect? Rect { get; init; }
    /// <summary>The icon.</summary>
    public AnnotationFileAttachmentIcon? Icon { get; init; }
    /// <summary><c>/C</c>.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag)
    {
        bag.Put("rect", Rect);
        bag.Put("icon", Icon);
        bag.Put("color", Color);
        bag.Put("opacity", Opacity);
        bag.Put("blendMode", BlendMode);
        bag.PutRichText(RichText);
    }

    /// <inheritdoc/>
    public static FileAttachmentAnnotationPatch FromJson(string json) => Decode(json, r => new FileAttachmentAnnotationPatch
    {
        Rect = r.Rect("rect"),
        Icon = r.Enum<AnnotationFileAttachmentIcon>("icon"),
        Color = r.Color("color"),
        Opacity = r.Double("opacity"),
        BlendMode = r.Enum<AnnotationBlendMode>("blendMode"),
        RichText = r.RichText(),
    });
}

file static class FreeText
{
    internal static void Write(
        FieldBag bag, string? subtype, PdfRect? box, string? fontFamily, double? fontSize, AnnotationColor? fontColor,
        AnnotationTextAlignment? textAlign, AnnotationVerticalAlignment? verticalAlign, AnnotationColor? color,
        JsonNullable<AnnotationColor>? interiorColor, double? opacity, double? strokeWidth, AnnotationBorderStyle? borderStyle,
        JsonNullable<IReadOnlyList<double>>? dashArray, JsonNullable<double>? cloudyIntensity, JsonNullable<double>? rotation,
        JsonNullable<AnnotationFreeTextIntent>? intent, JsonNullable<AnnotationCalloutLine>? calloutLine, JsonNullable<AnnotationLineEnding>? lineEnding,
        AnnotationBlendMode? blendMode, AnnotationRichText? richText)
    {
        bag.Put("subtype", subtype);
        bag.Put("box", box);
        bag.Put("fontFamily", fontFamily);
        bag.Put("fontSize", fontSize);
        bag.Put("fontColor", fontColor);
        bag.Put("textAlign", textAlign);
        bag.Put("verticalAlign", verticalAlign);
        bag.Put("color", color);
        bag.Put("interiorColor", interiorColor);
        bag.Put("opacity", opacity);
        bag.Put("strokeWidth", strokeWidth);
        bag.Put("borderStyle", borderStyle);
        bag.PutNumbers("dashArray", dashArray);
        bag.Put("cloudyIntensity", cloudyIntensity);
        bag.Put("rotation", rotation);
        bag.PutEnum("intent", intent);
        bag.Put("calloutLine", calloutLine, line => AnnotationWire.Points(line.Points, "calloutLine"));
        bag.PutEnum("lineEnding", lineEnding);
        bag.Put("blendMode", blendMode);
        if (richText is not null) bag.Set("richText", richText.ToNode("richText"));
    }

    internal static AnnotationCalloutLine ReadCallout(JsonNode node, string key) =>
        AnnotationCalloutLine.FromPoints(AnnotationWire.ReadPoints(node, key)) ?? throw new FieldBagException("a callout line has two or three points");
}

file static class Caret
{
    internal static void Write(
        FieldBag bag, string? subtype, PdfRect? box, AnnotationColor? color, double? opacity, JsonNullable<double>? rotation,
        JsonNullable<AnnotationCaretIntent>? intent, AnnotationBlendMode? blendMode, JsonNullable<AnnotationRichText>? richText)
    {
        bag.Put("subtype", subtype);
        bag.Put("box", box);
        bag.Put("color", color);
        bag.Put("opacity", opacity);
        bag.Put("rotation", rotation);
        bag.PutEnum("intent", intent);
        bag.Put("blendMode", blendMode);
        bag.PutRichText(richText);
    }
}
