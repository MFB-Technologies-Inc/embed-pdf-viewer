using System.Runtime.ExceptionServices;

namespace EmbedPDF.Models;

/// <summary>
/// The home context of a model made where there is no
/// <see cref="SynchronizationContext"/>, as in a console app: posted work
/// runs one item at a time, in order, on the thread pool, with this context
/// current while it runs. Notifications then arrive on pool threads, never
/// two at once.
/// </summary>
internal sealed class SerialSynchronizationContext : SynchronizationContext
{
    readonly object gate = new();
    readonly Queue<(SendOrPostCallback Callback, object? State)> queue = new();
    bool draining;

    public override void Post(SendOrPostCallback d, object? state)
    {
        lock (gate)
        {
            queue.Enqueue((d, state));
            if (draining) return;
            draining = true;
        }
        ThreadPool.UnsafeQueueUserWorkItem(static self => self.Drain(), this, preferLocal: false);
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (ReferenceEquals(Current, this))
        {
            d(state);
            return;
        }
        using var done = new ManualResetEventSlim();
        ExceptionDispatchInfo? failure = null;
        Post(_ =>
        {
            try { d(state); }
            catch (Exception e) { failure = ExceptionDispatchInfo.Capture(e); }
            finally { done.Set(); }
        }, null);
        done.Wait();
        failure?.Throw();
    }

    public override SynchronizationContext CreateCopy() => this;

    void Drain()
    {
        var previous = Current;
        SetSynchronizationContext(this);
        try
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State) item;
                lock (gate)
                {
                    if (queue.Count == 0)
                    {
                        draining = false;
                        return;
                    }
                    item = queue.Dequeue();
                }
                try
                {
                    item.Callback(item.State);
                }
                catch
                {
                    // The rest of the queue still runs, on a fresh drain.
                    ThreadPool.UnsafeQueueUserWorkItem(static self => self.Drain(), this, preferLocal: false);
                    throw;
                }
            }
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }
}
