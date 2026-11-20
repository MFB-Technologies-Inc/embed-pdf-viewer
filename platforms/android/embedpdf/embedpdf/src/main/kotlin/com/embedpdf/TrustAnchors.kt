package com.embedpdf

import android.content.res.AssetManager
import java.io.ByteArrayInputStream
import java.io.FileNotFoundException
import java.io.IOException
import java.security.cert.CertificateException
import java.security.cert.CertificateFactory
import java.security.cert.X509Certificate

/**
 * Trust anchors the app supplies: the roots validation trusts (its
 * organisation's CA, say). Pass it to [PdfViewer.setTrust]. Without anchors a
 * good signature reads [SignatureSummary.VALID_UNTRUSTED]; the system's own
 * roots aren't consulted.
 */
public class TrustAnchors(public val certificates: List<X509Certificate>) : SignatureTrust {
    override fun anchors(): List<ByteArray> = certificates.map { it.encoded }

    public companion object {
        /**
         * The certificates in the asset files at [paths], each PEM or DER (one
         * or more certificates a file). Throws `not-found` for a missing asset
         * and `invalid-input` for one that holds no certificate.
         */
        public fun fromAssets(assets: AssetManager, paths: List<String>): TrustAnchors =
            TrustAnchors(paths.flatMap { path -> certificates(assets, path) })

        /**
         * The certificates in [pem], one or more `BEGIN CERTIFICATE` blocks,
         * as Swift's `TrustAnchors` takes them. Throws `invalid-input` for
         * text that holds no certificate.
         */
        public fun fromPem(pem: String): TrustAnchors {
            // Each block on its own: a provider may stop after the first one.
            val blocks = pemBlock.findAll(pem).map { it.groupValues[1].filterNot(Char::isWhitespace) }.toList()
            if (blocks.isEmpty()) {
                throw PdfException(PdfException.Code.INVALID_INPUT, "the PEM text holds no certificate", "UnreadableCertificate")
            }
            return TrustAnchors(
                blocks.flatMap { body ->
                    val der = try {
                        java.util.Base64.getDecoder().decode(body)
                    } catch (e: IllegalArgumentException) {
                        throw PdfException(PdfException.Code.INVALID_INPUT, "the PEM text is not base64: ${readable(e)}", "UnreadableCertificate", e)
                    }
                    certificates("the PEM text") { ByteArrayInputStream(der) }
                },
            )
        }

        private val pemBlock = Regex("-----BEGIN CERTIFICATE-----([^-]*)-----END CERTIFICATE-----")

        /**
         * The certificates in [der], DER bytes (one certificate, or several
         * one after another). Throws `invalid-input` for bytes that hold no
         * certificate.
         */
        public fun fromBytes(der: ByteArray): TrustAnchors =
            TrustAnchors(certificates("the bytes") { ByteArrayInputStream(der) })

        private fun certificates(assets: AssetManager, path: String): List<X509Certificate> =
            certificates(path) { assets.open(path) }

        private fun certificates(source: String, open: () -> java.io.InputStream): List<X509Certificate> {
            val found = try {
                open().use { CertificateFactory.getInstance("X.509").generateCertificates(it) }
            } catch (e: FileNotFoundException) {
                throw PdfException(PdfException.Code.NOT_FOUND, "no asset $source", "FileNotFoundException", e)
            } catch (e: CertificateException) {
                throw PdfException(PdfException.Code.INVALID_INPUT, "$source is not a certificate: ${readable(e)}", "UnreadableCertificate", e)
            } catch (e: IOException) {
                throw PdfException(PdfException.Code.OPERATION_FAILED, readable(e), e.javaClass.simpleName, e)
            }
            val certificates = found.filterIsInstance<X509Certificate>()
            if (certificates.isEmpty()) {
                throw PdfException(PdfException.Code.INVALID_INPUT, "$source holds no certificate", "UnreadableCertificate")
            }
            return certificates
        }
    }
}
