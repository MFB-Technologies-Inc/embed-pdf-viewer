namespace EmbedPDF;

// The closed sets the typed drafts and patches write. Each case's wire
// spelling is the engine's (`crates/epdf-engine/src/features/annotation/
// vocabulary.rs` and `read_field` in `write/prepare.rs`); the spellings live
// in AnnotationVocabulary.NamesOf, in case order. A value outside an enum's
// cases is `invalid-input` from ToJson and from every call that writes it.

/// <summary>The border styles the engine writes for ink, shapes and free text (<c>solid</c>, <c>dashed</c>).</summary>
public enum AnnotationBorderStyle
{
    /// <summary><c>solid</c>.</summary>
    Solid,
    /// <summary><c>dashed</c>.</summary>
    Dashed,
}

/// <summary>An ink annotation's <c>/IT</c>. An ink without one is pen ink.</summary>
public enum AnnotationInkIntent
{
    /// <summary><c>ink-highlight</c>: a freehand highlighter.</summary>
    InkHighlight,
}

/// <summary>A note's <c>/Name</c> icon.</summary>
public enum AnnotationNoteIcon
{
    /// <summary><c>comment</c>.</summary>
    Comment,
    /// <summary><c>key</c>.</summary>
    Key,
    /// <summary><c>note</c>.</summary>
    Note,
    /// <summary><c>help</c>.</summary>
    Help,
    /// <summary><c>new-paragraph</c>.</summary>
    NewParagraph,
    /// <summary><c>paragraph</c>.</summary>
    Paragraph,
    /// <summary><c>insert</c>.</summary>
    Insert,
}

/// <summary><c>/BM</c> on an annotation's appearance: how it blends with the page under it.</summary>
public enum AnnotationBlendMode
{
    /// <summary><c>normal</c>.</summary>
    Normal,
    /// <summary><c>multiply</c>.</summary>
    Multiply,
    /// <summary><c>screen</c>.</summary>
    Screen,
    /// <summary><c>overlay</c>.</summary>
    Overlay,
    /// <summary><c>darken</c>.</summary>
    Darken,
    /// <summary><c>lighten</c>.</summary>
    Lighten,
    /// <summary><c>color-dodge</c>.</summary>
    ColorDodge,
    /// <summary><c>color-burn</c>.</summary>
    ColorBurn,
    /// <summary><c>hard-light</c>.</summary>
    HardLight,
    /// <summary><c>soft-light</c>.</summary>
    SoftLight,
    /// <summary><c>difference</c>.</summary>
    Difference,
    /// <summary><c>exclusion</c>.</summary>
    Exclusion,
    /// <summary><c>hue</c>.</summary>
    Hue,
    /// <summary><c>saturation</c>.</summary>
    Saturation,
    /// <summary><c>color</c>.</summary>
    Color,
    /// <summary><c>luminosity</c>.</summary>
    Luminosity,
}

/// <summary>A <c>/LE</c> line ending.</summary>
public enum AnnotationLineEnding
{
    /// <summary><c>none</c>: the line just stops.</summary>
    None,
    /// <summary><c>square</c>.</summary>
    Square,
    /// <summary><c>circle</c>.</summary>
    Circle,
    /// <summary><c>diamond</c>.</summary>
    Diamond,
    /// <summary><c>open-arrow</c>.</summary>
    OpenArrow,
    /// <summary><c>closed-arrow</c>.</summary>
    ClosedArrow,
    /// <summary><c>butt</c>.</summary>
    Butt,
    /// <summary><c>r-open-arrow</c>.</summary>
    ReversedOpenArrow,
    /// <summary><c>r-closed-arrow</c>.</summary>
    ReversedClosedArrow,
    /// <summary><c>slash</c>.</summary>
    Slash,
}

/// <summary>How text lines up across its box (<c>/Q</c>). Only free text takes <see cref="Justify"/>.</summary>
public enum AnnotationTextAlignment
{
    /// <summary><c>left</c>.</summary>
    Left,
    /// <summary><c>center</c>.</summary>
    Center,
    /// <summary><c>right</c>.</summary>
    Right,
    /// <summary><c>justify</c>.</summary>
    Justify,
}

/// <summary>Where free text sits in its box, top to bottom.</summary>
public enum AnnotationVerticalAlignment
{
    /// <summary><c>top</c>.</summary>
    Top,
    /// <summary><c>middle</c>.</summary>
    Middle,
    /// <summary><c>bottom</c>.</summary>
    Bottom,
}

/// <summary>A strikeout's <c>/IT</c>. A strikeout without one is a plain strikeout.</summary>
public enum AnnotationStrikeoutIntent
{
    /// <summary><c>strikeout-text-edit</c>: text marked for deletion.</summary>
    StrikeoutTextEdit,
}

/// <summary>A line's <c>/IT</c>. A line without one is a plain line.</summary>
public enum AnnotationLineIntent
{
    /// <summary><c>line-arrow</c>.</summary>
    LineArrow,
    /// <summary><c>line-dimension</c>: a distance measurement, scaled by its measure.</summary>
    LineDimension,
}

/// <summary>A polygon's <c>/IT</c>. A polygon without one is a plain polygon.</summary>
public enum AnnotationPolygonIntent
{
    /// <summary><c>polygon-cloud</c>.</summary>
    PolygonCloud,
    /// <summary><c>polygon-dimension</c>: an area measurement, scaled by its measure.</summary>
    PolygonDimension,
}

/// <summary>A polyline's <c>/IT</c>. A polyline without one is a plain polyline.</summary>
public enum AnnotationPolylineIntent
{
    /// <summary><c>polyline-dimension</c>: a perimeter measurement, scaled by its measure.</summary>
    PolylineDimension,
}

/// <summary>A free text's <c>/IT</c>.</summary>
public enum AnnotationFreeTextIntent
{
    /// <summary><c>free-text</c>: a text box.</summary>
    FreeText,
    /// <summary><c>free-text-callout</c>: a text box with a leader line to what it calls out.</summary>
    FreeTextCallout,
}

/// <summary>A caret's <c>/IT</c>. A caret without one marks an insertion.</summary>
public enum AnnotationCaretIntent
{
    /// <summary><c>replace</c>: the caret of a replacement, paired with a strikeout.</summary>
    Replace,
}

/// <summary>A line's <c>/CP</c>: where its caption sits.</summary>
public enum AnnotationCaptionPosition
{
    /// <summary><c>inline</c>: in the line, which breaks around it.</summary>
    Inline,
    /// <summary><c>top</c>: above the line.</summary>
    Top,
}

/// <summary>A stamp's fit: how its image fills its box.</summary>
public enum AnnotationStampFit
{
    /// <summary><c>contain</c>.</summary>
    Contain,
    /// <summary><c>cover</c>.</summary>
    Cover,
    /// <summary><c>fill</c>.</summary>
    Fill,
}

/// <summary>A file attachment's <c>/Name</c> icon.</summary>
public enum AnnotationFileAttachmentIcon
{
    /// <summary><c>graph</c>.</summary>
    Graph,
    /// <summary><c>push-pin</c>.</summary>
    PushPin,
    /// <summary><c>paperclip</c>.</summary>
    Paperclip,
    /// <summary><c>tag</c>.</summary>
    Tag,
}

/// <summary>The page verbs a link may name.</summary>
public enum AnnotationLinkVerb
{
    /// <summary><c>NextPage</c>.</summary>
    NextPage,
    /// <summary><c>PrevPage</c>.</summary>
    PreviousPage,
    /// <summary><c>FirstPage</c>.</summary>
    FirstPage,
    /// <summary><c>LastPage</c>.</summary>
    LastPage,
}

/// <summary>How a reply relates to the annotation it answers.</summary>
public enum AnnotationReplyType
{
    /// <summary><c>reply</c>: a comment on it.</summary>
    Reply,
    /// <summary><c>group</c>: a member of its group.</summary>
    Group,
}

/// <summary>A number format's fraction display.</summary>
public enum MeasureFraction
{
    /// <summary><c>decimal</c>.</summary>
    Decimal,
    /// <summary><c>fraction</c>.</summary>
    Fraction,
    /// <summary><c>round</c>.</summary>
    Round,
    /// <summary><c>truncate</c>.</summary>
    Truncate,
}

/// <summary>Where a number format writes its unit label.</summary>
public enum MeasureLabelPosition
{
    /// <summary><c>suffix</c>.</summary>
    Suffix,
    /// <summary><c>prefix</c>.</summary>
    Prefix,
}

/// <summary>The <c>/LE</c> pair a line or polyline draws at its two ends.</summary>
/// <param name="Start">At the first point.</param>
/// <param name="End">At the last point.</param>
public readonly record struct AnnotationLineEndings(AnnotationLineEnding Start, AnnotationLineEnding End);

/// <summary>A line caption's displacement (<c>/CO</c>) in the line's own axes, in PDF units.</summary>
/// <param name="Along">Along the line from its midpoint.</param>
/// <param name="Perpendicular">Away from the line.</param>
public readonly record struct AnnotationCaptionOffset(double Along, double Perpendicular);

/// <summary>A line's leader lines: <c>/LL</c>, <c>/LLE</c> and <c>/LLO</c>.</summary>
/// <param name="Length"><c>/LL</c>: how far the leader lines reach from the line's ends, along its normal. Negative reaches the other way.</param>
public readonly record struct AnnotationLineLeader(double Length)
{
    /// <summary><c>/LLE</c>: how far the leader lines run past the line, 0 or more.</summary>
    public double? Extension { get; init; }

    /// <summary><c>/LLO</c>: the gap between the points and the leader lines, 0 or more.</summary>
    public double? Offset { get; init; }
}

/// <summary>Where a polygon's or polyline's caption sits.</summary>
public abstract record AnnotationCaptionCenter
{
    private protected AnnotationCaptionCenter() { }

    /// <summary>Placed by the engine. Writes <c>null</c>.</summary>
    public sealed record Automatic : AnnotationCaptionCenter;

    /// <summary>Centred on a page-space point.</summary>
    /// <param name="Point">The centre.</param>
    public sealed record At(PdfPoint Point) : AnnotationCaptionCenter;
}

/// <summary>A free-text callout's leader line (<c>/CL</c>), page space: from the point called out to the text box.</summary>
public abstract record AnnotationCalloutLine
{
    private protected AnnotationCalloutLine() { }

    /// <summary>The two or three points, in order.</summary>
    public abstract IReadOnlyList<PdfPoint> Points { get; }

    /// <summary>A straight line.</summary>
    /// <param name="From">The point called out.</param>
    /// <param name="To">The text box end.</param>
    public sealed record Straight(PdfPoint From, PdfPoint To) : AnnotationCalloutLine
    {
        /// <inheritdoc/>
        public override IReadOnlyList<PdfPoint> Points => [From, To];
    }

    /// <summary>A line with one knee.</summary>
    /// <param name="From">The point called out.</param>
    /// <param name="Knee">The knee.</param>
    /// <param name="To">The text box end.</param>
    public sealed record Kneed(PdfPoint From, PdfPoint Knee, PdfPoint To) : AnnotationCalloutLine
    {
        /// <inheritdoc/>
        public override IReadOnlyList<PdfPoint> Points => [From, Knee, To];
    }

    internal static AnnotationCalloutLine? FromPoints(IReadOnlyList<PdfPoint> points) => points.Count switch
    {
        2 => new Straight(points[0], points[1]),
        3 => new Kneed(points[0], points[1], points[2]),
        _ => null,
    };
}

internal static class AnnotationVocabulary
{
    internal static string[] NamesOf(Type type) =>
        type == typeof(AnnotationBorderStyle) ? ["solid", "dashed"]
        : type == typeof(AnnotationInkIntent) ? ["ink-highlight"]
        : type == typeof(AnnotationNoteIcon) ? ["comment", "key", "note", "help", "new-paragraph", "paragraph", "insert"]
        : type == typeof(AnnotationBlendMode) ? ["normal", "multiply", "screen", "overlay", "darken", "lighten", "color-dodge", "color-burn", "hard-light", "soft-light", "difference", "exclusion", "hue", "saturation", "color", "luminosity"]
        : type == typeof(AnnotationLineEnding) ? ["none", "square", "circle", "diamond", "open-arrow", "closed-arrow", "butt", "r-open-arrow", "r-closed-arrow", "slash"]
        : type == typeof(AnnotationTextAlignment) ? ["left", "center", "right", "justify"]
        : type == typeof(AnnotationVerticalAlignment) ? ["top", "middle", "bottom"]
        : type == typeof(AnnotationStrikeoutIntent) ? ["strikeout-text-edit"]
        : type == typeof(AnnotationLineIntent) ? ["line-arrow", "line-dimension"]
        : type == typeof(AnnotationPolygonIntent) ? ["polygon-cloud", "polygon-dimension"]
        : type == typeof(AnnotationPolylineIntent) ? ["polyline-dimension"]
        : type == typeof(AnnotationFreeTextIntent) ? ["free-text", "free-text-callout"]
        : type == typeof(AnnotationCaretIntent) ? ["replace"]
        : type == typeof(AnnotationCaptionPosition) ? ["inline", "top"]
        : type == typeof(AnnotationStampFit) ? ["contain", "cover", "fill"]
        : type == typeof(AnnotationFileAttachmentIcon) ? ["graph", "push-pin", "paperclip", "tag"]
        : type == typeof(AnnotationLinkVerb) ? ["NextPage", "PrevPage", "FirstPage", "LastPage"]
        : type == typeof(AnnotationReplyType) ? ["reply", "group"]
        : type == typeof(MeasureFraction) ? ["decimal", "fraction", "round", "truncate"]
        : type == typeof(MeasureLabelPosition) ? ["suffix", "prefix"]
        : throw new InvalidOperationException($"no wire names for {type.Name}");

    /// <summary>The wire spelling of a vocabulary value.</summary>
    internal static string WireName<T>(T value) where T : struct, Enum => Wire<T>.Name(value, typeof(T).Name);
}

/// <summary>
/// An annotation's subtype as the engine names it. The set is open: an
/// annotation a newer engine reads keeps its name.
/// </summary>
/// <param name="Value">The wire string.</param>
public readonly record struct AnnotationSubtype(string Value)
{
    /// <summary>A subtype by its wire string.</summary>
    public static implicit operator AnnotationSubtype(string value) => new(value);

    /// <summary>The wire string.</summary>
    public override string ToString() => Value;

    /// <summary><c>text</c>: a note.</summary>
    public static AnnotationSubtype Text { get; } = new("text");
    /// <summary><c>link</c>.</summary>
    public static AnnotationSubtype Link { get; } = new("link");
    /// <summary><c>free-text</c>.</summary>
    public static AnnotationSubtype FreeText { get; } = new("free-text");
    /// <summary><c>line</c>.</summary>
    public static AnnotationSubtype Line { get; } = new("line");
    /// <summary><c>square</c>.</summary>
    public static AnnotationSubtype Square { get; } = new("square");
    /// <summary><c>circle</c>.</summary>
    public static AnnotationSubtype Circle { get; } = new("circle");
    /// <summary><c>polygon</c>.</summary>
    public static AnnotationSubtype Polygon { get; } = new("polygon");
    /// <summary><c>polyline</c>.</summary>
    public static AnnotationSubtype Polyline { get; } = new("polyline");
    /// <summary><c>highlight</c>.</summary>
    public static AnnotationSubtype Highlight { get; } = new("highlight");
    /// <summary><c>underline</c>.</summary>
    public static AnnotationSubtype Underline { get; } = new("underline");
    /// <summary><c>squiggly</c>.</summary>
    public static AnnotationSubtype Squiggly { get; } = new("squiggly");
    /// <summary><c>strikeout</c>.</summary>
    public static AnnotationSubtype Strikeout { get; } = new("strikeout");
    /// <summary><c>stamp</c>.</summary>
    public static AnnotationSubtype Stamp { get; } = new("stamp");
    /// <summary><c>caret</c>.</summary>
    public static AnnotationSubtype Caret { get; } = new("caret");
    /// <summary><c>ink</c>.</summary>
    public static AnnotationSubtype Ink { get; } = new("ink");
    /// <summary><c>popup</c>.</summary>
    public static AnnotationSubtype Popup { get; } = new("popup");
    /// <summary><c>file-attachment</c>.</summary>
    public static AnnotationSubtype FileAttachment { get; } = new("file-attachment");
    /// <summary><c>redact</c>.</summary>
    public static AnnotationSubtype Redact { get; } = new("redact");
    /// <summary><c>widget</c>: a form field's widget.</summary>
    public static AnnotationSubtype Widget { get; } = new("widget");
    /// <summary><c>unsupported</c>: a subtype the engine doesn't read.</summary>
    public static AnnotationSubtype Unsupported { get; } = new("unsupported");

    /// <summary>Every named subtype, in the engine's order.</summary>
    public static IReadOnlyList<AnnotationSubtype> Named { get; } =
    [
        Text, Link, FreeText, Line, Square, Circle, Polygon, Polyline, Highlight, Underline, Squiggly, Strikeout, Stamp,
        Caret, Ink, Popup, FileAttachment, Redact, Widget, Unsupported,
    ];
}

/// <summary>
/// An annotation's <c>/IT</c> as a read hands it out. The set is open: a
/// file can carry an intent no constant names, and it keeps its text.
/// </summary>
/// <param name="Value">The wire string.</param>
public readonly record struct AnnotationIntent(string Value)
{
    /// <summary>An intent by its wire string.</summary>
    public static implicit operator AnnotationIntent(string value) => new(value);

    /// <summary>The wire string.</summary>
    public override string ToString() => Value;

    /// <summary><c>ink-highlight</c>.</summary>
    public static AnnotationIntent InkHighlight { get; } = new("ink-highlight");
    /// <summary><c>strikeout-text-edit</c>.</summary>
    public static AnnotationIntent StrikeoutTextEdit { get; } = new("strikeout-text-edit");
    /// <summary><c>line-arrow</c>.</summary>
    public static AnnotationIntent LineArrow { get; } = new("line-arrow");
    /// <summary><c>line-dimension</c>.</summary>
    public static AnnotationIntent LineDimension { get; } = new("line-dimension");
    /// <summary><c>polygon-cloud</c>.</summary>
    public static AnnotationIntent PolygonCloud { get; } = new("polygon-cloud");
    /// <summary><c>polygon-dimension</c>.</summary>
    public static AnnotationIntent PolygonDimension { get; } = new("polygon-dimension");
    /// <summary><c>polyline-dimension</c>.</summary>
    public static AnnotationIntent PolylineDimension { get; } = new("polyline-dimension");
    /// <summary><c>free-text</c>.</summary>
    public static AnnotationIntent FreeText { get; } = new("free-text");
    /// <summary><c>free-text-callout</c>.</summary>
    public static AnnotationIntent FreeTextCallout { get; } = new("free-text-callout");
    /// <summary><c>free-text-typewriter</c>.</summary>
    public static AnnotationIntent FreeTextTypewriter { get; } = new("free-text-typewriter");
    /// <summary><c>replace</c>.</summary>
    public static AnnotationIntent Replace { get; } = new("replace");
}

/// <summary>
/// A border style as a read hands it out. Open, since a read can name a style
/// a write refuses (<c>beveled</c>, <c>inset</c>); <see cref="AnnotationBorderStyle"/>
/// is the set drafts write.
/// </summary>
/// <param name="Value">The wire string.</param>
public readonly record struct AnnotationBorderStyleName(string Value)
{
    /// <summary>A style by its wire string.</summary>
    public static implicit operator AnnotationBorderStyleName(string value) => new(value);

    /// <summary>The wire string.</summary>
    public override string ToString() => Value;

    /// <summary><c>solid</c>.</summary>
    public static AnnotationBorderStyleName Solid { get; } = new("solid");
    /// <summary><c>dashed</c>.</summary>
    public static AnnotationBorderStyleName Dashed { get; } = new("dashed");
    /// <summary><c>beveled</c>.</summary>
    public static AnnotationBorderStyleName Beveled { get; } = new("beveled");
    /// <summary><c>inset</c>.</summary>
    public static AnnotationBorderStyleName Inset { get; } = new("inset");
}
