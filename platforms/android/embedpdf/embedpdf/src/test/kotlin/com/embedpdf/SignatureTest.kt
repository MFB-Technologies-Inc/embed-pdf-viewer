package com.embedpdf

import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.async
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeout
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class SignatureTest {
    data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val signatures: SignatureCapability)

    private suspend fun opened(fixture: String, signer: Boolean = true, mode: SignatureMode? = null): Opened {
        val viewer = started(
            ViewerConfig(interaction = true, annotation = "", form = FormConfig(), signature = SignatureConfig(mode = mode)),
        )
        if (signer) viewer.setSigner(TestSigner())
        val document = viewer.open(Fixture.bytes(fixture))
        val signatures = viewer.signatures(document)
        signatures.refresh()
        return Opened(viewer, document, signatures)
    }

    private suspend fun unsignedField(signatures: SignatureCapability): FormFieldRef = signatures.unsignedFields().first().field

    /**
     * `sign`'s input with the artwork as its mark: the appearance the engine
     * draws into the widget. A signing without one is refused by the engine.
     */
    private fun signInput(field: FormFieldRef): SignatureSignInput =
        SignatureSignInput(field = field, mark = SignatureMarkSource(Fixture.bytes("signature_artwork")))

    @Test
    fun twoApprovalsAreListed() = viewerTest {
        val (viewer, _, signatures) = opened("signed_two_approvals")
        assertEquals(2, signatures.signatures().size)
        assertEquals(SignatureModificationLevel.ANNOTATE, signatures.protection()?.judged)
        assertEquals(3, signatures.snapshot()?.revisions?.size)
        viewer.destroy()
    }

    @Test
    fun aCertificationIsNamed() = viewerTest {
        val (viewer, _, signatures) = opened("signed_certified")
        assertEquals(2.toUByte(), signatures.protection()?.certification?.permission?.toUByte())
        viewer.destroy()
    }

    @Test
    fun aFieldSignsAndValidatesUntrusted() = viewerTest {
        val (viewer, _, signatures) = opened("unsigned_sigfield")
        val field = unsignedField(signatures)
        val done = signatures.sign(signInput(field))
        assertTrue(done.signature.signed)
        assertEquals(SignatureCompletionStatus.COMPLETED, done.status)
        val verdict = signatures.validate(field)
        assertEquals(SignatureIntegrity.VALID, verdict.integrity)
        assertEquals(SignatureSummary.VALID_UNTRUSTED, verdict.summary)
        viewer.destroy()
    }

    @Test
    fun withoutAnchorsASignatureIsValidUntrusted() = viewerTest {
        val (viewer, _, signatures) = opened("signed_two_approvals")
        val verdicts = signatures.validate()
        assertFalse(verdicts.isEmpty())
        assertTrue(verdicts.all { it.summary != SignatureSummary.VALID })
        viewer.destroy()
    }

    @Test
    fun theTestCertificateAsAnAnchorIsTrusted() = viewerTest {
        val (viewer, _, signatures) = opened("unsigned_sigfield")
        viewer.setTrust(
            object : SignatureTrust {
                override fun anchors(): List<ByteArray> = listOf(TestKeys.certificate)
            },
        )
        val field = unsignedField(signatures)
        signatures.sign(signInput(field))
        val verdict = signatures.validate(field)
        assertEquals(SignatureTrustStatus.TRUSTED, verdict.trust)
        assertEquals(SignatureSummary.VALID, verdict.summary)
        viewer.destroy()
    }

    @Test
    fun aCancelledSignerLeavesNothingPending() = viewerTest {
        val (viewer, _, signatures) = opened("unsigned_sigfield", signer = false)
        viewer.setSigner(CancellingSigner())
        val field = unsignedField(signatures)
        expectCode(PdfException.Code.OPERATION_CANCELLED) { signatures.sign(signInput(field)) }
        assertNull(signatures.pending())
        assertFalse(signatures.isBusy())
        viewer.destroy()
    }

    @Test
    fun aVisualFillNeedsNoSigner() = viewerTest {
        // The host's key is a function over the viewer's signer, so the mode
        // is `SIGN` unless the config asks for `VISUAL`.
        val (viewer, _, signatures) = opened("unsigned_sigfield", signer = false, mode = SignatureMode.VISUAL)
        assertEquals(SignatureMode.VISUAL, signatures.mode())
        // Without a signer the field still may be filled with a mark.
        val field = unsignedField(signatures)
        assertTrue(signatures.canFill(field))
        signatures.fill(field, SignatureMarkSource(Fixture.bytes("signature_artwork")))
        signatures.clearField(field)
        viewer.destroy()
    }

    @Test
    fun settingASignerTurnsSigningOn() = viewerTest {
        val (viewer, _, signatures) = opened("unsigned_sigfield", signer = false)
        // `canSign(field)` is the session's permission (`doc.sign`); the
        // signer decides the mode. Without one a signing is refused
        // `invalid-input`, with one it seals.
        val field = unsignedField(signatures)
        assertEquals(SignatureMode.SIGN, signatures.mode())
        expectCode(PdfException.Code.INVALID_INPUT) { signatures.sign(signInput(field)) }
        viewer.setSigner(TestSigner())
        assertTrue(signatures.canSign(field))
        assertEquals(SignatureCompletionStatus.COMPLETED, signatures.sign(signInput(field)).status)
        viewer.destroy()
    }

    @Test
    fun aStampMarkFillsAField() = viewerTest {
        val viewer = started(
            ViewerConfig(interaction = true, annotation = "", form = FormConfig(), stamp = StampConfig(), signature = SignatureConfig()),
        )
        val stamps = viewer.stamps()
        val library = stamps.importLibrary(
            Fixture.bytes("signature_artwork"),
            StampImportOptions(kind = StampAssetKind.SIGNATURE, libraryKind = "signatures"),
        )
        val asset = stamps.assets(StampAssetFilter(libraryId = library)).first()
        val document = viewer.open(Fixture.bytes("unsigned_sigfield"))
        val signatures = viewer.signatures(document)
        signatures.refresh()
        signatures.fill(unsignedField(signatures), SignatureMarkAsset(asset.id))
        viewer.destroy()
    }

    @Test
    fun aFreeMarkIsAStamp() = viewerTest {
        val (viewer, document, signatures) = opened("unsigned_sigfield", signer = false)
        val page = viewer.pages(document)[0].page
        val result = signatures.place(
            SignatureMarkSource(Fixture.bytes("signature_artwork")),
            SignaturePlaceTargetFree(page = page, at = Point(x = 50.0, y = 50.0), targetWidth = null, rotation = null),
        )
        assertTrue("expected a placed annotation, got $result", result is SignaturePlaceResultPlaced)
        viewer.destroy()
    }

    /** The companion refusals: a free asset mark needs stamp, a free source mark needs annotation. */
    @Test
    fun aFreeMarkNeedsItsCompanionPlugin() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, form = FormConfig(), signature = SignatureConfig()))
        val document = viewer.open(Fixture.bytes("unsigned_sigfield"))
        val signatures = viewer.signatures(document)
        val page = viewer.pages(document)[0].page
        val free = SignaturePlaceTargetFree(page = page, at = Point(x = 50.0, y = 50.0), targetWidth = null, rotation = null)
        expectCode(PdfException.Code.UNSUPPORTED) { signatures.place(SignatureMarkAsset("any"), free) }
        expectCode(PdfException.Code.UNSUPPORTED) { signatures.place(SignatureMarkSource(Fixture.bytes("signature_artwork")), free) }
        viewer.destroy()
    }

    @Test
    fun aTwoPhaseSigningParksThenAborts() = viewerTest {
        val (viewer, _, signatures) = opened("unsigned_sigfield", signer = false)
        val field = unsignedField(signatures)
        val prepared = signatures.prepareSignature(SignaturePrepareInput(field = field))
        assertEquals(32, prepared.digest.size)
        assertEquals(prepared.signingId, signatures.pending()?.signingId)
        signatures.cancelPending()
        assertNull(signatures.pending())
        viewer.destroy()
    }

    @Test
    fun aChangeAfterTheSignatureIsAnalyzed() = viewerTest {
        val (viewer, _, signatures) = opened("signed_two_approvals")
        val analysis = signatures.analyzeChanges(since = 0, until = SignatureModificationsBasis.PERSISTED)
        assertEquals(1, analysis.laterRevisions.toInt())
        assertEquals(SignatureModificationsVerdict.PERMITTED, analysis.verdict)
        val revision = signatures.readRevision(signatures.signatures().first().field)
        assertEquals("%PDF-", String(revision.copyOfRange(0, 5), Charsets.US_ASCII))
        expectCode(PdfException.Code.INVALID_INPUT) { signatures.analyzeChanges(since = -1) }
        viewer.destroy()
    }

    @Test
    fun settingTheTargetIsObserved() = viewerTest {
        val (viewer, _, signatures) = opened("unsigned_sigfield")
        val field = unsignedField(signatures)
        val events = subscribed(signatures.observeEvents())
        signatures.setTarget(field)
        assertEquals(field, signatures.target())
        assertEquals(field, events.nextMapped { it as? SignatureEventTargetChanged }.field)
        viewer.destroy()
    }

    @Test
    fun aClosedDocumentsCapabilityThrows() = viewerTest {
        val (viewer, document, signatures) = opened("signed_two_approvals")
        viewer.close(document)
        val error = expectCode(PdfException.Code.INSTANCE_CLOSED) { signatures.snapshot() }
        assertEquals("PluginError", error.name)
        viewer.destroy()
    }

    @Test
    fun aPendingSignatureDoesNotBlockTheViewer() = viewerTest {
        val (viewer, document, signatures) = opened("unsigned_sigfield", signer = false)
        val signer = TestSigner()
        val held = HeldSigner(signer)
        viewer.setSigner(held)
        val field = unsignedField(signatures)
        val signing = async { signatures.sign(signInput(field)) }
        val (message, completion) = withTimeout(10_000) { held.held.receive() }
        // The signer has not answered, and the viewer still serves calls.
        assertEquals(1, viewer.pages(document).size)
        assertEquals(ResourceStatus.READY, signatures.status())
        completion.finish(signer.sign(message))
        assertTrue(signing.await().signature.signed)
        viewer.destroy()
    }

    @Test
    fun aSignerNamedByTheCallSigns() = viewerTest {
        val (viewer, _, signatures) = opened("unsigned_sigfield", signer = false)
        val done = signatures.sign(signInput(unsignedField(signatures)), TestSigner())
        assertEquals(SignatureCompletionStatus.COMPLETED, done.status)
        viewer.destroy()
    }

    @Test
    fun droppingTheStreamUnsubscribes() = viewerTest {
        val (viewer, _, signatures) = opened("signed_two_approvals")
        val job = launch(start = CoroutineStart.UNDISPATCHED) { signatures.observeEvents().collect {} }
        delay(20)
        job.cancel()
        job.join()
        signatures.refresh()
        viewer.destroy()
    }

    // One per member that takes a `CancelToken`, each called in an
    // already-cancelled coroutine; the capability stays usable.

    private suspend fun cancels(signatures: SignatureCapability, body: suspend () -> Any?) {
        expectCancelledBeforeItStarts(body)
        signatures.status()
    }

    @Test
    fun refreshCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, signatures) = opened("signed_two_approvals")
        cancels(signatures) { signatures.refresh() }
        viewer.destroy()
    }

    @Test
    fun validateCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, signatures) = opened("signed_two_approvals")
        cancels(signatures) { signatures.validate() }
        viewer.destroy()
    }

    @Test
    fun validateFieldCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, signatures) = opened("signed_two_approvals")
        val field = signatures.signatures().first().field
        cancels(signatures) { signatures.validate(field) }
        viewer.destroy()
    }

    @Test
    fun analyzeChangesCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, signatures) = opened("signed_two_approvals")
        cancels(signatures) { signatures.analyzeChanges(since = 0) }
        viewer.destroy()
    }

    @Test
    fun readRevisionCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, signatures) = opened("signed_two_approvals")
        val field = signatures.signatures().first().field
        cancels(signatures) { signatures.readRevision(field) }
        viewer.destroy()
    }

    @Test
    fun signCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, signatures) = opened("unsigned_sigfield")
        val field = unsignedField(signatures)
        cancels(signatures) { signatures.sign(signInput(field)) }
        viewer.destroy()
    }

    @Test
    fun prepareSignatureCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, signatures) = opened("unsigned_sigfield")
        val field = unsignedField(signatures)
        cancels(signatures) { signatures.prepareSignature(SignaturePrepareInput(field = field)) }
        viewer.destroy()
    }

    @Test
    fun completeSignatureCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, signatures) = opened("unsigned_sigfield")
        cancels(signatures) { signatures.completeSignature("none", byteArrayOf(0x30, 0)) }
        viewer.destroy()
    }

    @Test
    fun cancelPendingCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, signatures) = opened("unsigned_sigfield")
        cancels(signatures) { signatures.cancelPending() }
        viewer.destroy()
    }

    @Test
    fun fillCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, signatures) = opened("unsigned_sigfield", signer = false)
        val field = unsignedField(signatures)
        val artwork = Fixture.bytes("signature_artwork")
        cancels(signatures) { signatures.fill(field, SignatureMarkSource(artwork)) }
        viewer.destroy()
    }

    @Test
    fun clearFieldCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, signatures) = opened("unsigned_sigfield", signer = false)
        val field = unsignedField(signatures)
        cancels(signatures) { signatures.clearField(field) }
        viewer.destroy()
    }

    @Test
    fun placeCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, document, signatures) = opened("unsigned_sigfield", signer = false)
        val page = viewer.pages(document)[0].page
        val artwork = Fixture.bytes("signature_artwork")
        cancels(signatures) {
            signatures.place(
                SignatureMarkSource(artwork),
                SignaturePlaceTargetFree(page = page, at = Point(x = 50.0, y = 50.0), targetWidth = null, rotation = null),
            )
        }
        viewer.destroy()
    }

    @Test
    fun addValidationInfoCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, signatures) = opened("signed_two_approvals")
        cancels(signatures) { signatures.addValidationInfo() }
        viewer.destroy()
    }

    /** `SecurityTests.aNarrowScopeCannotSign`. */
    @Test
    fun aNarrowScopeCannotSign() = viewerTest {
        // The field's ref, read where the session may read the form: refs
        // are object numbers, the same in every viewer of the file.
        val (full, _, readable) = opened("unsigned_sigfield")
        val field = unsignedField(readable)
        assertTrue(readable.canSign(field))
        full.destroy()
        val viewer = started(
            ViewerConfig(scope = listOf("doc.open", "doc.render"), interaction = true, form = FormConfig(), signature = SignatureConfig()),
        )
        viewer.setSigner(TestSigner())
        val signatures = viewer.signatures(viewer.open(Fixture.bytes("unsigned_sigfield")))
        assertFalse(signatures.canSign(field))
        assertFalse(signatures.canFill(field))
        viewer.destroy()
    }
}
