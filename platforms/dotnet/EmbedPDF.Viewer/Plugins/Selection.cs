using System.Diagnostics.CodeAnalysis;
using GSl = uniffi.epdf_plugin_selection;

namespace EmbedPDF;

/// <summary>Selection settings, shared by every document using the plugin.</summary>
/// <param name="DragThresholdPx">How far a press moves before it selects, in viewport points.</param>
/// <param name="Color">The highlight color, or the theme's.</param>
/// <param name="Handles">The touch handles.</param>
public sealed record SelectionSettings(double DragThresholdPx, string? Color, SelectionHandleSettings Handles);

/// <summary>The touch handles' look.</summary>
/// <param name="Color">Their color, or the accent.</param>
/// <param name="Shadow">Their CSS shadow.</param>
public sealed record SelectionHandleSettings(string? Color, string Shadow);

/// <summary>A change to <see cref="SelectionSettings"/>; a <see langword="null"/> field keeps its value.</summary>
public sealed record SelectionSettingsPatch
{
    /// <summary>The drag threshold.</summary>
    public double? DragThresholdPx { get; init; }

    /// <summary>The highlight color.</summary>
    public string? Color { get; init; }

    /// <summary>Clears the highlight color back to the theme's.</summary>
    public bool ClearColor { get; init; }

    /// <summary>The handles.</summary>
    public SelectionHandleSettingsPatch? Handles { get; init; }
}

/// <summary>A change to <see cref="SelectionHandleSettings"/>.</summary>
public sealed record SelectionHandleSettingsPatch
{
    /// <summary>The handle color.</summary>
    public string? Color { get; init; }

    /// <summary>Clears the handle color back to the accent.</summary>
    public bool ClearColor { get; init; }

    /// <summary>The handle shadow.</summary>
    public string? Shadow { get; init; }
}

/// <summary>A character on a page, by PDFium character index (the index search hits use).</summary>
/// <param name="Page">The page.</param>
/// <param name="Index">The character index.</param>
public readonly record struct SelectionTextPosition(PageRef Page, long Index);

/// <summary>A run of characters, possibly across pages.</summary>
/// <param name="Start">The first character.</param>
/// <param name="End">One past the last character.</param>
public sealed record SelectionTextRange(SelectionTextPosition Start, SelectionTextPosition End);

/// <summary>What to select.</summary>
public abstract record SelectionInput
{
    private protected SelectionInput() { }

    /// <summary>A range.</summary>
    public sealed record Range(SelectionTextRange Value) : SelectionInput;

    /// <summary><paramref name="Count"/> characters from <paramref name="Start"/> on one page.</summary>
    public sealed record Span(PageRef Page, long Start, long Count) : SelectionInput;
}

/// <summary>One run of selected glyphs on a line.</summary>
/// <param name="Quad">The run's quad in page space.</param>
/// <param name="Rect">Its bounds.</param>
/// <param name="Advance">Characters it covers.</param>
public sealed record SelectionSegment(PdfQuad Quad, PdfRect Rect, int Advance);

/// <summary>Where a selection starts or ends, for drawing its handles.</summary>
public sealed record SelectionEndpoint(PageRef Page, PdfQuad GlyphQuad, int Advance, PdfRect Rect);

/// <summary>Where to anchor a menu for the selection.</summary>
public sealed record SelectionAnchor(PageRef Page, PdfRect Bounds);

/// <summary>Which way the selection was dragged.</summary>
public enum SelectionDirection
{
    /// <summary>Toward the end of the document.</summary>
    Forward,
    /// <summary>Toward the start.</summary>
    Backward,
}

/// <summary>A page's part of the selection.</summary>
public sealed record SelectionSnapshotPage(PageRef Page, IReadOnlyList<SelectionSegment> Segments);

/// <summary>The whole selection's geometry.</summary>
public sealed record SelectionSnapshot(
    IReadOnlyList<SelectionSnapshotPage> Pages, SelectionEndpoint? Start, SelectionEndpoint? End, SelectionDirection Direction, SelectionTextRange? Range);

/// <summary>Something the selection did.</summary>
public abstract record SelectionEvent
{
    private protected SelectionEvent() { }

    /// <summary>The selection changed.</summary>
    public sealed record Changed(SelectionTextRange? Range, IReadOnlyList<PageRef> Pages) : SelectionEvent;

    /// <summary>A gesture finished a selection.</summary>
    public sealed record Committed(SelectionTextRange Range) : SelectionEvent;

    /// <summary>The selection was cleared.</summary>
    public sealed record Cleared : SelectionEvent;
}

/// <summary>
/// A document's text selection: select by range, span, word, line or page;
/// read its geometry and its text. Get one with
/// <see cref="PdfViewer.GetSelectionAsync"/>; the viewer needs
/// <see cref="PdfViewerOptions.Interaction"/> and
/// <see cref="PdfViewerOptions.Selection"/>. After its document closes, every
/// call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
/// <remarks>
/// Indices are PDFium character indices, the space search hits use, not
/// offsets into a .NET <see cref="string"/>. Points are page space.
/// </remarks>
[SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Select and read take a range or a hit, as in Swift.")]
public sealed class SelectionCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GSl.SelectionHandle handle;

    internal SelectionCapability(PdfViewer viewer, GSl.SelectionHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Current settings, shared by every document using this plugin registration.</summary>
    public async Task<SelectionSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Settings(), cancellationToken).ConfigureAwait(false));

    /// <summary>Merges the supplied fields. Clear flags remove nullable values; omission preserves them.</summary>
    public Task UpdateSettingsAsync(SelectionSettingsPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        return viewer.Run(() => handle.UpdateSettings(Ffi.ToFfi(patch)), cancellationToken);
    }

    /// <summary>Restores the values supplied at registration.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetSettings(), cancellationToken);

    /// <summary>Whether selection geometry is served (<c>doc.text.select</c>).</summary>
    public Task<bool> CanSelectAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanSelect(), cancellationToken);

    /// <summary>Whether text extraction is served (<c>doc.text.copy</c>).</summary>
    public Task<bool> CanCopyAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanCopy(), cancellationToken);

    /// <summary>Selects a range or a span.</summary>
    public Task SelectAsync(SelectionInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return viewer.Run(() => handle.Select(Ffi.ToFfi(input)), cancellationToken);
    }

    /// <summary>Selects a search hit's characters.</summary>
    public Task SelectAsync(SearchHit hit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hit);
        return SelectAsync(new SelectionInput.Span(hit.Page, (long)hit.Start, (long)hit.Count), cancellationToken);
    }

    /// <summary>Selects every page's text.</summary>
    public Task SelectAllAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.SelectAll(), cancellationToken);

    /// <summary>Selects a page's text.</summary>
    public Task SelectPageAsync(PageRef page, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.SelectPage(Ffi.ToFfi(page)), cancellationToken);

    /// <summary>Selects the word at a page point; <see langword="false"/> when no selectable text is there.</summary>
    public Task<bool> SelectWordAtAsync(PageRef page, PdfPoint point, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.SelectWordAt(Ffi.ToFfi(page), Ffi.ToFfi(point)), cancellationToken);

    /// <summary>Selects the line at a page point; <see langword="false"/> when no selectable text is there.</summary>
    public Task<bool> SelectLineAtAsync(PageRef page, PdfPoint point, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.SelectLineAt(Ffi.ToFfi(page), Ffi.ToFfi(point)), cancellationToken);

    /// <summary>Moves the selection's end to a page point.</summary>
    public Task ExtendToAsync(PageRef page, PdfPoint point, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.ExtendTo(Ffi.ToFfi(page), Ffi.ToFfi(point)), cancellationToken);

    /// <summary>Clears the selection.</summary>
    public Task ClearAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.Clear(), cancellationToken);

    /// <summary>Whether anything is selected.</summary>
    public Task<bool> HasSelectionAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.HasSelection(), cancellationToken);

    /// <summary>The selection's geometry.</summary>
    public async Task<SelectionSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Snapshot(), cancellationToken).ConfigureAwait(false));

    /// <summary>The selected range, if any.</summary>
    public async Task<SelectionTextRange?> GetRangeAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublicOrNull(await viewer.Run(() => handle.Range(), cancellationToken).ConfigureAwait(false));

    /// <summary>The pages the selection touches, in order.</summary>
    public async Task<IReadOnlyList<PageRef>> GetSelectedPagesAsync(CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.SelectedPages(), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>The selection's runs on <paramref name="page"/>.</summary>
    public async Task<IReadOnlyList<SelectionSegment>> GetSegmentsAsync(PageRef page, CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.Segments(Ffi.ToFfi(page)), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>The selection's rectangles on <paramref name="page"/>.</summary>
    public async Task<IReadOnlyList<PdfRect>> GetRectsAsync(PageRef page, CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.Rects(Ffi.ToFfi(page)), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>Where to anchor a menu for the selection.</summary>
    public async Task<SelectionAnchor?> GetAnchorAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.Anchor(), cancellationToken).ConfigureAwait(false) is { } a ? new SelectionAnchor(Ffi.ToPublic(a.Page), Ffi.ToPublic(a.Bounds)) : null;

    /// <summary>
    /// The selected text, pages joined with a newline. Throws
    /// <see cref="PdfErrorCode.PermissionDenied"/> without <c>doc.text.copy</c>;
    /// cancelling throws <see cref="OperationCanceledException"/>.
    /// </summary>
    public Task<string> ReadTextAsync(CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(token => handle.ReadText(token), cancellationToken);

    /// <summary>Text for any range, leaving the selection alone.</summary>
    public Task<string> ReadTextAsync(SelectionTextRange range, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(range);
        return viewer.RunCancellable(token => handle.ReadTextInRange(Ffi.ToFfi(range), token), cancellationToken);
    }

    /// <summary>Loads a page's text geometry.</summary>
    public Task EnsureLoadedAsync(PageRef page, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.EnsureLoaded(Ffi.ToFfi(page)), cancellationToken);

    /// <summary>Whether a page's text geometry is loaded and current.</summary>
    public Task<bool> IsLoadedAsync(PageRef page, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.IsLoaded(Ffi.ToFfi(page)), cancellationToken);

    /// <summary>Whether a page point is over selectable text.</summary>
    public Task<bool> IsOverTextAsync(PageRef page, PdfPoint point, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.IsOverText(Ffi.ToFfi(page), Ffi.ToFfi(point)), cancellationToken);

    /// <summary>Starts a selecting gesture.</summary>
    public Task BeginGestureAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.BeginGesture(), cancellationToken);

    /// <summary>Starts a selecting gesture at a page point; <see langword="false"/> off text.</summary>
    public Task<bool> BeginGestureAtAsync(PageRef page, PdfPoint point, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.BeginGestureAt(Ffi.ToFfi(page), Ffi.ToFfi(point)), cancellationToken);

    /// <summary>Ends the selecting gesture.</summary>
    public Task EndGestureAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.EndGesture(), cancellationToken);

    /// <summary>Whether a selecting gesture is in flight.</summary>
    public Task<bool> IsSelectingAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.IsSelecting(), cancellationToken);

    /// <summary>Shows or hides the highlight.</summary>
    public Task SetHighlightVisibleAsync(bool visible, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.SetHighlightVisible(visible), cancellationToken);

    /// <summary>Whether the highlight shows.</summary>
    public Task<bool> IsHighlightVisibleAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.IsHighlightVisible(), cancellationToken);

    /// <summary>
    /// Selection events, subscribed when this returns. Ends when the document
    /// closes (at once if it already has), when the viewer is disposed, or
    /// when <paramref name="cancellationToken"/> fires. Each call is a new
    /// subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<SelectionEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. It does not end the streams from <see cref="ObserveEvents"/>.</summary>
    public void Dispose() => handle.Dispose();

    sealed class Relay() : EventRelay<GSl.SelectionEvent, SelectionEvent>(Ffi.ToPublic), GSl.SelectionEventListener
    {
        public void OnEvent(GSl.SelectionEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's selection. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Selection"/>.</summary>
    public async Task<SelectionCapability> GetSelectionAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Selection(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
