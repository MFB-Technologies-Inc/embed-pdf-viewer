package com.embedpdf

import androidx.tracing.Trace
import java.util.concurrent.atomic.AtomicInteger

// Sections for the calls worth timing, visible in a system trace or the
// profiler without extra setup. The calls suspend and may resume on another
// thread, so each is an async section with its own cookie.

private val cookies = AtomicInteger()

internal suspend fun <T> measured(name: String, block: suspend () -> T): T {
    val section = "EmbedPDF.$name"
    val cookie = cookies.incrementAndGet()
    Trace.beginAsyncSection(section, cookie)
    try {
        return block()
    } finally {
        Trace.endAsyncSection(section, cookie)
    }
}
