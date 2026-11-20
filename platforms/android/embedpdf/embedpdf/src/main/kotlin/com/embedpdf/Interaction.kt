package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_interaction.InteractionEventListener
import uniffi.epdf_plugin_interaction.InteractionHandle

public typealias InteractionSettings = uniffi.epdf_plugin_interaction.InteractionSettings
public typealias InteractionSettingsPatch = uniffi.epdf_plugin_interaction.InteractionSettingsPatch

public typealias InteractionTool = uniffi.epdf_plugin_interaction.InteractionTool
public typealias InteractionToolTouch = uniffi.epdf_plugin_interaction.InteractionToolTouch
public typealias InteractionEvent = uniffi.epdf_plugin_interaction.InteractionEvent
public typealias InteractionEventToolChanged = uniffi.epdf_plugin_interaction.InteractionEvent.ToolChanged
public typealias InteractionEventGestureStarted = uniffi.epdf_plugin_interaction.InteractionEvent.GestureStarted
public typealias InteractionEventGestureEnded = uniffi.epdf_plugin_interaction.InteractionEvent.GestureEnded
public typealias InteractionEventGestureCancelled = uniffi.epdf_plugin_interaction.InteractionEvent.GestureCancelled
public typealias InteractionEventCursorChanged = uniffi.epdf_plugin_interaction.InteractionEvent.CursorChanged
public typealias InteractionToolChangedEvent = uniffi.epdf_plugin_interaction.InteractionToolChangedEvent
public typealias InteractionGestureEvent = uniffi.epdf_plugin_interaction.InteractionGestureEvent
public typealias InteractionCursorChangedEvent = uniffi.epdf_plugin_interaction.InteractionCursorChangedEvent

/**
 * A registered tool's `canUse`: whether the user may use it now. Asked on the
 * viewer's kernel thread whenever `canUseTool` is read, so answer from state
 * you already hold; waiting on a viewer call inside it deadlocks.
 */
public typealias ToolPermission = uniffi.epdf_plugin_interaction.ToolPermission
public typealias PointerSample = uniffi.epdf_ffi.PointerSample
public typealias PointerPhase = uniffi.epdf_ffi.PointerPhase
public typealias PointerKind = uniffi.epdf_ffi.PointerKind
public typealias PointerGesture = uniffi.epdf_ffi.PointerGesture
public typealias PointerPageHit = uniffi.epdf_ffi.PointerPageHit
public typealias Modifiers = uniffi.epdf_ffi.Modifiers

/**
 * A pointer handler. The hub routes each gesture to the first eligible
 * handler, by priority, whose [onDown] returns `true`; move and up then go to
 * it until the up.
 *
 * Every member runs on the viewer's kernel thread, synchronously, inside
 * [InteractionCapability.dispatchPointer]. Return quickly and never block
 * waiting for the viewer: a call into the viewer from here queues behind this
 * dispatch. A handler that touches a `View` posts to the main thread itself.
 *
 * [enabledFor] and [onDown] are required; the rest do what the hub does when a
 * handler leaves them out.
 */
public interface InteractionHandler : uniffi.epdf_plugin_interaction.InteractionHandler {
    override fun onMove(sample: PointerSample) {}

    override fun onUp(sample: PointerSample) {}

    /** The gesture was aborted, not completed. Calls [onUp] unless overridden. */
    override fun onCancel(sample: PointerSample) {
        onUp(sample)
    }

    /** The pointer moved with no gesture active: cursor feedback only. */
    override fun onHover(sample: PointerSample) {}

    /** Touch consent: claim this contact as a tool gesture. A pure read. */
    override fun claimsTouch(sample: PointerSample): Boolean = false
}

/**
 * A document's pointer hub: the active tool, the cursor, and the handlers
 * gestures route to. Get one with [PdfViewer.interaction]; the viewer needs
 * `ViewerConfig(interaction = true)`. It keeps the viewer alive. After its
 * document closes, every call throws `instance-closed`.
 */
public class InteractionCapability internal constructor(
    private val handle: InteractionHandle,
    internal val viewer: PdfViewer,
) {
    /** Current settings shared by every document using this plugin registration. */
    public suspend fun settings(): InteractionSettings = viewer.call { handle.settings() }

    /** Merges the supplied fields. Clear flags remove nullable values; omission preserves them. */
    public suspend fun updateSettings(patch: InteractionSettingsPatch) {
        viewer.call { handle.updateSettings(patch) }
    }

    /** Restores the values supplied at registration, preserving registered host resources. */
    public suspend fun resetSettings() {
        viewer.call { handle.resetSettings() }
    }

    public suspend fun hasCursorClaim(): Boolean = viewer.call { handle.hasCursorClaim() }

    public suspend fun activeTool(): InteractionTool = viewer.call { handle.activeTool() }

    /** The active tool's id. [activeTool] gives the whole tool. */
    public suspend fun activeToolId(): ToolId = ToolId(viewer.call { handle.activeToolId() })

    /** The tool the hub falls back to, `pointer` unless the settings name another. */
    public suspend fun defaultToolId(): ToolId = ToolId(viewer.call { handle.defaultToolId() })

    public suspend fun tools(): List<InteractionTool> = viewer.call { handle.tools() }

    public suspend fun tool(id: ToolId): InteractionTool? = viewer.call { handle.tool(id.value) }

    public suspend fun hasTool(id: ToolId): Boolean = viewer.call { handle.hasTool(id.value) }

    public suspend fun activeToolEnables(behavior: String): Boolean = viewer.call { handle.activeToolEnables(behavior) }

    /** Throws `not-found` for an id nobody registered. */
    public suspend fun activateTool(id: ToolId) {
        viewer.call { handle.activateTool(id.value) }
    }

    public suspend fun activateDefaultTool() {
        viewer.call { handle.activateDefaultTool() }
    }

    /** Arms a tool for now (hold to pan); [popTool] restores the last one. */
    public suspend fun pushTool(id: ToolId) {
        viewer.call { handle.pushTool(id.value) }
    }

    public suspend fun popTool() {
        viewer.call { handle.popTool() }
    }

    /** Replaces a tool's cursors by name; `null` brings the tool's own back. */
    public suspend fun setToolCursor(id: ToolId, cursors: Map<String, String>?) {
        viewer.call { handle.setToolCursor(id.value, cursors) }
    }

    /**
     * Throws `conflict` for an id already registered unless [replace].
     * [permission] is the tool's `canUse`; without one the tool may always be
     * used.
     */
    public suspend fun registerTool(
        tool: InteractionTool,
        replace: Boolean = false,
        permission: ToolPermission? = null,
    ): Registration = Registration(viewer.call { handle.registerTool(tool, replace, permission) })

    /**
     * Whether the user may use a tool: what its permission says, `true` for a
     * tool registered without one, `false` for an id nobody registered.
     */
    public suspend fun canUseTool(id: ToolId): Boolean = viewer.call { handle.canUseTool(id.value) }

    /**
     * Adds a pointer handler. [priority] is fixed here; higher wins a gesture.
     * [source] scopes it to one lens (a stage id). [handler] is called on the
     * viewer's kernel thread, synchronously, for every sample: return quickly,
     * and don't block waiting on the viewer from inside it.
     *
     * The viewer keeps [handler] until [Registration.unregister] runs or the
     * document closes. A handler that holds this capability or the
     * [PdfViewer] keeps the viewer alive until then.
     */
    public suspend fun registerHandler(
        handler: InteractionHandler,
        id: String,
        priority: Double,
        source: String? = null,
    ): Registration = Registration(viewer.call { handle.registerHandler(id, priority, source, handler) })

    /** `null` clears the claim. */
    public suspend fun claimCursor(token: String, cursor: String?, priority: Double = 0.0) {
        viewer.call { handle.claimCursor(token, cursor, priority) }
    }

    public suspend fun cursor(): String = viewer.call { handle.cursor() }

    /** Touch pre-flight: would any eligible handler claim this contact. */
    public suspend fun wouldClaimTouch(sample: PointerSample): Boolean = viewer.call { handle.wouldClaimTouch(sample) }

    /**
     * Returns at once, from any thread. Samples run in the order they were
     * sent. A sample sent after the viewer shut down is dropped.
     */
    public fun dispatchPointer(sample: PointerSample) {
        if (viewer.isReleased) return
        handle.dispatchPointer(sample)
    }

    /**
     * Tool, gesture and cursor events. Completes when the document closes (at
     * once if it already has) or the viewer is destroyed. Each collection is a
     * new subscription.
     */
    public fun observeEvents(): Flow<InteractionEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : InteractionEventListener {
                override fun onEvent(event: InteractionEvent) {
                    channel.trySend(event)
                }

                override fun onEnd() {
                    channel.close()
                }
            },
        )
    }
}

/**
 * The interaction capability for [document]. Throws `unsupported` when the
 * viewer was built without the interaction plugin, whatever the document's
 * state.
 */
public suspend fun PdfViewer.interaction(document: DocumentInfo): InteractionCapability =
    InteractionCapability(call { host.interaction(document.id) }, this)
