namespace EmbedPDF;

/// <summary>
/// A tool's id in a document's interaction hub, which every plugin's tools
/// join: the annotation plugin's authoring tools, the form plugin's field
/// tools, and the hub's own. <see cref="Value"/> is the wire string.
/// </summary>
/// <remarks>
/// The set is open, so this is a value type with constants rather than an
/// enum: the constants are the built-in tools, and an app's own tool is a
/// string (<c>ToolId laser = "laser";</c>). A tool a newer engine adds needs
/// no new constant to pass.
/// </remarks>
/// <param name="Value">The wire string.</param>
public readonly record struct ToolId(string Value)
{
    /// <summary>A tool by its wire string.</summary>
    public static implicit operator ToolId(string value) => new(value);

    /// <summary>The wire string.</summary>
    public override string ToString() => Value;

    // The hub's own.
    /// <summary><c>pointer</c>: selects text, edits annotations, fills forms and follows links. The default tool.</summary>
    public static ToolId Pointer { get; } = new("pointer");
    /// <summary><c>pan</c>.</summary>
    public static ToolId Pan { get; } = new("pan");
    /// <summary><c>zoom</c>.</summary>
    public static ToolId Zoom { get; } = new("zoom");
    // The render plugin's.
    /// <summary><c>snapshot</c>.</summary>
    public static ToolId Snapshot { get; } = new("snapshot");
    // The form plugin's.
    /// <summary><c>form-fill</c>.</summary>
    public static ToolId FormFill { get; } = new("form-fill");
    /// <summary><c>form-edit</c>.</summary>
    public static ToolId FormEdit { get; } = new("form-edit");
    /// <summary><c>form-text</c>.</summary>
    public static ToolId FormText { get; } = new("form-text");
    /// <summary><c>form-checkbox</c>.</summary>
    public static ToolId FormCheckbox { get; } = new("form-checkbox");
    /// <summary><c>form-radio</c>.</summary>
    public static ToolId FormRadio { get; } = new("form-radio");
    /// <summary><c>form-combobox</c>.</summary>
    public static ToolId FormCombobox { get; } = new("form-combobox");
    /// <summary><c>form-listbox</c>.</summary>
    public static ToolId FormListbox { get; } = new("form-listbox");
    /// <summary><c>form-button</c>.</summary>
    public static ToolId FormButton { get; } = new("form-button");
    /// <summary><c>form-signature</c>.</summary>
    public static ToolId FormSignature { get; } = new("form-signature");
    // The annotation plugin's.
    /// <summary><c>square</c>.</summary>
    public static ToolId Square { get; } = new("square");
    /// <summary><c>circle</c>.</summary>
    public static ToolId Circle { get; } = new("circle");
    /// <summary><c>line</c>.</summary>
    public static ToolId Line { get; } = new("line");
    /// <summary><c>distance</c>.</summary>
    public static ToolId Distance { get; } = new("distance");
    /// <summary><c>calibrate</c>.</summary>
    public static ToolId Calibrate { get; } = new("calibrate");
    /// <summary><c>polygon</c>.</summary>
    public static ToolId Polygon { get; } = new("polygon");
    /// <summary><c>polyline</c>.</summary>
    public static ToolId Polyline { get; } = new("polyline");
    /// <summary><c>perimeter</c>.</summary>
    public static ToolId Perimeter { get; } = new("perimeter");
    /// <summary><c>area</c>.</summary>
    public static ToolId Area { get; } = new("area");
    /// <summary><c>ink</c>.</summary>
    public static ToolId Ink { get; } = new("ink");
    /// <summary><c>ink-highlight</c>.</summary>
    public static ToolId InkHighlight { get; } = new("ink-highlight");
    /// <summary><c>free-text</c>.</summary>
    public static ToolId FreeText { get; } = new("free-text");
    /// <summary><c>free-text-callout</c>.</summary>
    public static ToolId FreeTextCallout { get; } = new("free-text-callout");
    /// <summary><c>highlight</c>.</summary>
    public static ToolId Highlight { get; } = new("highlight");
    /// <summary><c>redact</c>.</summary>
    public static ToolId Redact { get; } = new("redact");
    /// <summary><c>underline</c>.</summary>
    public static ToolId Underline { get; } = new("underline");
    /// <summary><c>strikeout</c>.</summary>
    public static ToolId Strikeout { get; } = new("strikeout");
    /// <summary><c>squiggly</c>.</summary>
    public static ToolId Squiggly { get; } = new("squiggly");
    /// <summary><c>insert-text</c>.</summary>
    public static ToolId InsertText { get; } = new("insert-text");
    /// <summary><c>replace-text</c>.</summary>
    public static ToolId ReplaceText { get; } = new("replace-text");
    /// <summary><c>stamp</c>.</summary>
    public static ToolId Stamp { get; } = new("stamp");
    /// <summary><c>link</c>.</summary>
    public static ToolId Link { get; } = new("link");
    /// <summary><c>note</c>.</summary>
    public static ToolId Note { get; } = new("note");
    /// <summary><c>attachment</c>.</summary>
    public static ToolId Attachment { get; } = new("attachment");

    /// <summary>Every constant this type names.</summary>
    public static IReadOnlyList<ToolId> Named { get; } =
    [
        Pointer, Pan, Zoom, Snapshot, FormFill, FormEdit, FormText, FormCheckbox, FormRadio,
        FormCombobox, FormListbox, FormButton, FormSignature, Square, Circle, Line, Distance,
        Calibrate, Polygon, Polyline, Perimeter, Area, Ink, InkHighlight, FreeText,
        FreeTextCallout, Highlight, Redact, Underline, Strikeout, Squiggly, InsertText,
        ReplaceText, Stamp, Link, Note, Attachment,
    ];
}
