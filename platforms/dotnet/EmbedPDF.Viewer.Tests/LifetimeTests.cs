using System.Diagnostics;
using System.Runtime.CompilerServices;
using uniffi.epdf_ffi;
using uniffi.epdf_plugin_render;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Review Focus 2: disposal under load, shared disposal, and the finalizer.</summary>
public sealed class LifetimeTests
{
    [Fact]
    public async Task DisposeWithRendersInFlightEnds()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Render = new RenderConfig() });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var pages = await viewer.GetPagesAsync(document);
        // Through the generated render handle until Wave A adds the capability.
        var render = await viewer.CallAsync(h => h.Render(document.Id), default);
        var tokens = new List<CancelToken>();
        var work = new List<Task>();
        foreach (var page in pages.Take(8))
        {
            var token = new CancelToken();
            tokens.Add(token);
            work.Add(render.RenderPage(Ffi.ToFfi(page.Page), new RenderPageOptions(Width: 1600), token));
        }
        work.Add(viewer.SaveAsync(document, SaveMode.Rewrite));
        work.Add(viewer.GetPagesAsync(document));

        await viewer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        foreach (var task in work)
        {
            var error = await Record.ExceptionAsync(() => task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(error is null or PdfException or OperationCanceledException or ViewerException, $"{error}");
        }
        foreach (var token in tokens) token.Dispose();
        render.Dispose();
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => viewer.GetDocumentsAsync())).Code);
    }

    [Fact]
    public async Task ConcurrentDisposeSharesCompletion()
    {
        var viewer = await PdfViewer.CreateAsync();
        _ = await viewer.OpenAsync(Fixture.Data("report"));
        var first = viewer.DisposeAsync().AsTask();
        var second = viewer.DisposeAsync().AsTask();
        Assert.Same(first, second);
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        await viewer.DisposeAsync();
    }

    [Fact]
    public async Task DisposingAnUnstartedViewerEnds()
    {
        var viewer = new PdfViewer();
        await viewer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static async Task<WeakReference> Abandon()
    {
        var viewer = await PdfViewer.CreateAsync();
        _ = await viewer.OpenAsync(Fixture.Data("report"));
        var events = viewer.ObserveDocumentEvents();
        _ = events;
        return new WeakReference(viewer);
    }

    [Fact]
    public async Task AnAbandonedViewerIsFinalizedWithoutBlocking()
    {
        var released = PdfViewer.AbandonedReleases;
        var reference = await Abandon();
        for (var i = 0; i < 20 && reference.IsAlive; i++)
        {
            GC.Collect();
            var clock = Stopwatch.StartNew();
            GC.WaitForPendingFinalizers();
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), $"the finalizers took {clock.Elapsed}");
            await Task.Delay(20);
        }
        Assert.False(reference.IsAlive);
        // The native viewer is released on the thread pool after the finalizer queues it.
        for (var i = 0; i < 500 && PdfViewer.AbandonedReleases == released; i++) await Task.Delay(10);
        Assert.True(PdfViewer.AbandonedReleases > released);
    }

    static void Explode(uniffi.epdf_viewer_host.ViewerHost host)
    {
        PdfViewer.Release(host);
        throw new InvalidOperationException("release failed");
    }

    [Fact]
    public async Task DisposeEndsWhenReleasingTheHostThrows()
    {
        var viewer = await PdfViewer.CreateAsync();
        viewer.ReleaseHost = Explode;
        await viewer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => viewer.GetDocumentsAsync())).Code);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static async Task AbandonWithAFailingRelease()
    {
        var viewer = await PdfViewer.CreateAsync();
        viewer.ReleaseHost = Explode;
    }

    [Fact]
    public async Task AFailingReleaseOnTheFinalizerPathDoesNotCrashTheProcess()
    {
        var released = PdfViewer.AbandonedReleases;
        await AbandonWithAFailingRelease();
        for (var i = 0; i < 500 && PdfViewer.AbandonedReleases == released; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(10);
        }
        Assert.True(PdfViewer.AbandonedReleases > released);
    }
}
