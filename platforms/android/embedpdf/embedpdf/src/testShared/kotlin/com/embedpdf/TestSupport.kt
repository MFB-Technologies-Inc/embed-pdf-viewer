package com.embedpdf

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.withTimeoutOrNull
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import kotlin.coroutines.cancellation.CancellationException
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.cancelChildren
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.channels.ReceiveChannel
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeout
import org.junit.Assert.assertEquals
import org.junit.Assert.fail
import java.io.File

/** The fixture PDFs the Rust and Swift tests read, found by name across the directories Gradle passes. */
object Fixture {
    private val dirs: List<File> =
        System.getProperty("embedpdf.fixtureDirs").split(File.pathSeparator).map(::File)

    fun file(name: String, ext: String = "pdf"): File =
        dirs.map { it.resolve("$name.$ext") }.firstOrNull { it.isFile }
            ?: error("no fixture $name.$ext in $dirs")

    fun bytes(name: String, ext: String = "pdf"): ByteArray = file(name, ext).readBytes()
}

fun viewer(config: ViewerConfig = ViewerConfig()): PdfViewer = PdfViewer(config)

suspend fun started(config: ViewerConfig = ViewerConfig()): PdfViewer = viewer(config).also { it.start() }

/** Expects [body] to throw a [PdfException] with [code], and returns it. */
suspend fun expectCode(code: PdfException.Code, body: suspend () -> Unit): PdfException = expectCode(code, "", body)

/** [expectCode] with [label] in front of a failure's message. */
suspend fun expectCode(code: PdfException.Code, label: String, body: suspend () -> Unit): PdfException {
    try {
        body()
    } catch (e: PdfException) {
        assertEquals("$label $e", code, e.code)
        return e
    }
    fail("$label expected $code")
    throw AssertionError()
}

/**
 * Calls [body] in a coroutine cancelled before the call, and expects the call
 * to end in the coroutine's cancellation without finishing: the token was
 * cancelled before the kernel started the work. A cancel that arrives while
 * the work runs is timing, so these tests don't try it; the Rust tests hold
 * an engine reply to pin that case.
 */
suspend fun expectCancelledBeforeItStarts(body: suspend () -> Any?) {
    var finished = false
    var thrown: Throwable? = null
    coroutineScope {
        launch(start = CoroutineStart.UNDISPATCHED) {
            coroutineContext[Job]!!.cancel()
            try {
                body()
                finished = true
            } catch (e: Throwable) {
                thrown = e
                throw e
            }
        }.join()
    }
    assertFalse("a call made in a cancelled coroutine finished", finished)
    assertTrue("$thrown", thrown is CancellationException && thrown !is PdfException)
}

/** Whether collecting [flow] completes within two seconds. */
suspend fun <T> completes(flow: Flow<T>): Boolean = withTimeoutOrNull(2_000) { flow.collect {} } != null

/**
 * Collects [flow] from a coroutine subscribed by the time this returns, for
 * [completes] to wait on later.
 */
fun <T> CoroutineScope.collecting(flow: Flow<T>): Job = launch(start = CoroutineStart.UNDISPATCHED) { flow.collect {} }

/** Whether [job] completes within two seconds. */
suspend fun finishes(job: Job): Boolean = withTimeoutOrNull(2_000) { job.join() } != null

/** The first value of [flow] that [select] maps to non-null, within five seconds. */
suspend fun <T, R : Any> Flow<T>.firstMapped(select: (T) -> R?): R =
    withTimeout(5_000) {
        var found: R? = null
        first { value -> select(value)?.also { found = it } != null }
        found!!
    }

/**
 * Collects [flow] into a channel from a coroutine that is subscribed by the
 * time this returns, the way Swift's `observe*()` streams are installed before
 * the call returns. The channel closes when the flow completes.
 */
fun <T> CoroutineScope.subscribed(flow: Flow<T>): ReceiveChannel<T> {
    val channel = Channel<T>(Channel.UNLIMITED)
    launch(start = CoroutineStart.UNDISPATCHED) {
        try {
            flow.collect { channel.send(it) }
        } finally {
            channel.close()
        }
    }
    return channel
}

/** [runBlocking] for a viewer test: cancels leftover collectors when [body] ends. */
fun viewerTest(body: suspend CoroutineScope.() -> Unit): Unit = runBlocking {
    try {
        body()
    } finally {
        coroutineContext.cancelChildren()
    }
}

/** The next value [channel] gives that [select] maps to non-null, within five seconds. */
suspend fun <T, R : Any> ReceiveChannel<T>.nextMapped(select: (T) -> R?): R =
    withTimeout(5_000) {
        var found: R? = null
        while (found == null) found = select(receive())
        found
    }

/** Every value [this] gives until it closes. */
suspend fun <T> ReceiveChannel<T>.drain(): List<T> = withTimeout(5_000) { buildList { for (value in this@drain) add(value) } }
