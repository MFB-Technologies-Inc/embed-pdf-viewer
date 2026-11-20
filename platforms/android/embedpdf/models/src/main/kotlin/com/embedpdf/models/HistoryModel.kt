package com.embedpdf.models

import com.embedpdf.HistoryCapability
import com.embedpdf.HistoryChangeLabel
import com.embedpdf.HistoryEvent
import com.embedpdf.HistoryEventSettingsChanged
import com.embedpdf.HistoryEventUndoFailed
import com.embedpdf.HistoryEventUndone
import com.embedpdf.HistoryLabelValueNumber
import com.embedpdf.HistoryLabelValueText
import com.embedpdf.I18nCapability
import com.embedpdf.I18nParam
import com.embedpdf.I18nParamNumber
import com.embedpdf.I18nParamText
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlin.coroutines.cancellation.CancellationException

/**
 * An observable model of a document's undo history, which undo and redo
 * controls read.
 *
 * Every state is a snapshot of the kernel, read again after each state
 * revision and history event while [run] runs, and after each action. A
 * refresh that started earlier never overwrites one that started later. A
 * failed read, as when the document closes, turns [canUndo] and [canRedo]
 * off and sets [error]; [run] ends with one more refresh, so a closed
 * document leaves them off.
 *
 * [undoTitle] and [redoTitle] are "Undo" and "Redo", followed by the change's
 * description when the model has a [translator]: "Undo Rotate pages". The
 * verbs are English; an app that words them another way builds its own title
 * from [undoLabel] and [redoLabel].
 *
 * The state lives on the main thread: every call switches to
 * `Dispatchers.Main.immediate` itself.
 */
public class HistoryModel(
    private val viewer: PdfViewer,
    private val history: HistoryCapability,
    private val translator: I18nCapability? = null,
) {
    private val _canUndo = MutableStateFlow(false)
    private val _canRedo = MutableStateFlow(false)
    private val _undoLabel = MutableStateFlow<HistoryChangeLabel?>(null)
    private val _redoLabel = MutableStateFlow<HistoryChangeLabel?>(null)
    private val _undoTitle = MutableStateFlow(UNDO)
    private val _redoTitle = MutableStateFlow(REDO)
    private val _submitting = MutableStateFlow(false)
    private val _notice = MutableStateFlow<String?>(null)
    private val _error = MutableStateFlow<PdfException?>(null)

    public val canUndo: StateFlow<Boolean> = _canUndo.asStateFlow()
    public val canRedo: StateFlow<Boolean> = _canRedo.asStateFlow()
    public val undoLabel: StateFlow<HistoryChangeLabel?> = _undoLabel.asStateFlow()
    public val redoLabel: StateFlow<HistoryChangeLabel?> = _redoLabel.asStateFlow()
    public val undoTitle: StateFlow<String> = _undoTitle.asStateFlow()
    public val redoTitle: StateFlow<String> = _redoTitle.asStateFlow()

    /** Whether an [undo] or [redo] is out; a second one asked for meanwhile is ignored. */
    public val submitting: StateFlow<Boolean> = _submitting.asStateFlow()

    /** Why the last undo or redo skipped some of its parts, `null` when it skipped none. */
    public val notice: StateFlow<String?> = _notice.asStateFlow()
    public val error: StateFlow<PdfException?> = _error.asStateFlow()

    internal val snapshots = SnapshotRefresh()
    private var observation = 0L

    /** Observes until the document closes or the calling coroutine is cancelled. */
    public suspend fun run(): Unit = onMain {
        val session = ++observation
        try {
            observeCapability(
                viewer = viewer,
                events = history.observeEvents(),
                initial = { if (observation == session) refresh() },
                refresh = { if (observation == session) refresh() },
                receive = { if (observation == session) receive(it) },
            )
            if (observation == session) refresh()
        } finally {
            if (observation == session) snapshots.invalidate()
        }
    }

    public suspend fun refresh(): Unit = onMain {
        snapshots.run(
            load = {
                val canUndo = history.canUndo()
                val canRedo = history.canRedo()
                val undo = history.undoLabel()
                val redo = history.redoLabel()
                Snapshot(canUndo, canRedo, undo, redo, title(undo, redo = false), title(redo, redo = true))
            },
            apply = {
                // A state flow keeps a value equal to the one it holds, so a
                // revision that leaves the history alone notifies no one.
                _canUndo.value = it.canUndo
                _canRedo.value = it.canRedo
                _undoLabel.value = it.undoLabel
                _redoLabel.value = it.redoLabel
                _undoTitle.value = it.undoTitle
                _redoTitle.value = it.redoTitle
            },
            failure = {
                _canUndo.value = false
                _canRedo.value = false
                _error.value = controlError(it)
            },
        )
    }

    public suspend fun undo(): Unit = submit { history.undo() }

    public suspend fun redo(): Unit = submit { history.redo() }

    internal suspend fun receive(event: HistoryEvent): Unit = onMain {
        when (event) {
            is HistoryEventUndoFailed -> {
                _error.value = PdfException(PdfException.Code(event.error.code), event.error.message)
                _notice.value = null
            }
            is HistoryEventUndone -> {
                _error.value = null
                _notice.value = when {
                    event.skipped == 0uL -> null
                    event.redo -> "Some changes could not be redone."
                    else -> "Some changes could not be undone."
                }
            }
            is HistoryEventSettingsChanged -> Unit
        }
        refresh()
    }

    private suspend fun submit(action: suspend () -> Unit): Unit = onMain {
        if (_submitting.value) return@onMain
        _submitting.value = true
        _error.value = null
        _notice.value = null
        try {
            try {
                action()
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                _error.value = controlError(e)
            }
            refresh()
        } finally {
            _submitting.value = false
        }
    }

    private suspend fun title(label: HistoryChangeLabel?, redo: Boolean): String {
        val verb = if (redo) REDO else UNDO
        val translator = translator ?: return verb
        if (label == null) return verb
        val params: Map<String, I18nParam> = label.values.mapValues { (_, value) ->
            when (value) {
                is HistoryLabelValueText -> I18nParamText(value.value)
                is HistoryLabelValueNumber -> I18nParamNumber(value.value)
            }
        }
        return "$verb ${translator.t(label.key, params, fallback = "change")}"
    }

    private data class Snapshot(
        val canUndo: Boolean,
        val canRedo: Boolean,
        val undoLabel: HistoryChangeLabel?,
        val redoLabel: HistoryChangeLabel?,
        val undoTitle: String,
        val redoTitle: String,
    )

    private companion object {
        const val UNDO = "Undo"
        const val REDO = "Redo"
    }
}
