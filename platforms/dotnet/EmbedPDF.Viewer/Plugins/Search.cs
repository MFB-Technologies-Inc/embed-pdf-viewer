using GS = uniffi.epdf_plugin_search;

namespace EmbedPDF;

/// <summary>
/// A document's text search: one session (<see cref="SearchAsync"/>,
/// navigated with <see cref="NextHitAsync"/>, <see cref="PreviousHitAsync"/>
/// and <see cref="GoToHitAsync"/>) and a session-free <see cref="FindAllAsync"/>.
/// Get one with <see cref="PdfViewer.GetSearchAsync"/>; the viewer needs
/// <see cref="PdfViewerOptions.Search"/>. After its document closes, every
/// call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
public sealed class SearchCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GS.SearchHandle handle;

    internal SearchCapability(PdfViewer viewer, GS.SearchHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Current settings, shared by every document using this plugin registration.</summary>
    public async Task<SearchSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Settings(), cancellationToken).ConfigureAwait(false));

    /// <summary>Merges the supplied fields. Clear flags remove nullable values; omission preserves them.</summary>
    public Task UpdateSettingsAsync(SearchSettingsPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        return viewer.Run(() =>
        {
            var ffi = Ffi.ToFfi(patch);
            return handle.UpdateSettings(ffi);
        }, cancellationToken);
    }

    /// <summary>Restores the values supplied at registration.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetSettings(), cancellationToken);

    /// <summary>
    /// Whether a search would be served now: <c>doc.text.search</c>, and with
    /// <paramref name="snippets"/> also <c>doc.text.copy</c>.
    /// </summary>
    public Task<bool> CanSearchAsync(bool snippets = false, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.CanSearch(snippets), cancellationToken);

    /// <summary>
    /// Starts a new search, superseding a running one. Returns when the scan
    /// completes, is superseded or is cancelled (cancelling the token cancels
    /// the scan and keeps the hits found so far). An empty
    /// <see cref="SearchQuery.Text"/> clears the session. <paramref name="from"/>
    /// is where the scan starts; <see langword="null"/> is the stage's current
    /// page when a stage is installed.
    /// </summary>
    public async Task<SearchResult> SearchAsync(SearchQuery query, PageRef? from = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var ffi = Ffi.ToFfi(query);
        var start = Ffi.ToFfiOrNull(from);
        var result = await Telemetry.Measured("search", () => viewer.RunCancellable(token => handle.Search(ffi, start, token), cancellationToken)).ConfigureAwait(false);
        return new((SearchResultStatus)(int)result.Status, WireInteger.ToInt(result.HitCount, "hit count"));
    }

    /// <summary>Runs the current query again from scratch.</summary>
    public Task RefreshAsync(CancellationToken cancellationToken = default) => viewer.RunCancellable(token => handle.Refresh(token), cancellationToken);

    /// <summary>Stops the scan and keeps the hits found so far.</summary>
    public Task CancelAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.Cancel(), cancellationToken);

    /// <summary>Stops the scan and drops the session.</summary>
    public Task ClearAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.Clear(), cancellationToken);

    /// <summary>Activates the next hit, wrapping; <see langword="null"/> with no hits.</summary>
    public async Task<SearchHit?> NextHitAsync(SearchRevealOptions? reveal = null, CancellationToken cancellationToken = default)
    {
        var ffi = Ffi.ToFfiOrNull(reveal);
        return Ffi.ToPublicOrNull(await viewer.Run(() => handle.NextHit(ffi), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Activates the previous hit, wrapping; <see langword="null"/> with no hits.</summary>
    public async Task<SearchHit?> PreviousHitAsync(SearchRevealOptions? reveal = null, CancellationToken cancellationToken = default)
    {
        var ffi = Ffi.ToFfiOrNull(reveal);
        return Ffi.ToPublicOrNull(await viewer.Run(() => handle.PreviousHit(ffi), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Activates a hit; the index wraps modulo the hit count. An index below zero throws <see cref="PdfErrorCode.InvalidInput"/>.</summary>
    public async Task<SearchHit?> GoToHitAsync(int index, SearchRevealOptions? reveal = null, CancellationToken cancellationToken = default)
    {
        var wire = WireInteger.ToUInt32(index, "hit index");
        var ffi = Ffi.ToFfiOrNull(reveal);
        return Ffi.ToPublicOrNull(await viewer.Run(() => handle.GoToHit(wire, ffi), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Scrolls the stage to the active hit.</summary>
    public Task RevealActiveHitAsync(SearchRevealOptions? reveal = null, CancellationToken cancellationToken = default)
    {
        return viewer.Run(() =>
        {
            var ffi = Ffi.ToFfiOrNull(reveal);
            return handle.RevealActiveHit(ffi);
        }, cancellationToken);
    }

    /// <summary>The session's query, if any.</summary>
    public async Task<SearchQuery?> GetQueryAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.Query(), cancellationToken).ConfigureAwait(false) is { } query ? Ffi.ToPublic(query) : null;

    /// <summary>Where the session stands.</summary>
    public async Task<SearchStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        (SearchStatus)(int)await viewer.Run(() => handle.Status(), cancellationToken).ConfigureAwait(false);

    /// <summary>Hits found so far, or those on <paramref name="page"/>.</summary>
    public async Task<IReadOnlyList<SearchHit>> GetHitsAsync(PageRef? page = null, CancellationToken cancellationToken = default)
    {
        var ffi = Ffi.ToFfiOrNull(page);
        return Ffi.ToPublic(await Telemetry.Measured("hits", () => viewer.Run(() => handle.Hits(ffi), cancellationToken)).ConfigureAwait(false));
    }

    /// <summary>How many hits so far, or on <paramref name="page"/>.</summary>
    public async Task<int> GetHitCountAsync(PageRef? page = null, CancellationToken cancellationToken = default)
    {
        var ffi = Ffi.ToFfiOrNull(page);
        return WireInteger.ToInt(await viewer.Run(() => handle.HitCount(ffi), cancellationToken).ConfigureAwait(false), "hit count");
    }

    /// <summary>The pages with at least one hit, in page order.</summary>
    public async Task<IReadOnlyList<PageRef>> GetPagesWithHitsAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.PagesWithHits(), cancellationToken).ConfigureAwait(false));

    /// <summary>The active hit's index; <see langword="null"/> when no hit is active.</summary>
    public async Task<int?> GetActiveHitIndexAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.ActiveHitIndex(), cancellationToken).ConfigureAwait(false) is { } index
            ? WireInteger.ToInt(index, "active hit index") : null;

    /// <summary>The active hit, if any.</summary>
    public async Task<SearchHit?> GetActiveHitAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublicOrNull(await viewer.Run(() => handle.ActiveHit(), cancellationToken).ConfigureAwait(false));

    /// <summary>How far the scan has come.</summary>
    public async Task<SearchProgress> GetProgressAsync(CancellationToken cancellationToken = default)
    {
        var progress = await viewer.Run(() => handle.Progress(), cancellationToken).ConfigureAwait(false);
        return new(progress.PagesSearched, progress.PageCount);
    }

    /// <summary>The failure that ended the last search, if it failed.</summary>
    public async Task<PluginErrorInfo?> GetErrorAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublicOrNull(await viewer.Run(() => handle.Error(), cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Every hit, in page order, touching no session state. With
    /// <see cref="ContentInclusion.WhenPermitted"/> the hits carry snippets when
    /// the session may copy text (<c>doc.text.copy</c>), and none when it may
    /// not; <see cref="ContentInclusion.Always"/> throws
    /// <see cref="PdfErrorCode.PermissionDenied"/> without <c>doc.text.copy</c>.
    /// Cancelling throws <see cref="OperationCanceledException"/>.
    /// </summary>
    public async Task<IReadOnlyList<SearchHit>> FindAllAsync(SearchQuery query, ContentInclusion snippets = ContentInclusion.WhenPermitted, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var ffi = Ffi.ToFfi(query);
        var engine = snippets.ToEngine();
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.FindAll(ffi, engine, token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Session events, subscribed when this returns. Ends when the document
    /// closes (at once if it already has), when the viewer is disposed, or
    /// when <paramref name="cancellationToken"/> fires. Each call is a new
    /// subscription; enumerate it once. A stream that is never enumerated
    /// still holds a native listener until its document closes, the viewer is disposed or
    /// <paramref name="cancellationToken"/> fires.
    /// </summary>
    public IAsyncEnumerable<SearchEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>
    /// Releases the native handle. Calls after it throw
    /// <see cref="PdfErrorCode.InstanceClosed"/>. It does not end the
    /// streams from <see cref="ObserveEvents"/>;
    /// a stream still ends when its document closes, the viewer is disposed or
    /// its token fires.
    /// </summary>
    public void Dispose() => handle.Dispose();

    sealed class Relay() : EventRelay<GS.SearchEvent, SearchEvent>(Ffi.ToPublic), GS.SearchEventListener
    {
        public void OnEvent(GS.SearchEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's search capability. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Search"/>.</summary>
    public async Task<SearchCapability> GetSearchAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Search(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
