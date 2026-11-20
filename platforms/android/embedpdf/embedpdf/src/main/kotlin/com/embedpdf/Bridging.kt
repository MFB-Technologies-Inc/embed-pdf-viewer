package com.embedpdf

import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.awaitCancellation
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import uniffi.epdf_ffi.CancelToken
import kotlin.coroutines.cancellation.CancellationException

/**
 * Runs a generated call and rethrows whatever it throws as a [PdfException].
 * A [CancellationException] passes through untouched, so structured
 * concurrency sees the cancellation it caused.
 */
internal suspend fun <T> bridged(block: suspend () -> T): T =
    try {
        block()
    } catch (e: CancellationException) {
        throw e
    } catch (e: Exception) {
        throw PdfException.from(e)
    }

/**
 * [bridged] for a call that takes a [CancelToken].
 *
 * Cancelling the calling coroutine cancels the token, which aborts the call on
 * the kernel thread, and then waits for the kernel to answer before the
 * cancellation propagates. The wait is what makes cancellation observable:
 * when a cancelled `open(url)` returns, its loading tab is already closed.
 * Pass the token this hands you; a `CancelToken()` made inline is never
 * cancelled.
 */
internal suspend fun <T> bridgedCancellable(block: suspend (CancelToken) -> T): T {
    val token = CancelToken()
    try {
        return coroutineScope {
            val watcher = launch(start = CoroutineStart.UNDISPATCHED) {
                try {
                    awaitCancellation()
                } finally {
                    token.cancel()
                }
            }
            try {
                // The generated call drops its Rust future when its coroutine is
                // cancelled, which would skip the kernel's answer; this one runs
                // to its answer and the token does the cancelling.
                withContext(NonCancellable) { bridged { block(token) } }
            } catch (e: PdfException) {
                // A cancelled caller sees its cancellation, not the kernel's
                // `operation-cancelled`.
                currentCoroutineContext().ensureActive()
                throw e
            } finally {
                watcher.cancel()
            }
        }
    } finally {
        token.close()
    }
}
