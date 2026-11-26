using GRd = uniffi.epdf_plugin_redaction;

namespace EmbedPDF;

/// <summary>
/// A document's redactions: mark regions, review them, apply them. Get one
/// with <see cref="PdfViewer.GetRedactionAsync"/>; the viewer needs
/// <see cref="PdfViewerOptions.Interaction"/>, <see cref="PdfViewerOptions.Annotation"/>
/// and <see cref="PdfViewerOptions.Redaction"/>. After its document closes,
/// every call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
public sealed class RedactionCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GRd.RedactionHandle handle;

    internal RedactionCapability(PdfViewer viewer, GRd.RedactionHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Current settings, shared by every document using this plugin registration.</summary>
    public Task<RedactionSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => Ffi.ToPublic(await handle.Settings().ConfigureAwait(false)), cancellationToken);

    /// <summary>Merges the supplied fields; omission preserves them.</summary>
    public Task UpdateSettingsAsync(RedactionConfig patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        return viewer.Run(() => handle.UpdateSettings(patch.ToFfi()), cancellationToken);
    }

    /// <summary>Restores the values supplied at registration.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetSettings(), cancellationToken);

    /// <summary>Whether the pending mark <paramref name="annotation"/> may be removed.</summary>
    public Task<bool> CanUnmarkAsync(AnnotationRef annotation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return viewer.Run(() => handle.CanUnmark(Ffi.ToFfi(annotation)), cancellationToken);
    }

    /// <summary>Whether the label of the pending mark <paramref name="annotation"/> may change.</summary>
    public Task<bool> CanUpdateLabelAsync(AnnotationRef annotation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return viewer.Run(() => handle.CanUpdateLabel(Ffi.ToFfi(annotation)), cancellationToken);
    }

    /// <summary>The pending marks on <paramref name="page"/>, or on every page when it is <see langword="null"/>.</summary>
    public Task<IReadOnlyList<RedactionMark>> GetPendingAsync(PageRef? page = null, CancellationToken cancellationToken = default) =>
        viewer.Run(async () => Marks(await handle.Pending(page is { } p ? Ffi.ToFfi(p) : null).ConfigureAwait(false)), cancellationToken);

    /// <summary>The pending mark <paramref name="annotation"/>, or <see langword="null"/>.</summary>
    public Task<RedactionMark?> GetPendingItemAsync(AnnotationRef annotation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return viewer.Run(async () => await handle.PendingItem(Ffi.ToFfi(annotation)).ConfigureAwait(false) is { } mark ? Ffi.ToPublic(mark) : null, cancellationToken);
    }

    /// <summary>How many marks are pending on <paramref name="page"/>, or on every page.</summary>
    public Task<int> GetPendingCountAsync(PageRef? page = null, CancellationToken cancellationToken = default) =>
        viewer.Run(async () => WireInteger.ToInt(await handle.PendingCount(page is { } p ? Ffi.ToFfi(p) : null).ConfigureAwait(false), "pending count"), cancellationToken);

    /// <summary>What applying <paramref name="refs"/> (every pending mark when <see langword="null"/>) would also remove.</summary>
    public Task<RedactionCollateral> EstimateCollateralAsync(IEnumerable<AnnotationRef>? refs = null, CancellationToken cancellationToken = default)
    {
        var list = refs is null ? null : Arguments.NoNullElements(refs, nameof(refs));
        return viewer.Run(async () => Ffi.ToPublic(await handle.EstimateCollateral(list is null ? null : [.. list.Select(Ffi.ToFfi)]).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Marks the text selection, one mark per page.</summary>
    public Task<IReadOnlyList<RedactionMark>> MarkSelectionAsync(CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(async token => Marks(await handle.MarkSelection(token).ConfigureAwait(false)), cancellationToken);

    /// <summary>Marks <paramref name="bounds"/> (page space) on <paramref name="page"/>.</summary>
    public Task<RedactionMark> MarkAreaAsync(PdfRect bounds, PageRef page, CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(async token => Ffi.ToPublic(await handle.MarkArea(Ffi.ToFfi(page), Ffi.ToFfi(bounds), token).ConfigureAwait(false)), cancellationToken);

    /// <summary>Marks the whole of <paramref name="page"/>.</summary>
    public Task<RedactionMark> MarkPageAsync(PageRef page, CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(async token => Ffi.ToPublic(await handle.MarkPage(Ffi.ToFfi(page), token).ConfigureAwait(false)), cancellationToken);

    /// <summary>Marks every match of <paramref name="query"/> on <paramref name="pages"/>, or on every page.</summary>
    /// <remarks><paramref name="cancellationToken"/> is checked only before the call starts: a running search holds the kernel thread until it finishes.</remarks>
    public Task<IReadOnlyList<RedactionMark>> MarkMatchesAsync(SearchQuery query, IEnumerable<PageRef>? pages = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var list = pages?.ToArray();
        return viewer.RunCancellable(async token =>
            Marks(await handle.MarkMatches(Ffi.ToFfi(query), list is null ? null : [.. list.Select(Ffi.ToFfi)], token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Removes these pending marks.</summary>
    public Task<RedactionUnmarkResult> UnmarkAsync(IEnumerable<AnnotationRef> refs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refs);
        var list = Arguments.NoNullElements(refs, nameof(refs));
        return viewer.RunCancellable(async token => Ffi.ToPublic(await handle.Unmark([.. list.Select(Ffi.ToFfi)], token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Removes every pending mark.</summary>
    public Task<RedactionUnmarkResult> ClearPendingAsync(CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(async token => Ffi.ToPublic(await handle.ClearPending(token).ConfigureAwait(false)), cancellationToken);

    /// <summary>Changes a pending mark's label and returns the mark.</summary>
    public Task<RedactionMark> UpdateLabelAsync(AnnotationRef annotation, RedactionLabelPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentNullException.ThrowIfNull(patch);
        return viewer.RunCancellable(async token => Ffi.ToPublic(await handle.UpdateLabel(Ffi.ToFfi(annotation), Ffi.ToFfi(patch), token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Applies these pending marks. Irreversible.</summary>
    public Task<RedactionApplyResult> ApplyAsync(IEnumerable<AnnotationRef> refs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refs);
        var list = Arguments.NoNullElements(refs, nameof(refs));
        return viewer.RunCancellable(async token => Ffi.ToPublic(await handle.Apply([.. list.Select(Ffi.ToFfi)], token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Applies every mark on these pages. Irreversible.</summary>
    public Task<RedactionApplyResult> ApplyPagesAsync(IEnumerable<PageRef> pages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var list = pages.ToArray();
        return viewer.RunCancellable(async token => Ffi.ToPublic(await handle.ApplyPages([.. list.Select(Ffi.ToFfi)], token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Applies every redaction in the document. Irreversible.</summary>
    public Task<RedactionApplyResult> ApplyAllAsync(CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(async token => Ffi.ToPublic(await handle.ApplyAll(token).ConfigureAwait(false)), cancellationToken);

    /// <summary>Whether marking is allowed.</summary>
    public Task<bool> CanMarkAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanMark(), cancellationToken);

    /// <summary>Whether applying is allowed.</summary>
    public Task<bool> CanApplyAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanApply(), cancellationToken);

    /// <summary>Whether an apply is running.</summary>
    public Task<bool> IsApplyingAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.IsApplying(), cancellationToken);

    /// <summary>The last apply's result, or <see langword="null"/> before the first.</summary>
    public Task<RedactionApplyResult?> GetLastResultAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => await handle.LastResult().ConfigureAwait(false) is { } result ? Ffi.ToPublic(result) : null, cancellationToken);

    /// <summary>
    /// Applies and pending-mark changes, subscribed when this returns. Ends
    /// when the document closes (at once if it already has), when the viewer
    /// is disposed, or when <paramref name="cancellationToken"/> fires. Each
    /// call is a new subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<RedactionEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. Calls after it throw <see cref="PdfErrorCode.InstanceClosed"/>; streams end as they would otherwise.</summary>
    public void Dispose() => handle.Dispose();

    static IReadOnlyList<RedactionMark> Marks(GRd.RedactionMark[] marks) => [.. marks.Select(Ffi.ToPublic)];

    sealed class Relay() : EventRelay<GRd.RedactionEvent, RedactionEvent>(Ffi.ToPublic), GRd.RedactionEventListener
    {
        public void OnEvent(GRd.RedactionEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's redaction capability. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Redaction"/>.</summary>
    public async Task<RedactionCapability> GetRedactionAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Redaction(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
