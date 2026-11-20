using System.Text.Json.Serialization;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Props for the typed shell overloads.</summary>
public sealed record StyleProps(string AnnotationId, int Page);

/// <summary>Source-generated, so the typed overloads run without reflection (AOT).</summary>
[JsonSerializable(typeof(StyleProps))]
[JsonSerializable(typeof(int[]))]
internal sealed partial class ShellJson : JsonSerializerContext;

/// <summary>Port of <c>ShellTests.swift</c>.</summary>
public sealed class ShellTests : IAsyncLifetime
{
    PdfViewer viewer = null!;
    DocumentInfo document = null!;
    ShellCapability shell = null!;

    public async Task InitializeAsync()
    {
        viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Shell = true });
        document = await viewer.OpenAsync(Fixture.Data("report"));
        shell = await viewer.ShellAsync(document);
    }

    public async Task DisposeAsync() => await viewer.DisposeAsync();

    static object Shape(ShellSnapshot snapshot) =>
        string.Join("|", snapshot.Surfaces.Select(s => $"{s.Id},{s.Open},{s.Exclusive},{s.PropsJson}")) + "/" + string.Join(",", snapshot.OpenMenus);

    [Fact]
    public async Task TypedPropsRoundTrip()
    {
        await shell.OpenAsync("style", new StyleProps("a1", 3), ShellJson.Default.StyleProps);
        var surface = Assert.IsType<ShellSurface>(await shell.GetSurfaceAsync("style"));
        Assert.True(surface.Open);
        Assert.Equal(new StyleProps("a1", 3), surface.GetProps(ShellJson.Default.StyleProps));
        await shell.UpdatePropsAsync("style", new StyleProps("a2", 4), ShellJson.Default.StyleProps);
        Assert.Equal(new StyleProps("a2", 4), (await shell.GetSurfaceAsync("style"))!.GetProps(ShellJson.Default.StyleProps));
    }

    [Fact]
    public async Task ANeverOpenedSurfaceIsNull() => Assert.Null(await shell.GetSurfaceAsync("thumbnails"));

    /// <summary>The web plugin's plain object finds a member every object has, so these ids are found, closed and bare.</summary>
    [Theory]
    [InlineData("constructor")]
    [InlineData("toString")]
    public async Task AnObjectPrototypeNameIsFoundClosed(string id)
    {
        Assert.Equal(new ShellSurface(id, false, null, null, 0), await shell.GetSurfaceAsync(id));
        Assert.False(await shell.IsOpenAsync(id));
    }

    [Fact]
    public async Task PropsThatAreNotAnObjectThrow()
    {
        var error = await Assert.ThrowsAsync<PdfException>(() => shell.OpenAsync("style", new[] { 1, 2, 3 }, ShellJson.Default.Int32Array));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        Assert.False(await shell.IsOpenAsync("style"));
    }

    [Fact]
    public async Task AnExclusiveOpenIsObserved()
    {
        await shell.OpenAsync("thumbnails", exclusive: "left");
        var seen = new List<ShellEvent>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var events = shell.ObserveEvents(timeout.Token);
        await shell.OpenAsync("search", exclusive: "left");
        await foreach (var e in events)
        {
            seen.Add(e);
            if (seen.Count == 2) break;
        }
        Assert.Equal([new ShellEvent.SurfaceClosed("thumbnails"), new ShellEvent.SurfaceOpened("search", null)], seen);
        Assert.Equal(["search"], (await shell.ListOpenSurfacesAsync()).Select(s => s.Id));
    }

    [Fact]
    public async Task ASnapshotRestores()
    {
        await shell.OpenAsync("sidebar", exclusive: "left");
        await shell.OpenMenuAsync("file");
        var snapshot = await shell.GetSnapshotAsync();
        await shell.CloseAllAsync();
        await shell.CloseAllMenusAsync();
        Assert.False(await shell.IsMenuOpenAsync("file"));
        await shell.ApplySnapshotAsync(snapshot);
        Assert.Equal(Shape(snapshot), Shape(await shell.GetSnapshotAsync()));
        Assert.Equal(["file"], await shell.ListOpenMenusAsync());
    }

    [Fact]
    public async Task MenusAndSurfacesToggle()
    {
        await shell.ToggleMenuAsync("view");
        Assert.True(await shell.IsMenuOpenAsync("view"));
        await shell.CloseMenuAsync("view");
        Assert.False(await shell.IsMenuOpenAsync("view"));
        await shell.ToggleAsync("panel", new StyleProps("a", 1), ShellJson.Default.StyleProps);
        Assert.True(await shell.IsOpenAsync("panel"));
        await shell.ToggleAsync("panel");
        Assert.False(await shell.IsOpenAsync("panel"));
        await shell.OpenAsync("panel", propsJson: "{\"annotationId\":\"b\",\"page\":2}");
        var events = shell.ObserveEvents();
        await shell.CloseAsync("panel");
        Assert.Equal("panel", (await Fixture.First(events, e => e as ShellEvent.SurfaceClosed)).Id);
    }

    [Fact]
    public async Task EachDocumentKeepsItsOwnSurfaces()
    {
        var colors = await viewer.OpenAsync(Fixture.Data("colors"));
        using var second = await viewer.ShellAsync(colors);
        await shell.OpenAsync("sidebar");
        Assert.True(await shell.IsOpenAsync("sidebar"));
        Assert.False(await second.IsOpenAsync("sidebar"));
    }

    [Fact]
    public async Task ClosingTheDocumentFinishesTheEvents()
    {
        var events = MetadataTests.Ends(shell.ObserveEvents());
        await Task.Delay(50);
        await viewer.CloseAsync(document);
        Assert.True(await events);
    }

    [Fact]
    public async Task AClosedDocumentsCapabilityThrows()
    {
        await viewer.CloseAsync(document);
        var error = await Assert.ThrowsAsync<PdfException>(() => shell.IsOpenAsync("sidebar"));
        Assert.Equal((PdfErrorCode.InstanceClosed, "PluginError"), (error.Code, error.Name));
    }
}
