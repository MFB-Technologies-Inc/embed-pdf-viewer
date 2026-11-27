using GF = uniffi.epdf_ffi;
using GS = uniffi.epdf_plugin_search;
using GSg = uniffi.epdf_plugin_signature;

namespace EmbedPDF;

internal static partial class Ffi
{
    internal static DocumentCertification ToPublic(GSg.DocumentCertification r) => new(WireInteger.ToInt(r.SignatureIndex, "signature index"), (int)r.Permission);

    internal static DocumentFieldLock ToPublic(GSg.DocumentFieldLock r) => new(WireInteger.ToInt(r.SignatureIndex, "signature index"), (DocumentFieldLockSource)(int)r.Source, ToPublic(r.Spec));

    internal static DocumentProtection ToPublic(GSg.DocumentProtection r) => new(r.Enforced is { } x5 ? (SignatureModificationLevel)(int)x5 : null, r.Judged is { } x7 ? (SignatureModificationLevel)(int)x7 : null, r.Certification is null ? null : ToPublic(r.Certification), [.. r.FieldLocks.Select(x11 => ToPublic(x11))], WireInteger.ToInt(r.PolicyVersion, "policy version"));

    internal static SignatureBaseVersion ToPublic(GSg.SignatureBaseVersion r) => new(r.Sha256, WireInteger.ToLong(r.SizeBytes, "size bytes"));

    internal static SignatureChangeAnalysis ToPublic(GSg.SignatureChangeAnalysis r) => new((SignatureModificationsVerdict)(int)r.Verdict, r.Primary is null ? null : ToPublic(r.Primary), (SignatureModificationsBasis)(int)r.Basis, WireInteger.ToInt(r.LaterRevisions, "later revisions"), [.. r.UndoneObjectNumbers.Select(x21 => WireInteger.ToInt(x21, "undone object numbers"))]);

    internal static SignatureChangeFinding ToPublic(GSg.SignatureChangeFinding r) => new(r.Rule, (SignatureFindingVerdict)(int)r.Verdict, WireInteger.ToInt(r.ObjectNumber, "object number"), r.Detail);

    internal static SignatureCompletion ToPublic(GSg.SignatureCompletion r) => new((SignatureCompletionStatus)(int)r.Status, ToPublic(r.Signature), ToPublic(r.Version), ToPublic(r.Previous), ToPublic(r.Protection), r.ValidationInfo is null ? null : ToPublic(r.ValidationInfo), r.ValidationInfoError is null ? null : ToPublic(r.ValidationInfoError));

    internal static SignatureFailure ToPublic(GSg.SignatureFailure r) => new(r.Name, r.Code, r.Detail);

    internal static SignatureFieldLock ToPublic(GSg.SignatureFieldLock r) => new((SignatureFieldLockAction)(int)r.Action, [.. r.Fields.Select(x40 => x40)]) { Permission = r.Permission is { } x42 ? (int)x42 : null };

    internal static GSg.SignatureFieldLock ToFfi(SignatureFieldLock r) => r is null ? throw new ArgumentException("A signature field lock is required.") : new(Action: (GSg.SignatureFieldLockAction)(int)Defined(r.Action, "action"), Fields: [.. Arguments.NoNullElements(r.Fields, "fields").Select(x45 => x45 ?? throw new ArgumentException("fields is required."))], Permission: r.Permission is { } x47 ? WireInteger.ToByte(x47, "permission") : null);

    internal static SignatureInfo ToPublic(GSg.SignatureInfo r) => new(WireInteger.ToInt(r.Index, "index"), ToPublic(r.Field), r.FieldName, r.Widget is null ? null : ToPublic(r.Widget), r.Signed, (SignatureKind)(int)r.Kind, r.Filter, r.SubFilter, r.ByteRange is null ? null : r.ByteRange.Select(x59 => WireInteger.ToLong(x59, "byte range")).ToList(), WireInteger.ToLong(r.ContentsSizeBytes, "contents size bytes"), r.Coverage is { } x62 ? (SignatureCoverage)(int)x62 : null, r.RevisionIndex is { } x64 ? WireInteger.ToInt(x64, "revision index") : null, ToPublic(r.Signer), r.DocMdp is { } x67 ? (int)x67 : null, r.CatalogCertification, r.FieldMdp is null ? null : ToPublic(r.FieldMdp), r.Lock is null ? null : ToPublic(r.Lock), r.SeedValue is null ? null : ToPublic(r.SeedValue));

    internal static SignatureLongTermGap ToPublic(GSg.SignatureLongTermGap r) => new(r.Certificate, (SignatureLongTermGapReason)(int)r.Reason);

    internal static SignatureLongTermVerdict ToPublic(GSg.SignatureLongTermVerdict r) => new(r.Enabled, r.ProvenAt, r.ExpiresAt, r.DocumentTimestampedAt, [.. r.Missing.Select(x82 => ToPublic(x82))]);

    internal static SignatureModifications ToPublic(GSg.SignatureModifications r) => new((SignatureModificationsVerdict)(int)r.Verdict, r.Detail, (SignatureModificationsBasis)(int)r.Basis, r.LaterRevisions is { } x87 ? WireInteger.ToInt(x87, "later revisions") : null, r.Undone);

    internal static SignaturePending ToPublic(GSg.SignaturePending r) => new(r.SigningId, ToPublic(r.Field));

    internal static GSg.SignaturePrepareInput ToFfi(SignaturePrepareInput r) => r is null ? throw new ArgumentException("A signature prepare input is required.") : new(Field: ToFfi(r.Field), Mark: r.Mark is null ? null : ToFfi(r.Mark), Signer: r.Signer is null ? null : ToFfi(r.Signer), Certify: r.Certify is { } x97 ? WireInteger.ToByte(x97, "certify") : null, Lock: r.Lock is null ? null : ToFfi(r.Lock), SubFilter: r.SubFilter, Digest: r.Digest is { } x102 ? (GSg.SignatureDigest)(int)Defined(x102, "digest") : null, TimestampAuthority: r.TimestampAuthority is null ? null : ToFfi(r.TimestampAuthority));

    internal static SignaturePrepared ToPublic(GSg.SignaturePrepared r) => new(r.SigningId, r.Digest, r.Algorithm, [.. r.ByteRange.Select(x109 => WireInteger.ToLong(x109, "byte range"))], WireInteger.ToLong(r.ContentsSizeBytes, "contents size bytes"), r.SubFilter, ToPublic(r.ExpectedVersion), r.ExpiresAt);

    internal static SignatureRevision ToPublic(GSg.SignatureRevision r) => new(WireInteger.ToInt(r.Index, "index"), WireInteger.ToLong(r.End, "end"), WireInteger.ToLong(r.XrefOffset, "xref offset"), r.SignatureIndex is { } x118 ? WireInteger.ToInt(x118, "signature index") : null);

    internal static GSg.SignatureRevocationRoute ToFfi(SignatureRevocationRoute r) => r is null ? throw new ArgumentException("A signature revocation route is required.") : new(Url: r.Url ?? throw new ArgumentException("url is required."), Headers: r.Headers is null ? [] : [.. Arguments.NoNullElements(r.Headers, "headers").Select(x123 => ToFfi(x123))], TimeoutMs: r.TimeoutMs is { } x125 ? WireInteger.ToUInt32(x125, "timeout ms") : null, Retries: r.Retries is { } x127 ? WireInteger.ToUInt32(x127, "retries") : null, MaxReplyBytes: r.MaxReplyBytes is { } x129 ? WireInteger.ToUInt32(x129, "max reply bytes") : null);

    internal static SignatureSeedValue ToPublic(GSg.SignatureSeedValue r) => new(r.ParserVersion is { } x131 ? WireInteger.ToInt(x131, "parser version") : null, r.ParserVersionRequired, r.Mdp is { } x134 ? (int)x134 : null, r.Filter, r.FilterRequired, [.. r.SubFilters.Select(x138 => x138)], r.SubFiltersRequired, [.. r.DigestMethods.Select(x141 => x141)], r.DigestMethodsRequired, [.. r.Reasons.Select(x144 => x144)], r.ReasonsRequired, r.UnsupportedRequired);

    internal static SignatureSettings ToPublic(GSg.SignatureSettings r) => new(r.Mode is { } x148 ? (SignatureMode)(int)x148 : null, r.AllowCertify, r.HasTimestampAuthority, r.HasRevocation, r.DocumentTimestamp);

    internal static GSg.SignatureSettingsPatch ToFfi(SignatureSettingsPatch r) => r is null ? throw new ArgumentException("A signature settings patch is required.") : new(Mode: r.Mode is { } x154 ? (GSg.SignatureMode)(int)Defined(x154, "mode") : null, ClearMode: r.ClearMode, AllowCertify: r.AllowCertify, TimestampAuthority: r.TimestampAuthority is null ? null : ToFfi(r.TimestampAuthority), ClearTimestampAuthority: r.ClearTimestampAuthority, Revocation: r.Revocation is null ? null : ToFfi(r.Revocation), ClearRevocation: r.ClearRevocation, DocumentTimestamp: r.DocumentTimestamp);

    internal static GSg.SignatureSignInput ToFfi(SignatureSignInput r) => r is null ? throw new ArgumentException("A signature sign input is required.") : new(Field: ToFfi(r.Field), Mark: r.Mark is null ? null : ToFfi(r.Mark), Signer: r.Signer is null ? null : ToFfi(r.Signer), Certify: r.Certify is { } x170 ? WireInteger.ToByte(x170, "certify") : null, Lock: r.Lock is null ? null : ToFfi(r.Lock), TimestampAuthority: r.TimestampAuthority is null ? null : ToFfi(r.TimestampAuthority));

    internal static SignatureSignerClaims ToPublic(GSg.SignatureSignerClaims r) => new(r.Name, r.Reason, r.Location, r.ContactInfo, r.SignedAt);

    internal static GSg.SignatureSignerInput ToFfi(SignatureSignerInput r) => r is null ? throw new ArgumentException("A signature signer input is required.") : new(Name: r.Name, Reason: r.Reason, Location: r.Location, ContactInfo: r.ContactInfo);

    internal static SignatureSnapshot ToPublic(GSg.SignatureSnapshot r) => new(r.ChainValid, [.. r.Revisions.Select(x186 => ToPublic(x186))], [.. r.Signatures.Select(x188 => ToPublic(x188))], ToPublic(r.Protection));

    internal static GSg.SignatureTimestampAuthority ToFfi(SignatureTimestampAuthority r) => r is null ? throw new ArgumentException("A signature timestamp authority is required.") : new(Url: r.Url ?? throw new ArgumentException("url is required."), Hash: r.Hash is { } x192 ? (GSg.SignatureDigest)(int)Defined(x192, "hash") : null, Policy: r.Policy, Headers: r.Headers is null ? [] : [.. Arguments.NoNullElements(r.Headers, "headers").Select(x197 => ToFfi(x197))], TimeoutMs: r.TimeoutMs is { } x199 ? WireInteger.ToUInt32(x199, "timeout ms") : null, Retries: r.Retries is { } x201 ? WireInteger.ToUInt32(x201, "retries") : null);

    internal static SignatureTimestampVerdict ToPublic(GSg.SignatureTimestampVerdict r) => new((SignatureCryptography)(int)r.Cryptography, (SignatureTrustStatus)(int)r.Trust, r.TrustReason, ToPublic(r.ValidationTime), (SignatureRevocationStatus)(int)r.Revocation, r.RevocationReason, r.AuthorityName, (SignatureSummary)(int)r.Summary, r.StampedAt, r.AuthorityCertificate is null ? null : r.AuthorityCertificate);

    internal static SignatureTransportHeader ToPublic(GSg.SignatureTransportHeader r) => new(r.Name, r.Value);

    internal static GSg.SignatureTransportHeader ToFfi(SignatureTransportHeader r) => r is null ? throw new ArgumentException("A signature transport header is required.") : new(Name: r.Name ?? throw new ArgumentException("name is required."), Value: r.Value ?? throw new ArgumentException("value is required."));

    internal static SignatureTransportRequest ToPublic(GSg.SignatureTransportRequest r) => new(r.Url, r.ContentType, [.. r.Headers.Select(x220 => ToPublic(x220))], r.Body, WireInteger.ToInt(r.TimeoutMs, "timeout ms"), WireInteger.ToInt(r.MaxReplyBytes, "max reply bytes"));

    internal static GSg.SignatureValidateRequest ToFfi(SignatureValidateRequest r) => r is null ? throw new ArgumentException("A signature validate request is required.") : new(ValidationTime: r.ValidationTime is null ? null : ToFfi(r.ValidationTime), Until: r.Until is { } x227 ? (GSg.SignatureModificationsBasis)(int)Defined(x227, "until") : null);

    internal static SignatureValidationInfoAdded ToPublic(GSg.SignatureValidationInfoAdded r) => new(ToPublic(r.Added), r.DocumentTimestamp is null ? null : ToPublic(r.DocumentTimestamp));

    internal static SignatureValidationInfoCounts ToPublic(GSg.SignatureValidationInfoCounts r) => new(WireInteger.ToInt(r.Certificates, "certificates"), WireInteger.ToInt(r.OcspResponses, "ocsp responses"), WireInteger.ToInt(r.Crls, "crls"));

    internal static SignatureVerdict ToPublic(GSg.SignatureVerdict r) => new(ToPublic(r.Signature), (SignatureIntegrity)(int)r.Integrity, (SignatureCryptography)(int)r.Cryptography, (SignatureTrustStatus)(int)r.Trust, r.TrustReason, ToPublic(r.ValidationTime), ToPublic(r.Modifications), (SignatureSummary)(int)r.Summary, (SignatureRevocationStatus)(int)r.Revocation, r.RevocationReason, r.Timestamp is null ? null : ToPublic(r.Timestamp), ToPublic(r.LongTerm));

    internal static SignatureVersionRef ToPublic(GSg.SignatureVersionRef r) => new(r.BaseSha256, WireInteger.ToLong(r.EditsVersion, "edits version"));

    internal static SignatureWidget ToPublic(GSg.SignatureWidget r) => new(r.Annotation is null ? null : ToPublic(r.Annotation), WireInteger.ToInt(r.AnnotObjectNumber, "annot object number"), r.Page is null ? null : ToPublic(r.Page));

    internal static GSg.SignatureCallAuthority ToFfi(SignatureCallAuthority u) => u switch
    {
        SignatureCallAuthority.Without => new GSg.SignatureCallAuthority.Without(),
        SignatureCallAuthority.Http c => new GSg.SignatureCallAuthority.Http(ToFfi(c.Authority)),
        null => throw new ArgumentException("A signature call authority is required."),
        _ => throw Unknown(u),
    };

    internal static SignatureEvent ToPublic(GSg.SignatureEvent u) => u switch
    {
        GSg.SignatureEvent.Signed c => new SignatureEvent.Signed(ToPublic(c.Field), ToPublic(c.Result)),
        GSg.SignatureEvent.Filled c => new SignatureEvent.Filled(ToPublic(c.Field)),
        GSg.SignatureEvent.Cleared c => new SignatureEvent.Cleared(ToPublic(c.Field)),
        GSg.SignatureEvent.Validated c => new SignatureEvent.Validated([.. c.Verdicts.Select(x260 => ToPublic(x260))]),
        GSg.SignatureEvent.ProtectionChanged c => new SignatureEvent.ProtectionChanged(ToPublic(c.Protection)),
        GSg.SignatureEvent.InvalidationPredicted c => new SignatureEvent.InvalidationPredicted(ToPublic(c.Field), c.Detail),
        GSg.SignatureEvent.TargetChanged c => new SignatureEvent.TargetChanged(c.Field is null ? null : ToPublic(c.Field)),
        GSg.SignatureEvent.SignRequested c => new SignatureEvent.SignRequested(ToPublic(c.Field), ToPublic(c.Mark)),
        GSg.SignatureEvent.InspectionRequested c => new SignatureEvent.InspectionRequested(ToPublic(c.Field)),
        _ => throw Unknown(u),
    };

    internal static SignatureMark ToPublic(GSg.SignatureMark u) => u switch
    {
        GSg.SignatureMark.Asset c => new SignatureMark.Asset(c.AssetId),
        GSg.SignatureMark.Source c => new SignatureMark.Source(c.Bytes),
        _ => throw Unknown(u),
    };

    internal static GSg.SignatureMark ToFfi(SignatureMark u) => u switch
    {
        SignatureMark.Asset c => new GSg.SignatureMark.Asset(c.AssetId ?? throw new ArgumentException("asset id is required.")),
        SignatureMark.Source c => new GSg.SignatureMark.Source(Bytes.Array(c.Bytes)),
        null => throw new ArgumentException("A signature mark is required."),
        _ => throw Unknown(u),
    };

    internal static SignaturePlaceResult ToPublic(GSg.SignaturePlaceResult u) => u switch
    {
        GSg.SignaturePlaceResult.Signed c => new SignaturePlaceResult.Signed(ToPublic(c.Field), ToPublic(c.Result)),
        GSg.SignaturePlaceResult.Filled c => new SignaturePlaceResult.Filled(ToPublic(c.Field)),
        GSg.SignaturePlaceResult.Requested c => new SignaturePlaceResult.Requested(ToPublic(c.Field)),
        GSg.SignaturePlaceResult.Placed c => new SignaturePlaceResult.Placed(ToPublic(c.Annotation)),
        _ => throw Unknown(u),
    };

    internal static GSg.SignaturePlaceTarget ToFfi(SignaturePlaceTarget u) => u switch
    {
        SignaturePlaceTarget.Field c => new GSg.SignaturePlaceTarget.Field(ToFfi(c.FieldValue)),
        SignaturePlaceTarget.Free c => new GSg.SignaturePlaceTarget.Free(ToFfi(c.Page), ToFfi(c.At), c.TargetWidth, c.Rotation),
        null => throw new ArgumentException("A signature place target is required."),
        _ => throw Unknown(u),
    };

    internal static GSg.SignatureRequestedTime ToFfi(SignatureRequestedTime u) => u switch
    {
        SignatureRequestedTime.EarliestTimestamp => new GSg.SignatureRequestedTime.EarliestTimestamp(),
        SignatureRequestedTime.Now => new GSg.SignatureRequestedTime.Now(),
        SignatureRequestedTime.Timestamp c => new GSg.SignatureRequestedTime.Timestamp(c.GenTime ?? throw new ArgumentException("gen time is required.")),
        null => throw new ArgumentException("A signature requested time is required."),
        _ => throw Unknown(u),
    };

    internal static SignatureValidationTime ToPublic(GSg.SignatureValidationTime u) => u switch
    {
        GSg.SignatureValidationTime.Now => new SignatureValidationTime.Now(),
        GSg.SignatureValidationTime.Timestamp c => new SignatureValidationTime.Timestamp(c.GenTime),
        _ => throw Unknown(u),
    };
}
