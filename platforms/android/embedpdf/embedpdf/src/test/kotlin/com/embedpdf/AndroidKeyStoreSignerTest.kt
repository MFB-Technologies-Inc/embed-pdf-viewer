package com.embedpdf

import android.security.keystore.KeyPermanentlyInvalidatedException
import android.security.keystore.UserNotAuthenticatedException
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.withTimeout
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config
import uniffi.epdf_plugin_signature.NoHandle
import java.security.KeyPairGenerator
import java.security.KeyStore
import java.security.Signature
import java.security.cert.CertificateFactory
import java.security.cert.X509Certificate

/**
 * `KeychainSignerTests` and `SecurityTests`' signer cases. Robolectric has no
 * Android Keystore, so the signer reads a software key from an injected
 * PKCS #12 store, or takes the key itself; a key that needs the user is a
 * signing that throws what the Keystore throws.
 */
@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class AndroidKeyStoreSignerTest {
    private val certificate: X509Certificate by lazy {
        CertificateFactory.getInstance("X.509").generateCertificate(TestKeys.certificate.inputStream()) as X509Certificate
    }

    /** A software store holding the test key under `signer`. */
    private fun store(): KeyStore = KeyStore.getInstance("PKCS12").apply {
        load(null)
        setKeyEntry("signer", TestKeys.privateKey, CharArray(0), arrayOf(certificate))
    }

    private fun testSigner(): AndroidKeyStoreSigner = AndroidKeyStoreSigner("signer", store())

    /** What a signing answered: the signature, or the failure's code and name. */
    private class Answer : SignatureSignCompletion(NoHandle) {
        val answer = CompletableDeferred<Any>()

        override fun finish(signature: ByteArray) {
            answer.complete(signature)
        }

        override fun fail(detail: String) {
            answer.complete(detail)
        }

        override fun failWith(failure: HostFailure) {
            answer.complete(failure)
        }
    }

    @Test
    fun anRsaKeySignsPkcs1() {
        val signer = testSigner()
        assertEquals(SignatureKeyAlgorithm.RSA_PKCS1, signer.algorithm())
        assertEquals(SignatureDigest.SHA256, signer.digest())
        val signature = signer.sign("hello".toByteArray())
        assertEquals(256, signature.size)
        val verified = Signature.getInstance("SHA256withRSA").run {
            initVerify(certificate)
            update("hello".toByteArray())
            verify(signature)
        }
        assertTrue(verified)
        assertEquals(1, signer.certificateChain().size)
        assertArrayEquals(TestKeys.certificate, signer.certificateChain()[0])
    }

    @Test
    fun anEcdsaSignatureIsRawRAndS() {
        val key = KeyPairGenerator.getInstance("EC").apply { initialize(256) }.generateKeyPair()
        val der = Signature.getInstance("SHA256withECDSA").run {
            initSign(key.private)
            update("x".toByteArray())
            sign()
        }
        assertEquals(64, AndroidKeyStoreSigner.rawEcdsa(der, 32).size)
    }

    @Test
    fun anEcKeySignsRawRAndS() {
        val key = KeyPairGenerator.getInstance("EC").apply { initialize(256) }.generateKeyPair()
        val signer = AndroidKeyStoreSigner(key.private, listOf(certificate))
        assertEquals(SignatureKeyAlgorithm.ECDSA, signer.algorithm())
        assertEquals(64, signer.sign("x".toByteArray()).size)
    }

    @Test
    fun aMalformedSignatureIsRefused() = viewerTest {
        expectCode(PdfException.Code.OPERATION_FAILED) { AndroidKeyStoreSigner.rawEcdsa(byteArrayOf(0x30, 0x05, 0x02), 32) }
    }

    /** Long-form lengths: eight length bytes, 127 of them, a length past the data, BER's indefinite length. */
    @Test
    fun aMalformedDerLengthThrowsInsteadOfCrashing() = viewerTest {
        val cases = listOf(
            listOf(0x30, 0x88, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff),
            listOf(0x30, 0xff) + List(127) { 0xff },
            listOf(0x30, 0x84, 0xff, 0xff, 0xff, 0xff, 0x02),
            listOf(0x30, 0x80, 0x02, 0x01, 0x01),
            listOf(0x30, 0x03, 0x02, 0x88, 0x80),
        )
        for (case in cases) {
            val der = ByteArray(case.size) { case[it].toByte() }
            expectCode(PdfException.Code.OPERATION_FAILED, "$case") { AndroidKeyStoreSigner.rawEcdsa(der, 32) }
        }
    }

    @Test
    fun anAliasWithoutAKeyIsNotFound() = viewerTest {
        expectCode(PdfException.Code.NOT_FOUND) { AndroidKeyStoreSigner("nobody", store()) }
    }

    @Test
    fun aKeyThatNeedsTheUserFailsWithPermissionDenied() = viewerTest {
        val signer = AndroidKeyStoreSigner(TestKeys.privateKey, listOf(certificate)) { _, _, _ ->
            throw UserNotAuthenticatedException("User not authenticated")
        }
        val error = expectCode(PdfException.Code.PERMISSION_DENIED) { signer.sign("x".toByteArray()) }
        assertEquals("UserNotAuthenticated", error.name)
        val invalidated = AndroidKeyStoreSigner(TestKeys.privateKey, listOf(certificate)) { _, _, _ ->
            throw KeyPermanentlyInvalidatedException("Key permanently invalidated")
        }
        assertEquals("KeyPermanentlyInvalidated", expectCode(PdfException.Code.PERMISSION_DENIED) { invalidated.sign("x".toByteArray()) }.name)

        // Through the viewer: the signing fails with the same code, and nothing is left pending.
        val viewer = started(ViewerConfig(interaction = true, annotation = "", form = FormConfig(), signature = SignatureConfig()))
        viewer.setSigner(signer)
        val signatures = viewer.signatures(viewer.open(Fixture.bytes("unsigned_sigfield")))
        signatures.refresh()
        val field = signatures.unsignedFields().first().field
        expectCode(PdfException.Code.PERMISSION_DENIED) {
            signatures.sign(SignatureSignInput(field = field, mark = SignatureMarkSource(Fixture.bytes("signature_artwork"))))
        }
        assertNull(signatures.pending())
        assertFalse(signatures.isBusy())
        viewer.destroy()
    }

    @Test
    fun theSigningRunsOffTheCallingThread() = viewerTest {
        val signer = testSigner()
        val caller = Thread.currentThread()
        var signedOn: Thread? = null
        val watched = AndroidKeyStoreSigner(TestKeys.privateKey, listOf(certificate)) { key, algorithm, message ->
            signedOn = Thread.currentThread()
            Signature.getInstance(algorithm).run {
                initSign(key)
                update(message)
                sign()
            }
        }
        val completion = Answer()
        watched.sign("x".toByteArray(), completion)
        val answer = withTimeout(10_000) { completion.answer.await() }
        assertArrayEquals(signer.sign("x".toByteArray()), answer as ByteArray)
        assertTrue(signedOn != null && signedOn != caller)
    }

    /** `SignatureTests.theTestCertificateAsAnAnchorIsTrusted`, with this signer and anchors read from assets. */
    @Test
    fun aKeyStoreSignatureIsTrustedByAnchorsFromAssets() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, annotation = "", form = FormConfig(), signature = SignatureConfig()))
        viewer.setSigner(testSigner())
        viewer.setTrust(TrustAnchors.fromAssets(RuntimeEnvironment.getApplication().assets, listOf("embedpdf/trust/TEST-ONLY-signer.pem")))
        val signatures = viewer.signatures(viewer.open(Fixture.bytes("unsigned_sigfield")))
        signatures.refresh()
        val field = signatures.unsignedFields().first().field
        val done = signatures.sign(SignatureSignInput(field = field, mark = SignatureMarkSource(Fixture.bytes("signature_artwork"))))
        assertEquals(SignatureCompletionStatus.COMPLETED, done.status)
        val verdict = signatures.validate(field)
        assertEquals(SignatureTrustStatus.TRUSTED, verdict.trust)
        assertEquals(SignatureSummary.VALID, verdict.summary)
        viewer.destroy()
    }
}

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class TrustAnchorsTest {
    private val assets get() = RuntimeEnvironment.getApplication().assets

    @Test
    fun aPemAndADerAssetAreAnchors() {
        val anchors = TrustAnchors.fromAssets(assets, listOf("embedpdf/trust/TEST-ONLY-signer.pem", "embedpdf/trust/TEST-ONLY-signer.cer"))
        assertEquals(2, anchors.certificates.size)
        assertArrayEquals(TestKeys.certificate, anchors.anchors()[0])
        assertArrayEquals(anchors.anchors()[0], anchors.anchors()[1])
        assertEquals(0x30, anchors.anchors()[0][0].toInt())
    }

    @Test
    fun aBadOrMissingAnchorIsRefused() = viewerTest {
        expectCode(PdfException.Code.INVALID_INPUT) { TrustAnchors.fromAssets(assets, listOf("embedpdf/trust/not-a-certificate.pem")) }
        expectCode(PdfException.Code.NOT_FOUND) { TrustAnchors.fromAssets(assets, listOf("embedpdf/trust/missing.pem")) }
    }
}
