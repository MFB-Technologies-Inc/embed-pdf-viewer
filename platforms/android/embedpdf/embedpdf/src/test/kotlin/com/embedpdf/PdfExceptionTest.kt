package com.embedpdf

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.async
import kotlinx.coroutines.delay
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertSame
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test
import uniffi.epdf_ffi.ViewerException
import kotlin.coroutines.cancellation.CancellationException

class PdfExceptionTest {
    private val known = listOf(
        PdfException.Code.PERMISSION_DENIED to "permission-denied",
        PdfException.Code.UNSUPPORTED to "unsupported",
        PdfException.Code.NOT_FOUND to "not-found",
        PdfException.Code.NOT_READY to "not-ready",
        PdfException.Code.INVALID_INPUT to "invalid-input",
        PdfException.Code.CONFLICT to "conflict",
        PdfException.Code.INSTANCE_CLOSED to "instance-closed",
        PdfException.Code.OPERATION_CANCELLED to "operation-cancelled",
        PdfException.Code.OPERATION_FAILED to "operation-failed",
        PdfException.Code.INVALID_STATE to "invalid-state",
    )

    @Test
    fun eachKnownCodeRoundTripsItsWireString() {
        for ((code, wire) in known) {
            assertEquals(wire, code.value)
            assertEquals(code, PdfException.Code(wire))
        }
    }

    @Test
    fun anUnnamedCodeRoundTripsAsItsOwnString() {
        val code = PdfException.Code("quota-exceeded")
        assertEquals("quota-exceeded", code.value)
        assertFalse(known.map { it.first }.contains(code))
    }

    @Test
    fun aWireStringComparesWithACode() {
        val error = PdfException(PdfException.Code.NOT_FOUND, "no page")
        assertEquals(PdfException.Code("not-found"), error.code)
        assertNotEquals(PdfException.Code("not-ready"), error.code)
    }

    @Test
    fun theShortConstructorNamesItAPluginError() {
        assertEquals(
            PdfException(PdfException.Code.PERMISSION_DENIED, "no"),
            PdfException(name = "PluginError", code = PdfException.Code.PERMISSION_DENIED, detail = "no"),
        )
        assertEquals("Custom", PdfException(PdfException.Code.CONFLICT, "d", name = "Custom").name)
    }

    @Test
    fun aWhenMatchesTheCode() {
        fun outcome(code: PdfException.Code): String =
            try {
                throw PdfException(code, "d")
            } catch (e: PdfException) {
                when (e.code) {
                    PdfException.Code.NOT_FOUND -> "not found"
                    PdfException.Code.PERMISSION_DENIED -> "denied"
                    else -> "other"
                }
            }
        assertEquals(
            listOf("not found", "denied", "other"),
            listOf(
                outcome(PdfException.Code.NOT_FOUND),
                outcome(PdfException.Code.PERMISSION_DENIED),
                outcome(PdfException.Code.CONFLICT),
            ),
        )
    }

    @Test
    fun theMessageIsTheDetail() {
        assertEquals("no page p9", PdfException(PdfException.Code.NOT_FOUND, "no page p9").message)
    }

    @Test
    fun toStringLeadsWithTheCode() {
        assertEquals("not-found: no page p9", PdfException(PdfException.Code.NOT_FOUND, "no page p9").toString())
    }

    @Test
    fun aKernelErrorKeepsItsNameCodeAndDetail() {
        val error = PdfException.from(ViewerException.Kernel("EngineError", "not-found", "no page p9"))
        assertEquals(PdfException(PdfException.Code.NOT_FOUND, "no page p9", "EngineError"), error)
        assertTrue(error.cause is ViewerException.Kernel)
    }

    @Test
    fun aKernelErrorWithoutACodeIsOperationFailed() {
        val error = PdfException.from(ViewerException.Kernel("PluginError", null, "boom"))
        assertEquals(PdfException(PdfException.Code.OPERATION_FAILED, "boom", "PluginError"), error)
    }

    @Test
    fun aClosedViewerIsInstanceClosed() {
        assertEquals(
            PdfException(PdfException.Code.INSTANCE_CLOSED, "The viewer has shut down.", "Closed"),
            PdfException.from(ViewerException.Closed()),
        )
    }

    @Test
    fun aCancellationIsOperationCancelled() {
        assertEquals(
            PdfException(PdfException.Code.OPERATION_CANCELLED, "operation cancelled", "AbortError"),
            PdfException.from(CancellationException("stop")),
        )
    }

    @Test
    fun aForeignErrorIsOperationFailedUnderItsClassName() {
        class Foreign : Exception("it broke")
        assertEquals(
            PdfException(PdfException.Code.OPERATION_FAILED, "it broke", "Foreign"),
            PdfException.from(Foreign()),
        )
        val described = PdfException.from(IllegalStateException())
        assertEquals("IllegalStateException", described.name)
        assertEquals("java.lang.IllegalStateException", described.detail)
    }

    @Test
    fun aPdfExceptionIsItself() {
        val error = PdfException(PdfException.Code.CONFLICT, "d")
        assertSame(error, PdfException.from(error))
    }

    @Test
    fun bridgedRethrowsAsPdfExceptionAndLetsCancellationThrough() = runBlocking {
        try {
            bridged { throw ViewerException.Kernel("PluginError", "not-ready", "loading") }
            fail("expected a PdfException")
        } catch (e: PdfException) {
            assertEquals(PdfException.Code.NOT_READY, e.code)
        }
        val cancellation = CancellationException("stop")
        try {
            bridged { throw cancellation }
            fail("expected the cancellation")
        } catch (e: CancellationException) {
            assertSame(cancellation, e)
        }
    }

    @Test
    fun bridgedCancellableCancelsTheTokenWhenTheCoroutineIsCancelled() = runBlocking {
        val started = CompletableDeferred<Unit>()
        var sawCancel = false
        val call = async(start = CoroutineStart.UNDISPATCHED) {
            bridgedCancellable { token ->
                started.complete(Unit)
                // The call keeps running after the coroutine is cancelled, until
                // the token reports the cancel the way the kernel would.
                while (!token.isCancelled()) delay(1)
                sawCancel = true
                throw ViewerException.Kernel("AbortError", "operation-cancelled", "operation cancelled")
            }
        }
        started.await()
        call.cancel()
        try {
            call.await()
            fail("expected the cancellation")
        } catch (e: CancellationException) {
            // The caller sees its own cancellation.
        }
        assertTrue(sawCancel)
    }

    @Test
    fun bridgedCancellableLeavesACompletedCallAlone() = runBlocking {
        var cancelledDuringTheCall = true
        val value = bridgedCancellable { token ->
            cancelledDuringTheCall = token.isCancelled()
            42
        }
        assertEquals(42, value)
        assertFalse(cancelledDuringTheCall)
    }
}
