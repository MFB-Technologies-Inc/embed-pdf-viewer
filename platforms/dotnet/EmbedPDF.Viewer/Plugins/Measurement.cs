using System.Diagnostics.CodeAnalysis;
using GM = uniffi.epdf_plugin_measurement;

namespace EmbedPDF;

/// <summary>
/// A document's page scales, calibration and measurement readouts. Get one
/// with <see cref="PdfViewer.GetMeasurementAsync"/>; the viewer needs
/// <see cref="PdfViewerOptions.Interaction"/>, <see cref="PdfViewerOptions.Annotation"/>
/// and <see cref="PdfViewerOptions.Measurement"/>. After its document closes,
/// every call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
[SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The JSON, ref and annotation overloads of readout, as in Swift.")]
public sealed class MeasurementCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GM.MeasurementHandle handle;

    internal MeasurementCapability(PdfViewer viewer, GM.MeasurementHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Current settings, shared by every document using this plugin registration.</summary>
    public Task<MeasurementSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => Ffi.ToPublic(await handle.Settings().ConfigureAwait(false)), cancellationToken);

    /// <summary>Merges the supplied fields; omission preserves them.</summary>
    public Task UpdateSettingsAsync(MeasurementConfig patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        return viewer.Run(() => handle.UpdateSettings(patch.ToFfi()), cancellationToken);
    }

    /// <summary>Restores the values supplied at registration.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetSettings(), cancellationToken);

    /// <summary>Whether <paramref name="page"/> can be measured.</summary>
    public Task<bool> CanMeasureAsync(PageRef page, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.CanMeasure(Ffi.ToFfi(page)), cancellationToken);

    /// <summary>The page's scale. <see cref="MeasurementPageScale.Ready"/> is false until <see cref="EnsureLoadedAsync"/> has read the page's viewports.</summary>
    public Task<MeasurementPageScale> GetPageScaleAsync(PageRef page, CancellationToken cancellationToken = default) =>
        viewer.Run(async () => Ffi.ToPublic(await handle.PageScale(Ffi.ToFfi(page)).ConfigureAwait(false)), cancellationToken);

    /// <summary>Whether a scale change is running.</summary>
    public Task<bool> IsBusyAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.IsBusy(), cancellationToken);

    /// <summary>The reports of the last scale change.</summary>
    public Task<IReadOnlyList<ScaleChangeReport>> GetLastReportsAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => Reports(await handle.LastReports().ConfigureAwait(false)), cancellationToken);

    /// <summary>The scale presets.</summary>
    public Task<IReadOnlyList<ScalePreset>> GetPresetsAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => (IReadOnlyList<ScalePreset>)[.. (await handle.Presets().ConfigureAwait(false)).Select(Ffi.ToPublic)], cancellationToken);

    /// <summary>The length units a scale can use.</summary>
    public Task<IReadOnlyList<string>> GetUnitsAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => (IReadOnlyList<string>)await handle.Units().ConfigureAwait(false), cancellationToken);

    /// <summary>The area units a scale can use.</summary>
    public Task<IReadOnlyList<string>> GetAreaUnitsAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => (IReadOnlyList<string>)await handle.AreaUnits().ConfigureAwait(false), cancellationToken);

    /// <summary>A measurement annotation's value in its own scale. <paramref name="annotationJson"/> is the engine's annotation ref record.</summary>
    public Task<MeasurementReading> ReadoutAsync(string annotationJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationJson);
        return viewer.Run(async () => Ffi.ToPublic(await handle.Readout(Ffi.WellFormed(annotationJson, "annotation")).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>
    /// A measurement annotation's value in its own scale, from its ref: the
    /// one <see cref="AnnotationCapability.CreateAsync(AnnotationDraft, PageRef, AnnotationCreateOptions?, CancellationToken)"/>
    /// returns, or an <see cref="AnnotationInfo.AnnotationRef"/>. The engine
    /// reads nothing else of the annotation from the caller.
    /// </summary>
    public Task<MeasurementReading> ReadoutAsync(AnnotationRef annotation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return viewer.Run(async () => Ffi.ToPublic(await handle.Readout(AnnotationPosition.RefJson(annotation)).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>A measurement annotation's value in its own scale, from the annotation as <see cref="AnnotationCapability"/> reads it.</summary>
    public Task<MeasurementReading> ReadoutAsync(AnnotationInfo annotation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return ReadoutAsync(annotation.AnnotationRef, cancellationToken);
    }

    /// <summary>The distance from <paramref name="from"/> to <paramref name="to"/> (page space) in the page's scale.</summary>
    public Task<MeasurementReading> MeasureDistanceAsync(PageRef page, PdfPoint from, PdfPoint to, CancellationToken cancellationToken = default) =>
        viewer.Run(async () => Ffi.ToPublic(await handle.MeasureDistance(Ffi.ToFfi(page), Ffi.ToFfi(from), Ffi.ToFfi(to)).ConfigureAwait(false)), cancellationToken);

    /// <summary>The area inside <paramref name="vertices"/> (page space) in the page's scale.</summary>
    public Task<MeasurementReading> MeasureAreaAsync(PageRef page, IEnumerable<PdfPoint> vertices, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        var list = vertices.ToArray();
        return viewer.Run(async () => Ffi.ToPublic(await handle.MeasureArea(Ffi.ToFfi(page), [.. list.Select(Ffi.ToFfi)]).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>The open calibration request, if any.</summary>
    public Task<MeasurementCalibrationRequest?> GetCalibrationRequestAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => await handle.CalibrationRequest().ConfigureAwait(false) is { } request ? Ffi.ToPublic(request) : null, cancellationToken);

    /// <summary>Sets the scale of <paramref name="pages"/>; one report per page. Cancelling stops waiting; a page write already sent still lands.</summary>
    public Task<IReadOnlyList<ScaleChangeReport>> SetScaleAsync(MeasurementPageTarget pages, PdfMeasurement measure, MeasurementScaleChangeOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(measure);
        return viewer.RunCancellable(async token =>
            Reports(await handle.SetScale(Ffi.ToFfi(pages), Ffi.ToFfi(measure), Options(options), token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Calibrates a scale from a known length.</summary>
    public Task<IReadOnlyList<ScaleChangeReport>> CalibrateAsync(MeasurementCalibrateInput input, MeasurementCalibrateOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return viewer.RunCancellable(async token =>
            Reports(await handle.Calibrate(Ffi.ToFfi(input), Ffi.ToFfi(options ?? new MeasurementCalibrateOptions()), token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Changes the length unit of <paramref name="pages"/>' scale.</summary>
    public Task<IReadOnlyList<ScaleChangeReport>> SetUnitAsync(MeasurementPageTarget pages, string unit, MeasurementScaleChangeOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(unit);
        return viewer.RunCancellable(async token => Reports(await handle.SetUnit(Ffi.ToFfi(pages), unit, Options(options), token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Changes the area unit of <paramref name="pages"/>' scale.</summary>
    public Task<IReadOnlyList<ScaleChangeReport>> SetAreaUnitAsync(MeasurementPageTarget pages, string unit, MeasurementScaleChangeOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(unit);
        return viewer.RunCancellable(async token => Reports(await handle.SetAreaUnit(Ffi.ToFfi(pages), unit, Options(options), token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Changes the precision of <paramref name="pages"/>' scale.</summary>
    public Task<IReadOnlyList<ScaleChangeReport>> SetPrecisionAsync(MeasurementPageTarget pages, double precision, MeasurementScaleChangeOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return viewer.RunCancellable(async token =>
            Reports(await handle.SetPrecision(Ffi.ToFfi(pages), Ffi.Finite(precision, "precision"), Options(options), token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Applies the preset <paramref name="presetId"/> to <paramref name="pages"/>.</summary>
    public Task<IReadOnlyList<ScaleChangeReport>> SetPresetAsync(MeasurementPageTarget pages, string presetId, MeasurementScaleChangeOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(presetId);
        return viewer.RunCancellable(async token => Reports(await handle.SetPreset(Ffi.ToFfi(pages), presetId, Options(options), token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Removes this library's scale from <paramref name="pages"/>; another producer's stays.</summary>
    public Task<IReadOnlyList<ScaleChangeReport>> ClearScaleAsync(MeasurementPageTarget pages, MeasurementScaleChangeOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return viewer.RunCancellable(async token => Reports(await handle.ClearScale(Ffi.ToFfi(pages), Options(options), token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Asks the app for a calibration (<see cref="MeasurementEvent.CalibrationRequested"/>).</summary>
    public Task StartCalibrationAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.StartCalibration(), cancellationToken);

    /// <summary>Dismisses the open calibration request.</summary>
    public Task DismissCalibrationAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.DismissCalibration(), cancellationToken);

    /// <summary>Creates a measurement annotation through the annotation plugin and returns its annotation ref record as the engine's JSON.</summary>
    public Task<string> CreateMeasurementAsync(CreateMeasurementInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return viewer.RunCancellable(token => handle.CreateMeasurement(Ffi.ToFfi(input), token), cancellationToken);
    }

    /// <summary>Whether calibrating is allowed.</summary>
    public Task<bool> CanCalibrateAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanCalibrate(), cancellationToken);

    /// <summary>Reads <paramref name="page"/>'s viewports, so its scale is ready.</summary>
    public Task EnsureLoadedAsync(PageRef page, CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(token => handle.EnsureLoaded(Ffi.ToFfi(page), token), cancellationToken);

    /// <summary>
    /// Scale changes and calibration requests, subscribed when this returns.
    /// Ends when the document closes (at once if it already has), when the
    /// viewer is disposed, or when <paramref name="cancellationToken"/> fires.
    /// Each call is a new subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<MeasurementEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. Calls after it throw <see cref="PdfErrorCode.InstanceClosed"/>; streams end as they would otherwise.</summary>
    public void Dispose() => handle.Dispose();

    static GM.MeasurementScaleChangeOptions Options(MeasurementScaleChangeOptions? options) => Ffi.ToFfi(options ?? new MeasurementScaleChangeOptions());

    static IReadOnlyList<ScaleChangeReport> Reports(GM.ScaleChangeReport[] reports) => [.. reports.Select(Ffi.ToPublic)];

    sealed class Relay() : EventRelay<GM.MeasurementEvent, MeasurementEvent>(Ffi.ToPublic), GM.MeasurementEventListener
    {
        public void OnEvent(GM.MeasurementEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's measurement capability. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Measurement"/>.</summary>
    public async Task<MeasurementCapability> GetMeasurementAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Measurement(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
