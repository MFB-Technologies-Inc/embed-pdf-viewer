package com.embedpdf

import kotlinx.coroutines.flow.Flow
import kotlinx.serialization.DeserializationStrategy
import kotlinx.serialization.SerializationException
import kotlinx.serialization.SerializationStrategy
import kotlinx.serialization.json.Json
import kotlinx.serialization.serializer
import uniffi.epdf_plugin_shell.ShellEventListener
import uniffi.epdf_plugin_shell.ShellHandle

public typealias ShellSurface = uniffi.epdf_plugin_shell.ShellSurface
public typealias ShellOpenOptions = uniffi.epdf_plugin_shell.ShellOpenOptions
public typealias ShellSnapshot = uniffi.epdf_plugin_shell.ShellSnapshot
public typealias ShellSnapshotSurface = uniffi.epdf_plugin_shell.ShellSnapshotSurface
public typealias ShellSurfaceOpenedEvent = uniffi.epdf_plugin_shell.ShellSurfaceOpenedEvent
public typealias ShellSurfaceClosedEvent = uniffi.epdf_plugin_shell.ShellSurfaceClosedEvent
public typealias ShellMenuEvent = uniffi.epdf_plugin_shell.ShellMenuEvent
public typealias ShellEvent = uniffi.epdf_plugin_shell.ShellEvent
public typealias ShellEventSurfaceOpened = uniffi.epdf_plugin_shell.ShellEvent.SurfaceOpened
public typealias ShellEventSurfaceClosed = uniffi.epdf_plugin_shell.ShellEvent.SurfaceClosed
public typealias ShellEventMenuOpened = uniffi.epdf_plugin_shell.ShellEvent.MenuOpened
public typealias ShellEventMenuClosed = uniffi.epdf_plugin_shell.ShellEvent.MenuClosed

/**
 * Which named surfaces (panels, sheets, inspectors) and menus are open in one
 * document. Pure state: your views decide what a surface looks like and bind
 * to this. Get one with [PdfViewer.shell]; the viewer needs
 * `ViewerConfig(shell = true)`. A surface's props are a JSON object, given as
 * text or as any value whose serializer encodes it to an object. After its
 * document closes, every call throws `instance-closed`.
 */
public class ShellCapability internal constructor(
    private val handle: ShellHandle,
    internal val viewer: PdfViewer,
) {
    public suspend fun isOpen(id: String): Boolean = viewer.call { handle.isOpen(id) }

    /**
     * `null` for a surface never opened. The TypeScript plugin keeps surfaces
     * in a plain object and reads `surfaces[id]`, so an id naming an
     * `Object.prototype` member (`"constructor"`, `"toString"`) is found
     * there; it is found here too, closed, with no tag or props.
     */
    public suspend fun surface(id: String): ShellSurface? = viewer.call { handle.surface(id) }

    public suspend fun openSurfaces(): List<ShellSurface> = viewer.call { handle.openSurfaces() }

    /** A tagged open closes every other open surface with the same tag. */
    public suspend fun open(id: String, exclusive: String? = null, propsJson: String? = null) {
        viewer.call { handle.openSurface(id, ShellOpenOptions(exclusive = exclusive, propsJson = propsJson)) }
    }

    /** [open] with props encoded by [serializer]. Props that don't encode to a JSON object throw `invalid-input`. */
    public suspend fun <P> open(id: String, exclusive: String? = null, props: P, serializer: SerializationStrategy<P>) {
        open(id, exclusive, encodeProps(serializer, props))
    }

    public suspend fun close(id: String) {
        viewer.call { handle.closeSurface(id) }
    }

    public suspend fun toggle(id: String, exclusive: String? = null, propsJson: String? = null) {
        viewer.call { handle.toggleSurface(id, ShellOpenOptions(exclusive = exclusive, propsJson = propsJson)) }
    }

    /** [toggle] with props encoded by [serializer]. */
    public suspend fun <P> toggle(id: String, exclusive: String? = null, props: P, serializer: SerializationStrategy<P>) {
        toggle(id, exclusive, encodeProps(serializer, props))
    }

    /** Changes a surface's props without reopening it. */
    public suspend fun updateProps(surfaceId: String, propsJson: String) {
        viewer.call { handle.updateSurfaceProps(surfaceId, propsJson) }
    }

    /** [updateProps] with props encoded by [serializer]. */
    public suspend fun <P> updateProps(surfaceId: String, props: P, serializer: SerializationStrategy<P>) {
        updateProps(surfaceId, encodeProps(serializer, props))
    }

    public suspend fun closeAll() {
        viewer.call { handle.closeAllSurfaces() }
    }

    public suspend fun isMenuOpen(id: String): Boolean = viewer.call { handle.isMenuOpen(id) }

    /** In opening order; the last is topmost. */
    public suspend fun openMenus(): List<String> = viewer.call { handle.openMenus() }

    public suspend fun openMenu(id: String) {
        viewer.call { handle.openMenu(id) }
    }

    public suspend fun closeMenu(id: String) {
        viewer.call { handle.closeMenu(id) }
    }

    public suspend fun toggleMenu(id: String) {
        viewer.call { handle.toggleMenu(id) }
    }

    public suspend fun closeAllMenus() {
        viewer.call { handle.closeAllMenus() }
    }

    /** Everything this document's shell holds, for persist and restore. */
    public suspend fun snapshot(): ShellSnapshot = viewer.call { handle.snapshot() }

    public suspend fun applySnapshot(snapshot: ShellSnapshot) {
        viewer.call { handle.applySnapshot(snapshot) }
    }

    /**
     * Surface and menu events. Completes when the document closes (at once if
     * it already has) or the viewer is destroyed. Each collection is a new
     * subscription.
     */
    public fun observeEvents(): Flow<ShellEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : ShellEventListener {
                override fun onEvent(event: ShellEvent) {
                    channel.trySend(event)
                }

                override fun onEnd() {
                    channel.close()
                }
            },
        )
    }
}

/** [ShellCapability.open] with props encoded by their `@Serializable` serializer. */
public suspend inline fun <reified P> ShellCapability.open(id: String, exclusive: String? = null, props: P) {
    open(id, exclusive, props, serializer<P>())
}

/** [ShellCapability.toggle] with props encoded by their `@Serializable` serializer. */
public suspend inline fun <reified P> ShellCapability.toggle(id: String, exclusive: String? = null, props: P) {
    toggle(id, exclusive, props, serializer<P>())
}

/** [ShellCapability.updateProps] with props encoded by their `@Serializable` serializer. */
public suspend inline fun <reified P> ShellCapability.updateProps(surfaceId: String, props: P) {
    updateProps(surfaceId, props, serializer<P>())
}

/** The props decoded by [deserializer], or `null` when the surface has none. */
public fun <P> ShellSurface.props(deserializer: DeserializationStrategy<P>): P? = propsJson?.let { decodeProps(deserializer, it) }

/** The props decoded by their `@Serializable` serializer, or `null` when the surface has none. */
public inline fun <reified P> ShellSurface.props(): P? = props(serializer<P>())

/** The props decoded by [deserializer], or `null` when the surface opened without any. */
public fun <P> ShellSurfaceOpenedEvent.props(deserializer: DeserializationStrategy<P>): P? =
    propsJson?.let { decodeProps(deserializer, it) }

/** The props decoded by their `@Serializable` serializer, or `null` when the surface opened without any. */
public inline fun <reified P> ShellSurfaceOpenedEvent.props(): P? = props(serializer<P>())

/** Props that don't decode with [deserializer] throw `invalid-input`. */
private fun <P> decodeProps(deserializer: DeserializationStrategy<P>, json: String): P =
    try {
        Json.decodeFromString(deserializer, json)
    } catch (e: SerializationException) {
        throw PdfException(PdfException.Code.INVALID_INPUT, readable(e), name = "DecodingError", cause = e)
    }

private fun <P> encodeProps(serializer: SerializationStrategy<P>, props: P): String =
    try {
        Json.encodeToString(serializer, props)
    } catch (e: SerializationException) {
        throw PdfException(PdfException.Code.INVALID_INPUT, readable(e), name = "EncodingError", cause = e)
    }

/**
 * The shell capability for [document]. Throws `unsupported` when the viewer
 * was built without the shell plugin, whatever the document's state.
 */
public suspend fun PdfViewer.shell(document: DocumentInfo): ShellCapability =
    ShellCapability(call { host.shell(document.id) }, this)
