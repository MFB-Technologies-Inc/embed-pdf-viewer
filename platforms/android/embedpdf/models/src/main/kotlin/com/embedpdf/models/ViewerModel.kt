package com.embedpdf.models

import com.embedpdf.DocumentInfo
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import kotlinx.coroutines.cancelChildren
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * An observable model of the viewer's open documents, which a view reads.
 *
 * [activeDocument] and [documentCount] are snapshots of the kernel, read
 * again after each state revision while [run] runs. A snapshot is one
 * revision behind the kernel until its refresh lands: read right after an
 * open or a close, it still holds the value from before. The [PdfViewer]
 * reads answer with the kernel's current value. A refresh that started
 * earlier never overwrites one that started later. A failed read, as when
 * the viewer is destroyed, empties the model and sets [error]; [run] ends
 * with one more refresh, so a destroyed viewer leaves it empty.
 *
 * The state lives on the main thread: every call switches to
 * `Dispatchers.Main.immediate` itself.
 */
public class ViewerModel(private val viewer: PdfViewer) {
    private val _activeDocument = MutableStateFlow<DocumentInfo?>(null)
    private val _documentCount = MutableStateFlow(0)
    private val _error = MutableStateFlow<PdfException?>(null)

    /** The active tab, which may be loading, failed or locked; `null` when no document is open. */
    public val activeDocument: StateFlow<DocumentInfo?> = _activeDocument.asStateFlow()

    /** Open tabs, including loading, failed and locked ones. */
    public val documentCount: StateFlow<Int> = _documentCount.asStateFlow()

    public val error: StateFlow<PdfException?> = _error.asStateFlow()

    internal val snapshots = SnapshotRefresh()
    private var observation = 0L

    /**
     * Observes until the viewer is destroyed or the calling coroutine is
     * cancelled. A later `run()` takes over from an earlier one still running.
     */
    public suspend fun run(): Unit = onMain {
        val session = ++observation
        try {
            coroutineScope {
                val revisions = subscribe(viewer.observeStateRevisions())
                refresh()
                for (revision in revisions) {
                    if (observation != session) break
                    refresh()
                }
                // A superseded run leaves its subscription behind.
                coroutineContext.cancelChildren()
                if (observation == session) refresh()
            }
        } finally {
            if (observation == session) snapshots.invalidate()
        }
    }

    public suspend fun refresh(): Unit = onMain {
        snapshots.run(
            load = { viewer.activeDocument() to viewer.documentCount() },
            apply = { (active, count) ->
                _activeDocument.value = active
                _documentCount.value = count
                _error.value = null
            },
            failure = {
                _activeDocument.value = null
                _documentCount.value = 0
                _error.value = controlError(it)
            },
        )
    }
}
