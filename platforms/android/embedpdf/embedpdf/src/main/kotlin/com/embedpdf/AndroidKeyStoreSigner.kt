package com.embedpdf

import android.security.keystore.KeyPermanentlyInvalidatedException
import android.security.keystore.UserNotAuthenticatedException
import java.security.GeneralSecurityException
import java.security.KeyStore
import java.security.PrivateKey
import java.security.Signature
import java.security.cert.X509Certificate
import java.security.interfaces.ECKey
import java.util.concurrent.Executor
import java.util.concurrent.Executors

/**
 * A [SignatureSigner] over a private key that never leaves the Android
 * Keystore (or StrongBox), and its certificate chain. Build one from the
 * key's alias, or from a key and its certificates.
 *
 * An RSA key signs PKCS #1 v1.5 and an EC key ECDSA, both over SHA-256; an
 * ECDSA signature is handed to the engine as `r || s`, each padded to the
 * curve's size.
 *
 * A key that needs the user to authenticate (`setUserAuthenticationRequired`)
 * signs only after the app has authenticated them, with a biometric or
 * device-credential prompt of its own. Signing before that fails with
 * `permission-denied` (`UserNotAuthenticated`), and a key the system
 * invalidated (a new fingerprint, say) with `permission-denied`
 * (`KeyPermanentlyInvalidated`); neither crashes.
 */
public class AndroidKeyStoreSigner internal constructor(
    private val key: PrivateKey,
    certificates: List<X509Certificate>,
    /** The JCA signature; a test passes one that fails as a Keystore would. */
    private val signing: (PrivateKey, String, ByteArray) -> ByteArray,
) : SignatureSigner {
    private val chain: List<ByteArray> = certificates.map { it.encoded }

    private val keyAlgorithm: SignatureKeyAlgorithm = when (key.algorithm) {
        "EC" -> SignatureKeyAlgorithm.ECDSA
        "RSA" -> SignatureKeyAlgorithm.RSA_PKCS1
        else -> throw PdfException(PdfException.Code.INVALID_INPUT, "a ${key.algorithm} key can't sign a PDF", "UnreadableKey")
    }

    /** Bytes per ECDSA half: the curve order's size. */
    private val fieldSize: Int = ((key as? ECKey)?.params?.order?.bitLength() ?: certificates.firstOrNull()?.ecFieldBits() ?: 256)
        .let { (it + 7) / 8 }

    /**
     * The key and chain stored under [alias] in [keyStore], the Android
     * Keystore unless a test passes another. Throws `not-found` when the
     * alias holds no private key, and `operation-failed` when the store can't
     * be read.
     */
    public constructor(alias: String, keyStore: KeyStore = androidKeyStore()) :
        this(keyStore.privateKey(alias), keyStore.chain(alias), ::jcaSign)

    /** [privateKey] with [certificates], leaf first. */
    public constructor(privateKey: PrivateKey, certificates: List<X509Certificate>) : this(privateKey, certificates, ::jcaSign)

    override fun certificateChain(): List<ByteArray> = chain.map { it.copyOf() }

    override fun algorithm(): SignatureKeyAlgorithm = keyAlgorithm

    override fun digest(): SignatureDigest = SignatureDigest.SHA256

    /**
     * Called on the viewer's kernel thread, and returns at once: the signing
     * runs on a thread of its own, so the viewer keeps serving other calls
     * while the key works. A failure fails the signing with its code.
     */
    override fun sign(message: ByteArray, completion: SignatureSignCompletion) {
        executor.execute {
            val signature = try {
                sign(message)
            } catch (e: Exception) {
                completion.fail(e)
                return@execute
            }
            completion.finish(signature)
        }
    }

    /**
     * Signs [message] on the calling thread. Throws `permission-denied` for a
     * key that needs the user to authenticate first or that the system
     * invalidated, and `operation-failed` for any other failure.
     */
    public fun sign(message: ByteArray): ByteArray {
        val algorithm = if (keyAlgorithm == SignatureKeyAlgorithm.ECDSA) "SHA256withECDSA" else "SHA256withRSA"
        val signature = try {
            signing(key, algorithm, message)
        } catch (e: UserNotAuthenticatedException) {
            throw PdfException(
                PdfException.Code.PERMISSION_DENIED,
                "the key needs the user to authenticate first: ${readable(e)}",
                "UserNotAuthenticated",
                e,
            )
        } catch (e: KeyPermanentlyInvalidatedException) {
            throw PdfException(PdfException.Code.PERMISSION_DENIED, "the key is no longer valid: ${readable(e)}", "KeyPermanentlyInvalidated", e)
        } catch (e: GeneralSecurityException) {
            throw PdfException(PdfException.Code.OPERATION_FAILED, readable(e), "SigningError", e)
        } catch (e: RuntimeException) {
            // A provider that fails outside the checked exceptions, such as
            // a Keystore whose service is gone.
            throw PdfException(PdfException.Code.OPERATION_FAILED, readable(e), "SigningError", e)
        }
        return if (keyAlgorithm == SignatureKeyAlgorithm.ECDSA) rawEcdsa(signature, fieldSize) else signature
    }

    public companion object {
        /** The Android Keystore, loaded. */
        public fun androidKeyStore(): KeyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }

        private val executor: Executor = Executors.newCachedThreadPool { task ->
            Thread(task, "embedpdf-signer").apply { isDaemon = true }
        }

        private fun jcaSign(key: PrivateKey, algorithm: String, message: ByteArray): ByteArray =
            Signature.getInstance(algorithm).run {
                initSign(key)
                update(message)
                sign()
            }

        private fun KeyStore.privateKey(alias: String): PrivateKey {
            val key = try {
                getKey(alias, null)
            } catch (e: GeneralSecurityException) {
                throw PdfException(PdfException.Code.OPERATION_FAILED, readable(e), "UnreadableKey", e)
            }
            return key as? PrivateKey
                ?: throw PdfException(PdfException.Code.NOT_FOUND, "no private key under the alias $alias", "UnreadableKey")
        }

        private fun KeyStore.chain(alias: String): List<X509Certificate> = try {
            getCertificateChain(alias)?.filterIsInstance<X509Certificate>().orEmpty()
        } catch (e: GeneralSecurityException) {
            throw PdfException(PdfException.Code.OPERATION_FAILED, readable(e), "UnreadableKey", e)
        }

        private fun X509Certificate.ecFieldBits(): Int? = (publicKey as? ECKey)?.params?.order?.bitLength()

        /**
         * X9.62 `SEQUENCE { INTEGER r, INTEGER s }` to `r || s`, each
         * left-padded to [fieldSize] bytes, which is what
         * `epdf-signature-core` expects. Throws `operation-failed` for DER that
         * isn't such a sequence.
         */
        internal fun rawEcdsa(der: ByteArray, fieldSize: Int): ByteArray {
            val sequence = DerReader(der).take(0x30)
            val r = sequence.take(0x02).bytes().dropWhile { it == 0.toByte() }
            val s = sequence.take(0x02).bytes().dropWhile { it == 0.toByte() }
            if (r.size > fieldSize || s.size > fieldSize) throw malformedSignature()
            return ByteArray(fieldSize - r.size) + r.toByteArray() + ByteArray(fieldSize - s.size) + s.toByteArray()
        }

        internal fun malformedSignature(): PdfException =
            PdfException(PdfException.Code.OPERATION_FAILED, "the key's ECDSA signature isn't DER", "SigningError")
    }
}

/** A DER TLV reader: definite lengths only, which is all a signature uses. */
internal class DerReader(private val data: ByteArray, private var start: Int = 0, private val end: Int = data.size) {
    fun bytes(): List<Byte> = data.asList().subList(start, end)

    /** The value of the next element, which must carry [tag]. */
    fun take(tag: Int): DerReader {
        if (end - start < 2 || data[start].toInt() and 0xff != tag) throw AndroidKeyStoreSigner.malformedSignature()
        val first = data[start + 1].toInt() and 0xff
        var position = start + 2
        var length = first
        if (first and 0x80 != 0) {
            // 0x80 alone is BER's indefinite length, which DER forbids, and
            // no signature needs more than four length bytes.
            val count = first and 0x7f
            if (count !in 1..4) throw AndroidKeyStoreSigner.malformedSignature()
            var value = 0L
            repeat(count) {
                if (position >= end) throw AndroidKeyStoreSigner.malformedSignature()
                value = (value shl 8) or (data[position].toLong() and 0xff)
                position += 1
            }
            if (value > Int.MAX_VALUE) throw AndroidKeyStoreSigner.malformedSignature()
            length = value.toInt()
        }
        if (end - position < length) throw AndroidKeyStoreSigner.malformedSignature()
        start = position + length
        return DerReader(data, position, position + length)
    }
}
