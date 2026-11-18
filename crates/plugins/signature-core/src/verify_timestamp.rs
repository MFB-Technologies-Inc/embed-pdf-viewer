//! `cms/verify-timestamp.ts`: the one verifier of RFC 3161 timestamp tokens,
//! what signing, the completion gate and reading all ask. Offline and
//! deterministic; it says nothing about trust.

use std::rc::Rc;

use epdf_kernel::Error;
use epdf_kernel::rt::Promise;

use crate::cert::Certificate;
use crate::cms::{Binding, Cryptography, check_certificate_binding, verify_signer_signature};
use crate::oids::Digest;
use crate::parse::{
    CmsContent, CmsParts, Internal, ParseError, SignerInfo, read_cms_parts, with_signer_certificate,
};
use crate::timestamp::{TimestampInfo, read_timestamp_info};

/// `signatureValueOf`: the bytes a signature's timestamp stamps, the
/// contents of the SignerInfo's signature octet string.
pub fn signature_value_of(signer_info: &SignerInfo) -> Vec<u8> {
    signer_info.signature.clone()
}

/// `Stamped`: what a token is expected to stamp.
#[derive(Clone)]
pub enum Stamped {
    /// Bytes this verifier hashes with the token's own algorithm.
    Bytes(Vec<u8>),
    /// A digest made elsewhere under a known algorithm.
    /// The algorithm as the caller spelled it; compared as text.
    Digest { digest: Vec<u8>, algorithm: String },
    /// A digest the caller makes under whichever algorithm the token names.
    DigestWith(Rc<dyn Fn(Digest) -> Promise<Vec<u8>>>),
}

/// `TimestampProblem`.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum TimestampProblem {
    ImprintAlgorithm,
    Imprint,
    MessageDigest,
    Binding,
    Signature,
    Unreadable,
    NoCertificate,
    NoBinding,
    UnsupportedAlgorithm,
}

/// `TimestampCheck`.
#[derive(Clone, Debug, PartialEq)]
pub struct TimestampCheck {
    pub verdict: Cryptography,
    pub problem: Option<TimestampProblem>,
    pub detail: Option<String>,
    pub info: Option<TimestampInfo>,
    /// The token with its authority's certificate, when that was found.
    pub token: Option<Rc<Internal>>,
}

/// The first half of a check: the token read, or the `unreadable` check.
fn begin(
    token: &[u8],
    certificates: &[Certificate],
) -> Result<(CmsParts, TimestampInfo), TimestampCheck> {
    let read = read_cms_parts(token, CmsContent::TstInfo, certificates).and_then(|parts| {
        let info = read_timestamp_info(parts.e_content.as_deref().unwrap_or_default())?;
        Ok((parts, info))
    });
    read.map_err(|error: ParseError| TimestampCheck {
        verdict: Cryptography::Unsupported,
        problem: Some(TimestampProblem::Unreadable),
        detail: Some(error.text()),
        info: None,
        token: None,
    })
}

/// The second half: everything after the stamped digest is known.
/// `algorithm` is the algorithm a `Stamped::Digest` was given under.
fn finish(
    parts: CmsParts,
    info: TimestampInfo,
    algorithm: Option<&str>,
    expected: Option<Vec<u8>>,
) -> TimestampCheck {
    let digest_algorithm = parts.digest_algorithm;
    let message_digest = parts.message_digest.clone();
    let e_content = parts.e_content.clone().unwrap_or_default();
    let token = with_signer_certificate(parts).map(Rc::new);
    let failed = |verdict, problem, detail: String| TimestampCheck {
        verdict,
        problem: Some(problem),
        detail: Some(detail),
        info: Some(info.clone()),
        token: token.clone(),
    };
    if let Some(algorithm) = algorithm
        && info.imprint_algorithm.name() != algorithm
    {
        return failed(
            Cryptography::Invalid,
            TimestampProblem::ImprintAlgorithm,
            format!(
                "the token stamps a {} digest, not the {} digest it was given",
                info.imprint_algorithm.name(),
                algorithm
            ),
        );
    }
    let expected = expected.unwrap_or_default();
    if info.imprint != expected {
        return failed(
            Cryptography::Invalid,
            TimestampProblem::Imprint,
            "the token's message imprint is not the stamped digest".into(),
        );
    }
    if digest_algorithm.hash(&e_content) != message_digest {
        return failed(
            Cryptography::Invalid,
            TimestampProblem::MessageDigest,
            "the token's message digest does not cover its TSTInfo".into(),
        );
    }
    let Some(internal) = &token else {
        return failed(
            Cryptography::Unsupported,
            TimestampProblem::NoCertificate,
            "the token's authority certificate is not carried by the token or by the signature it is in".into(),
        );
    };
    let binding = check_certificate_binding(internal);
    if binding == Binding::Invalid {
        return failed(
            Cryptography::Invalid,
            TimestampProblem::Binding,
            "the token's certificate binding names another certificate than the one that signed it"
                .into(),
        );
    }
    match verify_signer_signature(internal) {
        Cryptography::Invalid => {
            return failed(
                Cryptography::Invalid,
                TimestampProblem::Signature,
                "the token signature does not verify".into(),
            );
        }
        Cryptography::Unsupported => {
            return failed(
                Cryptography::Unsupported,
                TimestampProblem::UnsupportedAlgorithm,
                "the token's signature algorithm is not supported".into(),
            );
        }
        Cryptography::Valid => {}
    }
    match binding {
        Binding::Absent => failed(
            Cryptography::Unsupported,
            TimestampProblem::NoBinding,
            "the token does not bind its authority's certificate, which RFC 3161 requires".into(),
        ),
        Binding::Unsupported => failed(
            Cryptography::Unsupported,
            TimestampProblem::UnsupportedAlgorithm,
            "the token's certificate binding uses a hash that is not supported".into(),
        ),
        _ => TimestampCheck {
            verdict: Cryptography::Valid,
            problem: None,
            detail: None,
            info: Some(info),
            token,
        },
    }
}

/// `checkTimestampToken` for what is known without asking anyone: bytes or
/// a digest. A `Stamped::DigestWith` needs [`check_timestamp_token_async`].
pub fn check_timestamp_token(
    token: &[u8],
    stamped: &Stamped,
    certificates: &[Certificate],
) -> TimestampCheck {
    let (parts, info) = match begin(token, certificates) {
        Ok(read) => read,
        Err(check) => return check,
    };
    match stamped {
        Stamped::Bytes(bytes) => {
            let expected = info.imprint_algorithm.hash(bytes);
            finish(parts, info, None, Some(expected))
        }
        Stamped::Digest { digest, algorithm } => {
            finish(parts, info, Some(algorithm), Some(digest.clone()))
        }
        Stamped::DigestWith(_) => {
            panic!("check_timestamp_token: a DigestWith needs check_timestamp_token_async")
        }
    }
}

/// `checkTimestampToken`: a token checked against what it should stamp.
/// Fails only when a `Stamped::DigestWith` digest fails.
pub async fn check_timestamp_token_async(
    token: &[u8],
    stamped: &Stamped,
    certificates: &[Certificate],
) -> Result<TimestampCheck, Error> {
    let Stamped::DigestWith(digest) = stamped else {
        return Ok(check_timestamp_token(token, stamped, certificates));
    };
    let (parts, info) = match begin(token, certificates) {
        Ok(read) => read,
        Err(check) => return Ok(check),
    };
    let expected = digest(info.imprint_algorithm).await?;
    Ok(finish(parts, info, None, Some(expected)))
}
