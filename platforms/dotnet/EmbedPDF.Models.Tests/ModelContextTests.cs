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
