using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using GS = uniffi.epdf_plugin_shell;

namespace EmbedPDF;

/// <summary>A named surface (panel, sheet, inspector) and whether it is open.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Open">Whether it is open.</param>
/// <param name="Exclusive">Its tag: opening a surface with a tag closes every other open surface with the same tag.</param>
/// <param name="PropsJson">Its props, a JSON object, or <see langword="null"/> when it has none.</param>
/// <param name="FocusRequest">How many opens asked for focus since it opened; 0 once it closes. A rise is the moment to move the cursor into it.</param>
public sealed record ShellSurface(string Id, bool Open, string? Exclusive, string? PropsJson, double FocusRequest)
{
    /// <summary>The props decoded as <typeparamref name="TProps"/>, or the default when the surface has none.</summary>
    /// <exception cref="JsonException">The props don't decode as <typeparamref name="TProps"/>.</exception>
    public TProps? GetProps<TProps>(JsonTypeInfo<TProps> typeInfo) => ShellCapability.Decode(PropsJson, typeInfo);
}

/// <summary>A surface in a <see cref="ShellSnapshot"/>.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Open">Whether it is open.</param>
/// <param name="Exclusive">Its tag.</param>
/// <param name="PropsJson">Its props, a JSON object, or <see langword="null"/>.</param>
public sealed record ShellSnapshotSurface(string Id, bool Open, string? Exclusive, string? PropsJson);

/// <summary>Everything a document's shell holds, for persist and restore.</summary>
/// <param name="Surfaces">Every surface it knows.</param>
/// <param name="OpenMenus">The open menus, in opening order.</param>
public sealed record ShellSnapshot(IReadOnlyList<ShellSnapshotSurface> Surfaces, IReadOnlyList<string> OpenMenus);

/// <summary>An event from <see cref="ShellCapability.ObserveEvents"/>.</summary>
public abstract record ShellEvent
{
    private protected ShellEvent() { }

    /// <summary>A surface opened.</summary>
    /// <param name="Id">The surface.</param>
    /// <param name="PropsJson">The props it opened with.</param>
    public sealed record SurfaceOpened(string Id, string? PropsJson) : ShellEvent
    {
        /// <summary>The props decoded as <typeparamref name="TProps"/>, or the default when there are none.</summary>
        /// <exception cref="JsonException">The props don't decode as <typeparamref name="TProps"/>.</exception>
        public TProps? GetProps<TProps>(JsonTypeInfo<TProps> typeInfo) => ShellCapability.Decode(PropsJson, typeInfo);
    }

    /// <summary>A surface closed.</summary>
    /// <param name="Id">The surface.</param>
    public sealed record SurfaceClosed(string Id) : ShellEvent;

    /// <summary>A menu opened.</summary>
    /// <param name="Id">The menu.</param>
    public sealed record MenuOpened(string Id) : ShellEvent;

    /// <summary>A menu closed.</summary>
    /// <param name="Id">The menu.</param>
    public sealed record MenuClosed(string Id) : ShellEvent;
}

/// <summary>
/// Which named surfaces (panels, sheets, inspectors) and menus are open in
/// one document. Pure state: your views decide what a surface looks like and
/// bind to this. Get one with <see cref="PdfViewer.GetShellAsync"/>; the viewer
/// needs <see cref="PdfViewerOptions.Shell"/>. A surface's props are a JSON
/// object, given as text or as any value a <see cref="JsonTypeInfo{T}"/>
/// encodes to an object. After its document closes, every call throws
/// <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
[SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Props as JSON text or as a typed value, as in Swift.")]
public sealed class ShellCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GS.ShellHandle handle;

    internal ShellCapability(PdfViewer viewer, GS.ShellHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Whether a surface is open.</summary>
    public Task<bool> IsOpenAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(() => handle.IsOpen(id), cancellationToken);
    }

    /// <summary>
    /// A surface; <see langword="null"/> for one never opened. Ids that name
    /// a member every JavaScript object has (<c>constructor</c>, <c>toString</c>)
    /// are found, closed, with no tag or props, as in the web viewer.
    /// </summary>
    public async Task<ShellSurface?> GetSurfaceAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return await viewer.Run(() => handle.Surface(id), cancellationToken).ConfigureAwait(false) is { } surface ? Ffi.ToPublic(surface) : null;
    }

    /// <summary>The open surfaces.</summary>
    public async Task<IReadOnlyList<ShellSurface>> ListOpenSurfacesAsync(CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.OpenSurfaces(), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>Opens a surface. A tagged open closes every other open surface with the same tag. Props that are not a JSON object throw <see cref="PdfErrorCode.InvalidInput"/>.</summary>
    public Task OpenAsync(string id, string? exclusive = null, string? propsJson = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(() =>
        {
            var options = new GS.ShellOpenOptions(exclusive, propsJson);
            return handle.OpenSurface(id, options);
        }, cancellationToken);
    }

    /// <summary><see cref="OpenAsync(string, string?, string?, CancellationToken)"/> with typed props, encoded through <paramref name="typeInfo"/>.</summary>
    public Task OpenAsync<TProps>(string id, TProps props, JsonTypeInfo<TProps> typeInfo, string? exclusive = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(typeInfo);
        return viewer.Run(() => handle.OpenSurface(id, new GS.ShellOpenOptions(exclusive, Encode(props, typeInfo))), cancellationToken);
    }

    /// <summary>Closes a surface.</summary>
    public Task CloseAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(() => handle.CloseSurface(id), cancellationToken);
    }

    /// <summary>Opens a closed surface or closes an open one.</summary>
    public Task ToggleAsync(string id, string? exclusive = null, string? propsJson = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(() =>
        {
            var options = new GS.ShellOpenOptions(exclusive, propsJson);
            return handle.ToggleSurface(id, options);
        }, cancellationToken);
    }

    /// <summary><see cref="ToggleAsync(string, string?, string?, CancellationToken)"/> with typed props.</summary>
    public Task ToggleAsync<TProps>(string id, TProps props, JsonTypeInfo<TProps> typeInfo, string? exclusive = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(typeInfo);
        return viewer.Run(() => handle.ToggleSurface(id, new GS.ShellOpenOptions(exclusive, Encode(props, typeInfo))), cancellationToken);
    }

    /// <summary>Changes a surface's props without reopening it.</summary>
    public Task UpdatePropsAsync(string id, string propsJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(propsJson);
        return viewer.Run(() => handle.UpdateSurfaceProps(id, propsJson), cancellationToken);
    }

    /// <summary><see cref="UpdatePropsAsync(string, string, CancellationToken)"/> with typed props.</summary>
    public Task UpdatePropsAsync<TProps>(string id, TProps props, JsonTypeInfo<TProps> typeInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(typeInfo);
        return viewer.Run(() => handle.UpdateSurfaceProps(id, Encode(props, typeInfo)), cancellationToken);
    }

    /// <summary>Closes every surface.</summary>
    public Task CloseAllAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CloseAllSurfaces(), cancellationToken);

    /// <summary>Whether a menu is open.</summary>
    public Task<bool> IsMenuOpenAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(() => handle.IsMenuOpen(id), cancellationToken);
    }

    /// <summary>The open menus, in opening order; the last is topmost.</summary>
    public async Task<IReadOnlyList<string>> ListOpenMenusAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.OpenMenus(), cancellationToken).ConfigureAwait(false);

    /// <summary>Opens a menu.</summary>
    public Task OpenMenuAsync(string id, CancellationToken cancellationToken = default) => OnMenu(id, handle.OpenMenu, cancellationToken);

    /// <summary>Closes a menu.</summary>
    public Task CloseMenuAsync(string id, CancellationToken cancellationToken = default) => OnMenu(id, handle.CloseMenu, cancellationToken);

    /// <summary>Opens a closed menu or closes an open one.</summary>
    public Task ToggleMenuAsync(string id, CancellationToken cancellationToken = default) => OnMenu(id, handle.ToggleMenu, cancellationToken);

    /// <summary>Closes every menu.</summary>
    public Task CloseAllMenusAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CloseAllMenus(), cancellationToken);

    /// <summary>Everything this document's shell holds, for persist and restore.</summary>
    public async Task<ShellSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Snapshot(), cancellationToken).ConfigureAwait(false));

    /// <summary>Restores a snapshot.</summary>
    public Task ApplySnapshotAsync(ShellSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return viewer.Run(() =>
        {
            var ffi = Ffi.ToFfi(snapshot);
            return handle.ApplySnapshot(ffi);
        }, cancellationToken);
    }

    /// <summary>
    /// Surface and menu events, subscribed when this returns. Ends when the
    /// document closes (at once if it already has), when the viewer is
    /// disposed, or when <paramref name="cancellationToken"/> fires. Each call
    /// is a new subscription; enumerate it once. A stream that is never
    /// enumerated still holds a native listener until its document closes, the viewer is disposed or
    /// <paramref name="cancellationToken"/> fires.
    /// </summary>
    public IAsyncEnumerable<ShellEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>
    /// Releases the native handle. Calls after it throw
    /// <see cref="PdfErrorCode.InstanceClosed"/>. It does not end the
    /// streams from <see cref="ObserveEvents"/>;
    /// a stream still ends when its document closes, the viewer is disposed or
    /// its token fires.
    /// </summary>
    public void Dispose() => handle.Dispose();

    Task OnMenu(string id, Func<string, Task> call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(() => call(id), cancellationToken);
    }

    static string Encode<TProps>(TProps props, JsonTypeInfo<TProps> typeInfo)
    {
        try
        {
            return JsonSerializer.Serialize(props, typeInfo);
        }
        catch (Exception error) when (error is JsonException or NotSupportedException or InvalidOperationException)
        {
            throw new PdfException("EncodingError", PdfErrorCode.InvalidInput, error.Message, error);
        }
    }

    internal static TProps? Decode<TProps>(string? json, JsonTypeInfo<TProps> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return json is null ? default : JsonSerializer.Deserialize(json, typeInfo);
    }

    sealed class Relay() : EventRelay<GS.ShellEvent, ShellEvent>(Ffi.ToPublic), GS.ShellEventListener
    {
        public void OnEvent(GS.ShellEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's shell capability. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Shell"/>.</summary>
    public async Task<ShellCapability> GetShellAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Shell(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
