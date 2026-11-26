using GA = uniffi.epdf_plugin_annotation;

namespace EmbedPDF;

/// <summary>Which annotations <see cref="AnnotationCapability.ListAsync"/> returns. Both left out lists all.</summary>
public sealed record AnnotationFilter
{
    /// <summary>Only these pages.</summary>
    public IReadOnlyList<PageRef>? Pages { get; init; }

    /// <summary>Only this subtype.</summary>
    public AnnotationSubtype? Subtype { get; init; }
}

/// <summary>How <see cref="AnnotationCapability.CreateAsync(AnnotationDraft, PageRef, AnnotationCreateOptions?, CancellationToken)"/> creates.</summary>
public sealed record AnnotationCreateOptions
{
    /// <summary>The tool whose defaults the annotation starts from. Left out, it takes no defaults.</summary>
    public ToolId? Tool { get; init; }

    /// <summary>Whether to select it afterwards.</summary>
    public bool Select { get; init; }
}

/// <summary>An annotation a batch change could not apply.</summary>
/// <param name="AnnotationRef">The annotation.</param>
/// <param name="Error">Why.</param>
public sealed record AnnotationBatchFailure(AnnotationRef AnnotationRef, PluginErrorInfo Error);

/// <summary>What a change to every selected annotation did.</summary>
/// <param name="Applied">The annotations it changed.</param>
/// <param name="Failed">The ones it could not.</param>
public sealed record AnnotationBatchResult(IReadOnlyList<AnnotationRef> Applied, IReadOnlyList<AnnotationBatchFailure> Failed);

/// <summary>Where an imported annotation landed.</summary>
/// <param name="From">Its ref in the bundle.</param>
/// <param name="To">Its ref in this document.</param>
public sealed record AnnotationRefMapping(AnnotationRef From, AnnotationRef To);

/// <summary>What an import created.</summary>
/// <param name="Annotations">The new annotations.</param>
/// <param name="RefMap">Bundle refs to new refs.</param>
/// <param name="DroppedJson">What the import left out and why, as the engine's JSON.</param>
public sealed record AnnotationImportResult(IReadOnlyList<AnnotationInfo> Annotations, IReadOnlyList<AnnotationRefMapping> RefMap, string DroppedJson);

/// <summary>How <see cref="AnnotationCapability.DuplicateAsync"/> copies.</summary>
public sealed record AnnotationDuplicateOptions
{
    /// <summary>The page the copies go on. Left out, each goes on its original's page.</summary>
    public PageRef? Page { get; init; }

    /// <summary>How far each copy moves, in page points: <c>X</c> to the right, <c>Y</c> down. Left out, they don't move.</summary>
    public PdfPoint? Offset { get; init; }

    /// <summary>Whether to select the copies as they show.</summary>
    public bool Select { get; init; }
}

/// <summary>What a duplicate made.</summary>
/// <param name="Annotations">The copies, in the order their originals were named.</param>
/// <param name="DroppedJson">What was left out and why, as the engine's JSON array.</param>
public sealed record AnnotationDuplicateResult(IReadOnlyList<AnnotationInfo> Annotations, string DroppedJson);

/// <summary>Why <see cref="AnnotationCapability.StageRemeasureAsync"/> left a measurement as it was.</summary>
public enum AnnotationRemeasureSkipReason
{
    /// <summary>The session may not change it.</summary>
    NoAuthority,
    /// <summary>It is locked.</summary>
    Locked,
    /// <summary>Its scale is not a rectilinear one.</summary>
    ForeignMeasure,
    /// <summary>It has no readout at the new scale.</summary>
    Unavailable,
}

/// <summary>A measurement a remeasure left as it was.</summary>
/// <param name="AnnotationRef">The measurement.</param>
/// <param name="Reason">Why.</param>
public sealed record AnnotationRemeasureSkip(AnnotationRef AnnotationRef, AnnotationRemeasureSkipReason Reason);

/// <summary>What <see cref="AnnotationCapability.StageRemeasureAsync"/> did to a page's measurements.</summary>
/// <param name="Page">The page.</param>
/// <param name="Updated">The measurements it updated to the new scale.</param>
/// <param name="Skipped">The ones it left as they were.</param>
/// <param name="Failed">The ones it could not update.</param>
/// <param name="Error">Set, with nothing updated, when the annotations weren't loaded.</param>
public sealed record AnnotationRemeasureReport(PageRef Page, IReadOnlyList<AnnotationRef> Updated, IReadOnlyList<AnnotationRemeasureSkip> Skipped, IReadOnlyList<AnnotationBatchFailure> Failed, PluginErrorInfo? Error);

/// <summary>One comment of a thread.</summary>
/// <param name="AnnotationRef">The annotation that holds it.</param>
/// <param name="Author">Its author.</param>
/// <param name="Contents">Its text.</param>
/// <param name="ModifiedAt">When it last changed, as the engine's timestamp text.</param>
public sealed record AnnotationComment(AnnotationRef AnnotationRef, string? Author, string? Contents, string? ModifiedAt);

/// <summary>A comment thread: an annotation and its replies.</summary>
/// <param name="Root">The annotation the thread is on.</param>
/// <param name="Replies">Its replies, oldest first.</param>
/// <param name="Status">Its review status, if any.</param>
/// <param name="MarkedBy">Who marked it.</param>
public sealed record AnnotationCommentThread(AnnotationComment Root, IReadOnlyList<AnnotationComment> Replies, string? Status, IReadOnlyList<string> MarkedBy);

/// <summary>What this session may do to a comment.</summary>
/// <param name="CanReply">Reply to it.</param>
/// <param name="CanEditText">Change its text.</param>
/// <param name="CanDelete">Delete it.</param>
/// <param name="CanSetStatus">Set its thread's status.</param>
/// <param name="CanDeleteThread">Delete its whole thread.</param>
public sealed record AnnotationCommentPermissions(bool CanReply, bool CanEditText, bool CanDelete, bool CanSetStatus, bool CanDeleteThread);

/// <summary>A thread's review status.</summary>
public enum AnnotationThreadStatus
{
    /// <summary>Accepted.</summary>
    Accepted,
    /// <summary>Rejected.</summary>
    Rejected,
    /// <summary>Cancelled.</summary>
    Cancelled,
    /// <summary>Completed.</summary>
    Completed,
    /// <summary>No status.</summary>
    None,
}

/// <summary>What changed in a thread.</summary>
public enum AnnotationThreadChange
{
    /// <summary>A reply was added.</summary>
    Reply,
    /// <summary>A comment's text changed.</summary>
    Text,
    /// <summary>The status changed.</summary>
    Status,
    /// <summary>Its marks changed.</summary>
    Marked,
    /// <summary>A comment was deleted.</summary>
    Deleted,
}

/// <summary>A text style the selection toggles on free text.</summary>
public enum AnnotationTextFormat
{
    /// <summary>Bold.</summary>
    Bold,
    /// <summary>Italic.</summary>
    Italic,
    /// <summary>Underline.</summary>
    Underline,
}

/// <summary>A thread member a thread delete could not remove.</summary>
/// <param name="AnnotationRef">The annotation.</param>
/// <param name="Error">Why.</param>
public sealed record AnnotationThreadDeleteFailure(AnnotationRef AnnotationRef, PluginErrorInfo Error);

/// <summary>What deleting a thread did.</summary>
/// <param name="Deleted">The annotations removed.</param>
/// <param name="Failed">The ones it could not remove.</param>
public sealed record AnnotationThreadDeleteResult(IReadOnlyList<AnnotationRef> Deleted, IReadOnlyList<AnnotationThreadDeleteFailure> Failed);

/// <summary>A polygon or polyline being drawn.</summary>
/// <param name="Page">Its page.</param>
/// <param name="DraftJson">Its points so far, as the engine's JSON.</param>
public sealed record AnnotationCreationDraft(PageRef Page, string DraftJson);

/// <summary>What a placing tool asks <see cref="IAnnotationFilePicker"/> for.</summary>
/// <param name="ToolId">The tool.</param>
/// <param name="Subtype">The annotation it will create.</param>
/// <param name="Accept">The media types it takes, such as <c>image/*</c>.</param>
/// <param name="Page">Where it will go.</param>
/// <param name="Point">The page-space point clicked.</param>
public sealed record AnnotationFilePromptRequest(ToolId ToolId, AnnotationSubtype Subtype, string? Accept, PageRef Page, PdfPoint Point);

/// <summary>A file the picker answers with.</summary>
/// <param name="Bytes">Its contents.</param>
/// <param name="Name">Its name.</param>
/// <param name="MimeType">Its media type.</param>
public sealed record AnnotationPickedFile(ReadOnlyMemory<byte> Bytes, string? Name, string? MimeType);

/// <summary>One control of a style panel.</summary>
/// <param name="Key">The field it edits.</param>
/// <param name="Control">The control kind, such as <c>color</c> or <c>slider</c>.</param>
/// <param name="Label">Its label.</param>
/// <param name="LabelKey">Its label's translation key.</param>
/// <param name="PropertyJson">The rest of its description, as the engine's JSON.</param>
public sealed record AnnotationProperty(string Key, string Control, string Label, string? LabelKey, string PropertyJson);

/// <summary>What a style panel shows for a selection or a tool.</summary>
/// <param name="Properties">The controls.</param>
/// <param name="ValuesJson">Their current values, as a field bag.</param>
/// <param name="Mixed">The keys whose values differ across the selection.</param>
public sealed record AnnotationSelectionProps(IReadOnlyList<AnnotationProperty> Properties, string ValuesJson, IReadOnlyList<string> Mixed);

/// <summary>Where a stamp goes.</summary>
/// <param name="Page">Its page.</param>
/// <param name="Center">Its centre, page space.</param>
public sealed record AnnotationStampPlacement(PageRef Page, PdfPoint Center)
{
    /// <summary>Its rotation, degrees.</summary>
    public double? Rotation { get; init; }
}

/// <summary>A stamp's image or PDF.</summary>
/// <param name="Bytes">The file's bytes.</param>
public sealed record AnnotationStampSource(ReadOnlyMemory<byte> Bytes)
{
    /// <summary>Its media type, such as <c>image/png</c>.</summary>
    public string? MimeType { get; init; }

    /// <summary>The stamp's name.</summary>
    public string? Name { get; init; }

    /// <summary>The stamp's subject.</summary>
    public string? Subject { get; init; }

    /// <summary>A source from the rest of <paramref name="stream"/>.</summary>
    public static async Task<AnnotationStampSource> FromStreamAsync(Stream stream, string? mimeType = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new(await global::EmbedPDF.Bytes.ReadToEndAsync(stream, cancellationToken).ConfigureAwait(false)) { MimeType = mimeType };
    }
}

/// <summary>A file to attach as a file attachment annotation.</summary>
/// <param name="Bytes">The file's bytes.</param>
/// <param name="Name">Its file name.</param>
public sealed record AnnotationAttachmentFile(ReadOnlyMemory<byte> Bytes, string Name)
{
    /// <summary>Its media type.</summary>
    public string? MimeType { get; init; }

    /// <summary>Its description.</summary>
    public string? Description { get; init; }

    /// <summary>A file from the rest of <paramref name="stream"/>.</summary>
    public static async Task<AnnotationAttachmentFile> FromStreamAsync(Stream stream, string name, string? mimeType = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(name);
        return new(await global::EmbedPDF.Bytes.ReadToEndAsync(stream, cancellationToken).ConfigureAwait(false), name) { MimeType = mimeType };
    }
}

/// <summary>The end of a run of text, where a caret goes, as the engine's JSON.</summary>
/// <param name="AnchorJson">The anchor.</param>
public sealed record AnnotationTextEndAnchor(string AnchorJson);

/// <summary>Text quads on one page, as the engine's <c>Quad[]</c> JSON.</summary>
/// <param name="Page">The page.</param>
/// <param name="QuadsJson">The quads.</param>
public sealed record AnnotationPageQuads(PageRef Page, string QuadsJson);

/// <summary>An authoring tool.</summary>
/// <param name="Id">Its id; activate it with <see cref="InteractionCapability.ActivateToolAsync"/>.</param>
/// <param name="Subtype">The subtype it creates.</param>
/// <param name="Preset">Its preset name.</param>
/// <param name="Cursor">Its cursor.</param>
/// <param name="Enables">The plugin behaviours it turns on.</param>
/// <param name="Upright">Whether what it places stays upright on a rotated page.</param>
/// <param name="ToolJson">The whole tool, as the engine's JSON.</param>
public sealed record AnnotationTool(ToolId Id, AnnotationSubtype Subtype, string Preset, string Cursor, IReadOnlyList<string> Enables, bool Upright, string ToolJson);

/// <summary>Something the annotation plugin did.</summary>
public abstract record AnnotationEvent
{
    private protected AnnotationEvent() { }

    /// <summary>Annotations were re-read from the engine.</summary>
    /// <param name="Pages">The pages, or <see langword="null"/> for all.</param>
    public sealed record Resynced(IReadOnlyList<PageRef>? Pages) : AnnotationEvent;

    /// <summary>An annotation was created, by anyone.</summary>
    /// <param name="Annotation">The annotation.</param>
    public sealed record Created(AnnotationInfo Annotation) : AnnotationEvent;

    /// <summary>An annotation changed.</summary>
    /// <param name="Annotation">The annotation as it is now.</param>
    public sealed record Updated(AnnotationInfo Annotation) : AnnotationEvent;

    /// <summary>Annotations were deleted.</summary>
    /// <param name="Refs">The annotations.</param>
    /// <param name="Page">Their page.</param>
    public sealed record Deleted(IReadOnlyList<AnnotationRef> Refs, PageRef Page) : AnnotationEvent;

    /// <summary>A page's annotations were reordered.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="Order">Its annotations, in their new order.</param>
    public sealed record Reordered(PageRef Page, IReadOnlyList<AnnotationRef> Order) : AnnotationEvent;

    /// <summary>The selection changed.</summary>
    /// <param name="Refs">What is selected now.</param>
    /// <param name="PreviousRefs">What was.</param>
    public sealed record SelectionChanged(IReadOnlyList<AnnotationRef> Refs, IReadOnlyList<AnnotationRef> PreviousRefs) : AnnotationEvent;

    /// <summary>The polygon or polyline being drawn changed.</summary>
    /// <param name="Draft">It now, or <see langword="null"/> when there is none.</param>
    public sealed record DraftChanged(AnnotationCreationDraft? Draft) : AnnotationEvent;

    /// <summary>Text editing moved.</summary>
    /// <param name="AnnotationRef">The annotation being edited, or <see langword="null"/>.</param>
    public sealed record EditingChanged(AnnotationRef? AnnotationRef) : AnnotationEvent;

    /// <summary>The hovered annotation changed.</summary>
    /// <param name="AnnotationRef">It, or <see langword="null"/>.</param>
    public sealed record HoverChanged(AnnotationRef? AnnotationRef) : AnnotationEvent;

    /// <summary>A comment thread changed.</summary>
    /// <param name="RootRef">The thread's root.</param>
    /// <param name="Change">What changed.</param>
    public sealed record ThreadChanged(AnnotationRef RootRef, AnnotationThreadChange Change) : AnnotationEvent;

    /// <summary>A change the plugin had shown could not be written.</summary>
    /// <param name="Refs">The annotations.</param>
    /// <param name="Error">Why.</param>
    public sealed record WriteFailed(IReadOnlyList<AnnotationRef> Refs, PluginErrorInfo Error) : AnnotationEvent;
}

/// <summary>
/// Supplies the file a placing tool (a stamp, an attachment) asks for.
/// Called on the thread pool, so it may show UI through its own
/// synchronization context and may dispose the viewer.
/// </summary>
public interface IAnnotationFilePicker
{
    /// <summary>The file for <paramref name="request"/>, or <see langword="null"/> to decline. A throw is logged and declines.</summary>
    Task<AnnotationPickedFile?> PickAsync(AnnotationFilePromptRequest request);
}

internal static partial class Ffi
{
    internal static IReadOnlyList<AnnotationRef> Refs(uniffi.epdf_ffi.AnnotationRef[] refs) => [.. refs.Select(ToPublic)];

    internal static GA.AnnotationFilter? ToFfi(AnnotationFilter? filter) => filter is null ? null : new(
        filter.Pages is null ? null : [.. filter.Pages.Select(ToFfi)], filter.Subtype?.Value);

    internal static AnnotationBatchResult ToPublic(GA.AnnotationBatchResult r) =>
        new(Refs(r.Applied), [.. r.Failed.Select(f => new AnnotationBatchFailure(ToPublic(f.AnnotationRef), ToPublic(f.Error)))]);

    internal static AnnotationImportResult ToPublic(GA.AnnotationImportResult r) => new(
        [.. r.Annotations.Select(a => new AnnotationInfo(a))], [.. r.RefMap.Select(m => new AnnotationRefMapping(ToPublic(m.From), ToPublic(m.To)))], r.DroppedJson);

    internal static GA.AnnotationDuplicateOptions ToFfi(AnnotationDuplicateOptions? o) =>
        new(o?.Page is { } page ? ToFfi(page) : null, o?.Offset is { } offset ? ToFfi(offset) : null, o?.Select ?? false);

    internal static AnnotationDuplicateResult ToPublic(GA.AnnotationDuplicateResult r) =>
        new([.. r.Annotations.Select(a => new AnnotationInfo(a))], r.DroppedJson);

    internal static AnnotationRemeasureReport ToPublic(GA.AnnotationRemeasureReport r) => new(
        ToPublic(r.Page),
        Refs(r.Updated),
        [.. r.Skipped.Select(s => new AnnotationRemeasureSkip(ToPublic(s.AnnotationRef), (AnnotationRemeasureSkipReason)(int)s.Reason))],
        [.. r.Failed.Select(f => new AnnotationBatchFailure(ToPublic(f.AnnotationRef), ToPublic(f.Error)))],
        r.Error is { } error ? ToPublic(error) : null);

    /// <summary>A scale as the engine's <c>PdfMeasure</c> JSON; a value JSON can't hold is <see cref="PdfErrorCode.InvalidInput"/>.</summary>
    internal static string MeasureJson(PdfMeasurement scale)
    {
        try
        {
            return FieldBag.Sorted(scale.ToNode("scale"));
        }
        catch (FieldBagException e)
        {
            throw new PdfException("EncodingError", PdfErrorCode.InvalidInput, $"the scale doesn't encode as JSON: {e.Message}");
        }
    }

    internal static AnnotationComment ToPublic(GA.AnnotationComment c) => new(ToPublic(c.AnnotationRef), c.Author, c.Contents, c.ModifiedAt);

    internal static AnnotationCommentThread ToPublic(GA.AnnotationCommentThread t) =>
        new(ToPublic(t.Root), [.. t.Replies.Select(ToPublic)], t.Status, t.MarkedBy);

    internal static AnnotationCommentPermissions ToPublic(GA.AnnotationCommentPermissions p) =>
        new(p.CanReply, p.CanEditText, p.CanDelete, p.CanSetStatus, p.CanDeleteThread);

    internal static AnnotationThreadDeleteResult ToPublic(GA.AnnotationThreadDeleteResult r) =>
        new(Refs(r.Deleted), [.. r.Failed.Select(f => new AnnotationThreadDeleteFailure(ToPublic(f.AnnotationRef), ToPublic(f.Error)))]);

    internal static AnnotationCreationDraft ToPublic(GA.AnnotationCreationDraft d) => new(ToPublic(d.Page), d.DraftJson);

    internal static AnnotationFilePromptRequest ToPublic(GA.AnnotationFilePromptRequest r) =>
        new(new ToolId(r.ToolId), new AnnotationSubtype(r.Subtype), r.Accept, ToPublic(r.Page), ToPublic(r.Point));

    internal static GA.AnnotationPickedFile ToFfi(AnnotationPickedFile f) => new(Bytes.Array(f.Bytes), f.Name, f.MimeType);

    internal static AnnotationSelectionProps ToPublic(GA.AnnotationSelectionProps p) =>
        new([.. p.Properties.Select(x => new AnnotationProperty(x.Key, x.Control, x.Label, x.LabelKey, x.PropertyJson))], p.ValuesJson, p.Mixed);

    internal static GA.AnnotationStampPlacement ToFfi(AnnotationStampPlacement p) => new(ToFfi(p.Page), ToFfi(p.Center), p.Rotation);

    internal static GA.AnnotationStampSource ToFfi(AnnotationStampSource s) => new(Bytes.Array(s.Bytes), s.MimeType, s.Name, s.Subject);

    internal static GA.AnnotationAttachmentFile ToFfi(AnnotationAttachmentFile f) =>
        new(Bytes.Array(f.Bytes), f.Name ?? throw new ArgumentException("An attachment needs its name.", nameof(f)), f.MimeType, f.Description);

    internal static GA.AnnotationTextEndAnchor ToFfi(AnnotationTextEndAnchor a) =>
        new(Ffi.WellFormed(a.AnchorJson ?? throw new ArgumentException("An anchor needs its JSON.", nameof(a)), "anchor"));

    internal static GA.AnnotationPageQuads ToFfi(AnnotationPageQuads q) =>
        new(ToFfi(q.Page), Ffi.WellFormed(q.QuadsJson ?? throw new ArgumentException("Page quads need their JSON.", nameof(q)), "quads"));

    internal static AnnotationTool ToPublic(GA.AnnotationTool t) =>
        new(new ToolId(t.Id), new AnnotationSubtype(t.Subtype), t.Preset, t.Cursor, t.Enables, t.Upright, t.ToolJson);

    internal static GA.AnnotationTextFormat ToFfi(AnnotationTextFormat f) => (GA.AnnotationTextFormat)(int)Defined(f, "text format");

    internal static GA.AnnotationThreadStatus ToFfi(AnnotationThreadStatus s) => (GA.AnnotationThreadStatus)(int)Defined(s, "thread status");

    internal static AnnotationEvent ToPublic(GA.AnnotationEvent e) => e switch
    {
        GA.AnnotationEvent.Resynced r => new AnnotationEvent.Resynced(r.Pages is GA.AnnotationResyncedPages.Pages p ? [.. p.PagesValue.Select(ToPublic)] : null),
        GA.AnnotationEvent.Created c => new AnnotationEvent.Created(new AnnotationInfo(c.Event.Annotation)),
        GA.AnnotationEvent.Updated u => new AnnotationEvent.Updated(new AnnotationInfo(u.Event.Annotation)),
        GA.AnnotationEvent.Deleted d => new AnnotationEvent.Deleted(Refs(d.Event.Refs), ToPublic(d.Event.Page)),
        GA.AnnotationEvent.Reordered r => new AnnotationEvent.Reordered(ToPublic(r.Page), Refs(r.Order)),
        GA.AnnotationEvent.SelectionChanged s => new AnnotationEvent.SelectionChanged(Refs(s.Refs), Refs(s.PreviousRefs)),
        GA.AnnotationEvent.DraftChanged d => new AnnotationEvent.DraftChanged(d.Draft is { } draft ? ToPublic(draft) : null),
        GA.AnnotationEvent.EditingChanged x => new AnnotationEvent.EditingChanged(x.AnnotationRef is { } a ? ToPublic(a) : null),
        GA.AnnotationEvent.HoverChanged h => new AnnotationEvent.HoverChanged(h.AnnotationRef is { } a ? ToPublic(a) : null),
        GA.AnnotationEvent.ThreadChanged t => new AnnotationEvent.ThreadChanged(ToPublic(t.RootRef), (AnnotationThreadChange)(int)t.Change),
        GA.AnnotationEvent.WriteFailed w => new AnnotationEvent.WriteFailed(Refs(w.Refs), ToPublic(w.Error)),
        _ => throw Unknown(e),
    };
}
