namespace EmbedPDF.Models;

/// <summary>An observable model of a document's outline (bookmarks), which an outline panel reads.</summary>
/// <remarks>
/// <para>
/// Every property but <see cref="Performing"/> and <see cref="LastActivation"/>
/// is a snapshot of the kernel, read again after each state revision and
/// outline event while <see cref="RunAsync"/> runs, and after each action.
/// <see cref="RunAsync"/> first loads the outline. A refresh that started
/// earlier never overwrites one that started later. A failed read, as when
/// the document closes, empties the lists and sets <see cref="Error"/>.
/// </para>
/// <para>
/// Actions run one at a time: one asked for while <see cref="Performing"/>
/// is <c>true</c> is ignored and returns <c>null</c>. An action clears
/// <see cref="Error"/> when it starts and sets it when it fails. The error of
/// a failed read is cleared by the next successful load.
/// </para>
/// <para>
/// An <see cref="OutlineItem"/> holds its action as a
/// <see cref="PdfActionTree"/> handle; each read lifts new handles, and the
/// model compares items by the actions' JSON, so a revision that leaves the
/// outline as it was raises nothing.
/// </para>
/// </remarks>
public sealed class OutlineModel : ObservableModel
{
    readonly PdfViewer viewer;
    readonly OutlineCapability outline;
    long observation;
    bool errorIsReadFailure;
    IReadOnlyList<OutlineItem> items = [];
    IReadOnlyList<OutlineRow> rows = [];
    IReadOnlyList<OutlineItemRef> selectedItems = [];
    OutlineItemRef? currentItem;
    ResourceStatus status = ResourceStatus.Idle;
    bool incomplete;
    bool canEdit;
    bool performing;
    PdfException? error;
    LinkActivation? lastActivation;

    /// <param name="viewer">The viewer the document belongs to, for its state revisions. The model does not dispose it.</param>
    /// <param name="outline">The document's outline capability. The model does not dispose it.</param>
    /// <param name="context">The home context; see <see cref="ObservableModel"/>.</param>
    public OutlineModel(PdfViewer viewer, OutlineCapability outline, SynchronizationContext? context = null)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(outline);
        this.viewer = viewer;
        this.outline = outline;
    }

    /// <summary>Every item, in document order.</summary>
    public IReadOnlyList<OutlineItem> Items => items;

    /// <summary>The visible rows of the tree, as a list view shows them.</summary>
    public IReadOnlyList<OutlineRow> Rows => rows;

    /// <summary>The selected items.</summary>
    public IReadOnlyList<OutlineItemRef> SelectedItems { get => selectedItems; private set => SetList(ref selectedItems, value); }

    /// <summary>The item for the page in view.</summary>
    public OutlineItemRef? CurrentItem { get => currentItem; private set => Set(ref currentItem, value); }

    /// <summary>Where loading the outline stands.</summary>
    public ResourceStatus Status { get => status; private set => Set(ref status, value); }

    /// <summary>Whether reading the outline stopped early.</summary>
    public bool Incomplete { get => incomplete; private set => Set(ref incomplete, value); }

    /// <summary>Whether the session may edit the outline.</summary>
    public bool CanEdit { get => canEdit; private set => Set(ref canEdit, value); }

    /// <summary>Whether an action is out.</summary>
    public bool Performing { get => performing; private set => Set(ref performing, value); }

    /// <summary>The latest failure.</summary>
    public PdfException? Error { get => error; private set => Set(ref error, value); }

    /// <summary>What the latest <see cref="ActivateAsync"/> did; <c>null</c> after one that failed.</summary>
    public LinkActivation? LastActivation { get => lastActivation; private set => Set(ref lastActivation, value); }

    internal SnapshotRefresh Snapshots { get; } = new();

    /// <summary>
    /// Loads the outline, then observes until the document closes or
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
                viewer, outline.ObserveEvents,
                async () =>
                {
                    if (observation != session) return;
                    try
                    {
                        await outline.EnsureLoadedAsync();
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

    /// <summary>Reads the outline once and applies the result unless a newer read overtakes it.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await Home.SwitchTo();
        await Snapshots.RunAsync(async () =>
        {
            var readItems = await outline.ListAsync();
            var readRows = await outline.ListRowsAsync();
            var selected = await outline.GetSelectedAsync();
            var current = await outline.GetCurrentItemAsync();
            var readStatus = await outline.GetStatusAsync();
            var readIncomplete = await outline.IsIncompleteAsync();
            var editable = await outline.CanEditAsync();
            return (readItems, readRows, selected, current, readStatus, readIncomplete, editable);
        }, snapshot =>
        {
            // Most revisions are a scroll or a zoom that leaves the outline as
            // it was: write only what changed.
            if (!Structural.SameList(items, snapshot.readItems, Structural.SameItem))
            {
                items = snapshot.readItems;
                Raise(nameof(Items));
            }
            if (!Structural.SameList(rows, snapshot.readRows, Structural.SameRow))
            {
                rows = snapshot.readRows;
                Raise(nameof(Rows));
            }
            SelectedItems = snapshot.selected;
            CurrentItem = snapshot.current;
            Status = snapshot.readStatus;
            Incomplete = snapshot.readIncomplete;
            CanEdit = snapshot.editable;
        }, failure =>
        {
            if (items.Count > 0) { items = []; Raise(nameof(Items)); }
            if (rows.Count > 0) { rows = []; Raise(nameof(Rows)); }
            SelectedItems = [];
            CurrentItem = null;
            CanEdit = false;
            errorIsReadFailure = true;
            Error = ModelErrors.Control(failure);
        }, cancellationToken);
    }

    /// <summary>Reads the outline from the document again.</summary>
    public Task ReloadAsync() => PerformAsync(() => outline.RefreshAsync());

    /// <summary>Selects <paramref name="refs"/>.</summary>
    public Task SelectAsync(IEnumerable<OutlineItemRef> refs) => PerformAsync(() => outline.SelectAsync(refs));

    /// <summary>Expands an item.</summary>
    public Task ExpandAsync(OutlineItemRef item) => PerformAsync(() => outline.ExpandAsync(item));

    /// <summary>Collapses an item.</summary>
    public Task CollapseAsync(OutlineItemRef item) => PerformAsync(() => outline.CollapseAsync(item));

    /// <summary>Expands or collapses an item.</summary>
    public Task ToggleAsync(OutlineItemRef item) => PerformAsync(() => outline.ToggleAsync(item));

    /// <summary>Creates an item, selected unless <paramref name="options"/> says otherwise; <c>null</c> when it failed or was ignored.</summary>
    public Task<OutlineItem?> CreateAsync(OutlineItemInput input, OutlineCreateOptions? options = null) =>
        PerformAsync<OutlineItem?>(async () => await outline.CreateAsync(input, options ?? new OutlineCreateOptions { Select = true }));

    /// <summary>Changes an item; <c>null</c> when it failed or was ignored.</summary>
    public Task<OutlineItem?> UpdateAsync(OutlineItemRef item, OutlineItemPatch patch) =>
        PerformAsync<OutlineItem?>(async () => await outline.UpdateAsync(item, patch));

    /// <summary>Moves items under <paramref name="parent"/>, at the end unless <paramref name="position"/> says otherwise.</summary>
    public Task MoveAsync(IEnumerable<OutlineItemRef> refs, OutlineItemRef? parent, OutlinePosition? position = null) =>
        PerformAsync(() => outline.MoveAsync(refs, parent, position ?? new OutlinePosition.End()));

    /// <summary>Deletes items.</summary>
    public Task DeleteAsync(IEnumerable<OutlineItemRef> refs) => PerformAsync(() => outline.DeleteAsync(refs));

    /// <summary>
    /// Selects an item and follows it. The outcome is also
    /// <see cref="LastActivation"/>; <c>null</c> when it failed or was ignored.
    /// </summary>
    public async Task<LinkActivation?> ActivateAsync(OutlineItemRef item)
    {
        var result = await PerformAsync<LinkActivation?>(async () =>
        {
            await outline.SelectAsync([item]);
            return await outline.ActivateAsync(item);
        });
        LastActivation = result;
        return result;
    }

    Task PerformAsync(Func<Task> action) => PerformAsync<bool>(async () =>
    {
        await action();
        return true;
    });

    async Task<T?> PerformAsync<T>(Func<Task<T>> action)
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
            var value = await action();
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
