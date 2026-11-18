package com.embedpdf.tests

import com.embedpdf.viewer.HttpSignatureTransport
import com.sun.net.httpserver.HttpExchange
import com.sun.net.httpserver.HttpServer
import java.io.BufferedReader
import java.io.File
import java.io.InputStreamReader
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.security.KeyFactory
import java.security.Signature
import java.security.spec.PKCS8EncodedKeySpec
import java.util.Base64
import java.util.Collections
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import kotlinx.coroutines.runBlocking
import uniffi.epdf_ffi.CancelToken
import uniffi.epdf_ffi.ViewerException
import uniffi.epdf_plugin_commands.CommandExecuteResult
import uniffi.epdf_plugin_form.FormConfig
import uniffi.epdf_plugin_interaction.InteractionTool
import uniffi.epdf_plugin_interaction.ToolPermission
import uniffi.epdf_plugin_signature.SignatureCallAuthority
import uniffi.epdf_plugin_signature.SignatureConfig
import uniffi.epdf_plugin_signature.SignatureCryptography
import uniffi.epdf_plugin_signature.SignatureDigest
import uniffi.epdf_plugin_signature.SignatureKeyAlgorithm
import uniffi.epdf_plugin_signature.SignatureMark
import uniffi.epdf_plugin_signature.SignatureRequestedTime
import uniffi.epdf_plugin_signature.SignatureRevocationRoute
import uniffi.epdf_plugin_signature.SignatureRevocationStatus
import uniffi.epdf_plugin_signature.SignatureSettingsPatch
import uniffi.epdf_plugin_signature.SignatureSignCompletion
import uniffi.epdf_plugin_signature.SignatureSignInput
import uniffi.epdf_plugin_signature.SignatureSigner
import uniffi.epdf_plugin_signature.SignatureSummary
import uniffi.epdf_plugin_signature.SignatureTimestampAuthority
import uniffi.epdf_plugin_signature.SignatureTransportCompletionInterface
import uniffi.epdf_plugin_signature.SignatureTransportHeader
import uniffi.epdf_plugin_signature.SignatureTransportRequest
import uniffi.epdf_plugin_signature.SignatureTrust
import uniffi.epdf_plugin_signature.SignatureValidateRequest
import uniffi.epdf_plugin_stamp.StampConfig
import uniffi.epdf_plugin_stamp.StampImportOptions
import uniffi.epdf_viewer_host.OpenDocumentOptions
import uniffi.epdf_viewer_host.StandardCommandCompletion
import uniffi.epdf_viewer_host.StandardCommandList
import uniffi.epdf_viewer_host.StandardCommandsPlatform
import uniffi.epdf_viewer_host.ViewerConfig
import uniffi.epdf_viewer_host.ViewerDelegate
import uniffi.epdf_viewer_host.ViewerHost

/**
 * The D-1 transport on `HttpURLConnection` (`viewer/HttpSignatureTransport.kt`)
 * and the catch-up's viewer surfaces, on a desktop JVM against the full
 * native viewer host.
 *
 * Part one sends requests straight to a loopback `HttpServer` and counts the
 * answers: each outcome, the deadline, the size limit, exactly one answer.
 * Part two runs the viewer: a granted timestamp and a long-term validation
 * fill, through upstream's routes (`signature-routes.mjs`, the first
 * argument is its port), and one call through each surface new to Kotlin.
 */

// ── part one: the transport against a local server ──────────────────────────

/** A completion that keeps every answer it is given. */
class Answers : SignatureTransportCompletionInterface {
    val got: MutableList<String> = Collections.synchronizedList(mutableListOf())
    val bodies: MutableList<ByteArray> = Collections.synchronizedList(mutableListOf())
    val released = AtomicInteger(0)
    private val first = CountDownLatch(1)
    override fun replied(status: UShort, body: ByteArray) { bodies.add(body); answer("replied $status ${body.size}") }
    override fun timedOut() = answer("timedOut")
    override fun tooLarge() = answer("tooLarge")
    override fun failed(message: String) = answer("failed $message")
    private fun answer(what: String) { got.add(what); first.countDown() }
    fun await(): String {
        check(first.await(10, TimeUnit.SECONDS)) { "no answer within 10 s" }
        return got[0]
    }
}

class Recorded(val method: String, val headers: Map<String, List<String>>, val body: ByteArray)

fun bytes(n: Int) = ByteArray(n) { (it % 251).toByte() }

/** A handler body that runs on its own thread, so a held reply holds nothing else. */
fun HttpServer.route(path: String, handle: (HttpExchange) -> Unit) = createContext(path) { exchange ->
    try { handle(exchange) } catch (_: Throwable) { } finally { exchange.close() }
}

fun request(url: String, body: ByteArray = byteArrayOf(0x30, 0x03, 0x02, 0x01, 0x01), timeoutMs: UInt = 10_000u,
    maxReplyBytes: UInt = 1024u * 1024u, headers: List<SignatureTransportHeader> = emptyList(),
    contentType: String = "application/timestamp-query") =
    SignatureTransportRequest(url = url, contentType = contentType, headers = headers, body = body,
        timeoutMs = timeoutMs, maxReplyBytes = maxReplyBytes)

fun transportOutcomes() {
    val server = HttpServer.create(InetSocketAddress("127.0.0.1", 0), 0)
    server.executor = Executors.newCachedThreadPool()
    val seen = Collections.synchronizedList(mutableListOf<Recorded>())
    val heldDone = CountDownLatch(3)
    server.route("/echo") { x ->
        seen.add(Recorded(x.requestMethod, x.requestHeaders.toMap(), x.requestBody.readBytes()))
        val status = x.requestURI.query?.removePrefix("status=")?.toInt() ?: 200
        val reply = "ok:${seen.last().body.size}".toByteArray()
        x.sendResponseHeaders(status, reply.size.toLong())
        x.responseBody.write(reply)
    }
    server.route("/hold") { x ->
        Thread.sleep(1500)
        try { x.sendResponseHeaders(200, 2); x.responseBody.write(byteArrayOf(1, 2)) } finally { heldDone.countDown() }
    }
    server.route("/stall-body") { x ->
        x.sendResponseHeaders(200, 0)
        x.responseBody.write(bytes(10)); x.responseBody.flush()
        Thread.sleep(1500)
        try { x.responseBody.write(bytes(10)) } finally { heldDone.countDown() }
    }
    server.route("/error-stall") { x ->
        x.sendResponseHeaders(429, 0)
        x.responseBody.write(bytes(10)); x.responseBody.flush()
        Thread.sleep(1500)
        try { x.responseBody.write(bytes(10)) } finally { heldDone.countDown() }
    }
    server.route("/long-length") { x -> x.sendResponseHeaders(200, 2000); x.responseBody.write(bytes(2000)) }
    server.route("/long-stream") { x -> x.sendResponseHeaders(200, 0); repeat(4) { x.responseBody.write(bytes(500)); x.responseBody.flush() } }
    server.route("/exact") { x -> x.sendResponseHeaders(200, 0); x.responseBody.write(bytes(1000)) }
    server.route("/error-long") { x -> x.sendResponseHeaders(503, 0); x.responseBody.write(bytes(2000)) }
    server.route("/error-body") { x -> val b = "nope".toByteArray(); x.sendResponseHeaders(404, b.size.toLong()); x.responseBody.write(b) }
    server.start()
    val base = "http://127.0.0.1:${server.address.port}"
    val transport = HttpSignatureTransport()
    fun send(r: SignatureTransportRequest): Answers {
        val answers = Answers()
        transport.send(r, answers) { answers.released.incrementAndGet() }
        answers.await()
        return answers
    }
    try {
        // replied: a POST of the body, content-type then the headers, the bytes back, any status.
        val body = bytes(300)
        var a = send(request("$base/echo?status=201", body = body,
            headers = listOf(SignatureTransportHeader("x-key", "k"), SignatureTransportHeader("x-session", "s"))))
        check(a.got[0] == "replied 201 6") { a.got.toString() }
        check(String(a.bodies[0]) == "ok:300")
        val sent = seen.last()
        check(sent.method == "POST") { sent.method }
        check(sent.body.contentEquals(body))
        check(sent.headers["Content-type"] == listOf("application/timestamp-query")) { sent.headers.toString() }
        check(sent.headers["X-key"] == listOf("k") && sent.headers["X-session"] == listOf("s")) { sent.headers.toString() }
        // A request header named content-type is sent after it, and wins.
        send(request("$base/echo", headers = listOf(SignatureTransportHeader("Content-Type", "application/json"))))
        check(seen.last().headers["Content-type"] == listOf("application/json")) { seen.last().headers.toString() }
        a = send(request("$base/error-body"))
        check(a.got[0] == "replied 404 4" && String(a.bodies[0]) == "nope") { a.got.toString() }
        println("SignatureTest: replied: a POST with content-type then the headers (a later one wins), the body, any status")

        // timedOut: the deadline covers waiting for the reply and reading it.
        var started = System.nanoTime()
        val held = send(request("$base/hold", timeoutMs = 300u))
        var waited = (System.nanoTime() - started) / 1_000_000
        check(held.got[0] == "timedOut" && waited in 250..1400) { "${held.got} after $waited ms" }
        started = System.nanoTime()
        val stalled = send(request("$base/stall-body", timeoutMs = 300u))
        waited = (System.nanoTime() - started) / 1_000_000
        check(stalled.got[0] == "timedOut" && waited in 250..1400) { "${stalled.got} after $waited ms" }
        // A status outside 2xx in hand at the deadline is that status, with no body.
        val errorStall = send(request("$base/error-stall", timeoutMs = 300u))
        check(errorStall.got[0] == "replied 429 0") { errorStall.got.toString() }
        println("SignatureTest: timedOut on a held reply and a stalled body; a stalled 429 is its status")

        // tooLarge: by content-length, or once more than maxReplyBytes is read.
        a = send(request("$base/long-length", maxReplyBytes = 1000u))
        check(a.got[0] == "tooLarge") { a.got.toString() }
        a = send(request("$base/long-stream", maxReplyBytes = 1000u))
        check(a.got[0] == "tooLarge") { a.got.toString() }
        a = send(request("$base/exact", maxReplyBytes = 1000u))
        check(a.got[0] == "replied 200 1000" && a.bodies[0].contentEquals(bytes(1000))) { a.got.toString() }
        a = send(request("$base/error-long", maxReplyBytes = 1000u))
        check(a.got[0] == "replied 503 0") { a.got.toString() }
        println("SignatureTest: tooLarge past maxReplyBytes; exactly the limit is a reply; a long 503 is its status")

        // failed: nothing listens.
        val closed = ServerSocket(0).use { it.localPort }
        a = send(request("http://127.0.0.1:$closed/api/timestamp"))
        check(a.got[0].startsWith("failed ") && a.got[0].contains("Connect")) { a.got.toString() }
        a = send(request("not a url"))
        check(a.got[0].startsWith("failed ")) { a.got.toString() }
        // A reply whose status line has no status: -1 from getResponseCode, never a status.
        val notHttp = ServerSocket(0, 1, java.net.InetAddress.getByName("127.0.0.1"))
        val answering = Thread {
            notHttp.accept().use { s ->
                val input = BufferedReader(InputStreamReader(s.getInputStream()))
                var length = 0
                while (true) {
                    val line = input.readLine() ?: break
                    if (line.isEmpty()) break
                    if (line.lowercase().startsWith("content-length:")) length = line.substringAfter(":").trim().toInt()
                }
                repeat(length) { input.read() }
                s.getOutputStream().apply { write("HTTP/1.1 OK\r\ncontent-length: 2\r\n\r\nok".toByteArray()); flush() }
            }
        }.apply { isDaemon = true; start() }
        try {
            a = send(request("http://127.0.0.1:${notHttp.localPort}/api/timestamp"))
            check(a.got[0] == "failed the reply is not HTTP") { a.got.toString() }
        } finally {
            answering.join(5000)
            notHttp.close()
        }
        println("SignatureTest: failed when nothing listens, the address is not a URL, or the reply has no status")

        // Exactly once: the held replies have finished arriving; nothing more was answered.
        check(heldDone.await(10, TimeUnit.SECONDS))
        Thread.sleep(200)
        for (answers in listOf(held, stalled, errorStall)) {
            check(answers.got.size == 1 && answers.released.get() == 1) { "${answers.got}, released ${answers.released}" }
        }
        println("SignatureTest: each completion is answered exactly once and released once")
    } finally {
        server.stop(0)
    }
}

// ── part two: the viewer ────────────────────────────────────────────────────

object Quiet : ViewerDelegate {
    override fun stateChanged(revision: ULong) {}
    override fun report(error: ViewerException) {}
}

const val FIXTURES = "packages/engine/main/test/fixtures"

/** A key and chain from the routes helper, signed with on the JVM. */
class JvmSigner(private val chain: List<ByteArray>, pkcs8: ByteArray) : SignatureSigner {
    private val key = KeyFactory.getInstance("RSA").generatePrivate(PKCS8EncodedKeySpec(pkcs8))
    val signed = AtomicInteger(0)
    override fun certificateChain(): List<ByteArray> = chain
    override fun algorithm() = SignatureKeyAlgorithm.RSA_PKCS1
    override fun digest() = SignatureDigest.SHA256
    override fun sign(message: ByteArray, completion: SignatureSignCompletion) {
        // Answer off the kernel thread, as a key behind a prompt would.
        Thread {
            val signature = Signature.getInstance("SHA256withRSA").apply { initSign(key); update(message) }.sign()
            signed.incrementAndGet()
            completion.finish(signature)
        }.start()
    }
}

class Anchors(private val anchors: List<ByteArray>) : SignatureTrust {
    override fun anchors(): List<ByteArray> = anchors
}

class Material(private val json: String) {
    private fun list(key: String): List<ByteArray> =
        Regex("\"$key\":\\[([^\\]]*)\\]").find(json)!!.groupValues[1].split(",")
            .map { Base64.getDecoder().decode(it.trim('"')) }
    private fun one(key: String): ByteArray =
        Base64.getDecoder().decode(Regex("\"$key\":\"([^\"]*)\"").find(json)!!.groupValues[1])
    val chain = list("chain")
    val anchors = list("anchors")
    val key = one("key")
    val timestampCertificate = one("timestampCertificate")
}

fun get(url: String): String = java.net.URI(url).toURL().readText()

suspend fun viewerSurfaces(routes: String) {
    val material = Material(get("$routes/material"))
    val unsigned = File("$FIXTURES/unsigned_sigfield.pdf").readBytes()
    val artwork = File("$FIXTURES/signature_artwork.pdf").readBytes()
    val authority = SignatureTimestampAuthority(url = "$routes/api/timestamp", headers = listOf(SignatureTransportHeader("x-session", "s")))
    val revocation = SignatureRevocationRoute(url = "$routes/api/revocation")
    val signer = JvmSigner(material.chain, material.key)

    fun viewer(config: SignatureConfig) = ViewerHost(ViewerConfig(
        interaction = true, annotation = "", form = FormConfig(), signature = config,
    ), Quiet).also {
        it.setSignatureSigner(signer)
        it.setSignatureTrust(Anchors(material.anchors))
        it.setSignatureTransport(HttpSignatureTransport())
    }

    // A granted timestamp, asked per call (SignatureCallAuthority.Http), with no authority in the settings.
    var host = viewer(SignatureConfig())
    try {
        host.start()
        val doc = host.openDocument(unsigned, OpenDocumentOptions()).id
        val s = host.signature(doc)
        s.refresh(CancelToken())
        check(!s.settings().hasTimestampAuthority)
        val field = s.unsignedFields()[0].field
        val done = s.sign(SignatureSignInput(field = field, mark = SignatureMark.Source(artwork),
            timestampAuthority = SignatureCallAuthority.Http(authority)), null, CancelToken())
        check(done.signature.signed)
        val verdict = s.validateField(field, SignatureValidateRequest(), CancelToken())
        val stamp = checkNotNull(verdict.timestamp) { "no timestamp in $verdict" }
        check(stamp.cryptography == SignatureCryptography.VALID) { stamp.toString() }
        check(stamp.authorityName == "EmbedPDF test timestamp authority") { stamp.toString() }
        check(stamp.authorityCertificate!!.contentEquals(material.timestampCertificate))
        check(verdict.summary == SignatureSummary.VALID) { verdict.toString() }
        // The query carried an 8-octet nonce, and the token answered it.
        check(get("$routes/log").contains("\"nonces\":[8]")) { get("$routes/log") }
        println("SignatureTest: a granted timestamp through the per-call authority and timestampRoute; verdict ${verdict.summary}")
    } finally {
        host.destroyViewer(); host.shutdown(); host.close()
    }

    // A long-term fill: authority and route in the settings; sign seals, then fills.
    host = viewer(SignatureConfig(timestampAuthority = authority, revocation = revocation))
    try {
        host.start()
        val doc = host.openDocument(unsigned, OpenDocumentOptions()).id
        val s = host.signature(doc)
        s.refresh(CancelToken())
        val settings = s.settings()
        check(settings.hasTimestampAuthority && settings.hasRevocation && !settings.documentTimestamp) { settings.toString() }
        check(s.canAddValidationInfo())
        val field = s.unsignedFields()[0].field
        val done = s.sign(SignatureSignInput(field = field, mark = SignatureMark.Source(artwork)), null, CancelToken())
        check(done.validationInfoError == null) { done.validationInfoError.toString() }
        val added = checkNotNull(done.validationInfo) { "no validation info" }
        check(added.added.ocspResponses > 0u && added.documentTimestamp == null) { added.toString() }
        check(get("$routes/log").contains("POST http://test-authority.example/ocsp")) { get("$routes/log") }
        val verdict = s.validate(SignatureValidateRequest(validationTime = SignatureRequestedTime.EarliestTimestamp), CancelToken()).single()
        check(verdict.summary == SignatureSummary.VALID) { verdict.toString() }
        check(verdict.revocation == SignatureRevocationStatus.GOOD) { verdict.toString() }
        check(verdict.longTerm.enabled && verdict.longTerm.missing.isEmpty() && verdict.longTerm.documentTimestampedAt == null) { verdict.longTerm.toString() }
        println("SignatureTest: a long-term fill through revocationRoute; verdict ${verdict.summary}, revocation ${verdict.revocation}, longTerm.enabled")

        // The settings patch, then add_validation_info asked directly: the route is gone, so it refuses.
        s.updateSettings(SignatureSettingsPatch(clearRevocation = true))
        check(!s.settings().hasRevocation)
        check(!s.canAddValidationInfo())
        try {
            s.addValidationInfo(field, CancelToken())
            error("add_validation_info without a revocation route must fail")
        } catch (error: ViewerException.Kernel) {
            check(error.code == "invalid-input") { error.toString() }
            println("SignatureTest: without a route, addValidationInfo refuses: ${error.code} ${error.detail}")
        }
        s.updateSettings(SignatureSettingsPatch(revocation = revocation))
        check(s.canAddValidationInfo())
        // Everything the signature needs is already in the document, so nothing more is added.
        val again = s.addValidationInfo(field, CancelToken())
        check(again.added.certificates == 0u && again.added.ocspResponses == 0u && again.added.crls == 0u) { again.toString() }
        println("SignatureTest: addValidationInfo with the route back answers ${again.added}")
    } finally {
        host.destroyViewer(); host.shutdown(); host.close()
    }

    // The per-call SignatureCallAuthority.Without: no timestamp although the settings name an authority.
    host = viewer(SignatureConfig(timestampAuthority = authority))
    try {
        host.start()
        val doc = host.openDocument(unsigned, OpenDocumentOptions()).id
        val s = host.signature(doc)
        s.refresh(CancelToken())
        val before = Regex("\"timestampQueries\":(\\d+)").find(get("$routes/log"))!!.groupValues[1]
        val field = s.unsignedFields()[0].field
        s.sign(SignatureSignInput(field = field, mark = SignatureMark.Source(artwork),
            timestampAuthority = SignatureCallAuthority.Without), null, CancelToken())
        val verdict = s.validateField(field, SignatureValidateRequest(validationTime = SignatureRequestedTime.Now), CancelToken())
        check(verdict.timestamp == null) { verdict.toString() }
        check(get("$routes/log").contains("\"timestampQueries\":$before,")) { get("$routes/log") }
        println("SignatureTest: SignatureCallAuthority.Without signs with no timestamp")
    } finally {
        host.destroyViewer(); host.shutdown(); host.close()
    }
}

class Platform : StandardCommandsPlatform {
    val saved = Collections.synchronizedList(mutableListOf<Triple<ByteArray, String, String>>())
    override fun saveFile(bytes: ByteArray, fileName: String, contentType: String) { saved.add(Triple(bytes, fileName, contentType)) }
    override fun copyText(text: String, completion: StandardCommandCompletion) { completion.finish() }
    override fun copyImage(png: ByteArray, completion: StandardCommandCompletion) { completion.finish() }
}

suspend fun otherSurfaces() {
    val unsigned = File("$FIXTURES/unsigned_sigfield.pdf").readBytes()
    val host = ViewerHost(ViewerConfig(
        interaction = true, history = true, selection = true, commands = true, annotation = "",
        stamp = StampConfig(),
        // Named out of order: the join keeps upstream's.
        standardCommands = listOf(StandardCommandList.DOCUMENT, StandardCommandList.HISTORY),
    ), Quiet)
    try {
        host.start()
        val platform = Platform()
        host.setCommandsPlatform(platform)
        val doc = host.openDocument(unsigned, OpenDocumentOptions()).id
        val commands = host.commands()
        val ids = commands.commandIds()
        check(ids.indexOf("history:undo") in 0 until ids.indexOf("document:download")) { ids.toString() }
        check(commands.execute("document:download", doc, null) == CommandExecuteResult.Executed)
        check(platform.saved.size == 1 && String(platform.saved[0].first, 0, 4) == "%PDF" &&
            platform.saved[0].third == "application/pdf") { platform.saved.map { it.second } }
        println("SignatureTest: standard commands joined in upstream's order; document:download reached the platform")

        val interaction = host.interaction(doc)
        fun tool(id: String) = InteractionTool(id = id, cursor = "crosshair", enables = emptyList())
        interaction.registerTool(tool("kotlin-refused"), false, object : ToolPermission { override fun canUse() = false })
        interaction.registerTool(tool("kotlin-allowed"), false, object : ToolPermission { override fun canUse() = true })
        check(!interaction.canUseTool("kotlin-refused") && interaction.canUseTool("kotlin-allowed"))
        println("SignatureTest: a ToolPermission answers canUseTool")

        check(!host.annotation(doc).drafts().hasCreationAwaitingClick())
        println("SignatureTest: hasCreationAwaitingClick reads false with no tool armed")

        val stamps = File("packages/default-stamps/en/stamps.pdf").readBytes()
        val library = host.stamp().importLibrary(stamps, StampImportOptions(id = "kotlin-library"), CancelToken())
        check(library == "kotlin-library") { library }
        println("SignatureTest: StampImportOptions.id names the imported library")
    } finally {
        host.destroyViewer(); host.shutdown(); host.close()
    }
}

fun main(args: Array<String>) {
    transportOutcomes()
    val routes = "http://127.0.0.1:${args[0]}"
    runBlocking {
        viewerSurfaces(routes)
        otherSurfaces()
    }
    println("SignatureTest: all passed")
}
