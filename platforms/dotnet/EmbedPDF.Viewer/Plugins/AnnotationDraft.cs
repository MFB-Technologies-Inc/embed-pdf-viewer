using System.Text.Json.Nodes;

namespace EmbedPDF;

// Typed create drafts and patches. Each writes the engine's field bag for its
// subtype, the JSON object the string overloads of CreateAsync, UpdateAsync
// and UpdateDefaultsAsync take. The engine refuses a key its kind doesn't
// declare (`assert_declared_fields` in
// `crates/epdf-engine/src/features/annotation/write/prepare.rs`), so every
// key here is one the kind declares, with the shape its `read_field` reads.
// Geometry is page space, y down: points `{x, y}`, boxes `{x, y, width,
// height}`. A member left null is left out; a JsonNullable member's Null
// writes `null`. This file has the bases, ink, highlight and note;
// AnnotationDraft.Markup.cs, .Shapes.cs and .Boxes.cs have the rest.

/// <summary>
/// A typed create draft: the engine's field bag for one subtype, with its
/// <c>subtype</c>. <c>AnnotationCapability.CreateAsync</c> takes one.
/// </summary>
public abstract record AnnotationDraft
{
    private protected AnnotationDraft() { }

    /// <summary><c>/Contents</c>. <see cref="JsonNullable{T}.Null"/> is none.</summary>
    public JsonNullable<string>? Contents { get; init; }

    /// <summary>The fields every annotation shares, written flat beside the kind's own.</summary>
    public AnnotationCommonFields Common { get; init; } = new();

    /// <summary>
    /// The field bag as the engine reads it: keys sorted, so the same draft is
    /// always the same text. Throws <see cref="PdfException"/> with
    /// <see cref="PdfErrorCode.InvalidInput"/> for a number JSON can't hold
    /// (NaN or an infinity) or a value outside its enum.
    /// </summary>
    public string ToJson() => FieldBag.Encode(bag =>
    {
        Write(bag);
        bag.Put("contents", Contents);
        (Common ?? throw new FieldBagException("common is null")).Write(bag, patch: false);
    });

    internal abstract void Write(FieldBag bag);
}

/// <summary>
/// A typed patch: the members of one subtype to change. A member left
/// <see langword="null"/> keeps its value. <c>AnnotationCapability.UpdateAsync</c>
/// and <c>AnnotationToolsCapability.UpdateDefaultsAsync</c>
/// take one, and <c>AnnotationToolsCapability.GetDefaultsAsync&lt;TPatch&gt;</c>
/// reads a tool's defaults as one.
/// </summary>
/// <remarks>
/// A patch reads from the field bag it writes (<c>FromJson</c> on each
/// type). A key it doesn't declare is ignored, a missing key leaves its
/// member <see langword="null"/>, <c>null</c> reads as
/// <see cref="JsonNullable{T}.Null"/> on a nullable member and as left out on
/// any other, and a declared key whose value the member can't hold throws.
/// No patch writes <c>Common.Nm</c>; a decoded one keeps the <c>nm</c> its
/// JSON carries.
/// </remarks>
public abstract record AnnotationPatch
{
    private protected AnnotationPatch() { }

    /// <summary><c>/Contents</c>. <see cref="JsonNullable{T}.Null"/> removes it.</summary>
    public JsonNullable<string>? Contents { get; init; }

    /// <summary>The fields every annotation shares. A patch leaves <c>Nm</c> out.</summary>
    public AnnotationCommonFields Common { get; init; } = new();

    /// <summary>The field bag as the engine reads it: keys sorted, unset members left out. Throws <c>invalid-input</c> as <see cref="AnnotationDraft.ToJson"/> does.</summary>
    public string ToJson() => FieldBag.Encode(bag =>
    {
        Write(bag);
        bag.Put("contents", Contents);
        (Common ?? throw new FieldBagException("common is null")).Write(bag, patch: true);
    });

    internal abstract void Write(FieldBag bag);

    internal static T Decode<T>(string json, Func<FieldReader, T> read) where T : AnnotationPatch =>
        FieldReader.Decode(json, r => (T)(read(r) with { Contents = r.NullableString("contents"), Common = AnnotationCommonFields.Read(r) }));
}

/// <summary>A patch type that reads from the field bag it writes.</summary>
/// <typeparam name="TSelf">The patch type.</typeparam>
public interface IAnnotationPatch<TSelf> where TSelf : AnnotationPatch, IAnnotationPatch<TSelf>
{
    /// <summary>The patch a field bag holds. Throws <see cref="PdfException"/> with <see cref="PdfErrorCode.InvalidInput"/> for a declared key of the wrong shape.</summary>
    static abstract TSelf FromJson(string json);
}

/// <summary>The members more than one kind writes, read and written one way.</summary>
internal static class AnnotationMembers
{
    internal static void PutRichText(this FieldBag bag, JsonNullable<AnnotationRichText>? value) =>
        bag.Put("richText", value, r => r.ToNode("richText"));

    internal static void PutMeasure(this FieldBag bag, JsonNullable<PdfMeasurement>? value) =>
        bag.Put("measure", value, m => m.ToNode("measure"));

    internal static void PutNumbers(this FieldBag bag, string key, JsonNullable<IReadOnlyList<double>>? value) =>
        bag.Put(key, value, list => AnnotationWire.Numbers(list, key));

    internal static void PutQuads(this FieldBag bag, IReadOnlyList<PdfQuad>? value)
    {
        if (value is not null) bag.Set("quadPoints", AnnotationWire.Quads(value, "quadPoints"));
    }

    internal static void PutPoints(this FieldBag bag, string key, IReadOnlyList<PdfPoint>? value)
    {
        if (value is not null) bag.Set(key, AnnotationWire.Points(value, key));
    }

    internal static void PutLineEndings(this FieldBag bag, AnnotationLineEndings? value)
    {
        if (value is { } v)
        {
            bag.Set("lineEndings", new JsonObject
            {
                ["start"] = Wire<AnnotationLineEnding>.Name(v.Start, "lineEndings"),
                ["end"] = Wire<AnnotationLineEnding>.Name(v.End, "lineEndings"),
            });
        }
    }

    internal static void PutCaptionCenter(this FieldBag bag, AnnotationCaptionCenter? value)
    {
        switch (value)
        {
            case null: break;
            case AnnotationCaptionCenter.At at: bag.Set("captionCenter", AnnotationWire.Point(at.Point, "captionCenter")); break;
            default: bag.Set("captionCenter", null); break;
        }
    }

    internal static JsonNullable<AnnotationRichText>? RichText(this FieldReader r) => r.Nullable("richText", AnnotationRichText.Read);

    internal static JsonNullable<PdfMeasurement>? Measure(this FieldReader r) => r.Nullable("measure", PdfMeasurement.Read);

    internal static IReadOnlyList<PdfQuad>? Quads(this FieldReader r) => r.Value("quadPoints", AnnotationWire.ReadQuads);

    internal static IReadOnlyList<PdfPoint>? Points(this FieldReader r, string key) => r.Value(key, AnnotationWire.ReadPoints);

    internal static AnnotationLineEndings? LineEndings(this FieldReader r) => r.Struct("lineEndings", ReadLineEndings);

    internal static AnnotationLineEndings ReadLineEndings(JsonNode node, string key) =>
        node is JsonObject o && o["start"] is { } start && o["end"] is { } end
            ? new(AnnotationWire.ReadEnum<AnnotationLineEnding>(start, key), AnnotationWire.ReadEnum<AnnotationLineEnding>(end, key))
            : throw new FieldBagException($"{key} is not a pair of line endings");

    internal static AnnotationCaptionCenter? CaptionCenter(this FieldReader r)
    {
        if (!r.Has("captionCenter")) return null;
        return r.Node("captionCenter") is { } node
            ? new AnnotationCaptionCenter.At(AnnotationWire.ReadPoint(node, "captionCenter"))
            : new AnnotationCaptionCenter.Automatic();
    }

    internal static T Required<T>(T? value, string key) where T : class => value ?? throw new FieldBagException($"{key} is required");
}

// ── ink ─────────────────────────────────────────────────────────────────────

/// <summary>A new ink annotation: freehand strokes. A value outside the engine's rule for a member throws <c>invalid-input</c> from the create.</summary>
/// <param name="InkList"><c>/InkList</c>, page space: one list of points per stroke. At least one stroke, and no stroke without points.</param>
public sealed record InkAnnotationDraft(IReadOnlyList<IReadOnlyList<PdfPoint>> InkList) : AnnotationDraft
{
    /// <summary><c>/C</c>. <see cref="JsonNullable{T}.Null"/> draws no stroke.</summary>
    public JsonNullable<AnnotationColor>? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The line width, above 0.</summary>
    public double? StrokeWidth { get; init; }
    /// <summary>The border style.</summary>
    public AnnotationBorderStyle? BorderStyle { get; init; }
    /// <summary>The dash lengths, each 0 or more, at least one. <see cref="JsonNullable{T}.Null"/> is none.</summary>
    public JsonNullable<IReadOnlyList<double>>? DashArray { get; init; }
    /// <summary><see cref="JsonNullable{T}.Null"/> is pen ink.</summary>
    public JsonNullable<AnnotationInkIntent>? Intent { get; init; }
    /// <summary>Degrees. <see cref="JsonNullable{T}.Null"/> is none.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text (<c>/RC</c>); it sets <c>contents</c> to its plain text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag)
    {
        bag.Put("subtype", "ink");
        bag.Set("inkList", AnnotationWire.PointLists(InkList ?? throw new FieldBagException("inkList is required"), "inkList"));
        bag.Put("color", Color);
        bag.Put("opacity", Opacity);
        bag.Put("strokeWidth", StrokeWidth);
        bag.Put("borderStyle", BorderStyle);
        bag.PutNumbers("dashArray", DashArray);
        bag.PutEnum("intent", Intent);
        bag.Put("rotation", Rotation);
        bag.Put("blendMode", BlendMode);
        bag.PutRichText(RichText);
    }
}

/// <summary>A change to an ink annotation, or to an ink tool's defaults.</summary>
public sealed record InkAnnotationPatch : AnnotationPatch, IAnnotationPatch<InkAnnotationPatch>
{
    /// <summary>Replaces every stroke.</summary>
    public IReadOnlyList<IReadOnlyList<PdfPoint>>? InkList { get; init; }
    /// <summary><see cref="JsonNullable{T}.Null"/> removes the stroke colour.</summary>
    public JsonNullable<AnnotationColor>? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The line width, above 0.</summary>
    public double? StrokeWidth { get; init; }
    /// <summary>The border style.</summary>
    public AnnotationBorderStyle? BorderStyle { get; init; }
    /// <summary><see cref="JsonNullable{T}.Null"/> removes the pattern.</summary>
    public JsonNullable<IReadOnlyList<double>>? DashArray { get; init; }
    /// <summary><see cref="JsonNullable{T}.Null"/> makes it pen ink.</summary>
    public JsonNullable<AnnotationInkIntent>? Intent { get; init; }
    /// <summary><see cref="JsonNullable{T}.Null"/> removes the rotation.</summary>
    public JsonNullable<double>? Rotation { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text. <see cref="JsonNullable{T}.Null"/> makes it plain and keeps <c>contents</c>.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag)
    {
        if (InkList is not null) bag.Set("inkList", AnnotationWire.PointLists(InkList, "inkList"));
        bag.Put("color", Color);
        bag.Put("opacity", Opacity);
        bag.Put("strokeWidth", StrokeWidth);
        bag.Put("borderStyle", BorderStyle);
        bag.PutNumbers("dashArray", DashArray);
        bag.PutEnum("intent", Intent);
        bag.Put("rotation", Rotation);
        bag.Put("blendMode", BlendMode);
        bag.PutRichText(RichText);
    }

    /// <inheritdoc/>
    public static InkAnnotationPatch FromJson(string json) => Decode(json, r => new InkAnnotationPatch
    {
        InkList = r.Value("inkList", AnnotationWire.ReadPointLists),
        Color = r.NullableColor("color"),
        Opacity = r.Double("opacity"),
        StrokeWidth = r.Double("strokeWidth"),
        BorderStyle = r.Enum<AnnotationBorderStyle>("borderStyle"),
        DashArray = r.NullableNumbers("dashArray"),
        Intent = r.NullableEnum<AnnotationInkIntent>("intent"),
        Rotation = r.NullableDouble("rotation"),
        BlendMode = r.Enum<AnnotationBlendMode>("blendMode"),
        RichText = r.RichText(),
    });
}

// ── highlight ───────────────────────────────────────────────────────────────

/// <summary>A new highlight over text quads.</summary>
/// <param name="QuadPoints"><c>/QuadPoints</c>, page space: one quad per run of marked text, at least one.</param>
public sealed record HighlightAnnotationDraft(IReadOnlyList<PdfQuad> QuadPoints) : AnnotationDraft
{
    /// <summary><c>/C</c>. A highlight always has one.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag)
    {
        bag.Put("subtype", "highlight");
        bag.PutQuads(AnnotationMembers.Required(QuadPoints, "quadPoints"));
        bag.Put("color", Color);
        bag.Put("opacity", Opacity);
        bag.Put("blendMode", BlendMode);
        bag.PutRichText(RichText);
    }
}

/// <summary>A change to a highlight, or to a highlight tool's defaults.</summary>
public sealed record HighlightAnnotationPatch : AnnotationPatch, IAnnotationPatch<HighlightAnnotationPatch>
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

    internal override void Write(FieldBag bag)
    {
        bag.PutQuads(QuadPoints);
        bag.Put("color", Color);
        bag.Put("opacity", Opacity);
        bag.Put("blendMode", BlendMode);
        bag.PutRichText(RichText);
    }

    /// <inheritdoc/>
    public static HighlightAnnotationPatch FromJson(string json) => Decode(json, r => new HighlightAnnotationPatch
    {
        QuadPoints = r.Quads(),
        Color = r.Color("color"),
        Opacity = r.Double("opacity"),
        BlendMode = r.Enum<AnnotationBlendMode>("blendMode"),
        RichText = r.RichText(),
    });
}

// ── note ────────────────────────────────────────────────────────────────────

/// <summary>A new note: a <c>text</c> annotation, an icon with a comment.</summary>
/// <param name="Rect">Where its icon sits, page space.</param>
public sealed record NoteAnnotationDraft(PdfRect Rect) : AnnotationDraft
{
    /// <summary><c>/C</c>.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The icon.</summary>
    public AnnotationNoteIcon? Icon { get; init; }
    /// <summary>Whether its popup starts open.</summary>
    public bool? Open { get; init; }
    /// <summary>The review state, such as <c>accepted</c>.</summary>
    public JsonNullable<string>? State { get; init; }
    /// <summary>The state model, such as <c>review</c>.</summary>
    public JsonNullable<string>? StateModel { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag)
    {
        bag.Put("subtype", "text");
        bag.Put("rect", Rect);
        bag.Put("color", Color);
        bag.Put("opacity", Opacity);
        bag.Put("icon", Icon);
        bag.Put("open", Open);
        bag.Put("state", State);
        bag.Put("stateModel", StateModel);
        bag.Put("blendMode", BlendMode);
        bag.PutRichText(RichText);
    }
}

/// <summary>A change to a note, or to a note tool's defaults.</summary>
public sealed record NoteAnnotationPatch : AnnotationPatch, IAnnotationPatch<NoteAnnotationPatch>
{
    /// <summary>Moves the icon.</summary>
    public PdfRect? Rect { get; init; }
    /// <summary><c>/C</c>.</summary>
    public AnnotationColor? Color { get; init; }
    /// <summary><c>/CA</c>, 0 to 1.</summary>
    public double? Opacity { get; init; }
    /// <summary>The icon.</summary>
    public AnnotationNoteIcon? Icon { get; init; }
    /// <summary>Whether its popup is open.</summary>
    public bool? Open { get; init; }
    /// <summary>The review state.</summary>
    public JsonNullable<string>? State { get; init; }
    /// <summary>The state model.</summary>
    public JsonNullable<string>? StateModel { get; init; }
    /// <summary>How the appearance blends with the page under it.</summary>
    public AnnotationBlendMode? BlendMode { get; init; }
    /// <summary>The comment as styled text.</summary>
    public JsonNullable<AnnotationRichText>? RichText { get; init; }

    internal override void Write(FieldBag bag)
    {
        bag.Put("rect", Rect);
        bag.Put("color", Color);
        bag.Put("opacity", Opacity);
        bag.Put("icon", Icon);
        bag.Put("open", Open);
        bag.Put("state", State);
        bag.Put("stateModel", StateModel);
        bag.Put("blendMode", BlendMode);
        bag.PutRichText(RichText);
    }

    /// <inheritdoc/>
    public static NoteAnnotationPatch FromJson(string json) => Decode(json, r => new NoteAnnotationPatch
    {
        Rect = r.Rect("rect"),
        Color = r.Color("color"),
        Opacity = r.Double("opacity"),
        Icon = r.Enum<AnnotationNoteIcon>("icon"),
        Open = r.Bool("open"),
        State = r.NullableString("state"),
        StateModel = r.NullableString("stateModel"),
        BlendMode = r.Enum<AnnotationBlendMode>("blendMode"),
        RichText = r.RichText(),
    });
}
