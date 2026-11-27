using System.Diagnostics.CodeAnalysis;
using GA = uniffi.epdf_plugin_annotation;

namespace EmbedPDF;

/// <summary>Selects, groups, rotates and restyles the selected annotations. Get one with <see cref="AnnotationCapability.GetSelection"/>.</summary>
[SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Typed and JSON overloads of update, as in Swift.")]
public sealed class AnnotationSelectionCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GA.AnnotationSelectionHandle handle;

    internal AnnotationSelectionCapability(PdfViewer viewer, GA.AnnotationSelectionHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Selects <paramref name="refs"/>, replacing the selection unless <paramref name="adding"/>.</summary>
    public Task SelectAsync(IEnumerable<AnnotationRef> refs, bool adding = false, CancellationToken cancellationToken = default)
    {
        var list = Arguments.NoNullElements(refs, nameof(refs));
        return viewer.Run(() => handle.Select([.. list.Select(Ffi.ToFfi)], adding), cancellationToken);
    }

    /// <summary>Selects every annotation, or every one on <paramref name="page"/>.</summary>
    public Task SelectAllAsync(PageRef? page = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.SelectAll(page is { } p ? Ffi.ToFfi(p) : null), cancellationToken);

    /// <summary>Selects the annotations inside a page-space rectangle.</summary>
    public Task SelectInRectAsync(PageRef page, PdfRect rect, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.SelectInRect(Ffi.ToFfi(page), Ffi.ToFfi(rect)), cancellationToken);

    /// <summary>Clears the selection.</summary>
    public Task ClearAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.Clear(), cancellationToken);

    /// <summary>What is selected.</summary>
    public async Task<IReadOnlyList<AnnotationRef>> GetRefsAsync(CancellationToken cancellationToken = default) =>
        Ffi.Refs(await viewer.Run(() => handle.Refs(), cancellationToken).ConfigureAwait(false));

    /// <summary>What the selection's style panel shows.</summary>
    public async Task<AnnotationSelectionProps> GetPropsAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Props(), cancellationToken).ConfigureAwait(false));

    /// <summary>Changes every selected annotation with the engine's field bag; each takes the fields its kind has.</summary>
    public async Task<AnnotationBatchResult> UpdateAsync(string patchJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patchJson);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.Update(Ffi.WellFormed(patchJson, "patch"), token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Changes every selected annotation with a typed patch.</summary>
    public async Task<AnnotationBatchResult> UpdateAsync(AnnotationPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var json = patch.ToJson();
        return await UpdateAsync(json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes every selected annotation.</summary>
    public async Task<AnnotationBatchResult> DeleteAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.RunCancellable(token => handle.Delete(token), cancellationToken).ConfigureAwait(false));

    /// <summary>Sets, or with <see langword="null"/> clears, the link of every selected annotation that may carry one.</summary>
    public async Task<AnnotationBatchResult> UpdateLinkAsync(PdfLinkTarget? target, CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.RunCancellable(token => handle.UpdateLink(Ffi.ToFfiOrNull(target), token), cancellationToken).ConfigureAwait(false));

    /// <summary>Rotates the selection by <paramref name="degrees"/>.</summary>
    public Task RotateByAsync(double degrees, CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(token => handle.RotateBy(degrees, token), cancellationToken);

    /// <summary>Removes the selection's rotation.</summary>
    public Task ResetRotationAsync(CancellationToken cancellationToken = default) => viewer.RunCancellable(token => handle.ResetRotation(token), cancellationToken);

    /// <summary>Toggles a text style on the selected free text.</summary>
    public Task ToggleAsync(AnnotationTextFormat format, CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(token => handle.ToggleTextFormat(Ffi.ToFfi(format), token), cancellationToken);

    /// <summary>Groups the selection.</summary>
    public Task GroupAsync(CancellationToken cancellationToken = default) => viewer.RunCancellable(token => handle.Group(token), cancellationToken);

    /// <summary>Ungroups the selection.</summary>
    public Task UngroupAsync(CancellationToken cancellationToken = default) => viewer.RunCancellable(token => handle.Ungroup(token), cancellationToken);

    /// <summary>Whether the selection can be grouped.</summary>
    public Task<bool> CanGroupAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanGroup(), cancellationToken);

    /// <summary>Whether the selection can be ungrouped.</summary>
    public Task<bool> CanUngroupAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanUngroup(), cancellationToken);

    /// <summary>
    /// Copies the selection beside it and selects the copies, as one step to
    /// undo. They land <paramref name="offset"/> away, or 10 points right and
    /// down (left or up where that keeps them on the page) when it is left
    /// out. A selected group is copied as a group of its own. Nothing selected
    /// copies nothing.
    /// </summary>
    public async Task<AnnotationDuplicateResult> DuplicateAsync(PdfPoint? offset = null, CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.RunCancellable(token => handle.Duplicate(offset is { } o ? Ffi.ToFfiOffset(o) : null, token), cancellationToken).ConfigureAwait(false));

    /// <summary>Whether the selection can be copied: something is selected, the session may create annotations, and nothing selected is locked.</summary>
    public Task<bool> CanDuplicateAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanDuplicate(), cancellationToken);

    /// <summary>Releases the native handle.</summary>
    public void Dispose() => handle.Dispose();
}

/// <summary>The authoring tools, their defaults and style panels. Get one with <see cref="AnnotationCapability.GetTools"/>.</summary>
[SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Typed and JSON overloads, as in Swift.")]
public sealed class AnnotationToolsCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GA.AnnotationToolsHandle handle;

    internal AnnotationToolsCapability(PdfViewer viewer, GA.AnnotationToolsHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>The tools. Activate one with <see cref="InteractionCapability.ActivateToolAsync"/>.</summary>
    public async Task<IReadOnlyList<AnnotationTool>> GetToolsAsync(CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.Tools(), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>A tool, or <see langword="null"/> when there is none by that id.</summary>
    public async Task<AnnotationTool?> GetToolAsync(ToolId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id.Value, nameof(id));
        return await viewer.Run(() => handle.Tool(id.Value), cancellationToken).ConfigureAwait(false) is { } t ? Ffi.ToPublic(t) : null;
    }

    /// <summary>The field bag a new annotation from this tool starts with, as JSON.</summary>
    public Task<string> GetDefaultsAsync(ToolId toolId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolId.Value, nameof(toolId));
        return viewer.Run(() => handle.Defaults(toolId.Value), cancellationToken);
    }

    /// <summary>
    /// The tool's defaults read as a patch, typically its subtype's
    /// (<see cref="InkAnnotationPatch"/> for <see cref="ToolId.Ink"/>). A
    /// default the tool doesn't set is <see langword="null"/>; one set to
    /// nothing is <see cref="JsonNullable{T}.Null"/>. A default the patch has
    /// no member for is left unread. Throws <see cref="PdfErrorCode.InvalidInput"/>
    /// when a member of the patch holds a value of another shape.
    /// </summary>
    public async Task<TPatch> GetDefaultsAsync<TPatch>(ToolId toolId, CancellationToken cancellationToken = default)
        where TPatch : AnnotationPatch, IAnnotationPatch<TPatch>
    {
        // Performance: the engine writes the defaults as JSON and this parses
        // it again on every call; a caller that reads them often keeps the
        // typed value and reads it again when the defaults change.
        var json = await GetDefaultsAsync(toolId, cancellationToken).ConfigureAwait(false);
        return TPatch.FromJson(json);
    }

    /// <summary>
    /// Merges a typed patch over the tool's defaults, which every annotation it
    /// creates starts from. A create keeps only the defaults its subtype
    /// declares. The patch's <c>Common</c> is stored too: a reply or group id
    /// set there is the default for annotations this tool creates.
    /// </summary>
    public async Task UpdateDefaultsAsync(AnnotationPatch changes, ToolId toolId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var json = changes.ToJson();
        await UpdateDefaultsAsync(json, toolId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Merges a field bag over the tool's defaults: the way to set a default the typed patches don't cover.</summary>
    public Task UpdateDefaultsAsync(string changesJson, ToolId toolId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changesJson);
        ArgumentNullException.ThrowIfNull(toolId.Value, nameof(toolId));
        return viewer.Run(() => handle.UpdateDefaults(toolId.Value, Ffi.WellFormed(changesJson, "defaults")), cancellationToken);
    }

    /// <summary>What the tool's style panel shows.</summary>
    public async Task<AnnotationSelectionProps> GetPropertiesAsync(ToolId toolId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolId.Value, nameof(toolId));
        return Ffi.ToPublic(await viewer.Run(() => handle.Properties(toolId.Value), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Releases the native handle.</summary>
    public void Dispose() => handle.Dispose();
}

/// <summary>Creation drafts, text editing, markup from text, stamps, attachments and placement. Get one with <see cref="AnnotationCapability.GetDrafts"/>.</summary>
public sealed class AnnotationDraftsCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GA.AnnotationDraftsHandle handle;

    internal AnnotationDraftsCapability(PdfViewer viewer, GA.AnnotationDraftsHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>The polygon or polyline being drawn.</summary>
    public async Task<AnnotationCreationDraft?> GetCreationDraftAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.CreationDraft(), cancellationToken).ConfigureAwait(false) is { } d ? Ffi.ToPublic(d) : null;

    /// <summary>Commits the polygon or polyline being drawn; <see langword="null"/> when there was nothing to finish.</summary>
    public async Task<AnnotationRef?> FinishCreationDraftAsync(CancellationToken cancellationToken = default) =>
        await viewer.RunCancellable(token => handle.FinishCreationDraft(token), cancellationToken).ConfigureAwait(false) is { } r ? Ffi.ToPublic(r) : null;

    /// <summary>Commits the ink being drawn.</summary>
    public Task FinishInkDraftAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.FinishInkDraft(), cancellationToken);

    /// <summary>Whether a shape made click by click waits for its next click.</summary>
    public Task<bool> HasCreationAwaitingClickAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.HasCreationAwaitingClick(), cancellationToken);

    /// <summary>Drops every gesture in progress: drafts, ghosts, placement previews.</summary>
    public Task CancelAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.Cancel(), cancellationToken);

    /// <summary>Starts editing a free text's text.</summary>
    public Task BeginTextEditAsync(AnnotationRef annotationRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return viewer.Run(() => handle.BeginTextEdit(Ffi.ToFfi(annotationRef)), cancellationToken);
    }

    /// <summary>Starts editing the free text under a page-space point; <see langword="false"/> when there is none. The view values describe where the point came from.</summary>
    public Task<bool> BeginTextEditAtAsync(PageRef page, PdfPoint point, double? scale = null, double? rotation = null, double? zoom = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.BeginTextEditAt(Ffi.ToFfi(page), Ffi.ToFfi(point), scale, rotation, zoom), cancellationToken);

    /// <summary>Sends what was typed and stops editing.</summary>
    public Task EndTextEditAsync(CancellationToken cancellationToken = default) => viewer.RunCancellable(token => handle.EndTextEdit(token), cancellationToken);

    /// <summary>The annotation being edited.</summary>
    public async Task<AnnotationRef?> GetEditingRefAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.EditingRef(), cancellationToken).ConfigureAwait(false) is { } r ? Ffi.ToPublic(r) : null;

    /// <summary>Keystroke-level text, sent once typing pauses or at <see cref="EndTextEditAsync"/>.</summary>
    public Task DraftContentsAsync(AnnotationRef annotationRef, string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        ArgumentNullException.ThrowIfNull(text);
        return viewer.Run(() => handle.DraftContents(Ffi.ToFfi(annotationRef), text), cancellationToken);
    }

    /// <summary>The selection in the text being edited, in UTF-16 code units; both <see langword="null"/> clears it. A negative bound is <see cref="PdfErrorCode.InvalidInput"/>.</summary>
    public Task SetTextSelectionAsync(AnnotationRef annotationRef, int? start, int? end, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return viewer.Run(
            () => handle.SetTextSelection(Ffi.ToFfi(annotationRef), WireInteger.ToUInt32(start, "text selection start"), WireInteger.ToUInt32(end, "text selection end")),
            cancellationToken);
    }

    /// <summary>A highlight, underline, squiggly or strikeout over text quads (the engine's <c>Quad[]</c> JSON). The new annotation arrives on <see cref="AnnotationCapability.ObserveEvents"/>.</summary>
    public Task CreateMarkupAsync(AnnotationSubtype subtype, string quadsJson, PageRef page, ToolId? preset = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subtype.Value, nameof(subtype));
        ArgumentNullException.ThrowIfNull(quadsJson);
        return viewer.Run(() => handle.CreateMarkup(Ffi.ToFfi(page), subtype.Value, Ffi.WellFormed(quadsJson, "quads"), preset?.Value), cancellationToken);
    }

    /// <summary>An insert caret at the end of a run of text.</summary>
    public Task CreateCaretAsync(AnnotationTextEndAnchor anchor, PageRef page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        return viewer.Run(() => handle.CreateCaret(Ffi.ToFfi(page), Ffi.ToFfi(anchor)), cancellationToken);
    }

    /// <summary>A strikeout over quads paired with an insert caret at <paramref name="anchor"/>.</summary>
    public Task CreateReplaceTextAsync(string quadsJson, AnnotationTextEndAnchor anchor, PageRef page, ToolId? preset = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quadsJson);
        ArgumentNullException.ThrowIfNull(anchor);
        return viewer.Run(() => handle.CreateReplaceText(Ffi.ToFfi(page), Ffi.WellFormed(quadsJson, "quads"), Ffi.ToFfi(anchor), preset?.Value), cancellationToken);
    }

    /// <summary>Shows a markup preview over quads on several pages.</summary>
    public Task PreviewMarkupAsync(AnnotationSubtype subtype, IEnumerable<AnnotationPageQuads> pages, ToolId? preset = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subtype.Value, nameof(subtype));
        var list = Arguments.NoNullElements(pages, nameof(pages));
        return viewer.Run(() => handle.PreviewMarkup(subtype.Value, [.. list.Select(Ffi.ToFfi)], preset?.Value), cancellationToken);
    }

    /// <summary>Removes the markup preview.</summary>
    public Task ClearMarkupPreviewAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ClearMarkupPreview(), cancellationToken);

    /// <summary>
    /// Marks the current text selection (needs <see cref="PdfViewerOptions.Selection"/>)
    /// with a markup tool, one annotation per page; empty when nothing is
    /// selected. <paramref name="clear"/> drops the text selection afterwards.
    /// </summary>
    public async Task<IReadOnlyList<AnnotationRef>> CreateFromSelectionAsync(ToolId toolId, bool clear = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolId.Value, nameof(toolId));
        return Ffi.Refs(await viewer.RunCancellable(token => handle.CreateFromSelection(toolId.Value, clear, token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Arms a stamp: the next click on a page places it.</summary>
    public Task ArmStampAsync(AnnotationStampSource source, double? targetWidth = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return viewer.RunCancellable(token => handle.ArmStamp(Ffi.ToFfi(source), targetWidth, token), cancellationToken);
    }

    /// <summary>Disarms the armed stamp.</summary>
    public Task DisarmStampAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.DisarmStamp(), cancellationToken);

    /// <summary>Whether a stamp is armed.</summary>
    public Task<bool> HasArmedStampAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.HasArmedStamp(), cancellationToken);

    /// <summary>Places a stamp centred on a point, without arming.</summary>
    public async Task<AnnotationRef> PlaceStampAsync(AnnotationStampSource source, AnnotationStampPlacement placement, double? targetWidth = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(placement);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.PlaceStamp(Ffi.ToFfi(source), Ffi.ToFfi(placement), targetWidth, token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Places the armed stamp; <see langword="false"/> when none is armed.</summary>
    public Task<bool> PlaceArmedStampAsync(PageRef page, PdfPoint point, double? displayRotation = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.PlaceArmedStamp(Ffi.ToFfi(page), Ffi.ToFfi(point), displayRotation), cancellationToken);

    /// <summary>
    /// Asks the file picker for the active tool's file and places it at
    /// <paramref name="point"/>; <see langword="false"/> when the tool takes
    /// no picked file or no picker is installed. The pick answers later.
    /// </summary>
    public Task<bool> RequestStampAtAsync(PageRef page, PdfPoint point, double? displayRotation = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.RequestStampAt(Ffi.ToFfi(page), Ffi.ToFfi(point), displayRotation), cancellationToken);

    /// <summary>Shows where a tool would place its annotation.</summary>
    public Task HoverGhostAsync(ToolId toolId, PageRef page, PdfPoint point, double? displayRotation = null, double? zoom = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolId.Value, nameof(toolId));
        return viewer.Run(() => handle.HoverGhostAt(toolId.Value, Ffi.ToFfi(page), Ffi.ToFfi(point), displayRotation, zoom), cancellationToken);
    }

    /// <summary>Removes the hover ghost.</summary>
    public Task ClearGhostAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ClearGhost(), cancellationToken);

    /// <summary>
    /// A drag-to-place preview with one of this plugin's tools.
    /// <paramref name="displayRotation"/> is the page's total display rotation
    /// in degrees, so an upright tool previews on a turned page the box its
    /// commit will place; <see langword="null"/> previews as on an unturned page.
    /// </summary>
    public Task PreviewPlacementAsync(ToolId toolId, PageRef page, PdfPoint from, PdfPoint to, double? displayRotation = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolId.Value, nameof(toolId));
        return viewer.Run(() => handle.PreviewPlacement(toolId.Value, Ffi.ToFfi(page), Ffi.ToFfi(from), Ffi.ToFfi(to), displayRotation), cancellationToken);
    }

    /// <summary>Removes the placement preview.</summary>
    public Task ClearPlacementPreviewAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ClearPlacementPreview(), cancellationToken);

    /// <summary>Click-places with the active tool (a note, a stamp); <see langword="false"/> when the tool places nothing on click.</summary>
    public Task<bool> PlaceAtAsync(PageRef page, PdfPoint point, double? displayRotation = null, double? zoom = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.PlaceAt(Ffi.ToFfi(page), Ffi.ToFfi(point), displayRotation, zoom), cancellationToken);

    /// <summary>Attaches a file as a file attachment annotation at a point.</summary>
    public async Task<AnnotationRef> CreateAttachmentAsync(AnnotationAttachmentFile file, PageRef page, PdfPoint point, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.CreateAttachment(Ffi.ToFfi(page), Ffi.ToFfi(point), Ffi.ToFfi(file), token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>An attachment annotation's embedded bytes.</summary>
    public async Task<ReadOnlyMemory<byte>> ReadAttachmentAsync(AnnotationRef annotationRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return await viewer.RunCancellable(token => handle.ReadAttachment(Ffi.ToFfi(annotationRef), token), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Releases the native handle.</summary>
    public void Dispose() => handle.Dispose();
}

/// <summary>Comment threads and the links attached to annotations. Get one with <see cref="AnnotationCapability.GetThreads"/>.</summary>
[SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Typed and JSON overloads, as in Swift.")]
public sealed class AnnotationThreadsCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GA.AnnotationThreadsHandle handle;

    internal AnnotationThreadsCapability(PdfViewer viewer, GA.AnnotationThreadsHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Every comment thread.</summary>
    public async Task<IReadOnlyList<AnnotationCommentThread>> GetThreadsAsync(CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.Threads(), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>The thread any member of it belongs to.</summary>
    public async Task<AnnotationCommentThread?> GetThreadAsync(AnnotationRef annotationRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return await viewer.Run(() => handle.Thread(Ffi.ToFfi(annotationRef)), cancellationToken).ConfigureAwait(false) is { } t ? Ffi.ToPublic(t) : null;
    }

    /// <summary>Replies to an annotation; answers the reply.</summary>
    public async Task<AnnotationRef> ReplyAsync(AnnotationRef annotationRef, string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        ArgumentNullException.ThrowIfNull(text);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.Reply(Ffi.ToFfi(annotationRef), text, token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Changes a comment's text.</summary>
    public Task SetTextAsync(AnnotationRef annotationRef, string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        ArgumentNullException.ThrowIfNull(text);
        return viewer.RunCancellable(token => handle.SetText(Ffi.ToFfi(annotationRef), text, token), cancellationToken);
    }

    /// <summary>Sets a thread's review status.</summary>
    public Task SetStatusAsync(AnnotationRef annotationRef, AnnotationThreadStatus status, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return viewer.RunCancellable(token => handle.SetStatus(Ffi.ToFfi(annotationRef), Ffi.ToFfi(status), token), cancellationToken);
    }

    /// <summary>Marks or unmarks a thread for this session's user.</summary>
    public Task SetMarkedAsync(AnnotationRef annotationRef, bool marked, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return viewer.RunCancellable(token => handle.SetMarked(Ffi.ToFfi(annotationRef), marked, token), cancellationToken);
    }

    /// <summary>Deletes one comment.</summary>
    public Task DeleteAsync(AnnotationRef annotationRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return viewer.RunCancellable(token => handle.Delete(Ffi.ToFfi(annotationRef), token), cancellationToken);
    }

    /// <summary>Deletes a whole thread.</summary>
    public async Task<AnnotationThreadDeleteResult> DeleteThreadAsync(AnnotationRef annotationRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.DeleteThread(Ffi.ToFfi(annotationRef), token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>What this session may do to a comment.</summary>
    public async Task<AnnotationCommentPermissions> GetPermissionsAsync(AnnotationRef annotationRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return Ffi.ToPublic(await viewer.Run(() => handle.Permissions(Ffi.ToFfi(annotationRef)), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The link attached to an annotation (a shape that acts as a link), as the engine's <c>LinkTarget</c> JSON.</summary>
    public Task<string?> GetLinkAsync(AnnotationRef annotationRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return viewer.Run(() => handle.Link(Ffi.ToFfi(annotationRef)), cancellationToken);
    }

    /// <summary>The link attached to an annotation, typed. A target <see cref="AnnotationLinkTarget"/> can't hold (another file, a launch, a script) is <see cref="PdfErrorCode.OperationFailed"/>; read those with <see cref="GetLinkAsync"/>.</summary>
    public async Task<AnnotationLinkTarget?> GetLinkTargetAsync(AnnotationRef annotationRef, CancellationToken cancellationToken = default) =>
        await GetLinkAsync(annotationRef, cancellationToken).ConfigureAwait(false) is { } json ? AnnotationLinkTarget.FromEngineJson(json) : null;

    /// <summary>Attaches a link given as the engine's <c>LinkTarget</c> JSON.</summary>
    public Task SetLinkAsync(AnnotationRef annotationRef, string targetJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        ArgumentNullException.ThrowIfNull(targetJson);
        return viewer.RunCancellable(token => handle.SetLink(Ffi.ToFfi(annotationRef), Ffi.WellFormed(targetJson, "target"), token), cancellationToken);
    }

    /// <summary>Attaches a typed link.</summary>
    public async Task SetLinkAsync(AnnotationRef annotationRef, AnnotationLinkTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        var json = target.ToJson();
        await SetLinkAsync(annotationRef, json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes an annotation's link.</summary>
    public Task ClearLinkAsync(AnnotationRef annotationRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return viewer.RunCancellable(token => handle.ClearLink(Ffi.ToFfi(annotationRef), token), cancellationToken);
    }

    /// <summary>Releases the native handle.</summary>
    public void Dispose() => handle.Dispose();
}
