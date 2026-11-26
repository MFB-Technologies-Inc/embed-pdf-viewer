using System.Globalization;

namespace EmbedPDF.Models;

/// <summary>An observable model of a document's search session, which a view reads.</summary>
/// <remarks>
/// <para>
/// <see cref="Status"/>, <see cref="ActiveHitIndex"/>, <see cref="HitCount"/>,
/// <see cref="Progress"/> and <see cref="Hits"/> are snapshots of the kernel,
/// read again after each state revision and search event while
/// <see cref="RunAsync"/> runs, and after each action. A snapshot is one
/// revision behind the kernel until its refresh lands: read right after a
/// change, it still holds the value from before. The
/// <see cref="SearchCapability"/> reads answer with the kernel's current
/// value. A refresh that started earlier never overwrites one that started
/// later. A failed read, as when the document closes, empties the model and
/// sets <see cref="Error"/>; <see cref="RunAsync"/> ends with one more
/// refresh, so a closed document leaves the model empty.
/// </para>
/// <para>
/// <see cref="Error"/> is the latest failure of an action or a read. A
/// successful background refresh does not clear it; a successful
/// <see cref="SubmitSearchAsync"/> or <see cref="ClearAsync"/> does, so a
/// caller that polls it can miss a failure one of those cleared. After a
/// refused <see cref="SubmitSearchAsync"/>, <see cref="Status"/> stays
/// <see cref="SearchStatus.Error"/> through background refreshes until the
/// next submitted search or <see cref="ClearAsync"/>.
/// </para>
/// <para>
/// The search plugin reruns its query by itself after every confirmed edit.
/// Each search event rereads <see cref="Hits"/>, so a rerun that finds as
/// many hits in other places still reaches the model.
/// </para>
/// </remarks>
public sealed class SearchModel : ObservableModel
{
    static readonly SearchProgress NoProgress = new(0, 0);

    readonly SearchCapability search;
    long observation;
    string text = "";
    IReadOnlyList<SearchHit> hits = [];
    IReadOnlyDictionary<PageRef, int> pageHitCounts = new Dictionary<PageRef, int>();
    int? activeHitIndex;
    SearchStatus status = SearchStatus.Idle;
    int hitCount;
    SearchProgress progress = NoProgress;
    PdfException? error;
    SearchQuery? query;
    /// <summary>Set by a submitted search and cleared only when a hit list is applied, so a refresh that supersedes the submitted one still rereads it.</summary>
    bool hitsOutdated;
    /// <summary>Set by a refused submission, so a background refresh keeps <see cref="Status"/> at <see cref="SearchStatus.Error"/>.</summary>
    bool submissionRefused;

    /// <param name="search">The document's search capability. The model does not dispose it.</param>
    /// <param name="context">The home context; see <see cref="ObservableModel"/>.</param>
    public SearchModel(SearchCapability search, SynchronizationContext? context = null)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(search);
        this.search = search;
    }

    /// <summary>
    /// The search field's text, which <see cref="SubmitSearchAsync"/> searches
    /// for. Set from another thread, the write is posted to the home context
    /// and lands after this returns.
    /// </summary>
    public string Text
    {
        get => text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Home.Run(() =>
            {
                if (Set(ref text, value, nameof(Text))) Raise(nameof(CanClear));
            });
        }
    }

    /// <summary>The hits found so far, replaced as a whole when they change.</summary>
    public IReadOnlyList<SearchHit> Hits => hits;

    /// <summary>The active hit's position in <see cref="Hits"/>, <c>null</c> when no hit is active.</summary>
    public int? ActiveHitIndex => activeHitIndex;

    /// <summary>Where the session stands.</summary>
    public SearchStatus Status => status;

    /// <summary>Hits found so far in the whole document.</summary>
    public int HitCount => hitCount;

    /// <summary>How far the scan has come.</summary>
    public SearchProgress Progress { get => progress; private set => Set(ref progress, value); }

    /// <summary>The latest failure of an action or a read.</summary>
    public PdfException? Error { get => error; private set => Set(ref error, value); }

    internal SnapshotRefresh Snapshots { get; } = new();

    internal SearchCapability Search => search;

    /// <summary>
    /// The hit at <see cref="ActiveHitIndex"/> in <see cref="Hits"/>,
    /// <c>null</c> when no hit is active.
    /// </summary>
    /// <remarks>
    /// The index and the list come from separate reads. A refresh already
    /// reading when a new search starts can apply the old search's index with
    /// the new search's list. For that one refresh the index either points
    /// past the list, which reads <c>null</c>, or lands inside it, and this is
    /// then a hit of the new search that may not be its active one. The next
    /// refresh corrects it.
    /// </remarks>
    public SearchHit? ActiveHit =>
        activeHitIndex is { } index && index >= 0 && index < hits.Count ? hits[index] : null;

    /// <summary>
    /// The active hit's place among the hits, counted from 1, as
    /// <c>"3 of 12"</c>; <c>null</c> when no hit is active, which includes a
    /// session with no hits.
    /// </summary>
    /// <remarks>
    /// While <see cref="Status"/> is <see cref="SearchStatus.Searching"/>,
    /// <see cref="HitCount"/> is the hits found so far and still grows, so the
    /// text ends in a plus sign: <c>"3 of 12+"</c>. After a cancelled or failed
    /// scan it is the count of the hits that scan kept, which is every hit the
    /// session navigates. The text is English, with the numbers grouped as
    /// <see cref="CultureInfo.CurrentCulture"/> groups them. An app that words
    /// it another way builds its own from <see cref="ActiveHitIndex"/>,
    /// <see cref="HitCount"/> and <see cref="Status"/>.
    /// </remarks>
    public string? Summary => FormatSummary(activeHitIndex, hitCount, status);

    /// <summary>
    /// Whether a clear button has anything to clear: text in the field, hits,
    /// or a session that is not idle. A running scan with no hits yet and a
    /// refused search's <see cref="SearchStatus.Error"/> both count, with an
    /// empty field too.
    /// </summary>
    public bool CanClear => text.Length > 0 || hitCount > 0 || status != SearchStatus.Idle;

    /// <summary>Hits found so far on one page, counted from <see cref="Hits"/>.</summary>
    public int GetHitCount(PageRef page) => pageHitCounts.TryGetValue(page, out var count) ? count : 0;

    internal static string? FormatSummary(int? activeHitIndex, int hitCount, SearchStatus status)
    {
        if (activeHitIndex is not { } index || index >= hitCount) return null;
        var text = string.Format(CultureInfo.CurrentCulture, "{0:N0} of {1:N0}", index + 1, hitCount);
        return status == SearchStatus.Searching ? text + "+" : text;
    }

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
                search.Viewer, search.ObserveEvents, RefreshIfCurrent, RefreshIfCurrent,
                item =>
                {
                    if (observation != session) return Task.CompletedTask;
                    // A run started, finished or failed, the plugin's own rerun
                    // after an edit included: the hit list may have moved even
                    // when its count didn't.
                    if (ChangesHits(item)) hitsOutdated = true;
                    return RefreshAsync(cancellationToken);
                },
                cancellationToken);
            if (observation == session) await RefreshAsync(cancellationToken);
        }
        finally
        {
            if (observation == session) Snapshots.Invalidate();
        }
    }

    /// <summary>Reads the session once and applies the result unless a newer read overtakes it.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await Home.SwitchTo();
        await Snapshots.RunAsync(async () =>
        {
            var readStatus = await search.GetStatusAsync();
            // Performance: the active index here and the hit list below are
            // two of this refresh's bridge round trips, and a search that
            // starts between them pairs the old index with the new list (see
            // ActiveHit). One capability read that returns both from the same
            // kernel state closes that window.
            var readIndex = await search.GetActiveHitIndexAsync();
            var readCount = await search.GetHitCountAsync();
            var readProgress = await search.GetProgressAsync();
            var readQuery = await search.GetQueryAsync();
            // Between searches the hit list changes only with the query, the
            // count or the status, and a revision is often a scroll or a zoom:
            // keep the list then. A submitted search always rereads it.
            var unchanged = !hitsOutdated && readQuery == query && readCount == hits.Count && readStatus == status;
            var readHits = unchanged ? null : await search.GetHitsAsync();
            return (readStatus, readIndex, readCount, readProgress, readQuery, readHits);
        }, snapshot =>
        {
            SetStatus(submissionRefused ? SearchStatus.Error : snapshot.readStatus);
            SetActiveHitIndex(snapshot.readIndex);
            SetHitCount(snapshot.readCount);
            Progress = snapshot.readProgress;
            query = snapshot.readQuery;
            if (snapshot.readHits is { } next)
            {
                hitsOutdated = false;
                SetHits(next);
            }
        }, failure =>
        {
            SetStatus(SearchStatus.Idle);
            SetActiveHitIndex(null);
            SetHitCount(0);
            Progress = NoProgress;
            query = null;
            SetHits([]);
            Error = ModelErrors.Control(failure);
        }, cancellationToken);
    }

    /// <summary>
    /// Searches for <see cref="Text"/>. Success clears <see cref="Error"/>; a
    /// refusal sets it and holds <see cref="Status"/> at
    /// <see cref="SearchStatus.Error"/>.
    /// </summary>
    public async Task SubmitSearchAsync()
    {
        await Home.SwitchTo();
        try
        {
            await search.SearchAsync(new SearchQuery(text));
            Error = null;
            submissionRefused = false;
        }
        catch (Exception failure)
        {
            Snapshots.Invalidate();
            Error = ModelErrors.Control(failure);
            submissionRefused = true;
            SetStatus(SearchStatus.Error);
            return;
        }
        hitsOutdated = true;
        await RefreshAsync();
    }

    /// <summary>Makes the next hit active. A failure lands in <see cref="Error"/>.</summary>
    public Task NextHitAsync() => StepAsync(() => search.NextHitAsync());

    /// <summary>Makes the previous hit active. A failure lands in <see cref="Error"/>.</summary>
    public Task PreviousHitAsync() => StepAsync(() => search.PreviousHitAsync());

    /// <summary>
    /// Scrolls the document's stage to the active hit, placed as the search
    /// settings say: the reveal a next or previous step already makes. The
    /// first hit of a new search becomes active without one, so a view calls
    /// this to bring it into view. Repeating it is safe, and it leaves the
    /// active hit as it is. A failure lands in <see cref="Error"/>.
    /// </summary>
    public async Task RevealActiveHitAsync()
    {
        await Home.SwitchTo();
        try { await search.RevealActiveHitAsync(); }
        catch (Exception failure) { Error = ModelErrors.Control(failure); }
    }

    /// <summary>Ends the session and empties the model. Success clears <see cref="Error"/>.</summary>
    public async Task ClearAsync()
    {
        await Home.SwitchTo();
        try
        {
            await search.ClearAsync();
            Error = null;
            submissionRefused = false;
        }
        catch (Exception failure)
        {
            Error = ModelErrors.Control(failure);
            return;
        }
        await RefreshAsync();
    }

    async Task StepAsync(Func<Task<SearchHit?>> step)
    {
        await Home.SwitchTo();
        try
        {
            await step();
        }
        catch (Exception failure)
        {
            Error = ModelErrors.Control(failure);
            return;
        }
        await RefreshAsync();
    }

    void SetStatus(SearchStatus value)
    {
        if (Set(ref status, value, nameof(Status)))
        {
            Raise(nameof(Summary));
            Raise(nameof(CanClear));
        }
    }

    void SetActiveHitIndex(int? value)
    {
        if (Set(ref activeHitIndex, value, nameof(ActiveHitIndex)))
        {
            Raise(nameof(ActiveHit));
            Raise(nameof(Summary));
        }
    }

    void SetHitCount(int value)
    {
        if (Set(ref hitCount, value, nameof(HitCount)))
        {
            Raise(nameof(Summary));
            Raise(nameof(CanClear));
        }
    }

    void SetHits(IReadOnlyList<SearchHit> value)
    {
        if (Structural.SameHits(hits, value)) return;
        hits = value;
        var counts = new Dictionary<PageRef, int>();
        foreach (var hit in value) counts[hit.Page] = counts.TryGetValue(hit.Page, out var count) ? count + 1 : 1;
        pageHitCounts = counts;
        Raise(nameof(Hits));
        Raise(nameof(ActiveHit));
    }

    /// <summary>Whether the hit list can differ after this event: a run started, ended or failed. Progress and active-hit moves leave the list alone.</summary>
    static bool ChangesHits(SearchEvent item) => item is SearchEvent.Started or SearchEvent.Completed
        or SearchEvent.Cancelled or SearchEvent.Failed;
}
