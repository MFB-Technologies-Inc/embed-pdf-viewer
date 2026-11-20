using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// An HTTP server on a loopback port. Each request goes to the handler, which
/// may answer at once, answer later, or never.
/// </summary>
sealed class Loopback : IAsyncDisposable
{
    readonly HttpListener listener = new();
    readonly Func<HttpListenerContext, Task> handler;
    readonly Task loop;
    readonly ConcurrentQueue<string> requests = new();

    Loopback(Func<HttpListenerContext, Task> handler)
    {
        this.handler = handler;
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        Port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        listener.Start();
        loop = Task.Run(Serve);
    }

    public int Port { get; }

    public IReadOnlyCollection<string> Requests => requests;

    public static Loopback Start(Func<HttpListenerContext, Task> handler) => new(handler);

    /// <summary>Answers every request with <paramref name="status"/> and <paramref name="body"/>.</summary>
    public static Loopback Answering(int status, byte[] body) => new(context => Reply(context, status, body));

    public Uri Url(string path) => new($"http://127.0.0.1:{Port}/{path}");

    public static async Task Reply(HttpListenerContext context, int status, byte[] body)
    {
        try
        {
            context.Response.StatusCode = status;
            context.Response.ContentLength64 = body.Length;
            await context.Response.OutputStream.WriteAsync(body);
            context.Response.Close();
        }
        catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or IOException or InvalidOperationException)
        {
            // The client went away first.
        }
    }

    public async Task WaitForRequests(int count)
    {
        for (var tries = 0; tries < 500 && requests.Count < count; tries++)
        {
            await Task.Delay(10);
        }
    }

    async Task Serve()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }
            requests.Enqueue(context.Request.Url!.AbsolutePath);
            _ = Task.Run(() => handler(context));
        }
    }

    public async ValueTask DisposeAsync()
    {
        listener.Stop();
        listener.Close();
        await loop;
    }
}
