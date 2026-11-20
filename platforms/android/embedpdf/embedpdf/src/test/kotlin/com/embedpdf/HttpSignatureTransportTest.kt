package com.embedpdf

import com.sun.net.httpserver.HttpExchange
import com.sun.net.httpserver.HttpServer
import org.junit.After
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import uniffi.epdf_plugin_signature.SignatureTransportCompletionInterface
import java.io.BufferedReader
import java.io.InputStreamReader
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.util.Collections
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger

/**
 * `SignatureTransportTests`' transport cases (and what `tests/SignatureTest.kt`
 * checked before the transport moved here): requests go straight to a
 * loopback `HttpServer`, and the answers are counted.
 */
class HttpSignatureTransportTest {
    /** A completion that keeps every answer it is given. */
    class Answers : SignatureTransportCompletionInterface {
        val got: MutableList<String> = Collections.synchronizedList(mutableListOf())
        val bodies: MutableList<ByteArray> = Collections.synchronizedList(mutableListOf())
        val released = AtomicInteger(0)
        private val first = CountDownLatch(1)

        override fun replied(status: UShort, body: ByteArray) {
            bodies.add(body)
            answer("replied $status ${body.size}")
        }

        override fun timedOut() = answer("timedOut")

        override fun tooLarge() = answer("tooLarge")

        override fun failed(message: String) = answer("failed $message")

        private fun answer(what: String) {
            got.add(what)
            first.countDown()
        }

        fun await(): String {
            check(first.await(10, TimeUnit.SECONDS)) { "no answer within 10 s" }
            return got[0]
        }
    }

    class Recorded(val path: String, val method: String, val headers: Map<String, List<String>>, val body: ByteArray)

    private val seen: MutableList<Recorded> = Collections.synchronizedList(mutableListOf())

    /** The held routes count down here once their reply has finished arriving. */
    private val heldDone = CountDownLatch(3)

    private val server: HttpServer = HttpServer.create(InetSocketAddress("127.0.0.1", 0), 0).apply {
        executor = Executors.newCachedThreadPool()
        route("/echo") { x ->
            val status = x.requestURI.query?.removePrefix("status=")?.toInt() ?: 200
            val reply = "ok:${seen.last().body.size}".toByteArray()
            x.sendResponseHeaders(status, reply.size.toLong())
            x.responseBody.write(reply)
        }
        route("/hold") { x ->
            Thread.sleep(1500)
            try {
                x.sendResponseHeaders(200, 2)
                x.responseBody.write(byteArrayOf(1, 2))
            } finally {
                heldDone.countDown()
            }
        }
        route("/stall-body") { x -> stall(x, 200) }
        route("/error-stall") { x -> stall(x, 429) }
        route("/long-length") { x ->
            x.sendResponseHeaders(200, 2000)
            x.responseBody.write(bytes(2000))
        }
        route("/long-stream") { x ->
            x.sendResponseHeaders(200, 0)
            repeat(4) {
                x.responseBody.write(bytes(500))
                x.responseBody.flush()
            }
        }
        route("/exact") { x ->
            x.sendResponseHeaders(200, 0)
            x.responseBody.write(bytes(1000))
        }
        route("/error-long") { x ->
            x.sendResponseHeaders(503, 0)
            x.responseBody.write(bytes(2000))
        }
        route("/error-body") { x ->
            val body = "nope".toByteArray()
            x.sendResponseHeaders(404, body.size.toLong())
            x.responseBody.write(body)
        }
        start()
    }

    private val base = "http://127.0.0.1:${server.address.port}"

    private val transport = HttpSignatureTransport()

    @After
    fun stop() {
        server.stop(0)
    }

    /** A handler that records the request and runs on its own thread, so a held reply holds nothing else. */
    private fun HttpServer.route(path: String, handle: (HttpExchange) -> Unit) {
        createContext(path) { exchange ->
            try {
                seen.add(Recorded(path, exchange.requestMethod, exchange.requestHeaders.toMap(), exchange.requestBody.readBytes()))
                handle(exchange)
            } catch (_: Throwable) {
                // The client went away; nothing to answer.
            } finally {
                exchange.close()
            }
        }
    }

    private fun stall(x: HttpExchange, status: Int) {
        x.sendResponseHeaders(status, 0)
        x.responseBody.write(bytes(10))
        x.responseBody.flush()
        Thread.sleep(1500)
        try {
            x.responseBody.write(bytes(10))
        } finally {
            heldDone.countDown()
        }
    }

    private fun send(request: SignatureTransportRequest): Answers {
        val answers = Answers()
        transport.send(request, answers) { answers.released.incrementAndGet() }
        answers.await()
        return answers
    }

    /** Sends [request] and returns the answers and how long the first took, in milliseconds. */
    private fun timed(request: SignatureTransportRequest): Pair<Answers, Long> {
        val started = System.nanoTime()
        val answers = send(request)
        return answers to (System.nanoTime() - started) / 1_000_000
    }

    @Test
    fun aReplyOfAnyStatusIsReplied() {
        // A POST of the body, content-type then the headers, the bytes back, any status.
        val body = bytes(300)
        var answers = send(
            request(
                "$base/echo?status=201",
                body = body,
                headers = listOf(SignatureTransportHeader("x-key", "k"), SignatureTransportHeader("x-session", "s")),
            ),
        )
        assertEquals(listOf("replied 201 6"), answers.got)
        assertEquals("ok:300", String(answers.bodies[0]))
        val sent = seen.last()
        assertEquals("POST", sent.method)
        assertArrayEquals(body, sent.body)
        assertEquals(listOf("application/timestamp-query"), sent.headers["Content-type"])
        assertEquals(listOf("k"), sent.headers["X-key"])
        assertEquals(listOf("s"), sent.headers["X-session"])
        // A request header named content-type is sent after it, and wins.
        send(request("$base/echo", headers = listOf(SignatureTransportHeader("Content-Type", "application/json"))))
        assertEquals(listOf("application/json"), seen.last().headers["Content-type"])
        answers = send(request("$base/error-body"))
        assertEquals(listOf("replied 404 4"), answers.got)
        assertEquals("nope", String(answers.bodies[0]))
    }

    @Test
    fun theDeadlineCoversTheWholeExchangeAndAnswersOnce() {
        val (held, heldAfter) = timed(request("$base/hold", timeoutMs = 300u))
        assertTrue("${held.got} after $heldAfter ms", held.got[0] == "timedOut" && heldAfter in 250..1400)
        val (stalled, stalledAfter) = timed(request("$base/stall-body", timeoutMs = 300u))
        assertTrue("${stalled.got} after $stalledAfter ms", stalled.got[0] == "timedOut" && stalledAfter in 250..1400)
        // A status outside 2xx in hand at the deadline is that status, with no body.
        val errorStall = send(request("$base/error-stall", timeoutMs = 300u))
        assertEquals("replied 429 0", errorStall.got[0])
        // Exactly once: the held replies have finished arriving; nothing more was answered.
        assertTrue(heldDone.await(10, TimeUnit.SECONDS))
        Thread.sleep(200)
        for (answers in listOf(held, stalled, errorStall)) {
            assertEquals(1, answers.got.size)
            assertEquals(1, answers.released.get())
        }
    }

    @Test
    fun maxReplyBytesIsEnforced() {
        // By content-length, or once more than maxReplyBytes is read.
        assertEquals("tooLarge", send(request("$base/long-length", maxReplyBytes = 1000u)).got[0])
        assertEquals("tooLarge", send(request("$base/long-stream", maxReplyBytes = 1000u)).got[0])
        val exact = send(request("$base/exact", maxReplyBytes = 1000u))
        assertEquals("replied 200 1000", exact.got[0])
        assertArrayEquals(bytes(1000), exact.bodies[0])
        assertEquals("replied 503 0", send(request("$base/error-long", maxReplyBytes = 1000u)).got[0])
    }

    @Test
    fun nothingListeningNoAddressOrNoStatusIsFailed() {
        val closed = ServerSocket(0).use { it.localPort }
        var answers = send(request("http://127.0.0.1:$closed/api/timestamp"))
        assertTrue(answers.got.toString(), answers.got.size == 1 && answers.got[0].startsWith("failed ") && answers.got[0].contains("Connect"))
        answers = send(request("not a url"))
        assertTrue(answers.got.toString(), answers.got.size == 1 && answers.got[0].startsWith("failed "))
        // A reply whose status line has no status: -1 from getResponseCode, never a status.
        val notHttp = ServerSocket(0, 1, InetAddress.getByName("127.0.0.1"))
        val answering = Thread {
            notHttp.accept().use { socket ->
                val input = BufferedReader(InputStreamReader(socket.getInputStream()))
                var length = 0
                while (true) {
                    val line = input.readLine() ?: break
                    if (line.isEmpty()) break
                    if (line.lowercase().startsWith("content-length:")) length = line.substringAfter(":").trim().toInt()
                }
                repeat(length) { input.read() }
                socket.getOutputStream().apply {
                    write("HTTP/1.1 OK\r\ncontent-length: 2\r\n\r\nok".toByteArray())
                    flush()
                }
            }
        }.apply {
            isDaemon = true
            start()
        }
        try {
            answers = send(request("http://127.0.0.1:${notHttp.localPort}/api/timestamp"))
            assertEquals(listOf("failed the reply is not HTTP"), answers.got)
        } finally {
            answering.join(5000)
            notHttp.close()
        }
    }

    /** Through the generated completion, as the viewer calls it. */
    @Test
    fun postAnswersThroughTheGeneratedCompletion() = viewerTest {
        val viewer = started(
            ViewerConfig(
                interaction = true,
                annotation = "",
                form = FormConfig(),
                signature = SignatureConfig(timestampAuthority = SignatureTimestampAuthority(url = "$base/error-body", retries = 0u)),
            ),
        )
        viewer.setSigner(TestSigner())
        viewer.setSignatureTransport(HttpSignatureTransport())
        val signatures = viewer.signatures(viewer.open(Fixture.bytes("unsigned_sigfield")))
        signatures.refresh()
        val field = signatures.unsignedFields().first().field
        // The authority answers 404: the Rust client refuses the timestamp,
        // so the signing fails, after one POST that reached the server.
        try {
            signatures.sign(SignatureSignInput(field = field, mark = SignatureMarkSource(Fixture.bytes("signature_artwork"))))
            throw AssertionError("a signing whose timestamp was refused finished")
        } catch (e: PdfException) {
            // Expected.
        }
        assertEquals(1, seen.count { it.path == "/error-body" })
        viewer.destroy()
    }

    companion object {
        fun bytes(n: Int): ByteArray = ByteArray(n) { (it % 251).toByte() }

        fun request(
            url: String,
            body: ByteArray = byteArrayOf(0x30, 0x03, 0x02, 0x01, 0x01),
            timeoutMs: UInt = 10_000u,
            maxReplyBytes: UInt = 1024u * 1024u,
            headers: List<SignatureTransportHeader> = emptyList(),
            contentType: String = "application/timestamp-query",
        ): SignatureTransportRequest = SignatureTransportRequest(
            url = url,
            contentType = contentType,
            headers = headers,
            body = body,
            timeoutMs = timeoutMs,
            maxReplyBytes = maxReplyBytes,
        )
    }
}
