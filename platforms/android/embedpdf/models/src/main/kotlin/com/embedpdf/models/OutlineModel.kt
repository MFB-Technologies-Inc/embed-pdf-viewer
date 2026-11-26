package com.embedpdf.models

import com.embedpdf.LinkActivation
import com.embedpdf.OutlineCapability
import com.embedpdf.OutlineCreateOptions
import com.embedpdf.OutlineItem
import com.embedpdf.OutlineItemInput
import com.embedpdf.OutlineItemPatch
import com.embedpdf.OutlineItemRef
import com.embedpdf.OutlinePosition
import com.embedpdf.OutlinePositionEnd
import com.embedpdf.OutlineRow
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import com.embedpdf.ResourceStatus
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive

/**
 * An observable model of a document's outline (its bookmarks), which an
 * outline panel reads.
 *
 * Every state is a snapshot of the kernel, read again after each state
 * revision and outline event while [run] runs, and after each action. A
 * refresh that started earlier never overwrites one that started later, and
 * a revision that leaves the outline as it was notifies no one. [run] loads
 * the outline first. A failed read, as when the document closes, empties the
 * model and sets [error]; [run] ends with one more refresh, so a closed
 * document leaves it empty.
 *
 * [error] is the latest failure of an action or a read. A successful load
 * clears a read's failure and keeps an action's. An action asked for while
 * another is [performing] is ignored and returns `null`.
 *
 * The state lives on the main thread: every call switches to
 * `Dispatchers.Main.immediate` itself. An action whose caller is cancelled
 * records `operation-cancelled` in [error] and rethrows the cancellation;
 * the kernel may still have made the change (see the facade README's
 * Cancellation section), so read the state again after one.
 */
public class OutlineModel(
    private val viewer: PdfViewer,
    private val outline: OutlineCapability,
) {
    private val _items = MutableStateFlow<List<OutlineItem>>(emptyList())
    private val _rows = MutableStateFlow<List<OutlineRow>>(emptyList())
    private val _selectedItems = MutableStateFlow<List<OutlineItemRef>>(emptyList())
    private val _currentItem = MutableStateFlow<OutlineItemRef?>(null)
    private val _status = MutableStateFlow(ResourceStatus.IDLE)
    private val _incomplete = MutableStateFlow(false)
    private val _canEdit = MutableStateFlow(false)
    private val _performing = MutableStateFlow(false)
    private val _error = MutableStateFlow<PdfException?>(null)
    private val _lastActivation = MutableStateFlow<LinkActivation?>(null)

    public val items: StateFlow<List<OutlineItem>> = _items.asStateFlow()
    public val rows: StateFlow<List<OutlineRow>> = _rows.asStateFlow()
    public val selectedItems: StateFlow<List<OutlineItemRef>> = _selectedItems.asStateFlow()
    public val currentItem: StateFlow<OutlineItemRef?> = _currentItem.asStateFlow()
    public val status: StateFlow<ResourceStatus> = _status.asStateFlow()
    public val incomplete: StateFlow<Boolean> = _incomplete.asStateFlow()
    public val canEdit: StateFlow<Boolean> = _canEdit.asStateFlow()
    public val performing: StateFlow<Boolean> = _performing.asStateFlow()
    public val error: StateFlow<PdfException?> = _error.asStateFlow()

    /** What the last [activate] did, `null` before one and after one that failed. */
    public val lastActivation: StateFlow<LinkActivation?> = _lastActivation.asStateFlow()

    /** Whether [error] came from a read, which the next successful load clears. */
    private var errorIsReadFailure = false
    internal val snapshots = SnapshotRefresh()
    private var observation = 0L

    /** Loads the outline and observes until the document closes or the calling coroutine is cancelled. */
    public suspend fun run(): Unit = onMain {
        val session = ++observation
        try {
            observeCapability(
                viewer = viewer,
                events = outline.observeEvents(),
                initial = initial@{
                    if (observation != session) return@initial
                    try {
                        outline.ensureLoaded()
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
                Snapshot(
                    outline.list(), outline.rows(), outline.selectedItems(), outline.currentItem(),
                    outline.status(), outline.isIncomplete(), outline.canEdit(),
                )
            },
            apply = {
                // Most revisions are a scroll or a zoom that leaves the outline
                // as it was: write only what changed. Items hold an action
                // handle, which a state flow compares by identity and each read
                // lifts anew, so they're compared field by field.
                if (!sameList(it.items, _items.value, ::sameItem)) _items.value = it.items
                if (!sameList(it.rows, _rows.value, ::sameRow)) _rows.value = it.rows
                _selectedItems.value = it.selected
                _currentItem.value = it.current
                _status.value = it.status
                _incomplete.value = it.incomplete
                _canEdit.value = it.canEdit
            },
            failure = {
                _items.value = emptyList()
                _rows.value = emptyList()
                _selectedItems.value = emptyList()
                _currentItem.value = null
                _canEdit.value = false
                errorIsReadFailure = true
                _error.value = controlError(it)
            },
        )
    }

    public suspend fun reload() {
        perform { outline.refresh() }
    }

    public suspend fun select(refs: List<OutlineItemRef>) {
        perform { outline.select(refs) }
    }

    public suspend fun expand(ref: OutlineItemRef) {
        perform { outline.expand(ref) }
    }

    public suspend fun collapse(ref: OutlineItemRef) {
        perform { outline.collapse(ref) }
    }

    public suspend fun toggle(ref: OutlineItemRef) {
        perform { outline.toggle(ref) }
    }

    /** The new item, or `null` when the create failed (see [error]) or was ignored. */
    public suspend fun create(
        input: OutlineItemInput,
        options: OutlineCreateOptions = OutlineCreateOptions(select = true),
    ): OutlineItem? = perform { outline.create(input, options) }

    /** The updated item, or `null` when the update failed (see [error]) or was ignored. */
    public suspend fun update(ref: OutlineItemRef, patch: OutlineItemPatch): OutlineItem? =
        perform { outline.update(ref, patch) }

    public suspend fun move(refs: List<OutlineItemRef>, parent: OutlineItemRef?, position: OutlinePosition = OutlinePositionEnd) {
        perform { outline.move(refs, parent, position) }
    }

    public suspend fun delete(refs: List<OutlineItemRef>) {
        perform { outline.delete(refs) }
    }

    /** Selects the item and follows its target or action; also in [lastActivation]. */
    public suspend fun activate(ref: OutlineItemRef): LinkActivation? = onMain {
        val result = perform {
            outline.select(listOf(ref))
            outline.activate(ref)
        }
        _lastActivation.value = result
        result
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

    private data class Snapshot(
        val items: List<OutlineItem>,
        val rows: List<OutlineRow>,
        val selected: List<OutlineItemRef>,
        val current: OutlineItemRef?,
        val status: ResourceStatus,
        val incomplete: Boolean,
        val canEdit: Boolean,
    )

    private companion object {
        fun <T> sameList(a: List<T>, b: List<T>, same: (T, T) -> Boolean): Boolean =
            a.size == b.size && a.indices.all { same(a[it], b[it]) }

        fun sameRow(a: OutlineRow, b: OutlineRow): Boolean =
            a.depth == b.depth && a.hasChildren == b.hasChildren && a.expanded == b.expanded && sameItem(a.item, b.item)

        fun sameItem(a: OutlineItem, b: OutlineItem): Boolean {
            if (a.itemRef != b.itemRef || a.parent != b.parent || a.title != b.title || a.target != b.target ||
                a.open != b.open || a.color != b.color || a.bold != b.bold || a.italic != b.italic
            ) {
                return false
            }
            // Each read lifts a new handle, so equal actions compare by their JSON.
            val left = a.action
            val right = b.action
            return when {
                left == null || right == null -> left == null && right == null
                else -> left === right || left.json() == right.json()
            }
        }
    }
}
