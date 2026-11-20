using System.Threading.Channels;

namespace EmbedPDF;

/// <summary>
/// Fans one producer out to any number of consumers. Each <see cref="Subscribe"/>
/// call is its own subscription with its own bounded channel, installed before
/// the call returns, so one consumer's cancellation or slowness never affects
/// another. A subscription sees only values published after it was installed.
/// </summary>
/// <remarks>
/// <see cref="Publish"/> runs on whatever thread the kernel calls back on and
/// never blocks: a full channel drops its oldest value. With capacity 1 a slow
/// consumer keeps only the newest value, which is how state revisions coalesce.
/// </remarks>
internal sealed class BroadcastHub<T>(int capacity)
{
    readonly Lock gate = new();
    readonly HashSet<Subscription<T>> subscribers = [];
    bool completed;

    internal int SubscriberCountForTests
    {
        get { lock (gate) return subscribers.Count; }
    }

    /// <summary>A fresh subscription, installed before this returns.</summary>
    public IAsyncEnumerable<T> Subscribe(CancellationToken cancellationToken = default) => Subscribe(cancellationToken, null);

    /// <summary>
    /// A fresh subscription whose <paramref name="onEnd"/> runs once when it
    /// ends: its enumerator is disposed, its token fires, or the hub completes.
    /// </summary>
    internal Subscription<T> Subscribe(CancellationToken cancellationToken, Action? onEnd)
    {
        var subscription = new Subscription<T>(this, capacity, onEnd);
        bool live;
        lock (gate)
        {
            live = !completed;
            if (live) subscribers.Add(subscription);
        }
        if (live)
        {
            subscription.Watch(cancellationToken);
        }
        else
        {
            subscription.End();
        }
        return subscription;
    }

    /// <summary>Offers <paramref name="value"/> to every subscriber without blocking.</summary>
    public void Publish(T value)
    {
        Subscription<T>[] snapshot;
        lock (gate) snapshot = [.. subscribers];
        foreach (var subscriber in snapshot)
        {
            subscriber.Writer.TryWrite(value);
        }
    }

    /// <summary>Ends every subscription after it drains; later subscriptions end at once.</summary>
    public void Complete()
    {
        Subscription<T>[] snapshot;
        lock (gate)
        {
            completed = true;
            snapshot = [.. subscribers];
            subscribers.Clear();
        }
        foreach (var subscriber in snapshot)
        {
            subscriber.End();
        }
    }

    internal void Remove(Subscription<T> subscription)
    {
        lock (gate) subscribers.Remove(subscription);
    }
}

/// <summary>
/// One consumer's stream. It is enumerable once; cancellation of either the
/// subscribing token or the enumerator's token ends it with
/// <see cref="OperationCanceledException"/>.
/// </summary>
internal sealed class Subscription<T> : IAsyncEnumerable<T>
{
    readonly BroadcastHub<T> hub;
    readonly Channel<T> channel;
    readonly Action? onEnd;
    CancellationToken subscribed;
    CancellationTokenRegistration registration;
    int enumerated;
    int ended;

    internal Subscription(BroadcastHub<T> hub, int capacity, Action? onEnd)
    {
        this.hub = hub;
        this.onEnd = onEnd;
        channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    internal ChannelWriter<T> Writer => channel.Writer;

    internal void Watch(CancellationToken cancellationToken)
    {
        subscribed = cancellationToken;
        if (cancellationToken.CanBeCanceled)
        {
            registration = cancellationToken.Register(static state => ((Subscription<T>)state!).End(), this);
        }
    }

    /// <summary>Stops the subscription: no more values arrive, and what is buffered still drains.</summary>
    internal void End()
    {
        if (Interlocked.Exchange(ref ended, 1) != 0) return;
        hub.Remove(this);
        channel.Writer.TryComplete();
        registration.Dispose();
        onEnd?.Invoke();
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref enumerated, 1) != 0)
        {
            throw new InvalidOperationException("A subscription can be enumerated once. Subscribe again for another consumer.");
        }
        return Iterate(cancellationToken);
    }

    IAsyncEnumerator<T> Iterate(CancellationToken cancellationToken) => new Enumerator(this, cancellationToken);

    // A class rather than an iterator method: an iterator that never started
    // skips its finally block on dispose, and disposal must unsubscribe.
    sealed class Enumerator(Subscription<T> owner, CancellationToken cancellationToken) : IAsyncEnumerator<T>
    {
        readonly CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, owner.subscribed);

        public T Current { get; private set; } = default!;

        public async ValueTask<bool> MoveNextAsync()
        {
            var reader = owner.channel.Reader;
            do
            {
                if (reader.TryRead(out var value))
                {
                    Current = value;
                    return true;
                }
            }
            while (await reader.WaitToReadAsync(linked.Token).ConfigureAwait(false));
            return false;
        }

        public ValueTask DisposeAsync()
        {
            owner.End();
            linked.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
