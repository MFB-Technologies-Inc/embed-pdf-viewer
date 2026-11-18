//! `gate.ts`: the check between a signer's answer and `signatures.complete`.

use serde_json::{Value, json};

use crate::cms::{Cryptography, Profile, verify_parsed};
use crate::parse::parse_cms_internal;
use crate::verify_timestamp::{
    Stamped, TimestampProblem, check_timestamp_token, signature_value_of,
};

/// `CompletionProfile`: the shape the CMS must have, a PDF signature's
/// (`pkcs7`, `cades-b`), or a document timestamp's RFC 3161 token
/// (`timestamp`).
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum CompletionProfile {
    Signature(Profile),
    Timestamp,
}

impl CompletionProfile {
    pub fn from_name(name: &str) -> Option<CompletionProfile> {
        match name {
            "timestamp" => Some(CompletionProfile::Timestamp),
            other => Profile::from_name(other).map(CompletionProfile::Signature),
        }
    }

    pub fn name(self) -> &'static str {
        match self {
            CompletionProfile::Signature(profile) => profile.name(),
            CompletionProfile::Timestamp => "timestamp",
        }
    }
}

impl From<Profile> for CompletionProfile {
    fn from(profile: Profile) -> Self {
        CompletionProfile::Signature(profile)
    }
}

/// `CompletionGate`.
#[derive(Clone, Debug, PartialEq)]
pub enum Gate {
    Ok,
    Refused { reason: &'static str, detail: String },
}

impl Gate {
    pub fn to_json(&self) -> Value {
        match self {
            Gate::Ok => json!({ "ok": true }),
            Gate::Refused { reason, detail } => {
                json!({ "ok": false, "reason": reason, "detail": detail })
            }
        }
    }
}

/// What the gate reads of `SignaturePrepared`.
#[derive(Clone, Debug, PartialEq)]
pub struct Prepared {
    pub digest: Vec<u8>,
    /// As the engine spelled it; compared as text.
    pub algorithm: String,
    pub contents_size_bytes: f64,
}

/// `verifyForCompletion`.
pub fn verify_for_completion(
    cms: &[u8],
    prepared: &Prepared,
    profile: impl Into<CompletionProfile>,
) -> Gate {
    let profile = profile.into();
    let refuse = |reason, detail: String| Gate::Refused { reason, detail };
    if cms.len() as f64 > prepared.contents_size_bytes {
        return refuse(
            "too-large",
            format!(
                "the signature is {} bytes and {} were reserved: {}",
                cms.len(),
                crate::number_text(prepared.contents_size_bytes),
                reserve_advice(cms.len())
            ),
        );
    }
    if profile == CompletionProfile::Timestamp {
        return verify_document_timestamp(cms, prepared);
    }
    let internal = match parse_cms_internal(cms) {
        Ok(internal) => internal,
        Err(e) => return refuse("malformed", e.text()),
    };
    let parsed = &internal.parsed;
    if parsed.digest_algorithm.name() != prepared.algorithm {
        return refuse(
            "algorithm-mismatch",
            format!(
                "the CMS digests with {}, the document was prepared with {}",
                parsed.digest_algorithm.name(),
                prepared.algorithm
            ),
        );
    }
    if parsed.message_digest != prepared.digest {
        return refuse(
            "digest-mismatch",
            "the CMS message digest is not the prepared digest".into(),
        );
    }
    if profile == CompletionProfile::Signature(Profile::CadesB) {
        if !parsed.signing_certificate_v2 {
            return refuse(
                "profile-violation",
                "CAdES requires the ESS signing-certificate-v2 attribute".into(),
            );
        }
        if parsed.signing_time.is_some() {
            return refuse(
                "profile-violation",
                "PAdES forbids the CMS signing-time attribute".into(),
            );
        }
    }
    match verify_parsed(&internal) {
        Cryptography::Valid => {}
        Cryptography::Unsupported => {
            return refuse(
                "signature-invalid",
                "the signature or its certificate binding uses an algorithm that is not supported"
                    .into(),
            );
        }
        Cryptography::Invalid => {
            return refuse("signature-invalid", "the signature does not verify".into());
        }
    }
    // A timestamp the CMS carries must belong to it. One that can't be
    // checked passes.
    let signature_value = signature_value_of(&internal.signer_info);
    for token in &internal.timestamp_tokens {
        let check = check_timestamp_token(
            token,
            &Stamped::Bytes(signature_value.clone()),
            &internal.certificates,
        );
        if check.verdict == Cryptography::Invalid {
            return refuse(
                if check.problem == Some(TimestampProblem::Imprint) {
                    "digest-mismatch"
                } else {
                    "signature-invalid"
                },
                format!(
                    "the signature's timestamp does not belong to it: {}",
                    check.detail.unwrap_or_default()
                ),
            );
        }
    }
    Gate::Ok
}

const RESERVE_STEP: usize = 4096;
/// The most room `prepare` reserves for a signature: 4 MiB.
const LARGEST_RESERVE: usize = 4 * 1024 * 1024;

/// `reserveAdvice`: the reserve to prepare with for a signature of `size`
/// bytes, in whole steps of 4096.
fn reserve_advice(size: usize) -> String {
    let reserve = size.div_ceil(RESERVE_STEP) * RESERVE_STEP;
    if reserve > LARGEST_RESERVE {
        "that is more than a signature may reserve".into()
    } else {
        format!("pass `contentsSizeBytes: {reserve}`")
    }
}

/// `verifyDocumentTimestamp`: a document timestamp's token must stamp the
/// prepared digest with the prepared algorithm and pass
/// `checkTimestampToken`. One that can't be checked is refused like a wrong
/// one.
fn verify_document_timestamp(cms: &[u8], prepared: &Prepared) -> Gate {
    let refuse = |reason, detail: String| Gate::Refused { reason, detail };
    let stamped =
        Stamped::Digest { digest: prepared.digest.clone(), algorithm: prepared.algorithm.clone() };
    let check = check_timestamp_token(cms, &stamped, &[]);
    if check.verdict == Cryptography::Valid {
        return Gate::Ok;
    }
    let detail = check.detail.clone().unwrap_or_default();
    match check.problem {
        Some(TimestampProblem::Unreadable) => refuse("malformed", detail),
        Some(TimestampProblem::NoCertificate) => {
            refuse("malformed", "the token does not carry its authority's certificate".into())
        }
        Some(TimestampProblem::ImprintAlgorithm) => refuse(
            "algorithm-mismatch",
            format!(
                "the token stamps a {} digest, the document was prepared with {}",
                check.info.as_ref().map_or("", |i| i.imprint_algorithm.name()),
                prepared.algorithm
            ),
        ),
        Some(TimestampProblem::Imprint) => refuse(
            "digest-mismatch",
            "the token's message imprint is not the prepared digest".into(),
        ),
        _ => refuse("signature-invalid", detail),
    }
}
