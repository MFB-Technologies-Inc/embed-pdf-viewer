namespace EmbedPDF.Models;

/// <summary>
/// An observable model of what the session may do with a document's
/// annotations, which a view reads.
/// </summary>
/// <remarks>
/// <see cref="CanRead"/> and <see cref="CanCreate"/> are snapshots of the
/// kernel, read again after each state revision and annotation event while
/// <see cref="RunAsync"/> runs. A snapshot is one revision behind the
/// kernel until its refresh lands: read right after a change, it still holds
/// the value from before. The <see cref="AnnotationCapability"/> reads
/// answer with the kernel's current value. A refresh that started earlier
/// never overwrites one that started later. Both are <c>false</c> until the
/// first refresh lands and after a read fails, as when the document closes.
/// </remarks>
public sealed class AnnotationModel : ObservableModel
{
    readonly AnnotationCapability annotations;
    long observation;
    bool canRead;
    bool canCreate;
    PdfException? error;

    /// <param name="annotations">The document's annotation capability. The model does not dispose it.</param>
    /// <param name="context">The home context; see <see cref="ObservableModel"/>.</param>
    public AnnotationModel(AnnotationCapability annotations, SynchronizationContext? context = null)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        this.annotations = annotations;
    }

    /// <summary>Whether the session may read the document's annotations.</summary>
    public bool CanRead { get => canRead; private set => Set(ref canRead, value); }

    /// <summary>Whether the session may create annotations.</summary>
    public bool CanCreate { get => canCreate; private set => Set(ref canCreate, value); }

    /// <summary>The latest failed read; a successful one clears it.</summary>
    public PdfException? Error { get => error; private set => Set(ref error, value); }

    internal SnapshotRefresh Snapshots { get; } = new();

    /// <summary>
    /// Observes until the document closes or <paramref name="cancellationToken"/>
    /// is cancelled, and then returns without throwing. A later call
    /// supersedes an earlier one still running.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await Home.SwitchTo();
        var session = ++observation;
        try
        {
            Task RefreshIfCurrent() => observation == session ? RefreshAsync(cancellationToken) : Task.CompletedTask;
            await CapabilityObservation.ObserveAsync(
                annotations.Viewer, annotations.ObserveEvents,
                RefreshIfCurrent, RefreshIfCurrent, _ => RefreshIfCurrent(), cancellationToken);
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
            var read = await annotations.CanReadAsync();
            var create = await annotations.CanCreateAsync();
            return (read, create);
        }, snapshot =>
        {
            (CanRead, CanCreate) = snapshot;
            Error = null;
        }, failure =>
        {
            CanRead = false;
            CanCreate = false;
            Error = ModelErrors.Control(failure);
        }, cancellationToken);
    }
}
