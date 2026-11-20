using System.Diagnostics.CodeAnalysis;
using GV = uniffi.epdf_plugin_view_manager;

namespace EmbedPDF;

/// <summary>A pane: a tab strip of documents with one active.</summary>
/// <param name="Id">Its id.</param>
/// <param name="DocumentIds">Its documents, in tab order.</param>
/// <param name="ActiveDocumentId">The document it shows, if any.</param>
public sealed record PaneInfo(string Id, IReadOnlyList<string> DocumentIds, string? ActiveDocumentId);

/// <summary>An event from <see cref="ViewManagerCapability.ObserveEvents"/>.</summary>
public abstract record ViewManagerEvent
{
    private protected ViewManagerEvent() { }

    /// <summary>A pane was created.</summary>
    /// <param name="PaneId">The pane.</param>
    public sealed record PaneCreated(string PaneId) : ViewManagerEvent;

    /// <summary>A pane was removed.</summary>
    /// <param name="PaneId">The pane.</param>
    public sealed record PaneRemoved(string PaneId) : ViewManagerEvent;

    /// <summary>The focused pane changed.</summary>
    /// <param name="PaneId">The new focus, or <see langword="null"/> for none.</param>
    public sealed record FocusChanged(string? PaneId) : ViewManagerEvent;

    /// <summary>A document moved between panes, or into or out of one.</summary>
    /// <param name="DocumentId">The document.</param>
    /// <param name="FromPaneId">Where it was, if anywhere.</param>
    /// <param name="ToPaneId">Where it is, if anywhere.</param>
    public sealed record DocumentMoved(string DocumentId, string? FromPaneId, string? ToPaneId) : ViewManagerEvent;
}

/// <summary>
/// Panes over the open documents: each pane is a tab strip with one active
/// document, and the panes partition the documents. Get it with
/// <see cref="PdfViewer.GetViewManagerAsync"/>; the viewer needs
/// <see cref="PdfViewerOptions.ViewManager"/>. One capability for the whole
/// viewer; after the viewer is disposed every call throws
/// <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
/// <remarks>
/// No member throws for an id it doesn't know. An unknown pane is ignored.
/// <see cref="SetFocusedPaneAsync"/>, <see cref="CreatePaneAsync"/>,
/// <see cref="SplitPaneAsync"/> and <see cref="AddDocumentAsync"/> don't check
/// documents or panes against what is open: a closed document lands as a tab
/// (or an unknown pane as the focus) until the open documents next change.
/// </remarks>
[SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The two document moves mirror Swift's moveDocument(_:within:to:) and moveDocument(_:from:to:at:).")]
public sealed class ViewManagerCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GV.ViewManagerHandle handle;

    internal ViewManagerCapability(PdfViewer viewer, GV.ViewManagerHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Every pane, in display order.</summary>
    public async Task<IReadOnlyList<PaneInfo>> ListPanesAsync(CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.Panes(), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>One pane; <see langword="null"/> for an unknown id.</summary>
    public async Task<PaneInfo?> GetPaneAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return await viewer.Run(() => handle.Pane(id), cancellationToken).ConfigureAwait(false) is { } pane ? Ffi.ToPublic(pane) : null;
    }

    /// <summary>Pane ids in display order.</summary>
    public async Task<IReadOnlyList<string>> GetPaneOrderAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.PaneOrder(), cancellationToken).ConfigureAwait(false);

    /// <summary>The focused pane, if any.</summary>
    public Task<string?> GetFocusedPaneIdAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.FocusedPaneId(), cancellationToken);

    /// <summary>The pane that holds the document, if any.</summary>
    public Task<string?> GetPaneOfDocumentAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return viewer.Run(() => handle.PaneOfDocument(document.Id), cancellationToken);
    }

    /// <summary>A new pane, focused. Each document moves in from wherever it was. Returns its id.</summary>
    public Task<string> CreatePaneAsync(IEnumerable<DocumentInfo>? documents = null, CancellationToken cancellationToken = default)
    {
        return viewer.Run(() =>
        {
            string[] ids = documents is null ? [] : [.. documents.Select(d => d.Id)];
            return handle.CreatePane(ids);
        }, cancellationToken);
    }

    /// <summary>Removes a pane. Its documents go to <paramref name="movingDocumentsTo"/>, else the focused pane, else the last one.</summary>
    public Task RemovePaneAsync(string id, string? movingDocumentsTo = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(() => handle.RemovePane(id, movingDocumentsTo), cancellationToken);
    }

    /// <summary>Moves a pane; the index is clamped into the order. A negative index throws <see cref="PdfErrorCode.InvalidInput"/>.</summary>
    public Task MovePaneAsync(string id, int index, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(() =>
        {
            var wire = WireInteger.ToUInt32(index, "pane index");
            return handle.MovePane(id, wire);
        }, cancellationToken);
    }

    /// <summary>Focuses a pane, or none.</summary>
    public Task SetFocusedPaneAsync(string? id, CancellationToken cancellationToken = default) => viewer.Run(() => handle.SetFocusedPane(id), cancellationToken);

    /// <summary>A new pane showing the document, beside <paramref name="from"/> (or the focused pane), focused. Returns its id.</summary>
    public Task<string> SplitPaneAsync(DocumentInfo document, string? from = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return viewer.Run(() => handle.SplitPane(document.Id, from), cancellationToken);
    }

    /// <summary>Shows a document in a pane, or none.</summary>
    public Task SetActiveDocumentAsync(DocumentInfo? document, string paneId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paneId);
        return viewer.Run(() => handle.SetActiveDocument(paneId, document?.Id), cancellationToken);
    }

    /// <summary>Adds a document to a pane; one another pane holds moves. <see langword="null"/> appends; a negative index throws <see cref="PdfErrorCode.InvalidInput"/>.</summary>
    public Task AddDocumentAsync(DocumentInfo document, string paneId, int? index = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(paneId);
        return viewer.Run(() =>
        {
            var wire = WireInteger.ToUInt32(index, "tab index");
            return handle.AddDocument(paneId, document.Id, wire);
        }, cancellationToken);
    }

    /// <summary>Takes a document out of a pane.</summary>
    public Task RemoveDocumentAsync(DocumentInfo document, string paneId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(paneId);
        return viewer.Run(() => handle.RemoveDocument(paneId, document.Id), cancellationToken);
    }

    /// <summary>Reorders a tab inside its pane. A negative index throws <see cref="PdfErrorCode.InvalidInput"/>.</summary>
    public Task MoveDocumentAsync(DocumentInfo document, string paneId, int index, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(paneId);
        return viewer.Run(() =>
        {
            var wire = WireInteger.ToUInt32(index, "tab index");
            return handle.MoveDocumentWithin(paneId, document.Id, wire);
        }, cancellationToken);
    }

    /// <summary>Drags a tab into another pane, where it becomes active. <see langword="null"/> appends; a negative index throws <see cref="PdfErrorCode.InvalidInput"/>.</summary>
    public Task MoveDocumentAsync(DocumentInfo document, string from, string to, int? index = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        return viewer.Run(() =>
        {
            var wire = WireInteger.ToUInt32(index, "tab index");
            return handle.MoveDocumentBetween(from, to, document.Id, wire);
        }, cancellationToken);
    }

    /// <summary>
    /// Pane events, subscribed when this returns. Ends when the viewer is
    /// disposed (at once if it already has been) or when
    /// <paramref name="cancellationToken"/> fires. Each call is a new
    /// subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<ViewManagerEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. Calls after it throw <see cref="PdfErrorCode.InstanceClosed"/>.</summary>
    public void Dispose() => handle.Dispose();

    sealed class Relay() : EventRelay<GV.ViewManagerEvent, ViewManagerEvent>(Ffi.ToPublic), GV.ViewManagerEventListener
    {
        public void OnEvent(GV.ViewManagerEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The viewer's view manager. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.ViewManager"/>.</summary>
    public async Task<ViewManagerCapability> GetViewManagerAsync(CancellationToken cancellationToken = default) =>
        new(this, await CallAsync(h => h.ViewManager(), cancellationToken).ConfigureAwait(false));
}
