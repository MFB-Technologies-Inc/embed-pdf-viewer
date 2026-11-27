using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class TelemetryTests
{
    [Fact]
    public async Task OpenPagesAndSaveAreTracedAndTimed()
    {
        var activities = new ConcurrentQueue<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "EmbedPDF",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => activities.Enqueue(activity.OperationName),
        };
        ActivitySource.AddActivityListener(listener);
        var calls = new ConcurrentQueue<string>();
        using var meters = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == "EmbedPDF" && instrument.Name == "embedpdf.call.duration") l.EnableMeasurementEvents(instrument);
            },
        };
        meters.SetMeasurementEventCallback<double>((_, _, tags, _) =>
        {
            foreach (var tag in tags) if (tag.Key == "call") calls.Enqueue((string)tag.Value!);
        });
        meters.Start();

        await using var viewer = await PdfViewer.CreateAsync();
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        _ = await viewer.GetPagesAsync(document);
        _ = await viewer.SaveAsync(document);

        Assert.Equal(["open", "pages", "save"], activities);
        Assert.Equal(["open", "pages", "save"], calls);
    }
}
