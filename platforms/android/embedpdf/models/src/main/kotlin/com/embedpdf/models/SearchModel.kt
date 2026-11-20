package com.embedpdf.models

import com.embedpdf.PageRef
import com.embedpdf.PdfException
import com.embedpdf.SearchCapability
import com.embedpdf.SearchEvent
import com.embedpdf.SearchEventActiveHitChanged
import com.embedpdf.SearchEventProgressChanged
import com.embedpdf.SearchHit
import com.embedpdf.SearchProgress
import com.embedpdf.SearchQuery
import com.embedpdf.SearchStatus
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import java.text.NumberFormat

/**
 * An observable model of a document's search session, which a view reads.
 *
 * [status], [activeHitIndex], [hitCount], [progress] and [hits] are
 * snapshots of the kernel, read again after each state revision and search
 * event while [run] runs, and after each action. A snapshot is one revision
 * behind the kernel until its refresh lands: read right after a change, it
 * still holds the value from before. The [SearchCapability] reads answer
 * with the kernel's current value. A refresh that started earlier never
 * overwrites one that started later. A failed read, as when the document
 * closes, empties the model and sets [error]; [run] ends with one more
 * refresh, so a closed document leaves the model empty.
 *
 * [error] is the latest failure of an action or a read. A successful
 * background refresh does not clear it; a successful [searchSubmitted] or
 * [clearButtonTapped] does, so a caller that polls it can miss a failure one
 * of those cleared. After a refused [searchSubmitted], [status] stays
 * `ERROR` through background refreshes until the next submitted search or
 * [clearButtonTapped].
 *
 * The search plugin reruns its query by itself after every confirmed edit.
 * Each search event rereads [hits], so a rerun that finds as many hits in
 * other places still reaches the model.
 *
 * The state lives on the main thread: every call switches to
 * `Dispatchers.Main.immediate` itself. Set [text] on the main thread.
 */
public class SearchModel(internal val search: SearchCapability) {
    private val _text = MutableStateFlow("")
    private val _hits = MutableStateFlow<List<SearchHit>>(emptyList())
    private val _activeHitIndex = MutableStateFlow<Int?>(null)
    private val _status = MutableStateFlow(SearchStatus.IDLE)
    private val _hitCount = MutableStateFlow(0)
    private val _progress = MutableStateFlow(EMPTY_PROGRESS)
    private val _error = MutableStateFlow<PdfException?>(null)
    private val _activeHit = MutableStateFlow<SearchHit?>(null)
    private val _summary = MutableStateFlow<String?>(null)
    private val _canClear = MutableStateFlow(false)

    /** The search field's text, which [searchSubmitted] searches for; [textFlow] follows it. */
    public var text: String
        get() = _text.value
        set(value) {
            _text.value = value
            derive()
        }

    /** [text] as a flow. */
    public val textFlow: StateFlow<String> = _text.asStateFlow()
    public val hits: StateFlow<List<SearchHit>> = _hits.asStateFlow()

    /** The active hit's position in [hits], `null` when no hit is active. */
    public val activeHitIndex: StateFlow<Int?> = _activeHitIndex.asStateFlow()
    public val status: StateFlow<SearchStatus> = _status.asStateFlow()

    /** Hits found so far in the whole document. */
    public val hitCount: StateFlow<Int> = _hitCount.asStateFlow()
    public val progress: StateFlow<SearchProgress> = _progress.asStateFlow()
    public val error: StateFlow<PdfException?> = _error.asStateFlow()

    /**
     * The hit at [activeHitIndex] in [hits], `null` when no hit is active.
     *
     * The index and the list come from separate reads. A refresh already
     * reading when a new search starts can apply the old search's index with
     * the new search's list. For that one refresh the index either points
     * past the list, which reads `null`, or lands inside it, and this is then
     * a hit of the new search that may not be its active one. The next
     * refresh corrects it.
     */
    public val activeHit: StateFlow<SearchHit?> = _activeHit.asStateFlow()

    /**
     * The active hit's place among the hits, counted from 1, as `"3 of 12"`;
     * `null` when no hit is active, which includes a session with no hits.
     * While [status] is `SEARCHING` the count still grows, so the text ends
     * in a plus sign: `"3 of 12+"`. The numbers are grouped as the default
     * locale groups them, and the words are English; an app that words it
     * another way builds its own from [activeHitIndex], [hitCount] and
     * [status].
     */
    public val summary: StateFlow<String?> = _summary.asStateFlow()

    /**
     * Whether a clear button has anything to clear: text in the field, hits,
     * or a session that is not idle. A running scan with no hits yet and a
     * refused search's `ERROR` both count, with an empty field too.
     */
    public val canClear: StateFlow<Boolean> = _canClear.asStateFlow()

    private var pageHitCounts: Map<PageRef, Int> = emptyMap()
    private var query: SearchQuery? = null

    /** Set by a submitted search and cleared only when a hit list is applied. */
    private var hitsOutdated = false

    /** Set by a refused [searchSubmitted], so a background refresh keeps `ERROR`. */
    private var submissionRefused = false
    internal val snapshots = SnapshotRefresh()
    private var observation = 0L

    /** Hits found so far on one page, counted from [hits]. */
    public fun hitCount(page: PageRef): Int = pageHitCounts[page] ?: 0

    /** Observes until the document closes or the calling coroutine is cancelled. */
    public suspend fun run(): Unit = onMain {
        val session = ++observation
        try {
            observeCapability(
                viewer = search.viewer,
                events = search.observeEvents(),
                initial = { if (observation == session) refresh() },
                refresh = { if (observation == session) refresh() },
                receive = { event ->
                    if (observation == session) {
                        // A run started, finished or was cleared, the plugin's
                        // own rerun after an edit included: the hit list may
                        // have moved even when its count didn't.
                        if (event.changesHits) hitsOutdated = true
                        refresh()
                    }
                },
            )
            if (observation == session) refresh()
        } finally {
            if (observation == session) snapshots.invalidate()
        }
    }

    public suspend fun refresh(): Unit = onMain {
        snapshots.run(
            load = {
                val status = search.status()
                // Performance: the active index here and the hit list below
                // are two of this refresh's bridge round trips, and a search
                // that starts between them pairs the old index with the new
                // list (see `activeHit`).
                val activeHitIndex = search.activeHitIndex()
                val hitCount = search.hitCount()
                val progress = search.progress()
                val query = search.query()
                // Between searches the hit list changes only with the query,
                // the count or the status, and a revision is often a scroll or
                // a zoom: keep the list then.
                val unchanged = !hitsOutdated && query == this@SearchModel.query && hitCount == _hits.value.size &&
                    status == _status.value
                val hits = if (unchanged) null else search.hits()
                Snapshot(status, activeHitIndex, hitCount, progress, query, hits)
            },
            apply = {
                _status.value = if (submissionRefused) SearchStatus.ERROR else it.status
                _activeHitIndex.value = it.activeHitIndex
                _hitCount.value = it.hitCount
                _progress.value = it.progress
                query = it.query
                if (it.hits != null) {
                    hitsOutdated = false
                    if (_hits.value != it.hits) {
                        _hits.value = it.hits
                        pageHitCounts = it.hits.groupingBy { hit -> hit.page }.eachCount()
                    }
                }
                derive()
            },
            failure = {
                _status.value = SearchStatus.IDLE
                _activeHitIndex.value = null
                _hitCount.value = 0
                _progress.value = EMPTY_PROGRESS
                query = null
                _hits.value = emptyList()
                pageHitCounts = emptyMap()
                _error.value = controlError(it)
                derive()
            },
        )
    }

    /** Searches for [text]. A refused search sets [error] and [status] `ERROR`. */
    public suspend fun searchSubmitted(): Unit = onMain {
        try {
            search.search(SearchQuery(text = _text.value))
            _error.value = null
            submissionRefused = false
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            snapshots.invalidate()
            _error.value = controlError(e)
            submissionRefused = true
            _status.value = SearchStatus.ERROR
            derive()
            return@onMain
        }
        hitsOutdated = true
        refresh()
    }

    public suspend fun nextButtonTapped(): Unit = onMain {
        if (attempt { search.nextHit() }) refresh()
    }

    public suspend fun previousButtonTapped(): Unit = onMain {
        if (attempt { search.previousHit() }) refresh()
    }

    /**
     * Scrolls the document's stage to the active hit, placed as the search
     * settings say: the reveal a next or previous step already makes. The
     * first hit of a new search becomes active without one, so a view calls
     * this to bring it into view. Repeating it is safe, and it leaves the
     * active hit as it is. A failure lands in [error].
     */
    public suspend fun revealActiveHit(): Unit = onMain {
        attempt { search.revealActiveHit() }
    }

    public suspend fun clearButtonTapped(): Unit = onMain {
        val cleared = attempt { search.clear() }
        if (!cleared) return@onMain
        _error.value = null
        submissionRefused = false
        refresh()
    }

    /** Runs [action], putting a failure in [error]. Whether it succeeded. */
    private suspend fun attempt(action: suspend () -> Unit): Boolean = try {
        action()
        true
    } catch (e: CancellationException) {
        throw e
    } catch (e: Exception) {
        _error.value = controlError(e)
        false
    }

    /** Recomputes the states that follow from the others. */
    private fun derive() {
        val index = _activeHitIndex.value
        val hits = _hits.value
        _activeHit.value = index?.let { hits.getOrNull(it) }
        _summary.value = summary(index, _hitCount.value, _status.value)
        _canClear.value = _text.value.isNotEmpty() || _hitCount.value > 0 || _status.value != SearchStatus.IDLE
    }

    private data class Snapshot(
        val status: SearchStatus,
        val activeHitIndex: Int?,
        val hitCount: Int,
        val progress: SearchProgress,
        val query: SearchQuery?,
        val hits: List<SearchHit>?,
    )

    internal companion object {
        val EMPTY_PROGRESS = SearchProgress(pagesSearched = 0.0, pageCount = 0.0)

        fun summary(activeHitIndex: Int?, hitCount: Int, status: SearchStatus): String? {
            if (activeHitIndex == null || activeHitIndex >= hitCount) return null
            val numbers = NumberFormat.getIntegerInstance()
            val text = "${numbers.format(activeHitIndex + 1)} of ${numbers.format(hitCount)}"
            return if (status == SearchStatus.SEARCHING) "$text+" else text
        }
    }
}

/**
 * Whether the hit list can differ after this event: a run started, ended or
 * was cleared. Progress and active-hit moves leave the list alone.
 */
private val SearchEvent.changesHits: Boolean
    get() = this !is SearchEventProgressChanged && this !is SearchEventActiveHitChanged
