package com.embedpdf

import kotlinx.coroutines.channels.Channel
import java.security.KeyFactory
import java.security.PrivateKey
import java.security.Signature
import java.security.spec.PKCS8EncodedKeySpec
import java.util.Base64

/** The base64 body of a PEM. */
fun pemBody(pem: String): ByteArray =
    Base64.getMimeDecoder().decode(pem.lines().filter { !it.startsWith("-----") }.joinToString(""))

/** The test-only key and certificate the Rust and Swift signature tests sign with. */
object TestKeys {
    val privateKey: PrivateKey by lazy {
        KeyFactory.getInstance("RSA").generatePrivate(PKCS8EncodedKeySpec(pemBody(Fixture.file("TEST-ONLY-private-key", "pem").readText())))
    }

    /** The signer's certificate, DER. */
    val certificate: ByteArray by lazy { pemBody(Fixture.file("TEST-ONLY-signer", "pem").readText()) }
}

/** Signs with [TestKeys] on a thread of its own, as a key behind a prompt would. */
class TestSigner : SignatureSigner {
    override fun certificateChain(): List<ByteArray> = listOf(TestKeys.certificate)

    override fun algorithm(): SignatureKeyAlgorithm = SignatureKeyAlgorithm.RSA_PKCS1

    override fun digest(): SignatureDigest = SignatureDigest.SHA256

    override fun sign(message: ByteArray, completion: SignatureSignCompletion) {
        Thread { completion.finish(sign(message)) }.start()
    }

    fun sign(message: ByteArray): ByteArray =
        Signature.getInstance("SHA256withRSA").apply {
            initSign(TestKeys.privateKey)
            update(message)
        }.sign()
}

/** A signer whose prompt the user cancelled. */
class CancellingSigner : SignatureSigner {
    override fun certificateChain(): List<ByteArray> = listOf(TestKeys.certificate)

    override fun algorithm(): SignatureKeyAlgorithm = SignatureKeyAlgorithm.RSA_PKCS1

    override fun digest(): SignatureDigest = SignatureDigest.SHA256

    override fun sign(message: ByteArray, completion: SignatureSignCompletion) {
        completion.fail(PdfException(PdfException.Code.OPERATION_CANCELLED, "the user cancelled", "UserCancelled"))
    }
}

/**
 * A signer that holds each signing, as a prompt waiting on the user does,
 * until the test answers it from [held].
 */
class HeldSigner(private val signer: TestSigner) : SignatureSigner {
    val held: Channel<Pair<ByteArray, SignatureSignCompletion>> = Channel(Channel.UNLIMITED)

    override fun certificateChain(): List<ByteArray> = signer.certificateChain()

    override fun algorithm(): SignatureKeyAlgorithm = signer.algorithm()

    override fun digest(): SignatureDigest = signer.digest()

    override fun sign(message: ByteArray, completion: SignatureSignCompletion) {
        held.trySend(message to completion)
    }
}
