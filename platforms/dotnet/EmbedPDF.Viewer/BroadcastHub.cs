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
/// never blocks. With a capacity a full channel drops its oldest value: with
/// capacity 1 a slow consumer keeps only the newest, which is how state
/// revisions coalesce. With no capacity (<see langword="null"/>) every value is
/// kept, for streams where a dropped value would mislead, such as document events.
/// </remarks>
internal sealed class BroadcastHub<T>(int? capacity)
{
    readonly Lock gate = new();
    readonly HashSet<Subscription<T>> subscribers = [];

    internal int? Capacity => capacity;
    bool completed;

    /// <summary>Runs between the two steps of <see cref="Subscribe(CancellationToken, Action?)"/>; a seam for race tests.</summary>
    internal Action? BetweenStepsForTests { get; set; }

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
        // Watch first: a token that has already fired ends the subscription
        // here, and the install below then skips it.
        subscription.Watch(cancellationToken);
        BetweenStepsForTests?.Invoke();
        bool live;
        lock (gate)
        {
            // End takes this lock to remove, so a subscription is either
            // installed before End removes it or seen as ended here.
            live = !completed && !subscription.HasEnded;
            if (live) subscribers.Add(subscription);
        }
        if (!live) subscription.End();
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
    readonly Lock gate = new();
    CancellationToken subscribed;
    CancellationTokenRegistration registration;
    int enumerated;
    int ended;
    bool registered;

    internal bool HasEndedForTests => HasEnded;

    internal bool HasRegistrationForTests
    {
        get { lock (gate) return registered; }
    }

    internal Subscription(BroadcastHub<T> hub, int? capacity, Action? onEnd)
    {
        this.hub = hub;
        this.onEnd = onEnd;
        channel = capacity is { } bound
            ? Channel.CreateBounded<T>(new BoundedChannelOptions(bound)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            })
            : Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    }

    internal ChannelWriter<T> Writer => channel.Writer;

    internal bool HasEnded => Volatile.Read(ref ended) != 0;

    internal void Watch(CancellationToken cancellationToken)
    {
        subscribed = cancellationToken;
        if (!cancellationToken.CanBeCanceled) return;
        // Register may run End at once, on this thread, for a fired token.
        var watching = cancellationToken.Register(static state => ((Subscription<T>)state!).End(), this);
        lock (gate)
        {
            if (!HasEnded)
            {
                registration = watching;
                registered = true;
                return;
            }
        }
        watching.Dispose();
    }

    /// <summary>Stops the subscription: no more values arrive, and what is buffered still drains.</summary>
    internal void End()
    {
        if (Interlocked.Exchange(ref ended, 1) != 0) return;
        hub.Remove(this);
        channel.Writer.TryComplete();
        CancellationTokenRegistration watching;
        lock (gate)
        {
            watching = registration;
            registration = default;
            registered = false;
        }
        watching.Dispose();
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
