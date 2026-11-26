package com.embedpdf

import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout

/** The fixture PDFs the build copies into the test APK's assets, under `fixtures/`. */
object DeviceFixture {
    fun bytes(name: String): ByteArray =
        InstrumentationRegistry.getInstrumentation().context.assets.open("fixtures/$name.pdf").use { it.readBytes() }

    /** The DER inside the PEM fixture [name]. */
    fun pemBody(name: String): ByteArray {
        val text = InstrumentationRegistry.getInstrumentation().context.assets.open("fixtures/$name.pem").use { it.readBytes() }.decodeToString()
        val body = text.lines().filterNot { it.startsWith("-----") }.joinToString("")
        return android.util.Base64.decode(body, android.util.Base64.DEFAULT)
    }
}

/**
 * Runs [body] off the main thread and waits for it, failing after
 * [timeoutMs]. Instrumented tests run on the instrumentation thread, so
 * blocking it leaves the main looper free for the views and the sandbox.
 */
fun <T> onDevice(timeoutMs: Long = 60_000, body: suspend CoroutineScope.() -> T): T =
    runBlocking(Dispatchers.IO) { withTimeout(timeoutMs) { body() } }

/** Expects [body] to throw a [PdfException] with [code], and returns it. */
suspend fun expectPdfCode(code: PdfException.Code, body: suspend () -> Unit): PdfException {
    try {
        body()
    } catch (e: PdfException) {
        if (e.code != code) throw AssertionError("expected $code, got $e", e)
        return e
    }
    throw AssertionError("expected $code")
}
