package com.embedpdf.models

import kotlinx.coroutines.Job
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * Owns one page's render work, including superseded work still unwinding.
 * The work runs as a child of the caller's coroutine, so cancelling the
 * caller cancels it, and a later [run] cancels it without touching the
 * caller. Used on the main thread only.
 */
internal class PageRenderTask {
    private var job: Job? = null
    private var generation = 0

    /** Whether no work owns the page. */
    val isIdle: Boolean get() = job == null

    /** Replaces the current work, waiting for it to finish before [operation] starts. */
    suspend fun run(operation: suspend () -> Unit) {
        val current = ++generation
        val previous = job
        previous?.cancel()
        try {
            coroutineScope {
                val next = launch {
                    // A superseded run still waits for the work before it,
                    // so the work after it never overlaps that work.
                    withContext(NonCancellable) { previous?.join() }
                    if (isActive) operation()
                }
                job = next
                next.join()
            }
        } finally {
            if (generation == current) job = null
        }
    }

    /**
     * Runs [operation] as the page's work only when no other work owns the
     * page, and returns at once otherwise. Swift starts it as a task of its
     * own; here the caller's coroutine carries it.
     */
    suspend fun runIfIdle(operation: suspend () -> Unit) {
        if (job != null) return
        run(operation)
    }
}
