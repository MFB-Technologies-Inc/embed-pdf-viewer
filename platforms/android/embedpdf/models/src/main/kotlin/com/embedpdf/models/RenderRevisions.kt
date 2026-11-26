package com.embedpdf.models

import com.embedpdf.PageRef
import com.embedpdf.PdfException
import com.embedpdf.RenderCapability
import com.embedpdf.RenderEventInvalidated
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update

/**
 * A per-page counter that rises each time the engine invalidates a page's
 * render. Views key their page images on it, so an edit redraws only the
 * pages it touched.
 *
 * Swift's model counts from `init` with a task of its own. A model here owns
 * no scope, so it counts while [run] runs: from the moment [run] subscribes
 * until the document closes or the calling coroutine is cancelled. Start
 * [run] before the first edit that should count.
 */
public class RenderRevisions(private val render: RenderCapability) {
    // Performance: one map holds every page's revision, so one page's
    // invalidation notifies every collector of [revisions], each page's
    // view included. It bites on an edit while many pages or thumbnails are
    // on screen. A flow per page limits an invalidation to its own page.
    private val _revisions = MutableStateFlow<Map<PageRef, Long>>(emptyMap())

    /** Each invalidated page's revision; a page never invalidated is absent. */
    public val revisions: StateFlow<Map<PageRef, Long>> = _revisions.asStateFlow()

    /** `0` for a page never invalidated. */
    public fun revision(page: PageRef): Long = _revisions.value[page] ?: 0

    /** Counts invalidations until the document closes or the calling coroutine is cancelled. */
    public suspend fun run(): Unit = onMain {
        try {
            render.observeEvents().collect { event ->
                if (event is RenderEventInvalidated) {
                    _revisions.update { current ->
                        current.toMutableMap().apply { for (page in event.pages) this[page] = (this[page] ?: 0) + 1 }
                    }
                }
            }
        } catch (e: PdfException) {
            // The document had already closed: nothing more to count.
        }
    }
}
