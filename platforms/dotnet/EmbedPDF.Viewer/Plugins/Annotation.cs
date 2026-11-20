using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using GA = uniffi.epdf_plugin_annotation;
using GF = uniffi.epdf_ffi;

namespace EmbedPDF;

/// <summary>
/// A document's annotations. Get one with <see cref="PdfViewer.GetAnnotationAsync"/>;
/// the viewer needs <see cref="PdfViewerOptions.Interaction"/> and
/// <see cref="PdfViewerOptions.Annotation"/> (the settings JSON over the
/// defaults, <c>""</c> for the defaults). After its document closes, every
/// call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
/// <remarks>
/// Reads hand out <see cref="AnnotationInfo"/>. Creates, updates and tool
/// defaults take a typed draft or patch, or the engine's field bag as JSON for
/// a subtype or field the typed layer leaves out (popup, widget). Pointer
/// editing goes through the interaction hub:
/// <see cref="InteractionCapability.DispatchPointer"/> reaches the plugin's
/// handlers under the active tool.
/// JSON text, and a typed draft's or patch's strings, with an unpaired
/// surrogate are <see cref="PdfErrorCode.InvalidInput"/>, since they would
/// reach the engine as U+FFFD.
/// </remarks>
[SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Typed and JSON overloads of create and update, as in Swift.")]
public sealed class AnnotationCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GA.AnnotationHandle handle;

    internal AnnotationCapability(PdfViewer viewer, GA.AnnotationHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>A sub-capability's handle; after <see cref="Dispose"/> the call is <see cref="PdfErrorCode.InstanceClosed"/>, as every other member is.</summary>
    static T Sub<T>(Func<T> make)
    {
        try
        {
            return make();
        }
        catch (ObjectDisposedException error)
        {
            throw PdfException.Closed(error);
        }
    }

    /// <summary>The settings, including the per-tool field bags, as JSON.</summary>
    public Task<string> GetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.Settings(), cancellationToken);

    /// <summary>Merges a settings JSON object over the current settings.</summary>
    public Task UpdateSettingsAsync(string changesJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changesJson);
        return viewer.Run(() => handle.UpdateSettings(Ffi.WellFormed(changesJson, "settings")), cancellationToken);
    }

    /// <summary>Restores the registered settings.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetSettings(), cancellationToken);

    /// <summary>Selects, groups, rotates and restyles the selected annotations. A new handle each call; it keeps the viewer alive until disposed.</summary>
    public AnnotationSelectionCapability GetSelection() => new(viewer, Sub(handle.Selection));

    /// <summary>The authoring tools, their defaults and style panels.</summary>
    public AnnotationToolsCapability GetTools() => new(viewer, Sub(handle.Tools));

    /// <summary>Creation drafts, text editing, markup from text, stamps and placement.</summary>
    public AnnotationDraftsCapability GetDrafts() => new(viewer, Sub(handle.Drafts));

    /// <summary>Comment threads and the links attached to annotations.</summary>
    public AnnotationThreadsCapability GetThreads() => new(viewer, Sub(handle.Threads));

    /// <summary>An annotation, or <see langword="null"/> when the document doesn't have it.</summary>
    public async Task<AnnotationInfo?> GetAsync(AnnotationRef annotationRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return await viewer.Run(() => handle.Get(Ffi.ToFfi(annotationRef)), cancellationToken).ConfigureAwait(false) is { } r ? new AnnotationInfo(r) : null;
    }

    /// <summary>The annotations <paramref name="filter"/> picks; all of them when it is <see langword="null"/>.</summary>
    public async Task<IReadOnlyList<AnnotationInfo>> ListAsync(AnnotationFilter? filter = null, CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.List(Ffi.ToFfi(filter)), cancellationToken).ConfigureAwait(false)).Select(r => new AnnotationInfo(r))];

    /// <summary>The selected annotations.</summary>
    public async Task<IReadOnlyList<AnnotationInfo>> GetSelectedAsync(CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.Selected(), cancellationToken).ConfigureAwait(false)).Select(r => new AnnotationInfo(r))];

    /// <summary>
    /// A self-contained bundle, resource bytes encoded as <c>$bytes</c>.
    /// Both filters left out selects all; given ones contribute their union,
    /// and an empty list contributes nothing.
    /// </summary>
    public Task<string> ExportAsync(IEnumerable<AnnotationRef>? refs = null, IEnumerable<PageRef>? pages = null, CancellationToken cancellationToken = default)
    {
        var refList = refs is null ? null : Arguments.NoNullElements(refs, nameof(refs));
        var pageList = pages?.ToArray();
        return viewer.RunCancellable(
            token => handle.Export(refList is null ? null : [.. refList.Select(Ffi.ToFfi)], pageList is null ? null : [.. pageList.Select(Ffi.ToFfi)], token),
            cancellationToken);
    }

    /// <summary>Copies a bundle in, with the engine's <c>AnnotationImportOptions</c> JSON.</summary>
    public async Task<AnnotationImportResult> ImportAsync(string bundleJson, string optionsJson = "{}", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundleJson);
        ArgumentNullException.ThrowIfNull(optionsJson);
        return Ffi.ToPublic(await viewer.RunCancellable(token => handle.Import(Ffi.WellFormed(bundleJson, "bundle"), Ffi.WellFormed(optionsJson, "import options"), token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The topmost annotation under a page-space point.</summary>
    public async Task<AnnotationInfo?> HitTestAsync(PageRef page, PdfPoint point, CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.HitTestAt(Ffi.ToFfi(page), Ffi.ToFfi(point)), cancellationToken).ConfigureAwait(false) is { } r ? new AnnotationInfo(r) : null;

    /// <summary>Whether a change of this annotation is still on its way to the engine.</summary>
    public Task<bool> IsPendingAsync(AnnotationRef annotationRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return viewer.Run(() => handle.IsPending(Ffi.ToFfi(annotationRef)), cancellationToken);
    }

    /// <summary>Where the records mirror stands.</summary>
    public async Task<ResourceStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Status(), cancellationToken).ConfigureAwait(false));

    /// <summary>Re-reads every annotation from the engine and waits for it.</summary>
    public Task RefreshAsync(CancellationToken cancellationToken = default) => viewer.RunCancellable(token => handle.Refresh(token), cancellationToken);

    /// <summary>Waits until every load and page re-read in flight has settled.</summary>
    public Task WhenSyncedAsync(CancellationToken cancellationToken = default) => viewer.RunCancellable(token => handle.WhenSynced(token), cancellationToken);

    /// <summary>
    /// Creates an annotation from a typed draft and answers its ref once the
    /// engine confirmed it. A value the engine refuses is
    /// <see cref="PdfErrorCode.InvalidInput"/>, naming the field.
    /// </summary>
    public async Task<AnnotationRef> CreateAsync(AnnotationDraft draft, PageRef page, AnnotationCreateOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var json = draft.ToJson();
        return await CreateAsync(json, page, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an annotation from the engine's field bag for its subtype
    /// (<c>{"subtype": "square", "box": ...}</c>): the way to create a subtype,
    /// or set a field, the typed drafts don't cover.
    /// </summary>
    public async Task<AnnotationRef> CreateAsync(string fieldsJson, PageRef page, AnnotationCreateOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fieldsJson);
        return Ffi.ToPublic(await viewer.RunCancellable(
            token => handle.Create(Ffi.ToFfi(page), Ffi.WellFormed(fieldsJson, "fields"), options?.Tool?.Value, options?.Select ?? false, token), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Changes an annotation with the typed patch of its subtype. Members the patch leaves null keep their values; another subtype's fields are <see cref="PdfErrorCode.InvalidInput"/>.</summary>
    public async Task UpdateAsync(AnnotationRef annotationRef, AnnotationPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        ArgumentNullException.ThrowIfNull(patch);
        var json = patch.ToJson();
        await UpdateAsync(annotationRef, json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Changes an annotation with the engine's field bag: the way to change a field the typed patches don't cover.</summary>
    public Task UpdateAsync(AnnotationRef annotationRef, string patchJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        ArgumentNullException.ThrowIfNull(patchJson);
        return viewer.RunCancellable(token => handle.Update(Ffi.ToFfi(annotationRef), Ffi.WellFormed(patchJson, "patch"), token), cancellationToken);
    }

    /// <summary>Deletes an annotation.</summary>
    public Task DeleteAsync(AnnotationRef annotationRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return viewer.RunCancellable(token => handle.Delete(Ffi.ToFfi(annotationRef), token), cancellationToken);
    }

    /// <summary>
    /// Moves <paramref name="refs"/> together, in order, to <paramref name="position"/>
    /// among their page's annotations. They and the neighbour a position names
    /// must share a page (<see cref="PdfErrorCode.InvalidInput"/>), and the
    /// neighbour must exist (<see cref="PdfErrorCode.NotFound"/>).
    /// </summary>
    public Task ReorderAsync(IEnumerable<AnnotationRef> refs, AnnotationPosition position, CancellationToken cancellationToken = default)
    {
        var list = Arguments.NoNullElements(refs, nameof(refs));
        ArgumentNullException.ThrowIfNull(position);
        return viewer.RunCancellable(token => handle.Reorder([.. list.Select(Ffi.ToFfi)], position.Json, token), cancellationToken);
    }

    /// <summary>Whether this session may read annotations.</summary>
    public Task<bool> CanReadAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanRead(), cancellationToken);

    /// <summary>Whether this session may create annotations.</summary>
    public Task<bool> CanCreateAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanCreate(), cancellationToken);

    /// <summary>Whether this session may change an annotation.</summary>
    public Task<bool> CanUpdateAsync(AnnotationRef annotationRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return viewer.Run(() => handle.CanUpdate(Ffi.ToFfi(annotationRef)), cancellationToken);
    }

    /// <summary>Whether this session may delete an annotation.</summary>
    public Task<bool> CanDeleteAsync(AnnotationRef annotationRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotationRef);
        return viewer.Run(() => handle.CanDelete(Ffi.ToFfi(annotationRef)), cancellationToken);
    }

    /// <summary>
    /// Installs the picker a placing tool (a stamp, an attachment) asks for
    /// its file; <see langword="null"/> removes it. Without one, such a tool
    /// declines the click. A file picked after the user switched tools is
    /// dropped. The kernel holds the picker until it is replaced, so a picker
    /// that captures this capability keeps the viewer alive until then.
    /// </summary>
    public Task SetFilePickerAsync(IAnnotationFilePicker? picker, CancellationToken cancellationToken = default)
    {
        var adapter = picker is null ? null : new PickerAdapter(picker, viewer.Logger);
        return viewer.Run(() => handle.SetFilePicker(adapter), cancellationToken);
    }

    /// <summary>
    /// Confirmed changes, whoever made them, and selection, draft, editing and
    /// thread changes, subscribed when this returns. Ends when the document
    /// closes, the viewer is disposed or <paramref name="cancellationToken"/>
    /// fires. Each call is a new subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<AnnotationEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. It does not end the streams from <see cref="ObserveEvents"/>.</summary>
    public void Dispose() => handle.Dispose();

    sealed class Relay() : EventRelay<GA.AnnotationEvent, AnnotationEvent>(Ffi.ToPublic), GA.AnnotationEventListener
    {
        public void OnEvent(GA.AnnotationEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }

    /// <summary>
    /// The kernel calls <c>Pick</c> on its thread and awaits the task. The
    /// app's picker runs on the thread pool, and anything it throws declines
    /// the file, since an exception from a trait with no error type would
    /// panic the kernel.
    /// </summary>
    internal sealed class PickerAdapter(IAnnotationFilePicker picker, ILogger logger) : GA.AnnotationFilePicker
    {
        public Task<GA.AnnotationPickedFile?> Pick(GA.AnnotationFilePromptRequest request) => Task.Run(() => Answer(request));

        async Task<GA.AnnotationPickedFile?> Answer(GA.AnnotationFilePromptRequest request)
        {
            try
            {
                var picking = picker.PickAsync(Ffi.ToPublic(request));
                return picking is null ? null : await picking.ConfigureAwait(false) is { } file ? Ffi.ToFfi(file) : null;
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "The annotation file picker threw; the click is declined.");
                return null;
            }
        }
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's annotations. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Annotation"/>.</summary>
    public async Task<AnnotationCapability> GetAnnotationAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Annotation(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
