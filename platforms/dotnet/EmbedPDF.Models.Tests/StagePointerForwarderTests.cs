namespace EmbedPDF.Models.Tests;

/// <summary>
/// Port of <c>StagePointerForwarderTests.swift</c>, through
/// <see cref="StageReaderModel"/>. The case that opens through the reader
/// model, <c>aReaderWithOnlyTheLinksFeatureActivatesALinkOnATap</c>, is in
/// <see cref="PdfReaderModelTests"/>.
/// </summary>
public class StagePointerForwarderTests
{
    /// <summary>One sample as a test compares it: its phase and where it was.</summary>
    internal readonly record struct Sent(PointerPhase Phase, double X, double Y)
    {
        public Sent(PointerPhase phase, PointerInput at) : this(phase, at.X, at.Y) { }

        public Sent(PointerSample sample) : this(sample.Phase, sample.Viewport.X, sample.Viewport.Y) { }
    }

    /// <summary>A reader under the ink tool that records every sample it sends.</summary>
    static async Task<(Reader Reader, List<Sent> Sent)> RecordingAsync(SingleThreadContext context)
    {
        var reader = await Reader.OpenAsync(context);
        await reader.Interaction.ActivateToolAsync(ToolId.Ink);
        var sent = new List<Sent>();
        reader.Model.PointerSampleDispatched = sample => sent.Add(new Sent(sample));
        return (reader, sent);
    }

    static PointerInput[] Points(StageReaderModel model, int count) =>
        Enumerable.Range(1, count).Select(i => Reader.Point(model, 0.1 * i)).ToArray();

    [Fact]
    public Task AForwardedDragUnderTheInkToolCreatesInk() => SingleThreadContext.Run(async context =>
    {
        var reader = await Reader.OpenAsync(context);
        var model = reader.Model;
        var before = await Reader.InkCountAsync(reader.Annotations!);
        await Observed.ExpectAsync(model, () => model.ForwardsDrags, true, () => reader.Interaction.ActivateToolAsync(ToolId.Ink));
        Assert.Equal(ToolId.Ink, model.ActiveToolId);
        model.PointerDown(Reader.Point(model, 0.30));
        foreach (var fraction in new[] { 0.35, 0.40, 0.45 })
        {
            model.PointerMoved(Reader.Point(model, fraction));
            await model.PointerCallsFinishedAsync();
        }
        model.PointerUp(Reader.Point(model, 0.45));
        await model.PointerCallsFinishedAsync();
        using (var drafts = reader.Annotations!.GetDrafts()) await drafts.FinishInkDraftAsync();
        Assert.Equal(before + 1, await Reader.InkCountAsync(reader.Annotations!));
        Assert.Null(model.Error);
        await reader.EndAsync();
    });

    [Fact]
    public Task ADragUnderTheDefaultToolIsNotForwarded() => SingleThreadContext.Run(async context =>
    {
        var reader = await Reader.OpenAsync(context);
        Assert.Equal(await reader.Interaction.GetDefaultToolIdAsync(), reader.Model.ActiveToolId);
        Assert.False(reader.Model.ForwardsDrags);
        await reader.EndAsync();
    });

    /// <summary>Without an interaction hub the reader has no tool, no forwarding, and the pointer calls send nothing.</summary>
    [Fact]
    public Task AReaderWithoutAHubForwardsNothing() => SingleThreadContext.Run(async context =>
    {
        await using var viewer = await Fixture.StartedAsync(b => b.Render(new RenderConfig()).Stage(new StageConfig()));
        var document = await viewer.OpenAsync(Fixture.Data("annotation_kinds"));
        using var model = context.Watch(new StageReaderModel(await viewer.GetStageAsync(document), await viewer.GetRenderAsync(document)));
        var observing = model.RunAsync();
        await model.SizeChangedAsync(390, 844, 2);
        var sent = 0;
        model.PointerSampleDispatched = _ => sent++;
        model.PointerDown(Reader.Point(model, 0.3));
        model.PointerMoved(Reader.Point(model, 0.4));
        model.PointerUp(Reader.Point(model, 0.4));
        model.Tapped(Reader.Point(model, 0.4));
        model.PointerCancelled();
        await model.PointerCallsFinishedAsync();
        Assert.Equal(0, sent);
        Assert.Null(model.ActiveToolId);
        Assert.False(model.ForwardsDrags);
        Assert.Null(model.Error);
        await viewer.DisposeAsync();
        await observing.WaitAsync(TimeSpan.FromSeconds(10));
    });

    [Fact]
    public Task ACancelledDragLeavesNoGestureOpen() => SingleThreadContext.Run(async context =>
    {
        var reader = await Reader.OpenAsync(context);
        var model = reader.Model;
        var before = await Reader.InkCountAsync(reader.Annotations!);
        await Observed.ExpectAsync(model, () => model.ForwardsDrags, true, () => reader.Interaction.ActivateToolAsync(ToolId.Ink));
        var phases = new List<PointerPhase>();
        model.PointerSampleDispatched = sample => phases.Add(sample.Phase);
        model.PointerDown(Reader.Point(model, 0.30));
        model.PointerMoved(Reader.Point(model, 0.40));
        await model.PointerCallsFinishedAsync();
        model.PointerCancelled();
        await model.PointerCallsFinishedAsync();
        Assert.Equal([PointerPhase.Down, PointerPhase.Move, PointerPhase.Cancel], phases);
        using (var drafts = reader.Annotations!.GetDrafts()) await drafts.FinishInkDraftAsync();
        Assert.Equal(before, await Reader.InkCountAsync(reader.Annotations!));
        Assert.Equal(ToolId.Ink, model.ActiveToolId);
        phases.Clear();
        model.PointerDown(Reader.Point(model, 0.50));
        model.PointerMoved(Reader.Point(model, 0.60));
        model.PointerUp(Reader.Point(model, 0.60));
        await model.PointerCallsFinishedAsync();
        Assert.Equal([PointerPhase.Down, PointerPhase.Move, PointerPhase.Up], phases);
        using (var drafts = reader.Annotations!.GetDrafts()) await drafts.FinishInkDraftAsync();
        Assert.Equal(before + 1, await Reader.InkCountAsync(reader.Annotations!));
        await reader.EndAsync();
    });

    [Fact]
    public Task MovesBehindOneInFlightCollapse() => SingleThreadContext.Run(async context =>
    {
        var reader = await Reader.OpenAsync(context);
        var model = reader.Model;
        await reader.Interaction.ActivateToolAsync(ToolId.Ink);
        var samples = new List<PointerSample>();
        model.PointerSampleDispatched = samples.Add;
        var start = Reader.Point(model, 0.2);
        model.PointerDown(start);
        var last = start;
        for (var step = 1; step <= 50; step++)
        {
            last = new PointerInput(start.X + step, start.Y + step);
            model.PointerMoved(last);
        }
        await model.PointerCallsFinishedAsync();
        var moves = samples.Where(sample => sample.Phase == PointerPhase.Move).ToList();
        Assert.NotEmpty(moves);
        Assert.True(moves.Count < 50);
        Assert.Equal(PointerPhase.Down, samples[0].Phase);
        Assert.Equal(new PdfPoint(last.X, last.Y), samples[^1].Viewport);
        Assert.Equal(PointerKind.Touch, samples[^1].PointerType);
        Assert.All(samples, sample => Assert.NotNull(sample.Page));
        model.PointerUp(last);
        await model.PointerCallsFinishedAsync();
        Assert.Equal(PointerPhase.Up, samples[^1].Phase);
        await reader.EndAsync();
    });

    [Fact]
    public Task GesturesSentInOneTurnKeepTheirOwnMoves() => SingleThreadContext.Run(async context =>
    {
        var (reader, sent) = await RecordingAsync(context);
        var model = reader.Model;
        var at = Points(model, 6);
        model.PointerDown(at[0]);
        model.PointerMoved(at[1]);
        model.PointerUp(at[2]);
        model.PointerDown(at[3]);
        model.PointerMoved(at[4]);
        model.PointerUp(at[5]);
        await model.PointerCallsFinishedAsync();
        Assert.Equal([
            new(PointerPhase.Down, at[0]), new(PointerPhase.Move, at[1]), new(PointerPhase.Up, at[2]),
            new(PointerPhase.Down, at[3]), new(PointerPhase.Move, at[4]), new Sent(PointerPhase.Up, at[5]),
        ], sent);
        await reader.EndAsync();
    });

    /// <summary>A tap with a drag open cancels the open gesture first, so the hub never gets a second down inside it.</summary>
    [Fact]
    public Task ATapWhileAGestureIsOpenCancelsItFirst() => SingleThreadContext.Run(async context =>
    {
        var (reader, sent) = await RecordingAsync(context);
        var model = reader.Model;
        var at = Points(model, 4);
        model.PointerDown(at[0]);
        model.PointerMoved(at[1]);
        await model.PointerCallsFinishedAsync();
        model.Tapped(at[2]);
        // A tap closes with its own up, so nothing is left open.
        model.Tapped(at[3]);
        await model.PointerCallsFinishedAsync();
        Assert.Equal([
            new(PointerPhase.Down, at[0]), new(PointerPhase.Move, at[1]), new(PointerPhase.Cancel, at[1]),
            new(PointerPhase.Down, at[2]), new(PointerPhase.Up, at[2]), new(PointerPhase.Down, at[3]), new Sent(PointerPhase.Up, at[3]),
        ], sent);
        await reader.EndAsync();
    });

    /// <summary>A down right after an up leaves the earlier gesture's waiting move in place, and a tap does too.</summary>
    [Fact]
    public Task ANewGestureDoesNotDropTheLastOnesWaitingMove() => SingleThreadContext.Run(async context =>
    {
        var (reader, sent) = await RecordingAsync(context);
        var model = reader.Model;
        var at = Points(model, 5);
        model.PointerDown(at[0]);
        model.PointerMoved(at[1]);
        model.PointerUp(at[1]);
        model.PointerDown(at[2]);
        model.PointerMoved(at[3]);
        model.PointerUp(at[3]);
        model.Tapped(at[4]);
        await model.PointerCallsFinishedAsync();
        Assert.Equal([
            new(PointerPhase.Down, at[0]), new(PointerPhase.Move, at[1]), new(PointerPhase.Up, at[1]),
            new(PointerPhase.Down, at[2]), new(PointerPhase.Move, at[3]), new(PointerPhase.Up, at[3]),
            new(PointerPhase.Down, at[4]), new Sent(PointerPhase.Up, at[4]),
        ], sent);
        await reader.EndAsync();
    });

    /// <summary>The cancel's sample is at the dropped move's point: the last place the touch was.</summary>
    [Fact]
    public Task ACancelDropsOnlyItsOwnWaitingMove() => SingleThreadContext.Run(async context =>
    {
        var (reader, sent) = await RecordingAsync(context);
        var model = reader.Model;
        var before = await Reader.InkCountAsync(reader.Annotations!);
        var at = Points(model, 5);
        model.PointerDown(at[0]);
        model.PointerMoved(at[1]);
        model.PointerCancelled();
        model.PointerDown(at[2]);
        model.PointerMoved(at[3]);
        model.PointerUp(at[4]);
        await model.PointerCallsFinishedAsync();
        Assert.Equal([
            new(PointerPhase.Down, at[0]), new(PointerPhase.Cancel, at[1]),
            new(PointerPhase.Down, at[2]), new(PointerPhase.Move, at[3]), new Sent(PointerPhase.Up, at[4]),
        ], sent);
        using (var drafts = reader.Annotations!.GetDrafts()) await drafts.FinishInkDraftAsync();
        Assert.Equal(before + 1, await Reader.InkCountAsync(reader.Annotations!));
        await reader.EndAsync();
    });

    /// <summary>A pinch's first finger opens a tool gesture. The pinch cancels it, and the rest of that touch sends nothing; the next touch draws again.</summary>
    [Fact]
    public Task APinchCancelsTheOpenToolGesture() => SingleThreadContext.Run(async context =>
    {
        var (reader, sent) = await RecordingAsync(context);
        var model = reader.Model;
        var before = await Reader.InkCountAsync(reader.Annotations!);
        var at = Points(model, 6);
        model.PointerDown(at[0]);
        model.PointerMoved(at[1]);
        // The move has reached the hub when the pinch begins; one still
        // waiting would be dropped by the cancel.
        await model.PointerCallsFinishedAsync();
        model.MagnifyChanged(1.1, at[1].X, at[1].Y);
        model.PointerMoved(at[2]);
        model.MagnifyChanged(1.2, at[1].X, at[1].Y);
        model.PointerMoved(at[3]);
        model.PointerUp(at[3]);
        model.PointerCancelled();
        await model.MagnifyEndedAsync();
        await model.PointerCallsFinishedAsync();
        Assert.Equal([new(PointerPhase.Down, at[0]), new(PointerPhase.Move, at[1]), new Sent(PointerPhase.Cancel, at[1])], sent);
        using (var drafts = reader.Annotations!.GetDrafts()) await drafts.FinishInkDraftAsync();
        Assert.Equal(before, await Reader.InkCountAsync(reader.Annotations!));
        model.PointerDown(at[4]);
        model.PointerMoved(at[5]);
        model.PointerUp(at[5]);
        await model.PointerCallsFinishedAsync();
        Assert.Equal([new(PointerPhase.Down, at[4]), new(PointerPhase.Move, at[5]), new Sent(PointerPhase.Up, at[5])], sent.Skip(3));
        await reader.EndAsync();
    });

    /// <summary><c>features.pdf</c> has a link.</summary>
    [Fact]
    public Task ATapOnALinkYieldsItsActivation() => SingleThreadContext.Run(async context =>
    {
        var reader = await Reader.OpenAsync(context, "features");
        var model = reader.Model;
        LinkItem? found = null;
        foreach (var page in await reader.Viewer.GetPagesAsync(reader.Document))
        {
            await reader.Link!.EnsureLoadedAsync(page.Page);
            found = (await reader.Link.GetLinksAsync(page.Page)).FirstOrDefault();
            if (found is not null) break;
        }
        var link = found!;
        await reader.Stage.GoToPageAsync(link.Page);
        var center = new PdfPoint(link.Bounds.X + link.Bounds.Width / 2, link.Bounds.Y + link.Bounds.Height / 2);
        var viewport = (await reader.Stage.PageToViewportAsync(link.Page, center))!.Value;
        var activations = model.ObserveLinkActivations();
        var second = model.ObserveLinkActivations();
        var samples = new List<PointerSample>();
        model.PointerSampleDispatched = samples.Add;
        model.Tapped(new PointerInput(viewport.X, viewport.Y));
        await model.PointerCallsFinishedAsync();
        Assert.Equal([PointerPhase.Down, PointerPhase.Up], samples.Select(sample => sample.Phase));
        Assert.Equal([1, 1], samples.Select(sample => sample.ClickCount));
        Assert.Equal([link.Page, link.Page], samples.Select(sample => sample.Page?.Page));
        var received = await Fixture.First(Fixture.Cast(activations), item => item as LinkActivation);
        // A second stream gets the same activation, not a share of the first's.
        var other = await Fixture.First(Fixture.Cast(second), item => item as LinkActivation);
        Assert.Equal(received, other);
        await reader.EndAsync();
    });

    /// <summary>A reader built by hand on a viewer with the link and interaction plugins and no annotation plugin.</summary>
    [Fact]
    public Task AHandBuiltReaderWithoutTheAnnotationPluginActivatesALinkOnATap() => SingleThreadContext.Run(async context =>
    {
        var reader = await Reader.OpenAsync(context, "features", annotation: false);
        var activation = await TapAVisibleLinkAsync(reader.Model, reader.Link!);
        Assert.NotNull(activation);
        Assert.Null(reader.Model.Error);
        await reader.EndAsync();
    });

    /// <summary>A page the reader has scrolled to gets its links loaded too.</summary>
    [Fact]
    public Task APageScrolledIntoViewHasItsLinksLoaded() => SingleThreadContext.Run(async context =>
    {
        var reader = await Reader.OpenAsync(context, "report", annotation: false);
        var model = reader.Model;
        var last = (await reader.Viewer.GetPagesAsync(reader.Document))[^1].Page;
        Assert.DoesNotContain(last, model.VisiblePages.Select(page => page.Page));
        Assert.False(await reader.Link!.IsLoadedAsync(last));
        await Observed.ExpectAsync(model, () => model.VisiblePages.Any(page => page.Page == last), true,
            () => model.Stage.GoToPageAsync(last));
        await model.LinkLoadsFinishedAsync();
        Assert.True(await reader.Link.IsLoadedAsync(last));
        await reader.EndAsync();
    });

    /// <summary>
    /// Taps the first link on the reader's first visible page and returns the
    /// activation, or <c>null</c> when none arrives in 10 seconds. Loads no
    /// links itself: the page's links are empty unless the reader loaded it.
    /// </summary>
    internal static async Task<LinkActivation?> TapAVisibleLinkAsync(StageReaderModel model, LinkCapability links)
    {
        await model.LinkLoadsFinishedAsync();
        var page = model.VisiblePages[0].Page;
        var link = (await links.GetLinksAsync(page)).FirstOrDefault();
        Assert.True(link is not null, "the reader did not load the page's links");
        var center = new PdfPoint(link.Bounds.X + link.Bounds.Width / 2, link.Bounds.Y + link.Bounds.Height / 2);
        var spot = (await model.Stage.PageToViewportAsync(page, center))!.Value;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var activations = model.ObserveLinkActivations(timeout.Token);
        model.Tapped(new PointerInput(spot.X, spot.Y));
        try
        {
            await foreach (var activation in activations) return activation;
        }
        catch (OperationCanceledException)
        {
        }
        return null;
    }
}
