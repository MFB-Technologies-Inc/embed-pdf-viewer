namespace EmbedPDF;

/// <summary>
/// The viewer's signature transport on <see cref="HttpClient"/>: the one POST
/// a timestamp authority or a revocation route sends. Set it with
/// <c>viewer.SetSignatureTransport(new HttpClientSignatureTransport())</c>.
/// </summary>
/// <remarks>
/// <para>
/// One post is one attempt. The engine keeps everything around it (the
/// RFC 3161 query, the route's JSON, retries and their delay, what a status
/// means), so every status answers <see cref="SignatureTransportOutcome.Replied"/>.
/// </para>
/// <para>
/// The transport enforces the request's limits. <see cref="SignatureTransportRequest.TimeoutMs"/>
/// covers the whole exchange: connecting, sending and reading the reply;
/// past it the answer is <see cref="SignatureTransportOutcome.TimedOut"/>. A
/// 2xx reply whose <c>Content-Length</c> or body runs past
/// <see cref="SignatureTransportRequest.MaxReplyBytes"/> is
/// <see cref="SignatureTransportOutcome.TooLarge"/>, and reading stops there.
/// A reply outside 200 to 299 whose body runs past the limit, breaks off or is
/// still arriving at the deadline is <see cref="SignatureTransportOutcome.Replied"/>
/// with its status and no body, so the engine decides by the status. A 2xx
/// body that breaks off is <see cref="SignatureTransportOutcome.Failed"/>.
/// </para>
/// <para>
/// Pass a client to control proxies, authentication or handlers, for example
/// one from <c>IHttpClientFactory</c>. Its own <see cref="HttpClient.Timeout"/>
/// still applies and reads as a timeout. Content-type is sent first and the
/// request's headers after it, so a later header of the same name wins.
/// </para>
/// </remarks>
public sealed class HttpClientSignatureTransport : ISignatureTransport
{
    static readonly HttpClient Shared = new(HttpClientDocumentFetcher.SharedHandler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };

    static readonly HashSet<string> ContentHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Allow", "Content-Disposition", "Content-Encoding", "Content-Language", "Content-Length", "Content-Location",
        "Content-MD5", "Content-Range", "Content-Type", "Expires", "Last-Modified",
    };

    readonly HttpClient client;

    /// <summary>A transport over <paramref name="client"/>, or over a shared client when it is <see langword="null"/>.</summary>
    public HttpClientSignatureTransport(HttpClient? client = null) => this.client = client ?? Shared;

    /// <inheritdoc/>
    public async Task<SignatureTransportOutcome> PostAsync(SignatureTransportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            return new SignatureTransportOutcome.Failed($"the transport can't post to {request.Url}: not an http or https address");
        }
        var limit = (long)Math.Max(0, request.MaxReplyBytes);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Max(1, request.TimeoutMs)));
        int? status = null;
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = new ByteArrayContent(Bytes.Array(request.Body)) };
            message.Content.Headers.TryAddWithoutValidation("Content-Type", request.ContentType);
            foreach (var header in request.Headers ?? [])
            {
                if (header is null) continue;
                // A content header goes on the content: on the request it is dropped.
                var headers = ContentHeaders.Contains(header.Name) ? (System.Net.Http.Headers.HttpHeaders)message.Content.Headers : message.Headers;
                headers.Remove(header.Name);
                headers.TryAddWithoutValidation(header.Name, header.Value);
            }
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            var code = (int)response.StatusCode;
            status = code;
            var ok = code is >= 200 and <= 299;
            if (response.Content.Headers.ContentLength is { } length && length > limit)
            {
                return ok ? new SignatureTransportOutcome.TooLarge() : new SignatureTransportOutcome.Replied(code, ReadOnlyMemory<byte>.Empty);
            }
            byte[]? body;
            try
            {
                body = await ReadUpToAsync(response.Content, limit, deadline.Token).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException && !ok)
            {
                return new SignatureTransportOutcome.Replied(code, ReadOnlyMemory<byte>.Empty);
            }
            if (body is null) return ok ? new SignatureTransportOutcome.TooLarge() : new SignatureTransportOutcome.Replied(code, ReadOnlyMemory<byte>.Empty);
            return new SignatureTransportOutcome.Replied(code, body);
        }
        catch (OperationCanceledException)
        {
            // The deadline: a status outside 2xx already in hand is that reply.
            return status is { } code && code is < 200 or > 299
                ? new SignatureTransportOutcome.Replied(code, ReadOnlyMemory<byte>.Empty)
                : new SignatureTransportOutcome.TimedOut();
        }
        catch (Exception error)
        {
            return new SignatureTransportOutcome.Failed($"{error.GetType().Name}: {error.Message}");
        }
    }

    /// <summary>The body, or <see langword="null"/> once it passes <paramref name="limit"/> bytes.</summary>
    static async Task<byte[]?> ReadUpToAsync(HttpContent content, long limit, CancellationToken cancellationToken)
    {
        using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0) return buffer.ToArray();
            if (buffer.Length + read > limit) return null;
            buffer.Write(chunk, 0, read);
        }
    }
}
