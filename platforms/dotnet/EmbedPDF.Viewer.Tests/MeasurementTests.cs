using System.Text.Json.Nodes;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>The port of MeasurementTests.swift.</summary>
public sealed class MeasurementTests
{
    static readonly CancellationToken Cancelled = new(true);
    static readonly PdfPoint[] Line = [new(10, 10), new(82, 10)];

    internal static async Task<(PdfViewer Viewer, DocumentInfo Document, MeasurementCapability Measurement, PageRef Page)> Opened(string fixture = "report")
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { Interaction = true, Annotation = "", Measurement = new MeasurementConfig() });
        var document = await viewer.OpenAsync(Fixture.Data(fixture));
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        return (viewer, document, await viewer.GetMeasurementAsync(document), page);
    }

    static MeasurementPageTarget On(PageRef page) => new MeasurementPageTarget.Page(page);

    [Fact]
    public async Task EnsureLoadedMakesThePageReady()
    {
        var (viewer, _, measurement, page) = await Opened();
        await using var _v = viewer;
        await measurement.EnsureLoadedAsync(page);
        var scale = await measurement.GetPageScaleAsync(page);
        Assert.True(scale.Ready);
        Assert.Equal(MeasurementScaleSource.Default, scale.Source);
        Assert.True(scale.Persistent);
    }

    [Fact]
    public async Task APresetIsWrittenAndReadsBack()
    {
        var (viewer, _, measurement, page) = await Opened();
        await using var _v = viewer;
        var preset = (await measurement.GetPresetsAsync())[0];
        Assert.Single(await measurement.SetPresetAsync(On(page), preset.Id));
        Assert.Equal(MeasurementScaleSource.Owned, (await measurement.GetPageScaleAsync(page)).Source);
    }

    [Fact]
    public async Task AForeignScaleIsReadAndKept()
    {
        var (viewer, _, measurement, page) = await Opened("measure-foreign-viewport");
        await using var _v = viewer;
        await measurement.EnsureLoadedAsync(page);
        var foreign = await measurement.GetPageScaleAsync(page);
        Assert.Equal(MeasurementScaleSource.Foreign, foreign.Source);
        Assert.Equal("1 in = 1 m", foreign.Measure?.Ratio);
        var preset = (await measurement.GetPresetsAsync())[0];
        await measurement.SetPresetAsync(On(page), preset.Id);
        Assert.Equal(MeasurementScaleSource.Owned, (await measurement.GetPageScaleAsync(page)).Source);
        await measurement.ClearScaleAsync(On(page));
        Assert.Equal(MeasurementScaleSource.Foreign, (await measurement.GetPageScaleAsync(page)).Source);
    }

    [Fact]
    public async Task MeasureDistanceUsesThePageScale()
    {
        var (viewer, _, measurement, page) = await Opened("measure-foreign-viewport");
        await using var _v = viewer;
        await measurement.EnsureLoadedAsync(page);
        var reading = await measurement.MeasureDistanceAsync(page, new PdfPoint(0, 0), new PdfPoint(72, 0));
        var readout = Assert.IsType<MeasurementReading.Value>(reading).Readout;
        Assert.Equal(MeasurementReadoutKind.Distance, readout.Kind);
        Assert.True(readout.Value > 0);
    }

    [Fact]
    public async Task CalibratingAZeroLengthThrows()
    {
        var (viewer, _, measurement, page) = await Opened();
        await using var _v = viewer;
        var error = await Assert.ThrowsAsync<PdfException>(() =>
            measurement.CalibrateAsync(new MeasurementCalibrateInput(page, new PdfPoint(5, 5), new PdfPoint(5, 5), 1, "m")));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
    }

    [Fact]
    public async Task ACreatedMeasurementIsAnAnnotation()
    {
        var (viewer, document, measurement, page) = await Opened();
        await using var _v = viewer;
        // A measurement needs the page's scale: not-ready until it loads.
        await measurement.EnsureLoadedAsync(page);
        var json = await measurement.CreateMeasurementAsync(new CreateMeasurementInput(MeasurementKind.Distance, page, Line));
        // The engine's annotation ref record: its object number names the line.
        var objectNumber = JsonNode.Parse(json)!["objectNumber"]!.GetValue<int>();
        using var annotations = await viewer.GetAnnotationAsync(document);
        var found = await annotations.GetAsync(new AnnotationRef.ObjectNumber(page, objectNumber));
        Assert.Equal(AnnotationSubtype.Line, found?.Subtype);
        // A line dimension: 72 points, one inch, in the default metric scale.
        var readout = Assert.IsType<MeasurementReading.Value>(await measurement.ReadoutAsync(json)).Readout;
        Assert.Equal(MeasurementReadoutKind.Distance, readout.Kind);
        Assert.True(Math.Abs(readout.Value - 0.0254) < 1e-6);
    }

    [Fact]
    public async Task AClosedDocumentsCapabilityThrows()
    {
        var (viewer, document, measurement, page) = await Opened();
        await using var _v = viewer;
        await viewer.CloseAsync(document);
        var error = await Assert.ThrowsAsync<PdfException>(() => measurement.GetPageScaleAsync(page));
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
        Assert.Equal("PluginError", error.Name);
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, _, measurement, page) = await Opened();
        await using var _v = viewer;
        using var stop = new CancellationTokenSource();
        var reading = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in measurement.ObserveEvents(stop.Token)) { }
            }
            catch (OperationCanceledException)
            {
            }
        });
        await Task.Delay(20);
        await stop.CancelAsync();
        await reading.WaitAsync(TimeSpan.FromSeconds(5));
        var preset = (await measurement.GetPresetsAsync())[0];
        await measurement.SetPresetAsync(On(page), preset.Id);
    }

    [Fact]
    public async Task SetPresetCancelsWhenTheTaskIsCancelled()
    {
        var (viewer, _, measurement, page) = await Opened();
        await using var _v = viewer;
        var preset = (await measurement.GetPresetsAsync())[0];
        await RedactionTests.FinishesOrCancels(token => measurement.SetPresetAsync(On(page), preset.Id, null, token));
        await measurement.GetPageScaleAsync(page);
    }

    [Fact]
    public async Task EveryOtherCancellableMemberCancelsBeforeItStarts()
    {
        var (viewer, _, measurement, page) = await Opened();
        await using var _v = viewer;
        await measurement.EnsureLoadedAsync(page);
        Func<Task>[] calls =
        [
            () => measurement.SetScaleAsync(On(page), new PdfMeasurement("RL") { Ratio = "1 in = 1 ft" }, null, Cancelled),
            () => measurement.CalibrateAsync(new MeasurementCalibrateInput(page, new PdfPoint(0, 0), new PdfPoint(72, 0), 1, "in"), null, Cancelled),
            () => measurement.SetUnitAsync(On(page), "cm", null, Cancelled),
            () => measurement.SetAreaUnitAsync(On(page), "cm", null, Cancelled),
            () => measurement.SetPrecisionAsync(On(page), 2, null, Cancelled),
            () => measurement.ClearScaleAsync(On(page), null, Cancelled),
            () => measurement.CreateMeasurementAsync(new CreateMeasurementInput(MeasurementKind.Distance, page, Line), Cancelled),
            () => measurement.EnsureLoadedAsync(page, Cancelled),
        ];
        foreach (var call in calls) await Assert.ThrowsAnyAsync<OperationCanceledException>(call);
        Assert.Equal(MeasurementScaleSource.Default, (await measurement.GetPageScaleAsync(page)).Source);
    }

    [Fact]
    public async Task AScaleChangeIsObserved()
    {
        var (viewer, _, measurement, page) = await Opened();
        await using var _v = viewer;
        var events = measurement.ObserveEvents();
        var preset = (await measurement.GetPresetsAsync())[0];
        await measurement.SetPresetAsync(On(page), preset.Id);
        var changed = await Fixture.First(events, e => e as MeasurementEvent.ScaleChanged);
        Assert.Equal(page, changed.Page);
    }

    [Fact]
    public async Task AMeasureTheEngineCannotHoldIsInvalidInputAndChangesNothing()
    {
        var (viewer, _, measurement, page) = await Opened();
        await using var _v = viewer;
        await measurement.EnsureLoadedAsync(page);
        PdfMeasurement[] bad =
        [
            new("RL") { Ratio = "1 in = 1 ft", Cyx = double.NaN },
            new("RL") { Ratio = "1 in = 1 ft", Distance = [new PdfNumberFormat("ft") { Fraction = (MeasureFraction)42 }] },
            new("RL") { Ratio = "1 in = 1 ft", Distance = [new PdfNumberFormat("ft") { Conversion = double.PositiveInfinity }] },
        ];
        foreach (var measure in bad)
        {
            var error = await Assert.ThrowsAsync<PdfException>(() => measurement.SetScaleAsync(On(page), measure));
            Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        }
        Assert.Equal(MeasurementScaleSource.Default, (await measurement.GetPageScaleAsync(page)).Source);
    }

    [Fact]
    public async Task UnitsPresetsAndSettingsRoundTrip()
    {
        var (viewer, _, measurement, page) = await Opened();
        await using var _v = viewer;
        Assert.Contains("cm", await measurement.GetUnitsAsync());
        Assert.NotEmpty(await measurement.GetAreaUnitsAsync());
        Assert.True(await measurement.CanMeasureAsync(page));
        Assert.True(await measurement.CanCalibrateAsync());
        Assert.False(await measurement.IsBusyAsync());
        Assert.Null(await measurement.GetCalibrationRequestAsync());
        var preset = new ScalePreset("custom", "Custom", 1, 100, "m");
        await measurement.UpdateSettingsAsync(new MeasurementConfig { Presets = [preset], DefaultScale = new MeasurementDefaultScale.Imperial() });
        var settings = await measurement.GetSettingsAsync();
        Assert.Equal([preset], settings.Presets);
        Assert.IsType<MeasurementDefaultScale.Imperial>(settings.DefaultScale);
        await measurement.ResetSettingsAsync();
        Assert.IsType<MeasurementDefaultScale.Metric>((await measurement.GetSettingsAsync()).DefaultScale);
        await measurement.SetPresetAsync(On(page), (await measurement.GetPresetsAsync())[0].Id);
        Assert.Single(await measurement.GetLastReportsAsync());
        var area = await measurement.MeasureAreaAsync(page, [new(0, 0), new(72, 0), new(72, 72), new(0, 72)]);
        Assert.Equal(MeasurementReadoutKind.Area, Assert.IsType<MeasurementReading.Value>(area).Readout.Kind);
    }
}
