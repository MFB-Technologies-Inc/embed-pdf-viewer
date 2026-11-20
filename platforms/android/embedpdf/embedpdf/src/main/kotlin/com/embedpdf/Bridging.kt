package com.embedpdf

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
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
 * the kernel thread, and a cancelled caller still waits for the kernel's
 * answer before it sees its cancellation. The wait is what makes cancellation
 * observable: when a cancelled `open(url)` throws, its loading tab is
 * normally already closed. One window stays open: a caller cancelled after
 * the discard check below passed, but before it resumes, gets its
 * cancellation at its next suspension and the value is dropped without
 * [onDiscard]. A caller cancelled before the call cancels the token before
 * the call starts.
 *
 * A cancelled caller never gets a value: Kotlin would drop it at the caller's
 * next suspension anyway (`withTimeout` and `async` do). So a call that
 * changes something and has no [onDiscard] can land its change while its
 * caller sees cancellation; each such call says so in its KDoc, and the
 * README's Cancellation section says why this differs from Swift. When the kernel
 * answers with a value after the caller was cancelled (the cancel lost the
 * race with the work), [onDiscard] runs on that value, without cancellation,
 * so a call that made something (an open that made a tab) can undo it; then
 * the caller's `CancellationException` is thrown. An error in a cancelled
 * caller is also the caller's own `CancellationException` rather than the
 * kernel's `operation-cancelled`.
 *
 * Pass the token this hands you; a `CancelToken()` made inline is never
 * cancelled.
 */
internal suspend fun <T> bridgedCancellable(
    onDiscard: (suspend (T) -> Unit)? = null,
    block: suspend (CancelToken) -> T,
): T {
    val token = CancelToken()
    try {
        val context = currentCoroutineContext()
        if (!context.isActive) token.cancel()
        // The generated call drops its Rust future when its coroutine is
        // cancelled, which would skip the kernel's answer, so it runs in a
        // coroutine of its own that nothing cancels. Unconfined runs it on
        // the caller's thread up to its first suspension and then on
        // whichever thread the kernel's answer arrives on, never waiting for
        // the caller's dispatcher (a blocked main thread, say).
        val answer = CompletableDeferred<Result<T>>()
        CoroutineScope(Dispatchers.Unconfined).launch {
            answer.complete(
                try {
                    Result.success(bridged { block(token) })
                } catch (e: CancellationException) {
                    // Nothing cancels this coroutine, so a cancellation here
                    // is the call's own: report it as the kernel would.
                    Result.failure(PdfException(PdfException.Code.OPERATION_CANCELLED, "operation cancelled", "AbortError", e))
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
        val value = result.getOrElse { error ->
            context.ensureActive()
            throw error
        }
        if (!context.isActive) {
            if (onDiscard != null) {
                withContext(NonCancellable) {
                    try {
                        onDiscard(value)
                    } catch (e: Exception) {
                        // Best effort: the caller is gone and sees its cancellation.
                    }
                }
            }
            context.ensureActive()
        }
        return value
    } finally {
        token.close()
    }
}
