//! `judgement.ts`: reading a document's signatures and judging the
//! certificates behind them. A reading is what a signature says before any
//! certificate is judged; a judgement is its trust and its revocation at a
//! time.

use std::collections::HashMap;
use std::rc::Rc;

use epdf_kernel::Error;
use epdf_kernel::bytes::Bytes;
use epdf_kernel::engine::DocumentHandle;
use epdf_kernel::rt::Promise;
use serde_json::{Map, Value, json};

use crate::cert::Certificate;
use crate::cms::{Cryptography, verify_parsed};
use crate::long_term::{LongTermInput, ProvenDocumentTimestamp, ProvenTime};
use crate::oids::Digest;
use crate::parse::{Internal, ParseError, bytes_json, parse_cms_internal};
use crate::revocation::pool::{AnswersFrom, ValidationPool, path_revocation, summarize_revocation};
use crate::revocation::status::Status;
use crate::trust::{
    CmsInput, TrustPort, TrustStatus, ValidationTime, validate_chain_among,
    validate_timestamp_authority,
};
use crate::verify_timestamp::{
    Stamped, TimestampCheck, TimestampProblem, check_timestamp_token, check_timestamp_token_async,
    signature_value_of,
};

/// What `judge` is asked for: a time, or `earliest-timestamp`.
#[derive(Clone, Debug, PartialEq)]
pub enum Requested {
    Earliest,
    At(ValidationTime),
}

/// `TimestampVerdict`: what an RFC 3161 token is worth.
#[derive(Clone, Debug, PartialEq)]
pub struct TimestampVerdict {
    pub cryptography: Cryptography,
    pub trust: TrustStatus,
    pub trust_reason: Option<String>,
    pub validation_time: ValidationTime,
    pub revocation: Status,
    pub revocation_reason: Option<String>,
    pub authority_name: Option<String>,
    /// `valid`, `valid-untrusted`, `invalid` or `indeterminate`.
    pub summary: &'static str,
    pub stamped_at: Option<String>,
    pub authority_certificate: Option<Vec<u8>>,
}

impl TimestampVerdict {
    pub fn to_json(&self) -> Value {
        json!({
            "cryptography": self.cryptography.name(),
            "trust": self.trust.name(),
            "trustReason": self.trust_reason,
            "validationTime": self.validation_time.to_json(),
            "revocation": self.revocation.name(),
            "revocationReason": self.revocation_reason,
            "authorityName": self.authority_name,
            "summary": self.summary,
            "stampedAt": self.stamped_at,
            "authorityCertificate": self.authority_certificate.as_deref().map_or(Value::Null, bytes_json),
        })
    }
}

/// `Reading`: what one signature says before any certificate is judged.
#[derive(Clone, Debug)]
pub struct Reading {
    pub signature: Value,
    pub is_timestamp: bool,
    /// `valid`, `invalid` or `indeterminate`.
    pub integrity: &'static str,
    pub cryptography: Cryptography,
    pub internal: Option<Rc<Internal>>,
    pub cms: Option<Value>,
    pub tokens: Vec<TimestampCheck>,
    pub token: Option<TimestampCheck>,
}

impl Reading {
    pub fn revision_index(&self) -> Option<f64> {
        self.signature.get("revisionIndex").and_then(Value::as_f64)
    }

    /// The common name of the CMS's signer, for a gap about the signature.
    fn subject(&self) -> Result<Option<String>, Error> {
        let der = self
            .cms
            .as_ref()
            .and_then(|cms| Bytes::from_json(&cms["signerCertificate"]))
            .map(|b| b.0);
        match der {
            Some(der) => crate::signers::certificate_common_name(&der),
            None => Ok(None),
        }
    }
}

/// `Judgement`: what judging a signature's certificates adds to its reading.
#[derive(Clone, Debug)]
pub struct Judgement {
    pub trust: TrustStatus,
    pub trust_reason: Option<String>,
    pub validation_time: ValidationTime,
    pub path: Option<Vec<Vec<u8>>>,
    pub revocation: Status,
    pub revocation_reason: Option<String>,
    pub timestamp: Option<TimestampVerdict>,
    pub relies_on: Option<ProvenTime>,
    /// `no-timestamp` or `untrusted`.
    pub without_time: &'static str,
    pub timestamp_path: Option<Vec<Vec<u8>>>,
    pub without_issuer: Vec<Vec<u8>>,
}

/// `Judged`: every reading judged (by index), and the document timestamps
/// that proved a time.
pub struct Judged {
    pub judgements: HashMap<usize, Judgement>,
    pub document_timestamps: Vec<ProvenDocumentTimestamp>,
}

/// `JudgedToken`.
#[derive(Clone, Debug)]
pub struct JudgedToken {
    pub verdict: TimestampVerdict,
    pub path: Option<Vec<Vec<u8>>>,
    pub without_issuer: Option<Vec<u8>>,
}

const NO_ISSUER: &str =
    "the signer is not trusted, so there is no issuer to check an answer against";

fn timestamp_time(stamped_at: &str) -> ValidationTime {
    ValidationTime::Timestamp { stamped_at: stamped_at.to_string() }
}

/// `provenTimeOf`: the time a judged token proves, when it is `valid`.
fn proven_time_of(token: &JudgedToken) -> Option<ProvenTime> {
    if token.verdict.summary != "valid" {
        return None;
    }
    Some(ProvenTime {
        gen_time: token.verdict.stamped_at.clone()?,
        path: token.path.clone()?,
        authority_certificate: token.verdict.authority_certificate.clone()?,
    })
}

/// `judge`: the certificates behind every reading, each timestamp's
/// authority first, then each signer.
pub async fn judge(
    readings: &[Reading],
    trust: Option<Rc<dyn TrustPort>>,
    pool: &ValidationPool,
    requested: &Requested,
) -> Result<Judged, Error> {
    let earliest = *requested == Requested::Earliest;
    let mut proven: Vec<ProvenDocumentTimestamp> = Vec::new();
    let covered_by = |proven: &[ProvenDocumentTimestamp], revision: Option<f64>| {
        let revision = revision.filter(|_| earliest)?;
        proven
            .iter()
            .filter(|s| s.revision_index > revision)
            .fold(None, |first: Option<&ProvenDocumentTimestamp>, stamp| match first {
                Some(first) if stamp.time.gen_time >= first.time.gen_time => Some(first),
                _ => Some(stamp),
            })
            .cloned()
    };

    let mut judgements = HashMap::new();
    let mut stamps: Vec<usize> =
        (0..readings.len()).filter(|&i| readings[i].is_timestamp).collect();
    stamps.sort_by(|&a, &b| {
        let key = |i: usize| readings[i].revision_index().unwrap_or(-1.0);
        key(b).partial_cmp(&key(a)).unwrap_or(std::cmp::Ordering::Equal)
    });
    for index in stamps {
        let reading = &readings[index];
        let covered = covered_by(&proven, reading.revision_index());
        let at = covered.as_ref().map_or(ValidationTime::Now, |c| timestamp_time(&c.time.gen_time));
        let token = match &reading.token {
            Some(check) => Some(judge_token(check, trust.clone(), &at, pool, &[]).await?),
            None => None,
        };
        let timestamp = token.as_ref().map(|t| t.verdict.clone());
        let time = token.as_ref().filter(|_| reading.integrity == "valid").and_then(proven_time_of);
        if let (Some(time), Some(revision_index)) = (&time, reading.revision_index()) {
            proven.push(ProvenDocumentTimestamp { time: time.clone(), revision_index });
        }
        let sound = timestamp
            .as_ref()
            .is_some_and(|t| reading.integrity == "valid" && t.cryptography == Cryptography::Valid);
        judgements.insert(
            index,
            Judgement {
                trust: timestamp.as_ref().map_or(TrustStatus::Unknown, |t| t.trust),
                trust_reason: timestamp.as_ref().and_then(|t| t.trust_reason.clone()),
                validation_time: at,
                path: token.as_ref().and_then(|t| t.path.clone()),
                revocation: timestamp.as_ref().map_or(Status::Unknown, |t| t.revocation),
                revocation_reason: timestamp.as_ref().and_then(|t| t.revocation_reason.clone()),
                timestamp,
                relies_on: time,
                without_time: if sound { "untrusted" } else { "no-timestamp" },
                timestamp_path: None,
                without_issuer: token.and_then(|t| t.without_issuer).into_iter().collect(),
            },
        );
    }

    for (index, reading) in readings.iter().enumerate() {
        if reading.is_timestamp {
            continue;
        }
        let covered = covered_by(&proven, reading.revision_index());
        let token_at =
            covered.as_ref().map_or(ValidationTime::Now, |c| timestamp_time(&c.time.gen_time));
        let certificates =
            reading.internal.as_ref().map(|i| i.certificates.clone()).unwrap_or_default();
        let mut tokens = Vec::new();
        for check in &reading.tokens {
            tokens.push(judge_token(check, trust.clone(), &token_at, pool, &certificates).await?);
        }
        let own = earliest_valid(&tokens);
        let chosen = own.or_else(|| tokens.first());
        let timestamp = chosen.map(|t| t.verdict.clone());
        let own_time = own.and_then(proven_time_of);
        let relies_on = if earliest {
            match (own_time, covered.map(|c| c.time)) {
                (Some(own), Some(covered)) => {
                    Some(if own.gen_time <= covered.gen_time { own } else { covered })
                }
                (own, covered) => own.or(covered),
            }
        } else {
            None
        };
        let at = match requested {
            Requested::Earliest => {
                relies_on.as_ref().map_or(ValidationTime::Now, |r| timestamp_time(&r.gen_time))
            }
            Requested::At(time) => time.clone(),
        };
        let chain = match &reading.internal {
            Some(internal) => Some(
                validate_chain_among(
                    CmsInput::Parsed(internal.clone()),
                    trust.clone(),
                    &at,
                    &pool.certificates(),
                )
                .await?,
            ),
            None => None,
        };
        let trusted_path = chain
            .as_ref()
            .filter(|c| c.status == TrustStatus::Trusted)
            .and_then(|c| c.path.clone());
        let (revocation, revocation_reason) = match &trusted_path {
            Some(path) => summarize_revocation(
                &path_revocation(pool, path, at.time(), AnswersFrom::Anywhere)?,
                &at.name(),
            ),
            None => (Status::Unknown, Some(NO_ISSUER.to_string())),
        };
        let mut without_issuer: Vec<Vec<u8>> =
            chain.as_ref().and_then(|c| c.without_issuer.clone()).into_iter().collect();
        without_issuer.extend(tokens.iter().filter_map(|t| t.without_issuer.clone()));
        judgements.insert(
            index,
            Judgement {
                trust: chain.as_ref().map_or(TrustStatus::Unknown, |c| c.status),
                trust_reason: chain.as_ref().and_then(|c| c.reason.clone()),
                validation_time: at,
                path: trusted_path,
                revocation,
                revocation_reason,
                timestamp,
                relies_on,
                without_time: "no-timestamp",
                timestamp_path: chosen.and_then(|t| t.path.clone()),
                without_issuer,
            },
        );
    }
    Ok(Judged { judgements, document_timestamps: proven })
}

/// `earliestValid`: the earliest of the tokens whose verdict is `valid`.
fn earliest_valid(tokens: &[JudgedToken]) -> Option<&JudgedToken> {
    let mut earliest: Option<&JudgedToken> = None;
    for token in tokens {
        if token.verdict.summary != "valid" {
            continue;
        }
        if earliest.is_none_or(|e| token.verdict.stamped_at < e.verdict.stamped_at) {
            earliest = Some(token);
        }
    }
    earliest
}

/// `longTermInputOf`.
pub fn long_term_input_of(
    reading: &Reading,
    at_timestamps: &Judgement,
    document_timestamps: &[ProvenDocumentTimestamp],
    pool: &ValidationPool,
    answers_from: AnswersFrom,
) -> Result<LongTermInput, Error> {
    Ok(LongTermInput {
        is_timestamp: reading.is_timestamp,
        revision_index: reading.revision_index(),
        subject: reading.subject()?,
        relies_on: at_timestamps.relies_on.clone(),
        without_time: at_timestamps.without_time,
        signer_trusted: at_timestamps.trust == TrustStatus::Trusted,
        signer_path: at_timestamps.path.clone(),
        document_timestamps: document_timestamps.to_vec(),
        pool: pool.clone(),
        answers_from,
    })
}

fn field_of(signature: &Value) -> Value {
    signature.get("field").cloned().unwrap_or(Value::Null)
}

/// `read`: one signature, and what its CMS carries offered to `pool`.
pub async fn read(
    doc: &Rc<dyn DocumentHandle>,
    snapshot: &Value,
    signature: &Value,
    pool: &ValidationPool,
) -> Result<Reading, Error> {
    let mut reading = Reading {
        signature: signature.clone(),
        is_timestamp: signature.get("kind").and_then(Value::as_str) == Some("timestamp"),
        integrity: "indeterminate",
        cryptography: Cryptography::Unsupported,
        internal: None,
        cms: None,
        tokens: Vec::new(),
        token: None,
    };
    let chain_valid = snapshot.get("chainValid").and_then(Value::as_bool) == Some(true);
    if !chain_valid || signature.get("coverage").and_then(Value::as_str) == Some("malformed") {
        reading.integrity = if chain_valid { "invalid" } else { "indeterminate" };
        return Ok(reading);
    }
    if reading.is_timestamp {
        read_document_timestamp(doc, &mut reading).await?;
    } else {
        read_signature(doc, &mut reading).await;
    }
    if let Some(internal) = &reading.internal {
        pool.add_carried(&internal.certificates, &internal.revocation);
    }
    for check in reading.tokens.iter().chain(reading.token.as_ref()) {
        if let Some(token) = &check.token {
            pool.add_carried(&token.certificates, &token.revocation);
        }
    }
    Ok(reading)
}

fn whole_revision(reading: &Reading) -> bool {
    reading.signature.get("coverage").and_then(Value::as_str) == Some("whole-revision")
}

async fn read_signature(doc: &Rc<dyn DocumentHandle>, reading: &mut Reading) {
    let field = field_of(&reading.signature);
    let outcome: Result<Rc<Internal>, Option<ParseError>> = async {
        let contents = doc
            .call("signatures.getContents", None, vec![field.clone()])
            .await
            .map_err(|_| None)?;
        let bytes = Bytes::from_json(&contents).map(|b| b.0).unwrap_or_default();
        let internal = Rc::new(parse_cms_internal(&bytes).map_err(Some)?);
        reading.cms = Some(internal.parsed.to_json());
        let digest = doc
            .call(
                "signatures.getDigest",
                None,
                vec![field, json!(internal.parsed.digest_algorithm.name())],
            )
            .await
            .map_err(|_| None)?;
        let digest = Bytes::from_json(&digest).map(|b| b.0).unwrap_or_default();
        let matches = digest == internal.parsed.message_digest;
        reading.integrity = if whole_revision(reading) && matches { "valid" } else { "invalid" };
        Ok(internal)
    }
    .await;
    let internal = match outcome {
        Ok(internal) => internal,
        Err(error) => {
            // A CMS this engine cannot read is not evidence of tampering.
            reading.integrity = match error {
                Some(ParseError::Cms { reason: "unsupported", .. }) => "indeterminate",
                Some(ParseError::Cms { .. }) => "invalid",
                _ => "indeterminate",
            };
            return;
        }
    };
    reading.cryptography = verify_parsed(&internal);
    let signature_value = signature_value_of(&internal.signer_info);
    for token in &internal.timestamp_tokens {
        reading.tokens.push(check_timestamp_token(
            token,
            &Stamped::Bytes(signature_value.clone()),
            &internal.certificates,
        ));
    }
    reading.internal = Some(internal);
}

/// `readDocumentTimestamp`: `integrity` is whether the token stamps the
/// bytes it seals, `cryptography` whether the token itself is sound.
async fn read_document_timestamp(
    doc: &Rc<dyn DocumentHandle>,
    reading: &mut Reading,
) -> Result<(), Error> {
    let field = field_of(&reading.signature);
    let Ok(contents) = doc.call("signatures.getContents", None, vec![field.clone()]).await else {
        return Ok(());
    };
    let contents = Bytes::from_json(&contents).map(|b| b.0).unwrap_or_default();
    let digest_doc = doc.clone();
    let stamped = Stamped::DigestWith(Rc::new(move |algorithm: Digest| {
        let doc = digest_doc.clone();
        let field = field.clone();
        Promise::spawn(async move {
            let digest = doc
                .call("signatures.getDigest", None, vec![field, json!(algorithm.name())])
                .await?;
            Ok(Bytes::from_json(&digest).map(|b| b.0).unwrap_or_default())
        })
    }));
    // The engine couldn't hash the sealed bytes: nothing is known about the token.
    let Ok(check) = check_timestamp_token_async(&contents, &stamped, &[]).await else {
        return Ok(());
    };
    reading.cms = check.token.as_ref().map(|t| t.parsed.to_json());
    if check.problem == Some(TimestampProblem::Unreadable) {
        reading.token = Some(check);
        return Ok(());
    }
    let stamps_these_bytes = check.problem != Some(TimestampProblem::Imprint);
    reading.integrity =
        if whole_revision(reading) && stamps_these_bytes { "valid" } else { "invalid" };
    // A token over other bytes may be sound in itself: judge it against the
    // digest it does stamp.
    reading.cryptography = if stamps_these_bytes {
        check.verdict
    } else {
        let info = check.info.as_ref().expect("an imprint problem has the info");
        check_timestamp_token(
            &contents,
            &Stamped::Digest {
                digest: info.imprint.clone(),
                algorithm: info.imprint_algorithm.name().to_string(),
            },
            &[],
        )
        .verdict
    };
    reading.token = Some(check);
    Ok(())
}

/// `judgeToken`: a checked token, with its authority judged at `at`.
pub async fn judge_token(
    check: &TimestampCheck,
    trust: Option<Rc<dyn TrustPort>>,
    at: &ValidationTime,
    pool: &ValidationPool,
    certificates: &[Certificate],
) -> Result<JudgedToken, Error> {
    let gen_time = match &check.info {
        Some(info) => Some(
            crate::der::iso_string(info.gen_time)
                .ok_or_else(|| Error::named("RangeError", "Invalid time value"))?,
        ),
        None => None,
    };
    let authority_certificate = check.token.as_ref().map(|t| t.parsed.signer_certificate.clone());
    let mut status = TrustStatus::Unknown;
    let mut reason = check.detail.clone();
    let mut path = None;
    let mut without_issuer = None;
    if let (Some(token), Some(info)) = (&check.token, &check.info) {
        let mut offered = certificates.to_vec();
        offered.extend(pool.certificates());
        let authority =
            validate_timestamp_authority(token, &offered, trust, at, info.gen_time).await?;
        status = authority.status;
        reason = authority.reason.clone();
        path = if authority.status == TrustStatus::Trusted { authority.path.clone() } else { None };
        without_issuer = authority.without_issuer.clone();
    }
    let (revocation, revocation_reason) = match &path {
        Some(path) => summarize_revocation(
            &path_revocation(pool, path, at.time(), AnswersFrom::Anywhere)?,
            &at.name(),
        ),
        None => (
            Status::Unknown,
            Some(
                "the authority is not trusted, so there is no issuer to check an answer against"
                    .to_string(),
            ),
        ),
    };
    let authority_name = match &authority_certificate {
        Some(der) => crate::signers::certificate_common_name(der)?,
        None => None,
    };
    let revoked = status == TrustStatus::Trusted && revocation == Status::Revoked;
    let summary = if check.verdict == Cryptography::Valid
        && !revoked
        && gen_time.is_some()
        && authority_certificate.is_some()
    {
        if status == TrustStatus::Trusted { "valid" } else { "valid-untrusted" }
    } else if check.verdict == Cryptography::Invalid || revoked {
        "invalid"
    } else {
        "indeterminate"
    };
    Ok(JudgedToken {
        verdict: TimestampVerdict {
            cryptography: check.verdict,
            trust: status,
            trust_reason: reason,
            validation_time: at.clone(),
            revocation,
            revocation_reason,
            authority_name,
            summary,
            stamped_at: gen_time,
            authority_certificate,
        },
        path,
        without_issuer,
    })
}

/// The verdict of a reading's judgement for JSON (`trustReason` and
/// `revocationReason` only when present, as the TypeScript spreads them).
pub fn judgement_members(judgement: &Judgement, out: &mut Map<String, Value>) {
    out.insert("trust".into(), json!(judgement.trust.name()));
    if let Some(reason) = &judgement.trust_reason {
        out.insert("trustReason".into(), json!(reason));
    }
    out.insert("validationTime".into(), judgement.validation_time.to_json());
    out.insert("revocation".into(), json!(judgement.revocation.name()));
    if let Some(reason) = judgement.revocation_reason.as_ref().filter(|r| !r.is_empty()) {
        out.insert("revocationReason".into(), json!(reason));
    }
}
