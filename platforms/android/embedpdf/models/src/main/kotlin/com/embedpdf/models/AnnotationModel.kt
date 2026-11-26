package com.embedpdf.models

import com.embedpdf.AnnotationCapability
import com.embedpdf.PdfException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * An observable model of what the session may do with a document's
 * annotations, which a view reads.
 *
 * [canRead] and [canCreate] are snapshots of the kernel, read again after
 * each state revision and annotation event while [run] runs. A snapshot is
 * one revision behind the kernel until its refresh lands: read right after a
 * change, it still holds the value from before. The [AnnotationCapability]
 * reads answer with the kernel's current value. A refresh that started
 * earlier never overwrites one that started later. Both are `false` until the
 * first refresh lands and after a read fails, as when the document closes.
 *
 * The state lives on the main thread: every call switches to
 * `Dispatchers.Main.immediate` itself.
 */
public class AnnotationModel(private val annotations: AnnotationCapability) {
    private val _canRead = MutableStateFlow(false)
    private val _canCreate = MutableStateFlow(false)
    private val _error = MutableStateFlow<PdfException?>(null)

    public val canRead: StateFlow<Boolean> = _canRead.asStateFlow()
    public val canCreate: StateFlow<Boolean> = _canCreate.asStateFlow()
    public val error: StateFlow<PdfException?> = _error.asStateFlow()

    internal val snapshots = SnapshotRefresh()
    private var observation = 0L

    /** Observes until the document closes or the calling coroutine is cancelled. */
    public suspend fun run(): Unit = onMain {
        val session = ++observation
        try {
            observeCapability(
                viewer = annotations.viewer,
                events = annotations.observeEvents(),
                initial = { if (observation == session) refresh() },
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
            load = { annotations.canRead() to annotations.canCreate() },
            apply = { (canRead, canCreate) ->
                _canRead.value = canRead
                _canCreate.value = canCreate
                _error.value = null
            },
            failure = {
                _canRead.value = false
                _canCreate.value = false
                _error.value = controlError(it)
            },
        )
    }
}
