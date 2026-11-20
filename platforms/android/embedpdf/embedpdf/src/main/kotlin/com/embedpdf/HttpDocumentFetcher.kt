package com.embedpdf

import java.io.IOException
import java.io.InputStream
import java.net.HttpURLConnection
import java.net.URI
import java.net.URLConnection
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicReference

/**
 * The default transport for URL document sources, on [HttpURLConnection]. The
 * kernel owns loading tabs, HTTP refusals, PDF validation and retry; this owns
 * the request. Every status answers with its body, so a 404 reaches the kernel
 * as a 404 and leaves a failed tab [PdfViewer.retry] can reopen.
 *
 * Each request runs on a background thread. Cancelling it disconnects the
 * connection, and the open fails `operation-cancelled`. A `file:` URL reads
 * the file.
 *
 * @param configure runs on each connection before it connects: headers,
 *   cookies, authentication, timeouts.
 */
public class HttpDocumentFetcher(
    private val configure: (URLConnection) -> Unit = {},
) : DocumentFetcher {
    override fun fetch(url: String, completion: DocumentFetchCompletion): DocumentFetchCancellation {
        val target = try {
            URI(url).toURL()
        } catch (e: Exception) {
            completion.failWith(HostFailure(name = "InvalidURL", code = "invalid-input", detail = "The document URL is invalid."))
            return EmptyCancellation
        }
        val request = Request()
        fetchThreads.execute {
            try {
                val connection = target.openConnection()
                request.connection.set(connection)
                if (request.cancelled.get()) throw IOException("cancelled before connecting")
                configure(connection)
                val status = (connection as? HttpURLConnection)?.responseCode ?: 200
                if (status !in 0..UShort.MAX_VALUE.toInt()) {
                    completion.failWith(
                        HostFailure(name = "InvalidResponse", code = "operation-failed", detail = "The response has an invalid HTTP status."),
                    )
                    return@execute
                }
                val body = body(connection, status)
                completion.finish(status.toUShort(), body)
            } catch (e: Exception) {
                completion.failWith(
                    if (request.cancelled.get()) {
                        HostFailure(name = "AbortError", code = "operation-cancelled", detail = "operation cancelled")
                    } else {
                        HostFailure(name = e.javaClass.simpleName, code = null, detail = readable(e))
                    },
                )
            } finally {
                (request.connection.get() as? HttpURLConnection)?.disconnect()
            }
        }
        return request
    }

    private fun body(connection: URLConnection, status: Int): ByteArray {
        // A status outside 2xx reads its body from the error stream, which is
        // null when there is none.
        val stream: InputStream? = if (connection is HttpURLConnection && status >= 400) {
            connection.errorStream
        } else {
            connection.getInputStream()
        }
        return stream?.use { it.readBytes() } ?: ByteArray(0)
    }

    private class Request : DocumentFetchCancellation {
        val cancelled = AtomicBoolean(false)
        val connection = AtomicReference<URLConnection?>(null)

        override fun cancel() {
            cancelled.set(true)
            (connection.get() as? HttpURLConnection)?.disconnect()
        }
    }
}

private val fetchThreadCount = AtomicInteger()

/** The threads every fetcher's requests run on; idle threads end after a minute. */
internal val fetchThreads: ExecutorService = Executors.newCachedThreadPool { runnable ->
    Thread(runnable, "embedpdf-fetch-${fetchThreadCount.incrementAndGet()}").apply { isDaemon = true }
}

internal object EmptyCancellation : DocumentFetchCancellation {
    override fun cancel() {}
}
