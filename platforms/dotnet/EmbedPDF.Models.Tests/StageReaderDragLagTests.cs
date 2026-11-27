using System.Diagnostics;
using Xunit.Abstractions;

namespace EmbedPDF.Models.Tests;

/// <summary>Runs only with <c>EMBEDPDF_LATENCY=1</c>: it drives a two-second drag, and its numbers mean something only on an idle machine.</summary>
public sealed class LatencyFactAttribute : FactAttribute
{
    public LatencyFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("EMBEDPDF_LATENCY") != "1") Skip = "set EMBEDPDF_LATENCY=1 to measure drag latency";
    }
}

/// <summary>Port of <c>StageReaderDragLagTests.swift</c>.</summary>
public class StageReaderDragLagTests(ITestOutputHelper output)
{
    /// <summary>
    /// A drag at 120 Hz, each change its own call to DragChangedAsync.
    /// Measures each call from the change to its return, how long the chain
    /// runs on after the last change, and how long the event loop keeps
    /// writing the camera after that.
    /// </summary>
    [LatencyFact]
    public Task A120HzDragKeepsUp() => SingleThreadContext.Run(async context =>
    {
        await using var viewer = await Fixture.StartedAsync(b => b.Render(new RenderConfig()).Stage(new StageConfig()));
        var document = await viewer.OpenAsync(Fixture.Data());
        using var model = context.Watch(new StageReaderModel(await viewer.GetStageAsync(document), await viewer.GetRenderAsync(document)));
        using var stop = new CancellationTokenSource();
        var loop = model.RunAsync(stop.Token);
        await model.SizeChangedAsync(390, 844, 3);
        await Task.Delay(200);
        var clock = Stopwatch.StartNew();
        var writes = new List<TimeSpan>();
        model.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(StageReaderModel.Camera)) writes.Add(clock.Elapsed); };
        const int count = 240;
        var period = TimeSpan.FromTicks(83_333);
        var calls = new List<Task<TimeSpan>>();
        for (var i = 1; i <= count; i++)
        {
            var issued = clock.Elapsed;
            var step = i;
            // 2 units a frame: 240 a second, a slow scroll.
            calls.Add(Measure(model.DragChangedAsync(0, -2 * step), issued, clock));
            var due = period * i - clock.Elapsed;
            if (due > TimeSpan.Zero) await Task.Delay(due);
        }
        var lastChange = clock.Elapsed;
        var latencies = (await Task.WhenAll(calls)).OrderBy(t => t).ToList();
        var chainDrained = clock.Elapsed - lastChange;
        var quietSince = writes.Count > 0 ? writes[^1] : lastChange;
        while (clock.Elapsed - quietSince < TimeSpan.FromMilliseconds(250))
        {
            await Task.Delay(10);
            if (writes.Count > 0 && writes[^1] > quietSince) quietSince = writes[^1];
        }
        var loopTail = TimeSpan.FromTicks(Math.Max(0, ((writes.Count > 0 ? writes[^1] : lastChange) - lastChange).Ticks));
        await model.DragEndedAsync(0, 0);
        TimeSpan P(double q) => latencies[Math.Min(latencies.Count - 1, (int)(latencies.Count * q))];
        output.WriteLine($"stage drag 120Hz: changes={count} p50={P(0.5).TotalMilliseconds:F2}ms p95={P(0.95).TotalMilliseconds:F2}ms "
            + $"p99={P(0.99).TotalMilliseconds:F2}ms max={latencies[^1].TotalMilliseconds:F2}ms chainDrained={chainDrained.TotalMilliseconds:F2}ms "
            + $"cameraWrites={writes.Count} loopTail={loopTail.TotalMilliseconds:F2}ms");
        await Observed.ExpectAsync(model, () => model.Camera, await model.Stage.GetCameraAsync());
        stop.Cancel();
        await loop;
    });

    static async Task<TimeSpan> Measure(Task call, TimeSpan issued, Stopwatch clock)
    {
        await call;
        return clock.Elapsed - issued;
    }
}
