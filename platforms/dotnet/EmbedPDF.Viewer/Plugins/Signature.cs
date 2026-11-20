using GSg = uniffi.epdf_plugin_signature;

namespace EmbedPDF;

/// <summary>
/// A document's signature fields: reading, validating, signing and filling
/// them. Get one with <see cref="PdfViewer.GetSignaturesAsync"/>; the viewer
/// needs <see cref="PdfViewerOptions.Interaction"/>, <see cref="PdfViewerOptions.Form"/>
/// and <see cref="PdfViewerOptions.Signature"/>. Signing uses the signer from
/// <see cref="PdfViewer.SetSigner"/> unless a call names one. After its
/// document closes, every call throws <see cref="PdfErrorCode.InstanceClosed"/>.
/// </summary>
public sealed class SignatureCapability : IDisposable
{
    readonly PdfViewer viewer;
    readonly GSg.SignatureHandle handle;

    internal SignatureCapability(PdfViewer viewer, GSg.SignatureHandle handle)
    {
        this.viewer = viewer;
        this.handle = handle;
    }

    /// <summary>Current settings, shared by every document using this plugin registration.</summary>
    public Task<SignatureSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => Ffi.ToPublic(await handle.Settings().ConfigureAwait(false)), cancellationToken);

    /// <summary>Merges the supplied fields; the clear flags remove nullable values, omission preserves them.</summary>
    public Task UpdateSettingsAsync(SignatureSettingsPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        return viewer.Run(() => handle.UpdateSettings(Ffi.ToFfi(patch)), cancellationToken);
    }

    /// <summary>Restores the values supplied at registration.</summary>
    public Task ResetSettingsAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.ResetSettings(), cancellationToken);

    /// <summary>The last snapshot read, <see langword="null"/> before the first.</summary>
    public Task<SignatureSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => await handle.Snapshot().ConfigureAwait(false) is { } s ? Ffi.ToPublic(s) : null, cancellationToken);

    /// <summary>Whether the snapshot is loaded.</summary>
    public Task<ResourceStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => Ffi.ToPublic(await handle.Status().ConfigureAwait(false)), cancellationToken);

    /// <summary>The signed fields.</summary>
    public Task<IReadOnlyList<SignatureInfo>> GetSignaturesAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => Infos(await handle.Signatures().ConfigureAwait(false)), cancellationToken);

    /// <summary>The signature fields nobody has signed.</summary>
    public Task<IReadOnlyList<SignatureInfo>> GetUnsignedFieldsAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => Infos(await handle.UnsignedFields().ConfigureAwait(false)), cancellationToken);

    /// <summary>The signature field <paramref name="field"/>, or <see langword="null"/>.</summary>
    public Task<SignatureInfo?> GetSignatureAsync(FormFieldRef field, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        return viewer.Run(async () => await handle.Signature(Ffi.ToFfi(field)).ConfigureAwait(false) is { } s ? Ffi.ToPublic(s) : null, cancellationToken);
    }

    /// <summary>The last verdicts, <see langword="null"/> before the first validation.</summary>
    public Task<IReadOnlyList<SignatureVerdict>?> GetVerdictsAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => await handle.Verdicts().ConfigureAwait(false) is { } v ? Verdicts(v) : null, cancellationToken);

    /// <summary>The last verdict on <paramref name="field"/>, or <see langword="null"/>.</summary>
    public Task<SignatureVerdict?> GetVerdictAsync(FormFieldRef field, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        return viewer.Run(async () => await handle.Verdict(Ffi.ToFfi(field)).ConfigureAwait(false) is { } v ? Ffi.ToPublic(v) : null, cancellationToken);
    }

    /// <summary>What the document's signatures allow to change, or <see langword="null"/> without signatures.</summary>
    public Task<DocumentProtection?> GetProtectionAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => await handle.Protection().ConfigureAwait(false) is { } p ? Ffi.ToPublic(p) : null, cancellationToken);

    /// <summary>A parked two-phase signing: the document is read-only until it completes or is cancelled.</summary>
    public Task<SignaturePending?> GetPendingAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => await handle.Pending().ConfigureAwait(false) is { } p ? Ffi.ToPublic(p) : null, cancellationToken);

    /// <summary>Whether a sign or fill is in flight.</summary>
    public Task<bool> IsBusyAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.IsBusy(), cancellationToken);

    /// <summary>What placing a mark on a field does: <see cref="SignatureMode.Sign"/> while a signer is set, else <see cref="SignatureMode.Visual"/>, unless the settings name a mode.</summary>
    public Task<SignatureMode> GetModeAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => (SignatureMode)(int)await handle.Mode().ConfigureAwait(false), cancellationToken);

    /// <summary>The field the next picked mark goes to.</summary>
    public Task<FormFieldRef?> GetTargetAsync(CancellationToken cancellationToken = default) =>
        viewer.Run(async () => await handle.Target().ConfigureAwait(false) is { } f ? Ffi.ToPublic(f) : null, cancellationToken);

    /// <summary>Whether signing <paramref name="field"/> is allowed; <see langword="false"/> for a field the form doesn't have.</summary>
    public Task<bool> CanSignAsync(FormFieldRef field, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        return viewer.Run(() => handle.CanSign(Ffi.ToFfi(field)), cancellationToken);
    }

    /// <summary>Whether drawing a mark into <paramref name="field"/> is allowed.</summary>
    public Task<bool> CanFillAsync(FormFieldRef field, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        return viewer.Run(() => handle.CanFill(Ffi.ToFfi(field)), cancellationToken);
    }

    /// <summary>Whether the settings allow certifying and the session may.</summary>
    public Task<bool> CanCertifyAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanCertify(), cancellationToken);

    /// <summary>Whether signed revisions can be read.</summary>
    public Task<bool> CanReadRevisionAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanReadRevision(), cancellationToken);

    /// <summary>Re-reads the snapshot; <see langword="null"/> when the engine has no signatures.</summary>
    public Task<SignatureSnapshot?> RefreshAsync(CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(async token => await handle.Refresh(token).ConfigureAwait(false) is { } s ? Ffi.ToPublic(s) : null, cancellationToken);

    /// <summary>Judges every signature. Without trust anchors a good one tops out at <see cref="SignatureSummary.ValidUntrusted"/>.</summary>
    public Task<IReadOnlyList<SignatureVerdict>> ValidateAsync(SignatureValidateRequest? request = null, CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(async token => Verdicts(await handle.Validate(Ffi.ToFfi(request ?? new SignatureValidateRequest()), token).ConfigureAwait(false)), cancellationToken);

    /// <summary>Judges one signature.</summary>
    public Task<SignatureVerdict> ValidateFieldAsync(FormFieldRef field, SignatureValidateRequest? request = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        return viewer.RunCancellable(async token =>
            Ffi.ToPublic(await handle.ValidateField(Ffi.ToFfi(field), Ffi.ToFfi(request ?? new SignatureValidateRequest()), token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>
    /// What changed after signature <paramref name="since"/> (its index), up
    /// to the saved bytes or the working copy. An index below zero throws
    /// <see cref="PdfErrorCode.InvalidInput"/>.
    /// </summary>
    public Task<SignatureChangeAnalysis> AnalyzeChangesAsync(int since, SignatureModificationsBasis until = SignatureModificationsBasis.WorkingCopy, CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(async token => Ffi.ToPublic(await handle.AnalyzeChanges(
            WireInteger.ToUInt32(since, "signature index"), (GSg.SignatureModificationsBasis)(int)Ffi.Defined(until, "modifications basis"), token).ConfigureAwait(false)), cancellationToken);

    /// <summary>The revision the signature on <paramref name="field"/> seals: a complete PDF of its own.</summary>
    public Task<ReadOnlyMemory<byte>> ReadRevisionAsync(FormFieldRef field, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        return viewer.RunCancellable(async token => (ReadOnlyMemory<byte>)await handle.ReadRevision(Ffi.ToFfi(field), token).ConfigureAwait(false), cancellationToken);
    }

    /// <summary>Names the field the next picked mark goes to; <see langword="null"/> clears it.</summary>
    public Task SetTargetAsync(FormFieldRef? field, CancellationToken cancellationToken = default) =>
        viewer.Run(() => handle.SetTarget(field is null ? null : Ffi.ToFfi(field)), cancellationToken);

    /// <summary>Asks the app to show a signed field's facts (<see cref="SignatureEvent.InspectionRequested"/>).</summary>
    public Task RequestInspectionAsync(FormFieldRef field, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        return viewer.Run(() => handle.RequestInspection(Ffi.ToFfi(field)), cancellationToken);
    }

    /// <summary>
    /// Seals <see cref="SignatureSignInput.Field"/> with <paramref name="signer"/>,
    /// or the viewer's. Fails with the signer's own error when it throws, and
    /// leaves nothing pending.
    /// </summary>
    public Task<SignatureCompletion> SignAsync(SignatureSignInput input, ISignatureSigner? signer = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var adapter = signer is null ? null : new SignerAdapter(signer, viewer.Mark, viewer.Logger);
        return viewer.RunCancellable(async token => Ffi.ToPublic(await handle.Sign(Ffi.ToFfi(input), adapter, token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Phase one of an external signing: the digest to sign elsewhere. The document is read-only until phase two.</summary>
    public Task<SignaturePrepared> PrepareSignatureAsync(SignaturePrepareInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return viewer.RunCancellable(async token => Ffi.ToPublic(await handle.PrepareSignature(Ffi.ToFfi(input), token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Phase two: seals the parked signing with a detached CMS.</summary>
    public Task<SignatureCompletion> CompleteSignatureAsync(string signingId, ReadOnlyMemory<byte> cms, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signingId);
        return viewer.RunCancellable(async token => Ffi.ToPublic(await handle.CompleteSignature(signingId, Bytes.Array(cms), token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>Gives up the parked signing, if any.</summary>
    public Task CancelPendingAsync(CancellationToken cancellationToken = default) => viewer.RunCancellable(token => handle.CancelPending(token), cancellationToken);

    /// <summary>Draws <paramref name="mark"/> into an unsigned field without sealing it.</summary>
    public Task FillAsync(FormFieldRef field, SignatureMark mark, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(mark);
        return viewer.RunCancellable(token => handle.FillField(Ffi.ToFfi(field), Ffi.ToFfi(mark), token), cancellationToken);
    }

    /// <summary>Undoes a visual fill.</summary>
    public Task ClearFieldAsync(FormFieldRef field, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        return viewer.RunCancellable(token => handle.ClearField(Ffi.ToFfi(field), token), cancellationToken);
    }

    /// <summary>Puts <paramref name="mark"/> into a field by the mode, or freely on a page as a stamp.</summary>
    public Task<SignaturePlaceResult> PlaceAsync(SignatureMark mark, SignaturePlaceTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mark);
        ArgumentNullException.ThrowIfNull(target);
        return viewer.RunCancellable(async token => Ffi.ToPublic(await handle.PlaceMark(Ffi.ToFfi(mark), Ffi.ToFfi(target), token).ConfigureAwait(false)), cancellationToken);
    }

    /// <summary>
    /// Keeps a signed field checkable with no network: asks the revocation
    /// route whether its certificates were good at its timestamp and stores
    /// the answers in the file; every signature when <paramref name="field"/>
    /// is <see langword="null"/>. All or nothing. Needs the revocation
    /// setting, trust anchors and the session's signing permission; without
    /// them it throws <see cref="PdfErrorCode.InvalidInput"/>.
    /// </summary>
    public Task<SignatureValidationInfoAdded> AddValidationInfoAsync(FormFieldRef? field = null, CancellationToken cancellationToken = default) =>
        viewer.RunCancellable(async token => Ffi.ToPublic(await handle.AddValidationInfo(field is null ? null : Ffi.ToFfi(field), token).ConfigureAwait(false)), cancellationToken);

    /// <summary>Whether <see cref="AddValidationInfoAsync"/> can run.</summary>
    public Task<bool> CanAddValidationInfoAsync(CancellationToken cancellationToken = default) => viewer.Run(() => handle.CanAddValidationInfo(), cancellationToken);

    /// <summary>
    /// Signed, filled, cleared, validated, protection, invalidation, target,
    /// sign-request and inspection events, subscribed when this returns.
    /// Ends when the document closes (at once if it already has), when the
    /// viewer is disposed, or when <paramref name="cancellationToken"/> fires.
    /// Each call is a new subscription; enumerate it once.
    /// </summary>
    public IAsyncEnumerable<SignatureEvent> ObserveEvents(CancellationToken cancellationToken = default)
    {
        var relay = new Relay();
        return viewer.Stream(relay, _ => handle.Subscribe(relay), cancellationToken);
    }

    /// <summary>Releases the native handle. Calls after it throw <see cref="PdfErrorCode.InstanceClosed"/>; streams end as they would otherwise.</summary>
    public void Dispose() => handle.Dispose();

    static IReadOnlyList<SignatureInfo> Infos(GSg.SignatureInfo[] infos) => [.. infos.Select(Ffi.ToPublic)];

    static IReadOnlyList<SignatureVerdict> Verdicts(GSg.SignatureVerdict[] verdicts) => [.. verdicts.Select(Ffi.ToPublic)];

    sealed class Relay() : EventRelay<GSg.SignatureEvent, SignatureEvent>(Ffi.ToPublic), GSg.SignatureEventListener
    {
        public void OnEvent(GSg.SignatureEvent @event) => Deliver(@event);

        public void OnEnd() => Finish();
    }
}

public sealed partial class PdfViewer
{
    /// <summary>The document's signature capability. Throws <see cref="PdfErrorCode.Unsupported"/> without <see cref="PdfViewerOptions.Signature"/>.</summary>
    public async Task<SignatureCapability> GetSignaturesAsync(DocumentInfo document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(this, await CallAsync(h => h.Signature(document.Id), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// The signer <see cref="SignatureCapability.SignAsync"/> uses when a call
    /// names none, for every document. Takes effect at the next signing;
    /// <see langword="null"/> clears it. The viewer holds
    /// <paramref name="signer"/> until it is replaced or the viewer is disposed.
    /// </summary>
    public void SetSigner(ISignatureSigner? signer) =>
        SetSignaturePort(() => host.SetSignatureSigner(signer is null ? null : new SignerAdapter(signer, Mark, Logger)));

    /// <summary>The anchors validation trusts. <see langword="null"/> makes every verdict top out at <see cref="SignatureSummary.ValidUntrusted"/>.</summary>
    public void SetTrust(ISignatureTrust? trust) =>
        SetSignaturePort(() => host.SetSignatureTrust(trust is null ? null : new TrustAdapter(trust, Mark)));

    /// <summary>
    /// The HTTP stack the signature plugin's timestamp authority and
    /// revocation route post through, for every document; read per request.
    /// With <see langword="null"/> every request fails with no reply. Usually
    /// a <see cref="HttpClientSignatureTransport"/>.
    /// </summary>
    public void SetSignatureTransport(ISignatureTransport? transport) =>
        SetSignaturePort(() => host.SetSignatureTransport(transport is null ? null : new TransportAdapter(transport, Logger)));

    void SetSignaturePort(Action set)
    {
        NativeRuntime.EnsureLoaded();
        ThrowIfClosed();
        try
        {
            set();
        }
        catch (ObjectDisposedException)
        {
            throw PdfException.Closed();
        }
    }
}
