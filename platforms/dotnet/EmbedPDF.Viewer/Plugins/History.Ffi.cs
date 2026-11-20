using GH = uniffi.epdf_plugin_history;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static HistoryLabelValue ToPublic(GH.HistoryLabelValue value) => value switch
    {
        GH.HistoryLabelValue.Text t => new HistoryLabelValue.Text(t.Value),
        GH.HistoryLabelValue.Number n => new HistoryLabelValue.Number(n.Value),
        _ => throw Unknown(value),
    };

    internal static HistoryChangeLabel ToPublic(GH.HistoryChangeLabel label) =>
        new(label.Key, label.Values.ToDictionary(pair => pair.Key, pair => ToPublic(pair.Value)));

    internal static HistoryChangeLabel? ToPublicOrNull(GH.HistoryChangeLabel? label) => label is null ? null : ToPublic(label);

    internal static HistoryEvent ToPublic(GH.HistoryEvent e) => e switch
    {
        GH.HistoryEvent.Undone u => new HistoryEvent.Undone(ToPublic(u.Label), u.Redo, WireInteger.ToLong(u.Skipped, "skipped")),
        GH.HistoryEvent.UndoFailed f => new HistoryEvent.UndoFailed(ToPublic(f.Label), f.Redo,
            f.Reason == GH.HistoryUndoFailure.Refused ? HistoryUndoFailure.Refused : HistoryUndoFailure.Unavailable, ToPublic(f.Error)),
        GH.HistoryEvent.SettingsChanged s => new HistoryEvent.SettingsChanged(new HistorySettings(s.Settings.Limit), s.Changed),
        _ => throw Unknown(e),
    };
}
