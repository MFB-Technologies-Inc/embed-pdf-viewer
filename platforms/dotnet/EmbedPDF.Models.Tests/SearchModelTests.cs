using System.Globalization;
using System.Runtime.CompilerServices;

namespace EmbedPDF.Models.Tests;

/// <summary>
/// Port of <c>SearchModelTests.swift</c>. Swift's deprecated
/// <c>activeIndex</c> has no counterpart, so
/// <c>theUInt32ActiveIndexFollowsActiveHitIndex</c> is not ported.
/// </summary>
public class SearchModelTests
{
    static async Task<(PdfViewer Viewer, DocumentInfo Document, SearchCapability Search, SearchModel Model)> OpenedAsync(
        SingleThreadContext context, string[]? scope = null, bool pageEdit = true)
    {
        var viewer = await Fixture.StartedAsync(b =>
        {
            b = b.Scope(scope).Search(new SearchConfig());
            return pageEdit ? b.PageEdit() : b;
        });
        var document = await viewer.OpenAsync(Fixture.Data());
        var search = await viewer.GetSearchAsync(document);
        return (viewer, document, search, context.Watch(new SearchModel(search)));
    }

    static string Summary(int position, int count) =>
        $"{position.ToString("N0", CultureInfo.CurrentCulture)} of {count.ToString("N0", CultureInfo.CurrentCulture)}";

    static void SameHits(IReadOnlyList<SearchHit> expected, IReadOnlyList<SearchHit> actual) =>
        Assert.True(Structural.SameHits(expected, actual), "the hit lists differ");

    [Fact]
    public Task RevealingTheActiveHitLeavesItActive() => SingleThreadContext.Run(async context =>
    {
        await using var viewer = await Fixture.StartedAsync(b => b.Search(new SearchConfig()).Render(new RenderConfig()).Stage(new StageConfig()));
        var document = await viewer.OpenAsync(Fixture.Data());
        using var stage = await viewer.GetStageAsync(document);
        await stage.SetViewportSizeAsync(new PdfSize(400, 600));
        var model = context.Watch(new SearchModel(await viewer.GetSearchAsync(document)));
        model.Text = "the";
        await model.SubmitSearchAsync();
        await model.NextHitAsync();
        Assert.Equal(1, model.ActiveHitIndex);
        var active = model.ActiveHit!;
        // The step already revealed the hit, so scroll away from it first.
        var last = (await viewer.GetPagesAsync(document))[^1];
        Assert.NotEqual(last.Page, active.Page);
        await stage.GoToPageAsync(last.Page, new StageGoToOptions { Behavior = StageScrollBehavior.Instant });
        Assert.False(await stage.IsPageVisibleAsync(active.Page));
        await model.RevealActiveHitAsync();
        // The search settings reveal with a smooth scroll.
        Assert.True(await Observed.PollAsync(() => stage.IsPageVisibleAsync(active.Page), 5));
        // Safe to repeat.
        await model.RevealActiveHitAsync();
        Assert.Null(model.Error);
        Assert.Equal(1, model.ActiveHitIndex);
        using var again = await viewer.GetSearchAsync(document);
        Assert.Equal(1, await again.GetActiveHitIndexAsync());
    });

    [Fact]
    public Task RevealingWithNoActiveHitOrNoStageIsNotAnError() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, _, model) = await OpenedAsync(context, pageEdit: false);
        await model.RevealActiveHitAsync();
        Assert.Null(model.Error);
        model.Text = "the";
        await model.SubmitSearchAsync();
        await model.RevealActiveHitAsync();
        Assert.Null(model.Error);
        Assert.Equal(0, model.ActiveHitIndex);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task RevealingOnAClosedDocumentSetsTheError() => SingleThreadContext.Run(async context =>
    {
        var (viewer, document, _, model) = await OpenedAsync(context);
        await viewer.CloseAsync(document);
        await model.RevealActiveHitAsync();
        Assert.NotNull(model.Error);
        await viewer.DisposeAsync();
    });

    /// <summary>
    /// Moves <paramref name="page"/> to the end and waits for the search
    /// plugin's own rerun of the query, which it starts 250 ms after the edit
    /// (<c>RERUN_DELAY_MS</c> in <c>crates/plugins/search/src/controller.rs</c>).
    /// </summary>
    static async Task MoveToEndAndAwaitRerunAsync(PageRef page, PdfViewer viewer, DocumentInfo document, SearchCapability search, IReadOnlyList<SearchHit> before)
    {
        using var edit = await viewer.GetPageEditAsync(document);
        await edit.MovePagesAsync([page], new PageEditPlacement.End());
        Assert.True(await RerunFinished(search, before)(), "the plugin's rerun after the move did not finish");
    }

    static Func<Task<bool>> RerunFinished(SearchCapability search, IReadOnlyList<SearchHit> before) => () => Observed.PollAsync(async () =>
        await search.GetStatusAsync() == SearchStatus.Complete && !Structural.SameHits(await search.GetHitsAsync(), before));

    [Fact]
    public Task ResubmittingAfterAnEditRereadsHitsWhoseCountDidNotChange() => SingleThreadContext.Run(async context =>
    {
        var (viewer, document, search, model) = await OpenedAsync(context);
        model.Text = "the";
        await model.SubmitSearchAsync();
        var before = model.Hits;
        await MoveToEndAndAwaitRerunAsync(before[0].Page, viewer, document, search, before);
        await model.SubmitSearchAsync();
        var kernel = await search.GetHitsAsync();
        // The rerun finds as many hits, at other positions.
        Assert.Equal(before.Count, kernel.Count);
        Assert.False(Structural.SameHits(kernel, before));
        SameHits(kernel, model.Hits);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ARefreshThatOvertakesTheSubmittedOneStillRereadsHits() => SingleThreadContext.Run(async context =>
    {
        var (viewer, document, search, model) = await OpenedAsync(context);
        model.Text = "the";
        await model.SubmitSearchAsync();
        var before = model.Hits;
        await MoveToEndAndAwaitRerunAsync(before[0].Page, viewer, document, search, before);
        // A revision lands while the submitted refresh is reading: the newer
        // refresh supersedes it and must still reread the hit list.
        var overtaken = false;
        model.Snapshots.ReadFinished = async () =>
        {
            if (overtaken) return;
            overtaken = true;
            await model.RefreshAsync();
        };
        await model.SubmitSearchAsync();
        model.Snapshots.ReadFinished = null;
        Assert.True(overtaken);
        var kernel = await search.GetHitsAsync();
        Assert.Equal(before.Count, kernel.Count);
        Assert.False(Structural.SameHits(kernel, before));
        SameHits(kernel, model.Hits);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ClosingTheDocumentEndsTheTaskAndEmptiesTheModel() => SingleThreadContext.Run(async context =>
    {
        var (viewer, document, _, model) = await OpenedAsync(context);
        var observing = model.RunAsync();
        model.Text = "the";
        await model.SubmitSearchAsync();
        Assert.True(model.HitCount > 1);
        await viewer.CloseAsync(document);
        await observing.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Empty(model.Hits);
        Assert.Equal(0, model.HitCount);
        Assert.Null(model.ActiveHitIndex);
        Assert.Equal(SearchStatus.Idle, model.Status);
        Assert.Equal(new SearchProgress(0, 0), model.Progress);
        Assert.Equal(PdfErrorCode.InstanceClosed, model.Error?.Code);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ARefreshNotifiesObserversOfEachProperty() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, search, model) = await OpenedAsync(context);
        await search.SearchAsync(new SearchQuery("the"));
        await search.NextHitAsync();
        var names = await Observed.NotifiedAsync(model, () => model.RefreshAsync());
        // Hits carries the per-page counts GetHitCount(page) reads.
        Assert.Superset(new HashSet<string> { "Status", "ActiveHitIndex", "HitCount", "Progress", "Hits" }, names);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ABackgroundRefreshKeepsAnActionsError() => SingleThreadContext.Run(async context =>
    {
        // Without doc.text.search the search itself is refused, while the
        // session reads still answer.
        var (viewer, _, _, model) = await OpenedAsync(context, ["doc.open", "doc.render"], pageEdit: false);
        model.Text = "the";
        await model.SubmitSearchAsync();
        var refused = model.Error;
        Assert.NotNull(refused);
        await model.RefreshAsync();
        Assert.Same(refused, model.Error);
        Assert.Equal(SearchStatus.Error, model.Status);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ClearingAfterARefusedSearchLeavesTheErrorStatus() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, _, model) = await OpenedAsync(context, ["doc.open", "doc.render"], pageEdit: false);
        model.Text = "the";
        await model.SubmitSearchAsync();
        Assert.Equal(SearchStatus.Error, model.Status);
        await model.ClearAsync();
        Assert.Equal(SearchStatus.Idle, model.Status);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ASuccessfulClearClearsARefusedSearchsError() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, _, model) = await OpenedAsync(context, ["doc.open", "doc.render"], pageEdit: false);
        model.Text = "the";
        await model.SubmitSearchAsync();
        Assert.NotNull(model.Error);
        await model.ClearAsync();
        Assert.Null(model.Error);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ThePluginsOwnRerunAfterAnEditReachesTheObservingModel() => SingleThreadContext.Run(async context =>
    {
        var (viewer, document, search, model) = await OpenedAsync(context);
        model.Text = "the";
        await model.SubmitSearchAsync();
        var before = model.Hits;
        var finished = RerunFinished(search, before);
        // Hold every read until the rerun has finished, so the model never
        // applies the searching state in between: the count, query and status
        // it then reads are the ones it already holds, and only the search
        // events say the hits moved.
        model.Snapshots.ReadFinished = async () => await finished();
        using var stop = new CancellationTokenSource();
        var observing = model.RunAsync(stop.Token);
        // Nobody resubmits: the plugin reruns the query 250 ms after the move.
        using (var edit = await viewer.GetPageEditAsync(document))
        {
            await edit.MovePagesAsync([before[0].Page], new PageEditPlacement.End());
        }
        Assert.True(await finished());
        var kernel = await search.GetHitsAsync();
        await Observed.UntilAsync(model, () => Structural.SameHits(model.Hits, kernel));
        model.Snapshots.ReadFinished = null;
        Assert.Equal(before.Count, kernel.Count);
        Assert.False(Structural.SameHits(kernel, before));
        stop.Cancel();
        await observing;
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task SubmittingASearchFillsHitsAndNavigationMovesTheActiveHit() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, _, model) = await OpenedAsync(context, pageEdit: false);
        model.Text = "the";
        await model.SubmitSearchAsync();
        Assert.Equal(SearchStatus.Complete, model.Status);
        Assert.True(model.Hits.Count > 1);
        Assert.Equal(0, model.ActiveHitIndex);
        await model.NextHitAsync();
        Assert.Equal(1, model.ActiveHitIndex);
        await model.PreviousHitAsync();
        Assert.Equal(0, model.ActiveHitIndex);
        await viewer.DisposeAsync();
    });

    [Fact]
    public void TheSummaryGroupsALargeCountAsTheLocaleDoes() =>
        Assert.Equal(Summary(1234, 12345), SearchModel.FormatSummary(1233, 12345, SearchStatus.Complete));

    [Fact]
    public Task ClearIsAvailableForTextHitsOrASessionThatIsNotIdle() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, _, model) = await OpenedAsync(context, pageEdit: false);
        Assert.False(model.CanClear);
        model.Text = "trace";
        Assert.True(model.CanClear);
        await model.SubmitSearchAsync();
        // The field emptied by hand leaves the hits to clear.
        model.Text = "";
        Assert.True(model.HitCount > 0);
        Assert.True(model.CanClear);
        await model.ClearAsync();
        Assert.False(model.CanClear);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ClearIsAvailableAfterARefusedSearchWithAnEmptyField() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, _, model) = await OpenedAsync(context, ["doc.open", "doc.render"], pageEdit: false);
        model.Text = "the";
        await model.SubmitSearchAsync();
        model.Text = "";
        Assert.Equal(SearchStatus.Error, model.Status);
        Assert.Equal(0, model.HitCount);
        Assert.True(model.CanClear);
        await model.ClearAsync();
        Assert.False(model.CanClear);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task TheActiveHitAndSummaryFollowNavigation() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, _, model) = await OpenedAsync(context, pageEdit: false);
        Assert.Null(model.ActiveHit);
        Assert.Null(model.Summary);
        model.Text = "trace";
        await model.SubmitSearchAsync();
        Assert.Equal(SearchStatus.Complete, model.Status);
        Assert.True(model.HitCount > 1);
        Assert.Same(model.Hits[0], model.ActiveHit);
        Assert.Equal(Summary(1, model.HitCount), model.Summary);
        await model.NextHitAsync();
        Assert.Same(model.Hits[1], model.ActiveHit);
        Assert.Equal(Summary(2, model.HitCount), model.Summary);
        await model.ClearAsync();
        Assert.Null(model.ActiveHit);
        Assert.Null(model.Summary);
        await viewer.DisposeAsync();
    });

    [Fact]
    public void TheSummaryMarksACountThatIsStillGrowing()
    {
        // A running scan has found 12 hits so far, not 12 in all.
        Assert.Equal(Summary(3, 12) + "+", SearchModel.FormatSummary(2, 12, SearchStatus.Searching));
        Assert.Equal(Summary(3, 12), SearchModel.FormatSummary(2, 12, SearchStatus.Complete));
        // A cancelled scan keeps its hits and finds no more.
        Assert.Equal(Summary(3, 12), SearchModel.FormatSummary(2, 12, SearchStatus.Cancelled));
        Assert.Null(SearchModel.FormatSummary(null, 12, SearchStatus.Complete));
        Assert.Null(SearchModel.FormatSummary(null, 0, SearchStatus.Searching));
        // The index and the count come from separate reads and can disagree
        // for one refresh.
        Assert.Null(SearchModel.FormatSummary(12, 12, SearchStatus.Complete));
    }

    [Fact]
    public Task ClearingEmptiesTheModel() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, _, model) = await OpenedAsync(context, pageEdit: false);
        model.Text = "the";
        await model.SubmitSearchAsync();
        await model.ClearAsync();
        Assert.Empty(model.Hits);
        Assert.Null(model.ActiveHitIndex);
        Assert.Equal(SearchStatus.Idle, model.Status);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ActiveHitChangesFromTheCapabilityReachTheModel() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, search, model) = await OpenedAsync(context, pageEdit: false);
        model.Text = "the";
        await model.SubmitSearchAsync();
        using var stop = new CancellationTokenSource();
        var observing = model.RunAsync(stop.Token);
        await search.GoToHitAsync(2);
        await Observed.ExpectAsync(model, () => model.ActiveHitIndex, 2);
        stop.Cancel();
        await observing;
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ANewModelHasNoSearch() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, _, model) = await OpenedAsync(context, pageEdit: false);
        Assert.Equal(SearchStatus.Idle, model.Status);
        Assert.Null(model.ActiveHitIndex);
        Assert.Equal(0, model.HitCount);
        Assert.Equal(new SearchProgress(0, 0), model.Progress);
        Assert.Empty(model.Hits);
        Assert.Null(model.Error);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ASearchRunElsewhereReachesTheObservingModel() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, search, model) = await OpenedAsync(context, pageEdit: false);
        using var stop = new CancellationTokenSource();
        var observing = model.RunAsync(stop.Token);
        await search.SearchAsync(new SearchQuery("the"));
        var hitCount = await search.GetHitCountAsync();
        Assert.True(hitCount > 1);
        await Observed.ExpectAsync(model, () => model.HitCount, hitCount);
        await Observed.ExpectAsync(model, () => model.Status, await search.GetStatusAsync());
        Assert.Equal(await search.GetProgressAsync(), model.Progress);
        Assert.Equal(await search.GetActiveHitIndexAsync(), model.ActiveHitIndex);
        Assert.Equal(hitCount, model.Hits.Count);
        var page = model.Hits[0].Page;
        Assert.Equal(await search.GetHitCountAsync(page), model.GetHitCount(page));
        await Observed.ExpectAsync(model, () => model.HitCount, 0, () => search.ClearAsync());
        Assert.Equal(0, model.GetHitCount(page));
        Assert.Empty(model.Hits);
        stop.Cancel();
        await observing;
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task ACancelledTaskReleasesTheModel() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, search, model) = await OpenedAsync(context, pageEdit: false);
        var weak = await ObserveAndCancel(context, search);
        await Observed.CollectedAsync(weak);
        await viewer.DisposeAsync();
        GC.KeepAlive(model);
    });

    [MethodImpl(MethodImplOptions.NoInlining)]
    static async Task<WeakReference> ObserveAndCancel(SingleThreadContext context, SearchCapability search)
    {
        var model = context.Watch(new SearchModel(search));
        using var stop = new CancellationTokenSource();
        var observing = model.RunAsync(stop.Token);
        await search.SearchAsync(new SearchQuery("the"));
        await Observed.ExpectAsync(model, () => model.HitCount, await search.GetHitCountAsync());
        stop.Cancel();
        await observing;
        return new WeakReference(model);
    }

    [Fact]
    public Task AnOlderReadCannotOverwriteANewerOne() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, _, model) = await OpenedAsync(context, pageEdit: false);
        var (reached, release) = Observed.HoldFirstRead(model.Snapshots);
        var stale = model.RefreshAsync();
        await reached;
        model.Text = "the";
        await model.SubmitSearchAsync();
        var hitCount = model.HitCount;
        Assert.True(hitCount > 1);
        release();
        await stale;
        Assert.Equal(hitCount, model.HitCount);
        Assert.Equal(SearchStatus.Complete, model.Status);
        Assert.Equal(0, model.ActiveHitIndex);
        await viewer.DisposeAsync();
    });
}
