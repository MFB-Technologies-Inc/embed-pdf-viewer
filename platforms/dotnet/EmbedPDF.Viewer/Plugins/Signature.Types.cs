namespace EmbedPDF;

/// <summary>The certification signature and its DocMDP permission.</summary>
/// <param name="SignatureIndex">The signature index.</param>
/// <param name="Permission">The permission.</param>
public sealed record DocumentCertification(int SignatureIndex, int Permission);

/// <summary>A lock an earlier signature established.</summary>
/// <param name="SignatureIndex">The signature index.</param>
/// <param name="Source">The source.</param>
/// <param name="Spec">The spec.</param>
public sealed record DocumentFieldLock(int SignatureIndex, DocumentFieldLockSource Source, SignatureFieldLock Spec);

/// <summary>What the signatures in a document mean for what comes after: what a signer declared (enforced, refused) and what a validator holds later changes to (judged).</summary>
/// <param name="Enforced">The enforced.</param>
/// <param name="Judged">The judged.</param>
/// <param name="Certification">The certification.</param>
/// <param name="FieldLocks">The field locks.</param>
/// <param name="PolicyVersion">The policy version.</param>
public sealed record DocumentProtection(SignatureModificationLevel? Enforced, SignatureModificationLevel? Judged, DocumentCertification? Certification, IReadOnlyList<DocumentFieldLock> FieldLocks, int PolicyVersion);

/// <summary>The saved version the document is on.</summary>
/// <param name="Sha256">The sha256.</param>
/// <param name="SizeBytes">The size bytes.</param>
public sealed record SignatureBaseVersion(string Sha256, long SizeBytes);

/// <summary>What changed after a signature: the engine's ChangeAnalysis, the parts a UI reads.</summary>
/// <param name="Verdict">The verdict.</param>
/// <param name="Primary">The primary.</param>
/// <param name="Basis">The basis.</param>
/// <param name="LaterRevisions">Revisions after the signed one.</param>
/// <param name="UndoneObjectNumbers">Objects rewritten after the signature that hold the signed value again.</param>
public sealed record SignatureChangeAnalysis(SignatureModificationsVerdict Verdict, SignatureChangeFinding? Primary, SignatureModificationsBasis Basis, int LaterRevisions, IReadOnlyList<int> UndoneObjectNumbers);

/// <summary>The finding that decided an analysis.</summary>
/// <param name="Rule">The rule.</param>
/// <param name="Verdict">The verdict.</param>
/// <param name="ObjectNumber">The object number.</param>
/// <param name="Detail">The detail.</param>
public sealed record SignatureChangeFinding(string Rule, SignatureFindingVerdict Verdict, int ObjectNumber, string? Detail);

/// <summary>A completed signing.</summary>
/// <param name="Status">The status.</param>
/// <param name="Signature">The signature.</param>
/// <param name="Version">The version the sealed bytes became.</param>
/// <param name="Previous">What they were built on.</param>
/// <param name="Protection">The protection.</param>
/// <param name="ValidationInfo">The validation data the revocation setting added right after the seal, or <see langword="null"/> when nothing was asked for (SignFieldResult).</param>
/// <param name="ValidationInfoError">Why adding it failed. The signature is sealed either way.</param>
public sealed record SignatureCompletion(SignatureCompletionStatus Status, SignatureInfo Signature, SignatureBaseVersion Version, SignatureVersionRef Previous, DocumentProtection Protection, SignatureValidationInfoAdded? ValidationInfo, SignatureFailure? ValidationInfoError);

/// <summary>An error a result carries rather than throws, as ViewerError::Kernel would carry it.</summary>
/// <param name="Name">The name.</param>
/// <param name="Code">The code.</param>
/// <param name="Detail">The detail.</param>
public sealed record SignatureFailure(string Name, string? Code, string Detail);

/// <summary>A FieldMDP transform or a field /Lock: which fields it freezes, and an optional DocMDP /P (1, 2 or 3) tightening.</summary>
/// <param name="Action">The action.</param>
/// <param name="Fields">Fully qualified names; a name covers its descendants. Empty for All.</param>
public sealed record SignatureFieldLock(SignatureFieldLockAction Action, IReadOnlyList<string> Fields)
{
    /// <summary>The permission.</summary>
    public int? Permission { get; init; }
}

/// <summary>One signature field, signed or not.</summary>
/// <param name="Index">Position in the snapshot (field order).</param>
/// <param name="Field">The field.</param>
/// <param name="FieldName">The field name.</param>
/// <param name="Widget">The widget.</param>
/// <param name="Signed">The signed.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Filter">The filter.</param>
/// <param name="SubFilter">The sub filter.</param>
/// <param name="ByteRange">Four offsets, [a, b, c, d].</param>
/// <param name="ContentsSizeBytes">The contents size bytes.</param>
/// <param name="Coverage">The coverage.</param>
/// <param name="RevisionIndex">The revision index.</param>
/// <param name="Signer">The signer.</param>
/// <param name="DocMdp">The doc mdp.</param>
/// <param name="CatalogCertification">The catalog names this signature as THE certification.</param>
/// <param name="FieldMdp">The field mdp.</param>
/// <param name="Lock">The lock.</param>
/// <param name="SeedValue">The seed value.</param>
public sealed record SignatureInfo(int Index, FormFieldRef Field, string FieldName, SignatureWidget? Widget, bool Signed, SignatureKind Kind, string? Filter, string? SubFilter, IReadOnlyList<long>? ByteRange, long ContentsSizeBytes, SignatureCoverage? Coverage, int? RevisionIndex, SignatureSignerClaims Signer, int? DocMdp, bool CatalogCertification, SignatureFieldLock? FieldMdp, SignatureFieldLock? Lock, SignatureSeedValue? SeedValue);

/// <summary>One thing the file lacks (LongTermGap).</summary>
/// <param name="Certificate">The certificate's subject, when the gap is about one.</param>
/// <param name="Reason">The reason.</param>
public sealed record SignatureLongTermGap(string? Certificate, SignatureLongTermGapReason Reason);

/// <summary>Whether the file holds everything a reader needs to check the signature with no network ("LTV enabled"), judged at a time a timestamp proves (LongTermVerdict).</summary>
/// <param name="Enabled">The enabled.</param>
/// <param name="ProvenAt">The proven at.</param>
/// <param name="ExpiresAt">The expires at.</param>
/// <param name="DocumentTimestampedAt">The document timestamped at.</param>
/// <param name="Missing">The missing.</param>
public sealed record SignatureLongTermVerdict(bool Enabled, string? ProvenAt, string? ExpiresAt, string? DocumentTimestampedAt, IReadOnlyList<SignatureLongTermGap> Missing);

/// <summary>What changed after the signature, and how that is judged.</summary>
/// <param name="Verdict">The verdict.</param>
/// <param name="Detail">The detail.</param>
/// <param name="Basis">The basis.</param>
/// <param name="LaterRevisions">The later revisions.</param>
/// <param name="Undone">The undone.</param>
public sealed record SignatureModifications(SignatureModificationsVerdict Verdict, string? Detail, SignatureModificationsBasis Basis, int? LaterRevisions, bool? Undone);

/// <summary>A parked two-phase signing: the document is read-only until it completes or is aborted.</summary>
/// <param name="SigningId">The signing id.</param>
/// <param name="Field">The field.</param>
public sealed record SignaturePending(string SigningId, FormFieldRef Field);

/// <summary>prepare_signature's input: sign's, plus the CMS profile and digest the external signer will use.</summary>
/// <param name="Field">The field.</param>
public sealed record SignaturePrepareInput(FormFieldRef Field)
{
    /// <summary>The mark.</summary>
    public SignatureMark? Mark { get; init; }
    /// <summary>The signer.</summary>
    public SignatureSignerInput? Signer { get; init; }
    /// <summary>The certify.</summary>
    public int? Certify { get; init; }
    /// <summary>The lock.</summary>
    public SignatureFieldLock? Lock { get; init; }
    /// <summary>adbe.pkcs7.detached or ETSI.CAdES.detached (the default).</summary>
    public string? SubFilter { get; init; }
    /// <summary>The digest.</summary>
    public SignatureDigest? Digest { get; init; }
    /// <summary>The authority complete_signature will timestamp with; prepare reserves room for its token. <see langword="null"/> is the setting.</summary>
    public SignatureCallAuthority? TimestampAuthority { get; init; }
}

/// <summary>Phase one of an external signing: the digest to sign elsewhere.</summary>
/// <param name="SigningId">The signing id.</param>
/// <param name="Digest">The digest.</param>
/// <param name="Algorithm">sha256, sha384 or sha512.</param>
/// <param name="ByteRange">The byte range.</param>
/// <param name="ContentsSizeBytes">The contents size bytes.</param>
/// <param name="SubFilter">The sub filter.</param>
/// <param name="ExpectedVersion">What complete_signature must present.</param>
/// <param name="ExpiresAt">The expires at.</param>
public sealed record SignaturePrepared(string SigningId, ReadOnlyMemory<byte> Digest, string Algorithm, IReadOnlyList<long> ByteRange, long ContentsSizeBytes, string SubFilter, SignatureVersionRef ExpectedVersion, string? ExpiresAt);

/// <summary>One saved revision: the byte prefix [0, end).</summary>
/// <param name="Index">0 is the original document.</param>
/// <param name="End">The end.</param>
/// <param name="XrefOffset">The xref offset.</param>
/// <param name="SignatureIndex">The signature that seals exactly this revision.</param>
public sealed record SignatureRevision(int Index, long End, long XrefOffset, int? SignatureIndex);

/// <summary>A revocation source asked through the app's own route (httpRevocationSource, D-2).</summary>
/// <param name="Url">The route's address.</param>
public sealed record SignatureRevocationRoute(string Url)
{
    /// <summary>The headers.</summary>
    public IReadOnlyList<SignatureTransportHeader>? Headers { get; init; }
    /// <summary>One attempt's limit. <see langword="null"/> is 10000.</summary>
    public int? TimeoutMs { get; init; }
    /// <summary>Further attempts when no answer came. <see langword="null"/> is 1.</summary>
    public int? Retries { get; init; }
    /// <summary>The largest answer read. <see langword="null"/> is 2 MiB, the route's default.</summary>
    public int? MaxReplyBytes { get; init; }
}

/// <summary>The field's /SV seed value.</summary>
/// <param name="ParserVersion">/V: the seed-value parser the signer needs (1 = PDF 1.5, 2 = PDF 1.7).</param>
/// <param name="ParserVersionRequired">The parser version required.</param>
/// <param name="Mdp">/MDP /P when present: 0 = approval signature only.</param>
/// <param name="Filter">The filter.</param>
/// <param name="FilterRequired">The filter required.</param>
/// <param name="SubFilters">The sub filters.</param>
/// <param name="SubFiltersRequired">The sub filters required.</param>
/// <param name="DigestMethods">The digest methods.</param>
/// <param name="DigestMethodsRequired">The digest methods required.</param>
/// <param name="Reasons">The reasons.</param>
/// <param name="ReasonsRequired">The reasons required.</param>
/// <param name="UnsupportedRequired">A required entry the engine does not implement: signing refuses.</param>
public sealed record SignatureSeedValue(int? ParserVersion, bool ParserVersionRequired, int? Mdp, string? Filter, bool FilterRequired, IReadOnlyList<string> SubFilters, bool SubFiltersRequired, IReadOnlyList<string> DigestMethods, bool DigestMethodsRequired, IReadOnlyList<string> Reasons, bool ReasonsRequired, bool UnsupportedRequired);

/// <summary>A signature settings.</summary>
/// <param name="Mode">The mode.</param>
/// <param name="AllowCertify">The allow certify.</param>
/// <param name="HasTimestampAuthority">A timestamp authority is set.</param>
/// <param name="HasRevocation">A revocation source is set.</param>
/// <param name="DocumentTimestamp">The document timestamp.</param>
public sealed record SignatureSettings(SignatureMode? Mode, bool AllowCertify, bool HasTimestampAuthority, bool HasRevocation, bool DocumentTimestamp);

/// <summary>A signature settings patch.</summary>
public sealed record SignatureSettingsPatch
{
    /// <summary>The mode.</summary>
    public SignatureMode? Mode { get; init; }
    /// <summary>The clear mode.</summary>
    public bool ClearMode { get; init; }
    /// <summary>The allow certify.</summary>
    public bool? AllowCertify { get; init; }
    /// <summary>A new timestamp authority, through the host's transport.</summary>
    public SignatureTimestampAuthority? TimestampAuthority { get; init; }
    /// <summary>The clear timestamp authority.</summary>
    public bool ClearTimestampAuthority { get; init; }
    /// <summary>A new revocation route, through the host's transport.</summary>
    public SignatureRevocationRoute? Revocation { get; init; }
    /// <summary>The clear revocation.</summary>
    public bool ClearRevocation { get; init; }
    /// <summary>The document timestamp.</summary>
    public bool? DocumentTimestamp { get; init; }
}

/// <summary>sign's input.</summary>
/// <param name="Field">The field.</param>
public sealed record SignatureSignInput(FormFieldRef Field)
{
    /// <summary>The artwork drawn into the field; <see langword="null"/> signs with no appearance.</summary>
    public SignatureMark? Mark { get; init; }
    /// <summary>The signer.</summary>
    public SignatureSignerInput? Signer { get; init; }
    /// <summary>The DocMDP permission (1, 2 or 3) to certify with. Needs allow_certify and doc.sign.certify, and no earlier signature.</summary>
    public int? Certify { get; init; }
    /// <summary>A FieldMDP for this signature, mirrored as the field's /Lock.</summary>
    public SignatureFieldLock? Lock { get; init; }
    /// <summary>This call's timestamp authority. <see langword="null"/> is the setting.</summary>
    public SignatureCallAuthority? TimestampAuthority { get; init; }
}

/// <summary>What the signer claimed. None of it is verified.</summary>
/// <param name="Name">The name.</param>
/// <param name="Reason">The reason.</param>
/// <param name="Location">The location.</param>
/// <param name="ContactInfo">The contact info.</param>
/// <param name="SignedAt">/M, when the signer says they signed (signedAt, an ISO date time). A claim, not verified.</param>
public sealed record SignatureSignerClaims(string? Name, string? Reason, string? Location, string? ContactInfo, string? SignedAt);

/// <summary>What the signature dictionary says about the signer (SignatureSignerInput, the input's signer). Every member optional; the certificate's common name is the default name.</summary>
public sealed record SignatureSignerInput
{
    /// <summary>The name.</summary>
    public string? Name { get; init; }
    /// <summary>The reason.</summary>
    public string? Reason { get; init; }
    /// <summary>The location.</summary>
    public string? Location { get; init; }
    /// <summary>The contact info.</summary>
    public string? ContactInfo { get; init; }
}

/// <summary>The document's signatures, revisions and protection.</summary>
/// <param name="ChainValid">False when the cross-reference chain is broken: every byte fact is then indeterminate.</param>
/// <param name="Revisions">The revisions.</param>
/// <param name="Signatures">The signatures.</param>
/// <param name="Protection">The protection.</param>
public sealed record SignatureSnapshot(bool ChainValid, IReadOnlyList<SignatureRevision> Revisions, IReadOnlyList<SignatureInfo> Signatures, DocumentProtection Protection);

/// <summary>A timestamp authority reached over the host's transport (httpTimestampAuthority).</summary>
/// <param name="Url">The authority's address, or the app's own route in front of it.</param>
public sealed record SignatureTimestampAuthority(string Url)
{
    /// <summary>What sign stamps with. <see langword="null"/> is sha256.</summary>
    public SignatureDigest? Hash { get; init; }
    /// <summary>The policy to ask for (an OID).</summary>
    public string? Policy { get; init; }
    /// <summary>The headers.</summary>
    public IReadOnlyList<SignatureTransportHeader>? Headers { get; init; }
    /// <summary>One attempt's limit. <see langword="null"/> is 10000.</summary>
    public int? TimeoutMs { get; init; }
    /// <summary>Further attempts when no answer came. <see langword="null"/> is 1.</summary>
    public int? Retries { get; init; }
}

/// <summary>What an RFC 3161 token is worth (TimestampVerdict).</summary>
/// <param name="Cryptography">The cryptography.</param>
/// <param name="Trust">The trust.</param>
/// <param name="TrustReason">The trust reason.</param>
/// <param name="ValidationTime">The validation time.</param>
/// <param name="Revocation">The revocation.</param>
/// <param name="RevocationReason">The revocation reason.</param>
/// <param name="AuthorityName">The authority name.</param>
/// <param name="Summary">The summary.</param>
/// <param name="StampedAt">The token's time, ISO 8601.</param>
/// <param name="AuthorityCertificate">DER of the authority's certificate.</param>
public sealed record SignatureTimestampVerdict(SignatureCryptography Cryptography, SignatureTrustStatus Trust, string? TrustReason, SignatureValidationTime ValidationTime, SignatureRevocationStatus Revocation, string? RevocationReason, string? AuthorityName, SignatureSummary Summary, string? StampedAt, ReadOnlyMemory<byte>? AuthorityCertificate);

/// <summary>One request header, in the order sent.</summary>
/// <param name="Name">The name.</param>
/// <param name="Value">The value.</param>
public sealed record SignatureTransportHeader(string Name, string Value);

/// <summary>One HTTP POST.</summary>
/// <param name="Url">The url.</param>
/// <param name="ContentType">The content-type header: application/timestamp-query or application/json.</param>
/// <param name="Headers">More headers, after content-type: an API key, a session token.</param>
/// <param name="Body">The body.</param>
/// <param name="TimeoutMs">Give up after this long and answer timed_out.</param>
/// <param name="MaxReplyBytes">Stop reading a 2xx reply past this many bytes and answer too_large. A longer 2xx body answered through replied counts as too_large too; a longer body with any other status counts as that status with no body.</param>
public sealed record SignatureTransportRequest(string Url, string ContentType, IReadOnlyList<SignatureTransportHeader> Headers, ReadOnlyMemory<byte> Body, int TimeoutMs, int MaxReplyBytes);

/// <summary>validate's options. Every member optional.</summary>
public sealed record SignatureValidateRequest
{
    /// <summary><see langword="null"/> is EarliestTimestamp.</summary>
    public SignatureRequestedTime? ValidationTime { get; init; }
    /// <summary><see langword="null"/> is the working copy, the plugin's default.</summary>
    public SignatureModificationsBasis? Until { get; init; }
}

/// <summary>ValidationInfoAdded: what add_validation_info stored, and the document timestamp that followed, if any.</summary>
/// <param name="Added">The added.</param>
/// <param name="DocumentTimestamp">The new document timestamp's field.</param>
public sealed record SignatureValidationInfoAdded(SignatureValidationInfoCounts Added, FormFieldRef? DocumentTimestamp);

/// <summary>How many entries of each kind were stored.</summary>
/// <param name="Certificates">The certificates.</param>
/// <param name="OcspResponses">The ocsp responses.</param>
/// <param name="Crls">The crls.</param>
public sealed record SignatureValidationInfoCounts(int Certificates, int OcspResponses, int Crls);

/// <summary>One signature, judged. The parsed CMS is not exported: signature and trust_reason carry what a UI shows.</summary>
/// <param name="Signature">The signature.</param>
/// <param name="Integrity">The integrity.</param>
/// <param name="Cryptography">The cryptography.</param>
/// <param name="Trust">The trust.</param>
/// <param name="TrustReason">The trust reason.</param>
/// <param name="ValidationTime">The validation time.</param>
/// <param name="Modifications">The modifications.</param>
/// <param name="Summary">The summary.</param>
/// <param name="Revocation">The signer's certificates at validation_time, from the answers the file holds and those the revocation setting fetched.</param>
/// <param name="RevocationReason">The revocation reason.</param>
/// <param name="Timestamp">The signature's own RFC 3161 timestamp, when it carries one.</param>
/// <param name="LongTerm">Whether the file alone lets a reader check it with no network.</param>
public sealed record SignatureVerdict(SignatureInfo Signature, SignatureIntegrity Integrity, SignatureCryptography Cryptography, SignatureTrustStatus Trust, string? TrustReason, SignatureValidationTime ValidationTime, SignatureModifications Modifications, SignatureSummary Summary, SignatureRevocationStatus Revocation, string? RevocationReason, SignatureTimestampVerdict? Timestamp, SignatureLongTermVerdict LongTerm);

/// <summary>What a signing candidate was built on.</summary>
/// <param name="BaseSha256">The base sha256.</param>
/// <param name="EditsVersion">The edits version.</param>
public sealed record SignatureVersionRef(string BaseSha256, long EditsVersion);

/// <summary>The field's widget.</summary>
/// <param name="Annotation"><see langword="null"/> when the widget cannot be addressed as an annotation.</param>
/// <param name="AnnotObjectNumber">The annot object number.</param>
/// <param name="Page"><see langword="null"/> when no page lists the widget.</param>
public sealed record SignatureWidget(AnnotationRef? Annotation, int AnnotObjectNumber, PageRef? Page);

/// <summary>A call's timestamp authority, over the timestampAuthority setting.</summary>
public abstract record SignatureCallAuthority
{
    private protected SignatureCallAuthority() { }

    /// <summary>No timestamp on this one.</summary>
    public sealed record Without : SignatureCallAuthority;

    /// <summary>This authority, through the host's transport.</summary>
    /// <param name="Authority">The authority.</param>
    public sealed record Http(SignatureTimestampAuthority Authority) : SignatureCallAuthority;

}

/// <summary>The plugin's nine change hooks, as one stream. Origins are dropped.</summary>
public abstract record SignatureEvent
{
    private protected SignatureEvent() { }

    /// <summary>Signed.</summary>
    /// <param name="Field">The field.</param>
    /// <param name="Result">The result.</param>
    public sealed record Signed(FormFieldRef Field, SignatureCompletion Result) : SignatureEvent;

    /// <summary>A visual mark was drawn into a field.</summary>
    /// <param name="Field">The field.</param>
    public sealed record Filled(FormFieldRef Field) : SignatureEvent;

    /// <summary>A field's visual mark was removed.</summary>
    /// <param name="Field">The field.</param>
    public sealed record Cleared(FormFieldRef Field) : SignatureEvent;

    /// <summary>Validated.</summary>
    /// <param name="Verdicts">The verdicts.</param>
    public sealed record Validated(IReadOnlyList<SignatureVerdict> Verdicts) : SignatureEvent;

    /// <summary>Protection changed.</summary>
    /// <param name="Protection">The protection.</param>
    public sealed record ProtectionChanged(DocumentProtection Protection) : SignatureEvent;

    /// <summary>An unsaved edit just turned a held signature into one a save would invalidate. Once per edge.</summary>
    /// <param name="Field">The field.</param>
    /// <param name="Detail">The detail.</param>
    public sealed record InvalidationPredicted(FormFieldRef Field, string Detail) : SignatureEvent;

    /// <summary>Target changed.</summary>
    /// <param name="Field">The field.</param>
    public sealed record TargetChanged(FormFieldRef? Field) : SignatureEvent;

    /// <summary>Mode ask (or a certification to offer): the app decides.</summary>
    /// <param name="Field">The field.</param>
    /// <param name="Mark">The mark.</param>
    public sealed record SignRequested(FormFieldRef Field, SignatureMark Mark) : SignatureEvent;

    /// <summary>A signed field was activated.</summary>
    /// <param name="Field">The field.</param>
    public sealed record InspectionRequested(FormFieldRef Field) : SignatureEvent;

}

/// <summary>What goes into a signature field: a stamp-library asset, or artwork.</summary>
public abstract record SignatureMark
{
    private protected SignatureMark() { }

    /// <summary>A stamp-library asset (needs the stamp plugin).</summary>
    /// <param name="AssetId">The asset id.</param>
    public sealed record Asset(string AssetId) : SignatureMark;

    /// <summary>Artwork bytes: a PDF, PNG or JPEG.</summary>
    /// <param name="Bytes">The bytes.</param>
    public sealed record Source(ReadOnlyMemory<byte> Bytes) : SignatureMark;

}

/// <summary>What place_mark did.</summary>
public abstract record SignaturePlaceResult
{
    private protected SignaturePlaceResult() { }

    /// <summary>Signed.</summary>
    /// <param name="Field">The field.</param>
    /// <param name="Result">The result.</param>
    public sealed record Signed(FormFieldRef Field, SignatureCompletion Result) : SignaturePlaceResult;

    /// <summary>Filled.</summary>
    /// <param name="Field">The field.</param>
    public sealed record Filled(FormFieldRef Field) : SignaturePlaceResult;

    /// <summary>SignRequested fired: the app decides.</summary>
    /// <param name="Field">The field.</param>
    public sealed record Requested(FormFieldRef Field) : SignaturePlaceResult;

    /// <summary>A free placement became this stamp annotation.</summary>
    /// <param name="Annotation">The annotation.</param>
    public sealed record Placed(AnnotationRef Annotation) : SignaturePlaceResult;

}

/// <summary>Where place_mark puts a mark.</summary>
public abstract record SignaturePlaceTarget
{
    private protected SignaturePlaceTarget() { }

    /// <summary>Into a signature field, by mode.</summary>
    /// <param name="FieldValue">The field value.</param>
    public sealed record Field(FormFieldRef FieldValue) : SignaturePlaceTarget;

    /// <summary>Freely on a page, as a stamp. at is the centre, in page space.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="At">The at.</param>
    /// <param name="TargetWidth">The target width.</param>
    /// <param name="Rotation">The rotation.</param>
    public sealed record Free(PageRef Page, PdfPoint At, double? TargetWidth, double? Rotation) : SignaturePlaceTarget;

}

/// <summary>When validate judges the chains: upstream's ValidationTime | 'earliest-timestamp'.</summary>
public abstract record SignatureRequestedTime
{
    private protected SignatureRequestedTime() { }

    /// <summary>Each signature at the earliest trusted timestamp over what it signed, and now when there is none. The default.</summary>
    public sealed record EarliestTimestamp : SignatureRequestedTime;

    /// <summary>Now.</summary>
    public sealed record Now : SignatureRequestedTime;

    /// <summary>An RFC 3161 token's time, as an ISO 8601 string.</summary>
    /// <param name="GenTime">The gen time.</param>
    public sealed record Timestamp(string GenTime) : SignatureRequestedTime;

}

/// <summary>When the chain was judged.</summary>
public abstract record SignatureValidationTime
{
    private protected SignatureValidationTime() { }

    /// <summary>Now.</summary>
    public sealed record Now : SignatureValidationTime;

    /// <summary>A verified RFC 3161 token's time, as an ISO 8601 string.</summary>
    /// <param name="GenTime">The gen time.</param>
    public sealed record Timestamp(string GenTime) : SignatureValidationTime;

}

/// <summary>A document field lock source.</summary>
public enum DocumentFieldLockSource
{
    /// <summary>Fieldmdp.</summary>
    Fieldmdp,
    /// <summary>Lock.</summary>
    Lock,
}

/// <summary>A signature completion status.</summary>
public enum SignatureCompletionStatus
{
    /// <summary>Completed.</summary>
    Completed,
    /// <summary>An idempotent replay with the same CMS.</summary>
    AlreadyCompleted,
}

/// <summary>How a signature's /ByteRange relates to the file.</summary>
public enum SignatureCoverage
{
    /// <summary>Exactly one chained revision, the hole being this signature's /Contents.</summary>
    WholeRevision,
    /// <summary>Partial.</summary>
    Partial,
    /// <summary>Malformed.</summary>
    Malformed,
}

/// <summary>The CMS signature over its signed attributes.</summary>
public enum SignatureCryptography
{
    /// <summary>Valid.</summary>
    Valid,
    /// <summary>Invalid.</summary>
    Invalid,
    /// <summary>Unsupported.</summary>
    Unsupported,
}

/// <summary>A signature digest.</summary>
public enum SignatureDigest
{
    /// <summary>Sha256.</summary>
    Sha256,
    /// <summary>Sha384.</summary>
    Sha384,
    /// <summary>Sha512.</summary>
    Sha512,
}

/// <summary>A signature field lock action.</summary>
public enum SignatureFieldLockAction
{
    /// <summary>All.</summary>
    All,
    /// <summary>Include.</summary>
    Include,
    /// <summary>Exclude.</summary>
    Exclude,
}

/// <summary>A signature finding verdict.</summary>
public enum SignatureFindingVerdict
{
    /// <summary>Permitted.</summary>
    Permitted,
    /// <summary>Forbidden.</summary>
    Forbidden,
    /// <summary>Incomplete.</summary>
    Incomplete,
}

/// <summary>Do the signed bytes still hash to what the CMS signed?</summary>
public enum SignatureIntegrity
{
    /// <summary>Valid.</summary>
    Valid,
    /// <summary>Invalid.</summary>
    Invalid,
    /// <summary>Indeterminate.</summary>
    Indeterminate,
}

/// <summary>A signature key algorithm.</summary>
public enum SignatureKeyAlgorithm
{
    /// <summary>Rsa pkcs1.</summary>
    RsaPkcs1,
    /// <summary>Rsa pss.</summary>
    RsaPss,
    /// <summary>Ecdsa.</summary>
    Ecdsa,
}

/// <summary>A signature kind.</summary>
public enum SignatureKind
{
    /// <summary>Signature.</summary>
    Signature,
    /// <summary>A document timestamp (/DocTimeStamp).</summary>
    Timestamp,
}

/// <summary>Why a long-term verdict is not enabled for one certificate.</summary>
public enum SignatureLongTermGapReason
{
    /// <summary>No timestamp.</summary>
    NoTimestamp,
    /// <summary>Untrusted.</summary>
    Untrusted,
    /// <summary>No answer.</summary>
    NoAnswer,
    /// <summary>Revoked.</summary>
    Revoked,
}

/// <summary>What placeMark does with a mark over a field.</summary>
public enum SignatureMode
{
    /// <summary>Seal the field with the signer.</summary>
    Sign,
    /// <summary>Draw the mark into the field without sealing.</summary>
    Visual,
    /// <summary>Neither: SignRequested fires and the app decides.</summary>
    Ask,
}

/// <summary>The modification level a signed document allows: none &lt; lta (timestamps) &lt; fill (form fill and signing) &lt; annotate.</summary>
public enum SignatureModificationLevel
{
    /// <summary>None.</summary>
    None,
    /// <summary>Lta.</summary>
    Lta,
    /// <summary>Fill.</summary>
    Fill,
    /// <summary>Annotate.</summary>
    Annotate,
}

/// <summary>Which bytes were judged: the saved ones or the unsaved working copy.</summary>
public enum SignatureModificationsBasis
{
    /// <summary>Persisted.</summary>
    Persisted,
    /// <summary>Working copy.</summary>
    WorkingCopy,
}

/// <summary>A signature modifications verdict.</summary>
public enum SignatureModificationsVerdict
{
    /// <summary>Unchanged.</summary>
    Unchanged,
    /// <summary>Permitted.</summary>
    Permitted,
    /// <summary>Forbidden.</summary>
    Forbidden,
    /// <summary>Indeterminate.</summary>
    Indeterminate,
}

/// <summary>What the revocation answers say about a certificate.</summary>
public enum SignatureRevocationStatus
{
    /// <summary>Good.</summary>
    Good,
    /// <summary>Revoked.</summary>
    Revoked,
    /// <summary>Unknown.</summary>
    Unknown,
}

/// <summary>The one answer a UI shows. Never Valid without trust anchors.</summary>
public enum SignatureSummary
{
    /// <summary>Valid.</summary>
    Valid,
    /// <summary>Valid untrusted.</summary>
    ValidUntrusted,
    /// <summary>Invalid.</summary>
    Invalid,
    /// <summary>Indeterminate.</summary>
    Indeterminate,
}

/// <summary>The signer's chain against the trust anchors.</summary>
public enum SignatureTrustStatus
{
    /// <summary>Trusted.</summary>
    Trusted,
    /// <summary>Untrusted.</summary>
    Untrusted,
    /// <summary>Unknown.</summary>
    Unknown,
}
