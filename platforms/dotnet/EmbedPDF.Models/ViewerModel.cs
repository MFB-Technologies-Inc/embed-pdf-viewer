namespace EmbedPDF.Models;

/// <summary>An observable model of the viewer's open documents, which a view reads.</summary>
/// <remarks>
/// <see cref="ActiveDocument"/> and <see cref="DocumentCount"/> are snapshots
/// of the kernel, read again after each state revision while
/// <see cref="RunAsync"/> runs. A snapshot is one revision behind the kernel
/// until its refresh lands: read right after an open or a close, it still
/// holds the value from before. The <see cref="PdfViewer"/> reads answer
/// with the kernel's current value. A refresh that started earlier never
/// overwrites one that started later. A failed read, as when the viewer is
/// disposed, empties the model and sets <see cref="Error"/>;
/// <see cref="RunAsync"/> ends with one more refresh, so a disposed viewer
/// leaves it empty.
/// </remarks>
public sealed class ViewerModel : ObservableModel
{
    readonly PdfViewer viewer;
    long observation;
    DocumentInfo? activeDocument;
    int documentCount;
    PdfException? error;

    /// <param name="viewer">The viewer to follow. The model does not dispose it.</param>
    /// <param name="context">The home context; see <see cref="ObservableModel"/>.</param>
    public ViewerModel(PdfViewer viewer, SynchronizationContext? context = null)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        this.viewer = viewer;
    }

    /// <summary>The active tab, which may be loading, failed or locked; <c>null</c> when no document is open.</summary>
    public DocumentInfo? ActiveDocument { get => activeDocument; private set => Set(ref activeDocument, value); }

    /// <summary>Open tabs, including loading, failed and locked ones.</summary>
    public int DocumentCount { get => documentCount; private set => Set(ref documentCount, value); }

    /// <summary>The latest failed read; a successful one clears it.</summary>
    public PdfException? Error { get => error; private set => Set(ref error, value); }

    internal SnapshotRefresh Snapshots { get; } = new();

    /// <summary>
    /// Observes until the viewer is disposed or <paramref name="cancellationToken"/>
    /// is cancelled, and then returns without throwing. A later call
    /// supersedes an earlier one still running.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await Home.SwitchTo();
        var session = ++observation;
        try
        {
            var revisions = viewer.ObserveStateRevisions(cancellationToken);
            await RefreshAsync(cancellationToken);
            await CapabilityObservation.Follow(revisions, async _ =>
            {
                if (observation == session) await RefreshAsync(cancellationToken);
            }, cancellationToken);
            if (observation == session) await RefreshAsync(cancellationToken);
        }
        finally
        {
            if (observation == session) Snapshots.Invalidate();
        }
    }

    /// <summary>Reads the kernel once and applies the result unless a newer read overtakes it.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await Home.SwitchTo();
        await Snapshots.RunAsync(async () =>
        {
            var active = await viewer.GetActiveDocumentAsync();
            var count = await viewer.GetDocumentCountAsync();
            return (active, count);
        }, snapshot =>
        {
            (ActiveDocument, DocumentCount) = snapshot;
            Error = null;
        }, failure =>
        {
            ActiveDocument = null;
            DocumentCount = 0;
            Error = ModelErrors.Control(failure);
        }, cancellationToken);
    }
}
