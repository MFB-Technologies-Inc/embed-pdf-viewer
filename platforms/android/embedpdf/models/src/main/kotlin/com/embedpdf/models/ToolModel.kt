package com.embedpdf.models

import com.embedpdf.InteractionCapability
import com.embedpdf.InteractionEventToolChanged
import com.embedpdf.InteractionTool
import com.embedpdf.PdfException
import com.embedpdf.ToolId
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * An observable model of a document's interaction tools, which a view
 * reads: which are registered, which is active, and which the user may use
 * now.
 *
 * [tools], [activeToolId] and [usable] are snapshots of the hub, read again
 * after each state revision and each `toolChanged` event while [run] runs,
 * and after each action. A snapshot is one revision behind the hub until its
 * refresh lands; the [InteractionCapability] reads answer with the hub's
 * current value. A refresh that started earlier never overwrites one that
 * started later, and that rule is the only arbitration between the reads. A
 * failed read, as when the document closes, empties the model and sets
 * [error]; [run] ends with one more refresh, so a closed document leaves the
 * model empty.
 *
 * [activate] and [activateDefault] write [activeToolId] themselves when the
 * hub accepts, then start one refresh for [tools] and [usable]. So
 * [activeToolId] holds the action's result when the action returns, with or
 * without [run] running, and [tools] and [usable] can trail it by one read:
 * the action's refresh is dropped when a newer read supersedes it or the
 * calling coroutine is cancelled, and the newer read, or the next one [run]
 * makes, brings them. After an action whose caller was cancelled, without
 * [run] running, call [refresh].
 *
 * [submitting] is `true` while the hub call of [activate] or
 * [activateDefault] is out, and `false` again before the action's refresh
 * starts. A second action asked for while it is `true` is ignored.
 *
 * [usable] holds each registered tool whose `canUseTool` is `true`. A tool
 * registered or removed is a state revision, so [usable] follows it while
 * [run] runs. A tool's permission can start to answer differently with no
 * revision and no event: the document's rights after an unlock or a
 * signing, or a `ToolPermission` the app registered. Call [refresh] after
 * such a change.
 *
 * [error] is the latest failure of an action or a read. A successful
 * [activate] or [activateDefault] clears it. A successful background refresh
 * clears the error of an earlier read and keeps the error of an action. A
 * caller that polls it can miss a failure one of those cleared.
 *
 * The state lives on the main thread: every call switches to
 * `Dispatchers.Main.immediate` itself.
 */
public class ToolModel(internal val interaction: InteractionCapability) {
    private val _tools = MutableStateFlow<List<InteractionTool>>(emptyList())
    private val _activeToolId = MutableStateFlow<ToolId?>(null)
    private val _usable = MutableStateFlow<Set<ToolId>>(emptySet())
    private val _error = MutableStateFlow<PdfException?>(null)
    private val _submitting = MutableStateFlow(false)

    /** The registered tools, in the hub's order. */
    public val tools: StateFlow<List<InteractionTool>> = _tools.asStateFlow()

    /** `null` before the first read lands and after a failed one. */
    public val activeToolId: StateFlow<ToolId?> = _activeToolId.asStateFlow()
    public val usable: StateFlow<Set<ToolId>> = _usable.asStateFlow()
    public val error: StateFlow<PdfException?> = _error.asStateFlow()

    /** Whether the hub call of an [activate] or [activateDefault] is out. */
    public val submitting: StateFlow<Boolean> = _submitting.asStateFlow()

    /** Whether [error] came from a read, which the next successful read clears, and not from an action. */
    private var readFailed = false
    internal val snapshots = SnapshotRefresh()

    /** Runs with [submitting] set, right before an action's hub call. Tests read the buttons' state here. */
    internal var hubCallStarting: (suspend () -> Unit)? = null
    private var observation = 0L

    /** Observes until the document closes or the calling coroutine is cancelled. */
    public suspend fun run(): Unit = onMain {
        val session = ++observation
        try {
            observeCapability(
                viewer = interaction.viewer,
                events = interaction.observeEvents(),
                initial = { if (observation == session) refresh() },
                // Performance: runs for every coalesced state revision, and most
                // are a scroll or a zoom that leaves the tools alone. The
                // viewer's revision stream does not say what changed, so each
                // one costs a full read (see `refresh()`).
                refresh = { if (observation == session) refresh() },
                receive = { event ->
                    // Gestures and cursor moves leave the tools alone.
                    if (observation == session && event is InteractionEventToolChanged) refresh()
                },
            )
            if (observation == session) refresh()
        } finally {
            if (observation == session) snapshots.invalidate()
        }
    }

    public suspend fun refresh(): Unit = onMain {
        snapshots.run(
            load = {
                val tools = interaction.tools()
                val active = interaction.activeToolId()
                // Performance: one `canUseTool` round trip to the kernel thread
                // per registered tool, one after another, on every coalesced
                // state revision and once more after each action: about 22
                // calls per read with the annotation tools. A single engine call
                // that answers for every tool removes the round trips.
                val usable = buildSet {
                    for (tool in tools) {
                        val id = ToolId(tool.id)
                        if (interaction.canUseTool(id)) add(id)
                    }
                }
                Triple(tools, active, usable)
            },
            apply = { (tools, active, usable) ->
                _tools.value = tools
                _activeToolId.value = active
                _usable.value = usable
                if (readFailed) {
                    readFailed = false
                    _error.value = null
                }
            },
            failure = {
                _tools.value = emptyList()
                _activeToolId.value = null
                _usable.value = emptySet()
                _error.value = controlError(it)
                readFailed = true
            },
        )
    }

    /**
     * Makes [id] the active tool. [activeToolId] is [id] when this returns
     * after the hub accepted. An id nobody registered sets [error] to
     * `not-found` and leaves [activeToolId].
     */
    public suspend fun activate(id: ToolId): Unit = submit {
        interaction.activateTool(id)
        id
    }

    /** Returns to the hub's default tool. [activeToolId] is that tool when this returns after the hub accepted. */
    public suspend fun activateDefault(): Unit = submit {
        interaction.activateDefaultTool()
        // Performance: one more round trip per return to the default tool,
        // because the hub call does not say which tool is the default.
        interaction.activeToolId()
    }

    /**
     * Runs one action's hub call with [submitting] set, writes the active
     * tool it returns, and then refreshes. Nothing suspends between that
     * write and the start of the refresh, so a read that started before the
     * hub answered is superseded and cannot put the old tool back.
     */
    private suspend fun submit(action: suspend () -> ToolId): Unit = onMain {
        if (_submitting.value) return@onMain
        _submitting.value = true
        val active = try {
            hubCallStarting?.invoke()
            action()
        } catch (e: CancellationException) {
            _submitting.value = false
            throw e
        } catch (e: Exception) {
            _submitting.value = false
            _error.value = controlError(e)
            readFailed = false
            return@onMain
        }
        _submitting.value = false
        _error.value = null
        readFailed = false
        _activeToolId.value = active
        refresh()
    }

    /** Whether a picker shows [id]'s button as on: [id] is [activeToolId]. */
    public fun isSelected(id: ToolId): Boolean = _activeToolId.value == id

    /**
     * Whether a picker disables [id]'s button: [submitting] is `true`, or the
     * tool is not in [usable] and not active. The active tool's button stays
     * enabled when its tool stops being usable, so a tap on it can always
     * return to the default tool. An id nobody registered is disabled.
     */
    public fun isDisabled(id: ToolId): Boolean =
        _submitting.value || (id !in _usable.value && !isSelected(id))

    /**
     * A tap on [id]'s button in a picker: the active tool's button returns to
     * the default tool, any other activates its tool. Public so a picker in
     * another module can use it; Swift keeps it package-wide.
     */
    public suspend fun buttonTapped(id: ToolId) {
        if (onMain { isSelected(id) }) activateDefault() else activate(id)
    }
}
