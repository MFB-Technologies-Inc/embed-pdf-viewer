using GO = uniffi.epdf_plugin_outline;

namespace EmbedPDF;

/// <summary>An outline item's identity: its PDF object number.</summary>
/// <param name="ObjectNumber">The item's object number.</param>
public readonly record struct OutlineItemRef(int ObjectNumber);

/// <summary>An outline (bookmark) item.</summary>
/// <param name="ItemRef">Its identity.</param>
/// <param name="Parent">Its parent; <see langword="null"/> at the top level.</param>
/// <param name="Title">Its title.</param>
/// <param name="Target">Where it goes, when it names a destination.</param>
/// <param name="Action">Its action tree, when it has one.</param>
/// <param name="Open">Whether the document stores it expanded.</param>
/// <param name="Color">Its color, if set.</param>
/// <param name="Bold">Whether its title is bold.</param>
/// <param name="Italic">Whether its title is italic.</param>
public sealed record OutlineItem(
    OutlineItemRef ItemRef, OutlineItemRef? Parent, string Title, PageLinkTarget? Target, PdfActionTree? Action,
    bool Open, string? Color, bool Bold, bool Italic);

/// <summary>A visible row of the outline tree, as a list view shows it.</summary>
/// <param name="Item">The item.</param>
/// <param name="Depth">Its nesting, from 0.</param>
/// <param name="HasChildren">Whether it has children.</param>
/// <param name="Expanded">Whether its children show.</param>
public sealed record OutlineRow(OutlineItem Item, int Depth, bool HasChildren, bool Expanded);

/// <summary>The whole outline, as a resync delivers it.</summary>
/// <param name="Items">Every item.</param>
/// <param name="Incomplete">Whether reading stopped early.</param>
public sealed record OutlineSnapshot(IReadOnlyList<OutlineItem> Items, bool Incomplete);

/// <summary>Where an item goes among its siblings.</summary>
public abstract record OutlinePosition
{
    private protected OutlinePosition() { }

    /// <summary>First.</summary>
    public sealed record Start : OutlinePosition;

    /// <summary>Last.</summary>
    public sealed record End : OutlinePosition;

    /// <summary>Before a sibling.</summary>
    /// <param name="Item">The sibling.</param>
    public sealed record Before(OutlineItemRef Item) : OutlinePosition;

    /// <summary>After a sibling.</summary>
    /// <param name="Item">The sibling.</param>
    public sealed record After(OutlineItemRef Item) : OutlinePosition;
}

/// <summary>A new outline item.</summary>
public sealed record OutlineItemInput
{
    /// <summary>Its title.</summary>
    public string? Title { get; init; }
    /// <summary>Its parent; <see langword="null"/> is the top level.</summary>
    public OutlineItemRef? Parent { get; init; }
    /// <summary>Where it goes among its siblings; <see langword="null"/> is last.</summary>
    public OutlinePosition? Position { get; init; }
    /// <summary>Where it goes; left out, it takes the current view unless <see cref="ClearTarget"/>.</summary>
    public PageLinkTarget? Target { get; init; }
    /// <summary>Choose no target explicitly.</summary>
    public bool ClearTarget { get; init; }
    /// <summary>Its color.</summary>
    public string? Color { get; init; }
    /// <summary>Bold title.</summary>
    public bool? Bold { get; init; }
    /// <summary>Italic title.</summary>
    public bool? Italic { get; init; }
}

/// <summary>A field <see cref="OutlineItemPatch.Clear"/> can remove.</summary>
public enum OutlinePatchKey
{
    /// <summary>The target.</summary>
    Target,
    /// <summary>The color.</summary>
    Color,
}

/// <summary>A change to an outline item; <see langword="null"/> keeps a value, <see cref="Clear"/> removes one.</summary>
public sealed record OutlineItemPatch
{
    /// <summary>New title.</summary>
    public string? Title { get; init; }
    /// <summary>New target.</summary>
    public PageLinkTarget? Target { get; init; }
    /// <summary>New color.</summary>
    public string? Color { get; init; }
    /// <summary>New bold flag.</summary>
    public bool? Bold { get; init; }
    /// <summary>New italic flag.</summary>
    public bool? Italic { get; init; }
    /// <summary>New stored open state.</summary>
    public bool? Open { get; init; }
    /// <summary>Fields to remove.</summary>
    public IReadOnlyList<OutlinePatchKey> Clear { get; init; } = [];
}

/// <summary>How <see cref="OutlineCapability.CreateAsync"/> creates.</summary>
public sealed record OutlineCreateOptions
{
    /// <summary>Select the new item.</summary>
    public bool Select { get; init; }
}

/// <summary>An event from <see cref="OutlineCapability.ObserveEvents"/>.</summary>
public abstract record OutlineEvent
{
    private protected OutlineEvent() { }

    /// <summary>Items changed.</summary>
    /// <param name="Kind">What happened, such as <c>created</c>.</param>
    /// <param name="Items">The items.</param>
    /// <param name="Origin">Who did it.</param>
    public sealed record Changed(string Kind, IReadOnlyList<OutlineItemRef> Items, DocumentEventOrigin Origin) : OutlineEvent;

    /// <summary>The outline was read again.</summary>
    /// <param name="Outline">The outline read.</param>
    public sealed record Resynced(OutlineSnapshot Outline) : OutlineEvent;
}

/// <summary>
/// A document's outline (bookmarks): read, expand and collapse, select,
/// activate and edit. Get one with <see cref="PdfViewer.GetOutlineAsync"/>; the
/// viewer needs <see cref="PdfViewerOptions.Outline"/>. After its document
/// closes, every call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
public sealed class OutlineCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GO.OutlineHandle handle;

    internal OutlineCapability(PdfViewer viewer, GO.OutlineHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Every item, depth first.</summary>
    public async Task<IReadOnlyList<OutlineItem>> ListAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.List(), cancellationToken).ConfigureAwait(false));

    /// <summary>One item; <see langword="null"/> when it does not exist.</summary>
    public async Task<OutlineItem?> GetItemAsync(OutlineItemRef item, CancellationToken cancellationToken = default)
    {
        var ffi = Ffi.ToFfi(item);
        return await viewer.Run(() => handle.Item(ffi), cancellationToken).ConfigureAwait(false) is { } found ? Ffi.ToPublic(found) : null;
    }

    /// <summary>An item's children, or the top level for <see langword="null"/>.</summary>
    public async Task<IReadOnlyList<OutlineItem>> GetChildrenAsync(OutlineItemRef? parent = null, CancellationToken cancellationToken = default)
    {
        var ffi = Ffi.ToFfiOrNull(parent);
        return Ffi.ToPublic(await viewer.Run(() => handle.Children(ffi), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The visible rows, as expansion leaves them.</summary>
    public async Task<IReadOnlyList<OutlineRow>> ListRowsAsync(CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.Rows(), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>The item for the current view, if any.</summary>
    public async Task<OutlineItemRef?> GetCurrentItemAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublicOrNull(await viewer.Run(() => handle.CurrentItem(), cancellationToken).ConfigureAwait(false));

    /// <summary>The selected items.</summary>
    public async Task<IReadOnlyList<OutlineItemRef>> GetSelectedAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.SelectedItems(), cancellationToken).ConfigureAwait(false));

    /// <summary>Replaces the selection.</summary>
    public Task SelectAsync(IEnumerable<OutlineItemRef> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        var ffi = Ffi.ToFfi(items);
        return viewer.Run(() => handle.Select(ffi), cancellationToken);
    }

    /// <summary>Where the read stands.</summary>
    public async Task<ResourceStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Status(), cancellationToken).ConfigureAwait(false));

    /// <summary>Whether reading stopped early.</summary>
    public Task<bool> IsIncompleteAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.IsIncomplete(), cancellationToken);

    /// <summary>Whether an item's children show.</summary>
    public Task<bool> IsExpandedAsync(OutlineItemRef item, CancellationToken cancellationToken = default)
    {
        var ffi = Ffi.ToFfi(item);
        return viewer.Run(() => handle.IsExpanded(ffi), cancellationToken);
    }

    /// <summary>Whether this caller may edit the outline.</summary>
    public Task<bool> CanEditAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanEdit(), cancellationToken);

    /// <summary>A target for the current view, as a new item would take it.</summary>
    public async Task<PageLinkTarget?> GetCurrentViewTargetAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublicOrNull(await viewer.Run(() => handle.CurrentViewTarget(), cancellationToken).ConfigureAwait(false));

    /// <summary>Follows an item, as a click does.</summary>
    public async Task<LinkActivation> ActivateAsync(OutlineItemRef item, CancellationToken cancellationToken = default)
    {
        var ffi = Ffi.ToFfi(item);
        return Ffi.ToPublic(await viewer.Run(() => handle.Activate(ffi), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Reads the outline unless it is loaded or loading. Nothing reads it until this or <see cref="RefreshAsync"/> asks.</summary>
    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) => viewer.RunCancellable(token => handle.EnsureLoaded(token), cancellationToken);

    /// <summary>Reads the outline again from the document.</summary>
    public Task RefreshAsync(CancellationToken cancellationToken = default) => viewer.RunCancellable(token => handle.Refresh(token), cancellationToken);

    /// <summary>Creates an item.</summary>
    public async Task<OutlineItem> CreateAsync(OutlineItemInput input, OutlineCreateOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var ffi = Ffi.ToFfi(input);
        var ffiOptions = new GO.OutlineCreateOptions(options?.Select ?? false);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.Create(ffi, ffiOptions, token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Changes an item.</summary>
    public async Task<OutlineItem> UpdateAsync(OutlineItemRef item, OutlineItemPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var (ffiItem, ffi) = (Ffi.ToFfi(item), Ffi.ToFfi(patch));
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.Update(ffiItem, ffi, token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Moves items under <paramref name="parent"/> (the top level for <see langword="null"/>) at <paramref name="position"/> (last for <see langword="null"/>).</summary>
    public Task MoveAsync(IEnumerable<OutlineItemRef> items, OutlineItemRef? parent, OutlinePosition? position = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        var ffi = Ffi.ToFfi(items);
        var ffiParent = Ffi.ToFfiOrNull(parent);
        var ffiPosition = Ffi.ToFfi(position ?? new OutlinePosition.End());
        return viewer.RunCancellable(token => handle.MoveItems(ffi, ffiParent, ffiPosition, token), cancellationToken);
    }

    /// <summary>Deletes items and their children.</summary>
    public Task DeleteAsync(IEnumerable<OutlineItemRef> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        var ffi = Ffi.ToFfi(items);
        return viewer.RunCancellable(token => handle.DeleteItems(ffi, token), cancellationToken);
    }

    /// <summary>Shows an item's children.</summary>
    public Task ExpandAsync(OutlineItemRef item, CancellationToken cancellationToken = default) => OnItem(item, handle.Expand, cancellationToken);

    /// <summary>Hides an item's children.</summary>
    public Task CollapseAsync(OutlineItemRef item, CancellationToken cancellationToken = default) => OnItem(item, handle.Collapse, cancellationToken);

    /// <summary>Flips whether an item's children show.</summary>
    public Task ToggleAsync(OutlineItemRef item, CancellationToken cancellationToken = default) => OnItem(item, handle.Toggle, cancellationToken);

    /// <summary>Expands an item's ancestors so it shows.</summary>
    public Task RevealAsync(OutlineItemRef item, CancellationToken cancellationToken = default) => OnItem(item, handle.Reveal, cancellationToken);

    /// <summary>Expands every item.</summary>
    public Task ExpandAllAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ExpandAll(), cancellationToken);

    /// <summary>Collapses every item.</summary>
    public Task CollapseAllAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CollapseAll(), cancellationToken);

    /// <summary>
    /// Confirmed changes and resyncs, subscribed when this returns. Ends when
    /// the document closes (at once if it already has), when the viewer is
    /// disposed, or when <paramref name="cancellationToken"/> fires. Each call
    /// is a new subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<OutlineEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. Calls after it throw <see cref="PdfErrorCode.InstanceClosed"/>.</summary>
    public void Dispose() => handle.Dispose();

    Task OnItem(OutlineItemRef item, Func<GO.OutlineItemRef, Task> call, CancellationToken cancellationToken)
    {
        var ffi = Ffi.ToFfi(item);
        return viewer.Run(() => call(ffi), cancellationToken);
    }

    sealed class Relay() : EventRelay<GO.OutlineEvent, OutlineEvent>(Ffi.ToPublic), GO.OutlineEventListener
    {
        public void OnEvent(GO.OutlineEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's outline capability. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Outline"/>.</summary>
    public async Task<OutlineCapability> GetOutlineAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Outline(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
