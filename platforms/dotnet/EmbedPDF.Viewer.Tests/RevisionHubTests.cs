using System.Diagnostics;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class RevisionHubTests
{
    static async Task<T?> Next<T>(IAsyncEnumerator<T> enumerator) =>
        await enumerator.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)) ? enumerator.Current : default;

    [Fact]
    public async Task DeliveryCoalescesIndependentlyAndCompletesEveryObserver()
    {
        var hub = new BroadcastHub<long>(1);
        await using var first = hub.Subscribe().GetAsyncEnumerator();
        await using var second = hub.Subscribe().GetAsyncEnumerator();
        hub.Publish(1);
        hub.Publish(2);
        Assert.Equal(2, await Next(first));
        Assert.Equal(2, await Next(second));

        using (var cancellation = new CancellationTokenSource())
        {
            var cancelled = hub.Subscribe(cancellation.Token);
            var draining = Task.Run(async () => { await foreach (var _ in cancelled) { } });
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => draining);
        }
        hub.Publish(3);
        Assert.Equal(3, await Next(second));
        hub.Complete();
        // A completed subscription still drains what it holds.
        Assert.Equal(3, await Next(first));
        Assert.False(await first.MoveNextAsync());
        Assert.False(await second.MoveNextAsync());
        await using var late = hub.Subscribe().GetAsyncEnumerator();
        Assert.False(await late.MoveNextAsync());
    }

    [Fact]
    public async Task DisposingTheEnumeratorWaitsForAnEndAlreadyUnderWay()
    {
        // A token callback can win End and still be running onEnd (the plugin
        // stream's unsubscribe) when the reader's own disposal calls End. The
        // disposal must not finish first: run 45804857099 saw a stream still
        // registered after its await foreach had returned.
        var hub = new BroadcastHub<long>(1);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var ended = 0;
        var subscription = hub.Subscribe(CancellationToken.None, () =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10));
            Volatile.Write(ref ended, 1);
        });
        var enumerator = subscription.GetAsyncEnumerator();
        var racing = Task.Run(subscription.End);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var disposing = enumerator.DisposeAsync().AsTask();
        await Task.Delay(50);
        Assert.False(disposing.IsCompleted, "disposal finished while the first End was still unsubscribing");
        release.Set();
        await disposing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, Volatile.Read(ref ended));
        await racing;
    }

    [Fact]
    public async Task RepeatedCancellationLeavesTheSurvivingObserverLive()
    {
        var hub = new BroadcastHub<long>(1);
        await using var survivor = hub.Subscribe().GetAsyncEnumerator();
        for (long revision = 1; revision <= 20; revision++)
        {
            using var cancellation = new CancellationTokenSource();
            var disposable = Task.Run(async () => { await foreach (var _ in hub.Subscribe(cancellation.Token)) { } });
            hub.Publish(revision);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => disposable);
            Assert.Equal(revision, await Next(survivor));
        }
        Assert.Equal(0, hub.SubscriberCountForTests - 1);
        hub.Complete();
        Assert.False(await survivor.MoveNextAsync());
    }

    [Fact]
    public async Task ADisposedEnumeratorUnsubscribes()
    {
        var hub = new BroadcastHub<long>(1);
        var enumerator = hub.Subscribe().GetAsyncEnumerator();
        Assert.Equal(1, hub.SubscriberCountForTests);
        await enumerator.DisposeAsync();
        Assert.Equal(0, hub.SubscriberCountForTests);
    }

    [Fact]
    public async Task ReportedErrorsKeepTheNewestSixteenPerObserver()
    {
        var hub = new BroadcastHub<PdfException>(16);
        var stream = hub.Subscribe();
        for (var index = 0; index < 20; index++)
        {
            hub.Publish(new PdfException($"E{index}", PdfErrorCode.OperationFailed, ""));
        }
        hub.Complete();
        var names = new List<string>();
        await foreach (var error in stream)
        {
            names.Add(error.Name);
        }
        Assert.Equal(Enumerable.Range(4, 16).Select(index => $"E{index}"), names);
    }

    [Fact]
    public void PublishingPastCapacityWithNoReaderNeverBlocks()
    {
        var hub = new BroadcastHub<long>(1);
        _ = hub.Subscribe();
        _ = hub.Subscribe();
        var clock = Stopwatch.StartNew();
        for (long value = 0; value < 100_000; value++)
        {
            hub.Publish(value);
        }
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ASubscriptionEnumeratesOnce()
    {
        var hub = new BroadcastHub<long>(1);
        var stream = hub.Subscribe();
        await using var first = stream.GetAsyncEnumerator();
        Assert.Throws<InvalidOperationException>(() => stream.GetAsyncEnumerator());
    }

    [Fact]
    public async Task TheEnumeratorTokenAlsoEndsTheStream()
    {
        var hub = new BroadcastHub<long>(1);
        using var cancellation = new CancellationTokenSource();
        await using var enumerator = hub.Subscribe().GetAsyncEnumerator(cancellation.Token);
        var waiting = enumerator.MoveNextAsync().AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    [Fact]
    public void CompletingBetweenTheStepsOfASubscriptionLeavesNoRegistration()
    {
        var hub = new BroadcastHub<long>(1);
        hub.BetweenStepsForTests = hub.Complete;
        using var cancellation = new CancellationTokenSource();
        var subscription = hub.Subscribe(cancellation.Token, null);
        Assert.True(subscription.HasEndedForTests);
        Assert.False(subscription.HasRegistrationForTests);
        Assert.Equal(0, hub.SubscriberCountForTests);
    }

    [Fact]
    public void CancellingBetweenTheStepsOfASubscriptionLeavesNoSubscriber()
    {
        var hub = new BroadcastHub<long>(1);
        using var cancellation = new CancellationTokenSource();
        hub.BetweenStepsForTests = cancellation.Cancel;
        var subscription = hub.Subscribe(cancellation.Token, null);
        Assert.True(subscription.HasEndedForTests);
        Assert.Equal(0, hub.SubscriberCountForTests);
    }

    [Fact]
    public async Task SubscribingWhileTheHubCompletesLeavesNoLiveRegistration()
    {
        for (var round = 0; round < 50; round++)
        {
            var hub = new BroadcastHub<long>(1);
            var subscriptions = new System.Collections.Concurrent.ConcurrentBag<Subscription<long>>();
            using var cancellation = new CancellationTokenSource();
            using var start = new Barrier(5);
            var subscribers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            {
                start.SignalAndWait();
                for (var i = 0; i < 200; i++) subscriptions.Add(hub.Subscribe(cancellation.Token, null));
            })).ToArray();
            start.SignalAndWait();
            hub.Complete();
            await Task.WhenAll(subscribers);
            Assert.Equal(0, hub.SubscriberCountForTests);
            Assert.All(subscriptions, subscription =>
            {
                Assert.True(subscription.HasEndedForTests);
                Assert.False(subscription.HasRegistrationForTests);
            });
        }
    }

    [Fact]
    public async Task SubscribingAndCancellingConcurrentlyLeavesNoSubscriber()
    {
        var hub = new BroadcastHub<long>(1);
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                using var cancellation = new CancellationTokenSource();
                var subscription = hub.Subscribe(cancellation.Token, null);
                var cancelling = Task.Run(cancellation.Cancel);
                hub.Publish(i);
                cancelling.Wait();
                Assert.True(subscription.HasEndedForTests);
            }
        }));
        await Task.WhenAll(tasks);
        Assert.Equal(0, hub.SubscriberCountForTests);
    }

    [Fact]
    public async Task AnUnboundedHubKeepsEveryValueForASlowReader()
    {
        var hub = new BroadcastHub<int>(null);
        var stream = hub.Subscribe();
        for (var i = 0; i < 5000; i++) hub.Publish(i);
        hub.Complete();
        var seen = new List<int>();
        await foreach (var value in stream) seen.Add(value);
        Assert.Equal(Enumerable.Range(0, 5000), seen);
    }

    [Fact]
    public async Task DocumentEventsAreNeverDropped()
    {
        await using var viewer = await PdfViewer.CreateAsync();
        Assert.Null(viewer.DocumentEventCapacity);
    }
}
