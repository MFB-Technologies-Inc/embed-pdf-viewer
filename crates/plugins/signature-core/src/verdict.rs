//! `verdict.ts`: integrity, cryptography, trust, revocation, the
//! timestamp, long-term validation and modifications for every signature
//! of a document.

use std::rc::Rc;

use epdf_kernel::Error;
use epdf_kernel::engine::DocumentHandle;
use serde_json::{Map, Value, json};

use crate::cms::Cryptography;
use crate::judgement::{
    Reading, Requested, TimestampVerdict, judge, judge_token, judgement_members,
    long_term_input_of, read,
};
use crate::long_term::long_term_verdict;
use crate::oids::Digest;
use crate::revocation::fill::{DEFAULT_MAX_CRL_BYTES, fill_pool};
use crate::revocation::pool::{AnswersFrom, ValidationPool, read_validation_pool};
use crate::revocation::source::RevocationSource;
use crate::revocation::status::Status;
use crate::trust::{TrustPort, TrustStatus, ValidationTime};
use crate::verify_timestamp::{Stamped, check_timestamp_token};

/// `ValidateSignaturesOptions`.
#[derive(Clone, Default)]
pub struct ValidateOptions {
    pub trust: Option<Rc<dyn TrustPort>>,
    /// `None` and `Some(Requested::Earliest)` both judge at the earliest
    /// trusted timestamp.
    pub validation_time: Option<Requested>,
    /// Where to ask for the answers the file lacks. Needs `trust`.
    pub revocation: Option<Rc<dyn RevocationSource>>,
    /// `persisted` (default) or `working-copy`.
    pub until: Option<String>,
}

/// JavaScript truthiness of a JSON value (`!signature.signed`).
pub(crate) fn truthy(value: &Value) -> bool {
    match value {
        Value::Null => false,
        Value::Bool(b) => *b,
        Value::Number(n) => n.as_f64().is_some_and(|n| n != 0.0 && !n.is_nan()),
        Value::String(s) => !s.is_empty(),
        _ => !epdf_kernel::is_undefined(value),
    }
}

fn text(value: &Value, key: &str) -> Option<String> {
    value.get(key).and_then(Value::as_str).map(str::to_string)
}

/// Read every signed signature of a document and the pool its validation
/// data and CMSs offer.
pub(crate) async fn read_all(
    doc: &Rc<dyn DocumentHandle>,
) -> Result<(Value, ValidationPool, Vec<Reading>), Error> {
    let snapshot = doc.call("signatures.list", None, vec![]).await?;
    let pool = read_validation_pool(doc).await?;
    let mut readings = Vec::new();
    for signature in
        snapshot.get("signatures").and_then(Value::as_array).cloned().unwrap_or_default()
    {
        if signature.get("signed").is_some_and(truthy) {
            readings.push(read(doc, &snapshot, &signature, &pool).await?);
        }
    }
    Ok((snapshot, pool, readings))
}

/// `validateSignatures`: one verdict per signed signature, as JSON.
///
/// `signatures` is a required member of the current `DocumentHandle`
/// (`DocumentHandle.ts:71`), so the TypeScript's `!doc.signatures` guard
/// never fires; an engine without the service refuses `signatures.list`.
pub async fn validate_signatures(
    doc: Rc<dyn DocumentHandle>,
    options: ValidateOptions,
) -> Result<Value, Error> {
    let (snapshot, pool, readings) = read_all(&doc).await?;
    let trust = options.trust.clone();

    // Long-term validation always speaks for the times the timestamps
    // prove, so that judgement is made whatever was asked for.
    let requested = options.validation_time.clone().unwrap_or(Requested::Earliest);
    if let (Some(source), Some(trust)) = (&options.revocation, &trust) {
        let times = match &requested {
            Requested::Earliest => vec![Requested::Earliest],
            other => vec![Requested::Earliest, other.clone()],
        };
        fill_pool(&readings, trust.clone(), &pool, source.clone(), DEFAULT_MAX_CRL_BYTES, &times)
            .await?;
    }
    let at_timestamps = judge(&readings, trust.clone(), &pool, &Requested::Earliest).await?;
    let judged = match &requested {
        Requested::Earliest => None,
        other => Some(judge(&readings, trust.clone(), &pool, other).await?),
    };
    let judged = judged.as_ref().unwrap_or(&at_timestamps);

    let mut verdicts = Vec::new();
    for (index, reading) in readings.iter().enumerate() {
        let judgement = &judged.judgements[&index];
        let proof = &at_timestamps.judgements[&index];
        let modifications = modifications_of(
            &doc,
            &snapshot,
            &reading.signature,
            options.until.as_deref().unwrap_or("persisted"),
        )
        .await;
        let verdict =
            modifications.get("verdict").and_then(Value::as_str).unwrap_or_default().to_string();
        let revoked =
            judgement.trust == TrustStatus::Trusted && judgement.revocation == Status::Revoked;
        let summary = if reading.integrity == "invalid"
            || reading.cryptography == Cryptography::Invalid
            || verdict == "forbidden"
        {
            "invalid"
        } else if reading.integrity == "indeterminate"
            || reading.cryptography == Cryptography::Unsupported
            || verdict == "indeterminate"
        {
            "indeterminate"
        } else if revoked {
            // Proof that the certificate was withdrawn by the time it is judged at.
            "invalid"
        } else if judgement.trust == TrustStatus::Trusted {
            "valid"
        } else {
            "valid-untrusted"
        };
        let long_term = long_term_verdict(&long_term_input_of(
            reading,
            proof,
            &at_timestamps.document_timestamps,
            &pool,
            // What the file holds: an answer that was only asked for is not in it.
            AnswersFrom::File,
        )?)?;
        let mut out = Map::new();
        out.insert("signature".into(), reading.signature.clone());
        out.insert("integrity".into(), json!(reading.integrity));
        out.insert("cryptography".into(), json!(reading.cryptography.name()));
        judgement_members(judgement, &mut out);
        out.insert("modifications".into(), modifications);
        out.insert("summary".into(), json!(summary));
        out.insert("cms".into(), reading.cms.clone().unwrap_or(Value::Null));
        out.insert(
            "timestamp".into(),
            judgement.timestamp.as_ref().map_or(Value::Null, TimestampVerdict::to_json),
        );
        out.insert("longTerm".into(), long_term.to_json());
        verdicts.push(Value::Object(out));
    }
    Ok(Value::Array(verdicts))
}

/// `verifyTimestampToken`: an RFC 3161 token over any digest, its authority
/// judged now from the answers it carries itself.
pub async fn verify_timestamp_token(
    token: &[u8],
    digest: &[u8],
    algorithm: Digest,
    trust: Option<Rc<dyn TrustPort>>,
) -> Result<Value, Error> {
    let check = check_timestamp_token(
        token,
        &Stamped::Digest { digest: digest.to_vec(), algorithm: algorithm.name().to_string() },
        &[],
    );
    let pool = ValidationPool::new();
    if let Some(token) = &check.token {
        pool.add_carried(&token.certificates, &token.revocation);
    }
    Ok(judge_token(&check, trust, &ValidationTime::Now, &pool, &[]).await?.verdict.to_json())
}

async fn modifications_of(
    doc: &Rc<dyn DocumentHandle>,
    snapshot: &Value,
    signature: &Value,
    until: &str,
) -> Value {
    let revision = signature.get("revisionIndex").and_then(Value::as_f64);
    let Some(revision) = revision else {
        return json!({
            "verdict": "indeterminate",
            "detail": "the signature seals no whole revision",
            "basis": "persisted",
        });
    };
    let revisions = snapshot.get("revisions").and_then(Value::as_array).map_or(0, Vec::len) as f64;
    if until == "persisted" && revision == revisions - 1.0 {
        return json!({ "verdict": "unchanged", "basis": "persisted" });
    }
    let input = json!({ "since": { "signatureIndex": signature.get("index").cloned().unwrap_or(Value::Null) }, "until": until });
    match doc.call("signatures.analyze", None, vec![input]).await {
        Err(error) => json!({
            "verdict": "indeterminate",
            "detail": format!("analysis failed: {}", error.message),
            "basis": "persisted",
        }),
        Ok(analysis) => {
            let current = analysis.get("current").cloned().unwrap_or(Value::Null);
            let primary = current.get("primary").filter(|p| !p.is_null());
            let object =
                |p: &Value| p.get("objectNumber").map_or_else(|| "undefined".into(), value_text);
            let detail = match primary {
                Some(p) if text(p, "verdict").as_deref() == Some("forbidden") => Some(format!(
                    "object {}, {}{}",
                    object(p),
                    p.get("rule").map_or_else(|| "undefined".into(), value_text),
                    text(p, "detail")
                        .filter(|d| !d.is_empty())
                        .map(|d| format!(": {d}"))
                        .unwrap_or_default()
                )),
                Some(p) if text(p, "verdict").as_deref() == Some("incomplete") => Some(format!(
                    "object {}: {}",
                    object(p),
                    text(p, "detail").unwrap_or_else(|| "incomplete evidence".into())
                )),
                _ => None,
            };
            let mut out = Map::new();
            out.insert("verdict".into(), current.get("verdict").cloned().unwrap_or(Value::Null));
            if let Some(detail) = detail {
                out.insert("detail".into(), json!(detail));
            }
            out.insert(
                "basis".into(),
                analysis.get("basis").and_then(|b| b.get("source")).cloned().unwrap_or(Value::Null),
            );
            let later = analysis.get("later").cloned().unwrap_or(Value::Null);
            let count = later.get("revisionCount").and_then(Value::as_f64).unwrap_or(0.0);
            if count > 0.0 {
                out.insert("laterRevisions".into(), later["revisionCount"].clone());
            }
            if later
                .get("undoneObjectNumbers")
                .and_then(Value::as_array)
                .is_some_and(|a| !a.is_empty())
            {
                out.insert("undone".into(), json!(true));
            }
            Value::Object(out)
        }
    }
}

/// A value as a template literal prints it.
fn value_text(value: &Value) -> String {
    match value {
        Value::String(s) => s.clone(),
        Value::Number(n) => crate::number_text(n.as_f64().unwrap_or(0.0)),
        Value::Null => "null".into(),
        other => other.to_string(),
    }
}
