package com.embedpdf.models

import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.isActive
import kotlin.coroutines.cancellation.CancellationException

/**
 * Orders a model's snapshot reads: a read applies only when no read started
 * after it and nothing invalidated it while it was out, so a refresh that
 * started earlier never overwrites one that started later. Used on the main
 * thread only.
 */
internal class SnapshotRefresh {
    private var version = 0L

    /**
     * Runs after a read finishes and before its result is applied or dropped.
     * Tests hold a read here so a newer one can overtake it.
     */
    var readFinished: (suspend () -> Unit)? = null

    fun invalidate() {
        version++
    }

    /**
     * Reads with [load] and hands the value to [apply], or the failure to
     * [failure], unless a later read or [invalidate] has superseded it or the
     * caller was cancelled. A cancelled caller sees its cancellation.
     */
    suspend fun <T> run(load: suspend () -> T, apply: (T) -> Unit, failure: (Throwable) -> Unit) {
        val request = ++version
        val result = try {
            Result.success(load())
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            Result.failure(e)
        }
        readFinished?.invoke()
        if (request != version || !currentCoroutineContext().isActive) return
        result.fold(apply, failure)
    }
}
