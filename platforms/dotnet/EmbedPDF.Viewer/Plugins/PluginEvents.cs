using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using GF = uniffi.epdf_ffi;

namespace EmbedPDF;

/// <summary>
/// The kernel side of one plugin event stream. Each plugin subclasses it once
/// to implement its generated listener, forwarding <c>OnEvent</c> to
/// <see cref="Deliver"/> and <c>OnEnd</c> to <see cref="Finish"/>.
/// </summary>
/// <remarks>
/// The kernel calls it on its own thread. <see cref="Deliver"/> never blocks:
/// the stream is unbounded, as Swift's <c>AsyncStream</c> is, so a reader
/// that stops reading holds every later event in memory.
/// </remarks>
internal abstract class EventRelay<TFfi, TPublic>(Func<TFfi, TPublic> map)
{
    readonly Lock gate = new();
    ChannelWriter<TPublic>? writer;
    Action? end;
    ILogger? logger;
    GF.ISubscription? native;
    bool stopped;

    internal void Bind(ChannelWriter<TPublic> writer, Action end, ILogger logger)
    {
        this.writer = writer;
        this.end = end;
        this.logger = logger;
    }

    /// <summary>Maps and offers one event. A mapping that throws drops the event and logs it.</summary>
    protected void Deliver(TFfi @event)
    {
        TPublic mapped;
        try
        {
            mapped = map(@event);
        }
        catch (Exception error)
        {
            logger?.LogWarning(error, "Dropped a plugin event that did not map.");
            return;
        }
        writer?.TryWrite(mapped);
    }

    /// <summary>The kernel ended the subscription: the document closed or the viewer shut down.</summary>
    protected void Finish() => end?.Invoke();

    internal void Attach(GF.ISubscription subscription)
    {
        lock (gate)
        {
            if (!stopped)
            {
                native = subscription;
                return;
            }
        }
        Cancel(subscription);
    }

    /// <summary>Cancels the kernel subscription once, whichever side ended first.</summary>
    internal void Stop()
    {
        GF.ISubscription? attached;
        lock (gate)
        {
            stopped = true;
            attached = native;
            native = null;
        }
        if (attached is not null) Cancel(attached);
    }

    internal int CancelCountForTests;

    void Cancel(GF.ISubscription subscription)
    {
        Interlocked.Increment(ref CancelCountForTests);
        try
        {
            subscription.Cancel();
        }
        catch (Exception error) when (error is ObjectDisposedException or GF.ViewerException)
        {
            // The viewer shut down first.
        }
        (subscription as IDisposable)?.Dispose();
    }
}

public sealed partial class PdfViewer
{
    readonly Dictionary<object, Action> streams = [];

    /// <summary>
    /// A plugin event stream, subscribed before this returns. It ends when the
    /// kernel ends it (<c>OnEnd</c>), when <paramref name="cancellationToken"/>
    /// fires, when the enumerator is disposed, or when the viewer is disposed;
    /// a stream opened after disposal ends at once.
    /// </summary>
    internal IAsyncEnumerable<TPublic> Stream<TFfi, TPublic>(
        EventRelay<TFfi, TPublic> relay, Func<EventRelay<TFfi, TPublic>, GF.ISubscription> subscribe, CancellationToken cancellationToken)
    {
        var key = new object();
        var hub = new BroadcastHub<TPublic>(null);
        var subscription = hub.Subscribe(cancellationToken, () =>
        {
            lock (gate) streams.Remove(key);
            relay.Stop();
        });
        relay.Bind(subscription.Writer, subscription.End, logger);
        bool live;
        lock (gate)
        {
            live = !closed && !subscription.HasEnded;
            if (live) streams[key] = subscription.End;
        }
        if (!live)
        {
            subscription.End();
            return subscription;
        }
        try
        {
            relay.Attach(subscribe(relay));
        }
        catch (Exception error)
        {
            logger.LogDebug(error, "A plugin subscription failed; its stream ends at once.");
            subscription.End();
        }
        return subscription;
    }

    /// <summary>Ends every plugin stream; disposal calls it.</summary>
    void EndStreams()
    {
        Action[] ends;
        lock (gate)
        {
            ends = [.. streams.Values];
            streams.Clear();
        }
        foreach (var end in ends) end();
    }

    internal int StreamCountForTests
    {
        get { lock (gate) return streams.Count; }
    }

    /// <summary>Runs a capability call that takes no cancel token.</summary>
    internal Task<T> Run<T>(Func<Task<T>> call, CancellationToken cancellationToken) => CallAsync(_ => call(), cancellationToken);

    internal Task Run(Func<Task> call, CancellationToken cancellationToken) => CallAsync(_ => call(), cancellationToken);

    /// <summary>Runs a capability call whose engine call takes a cancel token.</summary>
    internal Task<T> RunCancellable<T>(Func<GF.CancelToken, Task<T>> call, CancellationToken cancellationToken) =>
        CallCancellableAsync((_, token) => call(token), cancellationToken);

    internal Task RunCancellable(Func<GF.CancelToken, Task> call, CancellationToken cancellationToken) =>
        CallCancellableAsync(async (_, token) => { await call(token).ConfigureAwait(false); return true; }, cancellationToken);
}
