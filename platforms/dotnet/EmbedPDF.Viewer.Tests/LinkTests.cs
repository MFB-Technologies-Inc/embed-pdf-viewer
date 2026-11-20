using System.Text.Json;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class LinkTests
{
    static async Task<(PdfViewer Viewer, DocumentInfo Document, LinkCapability Link, PageRef Page)> Opened()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Link = true });
        var document = await viewer.OpenAsync(Fixture.Data("features"));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        var link = await viewer.GetLinkAsync(document);
        await link.EnsureLoadedAsync(page);
        return (viewer, document, link, page);
    }

    static async Task<LinkEvent.Activated> Activated(IAsyncEnumerable<LinkEvent> events)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var e in events.WithCancellation(timeout.Token))
        {
            if (e is LinkEvent.Activated found) return found;
        }
        throw new InvalidOperationException("the stream ended first");
    }

    static readonly LinkActivation Target = new LinkActivation.Uri("https://example.invalid/target");

    [Fact]
    public async Task APagesLinksAreListed()
    {
        var (viewer, _, link, page) = await Opened();
        await using var _v = viewer;
        var links = await link.GetLinksAsync(page);
        Assert.Equal(2, links.Count);
        Assert.True(await link.IsLoadedAsync(page));
        Assert.Equal("ready", await link.GetStatusAsync(page));
        Assert.Equal(links[0], await link.GetLinkAsync(page, links[0].Id));
        Assert.Equal(2, (await link.GetAllLinksAsync()).Count(l => l.Page == page));
        Assert.Equal(page, (await link.GetLinkAtAsync(page, new PdfPoint(100, 145)))?.Page);
        // The default pointer tool enables link navigation.
        Assert.True(await link.IsNavigationEngagedAsync());
    }

    [Fact]
    public async Task ABareTargetIsLabelled()
    {
        var (viewer, _, link, page) = await Opened();
        await using var _v = viewer;
        Assert.Equal("https://example.invalid/x", await link.GetLabelAsync("""{"kind": "uri", "uri": "https://example.invalid/x"}"""));
        Assert.Equal("NextPage", await link.GetLabelAsync("""{"kind": "named", "name": "NextPage"}"""));
        // The same label a listed link gets for its own target.
        var listed = (await link.GetLinksAsync(page))[0];
        Assert.Equal(await link.GetLabelAsync(listed), await link.GetLabelAsync(listed.TargetJson));
    }

    [Fact]
    public async Task AUriLinkIsReportedToTheApp()
    {
        var (viewer, _, link, page) = await Opened();
        await using var _v = viewer;
        var events = link.ObserveEvents();
        Assert.Equal(Target, await link.ActivateAtAsync(page, new PdfPoint(100, 145)));
        Assert.Equal(Target, (await Activated(events)).Activation);
    }

    [Fact]
    public async Task AGotoLinkWithoutAStageReportsItsDestination()
    {
        var (viewer, _, link, page) = await Opened();
        await using var _v = viewer;
        var activation = Assert.IsType<LinkActivation.Destination>(await link.ActivateAtAsync(page, new PdfPoint(100, 85)));
        // The page-space PageDestination record.
        using var json = JsonDocument.Parse(activation.DestinationJson);
        Assert.Equal("xyz", json.RootElement.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task ResolvingAndActivatingATargetMatch()
    {
        var (viewer, _, link, page) = await Opened();
        await using var _v = viewer;
        var listed = (await link.GetLinksAsync(page)).Single(l => l.TargetJson.Contains("uri"));
        Assert.Equal(new LinkResolution.Uri("https://example.invalid/target"), await link.ResolveAsync(listed.TargetJson));
        Assert.Equal(Target, await link.ActivateAsync(listed));
        Assert.Equal(Target, await link.ActivateAsync(listed.TargetJson));
        Assert.Null(await link.ActivateAtAsync(page, new PdfPoint(5, 5)));
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => link.ResolveAsync("{"))).Code);
    }

    [Fact]
    public async Task ATargetWithAnUnpairedSurrogateIsInvalidInput()
    {
        var (viewer, _, link, _) = await Opened();
        await using var _v = viewer;
        const string target = "{\"kind\": \"uri\", \"uri\": \"https://example.invalid/\uD800\"}";
        Func<Task>[] calls = [() => link.ResolveAsync(target), () => link.ActivateAsync(target), () => link.GetLabelAsync(target)];
        var events = link.ObserveEvents();
        foreach (var call in calls)
        {
            var task = call();
            var error = await Assert.ThrowsAsync<PdfException>(() => task);
            Assert.Equal((PdfErrorCode.InvalidInput, "EncodingError"), (error.Code, error.Name));
        }
        // Nothing was activated.
        using var quiet = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { await foreach (var _ in events.WithCancellation(quiet.Token)) Assert.Fail("an event arrived"); });
    }

    [Fact]
    public async Task ATapOnALinkActivatesItOnceTheTapHandlerIsRegistered()
    {
        var (viewer, document, link, page) = await Opened();
        await using var _v = viewer;
        using var interaction = await viewer.GetInteractionAsync(document);
        using var registration = await link.RegisterTapHandlerAsync();
        var events = link.ObserveEvents();
        // Inside the external link: x 40...360, y 130...160.
        var at = new PdfPoint(100, 145);
        foreach (var phase in new[] { PointerPhase.Down, PointerPhase.Up })
        {
            interaction.DispatchPointer(new PointerSample(phase, at, new PointerModifiers())
            {
                Page = new PointerPageHit(page, at),
                PointerType = PointerKind.Touch,
            });
        }
        Assert.Equal(Target, (await Activated(events)).Activation);
    }

    [Fact]
    public async Task AClosedDocumentsCapabilityThrows()
    {
        var (viewer, document, link, page) = await Opened();
        await using var _v = viewer;
        await viewer.CloseAsync(document);
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => link.GetLinksAsync(page))).Code);
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, _, link, page) = await Opened();
        await using var _v = viewer;
        using var cancel = new CancellationTokenSource();
        var reading = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in link.ObserveEvents(cancel.Token)) { }
            }
            catch (OperationCanceledException) { }
        });
        await Task.Delay(20);
        cancel.Cancel();
        await reading;
        _ = await link.ActivateAtAsync(page, new PdfPoint(100, 145));
    }
}
