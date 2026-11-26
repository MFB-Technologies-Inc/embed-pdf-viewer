namespace EmbedPDF.Models.Tests;

/// <summary>Port of <c>TiledPageModelTests.swift</c>.</summary>
public class TiledPageModelTests
{
    static Task WithModel(Func<TiledPageModel, PageInfo, Task> body) => SingleThreadContext.Run(async context =>
    {
        await using var viewer = await Fixture.StartedAsync(b => b.Render(new RenderConfig()));
        var document = await viewer.OpenAsync(Fixture.Data());
        var page = (await viewer.GetPagesAsync(document))[0];
        using var render = await viewer.GetRenderAsync(document);
        await using var demand = await render.CreateViewDemandAsync("c8-pages");
        var model = context.Watch(new TiledPageModel(render, demand, page.Page));
        try
        {
            await body(model, page);
        }
        finally
        {
            await model.ReleaseAsync();
        }
    });

    static PdfRect Visible(PageInfo page) => new(0, 0, page.Width / 2, page.Height / 4);

    [Fact]
    public Task SmallDemandOnlyNeedsTheBase() => WithModel(async (model, page) =>
    {
        await model.UpdateAsync(256 / page.Width, page.Width, null);
        Assert.NotNull(model.Base);
        Assert.Empty(model.Tiles);
    });

    [Fact]
    public Task VisibleDeepDemandAddsDetailOverTheBudgetedBase() => WithModel(async (model, page) =>
    {
        var visible = Visible(page);
        await model.UpdateAsync(8, page.Width, visible);
        Assert.True(model.Base!.Width <= 640);
        Assert.NotEmpty(model.Tiles);
        Assert.All(model.Tiles, tile =>
        {
            Assert.True(tile.Rect.X < visible.Width + 1);
            Assert.True(tile.Rect.Y < visible.Height + 1);
        });
    });

    [Fact]
    public Task OffscreenDemandRetiresTilesAndReturningRestoresThem() => WithModel(async (model, page) =>
    {
        var visible = Visible(page);
        await model.UpdateAsync(8, page.Width, visible);
        Assert.NotEmpty(model.Tiles);
        await model.UpdateAsync(256 / page.Width, page.Width, new PdfRect(0, 0, 0, 0));
        Assert.Empty(model.Tiles);
        Assert.True(model.Base!.Width <= 256);
        await model.UpdateAsync(8, page.Width, visible);
        Assert.NotEmpty(model.Tiles);
    });

    [Fact]
    public Task ReleaseDropsTilesAndThePageCanAppearAgain() => WithModel(async (model, page) =>
    {
        var visible = Visible(page);
        await model.UpdateAsync(8, page.Width, visible);
        var baseImage = model.Base;
        Assert.NotNull(baseImage);
        Assert.NotEmpty(model.Tiles);
        await model.ReleaseAsync();
        Assert.Empty(model.Tiles);
        Assert.Same(baseImage, model.Base);
        await model.UpdateAsync(8, page.Width, visible);
        Assert.NotEmpty(model.Tiles);
    });

    [Fact]
    public Task PaintAcknowledgementsDoNotDropTheCurrentTiles() => WithModel(async (model, page) =>
    {
        var visible = Visible(page);
        var update = model.UpdateAsync(8, page.Width, visible);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (model.Tiles.Count == 0 && DateTime.UtcNow < deadline) await Task.Delay(5);
        foreach (var tile in model.Tiles.ToList()) await model.TileDisplayedAsync(tile.Id);
        await update;
        var keys = model.Tiles.Select(tile => tile.Id).ToList();
        Assert.NotEmpty(keys);
        Assert.Equal(keys.Count, keys.Distinct().Count());
        await model.UpdateAsync(8, page.Width, visible);
        Assert.Equal(keys, model.Tiles.Select(tile => tile.Id));
    });

    [Fact]
    public void ATilesEqualityIgnoresItsPixels()
    {
        var a = new PageTile("k", new PdfRect(0, 0, 1, 1), 1, new PixelBuffer(new byte[4], 1, 1, 4));
        var b = a with { Image = new PixelBuffer(new byte[] { 1, 2, 3, 4 }, 1, 1, 4) };
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, a with { Z = 2 });
    }
}
