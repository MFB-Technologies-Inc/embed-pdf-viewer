package com.embedpdf

import java.io.ByteArrayOutputStream
import java.io.IOException
import java.io.InputStream
import java.net.HttpURLConnection
import java.net.SocketTimeoutException
import java.net.URI
import java.net.URL
import java.util.concurrent.Executor
import java.util.concurrent.Executors
import java.util.concurrent.RejectedExecutionException
import java.util.concurrent.ScheduledExecutorService
import java.util.concurrent.ScheduledThreadPoolExecutor
import java.util.concurrent.ThreadFactory
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicReference
import uniffi.epdf_plugin_signature.SignatureTransportCompletionInterface

/**
 * The viewer's signature transport on `HttpURLConnection`: the one POST a
 * timestamp authority or a revocation route sends. Set it with
 * `viewer.setSignatureTransport(HttpSignatureTransport())`, beside the signer
 * and the trust anchors.
 *
 * One `post` is one attempt. The Rust clients keep everything around it:
 * the RFC 3161 query, the route's JSON, retries and their delay, what a
 * status means and the error texts. So every status answers `replied`.
 *
 * The transport enforces the request's limits, because the client has no
 * timer of its own:
 * - `timeoutMs` covers the whole exchange, connecting, sending and reading
 *   the reply. Past it the answer is `timedOut` and the connection is cut.
 * - `maxReplyBytes`: a 2xx reply whose `content-length` or body runs past it
 *   is `tooLarge`, and reading stops there.
 * - A reply outside 200 to 299 whose body runs past the limit, breaks off or
 *   is still arriving at the deadline is `replied` with its status and no
 *   body, so the client decides by the status, as upstream's clients do.
 * - A reply with no readable status (`getResponseCode()` is -1) is `failed`.
 *
 * The completion is answered exactly once, whichever of the reply, the
 * deadline or a failure comes first, and released after.
 *
 * `post` is called on the viewer's kernel thread and returns at once: the
 * exchange runs on a thread of its own. On Android, a cleartext `http://`
 * address also needs the app's network security config to allow it.
 */
public class HttpSignatureTransport internal constructor(
    private val executor: Executor,
    private val timer: ScheduledExecutorService,
    private val open: (URL) -> HttpURLConnection,
) : SignatureTransport {
    public constructor() : this(sharedExecutor, sharedTimer, { it.openConnection() as HttpURLConnection })

    override fun post(request: SignatureTransportRequest, completion: SignatureTransportCompletion) {
        send(request, completion) { completion.destroy() }
    }

    /**
     * [post] against any completion, so a test can count the answers.
     * [released] runs once, after the one answer.
     */
    internal fun send(
        request: SignatureTransportRequest,
        completion: SignatureTransportCompletionInterface,
        released: () -> Unit = {},
    ) {
        val exchange = Exchange(request, completion, released)
        val deadline = try {
            timer.schedule(exchange::expire, request.timeoutMs.toLong(), TimeUnit.MILLISECONDS)
        } catch (error: RejectedExecutionException) {
            exchange.settle { it.failed("the transport is shut down: $error") }
            return
        }
        try {
            executor.execute {
                exchange.run()
                deadline.cancel(false)
            }
        } catch (error: RejectedExecutionException) {
            deadline.cancel(false)
            exchange.settle { it.failed("the transport is shut down: $error") }
        }
    }

    private inner class Exchange(
        private val request: SignatureTransportRequest,
        private val completion: SignatureTransportCompletionInterface,
        private val released: () -> Unit,
    ) {
        private val answered = AtomicBoolean(false)
        private val connection = AtomicReference<HttpURLConnection?>(null)
        /** The reply's status once it is known; -1 before. */
        private val status = AtomicInteger(-1)

        /** Answer once; a later answer is dropped. */
        fun settle(answer: (SignatureTransportCompletionInterface) -> Unit): Boolean {
            if (!answered.compareAndSet(false, true)) return false
            try {
                answer(completion)
            } finally {
                released()
            }
            return true
        }

        /** The deadline: a status outside 2xx already in hand is that reply. */
        fun expire() {
            val code = status.get()
            val settled = if (code >= 0 && code !in 200..299) {
                settle { it.replied(code.toUShort(), ByteArray(0)) }
            } else {
                settle { it.timedOut() }
            }
            if (settled) connection.get()?.disconnect()
        }

        fun run() {
            if (answered.get()) return
            val outcome: (SignatureTransportCompletionInterface) -> Unit = try {
                exchange()
            } catch (error: SocketTimeoutException) {
                { it.timedOut() }
            } catch (error: Throwable) {
                val message = error.message?.let { "${error.javaClass.simpleName}: $it" } ?: error.toString();
                { it.failed(message) }
            }
            settle(outcome)
            connection.get()?.disconnect()
        }

        private fun exchange(): (SignatureTransportCompletionInterface) -> Unit {
            val c = open(URI(request.url).toURL())
            connection.set(c)
            // The deadline may have passed while the connection was made.
            if (answered.get()) {
                c.disconnect()
                return {}
            }
            val timeout = request.timeoutMs.toLong().coerceIn(1, Int.MAX_VALUE.toLong()).toInt()
            c.connectTimeout = timeout
            c.readTimeout = timeout
            c.requestMethod = "POST"
            c.doOutput = true
            c.useCaches = false
            // content-type first, then the request's headers: a later one wins.
            c.setRequestProperty("content-type", request.contentType)
            for (header in request.headers) c.setRequestProperty(header.name, header.value)
            c.outputStream.use { it.write(request.body) }

            val code = c.responseCode
            // -1: the reply has no status line a status can be read from.
            if (code < 0) return { it.failed("the reply is not HTTP") }
            status.set(code)
            val limit = request.maxReplyBytes.toLong()
            val ok = code in 200..299
            if (!ok) {
                val body = if (c.contentLengthLong > limit) null else try {
                    c.errorStream?.use { readUpTo(it, limit) } ?: ByteArray(0)
                } catch (error: IOException) {
                    null
                }
                val reply = body ?: ByteArray(0)
                return { it.replied(code.toUShort(), reply) }
            }
            if (c.contentLengthLong > limit) return { it.tooLarge() }
            val body = c.inputStream.use { readUpTo(it, limit) } ?: return { it.tooLarge() }
            return { it.replied(code.toUShort(), body) }
        }
    }

    private companion object {
        private fun daemon(name: String) = ThreadFactory { task ->
            Thread(task, name).apply { isDaemon = true }
        }

        /** Exchanges block on I/O, so each gets a thread; idle ones go after a minute. */
        val sharedExecutor: Executor = Executors.newCachedThreadPool(daemon("embedpdf-signature-transport"))

        /** One timer for every deadline; a cancelled deadline leaves the queue at once. */
        val sharedTimer: ScheduledExecutorService =
            ScheduledThreadPoolExecutor(1, daemon("embedpdf-signature-deadline")).apply {
                removeOnCancelPolicy = true
            }

        /** The body, or null once it passes [limit] bytes. */
        fun readUpTo(input: InputStream, limit: Long): ByteArray? {
            val out = ByteArrayOutputStream()
            val buffer = ByteArray(16 * 1024)
            var total = 0L
            while (true) {
                val n = input.read(buffer)
                if (n < 0) break
                total += n
                if (total > limit) return null
                out.write(buffer, 0, n)
            }
            return out.toByteArray()
        }
    }
}
