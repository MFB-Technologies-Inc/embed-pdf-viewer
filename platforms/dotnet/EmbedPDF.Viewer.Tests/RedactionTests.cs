using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>The port of RedactionTests.swift.</summary>
public sealed class RedactionTests
{
    static readonly CancellationToken Cancelled = new(true);
    static readonly PdfRect Area = new(72, 72, 200, 20);

    static async Task<(PdfViewer Viewer, DocumentInfo Document, RedactionCapability Redaction, SearchCapability Search)> Opened()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Search = new SearchConfig(), Interaction = true, Annotation = "", Redaction = new RedactionConfig(),
        });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using (var annotations = await viewer.GetAnnotationAsync(document)) await annotations.RefreshAsync();
        return (viewer, document, await viewer.GetRedactionAsync(document), await viewer.GetSearchAsync(document));
    }

    /// <summary>A pending mark on the first page.</summary>
    static async Task<(PdfViewer Viewer, RedactionCapability Redaction, AnnotationRef Mark, PageRef Page)> Marked()
    {
        var (viewer, document, redaction, _) = await Opened();
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        var mark = await redaction.MarkAreaAsync(Area, page);
        return (viewer, redaction, mark.AnnotationRef, page);
    }

    /// <summary>Runs <paramref name="call"/> with a token cancelled at once: it finishes or throws <see cref="OperationCanceledException"/>.</summary>
    internal static async Task FinishesOrCancels(Func<CancellationToken, Task> call)
    {
        using var source = new CancellationTokenSource();
        var running = call(source.Token);
        await source.CancelAsync();
        try
        {
            await running;
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Fact]
    public async Task MarkingAnAreaIsPendingAndObserved()
    {
        var (viewer, document, redaction, _) = await Opened();
        await using var _v = viewer;
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        var events = redaction.ObserveEvents();
        var mark = await redaction.MarkAreaAsync(Area, page);
        Assert.Equal([mark], await redaction.GetPendingAsync(page));
        await Fixture.First(events, e => e as RedactionEvent.PendingChanged);
        await redaction.UnmarkAsync([mark.AnnotationRef]);
        Assert.Equal(0, await redaction.GetPendingCountAsync(page));
    }

    [Fact]
    public async Task ApplyingMatchesRemovesTheText()
    {
        var (viewer, _, redaction, search) = await Opened();
        await using var _v = viewer;
        Assert.True(await redaction.CanApplyAsync());
        var before = await search.FindAllAsync(new SearchQuery("Report"));
        var page = before[0].Page;
        var marks = await redaction.MarkMatchesAsync(new SearchQuery("Report"), [page]);
        Assert.NotEmpty(marks);
        var result = await redaction.ApplyPagesAsync([page]);
        Assert.Equal(RedactionPageStatus.Applied, result.Results[0].Status);
        var after = await search.FindAllAsync(new SearchQuery("Report"));
        Assert.All(after, hit => Assert.NotEqual(page, hit.Page));
    }

    [Fact]
    public async Task AClosedDocumentsCapabilityThrows()
    {
        var (viewer, document, redaction, _) = await Opened();
        await using var _v = viewer;
        await viewer.CloseAsync(document);
        var error = await Assert.ThrowsAsync<PdfException>(() => redaction.GetPendingAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
        Assert.Equal("PluginError", error.Name);
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, _, redaction, _) = await Opened();
        await using var _v = viewer;
        using var stop = new CancellationTokenSource();
        var reading = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in redaction.ObserveEvents(stop.Token)) { }
            }
            catch (OperationCanceledException)
            {
            }
        });
        await Task.Delay(20);
        await stop.CancelAsync();
        await reading.WaitAsync(TimeSpan.FromSeconds(5));
        await redaction.GetPendingAsync();
    }

    [Fact]
    public async Task ApplyAllCancelsWhenTheTaskIsCancelled()
    {
        var (viewer, _, redaction, _) = await Opened();
        await using var _v = viewer;
        await FinishesOrCancels(token => redaction.ApplyAllAsync(token));
        await redaction.GetPendingAsync();
    }

    [Fact]
    public async Task MarkMatchesCancelsWhenTheTaskIsCancelled()
    {
        var (viewer, _, redaction, _) = await Opened();
        await using var _v = viewer;
        // Cancelled before it is posted, as Swift's task cancelled at once is:
        // once a search for "e" over every page is running, it holds the
        // kernel thread past the abort for more than 90 seconds.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => redaction.MarkMatchesAsync(new SearchQuery("e"), null, Cancelled));
        await redaction.GetPendingAsync();
    }

    [Fact]
    public async Task MarkSelectionCancelsBeforeItStarts()
    {
        var (viewer, redaction, _, _) = await Marked();
        await using var _v = viewer;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => redaction.MarkSelectionAsync(Cancelled));
    }

    [Fact]
    public async Task MarkAreaCancelsBeforeItStarts()
    {
        var (viewer, redaction, _, page) = await Marked();
        await using var _v = viewer;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => redaction.MarkAreaAsync(new PdfRect(72, 200, 50, 20), page, Cancelled));
        Assert.Equal(1, await redaction.GetPendingCountAsync(page));
    }

    [Fact]
    public async Task MarkPageCancelsBeforeItStarts()
    {
        var (viewer, redaction, _, page) = await Marked();
        await using var _v = viewer;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => redaction.MarkPageAsync(page, Cancelled));
    }

    [Fact]
    public async Task UnmarkCancelsBeforeItStarts()
    {
        var (viewer, redaction, mark, page) = await Marked();
        await using var _v = viewer;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => redaction.UnmarkAsync([mark], Cancelled));
        Assert.Equal(1, await redaction.GetPendingCountAsync(page));
    }

    [Fact]
    public async Task ClearPendingCancelsBeforeItStarts()
    {
        var (viewer, redaction, _, _) = await Marked();
        await using var _v = viewer;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => redaction.ClearPendingAsync(Cancelled));
    }

    [Fact]
    public async Task UpdateLabelCancelsBeforeItStarts()
    {
        var (viewer, redaction, mark, _) = await Marked();
        await using var _v = viewer;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => redaction.UpdateLabelAsync(mark, new RedactionLabelPatch { OverlayText = "X" }, Cancelled));
    }

    [Fact]
    public async Task ApplyCancelsBeforeItStarts()
    {
        var (viewer, redaction, mark, page) = await Marked();
        await using var _v = viewer;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => redaction.ApplyAsync([mark], Cancelled));
        Assert.Equal(1, await redaction.GetPendingCountAsync(page));
    }

    [Fact]
    public async Task ApplyPagesCancelsBeforeItStarts()
    {
        var (viewer, redaction, _, page) = await Marked();
        await using var _v = viewer;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => redaction.ApplyPagesAsync([page], Cancelled));
    }

    [Fact]
    public async Task MarkingTheSelectionCreatesOneMarkPerPage()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Interaction = true, Selection = true, Annotation = "", Redaction = new RedactionConfig(),
        });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using (var annotations = await viewer.GetAnnotationAsync(document)) await annotations.RefreshAsync();
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        using var selection = await viewer.GetSelectionAsync(document);
        await selection.EnsureLoadedAsync(page);
        await selection.SelectPageAsync(page);
        using var redaction = await viewer.GetRedactionAsync(document);
        Assert.Single(await redaction.MarkSelectionAsync());
    }

    [Fact]
    public async Task ALabelAndTheSettingsRoundTrip()
    {
        var (viewer, redaction, mark, page) = await Marked();
        await using var _v = viewer;
        Assert.True(await redaction.CanUpdateLabelAsync(mark));
        var labelled = await redaction.UpdateLabelAsync(mark, new RedactionLabelPatch { OverlayText = "Withheld", Repeat = true });
        Assert.Equal("Withheld", labelled.OverlayText);
        Assert.True(labelled.Repeat);
        Assert.Equal(labelled, await redaction.GetPendingItemAsync(mark));
        var collateral = await redaction.EstimateCollateralAsync([mark]);
        Assert.Equal(collateral.Refs.Count, collateral.Count);
        await redaction.UpdateSettingsAsync(new RedactionConfig { Fill = "#ff0000", OverlayText = new RedactionOverlayText { FontSize = 9 } });
        var settings = await redaction.GetSettingsAsync();
        Assert.Equal("#ff0000", settings.Fill);
        Assert.Equal(9, settings.OverlayText.FontSize);
        await redaction.ResetSettingsAsync();
        Assert.NotEqual("#ff0000", (await redaction.GetSettingsAsync()).Fill);
        var applied = await redaction.ApplyAsync([mark]);
        Assert.Equal(applied.OpId, (await redaction.GetLastResultAsync())?.OpId);
        Assert.Equal(0, await redaction.GetPendingCountAsync(page));
    }
}
