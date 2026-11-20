using Microsoft.Extensions.Logging;
using GF = uniffi.epdf_ffi;
using GI = uniffi.epdf_plugin_interaction;

namespace EmbedPDF;

/// <summary>Where a pointer sample is in its gesture.</summary>
public enum PointerPhase
{
    /// <summary>The pointer went down.</summary>
    Down,
    /// <summary>The pointer moved.</summary>
    Move,
    /// <summary>The pointer went up.</summary>
    Up,
    /// <summary>The gesture was aborted.</summary>
    Cancel,
}

/// <summary>What produced a pointer sample.</summary>
public enum PointerKind
{
    /// <summary>A mouse.</summary>
    Mouse,
    /// <summary>A pen.</summary>
    Pen,
    /// <summary>A finger.</summary>
    Touch,
}

/// <summary>A gesture the platform recognized before the sample reached the hub.</summary>
public enum PointerGesture
{
    /// <summary>A long press.</summary>
    LongPress,
}

/// <summary>Modifier keys held during a pointer sample.</summary>
/// <param name="Shift">Shift.</param>
/// <param name="Alt">Alt or Option.</param>
/// <param name="Ctrl">Control.</param>
/// <param name="Meta">Command or the Windows key.</param>
public readonly record struct PointerModifiers(bool Shift = false, bool Alt = false, bool Ctrl = false, bool Meta = false);

/// <summary>The page under a pointer sample.</summary>
/// <param name="Page">The page.</param>
/// <param name="Point">The point in page space.</param>
public sealed record PointerPageHit(PageRef Page, PdfPoint Point)
{
    /// <summary>The page's display scale, if the caller knows it.</summary>
    public double? Scale { get; init; }

    /// <summary>The page's display rotation in degrees, if the caller knows it.</summary>
    public double? Rotation { get; init; }

    /// <summary>The view's zoom, if the caller knows it.</summary>
    public double? Zoom { get; init; }
}

/// <summary>One pointer event, as a view sends it to <see cref="InteractionCapability.DispatchPointer"/>.</summary>
/// <param name="Phase">Where it is in its gesture.</param>
/// <param name="Viewport">The point in viewport space.</param>
/// <param name="Modifiers">The modifier keys held.</param>
public sealed record PointerSample(PointerPhase Phase, PdfPoint Viewport, PointerModifiers Modifiers)
{
    /// <summary>The page under the pointer, if any.</summary>
    public PointerPageHit? Page { get; init; }

    /// <summary>The click count, for a multi-click.</summary>
    public int? ClickCount { get; init; }

    /// <summary>What produced the sample.</summary>
    public PointerKind? PointerType { get; init; }

    /// <summary>A recognized gesture.</summary>
    public PointerGesture? Gesture { get; init; }

    /// <summary>The lens it came through (a stage id).</summary>
    public string? Source { get; init; }
}

/// <summary>What one finger does on a touch screen while a tool is active.</summary>
public enum InteractionToolTouch
{
    /// <summary>It draws with the tool.</summary>
    Draw,
    /// <summary>It taps; a drag scrolls.</summary>
    Tap,
}

/// <summary>A tool in a document's interaction hub.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Cursor">Its cursor keyword.</param>
/// <param name="Enables">Behaviour tags (<c>text-select</c>, <c>scroll</c>, ...), each once, in order.</param>
public sealed record InteractionTool(ToolId Id, string Cursor, IReadOnlyList<string> Enables)
{
    /// <summary>The cursor over the viewport's page gaps; <see langword="null"/> is the arrow.</summary>
    public string? GapCursor { get; init; }

    /// <summary>What one finger does; <see langword="null"/> reads as <see cref="InteractionToolTouch.Tap"/>.</summary>
    public InteractionToolTouch? Touch { get; init; }
}

/// <summary>Interaction settings.</summary>
/// <param name="DefaultTool">The tool a freshly opened document starts with.</param>
public sealed record InteractionSettings(ToolId DefaultTool);

/// <summary>A change to <see cref="InteractionSettings"/>; <see langword="null"/> keeps a value.</summary>
public sealed record InteractionSettingsPatch
{
    /// <summary>New <see cref="InteractionSettings.DefaultTool"/>.</summary>
    public ToolId? DefaultTool { get; init; }
}

/// <summary>An event from <see cref="InteractionCapability.ObserveEvents"/>.</summary>
public abstract record InteractionEvent
{
    private protected InteractionEvent() { }

    /// <summary>The active tool changed.</summary>
    /// <param name="ToolId">The tool now active.</param>
    /// <param name="PreviousToolId">The tool active before.</param>
    public sealed record ToolChanged(ToolId ToolId, ToolId PreviousToolId) : InteractionEvent;

    /// <summary>A handler captured a gesture.</summary>
    /// <param name="ToolId">The tool active when the gesture began.</param>
    /// <param name="Page">The page it began on.</param>
    /// <param name="PointerType">What produced it.</param>
    public sealed record GestureStarted(ToolId ToolId, PageRef? Page, PointerKind PointerType) : InteractionEvent;

    /// <summary>A captured gesture completed.</summary>
    /// <inheritdoc cref="GestureStarted"/>
    public sealed record GestureEnded(ToolId ToolId, PageRef? Page, PointerKind PointerType) : InteractionEvent;

    /// <summary>A captured gesture was aborted.</summary>
    /// <inheritdoc cref="GestureStarted"/>
    public sealed record GestureCancelled(ToolId ToolId, PageRef? Page, PointerKind PointerType) : InteractionEvent;

    /// <summary>The resolved cursor changed.</summary>
    /// <param name="Cursor">The cursor keyword.</param>
    public sealed record CursorChanged(string Cursor) : InteractionEvent;
}

/// <summary>
/// A pointer handler. The hub routes each gesture to the first eligible
/// handler, by priority, whose <see cref="OnDown"/> returns
/// <see langword="true"/>; move and up then go to it until the up.
/// </summary>
/// <remarks>
/// The kernel calls every member on its own thread, synchronously, inside the
/// dispatch. Return quickly and never block waiting for the viewer: a call
/// into the viewer from here queues behind this dispatch, so start it and let
/// it finish later. <see cref="PdfViewer.Dispose"/> throws here; see
/// <see cref="PdfViewer.DisposeAsync"/>. An exception from
/// <see cref="OnDown"/> is reported as a <c>HostError</c> on
/// <see cref="PdfViewer.ObserveReportedErrors"/> and the gesture is not
/// captured. One from <see cref="EnabledFor"/> or <see cref="ClaimsTouch"/>
/// reads as <see langword="false"/>; one from any other member is logged and
/// dropped.
/// </remarks>
public interface IInteractionHandler
{
    /// <summary>Whether the handler takes gestures while <paramref name="tool"/> is active; usually whether the tool enables its tag.</summary>
    bool EnabledFor(InteractionTool tool);

    /// <summary>A pointer went down. <see langword="true"/> captures the gesture.</summary>
    bool OnDown(PointerSample sample);

    /// <summary>The captured pointer moved.</summary>
    void OnMove(PointerSample sample) { }

    /// <summary>The captured pointer went up.</summary>
    void OnUp(PointerSample sample) { }

    /// <summary>The captured gesture was aborted. By default it is <see cref="OnUp"/>.</summary>
    void OnCancel(PointerSample sample) => OnUp(sample);

    /// <summary>The pointer moved with no gesture active: cursor feedback only.</summary>
    void OnHover(PointerSample sample) { }

    /// <summary>Touch consent: whether to claim this contact as a tool gesture. A pure read.</summary>
    bool ClaimsTouch(PointerSample sample) => false;
}

/// <summary>
/// A document's pointer hub: the active tool, the cursor, and the handlers
/// gestures route to. Get one with <see cref="PdfViewer.GetInteractionAsync"/>;
/// the viewer needs <see cref="PdfViewerOptions.Interaction"/>. After its
/// document closes, every call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
public sealed class InteractionCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GI.InteractionHandle handle;

    internal InteractionCapability(PdfViewer viewer, GI.InteractionHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Current settings, shared by every document using this plugin registration.</summary>
    public async Task<InteractionSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        new((await viewer.Run(() => handle.Settings(), cancellationToken).ConfigureAwait(false)).DefaultTool);

    /// <summary>Merges the supplied fields; omission preserves them.</summary>
    public Task UpdateSettingsAsync(InteractionSettingsPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        return viewer.Run(() => handle.UpdateSettings(new GI.InteractionSettingsPatch(patch.DefaultTool?.Value)), cancellationToken);
    }

    /// <summary>Restores the values supplied at registration.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetSettings(), cancellationToken);

    /// <summary>Whether a hover claim holds the cursor.</summary>
    public Task<bool> HasCursorClaimAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.HasCursorClaim(), cancellationToken);

    /// <summary>The armed tool. An id nobody registered reads back as a neutral tool with no tags.</summary>
    public async Task<InteractionTool> GetActiveToolAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.ActiveTool(), cancellationToken).ConfigureAwait(false));

    /// <summary>The armed tool's id. <see cref="GetActiveToolAsync"/> gives the whole tool.</summary>
    public async Task<ToolId> GetActiveToolIdAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.ActiveToolId(), cancellationToken).ConfigureAwait(false);

    /// <summary>The tool the hub falls back to, <see cref="ToolId.Pointer"/> unless the settings name another.</summary>
    public async Task<ToolId> GetDefaultToolIdAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.DefaultToolId(), cancellationToken).ConfigureAwait(false);

    /// <summary>Every registered tool.</summary>
    public async Task<IReadOnlyList<InteractionTool>> GetToolsAsync(CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.Tools(), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>A registered tool, or <see langword="null"/>.</summary>
    public async Task<InteractionTool?> GetToolAsync(ToolId id, CancellationToken cancellationToken = default)
    {
        var tool = await viewer.Run(() => handle.Tool(Ffi.Wire(id)), cancellationToken).ConfigureAwait(false);
        return tool is null ? null : Ffi.ToPublic(tool);
    }

    /// <summary>Whether a tool is registered.</summary>
    public Task<bool> HasToolAsync(ToolId id, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.HasTool(Ffi.Wire(id)), cancellationToken);

    /// <summary>Whether the active tool enables a behaviour tag such as <c>text-select</c>.</summary>
    public Task<bool> ActiveToolEnablesAsync(string behavior, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        return viewer.Run(() => handle.ActiveToolEnables(behavior), cancellationToken);
    }

    /// <summary>Arms a tool. Throws <see cref="PdfErrorCode.NotFound"/> for an id nobody registered.</summary>
    public Task ActivateToolAsync(ToolId id, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.ActivateTool(Ffi.Wire(id)), cancellationToken);

    /// <summary>Arms the default tool.</summary>
    public Task ActivateDefaultToolAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ActivateDefaultTool(), cancellationToken);

    /// <summary>Arms a tool for now (hold to pan); <see cref="PopToolAsync"/> restores the last one.</summary>
    public Task PushToolAsync(ToolId id, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.PushTool(Ffi.Wire(id)), cancellationToken);

    /// <summary>Restores the tool active before the last push. Nothing when the stack is empty.</summary>
    public Task PopToolAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.PopTool(), cancellationToken);

    /// <summary>Replaces a tool's cursors by name; <see langword="null"/> brings the tool's own back.</summary>
    public Task SetToolCursorAsync(ToolId id, IReadOnlyDictionary<string, string>? cursors, CancellationToken cancellationToken = default)
    {
        if (cursors is not null) Arguments.NoNullValues(cursors, nameof(cursors));
        return viewer.Run(() => handle.SetToolCursor(Ffi.Wire(id), cursors?.ToDictionary(pair => pair.Key, pair => pair.Value)), cancellationToken);
    }

    /// <summary>
    /// Registers a tool. Throws <see cref="PdfErrorCode.Conflict"/> for an id
    /// already registered unless <paramref name="replace"/>.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <param name="replace">Whether to replace a tool with the same id.</param>
    /// <param name="permission">
    /// The tool's <c>canUse</c>: whether the user may use it now. Without one
    /// the tool may always be used. The kernel asks it on its own thread
    /// whenever <see cref="CanUseToolAsync"/> is read, so answer from state you
    /// already hold; waiting on a viewer call inside it deadlocks. A throw
    /// reads as <see langword="false"/>.
    /// </param>
    /// <param name="cancellationToken">Checked before the call is posted.</param>
    public async Task<Registration> RegisterToolAsync(InteractionTool tool, bool replace = false, Func<bool>? permission = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tool);
        var ffi = Ffi.ToFfi(tool);
        var adapter = permission is null ? null : new PermissionAdapter(permission, viewer.Mark, viewer.Logger);
        return new(await viewer.Run(() => handle.RegisterTool(ffi, replace, adapter), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Whether the user may use a tool: what its permission says,
    /// <see langword="true"/> for a tool registered without one,
    /// <see langword="false"/> for an id nobody registered.
    /// </summary>
    public Task<bool> CanUseToolAsync(ToolId id, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.CanUseTool(Ffi.Wire(id)), cancellationToken);

    /// <summary>
    /// Adds a pointer handler. See <see cref="IInteractionHandler"/> for the
    /// thread it is called on. The viewer keeps it until the registration is
    /// unregistered or the document closes.
    /// </summary>
    /// <param name="handler">The handler.</param>
    /// <param name="id">Its id.</param>
    /// <param name="priority">Fixed here; higher wins a gesture.</param>
    /// <param name="source">Scopes it to one lens (a stage id).</param>
    /// <param name="cancellationToken">Checked before the call is posted.</param>
    public async Task<Registration> RegisterHandlerAsync(IInteractionHandler handler, string id, double priority, string? source = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(id);
        var adapter = new HandlerAdapter(handler, viewer.Mark, viewer.Logger);
        return new(await viewer.Run(() => handle.RegisterHandler(id, priority, source, adapter), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Pushes a cursor claim, or clears it with a <see langword="null"/> cursor. Higher priority wins.</summary>
    public Task ClaimCursorAsync(string token, string? cursor, double priority = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        return viewer.Run(() => handle.ClaimCursor(token, cursor, priority), cancellationToken);
    }

    /// <summary>The resolved cursor keyword.</summary>
    public Task<string> GetCursorAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.Cursor(), cancellationToken);

    /// <summary>Touch pre-flight: whether any eligible handler would claim this contact.</summary>
    public Task<bool> WouldClaimTouchAsync(PointerSample sample, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sample);
        return viewer.Run(() => handle.WouldClaimTouch(Ffi.ToFfi(sample)), cancellationToken);
    }

    /// <summary>
    /// Sends a pointer sample. Returns at once; samples run on the kernel
    /// thread in the order they were sent. After the document closes or the
    /// viewer is disposed, samples are dropped.
    /// </summary>
    public void DispatchPointer(PointerSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        try
        {
            handle.DispatchPointer(Ffi.ToFfi(sample));
        }
        catch (Exception error) when (error is ObjectDisposedException or GF.ViewerException)
        {
            // The document or the viewer has gone: the sample has nowhere to go.
        }
    }

    /// <summary>
    /// Tool, gesture and cursor changes, subscribed when this returns. Ends
    /// when the document closes (at once if it already has), when the viewer
    /// is disposed, or when <paramref name="cancellationToken"/> fires. Each
    /// call is a new subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<InteractionEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. Calls after it throw <see cref="PdfErrorCode.InstanceClosed"/>.</summary>
    public void Dispose() => handle.Dispose();

    sealed class Relay() : EventRelay<GI.InteractionEvent, InteractionEvent>(Ffi.ToPublic), GI.InteractionEventListener
    {
        public void OnEvent(GI.InteractionEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }

    /// <summary>Called on the kernel thread, marked; app exceptions never reach the kernel unmapped.</summary>
    sealed class HandlerAdapter(IInteractionHandler app, KernelMark mark, ILogger logger) : GI.InteractionHandler
    {
        public bool EnabledFor(GI.InteractionTool tool) =>
            KernelCallback.Run(mark, logger, "An interaction handler's EnabledFor", () => app.EnabledFor(Ffi.ToPublic(tool)), _ => false);

        public bool OnDown(GF.PointerSample sample)
        {
            Exception? failure = null;
            var captured = KernelCallback.Run(mark, logger, "An interaction handler's OnDown", () => app.OnDown(Ffi.ToPublic(sample)), error =>
            {
                failure = error;
                return false;
            });
            if (failure is not null) throw new GF.HostException.Failed(failure.Message);
            return captured;
        }

        public void OnMove(GF.PointerSample sample) => KernelCallback.Run(mark, logger, "An interaction handler's OnMove", () => app.OnMove(Ffi.ToPublic(sample)));

        public void OnUp(GF.PointerSample sample) => KernelCallback.Run(mark, logger, "An interaction handler's OnUp", () => app.OnUp(Ffi.ToPublic(sample)));

        public void OnCancel(GF.PointerSample sample) => KernelCallback.Run(mark, logger, "An interaction handler's OnCancel", () => app.OnCancel(Ffi.ToPublic(sample)));

        public void OnHover(GF.PointerSample sample) => KernelCallback.Run(mark, logger, "An interaction handler's OnHover", () => app.OnHover(Ffi.ToPublic(sample)));

        public bool ClaimsTouch(GF.PointerSample sample) =>
            KernelCallback.Run(mark, logger, "An interaction handler's ClaimsTouch", () => app.ClaimsTouch(Ffi.ToPublic(sample)), _ => false);
    }

    sealed class PermissionAdapter(Func<bool> canUse, KernelMark mark, ILogger logger) : GI.ToolPermission
    {
        public bool CanUse() => KernelCallback.Run(mark, logger, "A tool permission", canUse, _ => false);
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's pointer hub. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Interaction"/>.</summary>
    public async Task<InteractionCapability> GetInteractionAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Interaction(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
