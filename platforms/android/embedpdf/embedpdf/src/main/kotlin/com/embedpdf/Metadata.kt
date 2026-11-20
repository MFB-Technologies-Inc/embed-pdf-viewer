package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_metadata.MetadataEventListener
import uniffi.epdf_plugin_metadata.MetadataHandle

public typealias CustomMetadata = Map<String, String>
public typealias CustomMetadataUpdatedEvent = uniffi.epdf_plugin_metadata.CustomMetadataUpdatedEvent
public typealias DocumentMetadata = uniffi.epdf_plugin_metadata.DocumentMetadata
public typealias DocumentMetadataTrapped = uniffi.epdf_plugin_metadata.DocumentMetadataTrapped
public typealias MetadataPatch = uniffi.epdf_plugin_metadata.MetadataPatch
public typealias MetadataField = uniffi.epdf_plugin_metadata.MetadataField
public typealias MetadataUpdatedEvent = uniffi.epdf_plugin_metadata.MetadataUpdatedEvent
public typealias ResourceStatus = uniffi.epdf_ffi.ResourceStatus
public typealias MetadataEvent = uniffi.epdf_plugin_metadata.MetadataEvent
public typealias MetadataEventUpdated = uniffi.epdf_plugin_metadata.MetadataEvent.Updated
public typealias MetadataEventResynced = uniffi.epdf_plugin_metadata.MetadataEvent.Resynced
public typealias MetadataEventCustomUpdated = uniffi.epdf_plugin_metadata.MetadataEvent.CustomUpdated
public typealias MetadataEventCustomResynced = uniffi.epdf_plugin_metadata.MetadataEvent.CustomResynced

/**
 * A document's Info dictionary, read and edited. Get one with
 * [PdfViewer.metadata]; the viewer needs `ViewerConfig(metadata = true)`. It
 * keeps the viewer alive. After its document closes, every call throws
 * `instance-closed`.
 */
public class MetadataCapability internal constructor(
    private val handle: MetadataHandle,
    internal val viewer: PdfViewer,
) {
    /** The custom keys: `null` until the first read lands. */
    public suspend fun customSnapshot(): CustomMetadata? = viewer.call { handle.customSnapshot() }

    public suspend fun customStatus(): ResourceStatus = viewer.call { handle.customStatus() }

    /** Sets keys mapped to a value and removes keys mapped to `null`; a key left out stays. */
    public suspend fun updateCustom(patch: Map<String, String?>): CustomMetadata =
        viewer.callCancellable { token -> handle.updateCustom(patch, token) }

    /** Re-reads the custom keys. */
    public suspend fun refreshCustom() {
        viewer.callCancellable { token -> handle.refreshCustom(token) }
    }

    /**
     * The current metadata: `null` until the first read lands, or while
     * reading is forbidden (see [status]).
     */
    public suspend fun snapshot(): DocumentMetadata? = viewer.call { handle.snapshot() }

    public suspend fun status(): ResourceStatus = viewer.call { handle.status() }

    /** Whether this caller may change the Info dict (`doc.metadata.modify`). */
    public suspend fun canUpdate(): Boolean = viewer.call { handle.canUpdate() }

    /**
     * Reads the fields unless they are loaded or loading. Nothing reads them
     * until this or [refresh] asks. Cancelling throws here; the shared read
     * still lands.
     */
    public suspend fun ensureLoaded() {
        viewer.callCancellable { token -> handle.ensureLoaded(token) }
    }

    /** Returns once the confirmed change has reached the snapshot and [observeEvents]. */
    public suspend fun update(patch: MetadataPatch): DocumentMetadata =
        viewer.callCancellable { token -> handle.update(patch, token) }

    public suspend fun refresh() {
        viewer.callCancellable { token -> handle.refresh(token) }
    }

    /**
     * Confirmed changes, whoever made them. Completes when the document closes
     * (at once if it already has) or the viewer is destroyed. Each collection
     * is a new subscription.
     */
    public fun observeEvents(): Flow<MetadataEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : MetadataEventListener {
                override fun onEvent(event: MetadataEvent) {
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
 * The metadata capability for [document]. Throws `unsupported` when the
 * viewer was built without the metadata plugin, whatever the document's state.
 */
public suspend fun PdfViewer.metadata(document: DocumentInfo): MetadataCapability =
    MetadataCapability(call { host.metadata(document.id) }, this)
