package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_history.HistoryEventListener
import uniffi.epdf_plugin_history.HistoryHandle

public typealias HistoryChangeLabel = uniffi.epdf_plugin_history.HistoryChangeLabel
public typealias HistoryLabelValue = uniffi.epdf_plugin_history.HistoryLabelValue
public typealias HistoryLabelValueText = uniffi.epdf_plugin_history.HistoryLabelValue.Text
public typealias HistoryLabelValueNumber = uniffi.epdf_plugin_history.HistoryLabelValue.Number
public typealias HistorySettings = uniffi.epdf_plugin_history.HistorySettings
public typealias HistorySettingsPatch = uniffi.epdf_plugin_history.HistorySettingsPatch
public typealias HistoryUndoFailure = uniffi.epdf_plugin_history.HistoryUndoFailure
public typealias HistoryEvent = uniffi.epdf_plugin_history.HistoryEvent
public typealias HistoryEventUndone = uniffi.epdf_plugin_history.HistoryEvent.Undone
public typealias HistoryEventUndoFailed = uniffi.epdf_plugin_history.HistoryEvent.UndoFailed
public typealias HistoryEventSettingsChanged = uniffi.epdf_plugin_history.HistoryEvent.SettingsChanged

/**
 * A document's history. [undo] and [redo] return when submitted; events
 * report confirmation or refusal. Get one with [PdfViewer.history]; the viewer
 * needs `ViewerConfig(history = true)`. After its document closes, every call
 * throws `instance-closed`.
 */
public class HistoryCapability internal constructor(
    private val handle: HistoryHandle,
    internal val viewer: PdfViewer,
) {
    public suspend fun canUndo(): Boolean = viewer.call { handle.canUndo() }

    public suspend fun canRedo(): Boolean = viewer.call { handle.canRedo() }

    public suspend fun undoLabel(): HistoryChangeLabel? = viewer.call { handle.undoLabel() }

    public suspend fun redoLabel(): HistoryChangeLabel? = viewer.call { handle.redoLabel() }

    public suspend fun undo() {
        viewer.call { handle.undo() }
    }

    public suspend fun redo() {
        viewer.call { handle.redo() }
    }

    public suspend fun clear() {
        viewer.call { handle.clear() }
    }

    public suspend fun settings(): HistorySettings = viewer.call { handle.settings() }

    public suspend fun updateSettings(patch: HistorySettingsPatch) {
        viewer.call { handle.updateSettings(patch) }
    }

    public suspend fun resetSettings() {
        viewer.call { handle.resetSettings() }
    }

    /**
     * Undo and redo outcomes and settings changes. Completes when the document
     * closes (at once if it already has) or the viewer is destroyed. Each
     * collection is a new subscription.
     */
    public fun observeEvents(): Flow<HistoryEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : HistoryEventListener {
                override fun onEvent(event: HistoryEvent) {
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
 * The history capability for [document]. Throws `unsupported` when the viewer
 * was built without the history plugin, whatever the document's state.
 */
public suspend fun PdfViewer.history(document: DocumentInfo): HistoryCapability =
    HistoryCapability(call { host.history(document.id) }, this)
