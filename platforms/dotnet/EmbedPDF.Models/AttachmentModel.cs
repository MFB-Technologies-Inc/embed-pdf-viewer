namespace EmbedPDF.Models;

/// <summary>An observable model of a document's attached files, which an attachments panel reads.</summary>
/// <remarks>
/// <para>
/// Every property but <see cref="Performing"/> is a snapshot of the kernel,
/// read again after each state revision and attachment event while
/// <see cref="RunAsync"/> runs, and after each action. <see cref="RunAsync"/>
/// first loads the list. A refresh that started earlier never overwrites one
/// that started later. A failed read, as when the document closes, empties
/// the lists, turns the abilities off and sets <see cref="Error"/>.
/// </para>
/// <para>
/// Actions run one at a time: one asked for while <see cref="Performing"/>
/// is <c>true</c> is ignored and returns <c>null</c>. An action clears
/// <see cref="Error"/> when it starts and sets it when it fails, a
/// cancellation of its token included. The error of a failed read is
/// cleared by the next successful load.
/// </para>
/// </remarks>
public sealed class AttachmentModel : ObservableModel
{
    static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".htm"] = "text/html",
        [".html"] = "text/html",
        [".xml"] = "application/xml",
        [".json"] = "application/json",
        [".zip"] = "application/zip",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".tif"] = "image/tiff",
        [".tiff"] = "image/tiff",
        [".svg"] = "image/svg+xml",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    };

    readonly PdfViewer viewer;
    readonly AttachmentCapability attachments;
    long observation;
    bool errorIsReadFailure;
    IReadOnlyList<AttachedFile> files = [];
    IReadOnlyList<AttachedFile> selectedFiles = [];
    ResourceStatus status = ResourceStatus.Idle;
    bool canAdd;
    bool canDownload;
    bool canRemoveSelection;
    IReadOnlySet<string> pendingIds = new HashSet<string>();
    bool performing;
    PdfException? error;

    /// <param name="viewer">The viewer the document belongs to, for its state revisions. The model does not dispose it.</param>
    /// <param name="attachments">The document's attachment capability. The model does not dispose it.</param>
    /// <param name="context">The home context; see <see cref="ObservableModel"/>.</param>
    public AttachmentModel(PdfViewer viewer, AttachmentCapability attachments, SynchronizationContext? context = null)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(attachments);
        this.viewer = viewer;
        this.attachments = attachments;
    }

    /// <summary>Every attached file.</summary>
    public IReadOnlyList<AttachedFile> Files { get => files; private set => SetList(ref files, value); }

    /// <summary>The selected files.</summary>
    public IReadOnlyList<AttachedFile> SelectedFiles { get => selectedFiles; private set => SetList(ref selectedFiles, value); }

    /// <summary>Where loading the list stands.</summary>
    public ResourceStatus Status { get => status; private set => Set(ref status, value); }

    /// <summary>Whether the session may attach files.</summary>
    public bool CanAdd { get => canAdd; private set => Set(ref canAdd, value); }

    /// <summary>Whether the session may download attached files.</summary>
    public bool CanDownload { get => canDownload; private set => Set(ref canDownload, value); }

    /// <summary>Whether a selection exists and every selected file may be removed.</summary>
    public bool CanRemoveSelection { get => canRemoveSelection; private set => Set(ref canRemoveSelection, value); }

    /// <summary>The ids of files added but not yet saved.</summary>
    public IReadOnlySet<string> PendingIds => pendingIds;

    /// <summary>Whether an action is out.</summary>
    public bool Performing { get => performing; private set => Set(ref performing, value); }

    /// <summary>The latest failure.</summary>
    public PdfException? Error { get => error; private set => Set(ref error, value); }

    internal SnapshotRefresh Snapshots { get; } = new();

    /// <summary>
    /// Loads the list, then observes until the document closes or
    /// <paramref name="cancellationToken"/> is cancelled, and returns without throwing.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await Home.SwitchTo();
        var session = ++observation;
        try
        {
            Task RefreshIfCurrent() => observation == session ? RefreshAsync(cancellationToken) : Task.CompletedTask;
            await CapabilityObservation.ObserveAsync(
                viewer, attachments.ObserveEvents,
                async () =>
                {
                    if (observation != session) return;
                    try
                    {
                        await attachments.EnsureLoadedAsync();
                        if (observation != session || cancellationToken.IsCancellationRequested) return;
                        if (errorIsReadFailure)
                        {
                            Error = null;
                            errorIsReadFailure = false;
                        }
                    }
                    catch (Exception failure)
                    {
                        if (observation != session || cancellationToken.IsCancellationRequested) return;
                        errorIsReadFailure = true;
                        Error = ModelErrors.Control(failure);
                    }
                    await RefreshAsync(cancellationToken);
                },
                RefreshIfCurrent, _ => RefreshIfCurrent(), cancellationToken);
            if (observation == session) await RefreshAsync(cancellationToken);
        }
        finally
        {
            if (observation == session) Snapshots.Invalidate();
        }
    }

    /// <summary>Reads the list once and applies the result unless a newer read overtakes it.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await Home.SwitchTo();
        await Snapshots.RunAsync(async () =>
        {
            var readFiles = await attachments.ListAsync();
            var selected = await attachments.GetSelectedAsync();
            var readStatus = await attachments.GetStatusAsync();
            var add = await attachments.CanAddAsync();
            var download = await attachments.CanDownloadAsync();
            var remove = selected.Count > 0;
            foreach (var file in selected)
            {
                if (!await attachments.CanRemoveAsync(file)) remove = false;
            }
            var pending = new HashSet<string>();
            foreach (var file in readFiles)
            {
                if (await attachments.IsPendingAsync(file)) pending.Add(file.Id);
            }
            return (readFiles, selected, readStatus, add, download, remove, pending);
        }, snapshot =>
        {
            // Most revisions are a scroll or a zoom: write only what changed.
            Files = snapshot.readFiles;
            SelectedFiles = snapshot.selected;
            Status = snapshot.readStatus;
            CanAdd = snapshot.add;
            CanDownload = snapshot.download;
            CanRemoveSelection = snapshot.remove;
            SetPending(snapshot.pending);
        }, failure =>
        {
            Files = [];
            SelectedFiles = [];
            SetPending(new HashSet<string>());
            CanAdd = false;
            CanDownload = false;
            CanRemoveSelection = false;
            errorIsReadFailure = true;
            Error = ModelErrors.Control(failure);
        }, cancellationToken);
    }

    /// <summary>Reads the list from the document again.</summary>
    public Task ReloadAsync(CancellationToken cancellationToken = default) =>
        PerformAsync(() => attachments.RefreshAsync(), cancellationToken);

    /// <summary>Selects <paramref name="selection"/>.</summary>
    public Task SelectAsync(IEnumerable<AttachedFile> selection, CancellationToken cancellationToken = default) =>
        PerformAsync(() => attachments.SelectAsync(selection), cancellationToken);

    /// <summary>Attaches files.</summary>
    public Task AddAsync(IEnumerable<AttachmentFileSource> sources, CancellationToken cancellationToken = default) =>
        PerformAsync(() => attachments.AddAsync(sources), cancellationToken);

    /// <summary>Removes a file.</summary>
    public Task RemoveAsync(AttachedFile file, CancellationToken cancellationToken = default) =>
        PerformAsync(() => attachments.RemoveAsync(file), cancellationToken);

    /// <summary>Shows where a file lives, as a page attachment's page.</summary>
    public Task RevealAsync(AttachedFile file, CancellationToken cancellationToken = default) =>
        PerformAsync(() => attachments.RevealAsync(file), cancellationToken);

    /// <summary>A file's bytes; <c>null</c> when the download failed or was ignored.</summary>
    public Task<AttachmentContent?> DownloadAsync(AttachedFile file, CancellationToken cancellationToken = default) =>
        PerformAsync<AttachmentContent?>(async () => await attachments.DownloadAsync(file), cancellationToken);

    /// <summary>
    /// Reads files from disk on the thread pool and attaches them, named by
    /// their file names. The media type comes from the extension for common
    /// types, and is left out otherwise. A file that cannot be read fails the
    /// whole import, which attaches nothing.
    /// </summary>
    public Task ImportFilesAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var list = paths.ToList();
        return PerformAsync(async () =>
        {
            var sources = await Task.Run(async () =>
            {
                var read = new List<AttachmentFileSource>();
                foreach (var path in list)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                    read.Add(new AttachmentFileSource(bytes)
                    {
                        Name = Path.GetFileName(path),
                        MimeType = MimeTypes.GetValueOrDefault(Path.GetExtension(path)),
                    });
                }
                return read;
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await attachments.AddAsync(sources);
        }, cancellationToken);
    }

    /// <summary>
    /// Records a failure of the app's own file work, as a save panel's, in
    /// <see cref="Error"/>. A cancellation is the user's choice and is ignored.
    /// </summary>
    public void FileOperationFailed(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        if (failure is OperationCanceledException) return;
        Home.Run(() =>
        {
            errorIsReadFailure = false;
            Error = ModelErrors.Control(failure);
        });
    }

    void SetPending(HashSet<string> value)
    {
        if (value.SetEquals(pendingIds)) return;
        pendingIds = value;
        Raise(nameof(PendingIds));
    }

    Task PerformAsync(Func<Task> action, CancellationToken cancellationToken) => PerformAsync<bool>(async () =>
    {
        await action();
        return true;
    }, cancellationToken);

    async Task<T?> PerformAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await Home.SwitchTo();
        if (performing) return default;
        Performing = true;
        errorIsReadFailure = false;
        Error = null;
        // Keeps the action in flight until the home context's next turn even
        // when the engine answers at once (see ToolModel.SubmitAsync).
        await Task.Yield();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = await action();
            cancellationToken.ThrowIfCancellationRequested();
            await RefreshAsync();
            return value;
        }
        catch (Exception failure)
        {
            await RefreshAsync();
            errorIsReadFailure = false;
            Error = ModelErrors.Control(failure);
            return default;
        }
        finally
        {
            Performing = false;
        }
    }
}
