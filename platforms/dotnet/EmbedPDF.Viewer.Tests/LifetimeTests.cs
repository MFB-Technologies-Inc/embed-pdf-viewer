using System.Diagnostics;
using System.Runtime.CompilerServices;
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
        using var render = await viewer.GetRenderAsync(document);
        var work = new List<Task>();
        foreach (var page in pages.Take(8))
        {
            work.Add(render.RenderPageAsync(page.Page, new RenderPageOptions { Width = 1600 }));
        }
        work.Add(viewer.SaveAsync(document, SaveMode.Rewrite));
        work.Add(viewer.GetPagesAsync(document));

        await viewer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        foreach (var task in work)
        {
            // Each call either finished before the kernel closed or reports that it closed.
            var error = await Record.ExceptionAsync(() => task.WaitAsync(TimeSpan.FromSeconds(5)));
            if (error is not null)
            {
                Assert.Equal(PdfErrorCode.InstanceClosed, Assert.IsType<PdfException>(error).Code);
            }
        }
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => viewer.GetDocumentsAsync())).Code);
    }

    [Fact]
    public async Task ConcurrentDisposeSharesCompletion()
    {
        var viewer = await PdfViewer.CreateAsync();
        _ = await viewer.OpenAsync(Fixture.Data("report"));
        using var start = new Barrier(2);
        // StartNew, not Run: Run would unwrap the inner task, and the test
        // compares the two disposal tasks themselves.
        Task<Task> Racer() => Task.Factory.StartNew(() =>
        {
            start.SignalAndWait();
            return viewer.DisposeAsync().AsTask();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var first = Racer();
        var second = Racer();
        var (a, b) = (await first, await second);
        Assert.Same(a, b);
        await Task.WhenAll(a, b).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Same(a, viewer.DisposeAsync().AsTask());
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

    /// <summary>A started viewer with a metadata stream nobody enumerates, dropped with everything else.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static async Task<(WeakReference Viewer, IAsyncEnumerable<MetadataEvent> Stream)> AbandonWithAPluginStream()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Metadata = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var metadata = await viewer.GetMetadataAsync(document);
        var stream = metadata.ObserveEvents();
        _ = metadata.ObserveEvents();
        return (new WeakReference(viewer), stream);
    }

    [Fact]
    public async Task ALivePluginStreamDoesNotKeepAnAbandonedViewerAlive()
    {
        var released = PdfViewer.AbandonedReleases;
        var (reference, stream) = await AbandonWithAPluginStream();
        for (var i = 0; i < 20 && reference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(20);
        }
        Assert.False(reference.IsAlive);
        for (var i = 0; i < 500 && PdfViewer.AbandonedReleases == released; i++) await Task.Delay(10);
        Assert.True(PdfViewer.AbandonedReleases > released);
        // Releasing the kernel ends its subscriptions, so a reader still holding the stream ends too.
        Assert.True(await MetadataTests.Ends(stream));
    }

    static async Task<(WeakReference Viewer, IAsyncEnumerable<InteractionEvent> Interaction, IAsyncEnumerable<I18nEvent> I18n, IAsyncEnumerable<ActionsEvent> Actions)> AbandonWithWaveBStreamsAndCallbacks()
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Interaction = true, Feedback = new() { Provider = new RecordingFeedback() },
            I18n = new() { Loader = new EmbeddedResourceLocaleLoader(typeof(LifetimeTests).Assembly, "Locales."), LazyLocales = ["es"] },
            Actions = new()
            {
                OpenSequence = OpenSequence.Off,
                JavaScript = new(true) { Engine = new EmbedPDF.Scripting.Jint.JintScriptEngineFactory() },
            },
        });
        var document = await viewer.OpenAsync(Fixture.Data("document_actions"));
        var interaction = await viewer.GetInteractionAsync(document);
        await interaction.RegisterHandlerAsync(new RecordingHandler(), "kept", 1);
        await interaction.RegisterToolAsync(new InteractionTool("kept", "default", []), permission: () => true);
        var i18n = await viewer.GetI18nAsync();
        var actions = await viewer.GetActionsAsync(document);
        await actions.SetUiHandlerAsync(new RecordingUI());
        await actions.RegisterExecutorAsync("goto", new RecordingExecutor());
        await actions.SetSubmitHandlerAsync(new NoSubmit());
        // Runs a script, so the engine factory has made a live realm.
        Assert.True(await actions.IsScriptingEnabledAsync());
        var ran = await actions.ExecuteAsync(ActionsTests.ScriptTree("var ran = true;"), ActionsTests.User);
        Assert.Equal([ActionNodeStatus.Executed], ran.Nodes.Select(n => n.Status));
        return (new WeakReference(viewer), interaction.ObserveEvents(), i18n.ObserveEvents(), actions.ObserveEvents());
    }

    sealed class NoSubmit : IActionSubmitHandler
    {
        public void Submit(ActionSubmitRequest request) { }
    }

    [Fact]
    public async Task LiveWaveBStreamsAndCallbacksDoNotKeepAnAbandonedViewerAlive()
    {
        var released = PdfViewer.AbandonedReleases;
        var (reference, interaction, i18n, actions) = await AbandonWithWaveBStreamsAndCallbacks();
        for (var i = 0; i < 20 && reference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(20);
        }
        Assert.False(reference.IsAlive);
        for (var i = 0; i < 500 && PdfViewer.AbandonedReleases == released; i++) await Task.Delay(10);
        Assert.True(PdfViewer.AbandonedReleases > released);
        Assert.True(await MetadataTests.Ends(interaction));
        Assert.True(await MetadataTests.Ends(i18n));
        Assert.True(await MetadataTests.Ends(actions));
    }
}
