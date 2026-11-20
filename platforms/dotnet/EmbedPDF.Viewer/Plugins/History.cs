using GH = uniffi.epdf_plugin_history;

namespace EmbedPDF;

/// <summary>One value a history label interpolates.</summary>
public abstract record HistoryLabelValue
{
    private protected HistoryLabelValue() { }

    /// <summary>Text.</summary>
    /// <param name="Value">The text.</param>
    public sealed record Text(string Value) : HistoryLabelValue;

    /// <summary>A number.</summary>
    /// <param name="Value">The number.</param>
    public sealed record Number(double Value) : HistoryLabelValue;
}

/// <summary>A change's label: a localization key and the values it interpolates.</summary>
/// <param name="Key">The localization key.</param>
/// <param name="Values">The values, by name.</param>
public sealed record HistoryChangeLabel(string Key, IReadOnlyDictionary<string, HistoryLabelValue> Values);

/// <summary>History settings.</summary>
/// <param name="Limit">How many changes undo keeps.</param>
public sealed record HistorySettings(double Limit);

/// <summary>A change to <see cref="HistorySettings"/>; <see langword="null"/> keeps a value.</summary>
public sealed record HistorySettingsPatch
{
    /// <summary>New <see cref="HistorySettings.Limit"/>.</summary>
    public double? Limit { get; init; }
}

/// <summary>Why an undo or redo did not land.</summary>
public enum HistoryUndoFailure
{
    /// <summary>Nothing to undo or redo by the time it ran.</summary>
    Unavailable,
    /// <summary>The engine refused it; the event's error says why.</summary>
    Refused,
}

/// <summary>An event from <see cref="HistoryCapability.ObserveEvents"/>.</summary>
public abstract record HistoryEvent
{
    private protected HistoryEvent() { }

    /// <summary>An undo or redo landed.</summary>
    /// <param name="Label">The change undone or redone.</param>
    /// <param name="Redo">Whether it was a redo.</param>
    /// <param name="Skipped">Entries skipped on the way, because they no longer applied.</param>
    public sealed record Undone(HistoryChangeLabel Label, bool Redo, long Skipped) : HistoryEvent;

    /// <summary>An undo or redo did not land.</summary>
    /// <param name="Label">The change it tried.</param>
    /// <param name="Redo">Whether it was a redo.</param>
    /// <param name="Reason">Why it failed.</param>
    /// <param name="Error">The engine's error.</param>
    public sealed record UndoFailed(HistoryChangeLabel Label, bool Redo, HistoryUndoFailure Reason, PluginErrorInfo Error) : HistoryEvent;

    /// <summary>The settings changed.</summary>
    /// <param name="Settings">The settings after the change.</param>
    /// <param name="Changed">The changed fields' names.</param>
    public sealed record SettingsChanged(HistorySettings Settings, IReadOnlyList<string> Changed) : HistoryEvent;
}

/// <summary>
/// A document's history. Undo and redo return when submitted;
/// <see cref="ObserveEvents"/> reports confirmation or refusal. Get one with
/// <see cref="PdfViewer.GetHistoryAsync"/>; the viewer needs
/// <see cref="PdfViewerOptions.History"/>. After its document closes, every
/// call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
public sealed class HistoryCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GH.HistoryHandle handle;

    internal HistoryCapability(PdfViewer viewer, GH.HistoryHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Whether there is a change to undo.</summary>
    public Task<bool> CanUndoAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanUndo(), cancellationToken);

    /// <summary>Whether there is a change to redo.</summary>
    public Task<bool> CanRedoAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanRedo(), cancellationToken);

    /// <summary>The label of the change undo would revert, if any.</summary>
    public async Task<HistoryChangeLabel?> GetUndoLabelAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublicOrNull(await viewer.Run(() => handle.UndoLabel(), cancellationToken).ConfigureAwait(false));

    /// <summary>The label of the change redo would reapply, if any.</summary>
    public async Task<HistoryChangeLabel?> GetRedoLabelAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublicOrNull(await viewer.Run(() => handle.RedoLabel(), cancellationToken).ConfigureAwait(false));

    /// <summary>Submits an undo. With nothing to undo it does nothing.</summary>
    public Task UndoAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.Undo(), cancellationToken);

    /// <summary>Submits a redo. With nothing to redo it does nothing.</summary>
    public Task RedoAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.Redo(), cancellationToken);

    /// <summary>Forgets every change.</summary>
    public Task ClearAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.Clear(), cancellationToken);

    /// <summary>Current settings, shared by every document using this plugin registration.</summary>
    public async Task<HistorySettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        new((await viewer.Run(() => handle.Settings(), cancellationToken).ConfigureAwait(false)).Limit);

    /// <summary>Merges the supplied fields; omission preserves them.</summary>
    public Task UpdateSettingsAsync(HistorySettingsPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        return viewer.Run(() =>
        {
            var ffi = new GH.HistorySettingsPatch(patch.Limit);
            return handle.UpdateSettings(ffi);
        }, cancellationToken);
    }

    /// <summary>Restores the values supplied at registration.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetSettings(), cancellationToken);

    /// <summary>
    /// Undo and redo outcomes and settings changes, subscribed when this
    /// returns. Ends when the document closes (at once if it already has),
    /// when the viewer is disposed, or when <paramref name="cancellationToken"/>
    /// fires. Each call is a new subscription; enumerate it once. A stream
    /// that is never enumerated still holds a native listener until its
    /// document closes, the viewer is disposed or
    /// <paramref name="cancellationToken"/> fires.
    /// </summary>
    public IAsyncEnumerable<HistoryEvent> ObserveEvents(CancellationToken cancellationToken = default)
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

    sealed class Relay() : EventRelay<GH.HistoryEvent, HistoryEvent>(Ffi.ToPublic), GH.HistoryEventListener
    {
        public void OnEvent(GH.HistoryEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's history capability. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.History"/>.</summary>
    public async Task<HistoryCapability> GetHistoryAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.History(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
