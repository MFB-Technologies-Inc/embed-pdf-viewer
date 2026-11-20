using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed partial class IntegerSurfaceTests
{
    // The search, stage, view manager, signature, render and redaction cases
    // of IntegerSurfaceTests.swift port with their Phase 3 waves.

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
}
