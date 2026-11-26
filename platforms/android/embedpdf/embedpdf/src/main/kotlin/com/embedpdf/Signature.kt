package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_signature.SignatureEventListener
import uniffi.epdf_plugin_signature.SignatureHandle

public typealias SignatureSettings = uniffi.epdf_plugin_signature.SignatureSettings
public typealias SignatureSettingsPatch = uniffi.epdf_plugin_signature.SignatureSettingsPatch
public typealias SignatureConfig = uniffi.epdf_plugin_signature.SignatureConfig
public typealias SignatureMode = uniffi.epdf_plugin_signature.SignatureMode
public typealias SignatureSnapshot = uniffi.epdf_plugin_signature.SignatureSnapshot
public typealias SignatureRevision = uniffi.epdf_plugin_signature.SignatureRevision
public typealias SignatureInfo = uniffi.epdf_plugin_signature.SignatureInfo
public typealias SignatureWidget = uniffi.epdf_plugin_signature.SignatureWidget
public typealias SignatureKind = uniffi.epdf_plugin_signature.SignatureKind
public typealias SignatureCoverage = uniffi.epdf_plugin_signature.SignatureCoverage
public typealias SignatureSignerClaims = uniffi.epdf_plugin_signature.SignatureSignerClaims
public typealias SignatureSeedValue = uniffi.epdf_plugin_signature.SignatureSeedValue
public typealias SignatureFieldLock = uniffi.epdf_plugin_signature.SignatureFieldLock
public typealias SignatureFieldLockAction = uniffi.epdf_plugin_signature.SignatureFieldLockAction
public typealias SignatureModificationLevel = uniffi.epdf_plugin_signature.SignatureModificationLevel
public typealias DocumentProtection = uniffi.epdf_plugin_signature.DocumentProtection
public typealias DocumentCertification = uniffi.epdf_plugin_signature.DocumentCertification
public typealias DocumentFieldLock = uniffi.epdf_plugin_signature.DocumentFieldLock
public typealias DocumentFieldLockSource = uniffi.epdf_plugin_signature.DocumentFieldLockSource
public typealias SignaturePending = uniffi.epdf_plugin_signature.SignaturePending
public typealias SignatureVerdict = uniffi.epdf_plugin_signature.SignatureVerdict
public typealias SignatureIntegrity = uniffi.epdf_plugin_signature.SignatureIntegrity
public typealias SignatureCryptography = uniffi.epdf_plugin_signature.SignatureCryptography
public typealias SignatureTrustStatus = uniffi.epdf_plugin_signature.SignatureTrustStatus
public typealias SignatureValidationTime = uniffi.epdf_plugin_signature.SignatureValidationTime
public typealias SignatureValidationTimeNow = uniffi.epdf_plugin_signature.SignatureValidationTime.Now
public typealias SignatureValidationTimeTimestamp = uniffi.epdf_plugin_signature.SignatureValidationTime.Timestamp
/**
 * What [SignatureValidateRequest.validationTime] asks for:
 * [SignatureRequestedTimeEarliestTimestamp] (the default),
 * [SignatureRequestedTimeNow], or a token's time. A verdict's own time stays
 * a [SignatureValidationTime].
 */
public typealias SignatureRequestedTime = uniffi.epdf_plugin_signature.SignatureRequestedTime
public typealias SignatureRequestedTimeEarliestTimestamp = uniffi.epdf_plugin_signature.SignatureRequestedTime.EarliestTimestamp
public typealias SignatureRequestedTimeNow = uniffi.epdf_plugin_signature.SignatureRequestedTime.Now
public typealias SignatureRequestedTimeTimestamp = uniffi.epdf_plugin_signature.SignatureRequestedTime.Timestamp
public typealias SignatureModifications = uniffi.epdf_plugin_signature.SignatureModifications
public typealias SignatureModificationsVerdict = uniffi.epdf_plugin_signature.SignatureModificationsVerdict
public typealias SignatureModificationsBasis = uniffi.epdf_plugin_signature.SignatureModificationsBasis
public typealias SignatureSummary = uniffi.epdf_plugin_signature.SignatureSummary
public typealias SignatureValidateRequest = uniffi.epdf_plugin_signature.SignatureValidateRequest
public typealias SignatureChangeAnalysis = uniffi.epdf_plugin_signature.SignatureChangeAnalysis
public typealias SignatureChangeFinding = uniffi.epdf_plugin_signature.SignatureChangeFinding
public typealias SignatureFindingVerdict = uniffi.epdf_plugin_signature.SignatureFindingVerdict
public typealias SignatureMark = uniffi.epdf_plugin_signature.SignatureMark
public typealias SignatureMarkAsset = uniffi.epdf_plugin_signature.SignatureMark.Asset
public typealias SignatureMarkSource = uniffi.epdf_plugin_signature.SignatureMark.Source
public typealias SignatureSignerInput = uniffi.epdf_plugin_signature.SignatureSignerInput
public typealias SignatureSignInput = uniffi.epdf_plugin_signature.SignatureSignInput
public typealias SignaturePrepareInput = uniffi.epdf_plugin_signature.SignaturePrepareInput
public typealias SignaturePrepared = uniffi.epdf_plugin_signature.SignaturePrepared
public typealias SignatureVersionRef = uniffi.epdf_plugin_signature.SignatureVersionRef
public typealias SignatureBaseVersion = uniffi.epdf_plugin_signature.SignatureBaseVersion
public typealias SignatureCompletion = uniffi.epdf_plugin_signature.SignatureCompletion
public typealias SignatureCompletionStatus = uniffi.epdf_plugin_signature.SignatureCompletionStatus
public typealias SignaturePlaceTarget = uniffi.epdf_plugin_signature.SignaturePlaceTarget
public typealias SignaturePlaceTargetField = uniffi.epdf_plugin_signature.SignaturePlaceTarget.Field
public typealias SignaturePlaceTargetFree = uniffi.epdf_plugin_signature.SignaturePlaceTarget.Free
public typealias SignaturePlaceResult = uniffi.epdf_plugin_signature.SignaturePlaceResult
public typealias SignaturePlaceResultSigned = uniffi.epdf_plugin_signature.SignaturePlaceResult.Signed
public typealias SignaturePlaceResultFilled = uniffi.epdf_plugin_signature.SignaturePlaceResult.Filled
public typealias SignaturePlaceResultRequested = uniffi.epdf_plugin_signature.SignaturePlaceResult.Requested
public typealias SignaturePlaceResultPlaced = uniffi.epdf_plugin_signature.SignaturePlaceResult.Placed
public typealias SignatureEvent = uniffi.epdf_plugin_signature.SignatureEvent
public typealias SignatureEventSigned = uniffi.epdf_plugin_signature.SignatureEvent.Signed
public typealias SignatureEventFilled = uniffi.epdf_plugin_signature.SignatureEvent.Filled
public typealias SignatureEventCleared = uniffi.epdf_plugin_signature.SignatureEvent.Cleared
public typealias SignatureEventValidated = uniffi.epdf_plugin_signature.SignatureEvent.Validated
public typealias SignatureEventProtectionChanged = uniffi.epdf_plugin_signature.SignatureEvent.ProtectionChanged
public typealias SignatureEventInvalidationPredicted = uniffi.epdf_plugin_signature.SignatureEvent.InvalidationPredicted
public typealias SignatureEventTargetChanged = uniffi.epdf_plugin_signature.SignatureEvent.TargetChanged
public typealias SignatureEventSignRequested = uniffi.epdf_plugin_signature.SignatureEvent.SignRequested
public typealias SignatureEventInspectionRequested = uniffi.epdf_plugin_signature.SignatureEvent.InspectionRequested
public typealias SignatureKeyAlgorithm = uniffi.epdf_plugin_signature.SignatureKeyAlgorithm
public typealias SignatureDigest = uniffi.epdf_plugin_signature.SignatureDigest
public typealias SignatureRevocationStatus = uniffi.epdf_plugin_signature.SignatureRevocationStatus
public typealias SignatureTimestampVerdict = uniffi.epdf_plugin_signature.SignatureTimestampVerdict
public typealias SignatureLongTermVerdict = uniffi.epdf_plugin_signature.SignatureLongTermVerdict
public typealias SignatureLongTermGap = uniffi.epdf_plugin_signature.SignatureLongTermGap
public typealias SignatureLongTermGapReason = uniffi.epdf_plugin_signature.SignatureLongTermGapReason
public typealias SignatureValidationInfoAdded = uniffi.epdf_plugin_signature.SignatureValidationInfoAdded
public typealias SignatureValidationInfoCounts = uniffi.epdf_plugin_signature.SignatureValidationInfoCounts
public typealias SignatureFailure = uniffi.epdf_plugin_signature.SignatureFailure
/**
 * A call's own timestamp authority, over the settings' one:
 * [SignatureCallAuthorityWithout] for none, [SignatureCallAuthorityHttp] for
 * another.
 */
public typealias SignatureCallAuthority = uniffi.epdf_plugin_signature.SignatureCallAuthority
public typealias SignatureCallAuthorityWithout = uniffi.epdf_plugin_signature.SignatureCallAuthority.Without
public typealias SignatureCallAuthorityHttp = uniffi.epdf_plugin_signature.SignatureCallAuthority.Http
/** A timestamp authority reached over the viewer's transport ([PdfViewer.setSignatureTransport]). */
public typealias SignatureTimestampAuthority = uniffi.epdf_plugin_signature.SignatureTimestampAuthority
/** The app's revocation route, reached over the viewer's transport. */
public typealias SignatureRevocationRoute = uniffi.epdf_plugin_signature.SignatureRevocationRoute
public typealias SignatureTransportRequest = uniffi.epdf_plugin_signature.SignatureTransportRequest
public typealias SignatureTransportHeader = uniffi.epdf_plugin_signature.SignatureTransportHeader

/**
 * Seals a signature: its certificate chain, its key's algorithm and digest,
 * and `sign(message, completion)`, which answers through
 * [SignatureSignCompletion] from any thread. Every member is called on the
 * viewer's kernel thread; `sign` returns at once and signs elsewhere, so a
 * prompt waiting on the user doesn't hold the viewer. A synchronous member
 * that fails throws [ViewerExceptionKernel]. [AndroidKeyStoreSigner] is the
 * one this package ships.
 */
public typealias SignatureSigner = uniffi.epdf_plugin_signature.SignatureSigner
public typealias SignatureSignCompletion = uniffi.epdf_plugin_signature.SignatureSignCompletion

/**
 * The anchors validation trusts, DER. Called on the kernel thread.
 * [TrustAnchors] is the one this package ships.
 */
public typealias SignatureTrust = uniffi.epdf_plugin_signature.SignatureTrust

/**
 * The viewer's HTTP stack for timestamps and revocation answers. `post` is
 * called on the kernel thread, returns at once and answers through its
 * [SignatureTransportCompletion] from any thread. [HttpSignatureTransport] is
 * the one this package ships.
 */
public typealias SignatureTransport = uniffi.epdf_plugin_signature.SignatureTransport
public typealias SignatureTransportCompletion = uniffi.epdf_plugin_signature.SignatureTransportCompletion

/**
 * What a [SignatureSigner]'s synchronous members or a [SignatureTrust]
 * throws: [ViewerExceptionKernel] with a kernel code.
 */
public typealias ViewerException = uniffi.epdf_ffi.ViewerException
public typealias ViewerExceptionKernel = uniffi.epdf_ffi.ViewerException.Kernel

/**
 * Fails the signing with [error], keeping its code (see [HostFailure]): a
 * cancelled prompt's `operation-cancelled` is what
 * [SignatureCapability.sign] throws.
 */
public fun SignatureSignCompletion.fail(error: Throwable) {
    failWith(HostFailure(error))
}

/**
 * A document's signature fields: reading, validating, signing and filling
 * them. Get one with [PdfViewer.signatures]; the viewer needs
 * `ViewerConfig(interaction = true, form = FormConfig(), signature = SignatureConfig())`.
 * Signing uses the signer from [PdfViewer.setSigner] unless a call names one.
 * It keeps the viewer alive. After its document closes, every call throws
 * `instance-closed`.
 *
 * [place] with a [SignaturePlaceTargetFree] target needs the stamp plugin for
 * a [SignatureMarkAsset] mark and the annotation plugin for a
 * [SignatureMarkSource] mark; without it the call throws `unsupported`.
 *
 * A signing, fill, clear, placement or validation-info fill that started
 * lands even when the caller is cancelled, and the caller then sees its
 * cancellation rather than the result (see the README's Cancellation
 * section). Read [pending] and [signatures] again after a cancelled one.
 */
public class SignatureCapability internal constructor(
    private val handle: SignatureHandle,
    internal val viewer: PdfViewer,
) {
    /** Current settings shared by every document using this plugin registration. */
    public suspend fun settings(): SignatureSettings = viewer.call { handle.settings() }

    /** Merges the supplied fields. Clear flags remove nullable values; omission preserves them. */
    public suspend fun updateSettings(patch: SignatureSettingsPatch) {
        viewer.call { handle.updateSettings(patch) }
    }

    /** Restores the values supplied at registration, preserving registered host resources. */
    public suspend fun resetSettings() {
        viewer.call { handle.resetSettings() }
    }

    public suspend fun canReadRevision(): Boolean = viewer.call { handle.canReadRevision() }

    /** The last snapshot read, `null` before the first. */
    public suspend fun snapshot(): SignatureSnapshot? = viewer.call { handle.snapshot() }

    public suspend fun status(): ResourceStatus = viewer.call { handle.status() }

    /** The signed fields. */
    public suspend fun signatures(): List<SignatureInfo> = viewer.call { handle.signatures() }

    /** The signature fields nobody has signed. */
    public suspend fun unsignedFields(): List<SignatureInfo> = viewer.call { handle.unsignedFields() }

    public suspend fun signature(field: FormFieldRef): SignatureInfo? = viewer.call { handle.signature(field) }

    /** The last verdicts, `null` before the first validation. */
    public suspend fun verdicts(): List<SignatureVerdict>? = viewer.call { handle.verdicts() }

    public suspend fun verdict(field: FormFieldRef): SignatureVerdict? = viewer.call { handle.verdict(field) }

    public suspend fun protection(): DocumentProtection? = viewer.call { handle.protection() }

    /** A parked two-phase signing: the document is read-only until it completes or is aborted. */
    public suspend fun pending(): SignaturePending? = viewer.call { handle.pending() }

    /** A sign or fill is in flight. */
    public suspend fun isBusy(): Boolean = viewer.call { handle.isBusy() }

    /**
     * What placing a mark on a field does: [SignatureMode.SIGN] while a
     * signer is set, else [SignatureMode.VISUAL], unless the config names a
     * mode.
     */
    public suspend fun mode(): SignatureMode = viewer.call { handle.mode() }

    /** The field the next picked mark goes to. */
    public suspend fun target(): FormFieldRef? = viewer.call { handle.target() }

    /**
     * Whether signing [field] is allowed: `doc.sign`, or `fields:sign` for its
     * group. `false` for a field the form doesn't have.
     */
    public suspend fun canSign(field: FormFieldRef): Boolean = viewer.call { handle.canSign(field) }

    /** Whether drawing a mark into [field] is allowed: `doc.forms.fill`, or `fields:fill` for its group. */
    public suspend fun canFill(field: FormFieldRef): Boolean = viewer.call { handle.canFill(field) }

    /** The config allows certifying and `doc.sign.certify` is granted. */
    public suspend fun canCertify(): Boolean = viewer.call { handle.canCertify() }

    /** Re-reads the snapshot. `null` when the engine has no signatures. Cancelling the calling coroutine cancels the read. */
    public suspend fun refresh(): SignatureSnapshot? = viewer.callCancellable { token -> handle.refresh(token) }

    /**
     * Judges every signature. Without trust anchors a good one tops out at
     * [SignatureSummary.VALID_UNTRUSTED]. Cancelling the calling coroutine
     * cancels the validation.
     */
    public suspend fun validate(request: SignatureValidateRequest = SignatureValidateRequest()): List<SignatureVerdict> =
        viewer.callCancellable { token -> handle.validate(request, token) }

    /** Judges one signature. Cancelling the calling coroutine cancels the validation. */
    public suspend fun validate(field: FormFieldRef, request: SignatureValidateRequest = SignatureValidateRequest()): SignatureVerdict =
        viewer.callCancellable { token -> handle.validateField(field, request, token) }

    /**
     * What changed after signature [since] (its index), up to the saved bytes
     * or the working copy. An index below zero throws `invalid-input`.
     */
    public suspend fun analyzeChanges(
        since: Int,
        until: SignatureModificationsBasis = SignatureModificationsBasis.WORKING_COPY,
    ): SignatureChangeAnalysis {
        val wire = since.toWireUInt("signature index")
        return viewer.callCancellable { token -> handle.analyzeChanges(wire, until, token) }
    }

    /** The revision the signature on [field] seals: a complete PDF of its own. */
    public suspend fun readRevision(field: FormFieldRef): ByteArray = viewer.callCancellable { token -> handle.readRevision(field, token) }

    /** Names the field the next picked mark goes to; `null` clears. */
    public suspend fun setTarget(field: FormFieldRef?) {
        viewer.call { handle.setTarget(field) }
    }

    /** Asks the app to show a signed field's facts ([SignatureEventInspectionRequested]). */
    public suspend fun requestInspection(field: FormFieldRef) {
        viewer.call { handle.requestInspection(field) }
    }

    /**
     * Seals `input.field` with [signer], or the viewer's. Fails with the
     * signer's own error when it fails, and leaves nothing pending.
     */
    public suspend fun sign(input: SignatureSignInput, signer: SignatureSigner? = null): SignatureCompletion =
        viewer.callCancellable { token -> handle.sign(input, signer, token) }

    /** Phase one of an external signing: the digest to sign elsewhere. */
    public suspend fun prepareSignature(input: SignaturePrepareInput): SignaturePrepared =
        viewer.callCancellable { token -> handle.prepareSignature(input, token) }

    /** Phase two: seals the parked signing with a detached CMS. */
    public suspend fun completeSignature(signingId: String, cms: ByteArray): SignatureCompletion =
        viewer.callCancellable { token -> handle.completeSignature(signingId, cms, token) }

    /** Gives up the parked signing, if any. */
    public suspend fun cancelPending() {
        viewer.callCancellable { token -> handle.cancelPending(token) }
    }

    /** Draws [mark] into an unsigned field without sealing it. */
    public suspend fun fill(field: FormFieldRef, mark: SignatureMark) {
        viewer.callCancellable { token -> handle.fillField(field, mark, token) }
    }

    /** Undoes a visual fill. */
    public suspend fun clearField(field: FormFieldRef) {
        viewer.callCancellable { token -> handle.clearField(field, token) }
    }

    /** Into a field by [mode], or freely on a page as a stamp. */
    public suspend fun place(mark: SignatureMark, target: SignaturePlaceTarget): SignaturePlaceResult =
        viewer.callCancellable { token -> handle.placeMark(mark, target, token) }

    /**
     * Keeps a signed field checkable with no network: asks the `revocation`
     * setting's route whether its certificates were good at its timestamp,
     * and stores the answers in the file; `null` for every signature. With
     * `documentTimestamp` set, a document timestamp follows. All or nothing
     * for the answers. Needs `doc.sign`, the `revocation` setting and trust
     * anchors; without them it throws `invalid-input`.
     */
    public suspend fun addValidationInfo(field: FormFieldRef? = null): SignatureValidationInfoAdded =
        viewer.callCancellable { token -> handle.addValidationInfo(field, token) }

    /** Whether [addValidationInfo] can run: the `revocation` setting, trust anchors and `doc.sign`. */
    public suspend fun canAddValidationInfo(): Boolean = viewer.call { handle.canAddValidationInfo() }

    /**
     * Signed, filled, cleared, validated, protection changed, invalidating,
     * target changed, sign requested and inspection requested. Completes when
     * the document closes (at once if it already has) or the viewer is
     * destroyed. Each collection is a new subscription.
     */
    public fun observeEvents(): Flow<SignatureEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : SignatureEventListener {
                override fun onEvent(event: SignatureEvent) {
                    channel.trySend(event)
                }

                override fun onEnd() {
                    channel.close()
                }
            },
        )
    }
}

/**
 * The signature capability for [document]. Throws `unsupported` when the
 * viewer was built without the signature plugin, whatever the document's
 * state.
 */
public suspend fun PdfViewer.signatures(document: DocumentInfo): SignatureCapability =
    SignatureCapability(call { host.signature(document.id) }, this)

/**
 * The signer [SignatureCapability.sign] uses when a call names none, for
 * every document. Takes effect at the next signing. `null` clears it.
 * Ignored after the viewer shut down.
 */
public fun PdfViewer.setSigner(signer: SignatureSigner?) {
    if (isReleased) return
    host.setSignatureSigner(signer)
}

/**
 * The anchors validation trusts. `null` makes every verdict top out at
 * [SignatureSummary.VALID_UNTRUSTED]. Ignored after the viewer shut down.
 */
public fun PdfViewer.setTrust(trust: SignatureTrust?) {
    if (isReleased) return
    host.setSignatureTrust(trust)
}

/**
 * The HTTP stack the signature plugin's timestamp authority and revocation
 * route post through, for every document. Read per request; with `null`
 * every request fails with no reply. Usually [HttpSignatureTransport].
 * Ignored after the viewer shut down.
 */
public fun PdfViewer.setSignatureTransport(transport: SignatureTransport?) {
    if (isReleased) return
    host.setSignatureTransport(transport)
}
