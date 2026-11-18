//! `cms/attach.ts`: timestamp a finished CMS signature, the token over its
//! signature value added as an unsigned attribute, every other byte kept.

use std::rc::Rc;

use epdf_kernel::Error;
use serde_json::json;

use crate::cert::Certificate;
use crate::cms::Cryptography;
use crate::oids::Digest;
use crate::parse::{CmsContent, read_cms_parts};
use crate::sign::{CmsSignRequest, CmsSigner, signing_refused};
use crate::splice::{add_unsigned_attribute, encode_element};
use crate::trust::{TrustPort, TrustStatus, ValidationTime, validate_timestamp_authority};
use crate::verify_timestamp::{
    Stamped, TimestampCheck, TimestampProblem, check_timestamp_token, signature_value_of,
};

/// id-aa-signatureTimeStampToken (1.2.840.113549.1.9.16.2.14), as DER.
const TIMESTAMP_TOKEN_OID: [u8; 13] =
    [0x06, 0x0b, 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x09, 0x10, 0x02, 0x0e];

/// `attachSignatureTimestamp`: ask `authority` for a token over the
/// signature value, check it, and add it to the CMS.
pub async fn attach_signature_timestamp(
    cms: &[u8],
    authority: &CmsSigner,
    trust: Option<Rc<dyn TrustPort>>,
) -> Result<Vec<u8>, Error> {
    let parts = read_cms_parts(cms, CmsContent::Detached, &[])
        .map_err(|e| signing_refused("malformed", &e.text()))?;
    if !parts.timestamp_tokens.is_empty() {
        return Err(Error::engine(
            "InvalidArg",
            "the signature already carries a timestamp, from the service that signed it; leave out `timestampAuthority`",
        )
        .with("details", json!({ "field": "timestampAuthority" })));
    }
    let algorithm = authority.hash.unwrap_or(Digest::Sha256);
    let digest = algorithm.hash(&signature_value_of(&parts.signer_info));
    let token = (authority.sign)(CmsSignRequest {
        digest: digest.clone(),
        algorithm: algorithm.name().into(),
        sub_filter: "ETSI.RFC3161".into(),
    })
    .await?;
    let check = check_timestamp_token(
        &token,
        &Stamped::Digest { digest, algorithm: algorithm.name().into() },
        &parts.certificates,
    );
    if check.verdict != Cryptography::Valid {
        let problem = check.problem.unwrap_or(TimestampProblem::Signature);
        return Err(signing_refused(
            refusal_for(problem),
            &format!("the timestamp: {}", detail_for(&check)),
        ));
    }
    if let Some(trust) = trust {
        require_trusted_authority(&check, trust, &parts.certificates).await?;
    }
    add_unsigned_attribute(cms, &timestamp_token_attribute(&token))
        .map_err(|e| signing_refused("malformed", &e.text()))
}

/// `requireTrustedAuthority`: refuse a token whose authority `trust` doesn't
/// cover, with `details.trust` and `details.trustReason`.
pub async fn require_trusted_authority(
    check: &TimestampCheck,
    trust: Rc<dyn TrustPort>,
    certificates: &[Certificate],
) -> Result<(), Error> {
    let (Some(token), Some(info)) = (&check.token, &check.info) else {
        return Err(Error::named("TypeError", "Cannot read properties of null (reading 'parsed')"));
    };
    let judged = validate_timestamp_authority(
        token,
        certificates,
        Some(trust),
        &ValidationTime::Now,
        info.gen_time,
    )
    .await?;
    if judged.status == TrustStatus::Trusted {
        return Ok(());
    }
    let name = crate::signers::certificate_common_name(&token.parsed.signer_certificate)?;
    Err(Error::engine(
        "SignatureRefused",
        format!(
            "the timestamp's authority{} isn't trusted: {}. Add its root to `timestampAnchors`",
            name.map(|n| format!(" (\"{n}\")")).unwrap_or_default(),
            judged.reason.clone().unwrap_or_else(|| "no path to a timestamp anchor".into())
        ),
    )
    .with("details", json!({ "trust": judged.status.name(), "trustReason": judged.reason })))
}

/// `timestampTokenAttribute`: the unsigned attribute a signature carries its
/// token in, as DER.
pub fn timestamp_token_attribute(token: &[u8]) -> Vec<u8> {
    let mut content = TIMESTAMP_TOKEN_OID.to_vec();
    content.extend(encode_element(0x31, token));
    encode_element(0x30, &content)
}

/// `refusalFor`: a token asked for must verify, or it is refused.
fn refusal_for(problem: TimestampProblem) -> &'static str {
    match problem {
        TimestampProblem::Unreadable => "malformed",
        TimestampProblem::Imprint => "digest-mismatch",
        TimestampProblem::ImprintAlgorithm => "algorithm-mismatch",
        _ => "signature-invalid",
    }
}

fn detail_for(check: &TimestampCheck) -> String {
    let detail = check.detail.clone().unwrap_or_default();
    if check.problem == Some(TimestampProblem::Unreadable) {
        format!(
            "`timestampAuthority` didn't return an RFC 3161 token ({detail}): is it a signing service?"
        )
    } else {
        detail
    }
}
