package com.embedpdf

import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.security.keystore.KeyProtection
import android.util.Log
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.After
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import java.io.ByteArrayOutputStream
import java.math.BigInteger
import java.security.KeyFactory
import java.security.KeyPairGenerator
import java.security.KeyStore
import java.security.Signature
import java.security.cert.CertificateFactory
import java.security.cert.X509Certificate
import java.security.spec.PKCS8EncodedKeySpec
import java.security.spec.ECGenParameterSpec
import javax.security.auth.x500.X500Principal

/**
 * `AndroidKeyStoreSigner` over keys in the real Android Keystore, which
 * Robolectric replaces with a software store and throwing fakes.
 *
 * The user-authentication case needs a secure lock screen to generate its
 * key (`adb shell locksettings set-pin 1234` before the run). No test here
 * shows a biometric or credential prompt: those can't be answered from a
 * test, so a key behind one is checked only for its refusal.
 */
@RunWith(AndroidJUnit4::class)
class DeviceKeyStoreTest {
    private val store = AndroidKeyStoreSigner.androidKeyStore()
    private val aliases = mutableListOf<String>()

    @After
    fun deleteKeys() {
        aliases.forEach { store.deleteEntry(it) }
    }

    /**
     * An EC P-256 signing key in the Keystore. Without an attestation
     * challenge the Keystore makes a certificate signed by the key itself.
     */
    private fun generate(alias: String, configure: KeyGenParameterSpec.Builder.() -> Unit = {}): X509Certificate {
        store.deleteEntry(alias)
        aliases += alias
        val spec = KeyGenParameterSpec.Builder(alias, KeyProperties.PURPOSE_SIGN or KeyProperties.PURPOSE_VERIFY)
            .setAlgorithmParameterSpec(ECGenParameterSpec("secp256r1"))
            .setDigests(KeyProperties.DIGEST_SHA256)
            .setCertificateSubject(X500Principal("CN=EmbedPDF device test"))
            .apply(configure)
            .build()
        KeyPairGenerator.getInstance(KeyProperties.KEY_ALGORITHM_EC, "AndroidKeyStore").apply { initialize(spec) }.generateKeyPair()
        return store.getCertificate(alias) as X509Certificate
    }

    /** `r || s` back to X9.62 DER, for the JCA verifier. */
    private fun der(raw: ByteArray): ByteArray {
        val half = raw.size / 2
        fun integer(bytes: ByteArray): ByteArray {
            val value = BigInteger(1, bytes).toByteArray()
            return byteArrayOf(0x02, value.size.toByte()) + value
        }
        val body = ByteArrayOutputStream().apply {
            write(integer(raw.copyOfRange(0, half)))
            write(integer(raw.copyOfRange(half, raw.size)))
        }.toByteArray()
        return byteArrayOf(0x30, body.size.toByte()) + body
    }

    @Test
    fun aKeyStoreKeySignsAMessageThatVerifiesWithItsPublicKey() {
        val certificate = generate("embedpdf-device-ec")
        // Self-signed: the certificate verifies with its own key.
        certificate.verify(certificate.publicKey)
        val signer = AndroidKeyStoreSigner("embedpdf-device-ec")
        assertEquals(SignatureKeyAlgorithm.ECDSA, signer.algorithm())
        assertArrayEquals(certificate.encoded, signer.certificateChain().single())
        val message = "signed on the device".toByteArray()
        val raw = signer.sign(message)
        assertEquals(64, raw.size)
        val verified = Signature.getInstance("SHA256withECDSA").run {
            initVerify(certificate.publicKey)
            update(message)
            verify(der(raw))
        }
        assertTrue("the signature does not verify", verified)
    }

    /** A key bound to the user, signed with before any authentication: refused, not a crash. */
    @Test
    fun aKeyThatNeedsTheUserIsPermissionDeniedUserNotAuthenticated() = onDevice {
        // Per use. setUserAuthenticationRequired(true) alone means per use
        // with a biometric, which needs one enrolled; the device credential
        // needs only the lock screen.
        generate("embedpdf-device-per-use") {
            setUserAuthenticationRequired(true)
            setUserAuthenticationParameters(0, KeyProperties.AUTH_DEVICE_CREDENTIAL)
        }
        // With a validity window.
        generate("embedpdf-device-window") {
            setUserAuthenticationRequired(true)
            setUserAuthenticationParameters(30, KeyProperties.AUTH_DEVICE_CREDENTIAL or KeyProperties.AUTH_BIOMETRIC_STRONG)
        }
        for (alias in listOf("embedpdf-device-per-use", "embedpdf-device-window")) {
            val error = expectPdfCode(PdfException.Code.PERMISSION_DENIED) { AndroidKeyStoreSigner(alias).sign("x".toByteArray()) }
            Log.i(TAG, "$alias: $error")
            assertEquals(alias, "UserNotAuthenticated", error.name)
        }

        // Through the viewer: the signing fails with the same code, and nothing is left pending.
        val viewer = PdfViewer(ViewerConfig(interaction = true, annotation = "", form = FormConfig(), signature = SignatureConfig()))
        viewer.start()
        viewer.setSigner(AndroidKeyStoreSigner("embedpdf-device-per-use"))
        val signatures = viewer.signatures(viewer.open(DeviceFixture.bytes("unsigned_sigfield")))
        signatures.refresh()
        val field = signatures.unsignedFields().first().field
        val error = expectPdfCode(PdfException.Code.PERMISSION_DENIED) {
            signatures.sign(SignatureSignInput(field = field, mark = SignatureMarkSource(DeviceFixture.bytes("signature_artwork"))))
        }
        Log.i(TAG, "through the viewer: $error")
        assertNull(signatures.pending())
        assertFalse(signatures.isBusy())
        viewer.destroy()
    }

    /**
     * A whole document signed with a key generated in the Keystore, and the
     * engine's verdict on it. The signature and its integrity are valid; the
     * certificate the Keystore made is self-signed but not a CA, so with it
     * as the anchor the engine still answers untrusted.
     */
    @Test
    fun aDocumentSignedWithAKeyStoreKeyIsValidButItsSelfSignedCertificateIsNoAnchor() = onDevice {
        val certificate = generate("embedpdf-device-document")
        val viewer = PdfViewer(ViewerConfig(interaction = true, annotation = "", form = FormConfig(), signature = SignatureConfig()))
        viewer.start()
        viewer.setSigner(AndroidKeyStoreSigner("embedpdf-device-document"))
        viewer.setTrust(TrustAnchors.fromBytes(certificate.encoded))
        val document = viewer.open(DeviceFixture.bytes("unsigned_sigfield"))
        val signatures = viewer.signatures(document)
        signatures.refresh()
        val field = signatures.unsignedFields().first().field
        val done = signatures.sign(SignatureSignInput(field = field, mark = SignatureMarkSource(DeviceFixture.bytes("signature_artwork"))))
        assertEquals(SignatureCompletionStatus.COMPLETED, done.status)
        val verdict = signatures.validate(field)
        Log.i(TAG, "verdict: $verdict")
        assertEquals(SignatureIntegrity.VALID, verdict.integrity)
        assertEquals(SignatureCryptography.VALID, verdict.cryptography)
        assertEquals(SignatureTrustStatus.UNTRUSTED, verdict.trust)
        assertEquals("One of intermediate certificates is not a CA certificate", verdict.trustReason)
        assertEquals(SignatureSummary.VALID_UNTRUSTED, verdict.summary)

        // The saved file reopens with the signature in it, and validates the same.
        val saved = viewer.save(document)
        val reopened = viewer.signatures(viewer.open(saved))
        reopened.refresh()
        assertEquals(listOf(SignatureSummary.VALID_UNTRUSTED), reopened.validate().map { it.summary })
        viewer.destroy()
    }

    /**
     * The test signer's RSA key imported into the Keystore, so the key still
     * signs inside it, with a certificate the engine accepts as an anchor:
     * `aKeyStoreSignatureIsTrustedByAnchorsFromAssets` on a real Keystore.
     */
    @Test
    fun aDocumentSignedWithAnImportedKeyStoreKeyIsTrusted() = onDevice {
        val alias = "embedpdf-device-imported"
        aliases += alias
        val certificate = CertificateFactory.getInstance("X.509")
            .generateCertificate(DeviceFixture.pemBody("TEST-ONLY-signer").inputStream()) as X509Certificate
        val key = KeyFactory.getInstance("RSA").generatePrivate(PKCS8EncodedKeySpec(DeviceFixture.pemBody("TEST-ONLY-private-key")))
        store.setEntry(
            alias,
            KeyStore.PrivateKeyEntry(key, arrayOf(certificate)),
            KeyProtection.Builder(KeyProperties.PURPOSE_SIGN)
                .setDigests(KeyProperties.DIGEST_SHA256)
                .setSignaturePaddings(KeyProperties.SIGNATURE_PADDING_RSA_PKCS1)
                .build(),
        )
        val signer = AndroidKeyStoreSigner(alias)
        // The key the signer holds is the Keystore's, not the software one.
        assertEquals("AndroidKeyStore", store.provider.name)
        assertTrue(store.getKey(alias, null).javaClass.name.contains("keystore", ignoreCase = true))

        val viewer = PdfViewer(ViewerConfig(interaction = true, annotation = "", form = FormConfig(), signature = SignatureConfig()))
        viewer.start()
        viewer.setSigner(signer)
        viewer.setTrust(TrustAnchors.fromBytes(certificate.encoded))
        val signatures = viewer.signatures(viewer.open(DeviceFixture.bytes("unsigned_sigfield")))
        signatures.refresh()
        val field = signatures.unsignedFields().first().field
        val done = signatures.sign(SignatureSignInput(field = field, mark = SignatureMarkSource(DeviceFixture.bytes("signature_artwork"))))
        assertEquals(SignatureCompletionStatus.COMPLETED, done.status)
        val verdict = signatures.validate(field)
        Log.i(TAG, "imported key verdict: trust ${verdict.trust}, summary ${verdict.summary}")
        assertEquals(SignatureTrustStatus.TRUSTED, verdict.trust)
        assertEquals(SignatureSummary.VALID, verdict.summary)
        viewer.destroy()
    }

    private companion object {
        const val TAG = "EmbedPdfDevice"
    }
}
