package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_attachment.AttachmentEventListener
import uniffi.epdf_plugin_attachment.AttachmentHandle

public typealias AttachmentRef = uniffi.epdf_plugin_attachment.AttachmentRef
public typealias AttachmentHome = uniffi.epdf_plugin_attachment.AttachmentHome
public typealias AttachmentHomeDocument = uniffi.epdf_plugin_attachment.AttachmentHome.Document
public typealias AttachmentHomePage = uniffi.epdf_plugin_attachment.AttachmentHome.Page
public typealias AttachedFile = uniffi.epdf_plugin_attachment.AttachedFile
public typealias AttachmentFileSource = uniffi.epdf_plugin_attachment.AttachmentFileSource
public typealias AttachmentContent = uniffi.epdf_plugin_attachment.AttachmentContent
public typealias AttachmentEvent = uniffi.epdf_plugin_attachment.AttachmentEvent
public typealias AttachmentEventChanged = uniffi.epdf_plugin_attachment.AttachmentEvent.Changed
public typealias AttachmentEventResynced = uniffi.epdf_plugin_attachment.AttachmentEvent.Resynced

/**
 * Embedded files from the document and, when installed, its annotation
 * plugin. Get one with [PdfViewer.attachments]; the viewer needs
 * `ViewerConfig(attachment = true)`. After its document closes, every call
 * throws `instance-closed`.
 */
public class AttachmentCapability internal constructor(
    private val handle: AttachmentHandle,
    internal val viewer: PdfViewer,
) {
    public suspend fun files(): List<AttachedFile> = viewer.call { handle.files() }

    public suspend fun file(id: String): AttachedFile? = viewer.call { handle.file(id) }

    public suspend fun selectedFiles(): List<AttachedFile> = viewer.call { handle.selectedFiles() }

    public suspend fun select(files: List<AttachedFile>) {
        viewer.call { handle.select(files) }
    }

    public suspend fun isPending(file: AttachedFile): Boolean = viewer.call { handle.isPending(file) }

    public suspend fun status(): ResourceStatus = viewer.call { handle.status() }

    public suspend fun canDownload(): Boolean = viewer.call { handle.canDownload() }

    public suspend fun canAdd(): Boolean = viewer.call { handle.canAdd() }

    public suspend fun canRemove(file: AttachedFile): Boolean = viewer.call { handle.canRemove(file) }

    public suspend fun reveal(file: AttachedFile) {
        viewer.call { handle.reveal(file) }
    }

    public suspend fun ensureLoaded() {
        viewer.callCancellable { token -> handle.ensureLoaded(token) }
    }

    public suspend fun refresh() {
        viewer.callCancellable { token -> handle.refresh(token) }
    }

    /**
     * A change that started lands even when the caller is cancelled, and the caller then sees its
     * cancellation rather than the result (see the README's Cancellation section).
     */
    public suspend fun add(sources: List<AttachmentFileSource>): List<AttachedFile> =
        viewer.callCancellable { token -> handle.add(sources, token) }

    /**
     * A change that started lands even when the caller is cancelled, and the caller then sees its
     * cancellation rather than the result (see the README's Cancellation section).
     */
    public suspend fun remove(file: AttachedFile) {
        viewer.callCancellable { token -> handle.remove(file, token) }
    }

    public suspend fun download(file: AttachedFile): AttachmentContent =
        viewer.callCancellable { token -> handle.download(file, token) }

    /**
     * Attachment changes and resyncs. Completes when the document closes (at
     * once if it already has) or the viewer is destroyed. Each collection is a
     * new subscription.
     */
    public fun observeEvents(): Flow<AttachmentEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : AttachmentEventListener {
                override fun onEvent(event: AttachmentEvent) {
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
 * The attachment capability for [document]. Throws `unsupported` when the
 * viewer was built without the attachment plugin, whatever the document's
 * state.
 */
public suspend fun PdfViewer.attachments(document: DocumentInfo): AttachmentCapability =
    AttachmentCapability(call { host.attachments(document.id) }, this)
