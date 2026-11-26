package com.embedpdf

import org.junit.Assert.assertEquals
import org.junit.Test

class RecordingFeedback : PlatformFeedback {
    private val log = mutableListOf<String>()

    val calls: List<String> get() = synchronized(log) { log.toList() }

    override fun selection() {
        synchronized(log) { log.add("selection") }
    }

    override fun impact(weight: FeedbackImpactWeight?) {
        synchronized(log) { log.add("impact $weight") }
    }

    override fun notify(kind: FeedbackNotifyKind) {
        synchronized(log) { log.add("notify $kind") }
    }
}

class FeedbackTest {
    @Test
    fun feedbackReachesTheKotlinProvider() = viewerTest {
        val provider = RecordingFeedback()
        val viewer = started(ViewerConfig(feedback = FeedbackConfig(provider)))
        val feedback = viewer.feedback()
        feedback.selection()
        feedback.impact(FeedbackImpactWeight.HEAVY)
        feedback.impact()
        feedback.notify(FeedbackNotifyKind.SUCCESS)
        assertEquals(listOf("selection", "impact HEAVY", "impact null", "notify SUCCESS"), provider.calls)
        viewer.destroy()
    }

    // Feedback has no event stream of its own (its effects reach the
    // provider), so it has no `droppingTheFlowUnsubscribes`.

    @Test
    fun aDestroyedViewersCapabilityThrows() = viewerTest {
        val viewer = started(ViewerConfig(feedback = FeedbackConfig(RecordingFeedback())))
        val feedback = viewer.feedback()
        viewer.destroy()
        expectCode(PdfException.Code.INSTANCE_CLOSED) { feedback.selection() }
    }

    @Test
    fun withoutAProviderFeedbackIsNotInstalled() = viewerTest {
        val viewer = started(ViewerConfig())
        expectCode(PdfException.Code.UNSUPPORTED) { viewer.feedback() }
        viewer.destroy()
    }
}
