//! `collect.ts`: collecting validation data, asking certificate authorities
//! for the answers a file lacks and keeping them in the file, checked by the
//! one checker so what is stored is what a reader will accept.

use std::rc::Rc;

use epdf_kernel::Error;
use epdf_kernel::bytes::Bytes;
use epdf_kernel::engine::DocumentHandle;
use serde_json::{Value, json};

use crate::judgement::{Reading, Requested, judge, long_term_input_of, read};
use crate::long_term::{LongTermFindings, long_term_findings};
use crate::revocation::answer::{AnswerCheck, AnswerKind};
use crate::revocation::fill::{DEFAULT_MAX_CRL_BYTES, Problems, fill_pool_for};
use crate::revocation::pool::{AnswersFrom, read_validation_pool};
use crate::revocation::source::RevocationSource;
use crate::trust::TrustPort;
use crate::verdict::{ValidateOptions, truthy, validate_signatures};

/// `AddValidationInfoOptions`.
#[derive(Clone, Default)]
pub struct AddValidationInfoOptions {
    pub trust: Option<Rc<dyn TrustPort>>,
    pub revocation: Option<Rc<dyn RevocationSource>>,
    /// Answers and certificates already had, each in DER.
    pub known_certificates: Vec<Vec<u8>>,
    pub known_ocsp_responses: Vec<Vec<u8>>,
    pub known_crls: Vec<Vec<u8>>,
    /// The signature fields to cover (`FormFieldRef` JSON); every one when `None`.
    pub fields: Option<Vec<Value>>,
    pub partial: bool,
    pub max_crl_bytes: Option<usize>,
}

/// DERs in the order a `Map` keyed by their hex keeps them.
#[derive(Default)]
struct Kept(Vec<Vec<u8>>);

impl Kept {
    fn keep(&mut self, der: &[u8]) {
        if !self.0.iter().any(|d| d == der) {
            self.0.push(der.to_vec());
        }
    }

    fn json(&self) -> Value {
        Value::Array(self.0.iter().map(|d| Bytes(d.clone()).to_json()).collect())
    }
}

/// `addValidationInfo`: keep a document's signatures checkable with no
/// network. All or nothing unless `partial`. Answers `{ added, verdicts }`.
pub async fn add_validation_info(
    doc: Rc<dyn DocumentHandle>,
    options: AddValidationInfoOptions,
) -> Result<Value, Error> {
    let Some(trust) = options.trust.clone() else {
        return Err(Error::engine(
            "InvalidArg",
            "addValidationInfo() needs `trust`: the answers kept are the ones for paths you trust",
        )
        .with("details", json!({ "field": "trust" })));
    };
    // The engine's own refusals come first, before any authority is asked.
    doc.call("signatures.addValidationInfo", None, vec![json!({})]).await?;

    let snapshot = doc.call("signatures.list", None, vec![]).await?;
    let pool = read_validation_pool(&doc).await?;
    let mut readings: Vec<Reading> = Vec::new();
    for signature in
        snapshot.get("signatures").and_then(Value::as_array).cloned().unwrap_or_default()
    {
        if signature.get("signed").is_some_and(truthy) {
            readings.push(read(&doc, &snapshot, &signature, &pool).await?);
        }
    }
    let wanted = wanted_among(&readings, options.fields.as_deref())?;
    for der in &options.known_certificates {
        pool.add_certificate_der(der, false);
    }
    for der in &options.known_ocsp_responses {
        pool.add_answer(AnswerKind::Ocsp, der, false);
    }
    for der in &options.known_crls {
        pool.add_answer(AnswerKind::Crl, der, false);
    }

    let is_wanted = |index: usize| wanted.contains(&index);
    let problems = fill_pool_for(
        &readings,
        Some(&is_wanted),
        trust.clone(),
        &pool,
        options.revocation.clone(),
        options.max_crl_bytes.unwrap_or(DEFAULT_MAX_CRL_BYTES),
        &[Requested::Earliest],
    )
    .await?;

    // What a reader would find now, and what it needs in the file to find it again.
    let judged = judge(&readings, Some(trust.clone()), &pool, &Requested::Earliest).await?;
    let (mut certificates, mut ocsp_responses, mut crls) =
        (Kept::default(), Kept::default(), Kept::default());
    for (index, reading) in readings.iter().enumerate() {
        if !wanted.contains(&index) {
            continue;
        }
        let judgement = &judged.judgements[&index];
        let findings = long_term_findings(&long_term_input_of(
            reading,
            judgement,
            &judged.document_timestamps,
            &pool,
            AnswersFrom::Anywhere,
        )?)?;
        if !findings.verdict.enabled && !options.partial {
            return Err(failure_of(reading, &findings, &problems));
        }
        let relied = judgement.relies_on.as_ref().map(|r| &r.path);
        for path in [judgement.path.as_ref(), relied].into_iter().flatten() {
            for der in path {
                certificates.keep(der);
            }
        }
        for row in &findings.rows {
            if let Some(evidence) = row.evidence.as_ref().filter(|e| !e.in_file) {
                match evidence.answer.kind {
                    AnswerKind::Ocsp => ocsp_responses.keep(&evidence.answer.der),
                    AnswerKind::Crl => crls.keep(&evidence.answer.der),
                }
            }
            // A responder that answered in the issuer's place.
            if let Some(responder) = row
                .answer
                .as_deref()
                .and_then(AnswerCheck::window)
                .and_then(|w| w.responder.as_ref())
            {
                certificates.keep(responder);
            }
        }
    }
    let mut added = json!({ "certificates": 0, "ocspResponses": 0, "crls": 0 });
    if certificates.0.len() + ocsp_responses.0.len() + crls.0.len() > 0 {
        let stored = doc
            .call(
                "signatures.addValidationInfo",
                None,
                vec![json!({
                    "certificates": certificates.json(),
                    "ocspResponses": ocsp_responses.json(),
                    "crls": crls.json(),
                })],
            )
            .await?;
        added = stored.get("added").cloned().unwrap_or_else(epdf_kernel::undefined);
    }

    // Read the result the way a reader would: success means the file reads complete.
    let verdicts = validate_signatures(
        doc.clone(),
        ValidateOptions { trust: Some(trust), ..ValidateOptions::default() },
    )
    .await?;
    if !options.partial {
        for (index, reading) in readings.iter().enumerate() {
            if !wanted.contains(&index) {
                continue;
            }
            let verdict = verdicts.as_array().and_then(|list| {
                list.iter().find(|v| v["signature"].get("index") == reading.signature.get("index"))
            });
            let enabled = verdict
                .and_then(|v| v["longTerm"].get("enabled"))
                .and_then(Value::as_bool)
                .unwrap_or(false);
            if !enabled {
                let field = reading.signature.get("fieldName").cloned().unwrap_or(Value::Null);
                let mut details = json!({ "field": field });
                if let Some(missing) = verdict.and_then(|v| v["longTerm"].get("missing")) {
                    details["missing"] = missing.clone();
                }
                return Err(Error::engine(
                    "Unknown",
                    format!(
                        "the validation data was stored, and the signature in \"{}\" still does not read as complete",
                        field.as_str().unwrap_or_default()
                    ),
                )
                .with("details", details));
            }
        }
    }
    Ok(json!({ "added": added, "verdicts": verdicts }))
}

/// `wantedAmong`: the readings `fields` names; every one when it names none.
fn wanted_among(readings: &[Reading], fields: Option<&[Value]>) -> Result<Vec<usize>, Error> {
    let Some(fields) = fields else { return Ok((0..readings.len()).collect()) };
    let mut wanted = Vec::new();
    for field in fields {
        let by_name = field.get("kind").and_then(Value::as_str) == Some("fqn");
        let found = readings.iter().position(|reading| {
            let signature = &reading.signature;
            if by_name {
                signature.get("fieldName") == field.get("name")
            } else {
                signature["field"].get("kind").and_then(Value::as_str) == Some("objectNumber")
                    && signature["field"].get("objectNumber") == field.get("objectNumber")
            }
        });
        let Some(index) = found else {
            let named = if by_name {
                format!("\"{}\"", field.get("name").and_then(Value::as_str).unwrap_or_default())
            } else {
                format!(
                    "object {}",
                    field.get("objectNumber").map(|n| n.to_string()).unwrap_or_default()
                )
            };
            return Err(Error::engine("NotFound", format!("no signature in field {named}"))
                .with("details", json!({ "field": "fields" })));
        };
        if !wanted.contains(&index) {
            wanted.push(index);
        }
    }
    Ok(wanted)
}

/// `failureOf`: the error for a signature that would not read as complete.
fn failure_of(reading: &Reading, findings: &LongTermFindings, problems: &Problems) -> Error {
    let gap = &findings.verdict.missing[0];
    let field = reading.signature.get("fieldName").and_then(Value::as_str).unwrap_or_default();
    let problem = gap.der.as_ref().and_then(|der| problems.get(der));
    if gap.reason == "no-answer"
        && let Some(network) = problem.and_then(|p| p.network.clone())
    {
        return network;
    }
    let named =
        gap.certificate.as_ref().map_or("a certificate".to_string(), |c| format!("\"{c}\""));
    let why = match gap.reason {
        "no-timestamp" => format!(
            "the signature in \"{field}\" has no timestamp you trust, so no time is proven to judge its certificates at"
        ),
        "untrusted" => format!("{named} does not lead to a certificate in `trust`"),
        "revoked" => format!("{named} was withdrawn by the time of the signature's timestamp"),
        _ => format!(
            "no answer that checks out says {named} was good at the signature's timestamp{}",
            problem.map(|p| format!(": {}", p.detail)).unwrap_or_default()
        ),
    };
    Error::engine("SignatureRefused", format!("validation data not added: {why}")).with(
        "details",
        json!({ "reason": gap.reason, "certificate": gap.certificate, "field": field }),
    )
}
