using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using GO = uniffi.epdf_plugin_form;

namespace EmbedPDF;

/// <summary>
/// A document's form: reading, filling, focus and the field editor. Get one
/// with <see cref="PdfViewer.GetFormAsync"/>; the viewer needs
/// <see cref="PdfViewerOptions.Interaction"/> and <see cref="PdfViewerOptions.Form"/>.
/// After its document closes, every call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
/// <remarks>
/// The field editor also takes the engine's JSON, for what the typed records
/// leave out: <see cref="CreateAsync(string, CancellationToken)"/> a draft,
/// <see cref="UpdateAsync(FormFieldRef, string, CancellationToken)"/> a patch,
/// <see cref="AddWidgetAsync(FormFieldRef, string, CancellationToken)"/> a
/// placement and <see cref="UpdateWidgetAsync"/> a widget patch.
/// JSON text, and a typed draft's or patch's strings, with an unpaired
/// surrogate are <see cref="PdfErrorCode.InvalidInput"/>, since they would
/// reach the engine as U+FFFD.
/// </remarks>
[SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Typed and JSON overloads, as in Swift.")]
public sealed class FormCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GO.FormHandle handle;

    internal FormCapability(PdfViewer viewer, GO.FormHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>The form settings.</summary>
    public async Task<FormSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Settings(), cancellationToken).ConfigureAwait(false));

    /// <summary>Changes the form settings; null members keep their value.</summary>
    public Task UpdateSettingsAsync(FormSettingsPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        return viewer.Run(() => handle.UpdateSettings(Ffi.ToFfi(patch)), cancellationToken);
    }

    /// <summary>Restores the registered settings.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetSettings(), cancellationToken);

    /// <summary>Whether every required field has a value.</summary>
    public async Task<FormValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var r = await viewer.Run(() => handle.Validate(), cancellationToken).ConfigureAwait(false);
        return new(r.Valid, Ffi.Fields(r.Missing));
    }

    /// <summary>Whether this session may restore a field's attribution.</summary>
    public Task<bool> CanRestoreAttributionAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanRestoreAttribution(), cancellationToken);

    /// <summary>Whether this session may write field scripts.</summary>
    public Task<bool> CanWriteScriptsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanWriteScripts(), cancellationToken);

    /// <summary>Focuses a field; <see langword="false"/> when it can't take focus.</summary>
    public Task<bool> FocusAsync(FormFieldRef field, FormFocusOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        return viewer.Run(() => handle.Focus(Ffi.ToFfi(field), Ffi.ToFfi(options)), cancellationToken);
    }

    /// <summary>Moves focus along the tab order; <see langword="false"/> when there is nowhere to go.</summary>
    public Task<bool> FocusNextAsync(FormFocusNextOptions? options = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.FocusNext(Ffi.ToFfi(options)), cancellationToken);

    /// <summary>The focused field.</summary>
    public async Task<FormField?> GetFocusedFieldAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublicOrNull(await viewer.Run(() => handle.FocusedField(), cancellationToken).ConfigureAwait(false));

    /// <summary>Moves a radio group's selection from <paramref name="widget"/> to the next button; <see langword="false"/> when it can't.</summary>
    public Task<bool> MoveInGroupAsync(AnnotationRef widget, bool backward = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return viewer.Run(() => handle.MoveInGroup(Ffi.ToFfi(widget), backward), cancellationToken);
    }

    /// <summary>
    /// Attaches the view's text input for a widget, which the plugin focuses.
    /// Dispose the registration to detach it. The kernel holds the target until
    /// then, so a target that captures this capability keeps the viewer alive.
    /// </summary>
    public async Task<Registration> AttachFocusTargetAsync(AnnotationRef widget, IFormFocusTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(target);
        var adapter = new FocusTargetAdapter(target, viewer.Mark, viewer.Logger);
        return new(await viewer.Run(() => handle.AttachFocusTarget(Ffi.ToFfi(widget), adapter), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Tells the plugin about a pointer or focus event on a widget, for its scripts.</summary>
    public Task NotifyWidgetEventAsync(FormFieldRef field, AnnotationRef widget, FormWidgetEvent widgetEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(widget);
        return viewer.Run(() => handle.NotifyWidgetEvent(Ffi.ToFfi(field), Ffi.ToFfi(widget), Ffi.ToFfi(widgetEvent)), cancellationToken);
    }

    /// <summary>The whole form, or <see langword="null"/> before it loads.</summary>
    public async Task<FormSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.Snapshot(), cancellationToken).ConfigureAwait(false) is { } s ? Ffi.ToPublic(s) : null;

    /// <summary>Where the form mirror stands.</summary>
    public async Task<ResourceStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Status(), cancellationToken).ConfigureAwait(false));

    /// <summary>Re-reads the form and waits for it.</summary>
    public Task RefreshAsync(CancellationToken cancellationToken = default) => viewer.RunCancellable(token => handle.Refresh(token), cancellationToken);

    /// <summary>A field.</summary>
    public async Task<FormField?> GetFieldAsync(FormFieldRef field, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        return Ffi.ToPublicOrNull(await viewer.Run(() => handle.Field(Ffi.ToFfi(field)), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The field a widget belongs to.</summary>
    public async Task<FormField?> GetFieldForWidgetAsync(AnnotationRef widget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return Ffi.ToPublicOrNull(await viewer.Run(() => handle.FieldForWidget(Ffi.ToFfi(widget)), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The fields <paramref name="filter"/> picks; all of them when it is <see langword="null"/>.</summary>
    public async Task<IReadOnlyList<FormField>> GetFieldsAsync(FormFilter? filter = null, CancellationToken cancellationToken = default) =>
        Ffi.Fields(await viewer.Run(() => handle.Fields(Ffi.ToFfi(filter)), cancellationToken).ConfigureAwait(false));

    /// <summary>A widget as the field editor reads it.</summary>
    public async Task<FormWidgetRecord?> GetWidgetAsync(AnnotationRef widget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return await viewer.Run(() => handle.Widget(Ffi.ToFfi(widget)), cancellationToken).ConfigureAwait(false) is { } w ? Ffi.ToPublic(w) : null;
    }

    /// <summary>A field's widgets as the field editor reads them.</summary>
    public async Task<IReadOnlyList<FormWidgetRecord>> GetWidgetsAsync(FormFieldRef field, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        return [.. (await viewer.Run(() => handle.FieldWidgets(Ffi.ToFfi(field)), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];
    }

    /// <summary>The field the editor has selected.</summary>
    public async Task<FormField?> GetSelectedFieldAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublicOrNull(await viewer.Run(() => handle.SelectedField(), cancellationToken).ConfigureAwait(false));

    /// <summary>Moves fields in the calculation order; answers the order after.</summary>
    public async Task<IReadOnlyList<FormFieldRef?>> ReorderCalculationsAsync(IEnumerable<FormFieldRef> fields, FormCalculationPosition position, CancellationToken cancellationToken = default)
    {
        var list = Arguments.NoNullElements(fields, nameof(fields));
        ArgumentNullException.ThrowIfNull(position);
        return [.. (await viewer.RunCancellable(token => handle.ReorderCalculations([.. list.Select(Ffi.ToFfi)], Ffi.ToFfi(position), token), cancellationToken).ConfigureAwait(false))
            .Select(r => r is null ? null : Ffi.ToPublic(r))];
    }

    /// <summary>Changes a widget with the engine's widget patch JSON.</summary>
    public async Task<FormWidgetRecord> UpdateWidgetAsync(AnnotationRef widget, string patchJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(patchJson);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.UpdateWidget(Ffi.ToFfi(widget), Ffi.WellFormed(patchJson, "widget patch"), token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes a widget; answers its field after, or <see langword="null"/> when the field went with it.</summary>
    public async Task<FormField?> DeleteWidgetAsync(AnnotationRef widget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return Ffi.ToPublicOrNull(await viewer.RunCancellable(token => handle.DeleteWidget(Ffi.ToFfi(widget), token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>A field's value.</summary>
    public async Task<FormValueInput?> GetValueAsync(FormFieldRef field, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        return await viewer.Run(() => handle.Value(Ffi.ToFfi(field)), cancellationToken).ConfigureAwait(false) is { } v ? Ffi.ToPublic(v) : null;
    }

    /// <summary>Every exportable value, as JSON.</summary>
    public Task<string> ExportValuesAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ExportValues(), cancellationToken);

    /// <summary>A page's widgets to fill.</summary>
    public async Task<IReadOnlyList<FormFillItem>> GetFillItemsAsync(PageRef page, CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.Widgets(Ffi.ToFfi(page)), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>Loads a page's widgets.</summary>
    public Task EnsureLoadedAsync(PageRef page, CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(token => handle.EnsureLoaded(Ffi.ToFfi(page), token), cancellationToken);

    /// <summary>The widget under a page-space point.</summary>
    public async Task<FormWidgetHit?> GetWidgetAtAsync(PageRef page, PdfPoint point, CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.WidgetAt(Ffi.ToFfi(page), point.X, point.Y), cancellationToken).ConfigureAwait(false) is { } h ? Ffi.ToPublic(h) : null;

    /// <summary>A page's box, page space.</summary>
    public async Task<PdfRect?> GetPageBoxAsync(PageRef page, CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.PageBox(Ffi.ToFfi(page)), cancellationToken).ConfigureAwait(false) is { } r ? Ffi.ToPublic(r) : null;

    /// <summary>Sets a field's value.</summary>
    public async Task<FormSetValueResult> SetValueAsync(FormFieldRef field, FormValueInput value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(value);
        var r = await viewer.RunCancellable(token => handle.SetValue(Ffi.ToFfi(field), Ffi.ToFfi(value), token), cancellationToken).ConfigureAwait(false);
        return new(Ffi.ToPublic(r.Field), (FormSetValueStatus)(int)r.Status);
    }

    /// <summary>Sets several values as one change.</summary>
    public async Task<FormBatchResult> SetValuesAsync(IEnumerable<FormValueEntry> entries, CancellationToken cancellationToken = default)
    {
        var list = Arguments.NoNullElements(entries, nameof(entries));
        return Ffi.ToPublic(await viewer.RunCancellable(
            token => handle.SetValues([.. list.Select(e => new GO.FormValueEntry(Ffi.ToFfi(e.FieldRef), Ffi.ToFfi(e.Value)))], token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Sets values from the JSON <see cref="ExportValuesAsync"/> writes.</summary>
    public async Task<FormBatchResult> ImportValuesAsync(string valuesJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(valuesJson);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.ImportValues(Ffi.WellFormed(valuesJson, "values"), token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Resets fields to their defaults, or every field; answers the fields reset.</summary>
    public async Task<IReadOnlyList<FormField>> ResetAsync(IEnumerable<FormFieldRef>? fields = null, CancellationToken cancellationToken = default)
    {
        var list = fields is null ? null : Arguments.NoNullElements(fields, nameof(fields));
        return Ffi.Fields(await viewer.RunCancellable(token => handle.Reset(list is null ? null : [.. list.Select(Ffi.ToFfi)], token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>A field's settings panel.</summary>
    public async Task<FormFieldPanelSettings?> GetFieldSettingsAsync(FormFieldRef field, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        return await viewer.Run(() => handle.FieldSettings(Ffi.ToFfi(field)), cancellationToken).ConfigureAwait(false) is { } s ? Ffi.ToPublic(s) : null;
    }

    /// <summary>Creates a field from the engine's field draft JSON.</summary>
    public async Task<FormField> CreateAsync(string draftJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draftJson);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.CreateDraft(Ffi.WellFormed(draftJson, "field draft"), token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Creates a field with its first widget.</summary>
    public async Task<FormField> CreateAsync(FormCreateFieldInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.Create(Ffi.ToFfi(input), token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Changes a field with the engine's field patch JSON.</summary>
    public async Task<FormField> UpdateAsync(FormFieldRef field, string patchJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(patchJson);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.UpdatePatch(Ffi.ToFfi(field), Ffi.WellFormed(patchJson, "field patch"), token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Changes a field.</summary>
    public async Task<FormField> UpdateAsync(FormFieldRef field, FormFieldPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(patch);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.Update(Ffi.ToFfi(field), Ffi.ToFfi(patch), token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Adds a widget from the engine's placement JSON.</summary>
    public async Task<FormWidgetAdded> AddWidgetAsync(FormFieldRef field, string placementJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(placementJson);
        var r = await viewer.RunCancellable(token => handle.AddWidgetPlacement(Ffi.ToFfi(field), Ffi.WellFormed(placementJson, "placement"), token), cancellationToken).ConfigureAwait(false);
        return new(Ffi.ToPublic(r.Field), r.WidgetJson);
    }

    /// <summary>Adds a widget to a field.</summary>
    public async Task<FormWidgetAdded> AddWidgetAsync(FormFieldRef field, FormWidgetPlacement placement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(placement);
        var r = await viewer.RunCancellable(token => handle.AddWidget(Ffi.ToFfi(field), Ffi.ToFfi(placement), token), cancellationToken).ConfigureAwait(false);
        return new(Ffi.ToPublic(r.Field), r.WidgetJson);
    }

    /// <summary>Deletes a field and its widgets.</summary>
    public Task DeleteAsync(FormFieldRef field, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        return viewer.RunCancellable(token => handle.Delete(Ffi.ToFfi(field), token), cancellationToken);
    }

    /// <summary>Removes one widget from a field; answers the field after.</summary>
    public async Task<FormField> RemoveWidgetAsync(AnnotationRef widget, FormFieldRef field, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(field);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.RemoveWidget(Ffi.ToFfi(field), Ffi.ToFfi(widget), token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>A self-contained bundle of fields, or of every field.</summary>
    public Task<string> ExportAsync(IEnumerable<FormFieldRef>? fields = null, CancellationToken cancellationToken = default)
    {
        var list = fields is null ? null : Arguments.NoNullElements(fields, nameof(fields));
        return viewer.RunCancellable(token => handle.Export(list is null ? null : [.. list.Select(Ffi.ToFfi)], token), cancellationToken);
    }

    /// <summary>Copies a bundle in, with the engine's import options JSON.</summary>
    public async Task<FormImportResult> ImportAsync(string bundleJson, string optionsJson = "{}", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundleJson);
        ArgumentNullException.ThrowIfNull(optionsJson);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.Import(Ffi.WellFormed(bundleJson, "bundle"), Ffi.WellFormed(optionsJson, "import options"), token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Repairs the form's structure, and with <paramref name="bakeAppearances"/> draws every appearance; answers a report as JSON.</summary>
    public Task<string> RepairAsync(bool bakeAppearances = false, CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(token => handle.Repair(bakeAppearances, token), cancellationToken);

    /// <summary>What a click on a widget does.</summary>
    public async Task<FormActivationResult> ActivateAsync(AnnotationRef widget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.ActivateWidget(Ffi.ToFfi(widget), token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Whether this session may read the form.</summary>
    public Task<bool> CanReadAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanRead(), cancellationToken);

    /// <summary>Whether this session may fill a field.</summary>
    public Task<bool> CanFillAsync(FormFieldRef field, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        return viewer.Run(() => handle.CanFill(Ffi.ToFfi(field)), cancellationToken);
    }

    /// <summary>Whether this session may edit the form's design.</summary>
    public Task<bool> CanDesignAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanDesign(), cancellationToken);

    /// <summary>
    /// Form events, subscribed when this returns. Ends when the document
    /// closes, the viewer is disposed or <paramref name="cancellationToken"/>
    /// fires. Each call is a new subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<FormEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. It does not end the streams from <see cref="ObserveEvents"/>.</summary>
    public void Dispose() => handle.Dispose();

    sealed class Relay() : EventRelay<GO.FormEvent, FormEvent>(Ffi.ToPublic), GO.FormEventListener
    {
        public void OnEvent(GO.FormEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }

    sealed class FocusTargetAdapter(IFormFocusTarget target, KernelMark mark, ILogger logger) : GO.FormFocusTarget
    {
        public void Focus(bool select) => KernelCallback.Run(mark, logger, "IFormFocusTarget.Focus", () => target.Focus(select));
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's form. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Form"/>.</summary>
    public async Task<FormCapability> GetFormAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Form(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
