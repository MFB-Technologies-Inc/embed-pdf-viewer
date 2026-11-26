package com.embedpdf.compose

/**
 * Which reveal a reader owes its search bar. A hit the bar reports is one
 * request, and it waits until its reveal finishes. A view keys an effect on
 * [waiting], so the effect that runs when the view comes back, with no new
 * report, finds nothing to reveal.
 */
internal class SearchHitReveals {
    /** The newest report whose reveal has not finished, or `null` for none. */
    var waiting: Int? = null
        private set

    private var reports = 0

    /** The bar reported an active hit: its reveal now waits, in place of an earlier one still waiting. */
    fun hitReported() {
        reports += 1
        waiting = reports
    }

    /** The reveal for [request] returned. A newer report keeps waiting. */
    fun finished(request: Int) {
        if (waiting == request) waiting = null
    }
}
