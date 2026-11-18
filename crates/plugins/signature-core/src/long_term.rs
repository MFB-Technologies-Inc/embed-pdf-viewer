//! `long-term.ts`: whether a file holds everything a reader needs to check
//! a signature with no network ("LTV enabled"), judged at the time a
//! timestamp proves.

use epdf_kernel::Error;
use serde_json::{Value, json};

use crate::cert::Certificate;
use crate::revocation::pool::{
    AnswersFrom, CertificateRevocation, ValidationPool, path_revocation,
};
use crate::revocation::status::Status;

/// `ProvenTime`: a timestamp whose token verifies and whose authority is
/// trusted.
#[derive(Clone, Debug, PartialEq)]
pub struct ProvenTime {
    pub gen_time: String,
    /// The authority's validated path, anchor last.
    pub path: Vec<Vec<u8>>,
    pub authority_certificate: Vec<u8>,
}

/// `ProvenDocumentTimestamp`.
#[derive(Clone, Debug, PartialEq)]
pub struct ProvenDocumentTimestamp {
    pub time: ProvenTime,
    pub revision_index: f64,
}

/// `LongTermGap` with the DER of the certificate it is about, when it is.
#[derive(Clone, Debug, PartialEq)]
pub struct LongTermGap {
    pub certificate: Option<String>,
    /// `no-timestamp`, `untrusted`, `no-answer` or `revoked`.
    pub reason: &'static str,
    pub der: Option<Vec<u8>>,
}

/// `LongTermVerdict`.
#[derive(Clone, Debug, PartialEq)]
pub struct LongTermVerdict {
    pub enabled: bool,
    pub proven_at: Option<String>,
    pub expires_at: Option<String>,
    pub document_timestamped_at: Option<String>,
    pub missing: Vec<LongTermGap>,
}

impl LongTermVerdict {
    pub fn to_json(&self) -> Value {
        json!({
            "enabled": self.enabled,
            "provenAt": self.proven_at,
            "expiresAt": self.expires_at,
            "documentTimestampedAt": self.document_timestamped_at,
            "missing": self.missing.iter().map(|gap| json!({
                "certificate": gap.certificate,
                "reason": gap.reason,
            })).collect::<Vec<_>>(),
        })
    }
}

/// `LongTermInput`.
#[derive(Clone)]
pub struct LongTermInput {
    /// A signature's signer path is asked about too; a document timestamp's
    /// is only its authority's.
    pub is_timestamp: bool,
    pub revision_index: Option<f64>,
    pub subject: Option<String>,
    pub relies_on: Option<ProvenTime>,
    /// `no-timestamp` or `untrusted`.
    pub without_time: &'static str,
    pub signer_trusted: bool,
    pub signer_path: Option<Vec<Vec<u8>>>,
    pub document_timestamps: Vec<ProvenDocumentTimestamp>,
    pub pool: ValidationPool,
    pub answers_from: AnswersFrom,
}

/// `LongTermFindings`: the verdict with what it was worked out from.
#[derive(Clone, Debug)]
pub struct LongTermFindings {
    pub verdict: LongTermVerdict,
    pub rows: Vec<CertificateRevocation>,
}

pub fn long_term_verdict(input: &LongTermInput) -> Result<LongTermVerdict, Error> {
    Ok(long_term_findings(input)?.verdict)
}

/// The latest of some times by their ISO text, the first on a tie.
fn last_of<'a>(stamps: impl Iterator<Item = &'a ProvenTime>) -> Option<&'a ProvenTime> {
    stamps.fold(None, |last: Option<&ProvenTime>, stamp| match last {
        Some(last) if stamp.gen_time <= last.gen_time => Some(last),
        _ => Some(stamp),
    })
}

/// `longTermFindings`.
pub fn long_term_findings(input: &LongTermInput) -> Result<LongTermFindings, Error> {
    let covering: Vec<&ProvenDocumentTimestamp> = match input.revision_index {
        None => Vec::new(),
        Some(index) => {
            input.document_timestamps.iter().filter(|s| s.revision_index > index).collect()
        }
    };
    let document_timestamped_at =
        last_of(covering.iter().map(|s| &s.time)).map(|t| t.gen_time.clone());
    let without = |proven_at: Option<String>, gaps: Vec<LongTermGap>, rows| LongTermFindings {
        verdict: LongTermVerdict {
            enabled: false,
            proven_at,
            expires_at: None,
            document_timestamped_at: document_timestamped_at.clone(),
            missing: gaps,
        },
        rows,
    };

    let Some(proven) = &input.relies_on else {
        return Ok(without(
            None,
            vec![LongTermGap {
                certificate: input.subject.clone(),
                reason: input.without_time,
                der: None,
            }],
            Vec::new(),
        ));
    };
    if !input.is_timestamp && (!input.signer_trusted || input.signer_path.is_none()) {
        return Ok(without(
            Some(proven.gen_time.clone()),
            vec![LongTermGap {
                certificate: input.subject.clone(),
                reason: "untrusted",
                der: None,
            }],
            Vec::new(),
        ));
    }

    let at = crate::der::date_from_iso(&proven.gen_time);
    let mut all = Vec::new();
    if !input.is_timestamp {
        all.extend(path_revocation(
            &input.pool,
            input.signer_path.as_deref().unwrap_or_default(),
            at,
            input.answers_from,
        )?);
    }
    all.extend(path_revocation(&input.pool, &proven.path, at, input.answers_from)?);
    // A certificate on both paths is asked about once.
    let mut rows: Vec<CertificateRevocation> = Vec::new();
    for row in all {
        if !rows.iter().any(|r| r.certificate == row.certificate) {
            rows.push(row);
        }
    }
    let gaps: Vec<LongTermGap> = rows
        .iter()
        .filter(|row| row.status != Status::Good)
        .map(|row| LongTermGap {
            certificate: row.name.clone(),
            reason: if row.status == Status::Revoked { "revoked" } else { "no-answer" },
            der: Some(row.certificate.clone()),
        })
        .collect();
    if !gaps.is_empty() {
        return Ok(without(Some(proven.gen_time.clone()), gaps, rows));
    }
    let last = last_of(std::iter::once(proven).chain(covering.iter().map(|s| &s.time)))
        .expect("the proven time");
    let authority = Certificate::from_ber(&last.authority_certificate)?;
    Ok(LongTermFindings {
        verdict: LongTermVerdict {
            enabled: true,
            proven_at: Some(proven.gen_time.clone()),
            expires_at: crate::der::iso_string(authority.not_after),
            document_timestamped_at,
            missing: Vec::new(),
        },
        rows,
    })
}
