package com.embedpdf

import kotlinx.coroutines.delay
import org.junit.Assert.assertEquals
import org.junit.Assume.assumeTrue
import org.junit.Test

/** Stamps each move's arrival on the kernel thread. */
class StampingHandler : InteractionHandler {
    val arrivals = java.util.concurrent.ConcurrentHashMap<Int, Long>()

    override fun enabledFor(tool: InteractionTool): Boolean = true

    override fun onDown(sample: PointerSample): Boolean = true

    override fun onMove(sample: PointerSample) {
        arrivals[sample.viewport.x.toInt()] = System.nanoTime()
    }
}

/**
 * Opt in with `EMBEDPDF_LATENCY=1`: it takes ten seconds and its numbers mean
 * something only on an idle machine. It prints the JVM's percentiles and
 * asserts only that every move arrived; the 120 Hz frame bound is Apple's.
 */
class PointerLatencyTest {
    @Test
    fun movesReachTheirHandler() = viewerTest {
        assumeTrue(System.getenv("EMBEDPDF_LATENCY") == "1")
        val (viewer, _, interaction, page) = InteractionTest.opened()
        val handler = StampingHandler()
        interaction.registerHandler(handler, id = "stamp", priority = 1.0)

        val count = 1200
        val periodNanos = 8_333_333L
        val sent = LongArray(count)
        interaction.dispatchPointer(sample(PointerPhase.DOWN, page, x = -1.0))
        var next = System.nanoTime()
        for (i in 0 until count) {
            sent[i] = System.nanoTime()
            interaction.dispatchPointer(sample(PointerPhase.MOVE, page, x = i.toDouble()))
            next += periodNanos
            val wait = next - System.nanoTime()
            if (wait > 0) delay(wait / 1_000_000)
        }
        interaction.dispatchPointer(sample(PointerPhase.UP, page, x = count.toDouble()))
        interaction.cursor()

        val latencies = (0 until count).mapNotNull { i -> handler.arrivals[i]?.let { it - sent[i] } }.sorted()
        assertEquals(count, latencies.size)
        val p50 = latencies[count / 2] / 1_000
        val p95 = latencies[count * 95 / 100] / 1_000
        val max = latencies[count - 1] / 1_000
        println("pointer latency p50=${p50}us p95=${p95}us max=${max}us")
        viewer.destroy()
    }
}
