using Microsoft.Extensions.Logging;
using GF = uniffi.epdf_ffi;
using GA = uniffi.epdf_plugin_actions;

namespace EmbedPDF;

/// <summary>
/// Runs one action node type for <see cref="ActionsCapability.RegisterExecutorAsync"/>.
/// It never sees the tree or the capability.
/// </summary>
/// <remarks>
/// Called on the thread pool, never on the kernel thread, so it may take its
/// time and may call the viewer. An exception or a faulted task fails the
/// node with the exception's message.
/// </remarks>
public interface IActionExecutor
{
    /// <summary>Runs <paramref name="node"/>.</summary>
    Task<ActionExecutorResult> ExecuteAsync(PdfActionNode node, ActionContext context);
}

/// <summary>The UI a document's actions reach: links, prints, script alerts and page changes.</summary>
/// <remarks>
/// The kernel calls every member on its own thread, synchronously, inside the
/// dispatch: return at once and do the work elsewhere.
/// <see cref="PdfViewer.Dispose"/> throws here. An exception fails the node
/// with its message.
/// </remarks>
public interface IActionUiHandler
{
    /// <summary>Opens a URI. It passed the scheme allowlist (http, https, mailto, tel) but is still document-supplied: treat it as untrusted.</summary>
    void OpenUri(string uri, ActionUriOptions options);

    /// <summary>The Named Print verb and script <c>print()</c>.</summary>
    void Print(ActionUiContext? context);

    /// <summary>A script alert.</summary>
    void Alert(string message, ActionAlertOptions options);

    /// <summary>A script page change; <paramref name="page"/> is what the script asked for, zero-based and unchecked.</summary>
    void GotoPage(double page, ActionUiContext context);
}

/// <summary>Receives form submissions. Return to accept (the network call is yours, and its outcome is yours to report); throw to fail the node.</summary>
/// <remarks>
/// Called on the kernel thread, synchronously: return at once.
/// <see cref="PdfViewer.Dispose"/> throws here.
/// </remarks>
public interface IActionSubmitHandler
{
    /// <summary>A submission the document asked for.</summary>
    void Submit(ActionSubmitRequest request);
}

/// <summary>Document JavaScript settings for <see cref="ActionsConfig.JavaScript"/>.</summary>
/// <param name="Enabled">Whether document JavaScript runs. Enabled with no engine, every script fails with <c>NO_DEFAULT_SANDBOX</c>.</param>
public sealed partial record ActionsJavaScriptConfig(bool Enabled)
{
    /// <summary>Script limits; <see langword="null"/> is the engine's default budget (50 ms, 16 MiB).</summary>
    public ActionsScriptBudget? Budget { get; init; }

    /// <summary>The most JavaScript nodes one dispatch may run; <see langword="null"/> is 16.</summary>
    public int? MaxScriptNodesPerDispatch { get; init; }
}

/// <summary>Installs the actions plugin: the action policy, the dispatcher and its ports.</summary>
public sealed record ActionsConfig
{
    /// <summary>Policy rows over the defaults.</summary>
    public ActionPolicyPatch? Policy { get; init; }

    /// <summary>Trigger gates over the defaults.</summary>
    public TriggerGates? Triggers { get; init; }

    /// <summary>What happens to a document's open actions; <see langword="null"/> is <see cref="EmbedPDF.OpenSequence.Auto"/>.</summary>
    public OpenSequence? OpenSequence { get; init; }

    /// <summary>Document JavaScript.</summary>
    public ActionsJavaScriptConfig? JavaScript { get; init; }

    /// <summary>The offset scripts see for local time, in minutes; <see langword="null"/> is UTC.</summary>
    public int? UtcOffsetMinutes { get; init; }

    /// <summary>A config whose <see cref="UtcOffsetMinutes"/> is the local time zone's offset now. The offset is fixed for the viewer's lifetime.</summary>
    public static ActionsConfig ForCurrentTimeZone() =>
        new() { UtcOffsetMinutes = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes };

    internal GA.ActionsConfig ToFfi(PluginContext context) => new(
        Policy: Ffi.ToFfi(Policy),
        Triggers: Ffi.ToFfi(Triggers),
        OpenSequence: OpenSequence is { } o ? (GA.OpenSequence)(int)o : null,
        Javascript: JavaScript?.ToFfi(context),
        UtcOffsetMinutes: UtcOffsetMinutes);
}

/// <summary>
/// A document's PDF actions: policy, the dispatcher and its ports. Get one
/// with <see cref="PdfViewer.GetActionsAsync"/>; the viewer needs
/// <see cref="PdfViewerOptions.Actions"/>. After its document closes, every
/// call throws <see cref="PdfErrorCode.InstanceClosed"/>. No call cancels once
/// posted: a dispatch is a transaction on the document's queue.
/// </summary>
public sealed class ActionsCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GA.ActionsHandle handle;

    internal ActionsCapability(PdfViewer viewer, GA.ActionsHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Current settings, shared by every document using this plugin registration.</summary>
    public async Task<ActionsSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Settings(), cancellationToken).ConfigureAwait(false));

    /// <summary>Merges the supplied fields; omission preserves them.</summary>
    public Task UpdateSettingsAsync(ActionsSettingsPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var ffi = Ffi.ToFfi(patch);
        return viewer.Run(() => handle.UpdateSettings(ffi), cancellationToken);
    }

    /// <summary>Restores the values supplied at registration.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetSettings(), cancellationToken);

    /// <summary>The action policy.</summary>
    public async Task<ActionPolicy> GetPolicyAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.Policy(), cancellationToken).ConfigureAwait(false));

    /// <summary>Changes policy rows.</summary>
    public Task UpdatePolicyAsync(ActionPolicyPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var ffi = Ffi.ToFfi(patch)!;
        return viewer.Run(() => handle.UpdatePolicy(ffi), cancellationToken);
    }

    /// <summary>Whether document JavaScript runs.</summary>
    public Task<bool> IsScriptingEnabledAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.IsScriptingEnabled(), cancellationToken);

    /// <summary>Runs a tree.</summary>
    public async Task<ActionDispatchResult> ExecuteAsync(PdfActionTree tree, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var ffi = Ffi.ToFfi(context);
        return Ffi.ToPublic(await viewer.Run(() => handle.Execute(tree.Handle, ffi), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Whether running a tree would do anything the policy allows.</summary>
    public Task<bool> CanExecuteAsync(PdfActionTree tree, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var ffi = Ffi.ToFfi(context);
        return viewer.Run(() => handle.CanExecute(tree.Handle, ffi), cancellationToken);
    }

    /// <summary>Runs a named action (<c>NextPage</c>, <c>Print</c>, ...).</summary>
    public async Task<ActionDispatchResult> ExecuteNamedAsync(string name, PartialActionContext? context = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        var ffi = Ffi.ToFfi(context);
        return Ffi.ToPublic(await viewer.Run(() => handle.ExecuteNamed(name, ffi), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Whether a named action would do anything the policy allows.</summary>
    public Task<bool> CanExecuteNamedAsync(string name, PartialActionContext? context = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        var ffi = Ffi.ToFfi(context);
        return viewer.Run(() => handle.CanExecuteNamed(name, ffi), cancellationToken);
    }

    /// <summary>Reads a tree from the document, or <see langword="null"/> when there is none.</summary>
    public async Task<PdfActionTree?> GetActionTreeAsync(ActionTreeSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var ffi = Ffi.ToFfi(source);
        var tree = await viewer.Run(() => handle.ActionTree(ffi), cancellationToken).ConfigureAwait(false);
        return tree is null ? null : new PdfActionTree(tree);
    }

    /// <summary>Runs every tree a trigger reaches.</summary>
    public async Task<ActionTriggerResult> DispatchAsync(ActionTrigger trigger, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var ffi = Ffi.ToFfi(trigger);
        return Ffi.ToPublic(await viewer.Run(() => handle.Dispatch(ffi), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Whether dispatching a trigger would run anything.</summary>
    public Task<bool> CanDispatchAsync(ActionTrigger trigger, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var ffi = Ffi.ToFfi(trigger);
        return viewer.Run(() => handle.CanDispatch(ffi), cancellationToken);
    }

    /// <summary>Runs the document's will-close trees. Call before <see cref="PdfViewer.CloseAsync"/>.</summary>
    public async Task<ActionTriggerResult> PrepareCloseAsync(CancellationToken cancellationToken = default) =>
        Ffi.ToPublic(await viewer.Run(() => handle.PrepareClose(), cancellationToken).ConfigureAwait(false));

    /// <summary>Runs nodes of <paramref name="actionType"/> with <paramref name="executor"/>. The last registration per type wins.</summary>
    public async Task<Registration> RegisterExecutorAsync(string actionType, IActionExecutor executor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actionType);
        ArgumentNullException.ThrowIfNull(executor);
        var adapter = new ExecutorAdapter(executor, viewer.Logger);
        return new(await viewer.Run(() => handle.RegisterExecutor(actionType, adapter), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Installs the UI handler; <see langword="null"/> uninstalls it. Installing one releases an <see cref="OpenSequence.Auto"/> open sequence.</summary>
    public async Task<Registration> SetUiHandlerAsync(IActionUiHandler? handler, ActionUiSupport? support = null, CancellationToken cancellationToken = default)
    {
        var adapter = handler is null ? null : new UiAdapter(handler, viewer.Mark);
        var ffiSupport = new GA.ActionUiSupport((support ?? new ActionUiSupport()).Alert, (support ?? new ActionUiSupport()).GotoPage);
        return new(await viewer.Run(() => handle.SetUiAdapter(adapter, ffiSupport), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Installs the submit handler; <see langword="null"/> uninstalls it.</summary>
    public async Task<Registration> SetSubmitHandlerAsync(IActionSubmitHandler? handler, CancellationToken cancellationToken = default)
    {
        var adapter = handler is null ? null : new SubmitAdapter(handler, viewer.Mark);
        return new(await viewer.Run(() => handle.SetSubmitHandler(adapter), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// The document's will- trees, then <paramref name="operation"/> on the
    /// thread pool, then its did- trees. A <see cref="PdfException"/> the
    /// operation throws keeps its code.
    /// </summary>
    public Task RunDocumentVerbAsync(DocumentVerb verb, Func<Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var adapter = new VerbAdapter(operation, viewer.Logger);
        return viewer.Run(() => handle.RunDocumentVerb((GA.DocumentVerb)(int)verb, adapter), cancellationToken);
    }

    /// <summary>
    /// Executions, diagnostics, script failures and the open sequence's end,
    /// subscribed when this returns. Ends when the document closes (at once if
    /// it already has), when the viewer is disposed, or when
    /// <paramref name="cancellationToken"/> fires. Each call is a new
    /// subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<ActionsEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. Calls after it throw <see cref="PdfErrorCode.InstanceClosed"/>.</summary>
    public void Dispose() => handle.Dispose();

    sealed class Relay() : EventRelay<GA.ActionsEvent, ActionsEvent>(Ffi.ToPublic), GA.ActionsEventListener
    {
        public void OnEvent(GA.ActionsEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }

    /// <summary>Runs the app's executor on the thread pool and answers the kernel once.</summary>
    sealed class ExecutorAdapter(IActionExecutor executor, ILogger logger) : GA.ActionExecutor
    {
        public void Execute(GA.PdfActionNode node, GA.ActionContext context, GA.ActionExecutorCompletion completion)
        {
            var publicNode = new PdfActionNode(node.NodeType(), node.Json());
            ActionContext publicContext;
            try
            {
                publicContext = Ffi.ToPublic(context);
            }
            catch (Exception error)
            {
                Answer(completion, () => completion.Failed(error.Message));
                return;
            }
            _ = Task.Run(async () =>
            {
                ActionExecutorResult? result = null;
                Exception? failure = null;
                try
                {
                    var running = executor.ExecuteAsync(publicNode, publicContext);
                    result = running is null ? null : await running.ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    logger.LogDebug(error, "An action executor failed.");
                    failure = error;
                }
                Answer(completion, () =>
                {
                    switch (result)
                    {
                        case ActionExecutorResult.Inert inert: completion.Inert(inert.Reason ?? ""); break;
                        case ActionExecutorResult.Executed: completion.Executed(); break;
                        default: completion.Failed(failure?.Message ?? "The executor returned no result."); break;
                    }
                });
            });
        }

        static void Answer(GA.ActionExecutorCompletion completion, Action answer)
        {
            try
            {
                answer();
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

    /// <summary>Called on the kernel thread, marked; a throw fails the node.</summary>
    sealed class UiAdapter(IActionUiHandler app, KernelMark mark) : GA.ActionUiHandler
    {
        public void OpenUri(string uri, GA.ActionUriOptions options) =>
            KernelCallback.RunOrFail(mark, () => app.OpenUri(uri, new ActionUriOptions(options.IsMap, (ActionOrigin)(int)options.Origin)));

        public void Print(GA.ActionUiContext? context) =>
            KernelCallback.RunOrFail(mark, () => app.Print(context is null ? null : Context(context)));

        public void Alert(string message, GA.ActionAlertOptions options) =>
            KernelCallback.RunOrFail(mark, () => app.Alert(message,
                new ActionAlertOptions((ActionOrigin)(int)options.Origin, (ScriptPhase)(int)options.Phase, options.Icon, options.Title)));

        public void GotoPage(double page, GA.ActionUiContext context) =>
            KernelCallback.RunOrFail(mark, () => app.GotoPage(page, Context(context)));

        static ActionUiContext Context(GA.ActionUiContext context) => new((ActionOrigin)(int)context.Origin, (ScriptPhase)(int)context.Phase);
    }

    /// <summary>Called on the kernel thread, marked; a throw fails the node.</summary>
    sealed class SubmitAdapter(IActionSubmitHandler app, KernelMark mark) : GA.ActionSubmitHandler
    {
        public void Submit(GA.ActionSubmitRequest request) => KernelCallback.RunOrFail(mark, () => app.Submit(Ffi.ToPublic(request)));
    }

    /// <summary>Runs the operation on the thread pool and answers the kernel once.</summary>
    sealed class VerbAdapter(Func<Task> operation, ILogger logger) : GA.DocumentVerbOperation
    {
        public void Run(GA.DocumentVerbCompletion completion) => _ = Task.Run(async () =>
        {
            GF.HostFailure? failure = null;
            try
            {
                var running = operation();
                if (running is not null) await running.ConfigureAwait(false);
            }
            catch (Exception error)
            {
                logger.LogDebug(error, "A document verb's operation failed.");
                failure = FetcherAdapter.Failure(error, cancelled: false);
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
}

public sealed partial class PdfViewer
{
    /// <summary>The document's actions capability. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Actions"/>.</summary>
    public async Task<ActionsCapability> GetActionsAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Actions(document.Id), cancellationToken).ConfigureAwait(false));
    }
}
