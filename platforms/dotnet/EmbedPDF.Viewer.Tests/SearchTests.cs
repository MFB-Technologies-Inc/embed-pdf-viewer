using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Port of <c>SearchTests.swift</c> and the search case of <c>PluginSettingsTests.swift</c>.</summary>
public sealed class SearchTests : IAsyncLifetime
{
    readonly List<PdfViewer> viewers = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var viewer in viewers) await viewer.DisposeAsync();
    }

    async Task<(PdfViewer Viewer, DocumentInfo Document, SearchCapability Search)> Opened(PdfViewerOptions? options = null)
    {
        var viewer = await PdfViewer.CreateAsync(options ?? new PdfViewerOptions { Search = new() });
        viewers.Add(viewer);
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        return (viewer, document, await viewer.SearchAsync(document));
    }

    /// <summary>May search, may not copy: the scope where snippets change answers.</summary>
    async Task<SearchCapability> OpenedWithoutCopy() =>
        (await Opened(new PdfViewerOptions { Scope = ["doc.open", "doc.text.search"], Search = new() })).Search;

    static object Identity(SearchHit hit) => (hit.Page, hit.Start, hit.Count, hit.Snippet);

    [Fact]
    public async Task AQueryFindsHitsAndNavigatesThem()
    {
        var (_, _, search) = await Opened();
        var result = await search.SearchAsync(new SearchQuery("the"));
        Assert.Equal(SearchResultStatus.Complete, result.Status);
        Assert.True(result.HitCount > 1);
        Assert.Equal(SearchStatus.Complete, await search.GetStatusAsync());
        // The first hit becomes active as the hits arrive; navigating moves on.
        Assert.Equal(0, await search.GetActiveHitIndexAsync());
        var next = Assert.IsType<SearchHit>(await search.NextHitAsync());
        Assert.Equal(1, await search.GetActiveHitIndexAsync());
        Assert.Equal(Identity(next), Identity(Assert.IsType<SearchHit>(await search.GetActiveHitAsync())));
        Assert.Contains("the", next.Snippet?.Matched ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Equal(result.HitCount, await search.GetHitCountAsync());
        Assert.Equal(result.HitCount, (await search.GetHitsAsync()).Count);
        Assert.Equal("the", (await search.GetQueryAsync())?.Text);
        var pages = await search.GetPagesWithHitsAsync();
        Assert.NotEmpty(pages);
        Assert.Equal((await search.GetHitsAsync(pages[0])).Count, await search.GetHitCountAsync(pages[0]));
        var progress = await search.GetProgressAsync();
        Assert.Equal(progress.PageCount, progress.PagesSearched);
        Assert.Null(await search.GetErrorAsync());
        var previous = await search.PreviousHitAsync();
        Assert.Equal(0, await search.GetActiveHitIndexAsync());
        Assert.Equal(Identity(previous!), Identity((await search.GoToHitAsync(result.HitCount))!));
        await search.RevealActiveHitAsync();
        await search.CancelAsync();
        await search.ClearAsync();
        Assert.Equal(SearchStatus.Idle, await search.GetStatusAsync());
    }

    [Fact]
    public async Task ANegativeHitIndexIsInvalidInput()
    {
        var (_, _, search) = await Opened();
        var error = await Assert.ThrowsAsync<PdfException>(() => search.GoToHitAsync(-1));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        Assert.Equal("hit index -1 is outside 0...4294967295", error.Detail);
    }

    [Fact]
    public async Task CompletionIsObserved()
    {
        var (_, _, search) = await Opened();
        var events = search.ObserveEvents();
        var result = await search.SearchAsync(new SearchQuery("the"));
        var completed = await Fixture.First(events, e => e as SearchEvent.Completed);
        Assert.Equal(result.HitCount, completed.HitCount);
    }

    [Fact]
    public async Task FindAllCancelsWhenTheTokenIsCancelled()
    {
        var (_, _, search) = await Opened();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => search.FindAllAsync(new SearchQuery("e"), cancellationToken: new CancellationToken(true)));
        // The capability still answers after a cancelled call.
        Assert.NotEmpty(await search.FindAllAsync(new SearchQuery("Report")));
    }

    [Fact]
    public async Task SearchCancelsWhenTheTokenIsCancelled()
    {
        var (_, _, search) = await Opened();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => search.SearchAsync(new SearchQuery("the"), null, new CancellationToken(true)));
    }

    [Fact]
    public async Task RefreshCancelsWhenTheTokenIsCancelled()
    {
        var (_, _, search) = await Opened();
        _ = await search.SearchAsync(new SearchQuery("the"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => search.RefreshAsync(new CancellationToken(true)));
        await search.RefreshAsync();
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, _, search) = await Opened();
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in search.ObserveEvents(cancel.Token)) { }
        });
        Assert.Equal(0, viewer.StreamCountForTests);
        _ = await search.SearchAsync(new SearchQuery("the"));
    }

    [Fact]
    public async Task CanSearchLeftOutIsWithoutSnippets()
    {
        var search = await OpenedWithoutCopy();
        bool[] answers = [await search.CanSearchAsync(), await search.CanSearchAsync(false), await search.CanSearchAsync(true)];
        Assert.Equal([true, true, false], answers);
    }

    [Fact]
    public async Task FindAllLeftOutCarriesSnippetsWhenTheSessionMayCopy()
    {
        var (_, _, search) = await Opened();
        var query = new SearchQuery("Report");
        var omitted = await search.FindAllAsync(query);
        var always = await search.FindAllAsync(query, ContentInclusion.Always);
        var never = await search.FindAllAsync(query, ContentInclusion.Never);
        Assert.NotEmpty(omitted);
        Assert.Equal(omitted.Select(Identity), always.Select(Identity));
        Assert.All(never, hit => Assert.Null(hit.Snippet));
        Assert.All(omitted, hit => Assert.NotNull(hit.Snippet));
        Assert.All(omitted, hit => Assert.NotEmpty(hit.Segments));
    }

    [Fact]
    public async Task FindAllLeftOutDropsSnippetsWhenTheSessionMayNotCopy()
    {
        var search = await OpenedWithoutCopy();
        var query = new SearchQuery("Report");
        var omitted = await search.FindAllAsync(query);
        var never = await search.FindAllAsync(query, ContentInclusion.Never);
        Assert.NotEmpty(omitted);
        Assert.Equal(omitted.Select(Identity), never.Select(Identity));
        Assert.Equal(PdfErrorCode.PermissionDenied, (await Assert.ThrowsAsync<PdfException>(() => search.FindAllAsync(query, ContentInclusion.Always))).Code);
    }

    [Fact]
    public async Task AClosedDocumentsCapabilityThrows()
    {
        var (viewer, document, search) = await Opened();
        await viewer.CloseAsync(document);
        var error = await Assert.ThrowsAsync<PdfException>(() => search.SearchAsync(new SearchQuery("the")));
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
        Assert.Equal("PluginError", error.Name);
    }

    [Fact]
    public async Task SearchHighlightsAndNullableRevealMergeIndependently()
    {
        var (_, _, search) = await Opened(new PdfViewerOptions
        {
            Search = new SearchConfig { Reveal = new SearchRevealOptions { Behavior = SearchScrollBehavior.Instant } },
        });
        var registered = await search.GetSettingsAsync();
        await search.UpdateSettingsAsync(new SearchSettingsPatch
        {
            Reveal = new SearchRevealPatch { ClearAnchor = true },
            Highlight = new SearchHighlightPatch { Color = "red", BlendMode = SearchBlendMode.Screen },
        });
        var changed = await search.GetSettingsAsync();
        Assert.Null(changed.Reveal.Anchor);
        Assert.Equal(registered.Reveal.Behavior, changed.Reveal.Behavior);
        Assert.Equal(registered.Highlight.ActiveColor, changed.Highlight.ActiveColor);
        Assert.Equal(SearchBlendMode.Screen, changed.Highlight.BlendMode);
        await search.UpdateSettingsAsync(new SearchSettingsPatch { Highlight = new SearchHighlightPatch { ActiveColor = "blue" } });
        Assert.Equal("red", (await search.GetSettingsAsync()).Highlight.Color);
        await search.UpdateSettingsAsync(new SearchSettingsPatch
        {
            Reveal = new SearchRevealPatch { Anchor = new SearchRevealAnchor { X = new SearchRevealAxis.Keep(), Y = new SearchRevealAxis.Fraction(0.25) } },
        });
        Assert.Equal(new SearchRevealAxis.Fraction(0.25), (await search.GetSettingsAsync()).Reveal.Anchor?.Y);
        await search.ResetSettingsAsync();
        Assert.Equal(registered, await search.GetSettingsAsync());
    }
}
