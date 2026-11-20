namespace EmbedPDF;

// Text markup and redaction drafts and patches (AnnotationDraft.cs has the
// conventions).

/// <summary>A new underline over text quads.</summary>
/// <param name="QuadPoints"><c>/QuadPoints</c>, page space, at least one.</param>
public sealed record UnderlineAnnotationDraft(IReadOnlyList<PdfQuad> QuadPoints) : AnnotationDraft
{
    /// <summary><c>/C</c>.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag) =>
        TextMarkup.Write(bag, "underline", AnnotationMembers.Required(QuadPoints, "quadPoints"), Color, Opacity, BlendMode, RichText);
}

/// <summary>A change to an underline, or to an underline tool's defaults.</summary>
public sealed record UnderlineAnnotationPatch : AnnotationPatch, IAnnotationPatch<UnderlineAnnotationPatch>
{
    /// <summary>Replaces every quad.</summary>
    public IReadOnlyList<PdfQuad>? QuadPoints { get; init; }
    /// <summary><c>/C</c>.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag) => TextMarkup.Write(bag, null, QuadPoints, Color, Opacity, BlendMode, RichText);

    /// <inheritdoc/>
    public static UnderlineAnnotationPatch FromJson(string json) => Decode(json, r => new UnderlineAnnotationPatch
    {
        QuadPoints = r.Quads(),
        Color = r.Color("color"),
        Opacity = r.Double("opacity"),
        BlendMode = r.Enum<AnnotationBlendMode>("blendMode"),
        RichText = r.RichText(),
    });
}

/// <summary>A new squiggly underline over text quads.</summary>
/// <param name="QuadPoints"><c>/QuadPoints</c>, page space, at least one.</param>
public sealed record SquigglyAnnotationDraft(IReadOnlyList<PdfQuad> QuadPoints) : AnnotationDraft
{
    /// <summary><c>/C</c>.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag) =>
        TextMarkup.Write(bag, "squiggly", AnnotationMembers.Required(QuadPoints, "quadPoints"), Color, Opacity, BlendMode, RichText);
}

/// <summary>A change to a squiggly underline, or to its tool's defaults.</summary>
public sealed record SquigglyAnnotationPatch : AnnotationPatch, IAnnotationPatch<SquigglyAnnotationPatch>
{
    /// <summary>Replaces every quad.</summary>
    public IReadOnlyList<PdfQuad>? QuadPoints { get; init; }
    /// <summary><c>/C</c>.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag) => TextMarkup.Write(bag, null, QuadPoints, Color, Opacity, BlendMode, RichText);

    /// <inheritdoc/>
    public static SquigglyAnnotationPatch FromJson(string json) => Decode(json, r => new SquigglyAnnotationPatch
    {
        QuadPoints = r.Quads(),
        Color = r.Color("color"),
        Opacity = r.Double("opacity"),
        BlendMode = r.Enum<AnnotationBlendMode>("blendMode"),
        RichText = r.RichText(),
    });
}

/// <summary>A new strikeout over text quads.</summary>
/// <param name="QuadPoints"><c>/QuadPoints</c>, page space, at least one.</param>
public sealed record StrikeoutAnnotationDraft(IReadOnlyList<PdfQuad> QuadPoints) : AnnotationDraft
{
    /// <summary><c>/C</c>.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary><see cref="JsonNullable{T}.Null"/> is a plain strikeout.</summary>
    public JsonNullable<AnnotationStrikeoutIntent>? Intent { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag)
    {
        TextMarkup.Write(bag, "strikeout", AnnotationMembers.Required(QuadPoints, "quadPoints"), Color, Opacity, BlendMode, RichText);
        bag.PutEnum("intent", Intent);
    }
}

/// <summary>A change to a strikeout, or to a strikeout tool's defaults.</summary>
public sealed record StrikeoutAnnotationPatch : AnnotationPatch, IAnnotationPatch<StrikeoutAnnotationPatch>
{
    /// <summary>Replaces every quad.</summary>
    public IReadOnlyList<PdfQuad>? QuadPoints { get; init; }
    /// <summary><c>/C</c>.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary><see cref="JsonNullable{T}.Null"/> makes it a plain strikeout.</summary>
    public JsonNullable<AnnotationStrikeoutIntent>? Intent { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag)
    {
        TextMarkup.Write(bag, null, QuadPoints, Color, Opacity, BlendMode, RichText);
        bag.PutEnum("intent", Intent);
    }

    /// <inheritdoc/>
    public static StrikeoutAnnotationPatch FromJson(string json) => Decode(json, r => new StrikeoutAnnotationPatch
    {
        QuadPoints = r.Quads(),
        Color = r.Color("color"),
        Opacity = r.Double("opacity"),
        Intent = r.NullableEnum<AnnotationStrikeoutIntent>("intent"),
        BlendMode = r.Enum<AnnotationBlendMode>("blendMode"),
        RichText = r.RichText(),
    });
}

/// <summary>A new redaction mark: what a later apply removes, over text quads or a rectangle.</summary>
public sealed record RedactionAnnotationDraft : AnnotationDraft
{
    /// <summary><c>/QuadPoints</c>, page space: the text to remove. Give these, <see cref="Rect"/>, or both.</summary>
    public IReadOnlyList<PdfQuad>? QuadPoints { get; init; }
    /// <summary>The area to remove, page space.</summary>
    public PdfRect? Rect { get; init; }
    /// <summary><c>/OC</c>: the mark's outline colour.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The fill drawn once applied. <see cref="JsonNullable{T}.Null"/> is none.</summary>
    public JsonNullable<AnnotationColor>? InteriorColor { get; init; }
    /// <summary>The text drawn over the area once applied.</summary>
    public JsonNullable<string>? OverlayText { get; init; }
    /// <summary>Whether the overlay text repeats to fill the area.</summary>
    public bool? Repeat { get; init; }
    /// <summary>The overlay text's font family.</summary>
    public string? FontFamily { get; init; }
    /// <summary>The overlay text's size, above 0.</summary>
    public double? FontSize { get; init; }
    /// <summary>The overlay text's colour.</summary>
    public AnnotationColor? FontColor { get; init; }
    /// <summary>The overlay text's alignment; <see cref="AnnotationTextAlignment.Justify"/> is refused.</summary>
    public AnnotationTextAlignment? TextAlign { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag) => Redaction.Write(
        bag, "redact", QuadPoints, Rect, Color, Opacity, InteriorColor, OverlayText, Repeat, FontFamily, FontSize, FontColor, TextAlign, BlendMode, RichText);
}

/// <summary>A change to a redaction mark, or to a redaction tool's defaults.</summary>
public sealed record RedactionAnnotationPatch : AnnotationPatch, IAnnotationPatch<RedactionAnnotationPatch>
{
    /// <summary>Replaces every quad.</summary>
    public IReadOnlyList<PdfQuad>? QuadPoints { get; init; }
    /// <summary>The area to remove.</summary>
    public PdfRect? Rect { get; init; }
    /// <summary><c>/OC</c>.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The fill drawn once applied.</summary>
    public JsonNullable<AnnotationColor>? InteriorColor { get; init; }
    /// <summary>The overlay text.</summary>
    public JsonNullable<string>? OverlayText { get; init; }
    /// <summary>Whether the overlay text repeats.</summary>
    public bool? Repeat { get; init; }
    /// <summary>The overlay text's font family.</summary>
    public string? FontFamily { get; init; }
    /// <summary>The overlay text's size.</summary>
    public double? FontSize { get; init; }
    /// <summary>The overlay text's colour.</summary>
    public AnnotationColor? FontColor { get; init; }
    /// <summary>The overlay text's alignment.</summary>
    public AnnotationTextAlignment? TextAlign { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag) => Redaction.Write(
        bag, null, QuadPoints, Rect, Color, Opacity, InteriorColor, OverlayText, Repeat, FontFamily, FontSize, FontColor, TextAlign, BlendMode, RichText);

    /// <inheritdoc/>
    public static RedactionAnnotationPatch FromJson(string json) => Decode(json, r => new RedactionAnnotationPatch
    {
        QuadPoints = r.Quads(),
        Rect = r.Rect("rect"),
        Color = r.Color("color"),
        Opacity = r.Double("opacity"),
        InteriorColor = r.NullableColor("interiorColor"),
        OverlayText = r.NullableString("overlayText"),
        Repeat = r.Bool("repeat"),
        FontFamily = r.String("fontFamily"),
        FontSize = r.Double("fontSize"),
        FontColor = r.Color("fontColor"),
        TextAlign = r.Enum<AnnotationTextAlignment>("textAlign"),
        BlendMode = r.Enum<AnnotationBlendMode>("blendMode"),
        RichText = r.RichText(),
    });
}

file static class TextMarkup
{
    internal static void Write(
        FieldBag bag, string? subtype, IReadOnlyList<PdfQuad>? quads, AnnotationColor? color, double? opacity, AnnotationBlendMode? blendMode,
        JsonNullable<AnnotationRichText>? richText)
    {
        bag.Put("subtype", subtype);
        bag.PutQuads(quads);
        bag.Put("color", color);
        bag.Put("opacity", opacity);
        bag.Put("blendMode", blendMode);
        bag.PutRichText(richText);
    }
}

file static class Redaction
{
    internal static void Write(
        FieldBag bag, string? subtype, IReadOnlyList<PdfQuad>? quads, PdfRect? rect, AnnotationColor? color, double? opacity,
        JsonNullable<AnnotationColor>? interiorColor, JsonNullable<string>? overlayText, bool? repeat, string? fontFamily, double? fontSize,
        AnnotationColor? fontColor, AnnotationTextAlignment? textAlign, AnnotationBlendMode? blendMode, JsonNullable<AnnotationRichText>? richText)
    {
        bag.Put("subtype", subtype);
        bag.PutQuads(quads);
        bag.Put("rect", rect);
        bag.Put("color", color);
        bag.Put("opacity", opacity);
        bag.Put("interiorColor", interiorColor);
        bag.Put("overlayText", overlayText);
        bag.Put("repeat", repeat);
        bag.Put("fontFamily", fontFamily);
        bag.Put("fontSize", fontSize);
        bag.Put("fontColor", fontColor);
        bag.Put("textAlign", textAlign);
        bag.Put("blendMode", blendMode);
        bag.PutRichText(richText);
    }
}
