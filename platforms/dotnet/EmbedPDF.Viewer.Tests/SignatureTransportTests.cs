using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// The port of SignatureTransportTests.swift over <see cref="HttpClientSignatureTransport"/>.
/// The route tests need <c>EMBEDPDF_SIGNATURE_ROUTES</c> (the address of
/// <c>platforms/android/tests/signature-routes.mjs</c>) and are skipped without
/// it. The catch-up cases of that file are ported with their waves:
/// the standard commands in <see cref="CommandsTests"/>, the tool permission
/// in <see cref="InteractionTests"/>, the creation draft in
/// <see cref="AnnotationTests"/>, the stamp library id in <see cref="StampTests"/>
/// and the signature settings in <see cref="SignatureTests"/>.
/// </summary>
public sealed class SignatureTransportTests
{
    static readonly string? Routes = Environment.GetEnvironmentVariable("EMBEDPDF_SIGNATURE_ROUTES");

    static byte[] Bytes(int n) => [.. Enumerable.Range(0, n).Select(i => (byte)(i % 251))];

    static SignatureTransportRequest Request(
        string url, byte[]? body = null, int timeoutMs = 10_000, int maxReplyBytes = 1024 * 1024,
        IReadOnlyList<SignatureTransportHeader>? headers = null, string contentType = "application/timestamp-query") =>
        new(url, contentType, headers ?? [], body ?? [0x30, 0x03, 0x02, 0x01, 0x01], timeoutMs, maxReplyBytes);

    static string Text(SignatureTransportOutcome outcome) => outcome switch
    {
        SignatureTransportOutcome.Replied r => $"replied {r.Status} {r.Body.Length}",
        SignatureTransportOutcome.TimedOut => "timedOut",
        SignatureTransportOutcome.TooLarge => "tooLarge",
        SignatureTransportOutcome.Failed f => $"failed {f.Message}",
        _ => "?",
    };

    static async Task<(SignatureTransportOutcome Outcome, TimeSpan Took)> Send(SignatureTransportRequest request)
    {
        var clock = Stopwatch.StartNew();
        var outcome = await new HttpClientSignatureTransport().PostAsync(request);
        return (outcome, clock.Elapsed);
    }

    /// <summary>One raw HTTP/1.1 exchange per connection, so a route can stall, stream or break off.</summary>
    sealed class Server : IAsyncDisposable
    {
        readonly TcpListener listener = new(IPAddress.Loopback, 0);
        readonly CancellationTokenSource stop = new();
        readonly ConcurrentQueue<Sent> sent = new();
        readonly Task loop;

        public sealed record Sent(string Method, string Path, IReadOnlyDictionary<string, List<string>> Headers, byte[] Body);

        public Server()
        {
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            loop = Task.Run(Serve);
        }

        public int Port { get; }
        public string Base => $"http://127.0.0.1:{Port}";
        public IReadOnlyList<Sent> Requests => [.. sent];

        public void Stop()
        {
            stop.Cancel();
            listener.Stop();
        }

        public async ValueTask DisposeAsync()
        {
            Stop();
            try { await loop; } catch (Exception) { }
        }

        async Task Serve()
        {
            while (!stop.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync(stop.Token); }
                catch (Exception) { return; }
                _ = Task.Run(() => Handle(client));
            }
        }

        async Task Handle(TcpClient client)
        {
            using var _ = client;
            var stream = client.GetStream();
            try
            {
                var request = await Read(stream);
                sent.Enqueue(request);
                await Route(request, stream, client);
            }
            catch (Exception)
            {
            }
        }

        static async Task<Sent> Read(NetworkStream stream)
        {
            var head = new List<byte>();
            var one = new byte[1];
            while (!(head.Count >= 4 && head[^4] == '\r' && head[^3] == '\n' && head[^2] == '\r' && head[^1] == '\n'))
            {
                if (await stream.ReadAsync(one) == 0) throw new IOException("closed");
                head.Add(one[0]);
            }
            var lines = Encoding.ASCII.GetString([.. head]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            var parts = lines[0].Split(' ');
            var headers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines.Skip(1))
            {
                var colon = line.IndexOf(':');
                var name = line[..colon].Trim();
                if (!headers.TryGetValue(name, out var values)) headers[name] = values = [];
                values.Add(line[(colon + 1)..].Trim());
            }
            var length = headers.TryGetValue("content-length", out var l) ? int.Parse(l[0]) : 0;
            var body = new byte[length];
            await stream.ReadExactlyAsync(body);
            return new Sent(parts[0], parts[1], headers, body);
        }

        static Task Head(NetworkStream stream, int status, int? length)
        {
            var text = $"HTTP/1.1 {status} X\r\nConnection: close\r\n" + (length is { } n ? $"Content-Length: {n}\r\n" : "") + "\r\n";
            return stream.WriteAsync(Encoding.ASCII.GetBytes(text)).AsTask();
        }

        static async Task Whole(NetworkStream stream, int status, byte[] body)
        {
            await Head(stream, status, body.Length);
            await stream.WriteAsync(body);
        }

        static async Task Route(Sent request, NetworkStream stream, TcpClient client)
        {
            var path = request.Path.Split('?')[0];
            var query = request.Path.Contains('?') ? request.Path[(request.Path.IndexOf('?') + 1)..] : "";
            switch (path)
            {
                case "/echo":
                    var status = query.StartsWith("status=") ? int.Parse(query[7..]) : 200;
                    await Whole(stream, status, Encoding.UTF8.GetBytes($"ok:{request.Body.Length}"));
                    break;
                case "/hold":
                    await Task.Delay(1500);
                    await Whole(stream, 200, [1, 2]);
                    break;
                case "/stall-body":
                case "/error-stall":
                    await Head(stream, path == "/stall-body" ? 200 : 429, 20);
                    await stream.WriteAsync(Bytes(10));
                    await Task.Delay(1500);
                    await stream.WriteAsync(Bytes(10));
                    break;
                case "/broken":
                case "/error-broken":
                    await Head(stream, path == "/broken" ? 200 : 503, 100);
                    await stream.WriteAsync(Bytes(10));
                    await Task.Delay(100);
                    client.Client.LingerState = new LingerOption(true, 0);
                    client.Close();
                    break;
                case "/long-length":
                    await Whole(stream, 200, Bytes(2000));
                    break;
                case "/long-stream":
                    await Head(stream, 200, null);
                    for (var i = 0; i < 4; i++) await stream.WriteAsync(Bytes(500));
                    break;
                case "/exact":
                    await Head(stream, 200, null);
                    await stream.WriteAsync(Bytes(1000));
                    break;
                case "/error-long":
                    await Head(stream, 503, null);
                    await stream.WriteAsync(Bytes(2000));
                    break;
                case "/error-body":
                    await Whole(stream, 404, "nope"u8.ToArray());
                    break;
                default:
                    await Whole(stream, 500, []);
                    break;
            }
        }
    }

    /// <summary>One POST of the body, content-type then the headers (a later one wins), and any status is replied with its body.</summary>
    [Fact]
    public async Task AReplyOfAnyStatusIsReplied()
    {
        await using var server = new Server();
        var body = Bytes(300);
        var (outcome, _) = await Send(Request($"{server.Base}/echo?status=201", body, headers: [new("x-key", "k"), new("x-session", "s")]));
        Assert.Equal("replied 201 6", Text(outcome));
        Assert.Equal("ok:300", Encoding.UTF8.GetString(((SignatureTransportOutcome.Replied)outcome).Body.Span));
        var sent = server.Requests[^1];
        Assert.Equal("POST", sent.Method);
        Assert.Equal(body, sent.Body);
        Assert.Equal(["application/timestamp-query"], sent.Headers["content-type"]);
        Assert.Equal(["k"], sent.Headers["x-key"]);
        Assert.Equal(["s"], sent.Headers["x-session"]);

        // A request header named content-type is sent after it, and wins.
        await Send(Request(server.Base + "/echo", headers: [new("Content-Type", "application/json")]));
        Assert.Equal(["application/json"], server.Requests[^1].Headers["content-type"]);

        (outcome, _) = await Send(Request(server.Base + "/error-body"));
        Assert.Equal("replied 404 4", Text(outcome));
        Assert.Equal("nope", Encoding.UTF8.GetString(((SignatureTransportOutcome.Replied)outcome).Body.Span));
    }

    /// <summary>The deadline covers waiting for the reply and reading it; a status outside 2xx in hand at the deadline is that status with no body.</summary>
    [Fact]
    public async Task TheDeadlineCoversTheWholeExchange()
    {
        await using var server = new Server();
        var (held, heldTook) = await Send(Request(server.Base + "/hold", timeoutMs: 300));
        Assert.Equal("timedOut", Text(held));
        Assert.InRange(heldTook, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(1400));
        var (stalled, stalledTook) = await Send(Request(server.Base + "/stall-body", timeoutMs: 300));
        Assert.Equal("timedOut", Text(stalled));
        Assert.InRange(stalledTook, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(1400));
        var (errorStall, _) = await Send(Request(server.Base + "/error-stall", timeoutMs: 300));
        Assert.Equal("replied 429 0", Text(errorStall));
    }

    /// <summary>tooLarge by content-length or once more than the limit is read; exactly the limit is a reply; a long or broken body outside 2xx is its status with no body, and a broken 2xx body is failed.</summary>
    [Fact]
    public async Task MaxReplyBytesIsEnforced()
    {
        await using var server = new Server();
        Assert.Equal("tooLarge", Text((await Send(Request(server.Base + "/long-length", maxReplyBytes: 1000))).Outcome));
        Assert.Equal("tooLarge", Text((await Send(Request(server.Base + "/long-stream", maxReplyBytes: 1000))).Outcome));
        var exact = (await Send(Request(server.Base + "/exact", maxReplyBytes: 1000))).Outcome;
        Assert.Equal("replied 200 1000", Text(exact));
        Assert.Equal(Bytes(1000), ((SignatureTransportOutcome.Replied)exact).Body.ToArray());
        Assert.Equal("replied 503 0", Text((await Send(Request(server.Base + "/error-long", maxReplyBytes: 1000))).Outcome));
        Assert.Equal("replied 503 0", Text((await Send(Request(server.Base + "/error-broken", maxReplyBytes: 1000))).Outcome));
        Assert.StartsWith("failed ", Text((await Send(Request(server.Base + "/broken", maxReplyBytes: 1000))).Outcome));
    }

    [Fact]
    public async Task NothingListeningOrNoAddressIsFailed()
    {
        var closed = new Server();
        await closed.DisposeAsync();
        Assert.StartsWith("failed ", Text((await Send(Request($"http://127.0.0.1:{closed.Port}/api/timestamp"))).Outcome));
        Assert.StartsWith("failed ", Text((await Send(Request("not a url"))).Outcome));
        Assert.StartsWith("failed ", Text((await Send(Request("ftp://127.0.0.1/x"))).Outcome));
    }

    /// <summary>Through the generated completion, as the viewer calls it.</summary>
    [Fact]
    public async Task PostAnswersThroughTheGeneratedCompletion()
    {
        await using var server = new Server();
        var (viewer, _, signatures) = await SignatureTests.Opened("unsigned_sigfield", config: new SignatureConfig
        {
            TimestampAuthority = new SignatureTimestampAuthority(server.Base + "/error-body") { Retries = 0 },
        });
        await using var _v = viewer;
        viewer.SetSignatureTransport(new HttpClientSignatureTransport());
        var field = await SignatureTests.UnsignedField(signatures);
        // The authority answers 404: the engine refuses the timestamp, so the
        // signing fails, after one POST that reached the server.
        await Assert.ThrowsAsync<PdfException>(() => signatures.SignAsync(SignatureTests.SignInput(field)));
        Assert.Single(server.Requests, r => r.Path == "/error-body");
    }

    sealed class BrokenTransport(Func<Task<SignatureTransportOutcome>> post) : ISignatureTransport
    {
        public int Calls;
        public Task<SignatureTransportOutcome> PostAsync(SignatureTransportRequest request)
        {
            Interlocked.Increment(ref Calls);
            return post();
        }
    }

    [Fact]
    public async Task ATransportThatThrowsOrAnswersNothingFailsTheRequestOnce()
    {
        BrokenTransport[] transports =
        [
            new(() => throw new InvalidOperationException("no network")),
            new(() => null!),
            new(() => Task.FromResult<SignatureTransportOutcome>(null!)),
            new(() => Task.FromResult<SignatureTransportOutcome>(new SignatureTransportOutcome.Replied(70000, ReadOnlyMemory<byte>.Empty))),
        ];
        foreach (var transport in transports)
        {
            var (viewer, _, signatures) = await SignatureTests.Opened("unsigned_sigfield", config: new SignatureConfig
            {
                TimestampAuthority = new SignatureTimestampAuthority("https://tsa.example/") { Retries = 0 },
            });
            await using var _v = viewer;
            viewer.SetSignatureTransport(transport);
            var field = await SignatureTests.UnsignedField(signatures);
            await Assert.ThrowsAsync<PdfException>(() => signatures.SignAsync(SignatureTests.SignInput(field)));
            Assert.Equal(1, transport.Calls);
            Assert.Null(await signatures.GetPendingAsync());
        }
    }

    // Upstream's routes: skipped without EMBEDPDF_SIGNATURE_ROUTES.

    sealed record Material(byte[][] Chain, byte[] Key, byte[][] Anchors, byte[] TimestampCertificate)
    {
        public static async Task<Material> Load(string routes)
        {
            using var http = new HttpClient();
            var json = System.Text.Json.Nodes.JsonNode.Parse(await http.GetStringAsync(routes + "/material"))!;
            static byte[] Data(System.Text.Json.Nodes.JsonNode? node) => Convert.FromBase64String(node!.GetValue<string>());
            return new(
                [.. json["chain"]!.AsArray().Select(Data)], Data(json["key"]),
                [.. json["anchors"]!.AsArray().Select(Data)], Data(json["timestampCertificate"]));
        }

        public X509CertificateSigner Signer()
        {
            using var key = RSA.Create();
            key.ImportPkcs8PrivateKey(Key, out _);
            var leaf = X509CertificateLoader.LoadCertificate(Chain[0]).CopyWithPrivateKey(key);
            return new X509CertificateSigner(leaf, Chain.Skip(1).Select(c => X509CertificateLoader.LoadCertificate(c)));
        }
    }

    static async Task<string> Log(string routes)
    {
        using var http = new HttpClient();
        return await http.GetStringAsync(routes + "/log");
    }

    static async Task<int[]> Nonces(string routes) =>
        [.. System.Text.Json.Nodes.JsonNode.Parse(await Log(routes))!["nonces"]!.AsArray().Select(n => n!.GetValue<int>())];

    static async Task<(PdfViewer Viewer, SignatureCapability Signatures, FormFieldRef Field)> RouteViewer(SignatureConfig config, Material material)
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Annotation = "", Form = new FormConfig(), Signature = config });
        viewer.SetSigner(material.Signer());
        viewer.SetTrust(new TrustAnchors(material.Anchors.Select(a => (ReadOnlyMemory<byte>)a)));
        viewer.SetSignatureTransport(new HttpClientSignatureTransport());
        var document = await viewer.OpenAsync(Fixture.Data("unsigned_sigfield"));
        var signatures = await viewer.GetSignaturesAsync(document);
        await signatures.RefreshAsync();
        return (viewer, signatures, await SignatureTests.UnsignedField(signatures));
    }

    [SkippableRouteFact]
    public async Task AGrantedTimestampThroughTheTimestampRoute()
    {
        var routes = Routes!;
        var material = await Material.Load(routes);
        var authority = new SignatureTimestampAuthority(routes + "/api/timestamp") { Headers = [new("x-session", "s")] };
        var (viewer, signatures, field) = await RouteViewer(new SignatureConfig(), material);
        await using var _v = viewer;
        var before = await Nonces(routes);
        Assert.False((await signatures.GetSettingsAsync()).HasTimestampAuthority);
        var done = await signatures.SignAsync(SignatureTests.SignInput(field) with { TimestampAuthority = new SignatureCallAuthority.Http(authority) });
        Assert.True(done.Signature.Signed);
        var verdict = await signatures.ValidateFieldAsync(field);
        var stamp = verdict.Timestamp!;
        Assert.Equal(SignatureCryptography.Valid, stamp.Cryptography);
        Assert.Equal("EmbedPDF test timestamp authority", stamp.AuthorityName);
        Assert.Equal(material.TimestampCertificate, stamp.AuthorityCertificate?.ToArray());
        Assert.Equal(SignatureSummary.Valid, verdict.Summary);
        Assert.Equal([8], (await Nonces(routes)).Skip(before.Length));
    }

    [SkippableRouteFact]
    public async Task ALongTermFillThroughTheRevocationRoute()
    {
        var routes = Routes!;
        var material = await Material.Load(routes);
        var revocation = new SignatureRevocationRoute(routes + "/api/revocation");
        var (viewer, signatures, field) = await RouteViewer(new SignatureConfig
        {
            TimestampAuthority = new SignatureTimestampAuthority(routes + "/api/timestamp"), Revocation = revocation,
        }, material);
        await using var _v = viewer;
        var settings = await signatures.GetSettingsAsync();
        Assert.True(settings.HasTimestampAuthority && settings.HasRevocation && !settings.DocumentTimestamp);
        Assert.True(await signatures.CanAddValidationInfoAsync());
        var done = await signatures.SignAsync(SignatureTests.SignInput(field));
        Assert.Null(done.ValidationInfoError);
        Assert.True(done.ValidationInfo!.Added.OcspResponses > 0 && done.ValidationInfo.DocumentTimestamp is null);
        Assert.Contains("POST http://test-authority.example/ocsp", await Log(routes));
        var verdict = Assert.Single(await signatures.ValidateAsync(new SignatureValidateRequest { ValidationTime = new SignatureRequestedTime.EarliestTimestamp() }));
        Assert.Equal(SignatureSummary.Valid, verdict.Summary);
        Assert.Equal(SignatureRevocationStatus.Good, verdict.Revocation);
        Assert.True(verdict.LongTerm.Enabled && verdict.LongTerm.Missing.Count == 0 && verdict.LongTerm.DocumentTimestampedAt is null);

        await signatures.UpdateSettingsAsync(new SignatureSettingsPatch { ClearRevocation = true });
        Assert.False((await signatures.GetSettingsAsync()).HasRevocation);
        Assert.False(await signatures.CanAddValidationInfoAsync());
        Assert.Equal(PdfErrorCode.InvalidInput, (await Assert.ThrowsAsync<PdfException>(() => signatures.AddValidationInfoAsync(field))).Code);
        await signatures.UpdateSettingsAsync(new SignatureSettingsPatch { Revocation = revocation });
        Assert.True(await signatures.CanAddValidationInfoAsync());
        Assert.Equal(new SignatureValidationInfoCounts(0, 0, 0), (await signatures.AddValidationInfoAsync(field)).Added);
    }

    [SkippableRouteFact]
    public async Task ACallWithoutAnAuthoritySendsNoQuery()
    {
        var routes = Routes!;
        var material = await Material.Load(routes);
        var (viewer, signatures, field) = await RouteViewer(new SignatureConfig
        {
            TimestampAuthority = new SignatureTimestampAuthority(routes + "/api/timestamp"),
        }, material);
        await using var _v = viewer;
        static string? Queries(string log) => System.Text.RegularExpressions.Regex.Match(log, "\"timestampQueries\":(\\d+)") is { Success: true } m ? m.Value : null;
        var before = Queries(await Log(routes));
        await signatures.SignAsync(SignatureTests.SignInput(field) with { TimestampAuthority = new SignatureCallAuthority.Without() });
        var verdict = await signatures.ValidateFieldAsync(field, new SignatureValidateRequest { ValidationTime = new SignatureRequestedTime.Now() });
        Assert.Null(verdict.Timestamp);
        Assert.Equal(before, Queries(await Log(routes)));
    }

    sealed class SkippableRouteFactAttribute : FactAttribute
    {
        public SkippableRouteFactAttribute()
        {
            if (Routes is null) Skip = "EMBEDPDF_SIGNATURE_ROUTES is not set";
        }
    }
}
