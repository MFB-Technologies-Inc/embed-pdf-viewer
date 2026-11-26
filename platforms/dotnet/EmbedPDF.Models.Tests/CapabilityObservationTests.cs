using System.Runtime.CompilerServices;

namespace EmbedPDF.Models.Tests;

public class CapabilityObservationTests
{
    /// <summary>An event stream that waits forever and reports when its enumeration is cancelled.</summary>
    static async IAsyncEnumerable<int> Endless(TaskCompletionSource ended, [EnumeratorCancellation] CancellationToken token = default)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, token);
            yield break;
        }
        finally
        {
            ended.TrySetResult();
        }
    }

    [Fact]
    public Task AThrowingInitialStopsBothLoops() => SingleThreadContext.RunWatched(async () =>
    {
        await using var viewer = await Fixture.StartedAsync();
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => CapabilityObservation.ObserveAsync(
            viewer, token => Endless(ended, token),
            initial: () => throw new InvalidOperationException("initial failed"),
            refresh: () => Task.CompletedTask,
            receive: _ => Task.CompletedTask,
            CancellationToken.None));
        Assert.Equal("initial failed", failure.Message);
        Assert.Same(ended.Task, await Task.WhenAny(ended.Task, Task.Delay(TimeSpan.FromSeconds(10))));
    });
}
