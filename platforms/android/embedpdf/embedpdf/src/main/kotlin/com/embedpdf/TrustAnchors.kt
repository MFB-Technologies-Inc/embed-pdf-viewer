package com.embedpdf

import android.content.res.AssetManager
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

        private fun certificates(assets: AssetManager, path: String): List<X509Certificate> {
            val found = try {
                assets.open(path).use { CertificateFactory.getInstance("X.509").generateCertificates(it) }
            } catch (e: FileNotFoundException) {
                throw PdfException(PdfException.Code.NOT_FOUND, "no asset $path", "FileNotFoundException", e)
            } catch (e: CertificateException) {
                throw PdfException(PdfException.Code.INVALID_INPUT, "$path is not a certificate: ${readable(e)}", "UnreadableCertificate", e)
            } catch (e: IOException) {
                throw PdfException(PdfException.Code.OPERATION_FAILED, readable(e), e.javaClass.simpleName, e)
            }
            val certificates = found.filterIsInstance<X509Certificate>()
            if (certificates.isEmpty()) {
                throw PdfException(PdfException.Code.INVALID_INPUT, "$path holds no certificate", "UnreadableCertificate")
            }
            return certificates
        }
    }
}
