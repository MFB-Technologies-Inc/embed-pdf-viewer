using Xunit;
using GF = uniffi.epdf_ffi;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// The plumbing every plugin's <c>ObserveEvents</c> rides on, driven by a fake
/// kernel subscription.
/// </summary>
public sealed class PluginEventsTests
{
    sealed class FakeRelay() : EventRelay<int, string>(value => value < 0 ? throw new InvalidOperationException("unmappable") : $"#{value}")
    {
        public void Push(int value) => Deliver(value);

        public void End() => Finish();
    }

    sealed class FakeSubscription : GF.ISubscription
    {
        public int Cancels;

        public void Cancel() => Interlocked.Increment(ref Cancels);
    }

    static async Task<List<string>> Drain(IAsyncEnumerable<string> stream)
    {
        var seen = new List<string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var value in stream.WithCancellation(timeout.Token)) seen.Add(value);
        return seen;
    }

    [Fact]
    public async Task EventsArriveInOrderAndTheKernelEndEndsTheStream()
    {
        await using var viewer = await PdfViewer.CreateAsync();
        var relay = new FakeRelay();
        var native = new FakeSubscription();
        var stream = viewer.Stream(relay, _ => native, default);
        relay.Push(1);
        relay.Push(-1);
        relay.Push(2);
        relay.Push(3);
        relay.End();
        Assert.Equal(["#1", "#2", "#3"], await Drain(stream));
        Assert.Equal(1, native.Cancels);
        Assert.Equal(0, viewer.StreamCountForTests);
    }

    [Fact]
    public async Task CancellingEndsTheStreamAndCancelsTheKernelSubscriptionOnce()
    {
        await using var viewer = await PdfViewer.CreateAsync();
        var relay = new FakeRelay();
        var native = new FakeSubscription();
        using var cancel = new CancellationTokenSource();
        var stream = viewer.Stream(relay, _ => native, cancel.Token);
        relay.Push(1);
        var seen = new List<string>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var value in stream)
            {
                seen.Add(value);
                cancel.Cancel();
            }
        });
        relay.End();
        Assert.Equal(["#1"], seen);
        Assert.Equal(1, native.Cancels);
    }

    [Fact]
    public async Task DisposingTheViewerEndsLiveStreamsAndLaterOnesAtOnce()
    {
        var viewer = await PdfViewer.CreateAsync();
        var live = viewer.Stream(new FakeRelay(), _ => new FakeSubscription(), default);
        await viewer.DisposeAsync();
        Assert.Empty(await Drain(live));
        var subscribed = false;
        var late = viewer.Stream(new FakeRelay(), _ => { subscribed = true; return new FakeSubscription(); }, default);
        Assert.Empty(await Drain(late));
        Assert.False(subscribed);
    }

    [Fact]
    public async Task ASubscribeThatThrowsEndsTheStream()
    {
        await using var viewer = await PdfViewer.CreateAsync();
        var stream = viewer.Stream<int, string>(new FakeRelay(), _ => throw new InvalidOperationException("closed"), default);
        Assert.Empty(await Drain(stream));
    }

    [Fact]
    public async Task DeliveringWithNoReaderNeverBlocks()
    {
        await using var viewer = await PdfViewer.CreateAsync();
        var relay = new FakeRelay();
        _ = viewer.Stream(relay, _ => new FakeSubscription(), default);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 10_000; i++) relay.Push(i);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AQuadFromARectHasItsCornersWithYDown()
    {
        var quad = new PdfQuad(new PdfRect(1, 2, 3, 4));
        Assert.Equal(new PdfPoint(1, 2), quad.UpperLeft);
        Assert.Equal(new PdfPoint(4, 2), quad.UpperRight);
        Assert.Equal(new PdfPoint(1, 6), quad.LowerLeft);
        Assert.Equal(new PdfPoint(4, 6), quad.LowerRight);
    }

    public static TheoryData<PageLinkTarget> Targets => new()
    {
        new PageLinkTarget.GoTo(new PageDestination.Xyz(new PageRef(3), 1, null, 2)),
        new PageLinkTarget.GoTo(new PageDestination.Fit(new PageRef(3))),
        new PageLinkTarget.GoTo(new PageDestination.FitH(new PageRef(3), 4)),
        new PageLinkTarget.GoTo(new PageDestination.FitV(new PageRef(3), null)),
        new PageLinkTarget.GoTo(new PageDestination.FitR(new PageRef(3), 1, 2, 3, 4)),
        new PageLinkTarget.GoTo(new PageDestination.FitB(new PageRef(3))),
        new PageLinkTarget.GoTo(new PageDestination.FitBH(new PageRef(3), 5)),
        new PageLinkTarget.GoTo(new PageDestination.FitBV(new PageRef(3), 6)),
        new PageLinkTarget.Uri("https://embedpdf.test/"),
        new PageLinkTarget.GoToRemote("other.pdf"),
        new PageLinkTarget.Launch("app"),
        new PageLinkTarget.Named("NextPage"),
    };

    [Theory]
    [MemberData(nameof(Targets))]
    public void EveryLinkTargetRoundTrips(PageLinkTarget target) =>
        Assert.Equal(target, Ffi.ToPublic(Ffi.ToFfi(target)));

    [Fact]
    public void AnnotationRefsRoundTrip()
    {
        AnnotationRef[] refs = [new AnnotationRef.ObjectNumber(new PageRef(2), 9), new AnnotationRef.Index(new PageRef(2), 0)];
        foreach (var annotation in refs) Assert.Equal(annotation, Ffi.ToPublic(Ffi.ToFfi(annotation)));
    }

    [Fact]
    public void EveryLinkActivationMaps()
    {
        uniffi.epdf_plugin_link.LinkActivation[] generated =
        [
            new uniffi.epdf_plugin_link.LinkActivation.Revealed(),
            new uniffi.epdf_plugin_link.LinkActivation.Destination("{}"),
            new uniffi.epdf_plugin_link.LinkActivation.Uri("u"),
            new uniffi.epdf_plugin_link.LinkActivation.Named("n"),
            new uniffi.epdf_plugin_link.LinkActivation.Reported("{}"),
            new uniffi.epdf_plugin_link.LinkActivation.Dispatched(),
            new uniffi.epdf_plugin_link.LinkActivation.None(),
        ];
        Assert.Equal(
            [typeof(LinkActivation.Revealed), typeof(LinkActivation.Destination), typeof(LinkActivation.Uri), typeof(LinkActivation.Named),
             typeof(LinkActivation.Reported), typeof(LinkActivation.Dispatched), typeof(LinkActivation.None)],
            generated.Select(g => Ffi.ToPublic(g).GetType()));
    }
}
