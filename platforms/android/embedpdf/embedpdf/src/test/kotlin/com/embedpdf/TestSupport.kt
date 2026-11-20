package com.embedpdf

import kotlinx.coroutines.CoroutineScope
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

/** Stands in for a transport in tests that never open a URL. */
object UnusedFetcher : DocumentFetcher {
    override fun fetch(url: String, completion: DocumentFetchCompletion): DocumentFetchCancellation {
        completion.failWith(HostFailure(name = "UnusedFetcher", code = "unsupported", detail = "no transport"))
        return object : DocumentFetchCancellation {
            override fun cancel() {}
        }
    }
}

fun viewer(config: ViewerConfig = ViewerConfig()): PdfViewer = PdfViewer(config, UnusedFetcher)

suspend fun started(config: ViewerConfig = ViewerConfig()): PdfViewer = viewer(config).also { it.start() }

/** Expects [body] to throw a [PdfException] with [code], and returns it. */
suspend fun expectCode(code: PdfException.Code, body: suspend () -> Unit): PdfException {
    try {
        body()
    } catch (e: PdfException) {
        assertEquals(e.toString(), code, e.code)
        return e
    }
    fail("expected $code")
    throw AssertionError()
}

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
