namespace EmbedPDF.Models;

/// <summary>The port of Swift's <c>observeCapability</c>.</summary>
internal static class CapabilityObservation
{
    /// <summary>
    /// Follows a capability's events and the viewer's state revisions until
    /// either stream ends or <paramref name="cancellationToken"/> is
    /// cancelled. Both subscriptions start before <paramref name="initial"/>
    /// runs, so nothing that happens during it is missed. Call on the
    /// model's home context.
    /// </summary>
    public static async Task ObserveAsync<TEvent>(
        PdfViewer viewer,
        Func<CancellationToken, IAsyncEnumerable<TEvent>> events,
        Func<Task> initial,
        Func<Task> refresh,
        Func<TEvent, Task> receive,
        CancellationToken cancellationToken)
    {
        using var group = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var eventLoop = Follow(events(group.Token), receive, group.Token);
        var revisionLoop = Follow(viewer.ObserveStateRevisions(group.Token), _ => refresh(), group.Token);
        await initial();
        await Task.WhenAny(eventLoop, revisionLoop);
        group.Cancel();
        await Task.WhenAll(eventLoop, revisionLoop);
    }

    /// <summary>
    /// Hands each item to <paramref name="handle"/>, one at a time, until the
    /// stream ends or <paramref name="token"/> is cancelled. Never faults
    /// for a cancellation.
    /// </summary>
    public static async Task Follow<T>(IAsyncEnumerable<T> stream, Func<T, Task> handle, CancellationToken token)
    {
        try
        {
            await foreach (var item in stream.WithCancellation(token))
            {
                if (token.IsCancellationRequested) return;
                await handle(item);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }
}
