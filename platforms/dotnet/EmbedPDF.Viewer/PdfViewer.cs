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
public sealed partial class PdfViewer : IAsyncDisposable, IDisposable
{
    readonly GH.ViewerHost host;
    readonly ILogger logger;
    readonly BroadcastHub<long> revisions = new(1);
    readonly BroadcastHub<PdfException> reportedErrors = new(16);
    readonly BroadcastHub<DocumentEvent> documentEvents = new(null);
    readonly Lock gate = new();
    readonly HashSet<GF.CancelToken> cancellable = [];
    readonly TaskCompletionSource drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    int inFlight;
    bool closed;
    Task? disposal;

    /// <summary>This viewer's identity on its kernel thread; see <see cref="KernelCallback"/>.</summary>
    internal KernelMark Mark { get; } = new();

    /// <summary>Where adapters log what app callbacks throw.</summary>
    internal ILogger Logger => logger;

    /// <summary>Creates a viewer that is not started yet. Call <see cref="StartAsync"/> before anything else.</summary>
    /// <param name="options">What to install; <see langword="null"/> installs no plugin.</param>
    /// <exception cref="NativeRuntimeNotFoundException">The native runtime for this platform could not be loaded.</exception>
    /// <exception cref="PdfException">The engine refused the options.</exception>
    public PdfViewer(PdfViewerOptions? options = null)
    {
        NativeRuntime.EnsureLoaded();
        options ??= new PdfViewerOptions();
        logger = options.Logger;
        using var config = options.ToFfi(Mark);
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
    /// returns. Each subscription keeps every event until it is read, so a
    /// reader that stops reading holds them in memory; it ends when cancelled
    /// or when the viewer is disposed. Enumerate it once.
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
    /// <remarks>
    /// Destroying the kernel runs on the kernel thread. Called from a callback
    /// the kernel makes on that thread (an interaction handler, a feedback
    /// provider, a script engine, or any other adapter it calls on its own
    /// thread), it starts disposal, which is safe, and returns a task that can
    /// be awaited but not blocked on: <c>GetAwaiter().GetResult()</c> before it
    /// finishes throws <see cref="InvalidOperationException"/>, since the
    /// blocked thread is the one disposal needs. Never block on
    /// <c>AsTask()</c> there either; that wait cannot be detected, and it would
    /// stop both for good. The task returned there stays unblockable until
    /// disposal completes, whichever thread later holds it: awaiting it, or
    /// <c>AsTask()</c> off the kernel thread, is fine, but
    /// <c>GetAwaiter().GetResult()</c> on it before then throws on any thread.
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var existing = Interlocked.CompareExchange(ref disposal, completion.Task, null);
        if (existing is null) _ = DisposeCore(completion);
        var task = existing ?? completion.Task;
        if (!task.IsCompleted && KernelCallback.IsInside(Mark))
        {
            return new ValueTask(new KernelCallback.GuardedDisposal(task), 0);
        }
        return new ValueTask(task);
    }

    /// <summary>
    /// <see cref="DisposeAsync"/> for a caller that can't await, such as a
    /// service container disposed synchronously. It blocks until disposal
    /// ends; the disposal runs on the thread pool, so blocking a thread that
    /// owns a synchronization context does not deadlock it.
    /// </summary>
    /// <remarks>
    /// Disposal needs the kernel thread, so blocking that thread from a
    /// callback the kernel makes on it would deadlock. See
    /// <see cref="DisposeAsync"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Called from a callback the kernel makes on its own thread, where
    /// waiting for disposal would hold the thread it needs. Call
    /// <see cref="DisposeAsync"/> there without waiting.
    /// </exception>
    public void Dispose()
    {
        if (KernelCallback.IsInside(Mark)) throw KernelCallback.DisposalWait();
        Task.Run(() => DisposeAsync().AsTask()).GetAwaiter().GetResult();
    }

    async Task DisposeCore(TaskCompletionSource completion)
    {
        try
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
            var release = ReleaseHost;
            await Task.Run(() => ReleaseLogged(host, release, logger)).ConfigureAwait(false);
            await Settle(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            logger.LogError(error, "Disposing the viewer failed.");
        }
        finally
        {
            Finish();
            GC.SuppressFinalize(this);
            completion.TrySetResult();
        }
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
        EndStreams();
    }

    /// <summary>How the host is released; a seam for tests.</summary>
    internal Action<GH.ViewerHost> ReleaseHost { get; set; } = Release;

    /// <summary>Runs <paramref name="release"/>, logging rather than throwing: disposal and the finalizer path must always finish.</summary>
    static void ReleaseLogged(GH.ViewerHost host, Action<GH.ViewerHost> release, ILogger logger)
    {
        try
        {
            release(host);
        }
        catch (Exception error)
        {
            logger.LogError(error, "Releasing the native viewer failed.");
        }
    }

    internal static void Release(GH.ViewerHost host)
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

    static int abandonedReleases;

    /// <summary>How many abandoned viewers the finalizer path has released, for tests.</summary>
    internal static int AbandonedReleases => Volatile.Read(ref abandonedReleases);

    /// <summary>Releases an abandoned viewer on the thread pool, so the finalizer thread never waits on the kernel.</summary>
    ~PdfViewer()
    {
        // A constructor that threw leaves no host to release.
        if (Volatile.Read(ref disposal) is not null || host is null) return;
        var (abandoned, release, log, r, e, d, p) = (host, ReleaseHost, logger, revisions, reportedErrors, documentEvents, streams);
        ThreadPool.UnsafeQueueUserWorkItem(_ =>
        {
            try
            {
                ReleaseLogged(abandoned, release, log);
                r.Complete();
                e.Complete();
                d.Complete();
                p.Close();
            }
            finally
            {
                Interlocked.Increment(ref abandonedReleases);
            }
        }, null);
    }

    void ThrowIfClosed()
    {
        lock (gate)
        {
            if (closed) throw PdfException.Closed();
        }
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

    /// <summary>
    /// Maps a generated call's error. A cancellation is the caller's when
    /// their token fired; one that disposal caused is <c>instance-closed</c>.
    /// </summary>
    Exception Map(Exception error, CancellationToken cancellationToken)
    {
        var mapped = PdfException.From(error);
        if (mapped.Code != PdfErrorCode.OperationCancelled) return mapped;
        if (cancellationToken.IsCancellationRequested) return new OperationCanceledException(mapped.Detail, mapped, cancellationToken);
        bool closing;
        lock (gate) closing = closed;
        return closing ? PdfException.Closed(mapped) : mapped;
    }

    internal GH.ViewerHost Host => host;

    internal int? DocumentEventCapacity => documentEvents.Capacity;

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
