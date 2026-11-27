using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Runs only with <c>EMBEDPDF_LATENCY=1</c>: it takes ten seconds and its numbers mean something only on an idle machine.</summary>
public sealed class LatencyFactAttribute : FactAttribute
{
    public LatencyFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("EMBEDPDF_LATENCY") != "1") Skip = "set EMBEDPDF_LATENCY=1 to measure pointer latency";
    }
}

public sealed class PointerLatencyTests(ITestOutputHelper output)
{
    /// <summary>Stamps each move's arrival on the kernel thread.</summary>
    sealed class StampingHandler : IInteractionHandler
    {
        public readonly Dictionary<int, long> Arrivals = [];
        public bool EnabledFor(InteractionTool tool) => true;
        public bool OnDown(PointerSample sample) => true;
        public void OnMove(PointerSample sample)
        {
            var now = Stopwatch.GetTimestamp();
            lock (Arrivals) Arrivals[(int)sample.Viewport.X] = now;
        }
    }

    [LatencyFact]
    public async Task MovesReachTheirHandlerWithinAFrameAt120Hz()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        using var interaction = await viewer.GetInteractionAsync(document);
        var handler = new StampingHandler();
        using var registration = await interaction.RegisterHandlerAsync(handler, "stamp", 1);

        const int count = 1200;
        var period = TimeSpan.FromTicks(83_333);
        var sent = new long[count];
        interaction.DispatchPointer(Pointer.Sample(PointerPhase.Down, page, -1));
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < count; i++)
        {
            sent[i] = Stopwatch.GetTimestamp();
            interaction.DispatchPointer(Pointer.Sample(PointerPhase.Move, page, i));
            var wait = period * (i + 1) - Stopwatch.GetElapsedTime(start);
            if (wait > TimeSpan.Zero) await Task.Delay(wait);
        }
        interaction.DispatchPointer(Pointer.Sample(PointerPhase.Up, page, count));
        await interaction.GetCursorAsync();

        List<TimeSpan> latencies;
        lock (handler.Arrivals)
        {
            latencies = [.. Enumerable.Range(0, count).Where(handler.Arrivals.ContainsKey)
                .Select(i => Stopwatch.GetElapsedTime(sent[i], handler.Arrivals[i])).Order()];
        }
        Assert.Equal(count, latencies.Count);
        var (p50, p95, max) = (latencies[count / 2], latencies[count * 95 / 100], latencies[count - 1]);
        output.WriteLine($"pointer latency p50={p50.TotalMilliseconds:F3}ms p95={p95.TotalMilliseconds:F3}ms max={max.TotalMilliseconds:F3}ms");
        Assert.True(p95 < period, $"p95 {p95.TotalMilliseconds} ms is past one 120 Hz frame");
    }
}
