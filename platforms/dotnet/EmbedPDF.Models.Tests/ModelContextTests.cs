using System.Runtime.CompilerServices;

namespace EmbedPDF.Models.Tests;

/// <summary>A minimal model for the home-context rules.</summary>
sealed class Probe(SynchronizationContext? context = null) : ObservableModel(context)
{
    int value;
    int inside;

    public int Value => value;

    /// <summary>The most notifications seen running at once.</summary>
    public int MostAtOnce;

    public async Task SetAsync(int next)
    {
        await Home.SwitchTo();
        var now = Interlocked.Increment(ref inside);
        MostAtOnce = Math.Max(MostAtOnce, now);
        Set(ref value, next, nameof(Value));
        Interlocked.Decrement(ref inside);
    }
}

public class ModelContextTests
{
    [Fact]
    public async Task AModelWithoutAContextUsesASerialOne()
    {
        var probe = await Task.Run(() => new Probe());
        Assert.IsType<SerialSynchronizationContext>(probe.Context);
        var wrongContext = 0;
        probe.PropertyChanged += (_, _) =>
        {
            if (!ReferenceEquals(SynchronizationContext.Current, probe.Context)) Interlocked.Increment(ref wrongContext);
        };
        await Task.WhenAll(Enumerable.Range(1, 200).Select(i => Task.Run(() => probe.SetAsync(i))));
        Assert.Equal(0, wrongContext);
        Assert.Equal(1, probe.MostAtOnce);
    }

    [Fact]
    public Task AnEntryPointCalledOffTheContextRunsOnIt() => SingleThreadContext.Run(async context =>
    {
        var probe = context.Watch(new Probe());
        Assert.Same(context, probe.Context);
        await Task.Run(() => probe.SetAsync(5));
        Assert.Equal(5, probe.Value);
        Assert.Equal(1, context.Notifications);
    });

    [Fact]
    public async Task AGivenContextWinsOverTheCurrentOne()
    {
        var given = new SerialSynchronizationContext();
        var probe = new Probe(given);
        Assert.Same(given, probe.Context);
        await probe.SetAsync(1);
        Assert.Equal(1, probe.Value);
    }
}

/// <summary>
/// Runs alone: the unobserved-task event is process-wide, so the fault this
/// test abandons on purpose would reach the watch of any watched test running
/// beside it.
/// </summary>
[CollectionDefinition(nameof(UnobservedWatchTests), DisableParallelization = true)]
public sealed class UnobservedWatchCollection;

[Collection(nameof(UnobservedWatchTests))]
public class UnobservedWatchTests
{
    /// <summary>The watch the disposal tests run under sees a fault nobody observed.</summary>
    [Fact]
    public async Task TheUnobservedWatchSeesAnUnobservedFault()
    {
        var failure = await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() =>
            SingleThreadContext.RunWatched(() =>
            {
                Abandon();
                return Task.CompletedTask;
            }));
        Assert.Contains("abandoned on purpose", failure.Message);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Abandon()
    {
        var faulted = new TaskCompletionSource();
        faulted.SetException(new InvalidOperationException("abandoned on purpose"));
    }
}
