using GF = uniffi.epdf_ffi;
using GH = uniffi.epdf_viewer_host;

namespace EmbedPDF;

/// <summary>
/// The transport for URL document sources. The kernel owns loading tabs,
/// HTTP refusals, PDF validation and retry; the fetcher owns the request.
/// </summary>
/// <remarks>
/// <para>
/// Answer every HTTP status with its body: the kernel turns a refusal into a
/// failed tab (a 404 is <c>not-found</c>) that <see cref="PdfViewer.RetryAsync"/>
/// can fetch again. Throw for a transport failure: a <see cref="PdfException"/>
/// keeps its name, code and detail; an <see cref="OperationCanceledException"/>
/// is <c>operation-cancelled</c> only when the token passed in fired. Anything
/// else, including a cancellation the kernel did not ask for (an
/// <see cref="HttpClient.Timeout"/> elapsing), is reported under its type name
/// with no code: the tab fails, and <see cref="PdfViewer.RetryAsync"/> can
/// fetch it again.
/// </para>
/// <para>
/// The token fires when the open is cancelled or its loading tab closes. The
/// call starts on a thread-pool thread, never on the kernel's.
/// </para>
/// </remarks>
public interface IDocumentFetcher
{
    /// <summary>Fetches <paramref name="url"/> as the kernel passes it.</summary>
    Task<DocumentFetchResponse> FetchAsync(string url, CancellationToken cancellationToken);
}

/// <summary>What a fetch answered.</summary>
/// <param name="StatusCode">The HTTP status; 200 for a source with no status, such as a file.</param>
/// <param name="Content">The body.</param>
public sealed record DocumentFetchResponse(int StatusCode, byte[] Content);

/// <summary>
/// The default <see cref="IDocumentFetcher"/>: a <c>GET</c> through an
/// <see cref="HttpClient"/>, and a read for a <c>file:</c> URL. Supply a client,
/// for example from <c>IHttpClientFactory</c>, to set headers, cookies,
/// authentication or the handler.
/// </summary>
public sealed class HttpClientDocumentFetcher : IDocumentFetcher
{
    static readonly HttpClient Shared = new();
    readonly HttpClient client;

    /// <summary>Fetches through <paramref name="client"/>, or a shared client when it is <see langword="null"/>.</summary>
    public HttpClientDocumentFetcher(HttpClient? client = null) => this.client = client ?? Shared;

    /// <inheritdoc/>
    public async Task<DocumentFetchResponse> FetchAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new PdfException("InvalidURL", PdfErrorCode.InvalidInput, "The document URL is invalid.");
        }
        if (uri.IsFile)
        {
            return new DocumentFetchResponse(200, await File.ReadAllBytesAsync(uri.LocalPath, cancellationToken).ConfigureAwait(false));
        }
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return new DocumentFetchResponse((int)response.StatusCode, body);
    }
}

/// <summary>
/// Adapts an <see cref="IDocumentFetcher"/> to the kernel's callback
/// interface. The kernel calls <see cref="Fetch"/> on its own thread, so the
/// fetch starts on the thread pool, and the completion is answered exactly
/// once and then released.
/// </summary>
internal sealed class FetcherAdapter(IDocumentFetcher fetcher) : GH.DocumentFetcher
{
    public GH.DocumentFetchCancellation Fetch(string url, GH.DocumentFetchCompletion completion) => Start(url, completion);

    internal GH.DocumentFetchCancellation Start(string url, GH.IDocumentFetchCompletion completion)
    {
        var cancellation = new CancellationTokenSource();
        _ = Task.Run(() => Run(url, completion, cancellation));
        return new Cancellation(cancellation);
    }

    async Task Run(string url, GH.IDocumentFetchCompletion completion, CancellationTokenSource cancellation)
    {
        GF.HostFailure? failure = null;
        DocumentFetchResponse? response = null;
        try
        {
            response = await fetcher.FetchAsync(url, cancellation.Token).ConfigureAwait(false);
            if (response.StatusCode is < 0 or > ushort.MaxValue)
            {
                failure = new GF.HostFailure("InvalidResponse", "The response has an invalid HTTP status.", "operation-failed");
            }
        }
        catch (Exception error)
        {
            failure = Failure(error, cancellation.IsCancellationRequested);
        }
        try
        {
            if (failure is not null)
            {
                completion.FailWith(failure);
            }
            else
            {
                completion.Finish((ushort)response!.StatusCode, response.Content);
            }
        }
        catch (Exception error) when (error is ObjectDisposedException or GF.ViewerException)
        {
            // The viewer shut down first; nobody waits for this answer.
        }
        finally
        {
            (completion as IDisposable)?.Dispose();
            cancellation.Dispose();
        }
    }

    /// <summary>
    /// The kernel's failure for <paramref name="error"/>. A cancellation is an
    /// abort only when the kernel <paramref name="cancelled"/> the fetch; any
    /// other one, such as a client timeout, is a transport failure.
    /// </summary>
    internal static GF.HostFailure Failure(Exception error, bool cancelled) => error switch
    {
        PdfException pdf => new GF.HostFailure(pdf.Name, pdf.Detail, pdf.Code.Value),
        OperationCanceledException when cancelled => new GF.HostFailure("AbortError", error.Message, "operation-cancelled"),
        _ => new GF.HostFailure(error.GetType().Name, error.Message, null),
    };

    sealed class Cancellation(CancellationTokenSource source) : GH.DocumentFetchCancellation
    {
        public void Cancel()
        {
            try
            {
                source.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The fetch already finished.
            }
        }
    }
}
