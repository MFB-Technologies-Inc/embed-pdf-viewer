using System.Collections.Concurrent;
using System.Text;
using uniffi.epdf_ffi;
using uniffi.epdf_viewer_host;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class HttpClientDocumentFetcherTests
{
    static readonly byte[] Body = Encoding.ASCII.GetBytes("%PDF-body");

    [Fact]
    public async Task AnOkResponseAnswersItsStatusAndBytes()
    {
        await using var server = Loopback.Answering(200, Body);
        var response = await new HttpClientDocumentFetcher().FetchAsync(server.Url("report.pdf").ToString(), default);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(Body, response.Content);
    }

    [Fact]
    public async Task ARefusalAnswersItsStatusForTheKernelToMap()
    {
        await using var server = Loopback.Answering(404, Body);
        using var client = new HttpClient();
        var response = await new HttpClientDocumentFetcher(client).FetchAsync(server.Url("missing.pdf").ToString(), default);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(Body, response.Content);
    }

    [Fact]
    public async Task ARelativeUrlIsInvalidInput()
    {
        var error = await Assert.ThrowsAsync<PdfException>(() => new HttpClientDocumentFetcher().FetchAsync("report.pdf", default));
        Assert.Equal(("InvalidURL", PdfErrorCode.InvalidInput, "The document URL is invalid."), (error.Name, error.Code, error.Detail));
    }

    [Fact]
    public async Task AFileUrlReadsTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"epdf-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, Body);
        try
        {
            var response = await new HttpClientDocumentFetcher().FetchAsync(new Uri(path).AbsoluteUri, default);
            Assert.Equal(200, response.StatusCode);
            Assert.Equal(Body, response.Content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CancellingAStalledFetchThrowsOperationCanceled()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = Loopback.Start(async context =>
        {
            await release.Task;
            await Loopback.Reply(context, 200, Body);
        });
        using var cancellation = new CancellationTokenSource();
        var fetching = new HttpClientDocumentFetcher().FetchAsync(server.Url("held.pdf").ToString(), cancellation.Token);
        await server.WaitForRequests(1);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetching);
        release.SetResult();
    }

    sealed class RecordingCompletion : IDocumentFetchCompletion
    {
        public readonly ConcurrentQueue<object> Calls = new();
        public readonly TaskCompletionSource Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void FailWith(HostFailure failure) { Calls.Enqueue(failure); Done.TrySetResult(); }
        public void Finish(ushort status, byte[] bytes) { Calls.Enqueue((status, bytes)); Done.TrySetResult(); }
    }

    sealed class StubFetcher(Func<CancellationToken, Task<DocumentFetchResponse>> body) : IDocumentFetcher
    {
        public Task<DocumentFetchResponse> FetchAsync(string url, CancellationToken cancellationToken) => body(cancellationToken);
    }

    static async Task<object> Outcome(Func<CancellationToken, Task<DocumentFetchResponse>> body)
    {
        var completion = new RecordingCompletion();
        new FetcherAdapter(new StubFetcher(body)).Start("https://embedpdf.test/x.pdf", completion);
        await completion.Done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        return Assert.Single(completion.Calls);
    }

    [Fact]
    public async Task TheAdapterFinishesOnceWithTheResponse()
    {
        var call = await Outcome(_ => Task.FromResult(new DocumentFetchResponse(200, Body)));
        Assert.Equal(((ushort)200, Body), ((ushort, byte[]))call);
    }

    [Fact]
    public async Task TheAdapterPassesAPdfExceptionOn()
    {
        var call = await Outcome(_ => throw new PdfException(PdfErrorCode.NotFound, "no such document", "StubFetcher"));
        Assert.Equal(new HostFailure("StubFetcher", "no such document", "not-found"), call);
    }

    [Fact]
    public async Task TheAdapterReportsACancellationAsAnAbort()
    {
        var call = (HostFailure)await Outcome(_ => throw new OperationCanceledException());
        Assert.Equal(("AbortError", "operation-cancelled"), (call.Name, call.Code));
    }

    [Fact]
    public async Task TheAdapterLeavesAForeignErrorUncoded()
    {
        var call = await Outcome(_ => throw new HttpRequestException("connection refused"));
        Assert.Equal(new HostFailure("HttpRequestException", "connection refused", null), call);
    }

    [Fact]
    public async Task TheAdapterStatusOutsideSixteenBitsFails()
    {
        var call = (HostFailure)await Outcome(_ => Task.FromResult(new DocumentFetchResponse(70000, Body)));
        Assert.Equal(("InvalidResponse", "operation-failed"), (call.Name, call.Code));
    }

    [Fact]
    public async Task TheAdaptersCancellationCancelsTheFetch()
    {
        var seen = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new RecordingCompletion();
        var cancel = new FetcherAdapter(new StubFetcher(async token =>
        {
            seen.SetResult(token);
            await Task.Delay(Timeout.Infinite, token);
            return new DocumentFetchResponse(200, Body);
        })).Start("https://embedpdf.test/x.pdf", completion);
        var token = await seen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        Assert.True(token.IsCancellationRequested);
        await completion.Done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("operation-cancelled", ((HostFailure)Assert.Single(completion.Calls)).Code);
    }
}
