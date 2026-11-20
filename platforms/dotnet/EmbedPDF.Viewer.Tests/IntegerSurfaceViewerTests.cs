using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed partial class IntegerSurfaceTests
{
    // The stage, signature and redaction cases of IntegerSurfaceTests.swift
    // port with their Phase 3 waves.

    /// <summary>A C# int below zero; one past <c>UInt32.max</c> can't be written.</summary>
    static readonly int[] OutsideUInt32 = [-1, int.MinValue];

    static async Task ExpectInvalidInput(Func<Task> call) =>
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(call)).Code);

    [Fact]
    public async Task ViewerPagesAndTabsTakeAndGiveInt()
    {
        await using var viewer = await PdfViewer.CreateAsync();
        var first = await viewer.OpenAsync(Fixture.Data("report"));
        var second = await viewer.OpenAsync(Fixture.Data("colors"), new OpenDocumentOptions { Activate = false });
        Assert.Equal(2, await viewer.GetDocumentCountAsync());
        // The engine's page index is a long, so every int reaches it: one
        // below zero or past the last page has no page.
        Assert.Null(await viewer.GetPageAtAsync(-1, first));
        Assert.Null(await viewer.GetPageAtAsync(int.MaxValue, first));
        var page = Assert.IsType<PageInfo>(await viewer.GetPageAtAsync(0, first));
        Assert.Equal(0, await viewer.GetPageIndexAsync(page.Page, first));
        Assert.Equal(-1, await viewer.GetPageIndexAsync(page.Page, second));
        // A tab position is clamped into the order at both ends.
        await viewer.MoveAsync(first, int.MaxValue);
        Assert.Equal([second.Id, first.Id], await viewer.GetDocumentOrderAsync());
        await viewer.MoveAsync(first, -1);
        Assert.Equal([first.Id, second.Id], await viewer.GetDocumentOrderAsync());
        await viewer.MoveAsync(first, 0);
        Assert.Equal([first.Id, second.Id], await viewer.GetDocumentOrderAsync());
    }

    [Fact]
    public async Task SearchHitIndexesAreInt()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Search = new() });
        using var search = await viewer.SearchAsync(await viewer.OpenAsync(Fixture.Data("report")));
        _ = await search.SearchAsync(new SearchQuery("the"));
        int count = await search.GetHitCountAsync();
        Assert.Equal(count, (await search.GetHitsAsync()).Count);
        foreach (var value in OutsideUInt32) await ExpectInvalidInput(() => search.GoToHitAsync(value));
        Assert.Equal(0, await search.GetActiveHitIndexAsync());
        _ = await search.GoToHitAsync(1);
        Assert.Equal(1, await search.GetActiveHitIndexAsync());
        _ = await search.GoToHitAsync(0);
        Assert.Equal(0, await search.GetActiveHitIndexAsync());
        // The largest index C# can pass still wraps modulo the hit count.
        _ = await search.GoToHitAsync(int.MaxValue);
        Assert.Equal(int.MaxValue % count, await search.GetActiveHitIndexAsync());
        await search.ClearAsync();
        Assert.Null(await search.GetActiveHitIndexAsync());
    }

    [Fact]
    public async Task PaneAndTabIndexesAreInt()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { ViewManager = true });
        using var manager = await viewer.ViewManagerAsync();
        var report = await viewer.OpenAsync(Fixture.Data("report"));
        var colors = await viewer.OpenAsync(Fixture.Data("colors"));
        var second = await manager.CreatePaneAsync();
        foreach (var value in OutsideUInt32)
        {
            await ExpectInvalidInput(() => manager.MovePaneAsync(second, value));
            await ExpectInvalidInput(() => manager.AddDocumentAsync(colors, second, value));
            await ExpectInvalidInput(() => manager.MoveDocumentAsync(report, "pane-1", value));
            await ExpectInvalidInput(() => manager.MoveDocumentAsync(report, "pane-1", second, value));
        }
        Assert.Equal(["pane-1", second], await manager.GetPaneOrderAsync());
        Assert.Equal([report.Id, colors.Id], (await manager.GetPaneAsync("pane-1"))?.DocumentIds);
        await manager.MovePaneAsync(second, 0);
        Assert.Equal([second, "pane-1"], await manager.GetPaneOrderAsync());
        await manager.MoveDocumentAsync(colors, "pane-1", 0);
        Assert.Equal([colors.Id, report.Id], (await manager.GetPaneAsync("pane-1"))?.DocumentIds);
        await manager.AddDocumentAsync(colors, second, 0);
        Assert.Equal([colors.Id], (await manager.GetPaneAsync(second))?.DocumentIds);
        await manager.MoveDocumentAsync(report, "pane-1", second, 0);
        Assert.Equal([report.Id, colors.Id], (await manager.GetPaneAsync(second))?.DocumentIds);
    }

    [Fact]
    public async Task ABatchConcurrencyBelowZeroThrowsInvalidInput()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Render = new() });
        var document = await viewer.OpenAsync(Fixture.Data("colors"));
        using var render = await viewer.RenderAsync(document);
        var pages = (await viewer.GetPagesAsync(document)).Select(p => p.Page).ToList();
        var options = new RenderPageOptions { Width = 30 };
        foreach (var value in OutsideUInt32) await ExpectInvalidInput(() => render.RenderPagesAsync(pages, options, value));
        // The engine runs at least one render at a time.
        var batch = await render.RenderPagesAsync(pages, options, 0);
        Assert.Equal(pages, batch.Applied.Select(a => a.Page));
    }
}
