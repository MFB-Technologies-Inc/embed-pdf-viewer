using GC = uniffi.epdf_plugin_commands;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static CommandsSettings ToPublic(GC.CommandsSettings s) =>
        new(s.DisabledCategories, s.DisabledCommands, (NotAllowedMode)(int)s.NotAllowed, s.AppStateJson);

    internal static GC.CommandsSettingsPatch ToFfi(CommandsSettingsPatch p) => new(
        p.DisabledCategories is null ? null : Arguments.NoNullElements(p.DisabledCategories, nameof(p.DisabledCategories)),
        p.DisabledCommands is null ? null : Arguments.NoNullElements(p.DisabledCommands, nameof(p.DisabledCommands)),
        p.NotAllowed is { } mode ? (GC.NotAllowedMode)(int)Defined(mode, "not-allowed mode") : null,
        p.AppStateJson is null ? null : WellFormed(p.AppStateJson, "app state"));

    /// <summary>
    /// <paramref name="text"/> when every UTF-16 surrogate is paired. An unpaired
    /// one would cross to the engine as U+FFFD and change what the JSON says,
    /// so it is <c>invalid-input</c> here, as bytes that are not UTF-8 are in Swift.
    /// </summary>
    internal static string WellFormed(string text, string what)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) { i++; continue; }
            if (char.IsSurrogate(text[i]))
                throw new PdfException("EncodingError", PdfErrorCode.InvalidInput, $"{what} has an unpaired surrogate at {i}");
        }
        return text;
    }

    internal static CommandContext ToPublic(GC.CommandContext c) => new(c.CommandId, c.DocumentId, c.ArgsJson, c.AppStateJson);

    internal static IconAccent? ToPublicOrNull(GC.IconAccent? a) => a is null ? null : new(a.Primary, a.Secondary);

    internal static GC.IconAccent ToFfi(IconAccent a) => new(a.Primary, a.Secondary);

    internal static GC.CommandSettingLock ToFfi(CommandSettingLock l) => new(l.By, l.ReasonKey);

    internal static CommandPanel? ToPublicOrNull(GC.CommandPanel? p) => p is null ? null : new(p.Id) { Exclusive = p.Exclusive, Focus = p.Focus };

    internal static GC.CommandHook ToFfi(CommandHook hook) => (GC.CommandHook)(int)Defined(hook, "command hook");

    internal static CommandSummary ToPublic(GC.CommandSummary s) => new(
        s.Id, s.Label, s.LabelKey, s.Icon, s.Shortcuts, s.Categories, s.Menu, ToPublicOrNull(s.Panel), s.Modal, s.HasRunner);

    internal static ResolvedCommand ToPublic(GC.ResolvedCommand c) => new(
        c.Id, c.Label, c.Icon, ToPublicOrNull(c.IconAccent), c.Shortcuts, c.Menu, c.Enabled, c.Active, c.Pressable, c.Visible,
        c.Categories, c.DisabledReason);

    internal static CommandExecuteResult ToPublic(GC.CommandExecuteResult r) => r switch
    {
        GC.CommandExecuteResult.Rejected x => new CommandExecuteResult.Rejected((CommandRejectReason)(int)x.Reason),
        GC.CommandExecuteResult.Executed => new CommandExecuteResult.Executed(),
        _ => throw Unknown(r),
    };

    internal static GC.CommandKeyStroke ToFfi(CommandKeyStroke k) =>
        new(k.Key ?? throw new ArgumentException("A stroke needs its key.", nameof(k)), k.Code, k.Ctrl, k.Meta, k.Alt, k.Shift);

    internal static CommandRefusalInfo? ToPublicOrNull(GC.CommandRefusalInfo? r) =>
        r is null ? null : new((CommandRefusalReason)(int)r.Reason, r.Message);

    internal static CommandsEvent ToPublic(GC.CommandsEvent e) => e switch
    {
        GC.CommandsEvent.Executed x => new CommandsEvent.Executed(x.Event.CommandId, x.Event.DocumentId, x.Event.ArgsJson),
        GC.CommandsEvent.ExecutionFailed x => new CommandsEvent.ExecutionFailed(x.Event.CommandId, x.Event.DocumentId, ToPublic(x.Event.Error)),
        _ => throw Unknown(e),
    };
}
