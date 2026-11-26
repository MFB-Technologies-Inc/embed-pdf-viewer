namespace EmbedPDF.Models;

/// <summary>
/// The port of Swift's <c>CancelOnRelease</c> and its weakly held loop: a
/// model's own event loop that holds the model only while it handles one
/// event, so the loop never keeps the model alive. Cancelling it, as the
/// model's <c>Dispose</c> or finalizer does, ends the loop and its
/// subscription.
/// </summary>
internal sealed class WeakObservation
{
    readonly CancellationTokenSource cancel = new();

    WeakObservation()
    {
    }

    /// <summary>Finishes when the stream ends or the observation is cancelled.</summary>
    public Task Task { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Subscribes now, on the calling thread, so an event sent right after
    /// this returns is delivered, and handles each event on the home context.
    /// <paramref name="handle"/> must not capture the model.
    /// </summary>
    public static WeakObservation Start<TModel, TEvent>(
        TModel model,
        ModelContext home,
        Func<CancellationToken, IAsyncEnumerable<TEvent>> subscribe,
        Action<TModel, TEvent> handle)
        where TModel : class
    {
        var observation = new WeakObservation();
        var token = observation.cancel.Token;
        var events = subscribe(token);
        var weak = new WeakReference<TModel>(model);
        observation.Task = home.RunAsync(() => Loop(weak, events, handle, token));
        return observation;
    }

    /// <summary>Ends the loop. Safe from a finalizer and more than once.</summary>
    public void Cancel()
    {
        try
        {
            ThreadPool.UnsafeQueueUserWorkItem(static source =>
            {
                try { source.Cancel(); }
                catch (ObjectDisposedException) { }
            }, cancel, preferLocal: false);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    static async Task Loop<TModel, TEvent>(
        WeakReference<TModel> weak, IAsyncEnumerable<TEvent> events, Action<TModel, TEvent> handle, CancellationToken token)
        where TModel : class
    {
        try
        {
            await foreach (var item in events.WithCancellation(token))
            {
                if (token.IsCancellationRequested || !Deliver(weak, item, handle)) return;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    /// <summary>Not async, so the strong reference never outlives the call.</summary>
    static bool Deliver<TModel, TEvent>(WeakReference<TModel> weak, TEvent item, Action<TModel, TEvent> handle)
        where TModel : class
    {
        if (!weak.TryGetTarget(out var model)) return false;
        handle(model, item);
        return true;
    }
}
