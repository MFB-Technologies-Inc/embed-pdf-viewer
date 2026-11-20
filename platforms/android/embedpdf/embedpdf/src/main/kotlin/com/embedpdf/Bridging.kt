package com.embedpdf

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.Job
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import uniffi.epdf_ffi.CancelToken
import kotlin.coroutines.cancellation.CancellationException
import kotlin.coroutines.resume
import kotlin.coroutines.suspendCoroutine

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
 * the kernel thread, and the call still waits for the kernel's answer. The
 * wait is what makes cancellation observable: when a cancelled `open(url)`
 * returns, its loading tab is already closed. A caller cancelled before the
 * call cancels the token before the call starts.
 *
 * The kernel's answer decides the outcome. A value is returned even when the
 * caller was cancelled meanwhile (the cancel lost the race with the work), so
 * a document that did open is never lost to its caller; the caller's next
 * suspension then sees the cancellation. An error in a cancelled caller is the
 * caller's own `CancellationException` rather than the kernel's
 * `operation-cancelled`.
 *
 * Pass the token this hands you; a `CancelToken()` made inline is never
 * cancelled.
 */
internal suspend fun <T> bridgedCancellable(block: suspend (CancelToken) -> T): T {
    val token = CancelToken()
    try {
        val context = currentCoroutineContext()
        if (!context.isActive) token.cancel()
        // The generated call drops its Rust future when its coroutine is
        // cancelled, which would skip the kernel's answer, so it runs in a
        // coroutine of its own that nothing cancels.
        val answer = CompletableDeferred<Result<T>>()
        CoroutineScope(context.minusKey(Job)).launch(start = CoroutineStart.UNDISPATCHED) {
            answer.complete(
                try {
                    Result.success(bridged { block(token) })
                } catch (e: Throwable) {
                    Result.failure(e)
                },
            )
        }
        val result = try {
            answer.await()
        } catch (e: CancellationException) {
            token.cancel()
            // `suspendCoroutine` resumes whether or not the caller is
            // cancelled, and `await` on a completed answer returns at once.
            suspendCoroutine { continuation -> answer.invokeOnCompletion { continuation.resume(Unit) } }
            answer.await()
        }
        return result.getOrElse { error ->
            context.ensureActive()
            throw error
        }
    } finally {
        token.close()
    }
}
