package com.embedpdf

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.int
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.AfterClass
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Assume.assumeTrue
import org.junit.BeforeClass
import org.junit.Test
import java.io.File
import java.net.URI
import java.security.KeyFactory
import java.security.PrivateKey
import java.security.cert.CertificateFactory
import java.security.cert.X509Certificate
import java.security.spec.PKCS8EncodedKeySpec
import java.util.Base64
import java.util.concurrent.TimeUnit

/**
 * `SignatureTransportTests`' route cases: the viewer signs through
 * [HttpSignatureTransport] against upstream's `timestampRoute` and
 * `revocationRoute`, which `platforms/android/tests/signature-routes.mjs`
 * serves on a loopback port from `packages/core/signature/dist`.
 *
 * The class starts that helper with `node`. Without `node` or a built
 * `dist` it is skipped, unless `EMBEDPDF_SIGNATURE_ROUTES_REQUIRED=1`, which
 * `test-signature.sh` sets, makes that a failure.
 */
class SignatureRoutesTest {
    class Material(json: String) {
        private val root = Json.parseToJsonElement(json).jsonObject

        private fun one(key: String): ByteArray = Base64.getDecoder().decode(root.getValue(key).jsonPrimitive.content)

        private fun list(key: String): List<ByteArray> =
            root.getValue(key).jsonArray.map { Base64.getDecoder().decode(it.jsonPrimitive.content) }

        val chain: List<X509Certificate> = list("chain").map(::certificate)
        val anchors: List<X509Certificate> = list("anchors").map(::certificate)
        val key: PrivateKey = KeyFactory.getInstance("RSA").generatePrivate(PKCS8EncodedKeySpec(one("key")))
        val timestampCertificate: ByteArray = one("timestampCertificate")

        private fun certificate(der: ByteArray): X509Certificate =
            CertificateFactory.getInstance("X.509").generateCertificate(der.inputStream()) as X509Certificate
    }

    private val authority get() = SignatureTimestampAuthority(
        url = "$routes/api/timestamp",
        headers = listOf(SignatureTransportHeader("x-session", "s")),
    )

    private val revocation get() = SignatureRevocationRoute(url = "$routes/api/revocation")

    private suspend fun viewer(config: SignatureConfig): PdfViewer {
        val viewer = started(ViewerConfig(interaction = true, annotation = "", form = FormConfig(), signature = config))
        viewer.setSigner(AndroidKeyStoreSigner(material.key, material.chain))
        viewer.setTrust(TrustAnchors(material.anchors))
        viewer.setSignatureTransport(HttpSignatureTransport())
        return viewer
    }

    private suspend fun unsigned(viewer: PdfViewer): Pair<SignatureCapability, FormFieldRef> {
        val signatures = viewer.signatures(viewer.open(Fixture.bytes("unsigned_sigfield")))
        signatures.refresh()
        return signatures to signatures.unsignedFields().first().field
    }

    private fun mark(): SignatureMark = SignatureMarkSource(Fixture.bytes("signature_artwork"))

    private fun log(): String = get("/log")

    /** The nonce length of every timestamp query the helper has seen. */
    private fun nonces(): List<Int> = Json.parseToJsonElement(log()).jsonObject.getValue("nonces").jsonArray.map { it.jsonPrimitive.int }

    private fun timestampQueries(): Int = Json.parseToJsonElement(log()).jsonObject.getValue("timestampQueries").jsonPrimitive.int

    /** A granted timestamp asked per call ([SignatureCallAuthorityHttp]), with no authority in the settings. */
    @Test
    fun aGrantedTimestampThroughTheTimestampRoute() = viewerTest {
        val viewer = viewer(SignatureConfig())
        val (signatures, field) = unsigned(viewer)
        val before = nonces()
        assertFalse(signatures.settings().hasTimestampAuthority)
        val done = signatures.sign(SignatureSignInput(field = field, mark = mark(), timestampAuthority = SignatureCallAuthorityHttp(authority)))
        assertTrue(done.signature.signed)
        val verdict = signatures.validate(field)
        val stamp = assertNotNullValue(verdict.timestamp, "no timestamp in $verdict")
        assertEquals(SignatureCryptography.VALID, stamp.cryptography)
        assertEquals("EmbedPDF test timestamp authority", stamp.authorityName)
        assertArrayEquals(material.timestampCertificate, stamp.authorityCertificate)
        assertEquals(SignatureSummary.VALID, verdict.summary)
        // The query carried an 8-octet nonce, and the token answered it.
        assertEquals(before + 8, nonces())
        viewer.destroy()
    }

    /** A long-term fill: authority and route in the settings; `sign` seals, then fills. */
    @Test
    fun aLongTermFillThroughTheRevocationRoute() = viewerTest {
        val viewer = viewer(SignatureConfig(timestampAuthority = authority, revocation = revocation))
        val (signatures, field) = unsigned(viewer)
        val settings = signatures.settings()
        assertTrue("$settings", settings.hasTimestampAuthority && settings.hasRevocation && !settings.documentTimestamp)
        assertTrue(signatures.canAddValidationInfo())
        val done = signatures.sign(SignatureSignInput(field = field, mark = mark()))
        assertNull(done.validationInfoError)
        val added = assertNotNullValue(done.validationInfo, "no validation info")
        assertTrue("$added", added.added.ocspResponses > 0u && added.documentTimestamp == null)
        assertTrue(log(), log().contains("POST http://test-authority.example/ocsp"))
        val verdict = signatures.validate(SignatureValidateRequest(validationTime = SignatureRequestedTimeEarliestTimestamp)).single()
        assertEquals(SignatureSummary.VALID, verdict.summary)
        assertEquals(SignatureRevocationStatus.GOOD, verdict.revocation)
        assertTrue("${verdict.longTerm}", verdict.longTerm.enabled && verdict.longTerm.missing.isEmpty() && verdict.longTerm.documentTimestampedAt == null)

        // The settings patch, then addValidationInfo asked directly: the route is gone, so it refuses.
        signatures.updateSettings(SignatureSettingsPatch(clearRevocation = true))
        assertFalse(signatures.settings().hasRevocation)
        assertFalse(signatures.canAddValidationInfo())
        expectCode(PdfException.Code.INVALID_INPUT) { signatures.addValidationInfo(field) }
        signatures.updateSettings(SignatureSettingsPatch(revocation = revocation))
        assertTrue(signatures.canAddValidationInfo())
        // Everything the signature needs is already in the document, so nothing more is added.
        val again = signatures.addValidationInfo(field)
        assertEquals(SignatureValidationInfoCounts(certificates = 0u, ocspResponses = 0u, crls = 0u), again.added)
        viewer.destroy()
    }

    /** [SignatureCallAuthorityWithout]: no timestamp although the settings name an authority. */
    @Test
    fun aCallWithoutAnAuthoritySendsNoQuery() = viewerTest {
        val viewer = viewer(SignatureConfig(timestampAuthority = authority))
        val (signatures, field) = unsigned(viewer)
        val before = timestampQueries()
        signatures.sign(SignatureSignInput(field = field, mark = mark(), timestampAuthority = SignatureCallAuthorityWithout))
        val verdict = signatures.validate(field, SignatureValidateRequest(validationTime = SignatureRequestedTimeNow))
        assertNull(verdict.timestamp)
        assertEquals(before, timestampQueries())
        viewer.destroy()
    }

    private fun <T : Any> assertNotNullValue(value: T?, message: String): T {
        assertNotNull(message, value)
        return value!!
    }

    companion object {
        private var helper: Process? = null
        private lateinit var routes: String
        private lateinit var material: Material

        private fun get(path: String): String = URI("$routes$path").toURL().readText()

        @JvmStatic
        @BeforeClass
        fun startRoutes() {
            val required = System.getenv("EMBEDPDF_SIGNATURE_ROUTES_REQUIRED") == "1"
            val root = File(System.getProperty("embedpdf.repoRoot"))
            val built = root.resolve("packages/core/signature/dist/server.js").isFile
            val node = System.getenv("PATH").orEmpty().split(File.pathSeparator).any { File(it, "node").canExecute() }
            if (required) {
                check(built) { "build packages/core/signature first: pnpm --filter @embedpdf/core-signature build" }
                check(node) { "no node on the PATH" }
            }
            assumeTrue("needs node and a built packages/core/signature/dist", built && node)
            val process = ProcessBuilder("node", root.resolve("platforms/android/tests/signature-routes.mjs").path, root.path)
                .redirectErrorStream(true)
                .start()
            helper = process
            // The helper prints `READY <port>` once it listens, and stops when its standard input closes.
            val output = process.inputStream.bufferedReader()
            val ready = generateSequence { output.readLine() }.firstOrNull { it.startsWith("READY ") }
                ?: error("the routes helper did not start")
            Thread {
                output.forEachLine {}
            }.apply {
                isDaemon = true
                start()
            }
            routes = "http://127.0.0.1:${ready.removePrefix("READY ").trim()}"
            material = Material(get("/material"))
        }

        @JvmStatic
        @AfterClass
        fun stopRoutes() {
            helper?.let { process ->
                process.outputStream.close()
                if (!process.waitFor(5, TimeUnit.SECONDS)) process.destroyForcibly()
            }
        }
    }
}
