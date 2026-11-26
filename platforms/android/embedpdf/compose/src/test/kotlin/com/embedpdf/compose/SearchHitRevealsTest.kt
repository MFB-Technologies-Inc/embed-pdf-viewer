package com.embedpdf.compose

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Test

/** `SearchHitRevealsTests`. */
class SearchHitRevealsTest {
    @Test
    fun nothingWaitsBeforeAReport() {
        assertNull(SearchHitReveals().waiting)
    }

    @Test
    fun aReportedHitWaitsUntilItsRevealFinishes() {
        val reveals = SearchHitReveals()
        reveals.hitReported()
        reveals.finished(checkNotNull(reveals.waiting))
        assertNull(reveals.waiting)
    }

    /** A view that comes back runs its effect again with whatever waits, and reports no hit: nothing is revealed. */
    @Test
    fun nothingWaitsWhenTheViewReappearsAfterAReveal() {
        val reveals = SearchHitReveals()
        reveals.hitReported()
        reveals.finished(checkNotNull(reveals.waiting))
        assertNull(reveals.waiting)
    }

    @Test
    fun eachReportIsItsOwnRequest() {
        val reveals = SearchHitReveals()
        reveals.hitReported()
        val first = checkNotNull(reveals.waiting)
        reveals.hitReported()
        val second = checkNotNull(reveals.waiting)
        assertNotEquals(first, second)
        reveals.finished(second)
        reveals.hitReported()
        assertNotNull(reveals.waiting)
        assertNotEquals(second, reveals.waiting)
    }

    @Test
    fun anOlderRevealFinishingLeavesTheNewerWaiting() {
        val reveals = SearchHitReveals()
        reveals.hitReported()
        val first = checkNotNull(reveals.waiting)
        reveals.hitReported()
        val second = checkNotNull(reveals.waiting)
        reveals.finished(first)
        assertEquals(second, reveals.waiting)
    }
}
