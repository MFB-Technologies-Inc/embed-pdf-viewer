using System.Runtime.CompilerServices;

namespace EmbedPDF.Models.Tests;

/// <summary>Port of <c>RenderRevisionsTests.swift</c>.</summary>
public class RenderRevisionsTests
{
    static async Task<(PdfViewer Viewer, DocumentInfo Document, IReadOnlyList<PageInfo> Pages, RenderCapability Render)> OpenedAsync()
    {
        var viewer = await Fixture.StartedAsync(b => b.Render(new RenderConfig()));
        var document = await viewer.OpenAsync(Fixture.Data());
        return (viewer, document, await viewer.GetPagesAsync(document), await viewer.GetRenderAsync(document));
    }

    [Fact]
    public Task InvalidatingAPageBumpsOnlyThatPage() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, pages, render) = await OpenedAsync();
        using var revisions = context.Watch(new RenderRevisions(render));
        await Observed.ExpectAsync(revisions, () => revisions.GetRevision(pages[0].Page), 1L,
            () => render.InvalidateAsync([pages[0].Page], RenderInvalidateScope.Annotations));
        Assert.Equal(0L, revisions.GetRevision(pages[1].Page));
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task AnInvalidationRightAfterInitIsCounted() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, pages, render) = await OpenedAsync();
        using var revisions = context.Watch(new RenderRevisions(render));
        // Sent before the model's own loop has had a turn to run.
        await render.InvalidateAsync([pages[0].Page], RenderInvalidateScope.Annotations);
        await Observed.ExpectAsync(revisions, () => revisions.GetRevision(pages[0].Page), 1L);
        await viewer.DisposeAsync();
    });

    [Fact]
    public Task AReleasedModelStopsObservingWhileTheDocumentStaysOpen() => SingleThreadContext.Run(async context =>
    {
        var (viewer, _, pages, render) = await OpenedAsync();
        var (released, observation) = await ObserveOnce(context, render, pages[0].Page);
        var events = render.ObserveEvents();
        await render.InvalidateAsync([pages[1].Page], RenderInvalidateScope.Annotations);
        await Observed.CollectedAsync(released);
        var invalidated = await Fixture.First(Fixture.Cast(events), e => e as RenderEvent.Invalidated);
        Assert.Equal([pages[1].Page], invalidated.Pages);
        // The document is still open, so the loop ends only if the release
        // cancelled it.
        await observation.WaitAsync(TimeSpan.FromSeconds(10));
        await viewer.DisposeAsync();
    });

    [MethodImpl(MethodImplOptions.NoInlining)]
    static async Task<(WeakReference, Task)> ObserveOnce(SingleThreadContext context, RenderCapability render, PageRef page)
    {
        var revisions = context.Watch(new RenderRevisions(render));
        // The model is observing by the time it is released.
        await Observed.ExpectAsync(revisions, () => revisions.GetRevision(page), 1L,
            () => render.InvalidateAsync([page], RenderInvalidateScope.Annotations));
        return (new WeakReference(revisions), revisions.ObservationTask);
    }

    [Fact]
    public Task ClosingTheDocumentKeepsTheCounts() => SingleThreadContext.Run(async context =>
    {
        var (viewer, document, pages, render) = await OpenedAsync();
        using var revisions = context.Watch(new RenderRevisions(render));
        await Observed.ExpectAsync(revisions, () => revisions.GetRevision(pages[0].Page), 1L,
            () => render.InvalidateAsync([pages[0].Page], RenderInvalidateScope.Annotations));
        await viewer.CloseAsync(document);
        // The close ends the stream, which ends the loop.
        await revisions.ObservationTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1L, revisions.GetRevision(pages[0].Page));
        Assert.Equal(0L, revisions.GetRevision(pages[1].Page));
        await viewer.DisposeAsync();
    });

}
