using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class SelectionTests
{
    internal static async Task<(PdfViewer Viewer, DocumentInfo Document, SelectionCapability Selection, PageRef Page)> Opened(bool search = false)
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Search = search ? new() : null, Interaction = true, Selection = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        return (viewer, document, await viewer.GetSelectionAsync(document), page);
    }

    [Fact]
    public async Task SelectingAPageReadsItsText()
    {
        var (viewer, _, selection, page) = await Opened();
        await using var _v = viewer;
        await selection.SelectPageAsync(page);
        // report.pdf opens with the title "Trace-based Just-in-Time Type
        // Specialization for Dynamic Languages".
        Assert.Contains("Dynamic", await selection.ReadTextAsync());
        Assert.NotEmpty(await selection.GetRectsAsync(page));
        Assert.True(await selection.HasSelectionAsync());
        Assert.Equal([page], await selection.GetSelectedPagesAsync());
        var snapshot = await selection.GetSnapshotAsync();
        Assert.NotEmpty(snapshot.Pages);
        Assert.NotNull(snapshot.Range);
        Assert.NotEmpty(await selection.GetSegmentsAsync(page));
    }

    [Fact]
    public async Task ASearchHitSelectsItsCharacters()
    {
        var (viewer, document, selection, _) = await Opened(search: true);
        await using var _v = viewer;
        using var search = await viewer.GetSearchAsync(document);
        var hit = (await search.FindAllAsync(new SearchQuery("Report")))[0];
        await selection.SelectAsync(hit);
        // Search matches case-insensitively by default, so compare that way.
        Assert.Equal("report", (await selection.ReadTextAsync()).ToLowerInvariant());
    }

    [Fact]
    public async Task SelectAndClearAreObserved()
    {
        var (viewer, _, selection, page) = await Opened();
        await using var _v = viewer;
        var events = selection.ObserveEvents();
        await selection.SelectPageAsync(page);
        await selection.ClearAsync();
        var kinds = new List<string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var e in events.WithCancellation(timeout.Token))
        {
            kinds.Add(e switch
            {
                SelectionEvent.Changed => "changed",
                SelectionEvent.Committed => "committed",
                SelectionEvent.Cleared => "cleared",
                _ => "other",
            });
            if (kinds.Contains("cleared")) break;
        }
        Assert.Equal("changed", kinds[0]);
    }

    [Fact]
    public async Task ReadTextCancelsWhenTheTokenIsCancelled()
    {
        var (viewer, _, selection, _) = await Opened();
        await using var _v = viewer;
        await selection.SelectAllAsync();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => selection.ReadTextAsync(cancelled.Token));
        Assert.NotEmpty(await selection.ReadTextAsync());
    }

    [Fact]
    public async Task ReadTextInRangeCancelsWhenTheTokenIsCancelled()
    {
        var (viewer, _, selection, page) = await Opened();
        await using var _v = viewer;
        var range = new SelectionTextRange(new SelectionTextPosition(page, 0), new SelectionTextPosition(page, 20));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => selection.ReadTextAsync(range, cancelled.Token));
        Assert.Equal(20, (await selection.ReadTextAsync(range)).Length);
        Assert.False(await selection.HasSelectionAsync());
    }

    [Fact]
    public async Task AWordAndALineSelectUnderAPoint()
    {
        var (viewer, _, selection, page) = await Opened();
        await using var _v = viewer;
        await selection.EnsureLoadedAsync(page);
        Assert.True(await selection.IsLoadedAsync(page));
        await selection.SelectPageAsync(page);
        var first = (await selection.GetSegmentsAsync(page))[0].Rect;
        var inside = new PdfPoint(first.X + first.Width / 2, first.Y + first.Height / 2);
        await selection.ClearAsync();
        Assert.True(await selection.IsOverTextAsync(page, inside));
        Assert.True(await selection.SelectWordAtAsync(page, inside));
        Assert.NotEmpty(await selection.ReadTextAsync());
        Assert.True(await selection.SelectLineAtAsync(page, inside));
        Assert.NotNull(await selection.GetAnchorAsync());
        Assert.False(await selection.SelectWordAtAsync(page, new PdfPoint(-50, -50)));
    }

    [Fact]
    public async Task HighlightVisibilityAndGestures()
    {
        var (viewer, _, selection, page) = await Opened();
        await using var _v = viewer;
        await selection.SetHighlightVisibleAsync(false);
        Assert.False(await selection.IsHighlightVisibleAsync());
        await selection.SetHighlightVisibleAsync(true);
        Assert.True(await selection.IsHighlightVisibleAsync());
        await selection.BeginGestureAsync();
        Assert.True(await selection.IsSelectingAsync());
        await selection.EndGestureAsync();
        Assert.False(await selection.IsSelectingAsync());
        Assert.True(await selection.CanSelectAsync());
        Assert.True(await selection.CanCopyAsync());
        await selection.SelectAsync(new SelectionInput.Span(page, 0, 5));
        Assert.Equal(new SelectionTextRange(new SelectionTextPosition(page, 0), new SelectionTextPosition(page, 5)), await selection.GetRangeAsync());
    }

    [Fact]
    public async Task AClosedDocumentsCapabilityThrows()
    {
        var (viewer, document, selection, _) = await Opened();
        await using var _v = viewer;
        await viewer.CloseAsync(document);
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => selection.HasSelectionAsync())).Code);
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, _, selection, page) = await Opened();
        await using var _v = viewer;
        using var cancel = new CancellationTokenSource();
        var reading = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in selection.ObserveEvents(cancel.Token)) { }
            }
            catch (OperationCanceledException) { }
        });
        await Task.Delay(20);
        cancel.Cancel();
        await reading;
        await selection.SelectPageAsync(page);
    }

    /// <summary>The selection case of PluginSettingsTests.swift.</summary>
    [Fact]
    public async Task SelectionColorsClearAndSettingsReachBothDocuments()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Selection = true });
        var first = await viewer.OpenAsync(Fixture.Data("report"));
        var second = await viewer.OpenAsync(Fixture.Data("report"));
        using var selection = await viewer.GetSelectionAsync(first);
        using var other = await viewer.GetSelectionAsync(second);
        var registered = await selection.GetSettingsAsync();
        await selection.UpdateSettingsAsync(new SelectionSettingsPatch
        {
            DragThresholdPx = 11, Color = "red", Handles = new SelectionHandleSettingsPatch { Color = "blue", Shadow = "none" },
        });
        await selection.UpdateSettingsAsync(new SelectionSettingsPatch { ClearColor = true });
        var cleared = await other.GetSettingsAsync();
        Assert.Null(cleared.Color);
        Assert.Equal(11, cleared.DragThresholdPx);
        Assert.Equal("blue", cleared.Handles.Color);
        await selection.UpdateSettingsAsync(new SelectionSettingsPatch { Handles = new SelectionHandleSettingsPatch { ClearColor = true } });
        Assert.Equal("none", (await other.GetSettingsAsync()).Handles.Shadow);
        Assert.Null((await other.GetSettingsAsync()).Handles.Color);
        await other.ResetSettingsAsync();
        Assert.Equal(registered, await selection.GetSettingsAsync());
        await viewer.CloseAsync(first);
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => selection.GetSettingsAsync())).Code);
        Assert.Equal(registered, await other.GetSettingsAsync());
    }
}
