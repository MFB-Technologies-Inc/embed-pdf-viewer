using System.Collections.Concurrent;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Review Focus 1: cancelling a URL open mid-transport.</summary>
public sealed class CancellationTests
{
    /// <summary>Records whether the request's own token was cancelled, which is the fetch being abandoned.</summary>
    sealed class WatchingHandler : DelegatingHandler
    {
        public readonly TaskCompletionSource Cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public WatchingHandler() : base(new SocketsHttpHandler()) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => Cancelled.TrySetResult());
            return base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>Collects unobserved task exceptions while it is alive.</summary>
    internal sealed class UnobservedWatch : IDisposable
    {
        public readonly ConcurrentQueue<Exception> Seen = new();

        public UnobservedWatch() => TaskScheduler.UnobservedTaskException += OnUnobserved;

        void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs args) => Seen.Enqueue(args.Exception);

        public void Collect()
        {
            for (var i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public void Dispose() => TaskScheduler.UnobservedTaskException -= OnUnobserved;
    }

    [Fact]
    public async Task OpenUriCancelsTheTransportAndClosesTheLoadingTab()
    {
        using var unobserved = new UnobservedWatch();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var body = Fixture.Data("report");
        await using var server = Loopback.Start(async context =>
        {
            await release.Task;
            await Loopback.Reply(context, 200, body);
        });
        var watching = new WatchingHandler();
        using var client = new HttpClient(watching);
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { DocumentFetcher = new HttpClientDocumentFetcher(client) });
        using var cancellation = new CancellationTokenSource();

        var opening = viewer.OpenAsync(server.Url("held.pdf"), cancellationToken: cancellation.Token);
        await server.WaitForRequests(1);
        Assert.Single(server.Requests);
        Assert.Equal(DocumentStatus.Loading, (await viewer.GetDocumentsAsync()).Single().Status);

        cancellation.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
        // The kernel answered the cancelled Rust token, not just the .NET side.
        Assert.Equal(PdfErrorCode.OperationCancelled, Assert.IsType<PdfException>(error.InnerException).Code);
        Assert.Empty(await viewer.GetDocumentsAsync());
        await watching.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        release.SetResult();
        await Task.Delay(100);
        unobserved.Collect();
        Assert.Empty(unobserved.Seen);
        // The viewer is still usable after the cancelled open.
        Assert.Equal(14, (await viewer.OpenAsync(body)).PageCount);
    }

    sealed class GatedFetcher(byte[] body) : IDocumentFetcher
    {
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Answered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Ignores its token on purpose: a fetcher that answers late.
        public async Task<DocumentFetchResponse> FetchAsync(string url, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Release.Task;
            Answered.TrySetResult();
            return new DocumentFetchResponse(200, body);
        }
    }

    [Fact]
    public async Task AFetcherAnsweringAfterDisposeIsIgnored()
    {
        using var unobserved = new UnobservedWatch();
        var fetcher = new GatedFetcher(Fixture.Data("report"));
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { DocumentFetcher = fetcher });
        var opening = viewer.OpenAsync(new Uri("https://embedpdf.test/late.pdf"));
        await fetcher.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await viewer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));
        // Disposal, not the caller, cancelled it: the viewer closed.
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => opening)).Code);
        fetcher.Release.SetResult();
        await fetcher.Answered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);
        unobserved.Collect();
        Assert.Empty(unobserved.Seen);
    }

    [Fact]
    public async Task AnOpenCancelledBeforeItStartsPostsNothing()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { DocumentFetcher = new GatedFetcher([]) });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            viewer.OpenAsync(new Uri("https://embedpdf.test/x.pdf"), cancellationToken: new CancellationToken(true)));
        Assert.Empty(await viewer.GetDocumentsAsync());
    }
}
