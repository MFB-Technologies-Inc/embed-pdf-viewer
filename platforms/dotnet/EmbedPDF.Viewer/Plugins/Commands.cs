using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using GC = uniffi.epdf_plugin_commands;
using GF = uniffi.epdf_ffi;
using GH = uniffi.epdf_viewer_host;

namespace EmbedPDF;

/// <summary>
/// The workspace command registry. Get it with <see cref="PdfViewer.GetCommandsAsync"/>;
/// the viewer needs <see cref="PdfViewerOptions.Commands"/>. After the viewer
/// is disposed, every call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
/// <remarks>
/// JSON arguments are text. A string with an unpaired surrogate is
/// <see cref="PdfErrorCode.InvalidInput"/> before anything is posted, since
/// it would reach the engine as U+FFFD.
/// </remarks>
[SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Register one definition or several, as in Swift.")]
public sealed class CommandsCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GC.CommandsHandle handle;

    internal CommandsCapability(PdfViewer viewer, GC.CommandsHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Current settings.</summary>
    public async Task<CommandsSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Settings(), cancellationToken).ConfigureAwait(false));

    /// <summary>Merges the supplied fields; omission preserves them. Invalid app-state JSON changes nothing.</summary>
    public Task UpdateSettingsAsync(CommandsSettingsPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        if (patch.DisabledCategories is not null) Arguments.NoNullElements(patch.DisabledCategories, nameof(patch.DisabledCategories));
        if (patch.DisabledCommands is not null) Arguments.NoNullElements(patch.DisabledCommands, nameof(patch.DisabledCommands));
        return viewer.Run(() => handle.UpdateSettings(Ffi.ToFfi(patch)), cancellationToken);
    }

    /// <summary>Restores the values supplied at registration.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetSettings(), cancellationToken);

    /// <summary>
    /// Registers a command. Throws <see cref="PdfErrorCode.Conflict"/> for a
    /// duplicate id unless <paramref name="replace"/>. Disposing or dropping
    /// the registration keeps the command only if you never call
    /// <see cref="Registration.Unregister"/>; the viewer holds the definition's
    /// handler until then.
    /// </summary>
    public async Task<Registration> RegisterAsync(CommandDefinition definition, bool replace = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var ffi = ToFfi(definition);
        return new(await viewer.Run(() => handle.RegisterCommand(ffi, replace), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Registers several commands at once; one registration undoes them all.</summary>
    public Task<Registration> RegisterAsync(IEnumerable<CommandDefinition> definitions, bool replace = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var array = Arguments.NoNullElements(definitions, nameof(definitions));
        return Register();

        async Task<Registration> Register()
        {
            var ffi = array.Select(ToFfi).ToArray();
            return new(await viewer.Run(() => handle.RegisterCommands(ffi, replace), cancellationToken).ConfigureAwait(false));
        }
    }

    /// <summary>
    /// Replaces the app's JSON state whole. Every hook receives it in
    /// <see cref="CommandContext.AppStateJson"/>. Invalid JSON throws
    /// <see cref="PdfErrorCode.InvalidInput"/> and keeps the previous state;
    /// <c>{"$undefined":true}</c> leaves it unchanged and <c>null</c> replaces it.
    /// </summary>
    public Task SetAppStateAsync(string json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        return viewer.Run(() => handle.SetAppStateJson(Ffi.WellFormed(json, "app state")), cancellationToken);
    }

    /// <summary>Whether a command is registered.</summary>
    public Task<bool> HasCommandAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(() => handle.HasCommand(id), cancellationToken);
    }

    /// <summary>A registered command as defined.</summary>
    public async Task<CommandSummary?> GetCommandAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return await viewer.Run(() => handle.Command(id), cancellationToken).ConfigureAwait(false) is { } summary ? Ffi.ToPublic(summary) : null;
    }

    /// <summary>Every command id, in registration order.</summary>
    public async Task<IReadOnlyList<string>> GetCommandIdsAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.CommandIds(), cancellationToken).ConfigureAwait(false);

    /// <summary>A command as it stands for <paramref name="document"/> (the active one when <see langword="null"/>).</summary>
    public async Task<ResolvedCommand?> ResolveAsync(string id, DocumentInfo? document = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return await viewer.Run(() => handle.ResolveCommand(id, document?.Id), cancellationToken).ConfigureAwait(false) is { } c ? Ffi.ToPublic(c) : null;
    }

    /// <summary>Every command, resolved, in registration order.</summary>
    public async Task<IReadOnlyList<ResolvedCommand>> GetCommandsAsync(DocumentInfo? document = null, CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.Commands(document?.Id), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];

    /// <summary>Commands whose label matches <paramref name="query"/>.</summary>
    public async Task<IReadOnlyList<ResolvedCommand>> SearchAsync(string query, DocumentInfo? document = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return [.. (await viewer.Run(() => handle.SearchCommands(query, document?.Id), cancellationToken).ConfigureAwait(false)).Select(Ffi.ToPublic)];
    }

    /// <summary>Every shortcut and its command.</summary>
    public async Task<IReadOnlyList<CommandShortcutBinding>> GetShortcutsAsync(CancellationToken cancellationToken = default) =>
        [.. (await viewer.Run(() => handle.Shortcuts(), cancellationToken).ConfigureAwait(false)).Select(b => new CommandShortcutBinding(b.CommandId, b.Shortcut))];

    /// <summary>
    /// Runs a command. <paramref name="argsJson"/> is handed to its run and to
    /// the <see cref="CommandsEvent.Executed"/> event; invalid JSON throws
    /// <see cref="PdfErrorCode.InvalidInput"/>. A run that throws fails this
    /// call with its error. Cancelling the token doesn't cancel the command.
    /// </summary>
    public async Task<CommandExecuteResult> ExecuteAsync(string id, DocumentInfo? document = null, string? argsJson = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        var args = argsJson is null ? null : Ffi.WellFormed(argsJson, "args");
        return Ffi.ToPublic(await viewer.Run(() => handle.Execute(id, document?.Id, args), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Whether <see cref="ExecuteAsync"/> would run the command.</summary>
    public Task<bool> CanExecuteAsync(string id, DocumentInfo? document = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(() => handle.CanExecute(id, document?.Id), cancellationToken);
    }

    /// <summary>Why a command permanently refuses; <see langword="null"/> for an unknown or temporarily disabled command.</summary>
    public async Task<CommandRefusalInfo?> GetRefusalAsync(string id, DocumentInfo? document = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Ffi.ToPublicOrNull(await viewer.Run(() => handle.Refusal(id, document?.Id), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The categories turned off.</summary>
    public async Task<IReadOnlyList<string>> GetDisabledCategoriesAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.DisabledCategories(), cancellationToken).ConfigureAwait(false);

    /// <summary>Whether a category is turned off.</summary>
    public Task<bool> IsCategoryDisabledAsync(string category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        return viewer.Run(() => handle.IsCategoryDisabled(category), cancellationToken);
    }

    /// <summary>Turns a category off.</summary>
    public Task DisableCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        return viewer.Run(() => handle.DisableCategory(category), cancellationToken);
    }

    /// <summary>Turns a category back on.</summary>
    public Task EnableCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        return viewer.Run(() => handle.EnableCategory(category), cancellationToken);
    }

    /// <summary>Replaces the categories turned off.</summary>
    public Task SetDisabledCategoriesAsync(IEnumerable<string> categories, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categories);
        var array = Arguments.NoNullElements(categories, nameof(categories));
        return viewer.Run(() => handle.SetDisabledCategories(array), cancellationToken);
    }

    /// <summary>The commands turned off.</summary>
    public async Task<IReadOnlyList<string>> GetDisabledCommandsAsync(CancellationToken cancellationToken = default) =>
        await viewer.Run(() => handle.DisabledCommands(), cancellationToken).ConfigureAwait(false);

    /// <summary>Whether a command is turned off.</summary>
    public Task<bool> IsCommandDisabledAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(() => handle.IsCommandDisabled(id), cancellationToken);
    }

    /// <summary>Turns a command off.</summary>
    public Task DisableCommandAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(() => handle.DisableCommand(id), cancellationToken);
    }

    /// <summary>Turns a command back on.</summary>
    public Task EnableCommandAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return viewer.Run(() => handle.EnableCommand(id), cancellationToken);
    }

    /// <summary>Replaces the commands turned off.</summary>
    public Task SetDisabledCommandsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var array = Arguments.NoNullElements(ids, nameof(ids));
        return viewer.Run(() => handle.SetDisabledCommands(array), cancellationToken);
    }

    /// <summary>
    /// The command whose shortcut the stroke matches, for the active document:
    /// one that may run wins; a refused one counts only while it would show,
    /// and comes with its refusal. <paramref name="inTextField"/> is whether
    /// focus is in a text field.
    /// </summary>
    public async Task<CommandShortcutMatch?> MatchStrokeAsync(CommandKeyStroke stroke, bool isMac, bool inTextField = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        return await viewer.Run(() => handle.MatchStroke(Ffi.ToFfi(stroke), isMac, inTextField), cancellationToken).ConfigureAwait(false) is { } m
            ? new CommandShortcutMatch(m.CommandId, Ffi.ToPublicOrNull(m.Refusal)) : null;
    }

    /// <summary>The menu a command opens.</summary>
    public async Task<CommandMenuTarget?> GetMenuTargetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return await viewer.Run(() => handle.MenuTarget(id), cancellationToken).ConfigureAwait(false) is { } t ? new CommandMenuTarget(t.Menu) : null;
    }

    /// <summary>
    /// Registry events, subscribed when this returns. Ends when the viewer is
    /// disposed or when <paramref name="cancellationToken"/> fires. Each call is
    /// a new subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<CommandsEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. It does not end the streams from <see cref="ObserveEvents"/>.</summary>
    public void Dispose() => handle.Dispose();

    GC.CommandDefinition ToFfi(CommandDefinition d)
    {
        ArgumentNullException.ThrowIfNull(d.Id, nameof(CommandDefinition.Id));
        ArgumentNullException.ThrowIfNull(d.LabelKey, nameof(CommandDefinition.LabelKey));
        var runner = d.Handler is null ? null : new RunnerAdapter(d.Handler, viewer.Mark, viewer.Logger);
        return new GC.CommandDefinition(
            d.Id, d.LabelKey, d.Icon,
            Arguments.NoNullElements(d.Shortcuts ?? [], nameof(CommandDefinition.Shortcuts)),
            Arguments.NoNullElements(d.Categories ?? [], nameof(CommandDefinition.Categories)),
            d.Menu,
            d.Panel is null ? null : new GC.CommandPanel(d.Panel.Id, d.Panel.Exclusive, d.Panel.Focus),
            d.Modal, runner,
            d.Handler is null ? [] : [.. d.Handler.Hooks.Select(Ffi.ToFfi)]);
    }

    sealed class Relay() : EventRelay<GC.CommandsEvent, CommandsEvent>(Ffi.ToPublic), GC.CommandsEventListener
    {
        public void OnEvent(GC.CommandsEvent @event) => Deliver(@event);
    }

    /// <summary>
    /// <c>Run</c> goes to the thread pool and answers the completion once;
    /// the derivations run on the kernel thread, marked, and a throw is the
    /// kernel's host error.
    /// </summary>
    sealed class RunnerAdapter(CommandHandler app, KernelMark mark, ILogger logger) : GC.CommandRunner
    {
        public void Run(GC.CommandContext context, GC.CommandCompletion completion)
        {
            var body = app.RunBody;
            if (body is null)
            {
                Answer(completion, null);
                return;
            }
            var publicContext = Ffi.ToPublic(context);
            _ = Task.Run(async () =>
            {
                GF.HostFailure? failure = null;
                try
                {
                    var running = body(publicContext);
                    if (running is not null) await running.ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    logger.LogDebug(error, "A command's run failed.");
                    failure = FetcherAdapter.Failure(error, cancelled: error is OperationCanceledException);
                }
                Answer(completion, failure);
            });
        }

        public bool Derive(GC.CommandContext context, GC.CommandHook hook) => KernelCallback.RunOrFail(mark, () => hook switch
        {
            GC.CommandHook.Enabled => app.Enabled?.Invoke(Ffi.ToPublic(context)) ?? true,
            GC.CommandHook.Active => app.Active?.Invoke(Ffi.ToPublic(context)) ?? false,
            GC.CommandHook.Visible => app.Visible?.Invoke(Ffi.ToPublic(context)) ?? true,
            _ => true,
        });

        public GC.IconAccent? IconAccent(GC.CommandContext context) =>
            KernelCallback.RunOrFail(mark, () => app.Accent?.Invoke(Ffi.ToPublic(context)) is { } accent ? Ffi.ToFfi(accent) : null);

        public GC.CommandSettingLock? Locked(GC.CommandContext context) =>
            KernelCallback.RunOrFail(mark, () => app.Lock?.Invoke(Ffi.ToPublic(context)) is { } held ? Ffi.ToFfi(held) : null);

        static void Answer(GC.CommandCompletion completion, GF.HostFailure? failure)
        {
            try
            {
                if (failure is null) completion.Finish();
                else completion.FailWith(failure);
            }
            catch (Exception)
            {
                // The viewer shut down first; nobody waits for this answer.
            }
            finally
            {
                completion.Dispose();
            }
        }
    }
}

/// <summary>The standard commands' platform: <see cref="IStandardCommandsPlatform"/> behind the generated trait.</summary>
internal sealed class StandardCommandsAdapter(IStandardCommandsPlatform app, KernelMark mark, ILogger logger) : GH.StandardCommandsPlatform
{
    public void SaveFile(byte[] bytes, string fileName, string contentType) =>
        KernelCallback.Run(mark, logger, "A commands platform's SaveFile", () => app.SaveFile(bytes, fileName, contentType));

    public void CopyText(string text, GH.StandardCommandCompletion completion) => Copy(() => app.CopyTextAsync(text), completion);

    public void CopyImage(byte[] png, GH.StandardCommandCompletion completion) => Copy(() => app.CopyImageAsync(png), completion);

    void Copy(Func<Task> copy, GH.StandardCommandCompletion completion) => _ = Task.Run(async () =>
    {
        GF.HostFailure? failure = null;
        try
        {
            var running = copy();
            if (running is not null) await running.ConfigureAwait(false);
        }
        catch (Exception error)
        {
            logger.LogDebug(error, "A commands platform's copy failed.");
            failure = FetcherAdapter.Failure(error, cancelled: error is OperationCanceledException);
        }
        try
        {
            if (failure is null) completion.Finish();
            else completion.FailWith(failure);
        }
        catch (Exception)
        {
            // The viewer shut down first; nobody waits for this answer.
        }
        finally
        {
            completion.Dispose();
        }
    });
}

public sealed partial class PdfViewer
{
    /// <summary>The command registry. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Commands"/>.</summary>
    public async Task<CommandsCapability> GetCommandsAsync(CancellationToken cancellationToken = default) =>
        new(this, await CallAsync(h => h.Commands(), cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// The clipboard and file parts of the standard commands
    /// (<see cref="PdfViewerOptions.StandardCommands"/>), read when a command
    /// runs. With <see langword="null"/>, a copy fails with
    /// <see cref="PdfErrorCode.InvalidInput"/> and a save saves nothing.
    /// Throws <see cref="PdfErrorCode.InstanceClosed"/> after disposal. The
    /// viewer holds the platform until it is replaced or the viewer is
    /// disposed, so one that captures the viewer or a capability keeps an
    /// abandoned viewer from being collected; dispose the viewer, or keep the
    /// viewer out of it.
    /// </summary>
    public void SetCommandsPlatform(IStandardCommandsPlatform? platform)
    {
        NativeRuntime.EnsureLoaded();
        ThrowIfClosed();
        var adapter = platform is null ? null : new StandardCommandsAdapter(platform, Mark, Logger);
        try
        {
            host.SetCommandsPlatform(adapter);
        }
        catch (ObjectDisposedException)
        {
            throw PdfException.Closed();
        }
    }
}
