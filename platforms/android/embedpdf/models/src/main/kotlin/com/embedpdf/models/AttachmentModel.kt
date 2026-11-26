package com.embedpdf.models

import android.content.ContentResolver
import android.net.Uri
import android.provider.OpenableColumns
import com.embedpdf.AttachedFile
import com.embedpdf.AttachmentCapability
import com.embedpdf.AttachmentContent
import com.embedpdf.AttachmentFileSource
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import com.embedpdf.ResourceStatus
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.withContext
import java.io.File
import java.io.FileNotFoundException
import java.net.URLConnection

/**
 * An observable model of a document's attached files, which an attachment
 * panel reads.
 *
 * Every state is a snapshot of the kernel, read again after each state
 * revision and attachment event while [run] runs, and after each action. A
 * refresh that started earlier never overwrites one that started later, and
 * a revision that leaves the files as they were notifies no one. [run] loads
 * the attachments first. A failed read, as when the document closes, empties
 * the model and sets [error]; [run] ends with one more refresh, so a closed
 * document leaves it empty.
 *
 * [error] is the latest failure of an action, a file operation or a read. A
 * successful load clears a read's failure and keeps the others. An action
 * asked for while another is [performing] is ignored.
 *
 * The state lives on the main thread: every call switches to
 * `Dispatchers.Main.immediate` itself, and files are read on
 * `Dispatchers.IO`. An action whose caller is cancelled records
 * `operation-cancelled` in [error] and rethrows the cancellation; an add the
 * kernel had already started may still land, so read the state again after
 * one.
 */
public class AttachmentModel(
    private val viewer: PdfViewer,
    private val attachments: AttachmentCapability,
) {
    private val _files = MutableStateFlow<List<AttachedFile>>(emptyList())
    private val _selectedFiles = MutableStateFlow<List<AttachedFile>>(emptyList())
    private val _status = MutableStateFlow(ResourceStatus.IDLE)
    private val _canAdd = MutableStateFlow(false)
    private val _canDownload = MutableStateFlow(false)
    private val _canRemoveSelection = MutableStateFlow(false)
    private val _pendingIds = MutableStateFlow<Set<String>>(emptySet())
    private val _performing = MutableStateFlow(false)
    private val _error = MutableStateFlow<PdfException?>(null)

    public val files: StateFlow<List<AttachedFile>> = _files.asStateFlow()
    public val selectedFiles: StateFlow<List<AttachedFile>> = _selectedFiles.asStateFlow()
    public val status: StateFlow<ResourceStatus> = _status.asStateFlow()
    public val canAdd: StateFlow<Boolean> = _canAdd.asStateFlow()
    public val canDownload: StateFlow<Boolean> = _canDownload.asStateFlow()

    /** Whether something is selected and every selected file may be removed. */
    public val canRemoveSelection: StateFlow<Boolean> = _canRemoveSelection.asStateFlow()

    /** The ids of the files whose bytes are still loading. */
    public val pendingIds: StateFlow<Set<String>> = _pendingIds.asStateFlow()
    public val performing: StateFlow<Boolean> = _performing.asStateFlow()
    public val error: StateFlow<PdfException?> = _error.asStateFlow()

    private var errorIsReadFailure = false
    internal val snapshots = SnapshotRefresh()
    private var observation = 0L

    /** Loads the attachments and observes until the document closes or the calling coroutine is cancelled. */
    public suspend fun run(): Unit = onMain {
        val session = ++observation
        try {
            observeCapability(
                viewer = viewer,
                events = attachments.observeEvents(),
                initial = initial@{
                    if (observation != session) return@initial
                    try {
                        attachments.ensureLoaded()
                        if (observation != session || !currentCoroutineContext().isActive) return@initial
                        if (errorIsReadFailure) {
                            _error.value = null
                            errorIsReadFailure = false
                        }
                    } catch (e: CancellationException) {
                        throw e
                    } catch (e: Exception) {
                        if (observation != session) return@initial
                        errorIsReadFailure = true
                        _error.value = controlError(e)
                    }
                    refresh()
                },
                refresh = { if (observation == session) refresh() },
                receive = { if (observation == session) refresh() },
            )
            if (observation == session) refresh()
        } finally {
            if (observation == session) snapshots.invalidate()
        }
    }

    public suspend fun refresh(): Unit = onMain {
        snapshots.run(
            load = {
                val files = attachments.files()
                val selected = attachments.selectedFiles()
                val status = attachments.status()
                val add = attachments.canAdd()
                val download = attachments.canDownload()
                var remove = selected.isNotEmpty()
                for (file in selected) if (!attachments.canRemove(file)) remove = false
                val pending = buildSet { for (file in files) if (attachments.isPending(file)) add(file.id) }
                Snapshot(files, selected, status, add, download, remove, pending)
            },
            apply = {
                _files.value = it.files
                _selectedFiles.value = it.selected
                _status.value = it.status
                _canAdd.value = it.canAdd
                _canDownload.value = it.canDownload
                _canRemoveSelection.value = it.canRemove
                _pendingIds.value = it.pending
            },
            failure = {
                _files.value = emptyList()
                _selectedFiles.value = emptyList()
                _pendingIds.value = emptySet()
                _canAdd.value = false
                _canDownload.value = false
                _canRemoveSelection.value = false
                errorIsReadFailure = true
                _error.value = controlError(it)
            },
        )
    }

    public suspend fun reload() {
        perform { attachments.refresh() }
    }

    public suspend fun select(files: List<AttachedFile>) {
        perform { attachments.select(files) }
    }

    public suspend fun add(sources: List<AttachmentFileSource>) {
        perform { attachments.add(sources) }
    }

    public suspend fun remove(file: AttachedFile) {
        perform { attachments.remove(file) }
    }

    public suspend fun reveal(file: AttachedFile) {
        perform { attachments.reveal(file) }
    }

    /** The file's bytes, or `null` when the download failed (see [error]) or was ignored. */
    public suspend fun download(file: AttachedFile): AttachmentContent? = perform { attachments.download(file) }

    /**
     * Attaches each file, named by its file name, with the MIME type its
     * extension suggests. A file that can't be read sets [error] and attaches
     * none of them.
     */
    public suspend fun importFiles(files: List<File>) {
        perform {
            val sources = withContext(Dispatchers.IO) {
                files.map { file ->
                    ensureActive()
                    AttachmentFileSource(
                        data = file.readBytes(),
                        name = file.name,
                        mimeType = URLConnection.guessContentTypeFromName(file.name),
                    )
                }
            }
            currentCoroutineContext().ensureActive()
            attachments.add(sources)
        }
    }

    /**
     * Attaches each document [resolver] opens, as a document picker returns
     * them, named by its display name, with the type the resolver reports.
     * One that can't be read sets [error] and attaches none of them.
     */
    public suspend fun importFiles(uris: List<Uri>, resolver: ContentResolver) {
        perform {
            val sources = withContext(Dispatchers.IO) {
                uris.map { uri ->
                    ensureActive()
                    val bytes = resolver.openInputStream(uri)?.use { it.readBytes() }
                        ?: throw FileNotFoundException("no content at $uri")
                    AttachmentFileSource(data = bytes, name = displayName(uri, resolver), mimeType = resolver.getType(uri))
                }
            }
            currentCoroutineContext().ensureActive()
            attachments.add(sources)
        }
    }

    /**
     * Shows the failure of a file operation the app ran itself, such as
     * writing a download out, on the main thread. A cancellation is not a
     * failure and is ignored.
     */
    public fun fileOperationFailed(failure: Throwable) {
        if (failure is CancellationException) return
        errorIsReadFailure = false
        _error.value = controlError(failure)
    }

    private suspend fun <T> perform(action: suspend () -> T): T? = onMain {
        if (_performing.value) return@onMain null
        _performing.value = true
        errorIsReadFailure = false
        _error.value = null
        try {
            val value = action()
            refresh()
            value
        } catch (e: CancellationException) {
            errorIsReadFailure = false
            _error.value = controlError(e)
            throw e
        } catch (e: Exception) {
            refresh()
            errorIsReadFailure = false
            _error.value = controlError(e)
            null
        } finally {
            _performing.value = false
        }
    }

    private fun displayName(uri: Uri, resolver: ContentResolver): String {
        val named = resolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME), null, null, null)?.use { cursor ->
            if (cursor.moveToFirst()) cursor.getString(0) else null
        }
        return named ?: uri.lastPathSegment ?: "attachment"
    }

    private data class Snapshot(
        val files: List<AttachedFile>,
        val selected: List<AttachedFile>,
        val status: ResourceStatus,
        val canAdd: Boolean,
        val canDownload: Boolean,
        val canRemove: Boolean,
        val pending: Set<String>,
    )
}
