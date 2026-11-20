package com.embedpdf

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.launch
import uniffi.epdf_plugin_commands.CommandsEventListener
import uniffi.epdf_plugin_commands.CommandsHandle
import java.nio.ByteBuffer
import java.nio.charset.CharacterCodingException
import java.nio.charset.CodingErrorAction

public typealias CommandsSettings = uniffi.epdf_plugin_commands.CommandsSettings
public typealias CommandsSettingsPatch = uniffi.epdf_plugin_commands.CommandsSettingsPatch
public typealias NotAllowedMode = uniffi.epdf_plugin_commands.NotAllowedMode

public typealias CommandDefinition = uniffi.epdf_plugin_commands.CommandDefinition
public typealias CommandHook = uniffi.epdf_plugin_commands.CommandHook
public typealias CommandSettingLock = uniffi.epdf_plugin_commands.CommandSettingLock
public typealias CommandPanel = uniffi.epdf_plugin_commands.CommandPanel
public typealias CommandContext = uniffi.epdf_plugin_commands.CommandContext

/**
 * A command's hooks as the engine calls them, on the viewer's kernel thread.
 * [CommandHandler] implements it from lambdas.
 */
public typealias CommandRunner = uniffi.epdf_plugin_commands.CommandRunner
public typealias CommandCompletion = uniffi.epdf_plugin_commands.CommandCompletion
public typealias CommandSummary = uniffi.epdf_plugin_commands.CommandSummary
public typealias ResolvedCommand = uniffi.epdf_plugin_commands.ResolvedCommand
public typealias IconAccent = uniffi.epdf_plugin_commands.IconAccent
public typealias CommandExecuteResult = uniffi.epdf_plugin_commands.CommandExecuteResult
public typealias CommandExecuteResultExecuted = uniffi.epdf_plugin_commands.CommandExecuteResult.Executed
public typealias CommandExecuteResultRejected = uniffi.epdf_plugin_commands.CommandExecuteResult.Rejected
public typealias CommandRejectReason = uniffi.epdf_plugin_commands.CommandRejectReason
public typealias CommandShortcutBinding = uniffi.epdf_plugin_commands.CommandShortcutBinding
public typealias CommandMenuTarget = uniffi.epdf_plugin_commands.CommandMenuTarget
public typealias CommandKeyStroke = uniffi.epdf_plugin_commands.CommandKeyStroke
public typealias CommandShortcutMatch = uniffi.epdf_plugin_commands.CommandShortcutMatch
public typealias CommandRefusalInfo = uniffi.epdf_plugin_commands.CommandRefusalInfo
public typealias CommandRefusalReason = uniffi.epdf_plugin_commands.CommandRefusalReason
public typealias CommandsEvent = uniffi.epdf_plugin_commands.CommandsEvent
public typealias CommandsEventExecuted = uniffi.epdf_plugin_commands.CommandsEvent.Executed
public typealias CommandsEventExecutionFailed = uniffi.epdf_plugin_commands.CommandsEvent.ExecutionFailed
public typealias CommandExecuted = uniffi.epdf_plugin_commands.CommandExecuted
public typealias CommandExecutionFailure = uniffi.epdf_plugin_commands.CommandExecutionFailure

/**
 * One owner's standard command list, for `ViewerConfig.standardCommands`.
 * The commands plugin joins the named lists in upstream's order: stage,
 * interaction, selection, annotation, history, render, document.
 */
public typealias StandardCommandList = uniffi.epdf_viewer_host.StandardCommandList

/**
 * The clipboard and a file for the user: what the standard commands need from
 * the app. Called on the viewer's kernel thread: an implementation that
 * touches a `View` or the clipboard service posts to the main thread itself,
 * and answers through the completion from any thread.
 */
public typealias StandardCommandsPlatform = uniffi.epdf_viewer_host.StandardCommandsPlatform
public typealias StandardCommandCompletion = uniffi.epdf_viewer_host.StandardCommandCompletion

/**
 * A [CommandRunner] from lambdas: `CommandHandler { context -> save() }`.
 *
 * [enabled], [active], [visible], [iconAccent] and [locked] run on the
 * viewer's kernel thread on every resolve: answer from state you already
 * hold, never touch a `View`, and never wait on the main thread or on a
 * viewer call there. A hook that throws answers as the engine treats a
 * failed hook: not enabled, not active, not visible, no accent, unlocked.
 *
 * [run] runs in a new coroutine on [Dispatchers.Default], not on the main
 * thread; a body that touches a `View` switches with
 * `withContext(Dispatchers.Main)`. What it throws fails the command with its
 * code (a [PdfException] keeps its own, a `CancellationException` is
 * `operation-cancelled`, anything else `operation-failed` with its message).
 * Without [run], the command finishes at once. The coroutine belongs to no
 * scope of the viewer's: destroying the viewer doesn't cancel a [run] body
 * already running, which finishes on its own (a viewer call it makes then
 * throws `instance-closed`).
 *
 * The viewer keeps a registered handler until [Registration.unregister] runs
 * or the viewer is destroyed; closing a document doesn't release it. A
 * handler whose lambdas hold a capability or the [PdfViewer] keeps the viewer
 * alive until then.
 */
public class CommandHandler(
    private val enabled: ((CommandContext) -> Boolean)? = null,
    private val active: ((CommandContext) -> Boolean)? = null,
    private val visible: ((CommandContext) -> Boolean)? = null,
    private val iconAccent: ((CommandContext) -> IconAccent?)? = null,
    private val locked: ((CommandContext) -> CommandSettingLock?)? = null,
    private val run: (suspend (CommandContext) -> Unit)? = null,
) : CommandRunner {
    /** The hooks this handler implements, for [CommandDefinition.hooks]. */
    public val hooks: List<CommandHook>
        get() = buildList {
            if (run != null) add(CommandHook.RUN)
            if (enabled != null) add(CommandHook.ENABLED)
            if (active != null) add(CommandHook.ACTIVE)
            if (visible != null) add(CommandHook.VISIBLE)
            if (iconAccent != null) add(CommandHook.ICON_ACCENT)
            if (locked != null) add(CommandHook.LOCKED)
        }

    override fun run(context: CommandContext, completion: CommandCompletion) {
        val body = run ?: return completion.finish()
        CoroutineScope(Dispatchers.Default).launch {
            try {
                body(context)
                completion.finish()
            } catch (e: Throwable) {
                // A cancellation too: it reports as `operation-cancelled`.
                completion.fail(e)
            }
        }
    }

    override fun derive(context: CommandContext, hook: CommandHook): Boolean = hosted {
        when (hook) {
            CommandHook.ENABLED -> enabled?.invoke(context) ?: true
            CommandHook.ACTIVE -> active?.invoke(context) ?: false
            CommandHook.VISIBLE -> visible?.invoke(context) ?: true
            CommandHook.RUN, CommandHook.ICON_ACCENT, CommandHook.LOCKED -> true
        }
    }

    override fun locked(context: CommandContext): CommandSettingLock? = hosted { locked?.invoke(context) }

    override fun iconAccent(context: CommandContext): IconAccent? = hosted { iconAccent?.invoke(context) }

    /** [body], with what it throws as the `HostError` the engine expects from a hook. */
    private inline fun <T> hosted(body: () -> T): T =
        try {
            body()
        } catch (e: HostException) {
            throw e
        } catch (e: Exception) {
            throw HostExceptionFailed(readable(e))
        }
}

/**
 * A definition whose hooks come from [handler]. A routing command (menu,
 * panel or modal, no handler) uses the generated constructor.
 */
public fun CommandDefinition(
    id: String,
    labelKey: String,
    icon: String? = null,
    shortcuts: List<String> = emptyList(),
    categories: List<String> = emptyList(),
    menu: String? = null,
    panel: CommandPanel? = null,
    modal: String? = null,
    handler: CommandHandler,
): CommandDefinition = CommandDefinition(
    id = id,
    labelKey = labelKey,
    icon = icon,
    shortcuts = shortcuts,
    categories = categories,
    menu = menu,
    panel = panel,
    modal = modal,
    runner = handler,
    hooks = handler.hooks,
)

/**
 * Fails the command with [error], keeping its code (see [HostFailure]):
 * [CommandsCapability.execute] throws it and [CommandsEventExecutionFailed]
 * reports it.
 */
public fun CommandCompletion.fail(error: Throwable) {
    failWith(HostFailure(error))
}

/** Fails the copy with [error], keeping its code (see [HostFailure]). */
public fun StandardCommandCompletion.fail(error: Throwable) {
    failWith(HostFailure(error))
}

/**
 * The workspace command registry. Get it with [PdfViewer.commands]; the
 * viewer needs `ViewerConfig(commands = true)`. It keeps the viewer alive.
 * After the viewer is destroyed, every call throws `instance-closed`.
 */
public class CommandsCapability internal constructor(
    private val handle: CommandsHandle,
    internal val viewer: PdfViewer,
) {
    /** Current settings shared by every document using this plugin registration. */
    public suspend fun settings(): CommandsSettings = viewer.call { handle.settings() }

    /** Merges the supplied fields. Clear flags remove nullable values; omission preserves them. */
    public suspend fun updateSettings(patch: CommandsSettingsPatch) {
        viewer.call { handle.updateSettings(patch) }
    }

    /** Restores the values supplied at registration, preserving registered host resources. */
    public suspend fun resetSettings() {
        viewer.call { handle.resetSettings() }
    }

    /**
     * Throws `conflict` for a duplicate id unless [replace]. Dropping the
     * registration keeps the command; [Registration.unregister] removes it.
     * The viewer holds the definition's handler until then (see
     * [CommandHandler]).
     */
    public suspend fun register(definition: CommandDefinition, replace: Boolean = false): Registration =
        Registration(viewer.call { handle.registerCommand(definition, replace) })

    public suspend fun register(definitions: List<CommandDefinition>, replace: Boolean = false): Registration =
        Registration(viewer.call { handle.registerCommands(definitions, replace) })

    /**
     * Registers a command that runs [run]: `commands.register("doc:save") {
     * context -> save() }`. [labelKey] is [id] unless given. [run] runs as
     * [CommandHandler] runs it: in a new coroutine on [Dispatchers.Default].
     */
    public suspend fun register(
        id: String,
        labelKey: String = id,
        icon: String? = null,
        shortcuts: List<String> = emptyList(),
        categories: List<String> = emptyList(),
        replace: Boolean = false,
        run: suspend (CommandContext) -> Unit,
    ): Registration = register(
        CommandDefinition(id, labelKey, icon, shortcuts, categories, handler = CommandHandler(run = run)),
        replace,
    )

    /**
     * Replaces the app's JSON state whole. Every command hook receives the
     * current value in [CommandContext.appStateJson]. Invalid UTF-8 or JSON
     * throws `invalid-input` without changing the previous state. The
     * reserved `{"$undefined":true}` marker leaves state unchanged; `null`
     * replaces it.
     */
    public suspend fun setAppState(appState: ByteArray) {
        val json = strictUtf8(appState, "appState is not valid UTF-8")
        viewer.call { handle.setAppStateJson(json) }
    }

    public suspend fun hasCommand(id: String): Boolean = viewer.call { handle.hasCommand(id) }

    public suspend fun command(id: String): CommandSummary? = viewer.call { handle.command(id) }

    public suspend fun commandIds(): List<String> = viewer.call { handle.commandIds() }

    public suspend fun resolve(id: String, document: DocumentInfo? = null): ResolvedCommand? =
        viewer.call { handle.resolveCommand(id, document?.id) }

    public suspend fun commands(document: DocumentInfo? = null): List<ResolvedCommand> =
        viewer.call { handle.commands(document?.id) }

    public suspend fun search(query: String, document: DocumentInfo? = null): List<ResolvedCommand> =
        viewer.call { handle.searchCommands(query, document?.id) }

    public suspend fun shortcuts(): List<CommandShortcutBinding> = viewer.call { handle.shortcuts() }

    /**
     * [args] is JSON, handed back to the command's `run` and to the
     * `executed` event. Throws `invalid-input` when it isn't valid UTF-8
     * JSON. Cancelling the calling coroutine doesn't cancel the command.
     */
    public suspend fun execute(id: String, document: DocumentInfo? = null, args: ByteArray? = null): CommandExecuteResult {
        val json = args?.let { strictUtf8(it, "args are not valid UTF-8") }
        return viewer.call { handle.execute(id, document?.id, json) }
    }

    public suspend fun canExecute(id: String, document: DocumentInfo? = null): Boolean =
        viewer.call { handle.canExecute(id, document?.id) }

    public suspend fun disabledCategories(): List<String> = viewer.call { handle.disabledCategories() }

    public suspend fun disabledCommands(): List<String> = viewer.call { handle.disabledCommands() }

    public suspend fun isCommandDisabled(id: String): Boolean = viewer.call { handle.isCommandDisabled(id) }

    public suspend fun disableCommand(id: String) {
        viewer.call { handle.disableCommand(id) }
    }

    public suspend fun enableCommand(id: String) {
        viewer.call { handle.enableCommand(id) }
    }

    public suspend fun setDisabledCommands(ids: List<String>) {
        viewer.call { handle.setDisabledCommands(ids) }
    }

    /** Why a command permanently refuses; `null` for an unknown or temporarily disabled command. */
    public suspend fun refusal(id: String, document: DocumentInfo? = null): CommandRefusalInfo? =
        viewer.call { handle.refusal(id, document?.id) }

    public suspend fun isCategoryDisabled(category: String): Boolean = viewer.call { handle.isCategoryDisabled(category) }

    public suspend fun disableCategory(category: String) {
        viewer.call { handle.disableCategory(category) }
    }

    public suspend fun enableCategory(category: String) {
        viewer.call { handle.enableCategory(category) }
    }

    public suspend fun setDisabledCategories(categories: List<String>) {
        viewer.call { handle.setDisabledCategories(categories) }
    }

    /**
     * The command whose shortcut [stroke] matches, for the active document:
     * one that may run wins; a refused one counts only while it would show,
     * and comes with its refusal. [inTextField] is whether focus is in a text
     * field.
     */
    public suspend fun matchStroke(stroke: CommandKeyStroke, isMac: Boolean, inTextField: Boolean = false): CommandShortcutMatch? =
        viewer.call { handle.matchStroke(stroke, isMac, inTextField) }

    public suspend fun menuTarget(id: String): CommandMenuTarget? = viewer.call { handle.menuTarget(id) }

    /**
     * Executions and failures. Completes when the viewer is destroyed. Each
     * collection is a new subscription.
     */
    public fun observeEvents(): Flow<CommandsEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : CommandsEventListener {
                override fun onEvent(event: CommandsEvent) {
                    channel.trySend(event)
                }
            },
        )
    }
}

/** [bytes] as UTF-8, refusing malformed input with `invalid-input` rather than replacing it. */
private fun strictUtf8(bytes: ByteArray, detail: String): String =
    try {
        Charsets.UTF_8.newDecoder()
            .onMalformedInput(CodingErrorAction.REPORT)
            .onUnmappableCharacter(CodingErrorAction.REPORT)
            .decode(ByteBuffer.wrap(bytes))
            .toString()
    } catch (e: CharacterCodingException) {
        throw PdfException(PdfException.Code.INVALID_INPUT, detail, "EncodingError", e)
    }

/**
 * The workspace command registry. Throws `unsupported` when the viewer was
 * built without the commands plugin.
 */
public suspend fun PdfViewer.commands(): CommandsCapability = CommandsCapability(call { host.commands() }, this)

/**
 * The clipboard and file parts of the standard commands
 * (`ViewerConfig.standardCommands`). Read when a command runs; with `null`, a
 * copy fails with `invalid-input` and a save saves nothing. Ignored after the
 * viewer shut down.
 */
public fun PdfViewer.setCommandsPlatform(platform: StandardCommandsPlatform?) {
    if (isReleased) return
    host.setCommandsPlatform(platform)
}
