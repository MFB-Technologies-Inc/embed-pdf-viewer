using Microsoft.Extensions.Logging;
using GF = uniffi.epdf_ffi;
using GH = uniffi.epdf_viewer_host;

namespace EmbedPDF;

/// <summary>
/// A viewer: one kernel, its documents and the plugins its
/// <see cref="PdfViewerOptions"/> install.
/// </summary>
/// <remarks>
/// <para>
/// Create it with <see cref="CreateAsync"/>, or construct it and call
/// <see cref="StartAsync"/>, then open documents. Every call is safe from any
/// thread; the kernel runs them one at a time on its own thread.
/// </para>
/// <para>
/// Every async member takes a <see cref="CancellationToken"/>. A token that
/// has already fired throws <see cref="OperationCanceledException"/> before
/// the call is posted. A member whose engine call can be cancelled (an open
/// from a URL or a layer, a save) cancels it when the token fires and then
/// throws <see cref="OperationCanceledException"/>; the others run to
/// completion once posted, as the engine has no way to abandon them.
/// </para>
/// <para>
/// <see cref="DisposeAsync"/> ends calls in flight, closes every document,
/// finishes every stream and releases the kernel. After it, every call throws
/// <see cref="PdfException"/> with <see cref="PdfErrorCode.InstanceClosed"/>.
/// A viewer dropped without disposal is released on a thread-pool thread
/// after the garbage collector finds it.
/// </para>
/// </remarks>
public sealed partial class PdfViewer : IAsyncDisposable
{
    readonly GH.ViewerHost host;
    readonly ILogger logger;
    readonly BroadcastHub<long> revisions = new(1);
    readonly BroadcastHub<PdfException> reportedErrors = new(16);
    readonly BroadcastHub<DocumentEvent> documentEvents = new(1024);
    readonly Lock gate = new();
    readonly HashSet<GF.CancelToken> cancellable = [];
    readonly TaskCompletionSource drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    int inFlight;
    bool closed;
    Task? disposal;

    /// <summary>Creates a viewer that is not started yet. Call <see cref="StartAsync"/> before anything else.</summary>
    /// <param name="options">What to install; <see langword="null"/> installs no plugin.</param>
    /// <exception cref="NativeRuntimeNotFoundException">The native runtime for this platform could not be loaded.</exception>
    /// <exception cref="PdfException">The engine refused the options.</exception>
    public PdfViewer(PdfViewerOptions? options = null)
    {
        NativeRuntime.EnsureLoaded();
        options ??= new PdfViewerOptions();
        logger = options.Logger;
        using var config = options.ToFfi();
        try
        {
            host = GH.ViewerHost.NewWithFetcher(config, new StateRelay(revisions, reportedErrors, logger),
                new FetcherAdapter(options.DocumentFetcher ?? new HttpClientDocumentFetcher()));
        }
        catch (Exception error)
        {
            GC.SuppressFinalize(this);
            throw PdfException.From(error);
        }
        // The generated finalizer would release the host on the finalizer
        // thread, and releasing it joins the kernel thread. This viewer's
        // finalizer hands that to the thread pool instead.
        GC.SuppressFinalize(host);
    }

    /// <summary>Creates a viewer and starts it.</summary>
    /// <inheritdoc cref="PdfViewer(PdfViewerOptions?)"/>
    public static async Task<PdfViewer> CreateAsync(PdfViewerOptions? options = null, CancellationToken cancellationToken = default)
    {
        NativeRuntime.EnsureLoaded();
        var viewer = new PdfViewer(options);
        try
        {
            await viewer.StartAsync(cancellationToken).ConfigureAwait(false);
            return viewer;
        }
        catch
        {
            await viewer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Starts the kernel. Call once, before any other member.</summary>
    public Task StartAsync(CancellationToken cancellationToken = default) => CallAsync(h => h.Start(), cancellationToken);

    /// <summary>
    /// A fresh subscription to the viewer's state revisions, installed before
    /// this returns. Read an initial snapshot after subscribing; later
    /// revisions invalidate it. Revisions coalesce (a slow reader sees the
    /// newest), cancellation ends this subscription only, and the stream ends
    /// when the viewer is disposed. Enumerate it once.
    /// </summary>
    public IAsyncEnumerable<long> ObserveStateRevisions(CancellationToken cancellationToken = default) =>
        revisions.Subscribe(cancellationToken);

    /// <summary>
    /// A fresh subscription to the errors the kernel reports rather than
    /// throws: a listener that failed, a handler that threw (<c>HostError</c>).
    /// It sees only errors reported after this returns, so subscribe before
    /// the call that might report one. Each subscription keeps the newest 16,
    /// cancellation ends this subscription only, and the stream ends when the
    /// viewer is disposed. Enumerate it once.
    /// </summary>
    public IAsyncEnumerable<PdfException> ObserveReportedErrors(CancellationToken cancellationToken = default) =>
        reportedErrors.Subscribe(cancellationToken);

    /// <summary>
    /// A fresh subscription to document events, installed before this
    /// returns. Each subscription buffers up to 1,024 events and drops the
    /// oldest past that; it ends when cancelled or when the viewer is
    /// disposed. Enumerate it once.
    /// </summary>
    public IAsyncEnumerable<DocumentEvent> ObserveDocumentEvents(CancellationToken cancellationToken = default)
    {
        var listener = new DocumentListener();
        var subscription = documentEvents.Subscribe(cancellationToken, listener.End);
        listener.Writer = subscription.Writer;
        bool live;
        lock (gate) live = !closed;
        if (live)
        {
            try
            {
                listener.Attach(host.SubscribeDocuments(listener));
            }
            catch (Exception error) when (error is ObjectDisposedException or GF.ViewerException)
            {
                subscription.End();
            }
        }
        else
        {
            subscription.End();
        }
        return subscription;
    }

    /// <summary>
    /// Ends calls in flight, destroys the kernel, finishes every stream and
    /// releases the native viewer. Every call returns the same task.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var existing = Interlocked.CompareExchange(ref disposal, completion.Task, null);
        if (existing is not null) return new ValueTask(existing);
        _ = Dispose(completion);
        return new ValueTask(completion.Task);
    }

    async Task Dispose(TaskCompletionSource completion)
    {
        GF.CancelToken[] tokens;
        lock (gate)
        {
            closed = true;
            tokens = [.. cancellable];
            if (inFlight == 0) drained.TrySetResult();
        }
        foreach (var token in tokens)
        {
            Cancel(token);
        }
        try
        {
            await host.DestroyViewer().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            logger.LogDebug(error, "Destroying the viewer failed; shutting it down anyway.");
        }
        await Settle(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        // Shutdown joins the kernel thread: it can wait for a PDFium call in
        // progress, so it runs on the thread pool, never on the caller's.
        await Task.Run(() => Release(host)).ConfigureAwait(false);
        await Settle(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Finish();
        GC.SuppressFinalize(this);
        completion.SetResult();
    }

    async Task Settle(TimeSpan limit)
    {
        try
        {
            await drained.Task.WaitAsync(limit).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            logger.LogWarning("Calls were still in flight {Limit} after the viewer began to close.", limit);
        }
    }

    void Finish()
    {
        revisions.Complete();
        reportedErrors.Complete();
        documentEvents.Complete();
    }

    static void Release(GH.ViewerHost host)
    {
        try
        {
            host.Shutdown();
        }
        catch (Exception error) when (error is ObjectDisposedException or GF.ViewerException)
        {
            // Already shut down.
        }
        host.Dispose();
    }

    /// <summary>Releases an abandoned viewer on the thread pool, so the finalizer thread never waits on the kernel.</summary>
    ~PdfViewer()
    {
        // A constructor that threw leaves no host to release.
        if (Volatile.Read(ref disposal) is not null || host is null) return;
        var (abandoned, r, e, d) = (host, revisions, reportedErrors, documentEvents);
        ThreadPool.UnsafeQueueUserWorkItem(_ =>
        {
            Release(abandoned);
            r.Complete();
            e.Complete();
            d.Complete();
        }, null);
    }

    void Enter(GF.CancelToken? token)
    {
        lock (gate)
        {
            if (closed) throw PdfException.Closed();
            inFlight++;
            if (token is not null) cancellable.Add(token);
        }
    }

    void Exit(GF.CancelToken? token)
    {
        lock (gate)
        {
            if (token is not null) cancellable.Remove(token);
            if (--inFlight == 0 && closed) drained.TrySetResult();
        }
    }

    static void Cancel(GF.CancelToken token)
    {
        try
        {
            token.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The call finished first.
        }
    }

    /// <summary>Runs a generated call that takes no cancel token, mapping its errors.</summary>
    internal async Task<T> CallAsync<T>(Func<GH.ViewerHost, Task<T>> call, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Enter(null);
        try
        {
            return await call(host).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            throw Map(error, cancellationToken);
        }
        finally
        {
            Exit(null);
        }
    }

    internal Task CallAsync(Func<GH.ViewerHost, Task> call, CancellationToken cancellationToken) =>
        CallAsync(async h => { await call(h).ConfigureAwait(false); return true; }, cancellationToken);

    /// <summary>
    /// Runs a generated call that takes a cancel token: the caller's token
    /// and disposal both cancel it, which aborts the call on the kernel thread.
    /// </summary>
    internal async Task<T> CallCancellableAsync<T>(Func<GH.ViewerHost, GF.CancelToken, Task<T>> call, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var token = new GF.CancelToken();
        Enter(token);
        try
        {
            using var registration = cancellationToken.Register(static state => Cancel((GF.CancelToken)state!), token);
            return await call(host, token).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            throw Map(error, cancellationToken);
        }
        finally
        {
            Exit(token);
        }
    }

    static Exception Map(Exception error, CancellationToken cancellationToken)
    {
        var mapped = PdfException.From(error);
        return mapped.Code == PdfErrorCode.OperationCancelled && cancellationToken.IsCancellationRequested
            ? new OperationCanceledException(mapped.Detail, mapped, cancellationToken)
            : mapped;
    }

    internal GH.ViewerHost Host => host;

    /// <summary>Forwards the kernel's callbacks to the hubs. It holds no viewer, so an abandoned viewer can be collected.</summary>
    sealed class StateRelay(BroadcastHub<long> revisions, BroadcastHub<PdfException> errors, ILogger logger) : GH.ViewerDelegate
    {
        public void StateChanged(ulong revision) => revisions.Publish(revision > long.MaxValue ? long.MaxValue : (long)revision);

        public void Report(GF.ViewerException error)
        {
            var reported = PdfException.From(error);
            logger.LogWarning("The kernel reported {Name} ({Code}): {Detail}", reported.Name, reported.Code.Value, reported.Detail);
            errors.Publish(reported);
        }
    }

    /// <summary>One document-event subscription's kernel listener.</summary>
    sealed class DocumentListener : GH.DocumentEventListener
    {
        readonly Lock gate = new();
        GF.Subscription? subscription;
        bool ended;

        public System.Threading.Channels.ChannelWriter<DocumentEvent>? Writer { get; set; }

        public void OnEvent(GH.DocumentEvent @event) => Writer?.TryWrite(Ffi.ToPublic(@event));

        public void Attach(GF.Subscription attached)
        {
            lock (gate)
            {
                if (!ended)
                {
                    subscription = attached;
                    return;
                }
            }
            Stop(attached);
        }

        public void End()
        {
            GF.Subscription? attached;
            lock (gate)
            {
                ended = true;
                attached = subscription;
                subscription = null;
            }
            if (attached is not null) Stop(attached);
        }

        static void Stop(GF.Subscription attached)
        {
            try
            {
                attached.Cancel();
            }
            catch (Exception error) when (error is ObjectDisposedException or GF.ViewerException)
            {
                // The viewer shut down first.
            }
            attached.Dispose();
        }
    }
}
