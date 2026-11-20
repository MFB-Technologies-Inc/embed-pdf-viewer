namespace EmbedPDF;

/// <summary>What a command the session may never run does.</summary>
public enum NotAllowedMode
{
    /// <summary>It disappears.</summary>
    Hide,
    /// <summary>It shows, disabled.</summary>
    Disable,
}

/// <summary>The commands plugin's settings.</summary>
/// <param name="DisabledCategories">Categories turned off.</param>
/// <param name="DisabledCommands">Commands turned off.</param>
/// <param name="NotAllowed">What a command the session may never run does.</param>
/// <param name="AppStateJson">The app's JSON state, handed to every hook.</param>
public sealed record CommandsSettings(
    IReadOnlyList<string> DisabledCategories, IReadOnlyList<string> DisabledCommands, NotAllowedMode NotAllowed, string AppStateJson);

/// <summary>A change to <see cref="CommandsSettings"/>; a <see langword="null"/> field keeps its value.</summary>
public sealed record CommandsSettingsPatch
{
    /// <summary>Categories turned off, replacing the list.</summary>
    public IReadOnlyList<string>? DisabledCategories { get; init; }

    /// <summary>Commands turned off, replacing the list.</summary>
    public IReadOnlyList<string>? DisabledCommands { get; init; }

    /// <summary>What a command the session may never run does.</summary>
    public NotAllowedMode? NotAllowed { get; init; }

    /// <summary>The app's JSON state. Invalid JSON is refused and changes nothing.</summary>
    public string? AppStateJson { get; init; }
}

/// <summary>What a hook is asked about.</summary>
/// <param name="CommandId">The command.</param>
/// <param name="DocumentId">The document it runs against, if any.</param>
/// <param name="ArgsJson">The arguments given to <see cref="CommandsCapability.ExecuteAsync"/>, as JSON.</param>
/// <param name="AppStateJson">The app's JSON state (<c>{"$undefined":true}</c> until set).</param>
public sealed record CommandContext(string CommandId, string? DocumentId, string? ArgsJson, string AppStateJson);

/// <summary>Colors for a command's icon, as CSS colors.</summary>
public sealed record IconAccent(string? Primary, string? Secondary);

/// <summary>A lock that keeps a command disabled while held.</summary>
/// <param name="By">Who holds it.</param>
/// <param name="ReasonKey">A translation key for why.</param>
public sealed record CommandSettingLock(string By, string? ReasonKey = null);

/// <summary>A panel a routing command opens.</summary>
/// <param name="Id">The panel.</param>
public sealed record CommandPanel(string Id)
{
    /// <summary>The group of panels it replaces.</summary>
    public string? Exclusive { get; init; }

    /// <summary>Whether opening it takes focus.</summary>
    public bool? Focus { get; init; }
}

/// <summary>A command hook a handler implements.</summary>
public enum CommandHook
{
    /// <summary>Runs the command.</summary>
    Run,
    /// <summary>Whether it may run.</summary>
    Enabled,
    /// <summary>Whether it shows as on.</summary>
    Active,
    /// <summary>Whether it shows.</summary>
    Visible,
    /// <summary>Its icon colors.</summary>
    IconAccent,
    /// <summary>A lock that disables it.</summary>
    Locked,
}

/// <summary>
/// A command definition: a label, an icon, shortcuts and categories, and a
/// <see cref="Handler"/> for the hooks, or a menu, a panel or a modal for a
/// routing command with no handler.
/// </summary>
/// <param name="Id">The command id (<c>doc:save</c>).</param>
/// <param name="LabelKey">The label's translation key, or the label.</param>
public sealed record CommandDefinition(string Id, string LabelKey)
{
    /// <summary>The icon name.</summary>
    public string? Icon { get; init; }

    /// <summary>Shortcuts such as <c>Mod+=</c> or <c>Ctrl+L</c>.</summary>
    public IReadOnlyList<string> Shortcuts { get; init; } = [];

    /// <summary>Categories, for turning commands off in groups.</summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>The menu it opens.</summary>
    public string? Menu { get; init; }

    /// <summary>The panel it opens.</summary>
    public CommandPanel? Panel { get; init; }

    /// <summary>The modal it opens.</summary>
    public string? Modal { get; init; }

    /// <summary>The hooks. The viewer keeps the handler until the registration is undone or the viewer is disposed.</summary>
    public CommandHandler? Handler { get; init; }
}

/// <summary>A registered command as defined.</summary>
public sealed record CommandSummary(
    string Id, string? Label, string? LabelKey, string? Icon, IReadOnlyList<string> Shortcuts, IReadOnlyList<string> Categories,
    string? Menu, CommandPanel? Panel, string? Modal, bool HasRunner);

/// <summary>A command as it stands now: label translated, hooks asked.</summary>
public sealed record ResolvedCommand(
    string Id, string Label, string? Icon, IconAccent? IconAccent, IReadOnlyList<string> Shortcuts, string? Menu,
    bool Enabled, bool Active, bool Pressable, bool Visible, IReadOnlyList<string> Categories, string? DisabledReason);

/// <summary>Why <see cref="CommandsCapability.ExecuteAsync"/> did not run a command.</summary>
public enum CommandRejectReason
{
    /// <summary>No such command.</summary>
    NotFound,
    /// <summary>It may not run now.</summary>
    Disabled,
    /// <summary>It does not show.</summary>
    Hidden,
}

/// <summary>What <see cref="CommandsCapability.ExecuteAsync"/> did.</summary>
public abstract record CommandExecuteResult
{
    private protected CommandExecuteResult() { }

    /// <summary>Not run.</summary>
    public sealed record Rejected(CommandRejectReason Reason) : CommandExecuteResult;

    /// <summary>Run to completion.</summary>
    public sealed record Executed : CommandExecuteResult;
}

/// <summary>A shortcut and its command.</summary>
public sealed record CommandShortcutBinding(string CommandId, string Shortcut);

/// <summary>The menu a command opens.</summary>
public sealed record CommandMenuTarget(string? Menu);

/// <summary>A key press, as a keyboard event reports it.</summary>
/// <param name="Key">The key value (<c>l</c>, <c>=</c>, <c>ArrowDown</c>).</param>
/// <param name="Code">The physical key code.</param>
/// <param name="Ctrl">Control held.</param>
/// <param name="Meta">Command or Windows held.</param>
/// <param name="Alt">Alt or Option held.</param>
/// <param name="Shift">Shift held.</param>
public sealed record CommandKeyStroke(string Key, string? Code = null, bool Ctrl = false, bool Meta = false, bool Alt = false, bool Shift = false);

/// <summary>Why a command permanently refuses.</summary>
public enum CommandRefusalReason
{
    /// <summary>Turned off by settings.</summary>
    TurnedOff,
    /// <summary>The session may never run it.</summary>
    NotAllowed,
    /// <summary>A setting lock holds it.</summary>
    Locked,
}

/// <summary>A permanent refusal.</summary>
public sealed record CommandRefusalInfo(CommandRefusalReason Reason, string Message);

/// <summary>The command a stroke matches, and its refusal if it may not run.</summary>
public sealed record CommandShortcutMatch(string CommandId, CommandRefusalInfo? Refusal);

/// <summary>Something the command registry did.</summary>
public abstract record CommandsEvent
{
    private protected CommandsEvent() { }

    /// <summary>A command ran.</summary>
    public sealed record Executed(string CommandId, string? DocumentId, string? ArgsJson) : CommandsEvent;

    /// <summary>A command's run failed.</summary>
    public sealed record ExecutionFailed(string CommandId, string? DocumentId, PluginErrorInfo Error) : CommandsEvent;
}

/// <summary>
/// A command's hooks, from delegates. The viewer keeps a registered handler
/// until the registration is undone or the viewer is disposed; closing a
/// document doesn't release it. A handler whose delegates hold a capability
/// or the <see cref="PdfViewer"/> keeps the viewer alive until then.
/// </summary>
/// <remarks>
/// <para><c>run</c> runs on the thread pool, off the kernel thread, so it
/// may await viewer calls and may dispose the viewer. What it throws fails
/// the command: a <see cref="PdfException"/> keeps its name and code, an
/// <see cref="OperationCanceledException"/> is <c>operation-cancelled</c>,
/// anything else is <c>operation-failed</c> with its message.</para>
/// <para><c>enabled</c>, <c>active</c>, <c>visible</c>, <c>iconAccent</c>
/// and <c>locked</c> run on the viewer's kernel thread, synchronously, on
/// every resolve: answer from state you already have, and never block
/// waiting for the viewer there: a viewer call queues behind the hook, so
/// waiting for it deadlocks. Disposing the viewer there throws
/// <see cref="InvalidOperationException"/>. What they throw reaches the
/// kernel as a host error, which leaves the command out of that resolve.</para>
/// </remarks>
public sealed class CommandHandler
{
    /// <summary>A handler with the given hooks; a hook left out answers its default (enabled, visible, not active, no accent, no lock).</summary>
    public CommandHandler(
        Func<CommandContext, Task>? run = null,
        Func<CommandContext, bool>? enabled = null,
        Func<CommandContext, bool>? active = null,
        Func<CommandContext, bool>? visible = null,
        Func<CommandContext, IconAccent?>? iconAccent = null,
        Func<CommandContext, CommandSettingLock?>? locked = null)
    {
        RunBody = run;
        Enabled = enabled;
        Active = active;
        Visible = visible;
        Accent = iconAccent;
        Lock = locked;
        var hooks = new List<CommandHook>();
        if (run is not null) hooks.Add(CommandHook.Run);
        if (enabled is not null) hooks.Add(CommandHook.Enabled);
        if (active is not null) hooks.Add(CommandHook.Active);
        if (visible is not null) hooks.Add(CommandHook.Visible);
        if (iconAccent is not null) hooks.Add(CommandHook.IconAccent);
        if (locked is not null) hooks.Add(CommandHook.Locked);
        Hooks = hooks;
    }

    /// <summary>The hooks this handler implements.</summary>
    public IReadOnlyList<CommandHook> Hooks { get; }

    internal Func<CommandContext, Task>? RunBody { get; }
    internal Func<CommandContext, bool>? Enabled { get; }
    internal Func<CommandContext, bool>? Active { get; }
    internal Func<CommandContext, bool>? Visible { get; }
    internal Func<CommandContext, IconAccent?>? Accent { get; }
    internal Func<CommandContext, CommandSettingLock?>? Lock { get; }
}

/// <summary>
/// The clipboard and a file for the user: what the standard commands
/// (<see cref="PdfViewerOptions.StandardCommands"/>) need from the app. Set it
/// with <see cref="PdfViewer.SetCommandsPlatform"/>.
/// </summary>
/// <remarks>
/// <see cref="SaveFile"/> runs on the viewer's kernel thread, synchronously,
/// and what it throws is logged and dropped. Never block waiting for the
/// viewer there: a viewer call queues behind it, so waiting for it deadlocks,
/// and <see cref="PdfViewer.Dispose"/> throws there. <see cref="CopyTextAsync"/> and
/// <see cref="CopyImageAsync"/> run on the thread pool; a fault fails the
/// copy command as <see cref="CommandHandler"/>'s run does.
/// </remarks>
public interface IStandardCommandsPlatform
{
    /// <summary>Hands the user a file, such as the saved document.</summary>
    void SaveFile(byte[] bytes, string fileName, string contentType);

    /// <summary>Copies text to the clipboard.</summary>
    Task CopyTextAsync(string text);

    /// <summary>Copies a PNG image to the clipboard.</summary>
    Task CopyImageAsync(byte[] png);
}
