using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace EmbedPDF.Models;

/// <summary>Hands each link activation to every subscription open when it arrives.</summary>
internal sealed class LinkActivationFeed
{
    readonly Lock gate = new();
    readonly Dictionary<long, Channel<LinkActivation>> subscribers = [];
    long nextId;
    bool completed;

    /// <summary>
    /// A new stream of the activations yielded from now on, subscribed before
    /// this returns. Ending its enumeration or cancelling
    /// <paramref name="cancellationToken"/> removes it.
    /// </summary>
    public IAsyncEnumerable<LinkActivation> Subscribe(CancellationToken cancellationToken)
    {
        // Performance: the channel has no bound, so a stream that is taken and
        // never enumerated keeps every activation until the model is disposed.
        // Activations come one per tap, so it grows slowly.
        var channel = Channel.CreateUnbounded<LinkActivation>(new UnboundedChannelOptions { SingleReader = true });
        long id;
        lock (gate)
        {
            id = ++nextId;
            if (completed) channel.Writer.TryComplete();
            else subscribers[id] = channel;
        }
        var registration = cancellationToken.Register(() => Remove(id));
        return Read(channel, id, registration, cancellationToken);
    }

    public void Yield(LinkActivation activation)
    {
        Channel<LinkActivation>[] snapshot;
        lock (gate) snapshot = [.. subscribers.Values];
        foreach (var channel in snapshot) channel.Writer.TryWrite(activation);
    }

    /// <summary>Ends every stream; later subscriptions end at once.</summary>
    public void Complete()
    {
        Channel<LinkActivation>[] snapshot;
        lock (gate)
        {
            completed = true;
            snapshot = [.. subscribers.Values];
            subscribers.Clear();
        }
        foreach (var channel in snapshot) channel.Writer.TryComplete();
    }

    void Remove(long id)
    {
        Channel<LinkActivation>? channel;
        lock (gate)
        {
            if (!subscribers.Remove(id, out channel)) return;
        }
        channel.Writer.TryComplete();
    }

    async IAsyncEnumerable<LinkActivation> Read(
        Channel<LinkActivation> channel, long id, CancellationTokenRegistration registration,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (channel.Reader.TryRead(out var activation)) yield return activation;
            }
        }
        finally
        {
            registration.Dispose();
            Remove(id);
        }
    }
}
