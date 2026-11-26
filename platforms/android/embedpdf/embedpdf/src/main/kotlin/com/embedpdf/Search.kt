package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_search.SearchEventListener
import uniffi.epdf_plugin_search.SearchHandle

public typealias SearchSettings = uniffi.epdf_plugin_search.SearchSettings
public typealias SearchSettingsPatch = uniffi.epdf_plugin_search.SearchSettingsPatch
public typealias SearchHighlightSettings = uniffi.epdf_plugin_search.SearchHighlightSettings
public typealias SearchHighlightPatch = uniffi.epdf_plugin_search.SearchHighlightPatch
public typealias SearchRevealSettings = uniffi.epdf_plugin_search.SearchRevealSettings
public typealias SearchRevealPatch = uniffi.epdf_plugin_search.SearchRevealPatch
public typealias SearchBlendMode = uniffi.epdf_plugin_search.SearchBlendMode

public typealias SearchQuery = uniffi.epdf_plugin_search.SearchQuery
public typealias SearchHit = uniffi.epdf_plugin_search.SearchHit
public typealias TextSegment = uniffi.epdf_plugin_search.TextSegment
public typealias SearchSnippet = uniffi.epdf_plugin_search.SearchSnippet
public typealias SearchStatus = uniffi.epdf_plugin_search.SearchStatus
public typealias SearchProgress = uniffi.epdf_plugin_search.SearchProgress
public typealias SearchResult = uniffi.epdf_plugin_search.SearchResult
public typealias SearchResultStatus = uniffi.epdf_plugin_search.SearchResultStatus
public typealias SearchRevealOptions = uniffi.epdf_plugin_search.SearchRevealOptions
public typealias SearchRevealAnchor = uniffi.epdf_plugin_search.SearchRevealAnchor
public typealias SearchRevealAxis = uniffi.epdf_plugin_search.SearchRevealAxis
public typealias SearchRevealAxisKeep = uniffi.epdf_plugin_search.SearchRevealAxis.Keep
public typealias SearchRevealAxisStart = uniffi.epdf_plugin_search.SearchRevealAxis.Start
public typealias SearchRevealAxisCenter = uniffi.epdf_plugin_search.SearchRevealAxis.Center
public typealias SearchRevealAxisEnd = uniffi.epdf_plugin_search.SearchRevealAxis.End
public typealias SearchRevealAxisFraction = uniffi.epdf_plugin_search.SearchRevealAxis.Fraction
public typealias SearchScrollBehavior = uniffi.epdf_plugin_search.SearchScrollBehavior
public typealias SearchConfig = uniffi.epdf_plugin_search.SearchConfig
public typealias SearchEvent = uniffi.epdf_plugin_search.SearchEvent
public typealias SearchEventStarted = uniffi.epdf_plugin_search.SearchEvent.Started
public typealias SearchEventProgressChanged = uniffi.epdf_plugin_search.SearchEvent.ProgressChanged
public typealias SearchEventCompleted = uniffi.epdf_plugin_search.SearchEvent.Completed
public typealias SearchEventCancelled = uniffi.epdf_plugin_search.SearchEvent.Cancelled
public typealias SearchEventFailed = uniffi.epdf_plugin_search.SearchEvent.Failed
public typealias SearchEventActiveHitChanged = uniffi.epdf_plugin_search.SearchEvent.ActiveHitChanged
public typealias SearchEventCleared = uniffi.epdf_plugin_search.SearchEvent.Cleared
public typealias SearchStartedEvent = uniffi.epdf_plugin_search.SearchStartedEvent
public typealias SearchProgressChangedEvent = uniffi.epdf_plugin_search.SearchProgressChangedEvent
public typealias SearchCompletedEvent = uniffi.epdf_plugin_search.SearchCompletedEvent
public typealias SearchCancelledEvent = uniffi.epdf_plugin_search.SearchCancelledEvent
public typealias SearchCancelReason = uniffi.epdf_plugin_search.SearchCancelReason
public typealias SearchFailedEvent = uniffi.epdf_plugin_search.SearchFailedEvent
public typealias SearchActiveHitChangedEvent = uniffi.epdf_plugin_search.SearchActiveHitChangedEvent

/**
 * A document's text search: one session ([search], navigated with [nextHit],
 * [previousHit] and [goToHit]) and a session-free [findAll]. Get one with
 * [PdfViewer.search]; the viewer needs `ViewerConfig(search = SearchConfig())`.
 * It keeps the viewer alive. After its document closes, every call throws
 * `instance-closed`.
 */
public class SearchCapability internal constructor(
    private val handle: SearchHandle,
    /**
     * The viewer this capability belongs to. A model that follows the
     * capability reads the viewer's state revisions through it.
     */
    public val viewer: PdfViewer,
) {
    /** Current settings shared by every document using this plugin registration. */
    public suspend fun settings(): SearchSettings = viewer.call { handle.settings() }

    /** Merges the supplied fields. Clear flags remove nullable values; omission preserves them. */
    public suspend fun updateSettings(patch: SearchSettingsPatch) {
        viewer.call { handle.updateSettings(patch) }
    }

    /** Restores the values supplied at registration, preserving registered host resources. */
    public suspend fun resetSettings() {
        viewer.call { handle.resetSettings() }
    }

    /**
     * Whether a search would be served now: `doc.text.search`, and with
     * `snippets = true` also `doc.text.copy`. Leaving [snippets] out is
     * `false`, as in the engine (`SearchCapability::can_search` in
     * `crates/plugins/search/src/controller.rs` needs `doc.text.copy` only for
     * `Some(true)`).
     */
    public suspend fun canSearch(snippets: Boolean = false): Boolean = viewer.call { handle.canSearch(snippets) }

    /**
     * Starts a new search, superseding a running one. Returns when the scan
     * completes, is superseded or is cancelled (cancelling the calling
     * coroutine cancels the scan and keeps the hits found so far). An empty
     * `query.text` clears the session. [start] is the page the scan starts on; `null`
     * is the stage's current page when a stage is installed.
     *
     * A search that started replaces the session even when the caller is cancelled, and the caller
     * then sees its cancellation rather than the result (see the README's Cancellation section).
     */
    public suspend fun search(query: SearchQuery, start: PageRef? = null): SearchResult = measured("search") {
        viewer.callCancellable { token -> handle.search(query, start, token) }
    }

    /** Re-runs the current query from scratch. */
    public suspend fun refresh() {
        viewer.callCancellable { token -> handle.refresh(token) }
    }

    /** Stops the scan and keeps the hits found so far. */
    public suspend fun cancel() {
        viewer.call { handle.cancel() }
    }

    /** Stops the scan and drops the session. */
    public suspend fun clear() {
        viewer.call { handle.clear() }
    }

    public suspend fun nextHit(reveal: SearchRevealOptions? = null): SearchHit? = viewer.call { handle.nextHit(reveal) }

    public suspend fun previousHit(reveal: SearchRevealOptions? = null): SearchHit? =
        viewer.call { handle.previousHit(reveal) }

    /**
     * Wraps modulo the hit count. An index below zero throws `invalid-input`.
     */
    public suspend fun goToHit(index: Int, reveal: SearchRevealOptions? = null): SearchHit? {
        val wire = index.toWireUInt("hit index")
        return viewer.call { handle.goToHit(wire, reveal) }
    }

    public suspend fun revealActiveHit(reveal: SearchRevealOptions? = null) {
        viewer.call { handle.revealActiveHit(reveal) }
    }

    public suspend fun query(): SearchQuery? = viewer.call { handle.query() }

    public suspend fun status(): SearchStatus = viewer.call { handle.status() }

    /** Hits found so far, or those on one page. */
    public suspend fun hits(page: PageRef? = null): List<SearchHit> = measured("hits") {
        viewer.call { handle.hits(page) }
    }

    public suspend fun hitCount(page: PageRef? = null): Int = viewer.call { handle.hitCount(page) }.toIntResult("hit count")

    public suspend fun pagesWithHits(): List<PageRef> = viewer.call { handle.pagesWithHits() }

    /** `null` when no hit is active. */
    public suspend fun activeHitIndex(): Int? = viewer.call { handle.activeHitIndex() }?.toIntResult("active hit index")

    public suspend fun activeHit(): SearchHit? = viewer.call { handle.activeHit() }

    public suspend fun progress(): SearchProgress = viewer.call { handle.progress() }

    /** The failure that ended the last search, if it failed. */
    public suspend fun error(): PluginErrorInfo? = viewer.call { handle.error() }

    /**
     * Every hit, in page order, touching no session state. With
     * [ContentInclusion.WHEN_PERMITTED] the hits carry snippets when the
     * session may copy text (`doc.text.copy`), and none when it may not or the
     * engine still denies them; [ContentInclusion.ALWAYS] throws
     * `permission-denied` without `doc.text.copy`. Cancelling the calling
     * coroutine cancels the scan.
     */
    public suspend fun findAll(
        query: SearchQuery,
        snippets: ContentInclusion = ContentInclusion.WHEN_PERMITTED,
    ): List<SearchHit> = viewer.callCancellable { token -> handle.findAll(query, snippets.engineValue, token) }

    /**
     * Session events. Completes when the document closes (at once if it
     * already has) or the viewer is destroyed. Each collection is a new
     * subscription.
     */
    public fun observeEvents(): Flow<SearchEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : SearchEventListener {
                override fun onEvent(event: SearchEvent) {
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
 * The search capability for [document]. Throws `unsupported` when the viewer
 * was built without the search plugin, whatever the document's state.
 */
public suspend fun PdfViewer.search(document: DocumentInfo): SearchCapability =
    SearchCapability(call { host.search(document.id) }, this)
