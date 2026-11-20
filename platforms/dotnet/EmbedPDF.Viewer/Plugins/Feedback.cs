using GI = uniffi.epdf_plugin_interaction;

namespace EmbedPDF;

/// <summary>How strong an impact feels.</summary>
public enum FeedbackImpactWeight
{
    /// <summary>Light.</summary>
    Light,
    /// <summary>Medium.</summary>
    Medium,
    /// <summary>Heavy.</summary>
    Heavy,
}

/// <summary>What a notification reports.</summary>
public enum FeedbackNotifyKind
{
    /// <summary>Something succeeded.</summary>
    Success,
    /// <summary>A warning.</summary>
    Warning,
    /// <summary>Something failed.</summary>
    Error,
}

/// <summary>
/// Haptics, as the platform provides them. Desktop platforms have none, so
/// the default (<see cref="FeedbackConfig.Provider"/> left
/// <see langword="null"/>) does nothing.
/// </summary>
/// <remarks>
/// The kernel calls every member on its own thread, synchronously: return at
/// once, and hop to a UI thread yourself if the platform needs one. Never
/// block waiting for the viewer here: a viewer call queues behind this
/// callback, so waiting for it deadlocks. <see cref="PdfViewer.Dispose"/>
/// throws here. An exception is logged and
/// dropped.
/// </remarks>
public interface IPlatformFeedback
{
    /// <summary>A selection changed.</summary>
    void Selection();

    /// <summary>An impact; <see langword="null"/> is the platform's default weight.</summary>
    void Impact(FeedbackImpactWeight? weight);

    /// <summary>A notification.</summary>
    void Notify(FeedbackNotifyKind kind);
}

/// <summary>Installs the feedback plugin: haptics for the plugins that use them.</summary>
public sealed record FeedbackConfig
{
    /// <summary>
    /// Where feedback goes. <see langword="null"/> does nothing, which suits a desktop. The viewer holds it until it is disposed, so one that captures the
    /// viewer or a capability keeps an abandoned viewer from being collected;
    /// dispose the viewer, or keep the viewer out of it.
    /// </summary>
    public IPlatformFeedback? Provider { get; init; }

    internal GI.FeedbackConfig ToFfi(PluginContext context) =>
        new(Provider is null ? new SilentFeedback() : new FeedbackAdapter(Provider, context));

    sealed class SilentFeedback : GI.PlatformFeedback
    {
        public void Selection() { }
        public void Impact(GI.FeedbackImpactWeight? weight) { }
        public void Notify(GI.FeedbackNotifyKind kind) { }
    }

    /// <summary>Called on the kernel thread, marked; what the provider throws is logged.</summary>
    sealed class FeedbackAdapter(IPlatformFeedback app, PluginContext context) : GI.PlatformFeedback
    {
        public void Selection() => KernelCallback.Run(context.Mark, context.Logger, "A feedback provider's Selection", app.Selection);

        public void Impact(GI.FeedbackImpactWeight? weight) =>
            KernelCallback.Run(context.Mark, context.Logger, "A feedback provider's Impact",
                () => app.Impact(weight is { } w ? Ffi.ToPublic(w) : null));

        public void Notify(GI.FeedbackNotifyKind kind) =>
            KernelCallback.Run(context.Mark, context.Logger, "A feedback provider's Notify", () => app.Notify(Ffi.ToPublic(kind)));
    }
}

/// <summary>
/// The viewer's haptics. Get it with <see cref="PdfViewer.GetFeedbackAsync"/>;
/// the viewer needs <see cref="PdfViewerOptions.Feedback"/>. It has no event
/// stream: what it does reaches the <see cref="IPlatformFeedback"/>. After
/// the viewer is disposed, every call throws
/// <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
public sealed class FeedbackCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GI.FeedbackHandle handle;

    internal FeedbackCapability(PdfViewer viewer, GI.FeedbackHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Selection feedback.</summary>
    public Task SelectionAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.Selection(), cancellationToken);

    /// <summary>Impact feedback; <see langword="null"/> is the platform's default weight.</summary>
    public Task ImpactAsync(FeedbackImpactWeight? weight = null, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.Impact(weight is { } w ? Ffi.ToFfi(w) : null), cancellationToken);

    /// <summary>Notification feedback.</summary>
    public Task NotifyAsync(FeedbackNotifyKind kind, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.Notify(Ffi.ToFfi(kind)), cancellationToken);

    /// <summary>Releases the native handle. Calls after it throw <see cref="PdfErrorCode.InstanceClosed"/>.</summary>
    public void Dispose() => handle.Dispose();
}

internal static partial class Ffi
{
    internal static GI.FeedbackImpactWeight ToFfi(FeedbackImpactWeight weight) => weight switch
    {
        FeedbackImpactWeight.Light => GI.FeedbackImpactWeight.Light,
        FeedbackImpactWeight.Medium => GI.FeedbackImpactWeight.Medium,
        FeedbackImpactWeight.Heavy => GI.FeedbackImpactWeight.Heavy,
        _ => throw Undefined(weight, "impact weight"),
    };

    internal static GI.FeedbackNotifyKind ToFfi(FeedbackNotifyKind kind) => kind switch
    {
        FeedbackNotifyKind.Success => GI.FeedbackNotifyKind.Success,
        FeedbackNotifyKind.Warning => GI.FeedbackNotifyKind.Warning,
        FeedbackNotifyKind.Error => GI.FeedbackNotifyKind.Error,
        _ => throw Undefined(kind, "notify kind"),
    };

    internal static FeedbackImpactWeight ToPublic(GI.FeedbackImpactWeight weight) => (FeedbackImpactWeight)(int)weight;

    internal static FeedbackNotifyKind ToPublic(GI.FeedbackNotifyKind kind) => (FeedbackNotifyKind)(int)kind;
}

public sealed partial class PdfViewer
{
    /// <summary>The viewer's feedback capability. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Feedback"/>.</summary>
    public async Task<FeedbackCapability> GetFeedbackAsync(CancellationToken cancellationToken = default) =>
        new(this, await CallAsync(h => h.Feedback(), cancellationToken).ConfigureAwait(false));
}
