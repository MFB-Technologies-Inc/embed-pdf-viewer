using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>
/// The port of SignatureTests.swift, with the signature cases of
/// PluginSettingsTests.swift, SignatureTransportTests.swift and
/// IntegerSurfaceTests.swift.
/// </summary>
public sealed class SignatureTests
{
    static readonly CancellationToken Cancelled = new(true);

    internal static async Task<(PdfViewer Viewer, DocumentInfo Document, SignatureCapability Signatures)> Opened(
        string fixture, bool signer = true, SignatureConfig? config = null)
    {
        var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Interaction = true, Annotation = "", Form = new FormConfig(), Signature = config ?? new SignatureConfig(),
        });
        if (signer) viewer.SetSigner(TestKeys.Signer());
        var document = await viewer.OpenAsync(Fixture.Data(fixture));
        var signatures = await viewer.GetSignaturesAsync(document);
        await signatures.RefreshAsync();
        return (viewer, document, signatures);
    }

    internal static async Task<FormFieldRef> UnsignedField(SignatureCapability signatures) => (await signatures.GetUnsignedFieldsAsync())[0].Field;

    internal static SignatureMark Artwork() => new SignatureMark.Source(Fixture.Data("signature_artwork"));

    /// <summary>The artwork is the appearance the engine draws; a signing without one is refused.</summary>
    internal static SignatureSignInput SignInput(FormFieldRef field) => new(field) { Mark = Artwork() };

    /// <summary>A signer whose prompt the user cancelled.</summary>
    sealed class CancellingSigner : ISignatureSigner
    {
        public IReadOnlyList<ReadOnlyMemory<byte>> GetCertificateChain() => TestKeys.Signer().GetCertificateChain();
        public SignatureKeyAlgorithm Algorithm => SignatureKeyAlgorithm.RsaPkcs1;
        public SignatureDigest Digest => SignatureDigest.Sha256;
        public Task<ReadOnlyMemory<byte>> SignAsync(ReadOnlyMemory<byte> message) =>
            Task.FromException<ReadOnlyMemory<byte>>(new PdfException("UserCancelled", PdfErrorCode.OperationCancelled, "the user cancelled"));
    }

    /// <summary>A signer that holds each signing, as a prompt waiting on the user does, until the test answers it.</summary>
    sealed class HeldSigner(ISignatureSigner signer) : ISignatureSigner
    {
        public readonly TaskCompletionSource<ReadOnlyMemory<byte>> Message = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<ReadOnlyMemory<byte>> Answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<ReadOnlyMemory<byte>> GetCertificateChain() => signer.GetCertificateChain();
        public SignatureKeyAlgorithm Algorithm => signer.Algorithm;
        public SignatureDigest Digest => signer.Digest;
        public Task<ReadOnlyMemory<byte>> SignAsync(ReadOnlyMemory<byte> message)
        {
            Message.TrySetResult(message.ToArray());
            return Answer.Task;
        }
    }

    /// <summary>A signer whose chain throws, or which disposes its viewer there.</summary>
    sealed class ChainSigner(Func<IReadOnlyList<ReadOnlyMemory<byte>>> chain) : ISignatureSigner
    {
        public IReadOnlyList<ReadOnlyMemory<byte>> GetCertificateChain() => chain();
        public SignatureKeyAlgorithm Algorithm => SignatureKeyAlgorithm.RsaPkcs1;
        public SignatureDigest Digest => SignatureDigest.Sha256;
        public Task<ReadOnlyMemory<byte>> SignAsync(ReadOnlyMemory<byte> message) => TestKeys.Signer().SignAsync(message);
    }

    sealed class NullTaskSigner : ISignatureSigner
    {
        public IReadOnlyList<ReadOnlyMemory<byte>> GetCertificateChain() => TestKeys.Signer().GetCertificateChain();
        public SignatureKeyAlgorithm Algorithm => SignatureKeyAlgorithm.RsaPkcs1;
        public SignatureDigest Digest => SignatureDigest.Sha256;
        public Task<ReadOnlyMemory<byte>> SignAsync(ReadOnlyMemory<byte> message) => null!;
    }

    sealed class PemTrust : ISignatureTrust
    {
        public IReadOnlyList<ReadOnlyMemory<byte>> GetAnchors() => [TestKeys.Certificate().RawData];
    }

    [Fact]
    public async Task TwoApprovalsAreListed()
    {
        var (viewer, _, signatures) = await Opened("signed_two_approvals");
        await using var _v = viewer;
        Assert.Equal(2, (await signatures.GetSignaturesAsync()).Count);
        Assert.Equal(SignatureModificationLevel.Annotate, (await signatures.GetProtectionAsync())?.Judged);
        Assert.Equal(3, (await signatures.GetSnapshotAsync())?.Revisions.Count);
    }

    [Fact]
    public async Task ACertificationIsNamed()
    {
        var (viewer, _, signatures) = await Opened("signed_certified");
        await using var _v = viewer;
        Assert.Equal(2, (await signatures.GetProtectionAsync())?.Certification?.Permission);
    }

    [Fact]
    public async Task AFieldSignsAndValidatesUntrusted()
    {
        var (viewer, _, signatures) = await Opened("unsigned_sigfield");
        await using var _v = viewer;
        var field = await UnsignedField(signatures);
        var done = await signatures.SignAsync(SignInput(field));
        Assert.True(done.Signature.Signed);
        Assert.Equal(SignatureCompletionStatus.Completed, done.Status);
        var verdict = await signatures.ValidateFieldAsync(field);
        Assert.Equal(SignatureIntegrity.Valid, verdict.Integrity);
        Assert.Equal(SignatureSummary.ValidUntrusted, verdict.Summary);
    }

    [Fact]
    public async Task WithoutAnchorsASignatureIsValidUntrusted()
    {
        var (viewer, _, signatures) = await Opened("signed_two_approvals");
        await using var _v = viewer;
        var verdicts = await signatures.ValidateAsync();
        Assert.NotEmpty(verdicts);
        Assert.All(verdicts, v => Assert.NotEqual(SignatureSummary.Valid, v.Summary));
    }

    [Fact]
    public async Task TheTestCertificateAsAnAnchorIsTrusted()
    {
        var (viewer, _, signatures) = await Opened("unsigned_sigfield");
        await using var _v = viewer;
        viewer.SetTrust(new PemTrust());
        var field = await UnsignedField(signatures);
        await signatures.SignAsync(SignInput(field));
        var verdict = await signatures.ValidateFieldAsync(field);
        Assert.Equal(SignatureTrustStatus.Trusted, verdict.Trust);
        Assert.Equal(SignatureSummary.Valid, verdict.Summary);
    }

    [Fact]
    public async Task ACancelledSignerLeavesNothingPending()
    {
        var (viewer, _, signatures) = await Opened("unsigned_sigfield", signer: false);
        await using var _v = viewer;
        viewer.SetSigner(new CancellingSigner());
        var field = await UnsignedField(signatures);
        // The signer's own error, not the caller's token: a PdfException with its code.
        var error = await Assert.ThrowsAsync<PdfException>(() => signatures.SignAsync(SignInput(field)));
        Assert.Equal(PdfErrorCode.OperationCancelled, error.Code);
        Assert.Null(await signatures.GetPendingAsync());
        Assert.False(await signatures.IsBusyAsync());
    }

    [Fact]
    public async Task AVisualFillNeedsNoSigner()
    {
        var (viewer, _, signatures) = await Opened("unsigned_sigfield", signer: false, new SignatureConfig { Mode = SignatureMode.Visual });
        await using var _v = viewer;
        Assert.Equal(SignatureMode.Visual, await signatures.GetModeAsync());
        var field = await UnsignedField(signatures);
        Assert.True(await signatures.CanFillAsync(field));
        await signatures.FillAsync(field, Artwork());
        await signatures.ClearFieldAsync(field);
    }

    [Fact]
    public async Task SettingASignerTurnsSigningOn()
    {
        var (viewer, _, signatures) = await Opened("unsigned_sigfield", signer: false);
        await using var _v = viewer;
        var field = await UnsignedField(signatures);
        Assert.Equal(SignatureMode.Sign, await signatures.GetModeAsync());
        var error = await Assert.ThrowsAsync<PdfException>(() => signatures.SignAsync(SignInput(field)));
        Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        viewer.SetSigner(TestKeys.Signer());
        Assert.True(await signatures.CanSignAsync(field));
        Assert.Equal(SignatureCompletionStatus.Completed, (await signatures.SignAsync(SignInput(field))).Status);
    }

    [Fact]
    public async Task AStampMarkFillsAField()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions
        {
            Interaction = true, Annotation = "", Form = new FormConfig(), Stamp = new StampConfig(), Signature = new SignatureConfig(),
        });
        using var stamps = await viewer.GetStampsAsync();
        var library = await stamps.ImportLibraryAsync(Fixture.Data("signature_artwork"), new StampImportOptions { Kind = StampAssetKind.Signature, LibraryKind = "signatures" });
        var asset = (await stamps.GetAssetsAsync(new StampAssetFilter { LibraryId = library }))[0];
        var document = await viewer.OpenAsync(Fixture.Data("unsigned_sigfield"));
        using var signatures = await viewer.GetSignaturesAsync(document);
        await signatures.RefreshAsync();
        await signatures.FillAsync(await UnsignedField(signatures), new SignatureMark.Asset(asset.Id));
    }

    [Fact]
    public async Task AFreeMarkIsAStamp()
    {
        var (viewer, document, signatures) = await Opened("unsigned_sigfield", signer: false);
        await using var _v = viewer;
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        var result = await signatures.PlaceAsync(Artwork(), new SignaturePlaceTarget.Free(page, new PdfPoint(50, 50), null, null));
        Assert.IsType<SignaturePlaceResult.Placed>(result);
    }

    [Fact]
    public async Task ATwoPhaseSigningParksThenAborts()
    {
        var (viewer, _, signatures) = await Opened("unsigned_sigfield", signer: false);
        await using var _v = viewer;
        var field = await UnsignedField(signatures);
        var prepared = await signatures.PrepareSignatureAsync(new SignaturePrepareInput(field));
        Assert.Equal(32, prepared.Digest.Length);
        Assert.Equal(prepared.SigningId, (await signatures.GetPendingAsync())?.SigningId);
        await signatures.CancelPendingAsync();
        Assert.Null(await signatures.GetPendingAsync());
    }

    [Fact]
    public async Task AChangeAfterTheSignatureIsAnalyzed()
    {
        var (viewer, _, signatures) = await Opened("signed_two_approvals");
        await using var _v = viewer;
        var analysis = await signatures.AnalyzeChangesAsync(0, SignatureModificationsBasis.Persisted);
        Assert.Equal(1, analysis.LaterRevisions);
        Assert.Equal(SignatureModificationsVerdict.Permitted, analysis.Verdict);
        var revision = await signatures.ReadRevisionAsync((await signatures.GetSignaturesAsync())[0].Field);
        Assert.Equal("%PDF-"u8.ToArray(), revision[..5].ToArray());
    }

    [Fact]
    public async Task SettingTheTargetIsObserved()
    {
        var (viewer, _, signatures) = await Opened("unsigned_sigfield");
        await using var _v = viewer;
        var field = await UnsignedField(signatures);
        var events = signatures.ObserveEvents();
        await signatures.SetTargetAsync(field);
        Assert.Equal(field, await signatures.GetTargetAsync());
        var changed = await Fixture.First(events, e => e as SignatureEvent.TargetChanged);
        Assert.Equal(field, changed.Field);
    }

    [Fact]
    public async Task AClosedDocumentsCapabilityThrows()
    {
        var (viewer, document, signatures) = await Opened("signed_two_approvals");
        await using var _v = viewer;
        await viewer.CloseAsync(document);
        var error = await Assert.ThrowsAsync<PdfException>(() => signatures.GetSnapshotAsync());
        Assert.Equal(PdfErrorCode.InstanceClosed, error.Code);
        Assert.Equal("PluginError", error.Name);
    }

    [Fact]
    public async Task APendingSignatureDoesNotBlockTheViewer()
    {
        var (viewer, document, signatures) = await Opened("unsigned_sigfield", signer: false);
        await using var _v = viewer;
        var held = new HeldSigner(TestKeys.Signer());
        viewer.SetSigner(held);
        var field = await UnsignedField(signatures);
        var signing = signatures.SignAsync(SignInput(field));
        var message = await held.Message.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // The signer has not answered, and the viewer still serves calls.
        Assert.Single(await viewer.GetPagesAsync(document));
        Assert.Equal(ResourceStatus.Ready, await signatures.GetStatusAsync());
        held.Answer.SetResult(await TestKeys.Signer().SignAsync(message));
        Assert.True((await signing).Signature.Signed);
    }

    [Fact]
    public async Task ASignerNamedByTheCallSigns()
    {
        var (viewer, _, signatures) = await Opened("unsigned_sigfield", signer: false);
        await using var _v = viewer;
        var field = await UnsignedField(signatures);
        Assert.Equal(SignatureCompletionStatus.Completed, (await signatures.SignAsync(SignInput(field), TestKeys.Signer())).Status);
    }

    [Fact]
    public async Task DroppingTheStreamUnsubscribes()
    {
        var (viewer, _, signatures) = await Opened("signed_two_approvals");
        await using var _v = viewer;
        using var stop = new CancellationTokenSource();
        var reading = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in signatures.ObserveEvents(stop.Token)) { }
            }
            catch (OperationCanceledException)
            {
            }
        });
        await Task.Delay(20);
        await stop.CancelAsync();
        await reading.WaitAsync(TimeSpan.FromSeconds(5));
        await signatures.RefreshAsync();
    }

    [Fact]
    public async Task EveryCancellableMemberCancelsBeforeItStarts()
    {
        var (viewer, document, signatures) = await Opened("unsigned_sigfield");
        await using var _v = viewer;
        var field = await UnsignedField(signatures);
        var page = (await viewer.GetPagesAsync(document))[0].Page;
        Func<Task>[] calls =
        [
            () => signatures.RefreshAsync(Cancelled),
            () => signatures.ValidateAsync(null, Cancelled),
            () => signatures.ValidateFieldAsync(field, null, Cancelled),
            () => signatures.AnalyzeChangesAsync(0, SignatureModificationsBasis.WorkingCopy, Cancelled),
            () => signatures.ReadRevisionAsync(field, Cancelled),
            () => signatures.SignAsync(SignInput(field), null, Cancelled),
            () => signatures.PrepareSignatureAsync(new SignaturePrepareInput(field), Cancelled),
            () => signatures.CompleteSignatureAsync("none", new byte[] { 0x30, 0 }, Cancelled),
            () => signatures.CancelPendingAsync(Cancelled),
            () => signatures.FillAsync(field, Artwork(), Cancelled),
            () => signatures.ClearFieldAsync(field, Cancelled),
            () => signatures.PlaceAsync(Artwork(), new SignaturePlaceTarget.Free(page, new PdfPoint(50, 50), null, null), Cancelled),
            () => signatures.AddValidationInfoAsync(null, Cancelled),
        ];
        foreach (var call in calls) await Assert.ThrowsAnyAsync<OperationCanceledException>(call);
        Assert.Single(await signatures.GetUnsignedFieldsAsync());
        Assert.Null(await signatures.GetPendingAsync());
        Assert.Equal(ResourceStatus.Ready, await signatures.GetStatusAsync());
    }

    [Fact]
    public async Task ASigningCancelledWhileItRunsFinishesOrCancels()
    {
        var (viewer, _, signatures) = await Opened("unsigned_sigfield");
        await using var _v = viewer;
        var field = await UnsignedField(signatures);
        await RedactionTests.FinishesOrCancels(token => signatures.SignAsync(SignInput(field), null, token));
        await RedactionTests.FinishesOrCancels(token => signatures.ValidateAsync(null, token));
        Assert.Equal(ResourceStatus.Ready, await signatures.GetStatusAsync());
    }

    [Fact]
    public async Task AChainThatThrowsFailsTheSigningWithItsCode()
    {
        var (viewer, _, signatures) = await Opened("unsigned_sigfield", signer: false);
        await using var _v = viewer;
        var field = await UnsignedField(signatures);
        var refused = new ChainSigner(() => throw new PdfException("Locked", PdfErrorCode.PermissionDenied, "the key is locked"));
        var error = await Assert.ThrowsAsync<PdfException>(() => signatures.SignAsync(SignInput(field), refused));
        Assert.Equal(PdfErrorCode.PermissionDenied, error.Code);
        var broken = new ChainSigner(() => throw new InvalidOperationException("no chain"));
        error = await Assert.ThrowsAsync<PdfException>(() => signatures.SignAsync(SignInput(field), broken));
        Assert.Equal(PdfErrorCode.OperationFailed, error.Code);
        await Assert.ThrowsAsync<PdfException>(() => signatures.SignAsync(SignInput(field), new NullTaskSigner()));
        Assert.Null(await signatures.GetPendingAsync());
        Assert.Equal(SignatureCompletionStatus.Completed, (await signatures.SignAsync(SignInput(field), TestKeys.Signer())).Status);
    }

    [Fact]
    public async Task DisposingFromASignerThrowsIntoIt()
    {
        var (viewer, _, signatures) = await Opened("unsigned_sigfield", signer: false);
        await using var _v = viewer;
        var field = await UnsignedField(signatures);
        Exception? thrown = null;
        var signer = new ChainSigner(() =>
        {
            try { viewer.Dispose(); }
            catch (Exception error) { thrown = error; }
            return TestKeys.Signer().GetCertificateChain();
        });
        await signatures.SignAsync(SignInput(field), signer);
        Assert.IsType<InvalidOperationException>(thrown);
    }

    [Fact]
    public async Task SignatureModeCanClearAndResetToTheRegisteredMode()
    {
        var (viewer, _, signature) = await Opened("form_fields", signer: false, new SignatureConfig { Mode = SignatureMode.Visual });
        await using var _v = viewer;
        await signature.UpdateSettingsAsync(new SignatureSettingsPatch { ClearMode = true, AllowCertify = true });
        var changed = await signature.GetSettingsAsync();
        Assert.Null(changed.Mode);
        Assert.True(changed.AllowCertify);
        await signature.ResetSettingsAsync();
        var reset = await signature.GetSettingsAsync();
        Assert.Equal(SignatureMode.Visual, reset.Mode);
        Assert.False(reset.AllowCertify);
    }

    [Fact]
    public async Task TheSignatureSettingsCarryTheNewFields()
    {
        var (viewer, _, signatures) = await Opened("unsigned_sigfield", signer: false, new SignatureConfig
        {
            TimestampAuthority = new SignatureTimestampAuthority("https://tsa.example/"), DocumentTimestamp = true,
        });
        await using var _v = viewer;
        var settings = await signatures.GetSettingsAsync();
        Assert.True(settings.HasTimestampAuthority && !settings.HasRevocation && settings.DocumentTimestamp, settings.ToString());
        await signatures.UpdateSettingsAsync(new SignatureSettingsPatch { ClearTimestampAuthority = true, DocumentTimestamp = false });
        settings = await signatures.GetSettingsAsync();
        Assert.True(!settings.HasTimestampAuthority && !settings.DocumentTimestamp, settings.ToString());
        // No route and no anchors: nothing to fill with.
        Assert.False(await signatures.CanAddValidationInfoAsync());
    }

    [Fact]
    public async Task ASignatureIndexBelowZeroThrowsInvalidInput()
    {
        var (viewer, _, signatures) = await Opened("signed_two_approvals");
        await using var _v = viewer;
        foreach (var value in new[] { -1, int.MinValue })
        {
            var error = await Assert.ThrowsAsync<PdfException>(() => signatures.AnalyzeChangesAsync(value, SignatureModificationsBasis.Persisted));
            Assert.Equal(PdfErrorCode.InvalidInput, error.Code);
        }
        Assert.Equal(1, (await signatures.AnalyzeChangesAsync(0, SignatureModificationsBasis.Persisted)).LaterRevisions);
    }

    [Fact]
    public async Task ReadsAnswerForAField()
    {
        var (viewer, _, signatures) = await Opened("signed_two_approvals");
        await using var _v = viewer;
        var field = (await signatures.GetSignaturesAsync())[0].Field;
        Assert.Equal(field, (await signatures.GetSignatureAsync(field))?.Field);
        await signatures.ValidateAsync();
        Assert.Equal(2, (await signatures.GetVerdictsAsync())?.Count);
        Assert.Equal(field, (await signatures.GetVerdictAsync(field))?.Signature.Field);
        Assert.True(await signatures.CanReadRevisionAsync());
        Assert.False(await signatures.CanCertifyAsync());
        var events = signatures.ObserveEvents();
        await signatures.RequestInspectionAsync(field);
        Assert.Equal(field, (await Fixture.First(events, e => e as SignatureEvent.InspectionRequested)).Field);
    }
}
