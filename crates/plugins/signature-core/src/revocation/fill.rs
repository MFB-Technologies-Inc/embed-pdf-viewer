//! `revocation/fill.ts`: asking certificate authorities for what a file
//! lacks, an answer for every certificate that has none at the time it is
//! judged at, and the certificate of an issuer a path is missing.

use std::collections::HashSet;
use std::rc::Rc;

use epdf_kernel::Error;
use serde_json::json;

use super::addresses::revocation_addresses;
use super::answer::{AnswerCheck, AnswerKind, RevocationAnswer, check_revocation_answer};
use super::pool::{AnswersFrom, CertificateRevocation, ValidationPool, path_revocation};
use super::source::{RevocationRequest, RevocationSource, build_ocsp_request};
use super::status::Status;
use crate::cert::Certificate;
use crate::judgement::{Judgement, Reading, Requested, judge};
use crate::trust::TrustPort;

/// The largest list of withdrawn certificates that is stored by default.
pub const DEFAULT_MAX_CRL_BYTES: usize = 1024 * 1024;

/// How many times the paths are judged again after something new was learned.
const MAX_ROUNDS: usize = 8;

/// `AskingProblem`: why a certificate still has no answer after asking.
#[derive(Clone, Debug)]
pub struct AskingProblem {
    pub detail: String,
    /// The error of an address that did not answer, when that happened.
    pub network: Option<Error>,
}

/// What could not be had, by the DER of the certificate, in the order a
/// `Map` keeps: a key set again keeps its place.
#[derive(Clone, Debug, Default)]
pub struct Problems(pub Vec<(Vec<u8>, AskingProblem)>);

impl Problems {
    fn insert(&mut self, key: Vec<u8>, problem: AskingProblem) {
        match self.0.iter_mut().find(|(k, _)| *k == key) {
            Some(entry) => entry.1 = problem,
            None => self.0.push((key, problem)),
        }
    }

    fn remove(&mut self, key: &[u8]) {
        self.0.retain(|(k, _)| k != key);
    }

    pub fn get(&self, key: &[u8]) -> Option<&AskingProblem> {
        self.0.iter().find(|(k, _)| k == key).map(|(_, p)| p)
    }
}

/// `fillPool`: ask `source` for what the pool lacks, until nothing new
/// comes. What could not be had comes back by the DER of the certificate.
/// `wanted` picks the readings to work for (by index); every one when
/// `None`.
pub async fn fill_pool_for(
    readings: &[Reading],
    wanted: Option<&dyn Fn(usize) -> bool>,
    trust: Rc<dyn TrustPort>,
    pool: &ValidationPool,
    source: Option<Rc<dyn RevocationSource>>,
    max_crl_bytes: usize,
    times: &[Requested],
) -> Result<Problems, Error> {
    let mut problems = Problems::default();
    let mut asked: HashSet<Vec<u8>> = HashSet::new();
    let mut issuers_asked: HashSet<Vec<u8>> = HashSet::new();

    for _ in 0..MAX_ROUNDS {
        let mut learned = false;
        for time in times {
            let judged = judge(readings, Some(trust.clone()), pool, time).await?;
            for (index, reading) in readings.iter().enumerate() {
                if wanted.is_some_and(|wanted| !wanted(index)) {
                    continue;
                }
                let judgement = &judged.judgements[&index];
                for orphan in &judgement.without_issuer {
                    if fetch_issuer_of(orphan, &mut issuers_asked, source.as_ref(), pool).await? {
                        learned = true;
                    }
                }
                let rows = rows_to_answer(reading, judgement, pool, *time == Requested::Earliest)?;
                for row in rows {
                    if row.status == Status::Unknown
                        && ask_about(
                            &row,
                            &mut asked,
                            &mut problems,
                            source.as_ref(),
                            pool,
                            max_crl_bytes,
                        )
                        .await?
                    {
                        learned = true;
                    }
                }
            }
        }
        if !learned {
            break;
        }
    }
    Ok(problems)
}

/// `fillPool` over every reading, with a source.
pub async fn fill_pool(
    readings: &[Reading],
    trust: Rc<dyn TrustPort>,
    pool: &ValidationPool,
    source: Rc<dyn RevocationSource>,
    max_crl_bytes: usize,
    times: &[Requested],
) -> Result<Problems, Error> {
    fill_pool_for(readings, None, trust, pool, Some(source), max_crl_bytes, times).await
}

/// Fetch from `source`; a failure is noted and `None` comes back.
async fn fetch(
    source: &Rc<dyn RevocationSource>,
    request: RevocationRequest,
    note: &mut dyn FnMut(AskingProblem),
) -> Option<Vec<u8>> {
    let url = request.url().to_string();
    match source.fetch(request).await {
        Ok(der) => Some(der),
        Err(error) => {
            let network = if error.code() == Some("Network") {
                let mut details = error
                    .member("details")
                    .and_then(|d| d.as_object())
                    .cloned()
                    .unwrap_or_default();
                details.insert("source".into(), json!(url));
                Some(
                    Error::engine("Network", error.message.clone())
                        .with("details", serde_json::Value::Object(details)),
                )
            } else if error.code().is_some() {
                None
            } else {
                Some(
                    Error::engine("Network", format!("{url} did not answer: {}", error.message))
                        .with("details", json!({ "source": url })),
                )
            };
            note(AskingProblem { detail: error.message.clone(), network });
            None
        }
    }
}

/// Ask about one certificate: OCSP first, then its issuer's list. True when
/// an answer joined the pool.
async fn ask_about(
    row: &CertificateRevocation,
    asked: &mut HashSet<Vec<u8>>,
    problems: &mut Problems,
    source: Option<&Rc<dyn RevocationSource>>,
    pool: &ValidationPool,
    max_crl_bytes: usize,
) -> Result<bool, Error> {
    let key = row.certificate.clone();
    if !asked.insert(key.clone()) {
        return Ok(false);
    }
    let Some(source) = source else {
        problems.insert(
            key,
            AskingProblem {
                detail: "no `revocation` source was given to ask with".into(),
                network: None,
            },
        );
        return Ok(false);
    };
    let certificate = Certificate::from_ber(&row.certificate)?;
    let issuer = Certificate::from_ber(&row.issuer)?;
    let addresses = revocation_addresses(&certificate);
    let mut problem: Option<AskingProblem> = None;
    // The first address that did not answer is the one to report; otherwise the last word.
    let mut note = |next: AskingProblem| {
        if problem.as_ref().is_none_or(|p| p.network.is_none()) {
            problem = Some(next);
        }
    };
    let mut tries = Vec::new();
    for url in &addresses.ocsp {
        tries.push(RevocationRequest::Ocsp {
            url: url.clone(),
            body: build_ocsp_request(&certificate, &issuer),
        });
    }
    for url in &addresses.crl {
        tries.push(RevocationRequest::Crl { url: url.clone() });
    }
    if tries.is_empty() {
        problems.insert(
            key,
            AskingProblem {
                detail: "the certificate names no address to ask at".into(),
                network: None,
            },
        );
        return Ok(false);
    }
    for request in tries {
        let url = request.url().to_string();
        let kind = if matches!(request, RevocationRequest::Ocsp { .. }) {
            AnswerKind::Ocsp
        } else {
            AnswerKind::Crl
        };
        let Some(der) = fetch(source, request, &mut note).await else { continue };
        if kind == AnswerKind::Crl && der.len() > max_crl_bytes {
            note(AskingProblem {
                detail: format!(
                    "the list at {url} is {} bytes, over maxCrlBytes ({max_crl_bytes})",
                    der.len()
                ),
                network: None,
            });
            continue;
        }
        let check = check_revocation_answer(
            &RevocationAnswer { kind, der: der.clone() },
            &certificate,
            &issuer,
        );
        if let AnswerCheck::Nothing { detail, .. } = &check {
            note(AskingProblem {
                detail: format!("the answer from {url} says nothing: {detail}"),
                network: None,
            });
            continue;
        }
        pool.add_answer(kind, &der, false);
        problems.remove(&key);
        return Ok(true);
    }
    if let Some(problem) = problem {
        problems.insert(key, problem);
    }
    Ok(false)
}

/// Fetch the certificate that issued `orphan`. True when one joined the pool.
async fn fetch_issuer_of(
    orphan: &[u8],
    issuers_asked: &mut HashSet<Vec<u8>>,
    source: Option<&Rc<dyn RevocationSource>>,
    pool: &ValidationPool,
) -> Result<bool, Error> {
    let Some(source) = source else { return Ok(false) };
    if !issuers_asked.insert(orphan.to_vec()) {
        return Ok(false);
    }
    let known = pool.certificates().len();
    for url in revocation_addresses(&Certificate::from_ber(orphan)?).issuer {
        let Some(der) = fetch(source, RevocationRequest::Issuer { url }, &mut |_| {}).await else {
            continue;
        };
        for certificate in certificates_in(&der) {
            pool.add_certificate(&certificate, false);
        }
        if pool.certificates().len() > known {
            return Ok(true);
        }
    }
    Ok(false)
}

/// `rowsToAnswer`: the certificates a judgement asks about, each at the time
/// it is judged at.
fn rows_to_answer(
    reading: &Reading,
    judgement: &Judgement,
    pool: &ValidationPool,
    long_term: bool,
) -> Result<Vec<CertificateRevocation>, Error> {
    let mut rows = Vec::new();
    if let Some(path) = &judgement.path {
        rows.extend(path_revocation(
            pool,
            path,
            judgement.validation_time.time(),
            AnswersFrom::Anywhere,
        )?);
    }
    if !reading.is_timestamp
        && let (Some(path), Some(timestamp)) = (&judgement.timestamp_path, &judgement.timestamp)
    {
        rows.extend(path_revocation(
            pool,
            path,
            timestamp.validation_time.time(),
            AnswersFrom::Anywhere,
        )?);
    }
    if long_term && let Some(proven) = &judgement.relies_on {
        rows.extend(path_revocation(
            pool,
            &proven.path,
            crate::der::date_from_iso(&proven.gen_time),
            AnswersFrom::Anywhere,
        )?);
    }
    Ok(rows)
}

/// `certificatesIn`: one certificate, or a PKCS #7 bundle of them.
fn certificates_in(der: &[u8]) -> Vec<Certificate> {
    if let Ok(certificate) = Certificate::from_ber(der) {
        return vec![certificate];
    }
    let Some(root) = (match crate::asn1js::from_ber(der) {
        crate::asn1js::Verdict::Accepted => crate::der::from_ber(der),
        _ => None,
    }) else {
        return Vec::new();
    };
    // ContentInfo { contentType, [0] SignedData { version, digestAlgorithms,
    //   encapContentInfo, [0] certificates, ... } }
    let Some(signed_data) = root.children.get(1).and_then(|c| c.children.first()) else {
        return Vec::new();
    };
    signed_data
        .children
        .iter()
        .find(|m| m.is_context(0) && m.constructed)
        .map(|set| set.children.iter().filter_map(Certificate::read).collect())
        .unwrap_or_default()
}
