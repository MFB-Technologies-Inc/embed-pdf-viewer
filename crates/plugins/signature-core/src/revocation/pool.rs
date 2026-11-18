//! `revocation/pool.ts`: everything a document offers for checking its
//! signatures, certificates, OCSP responses and CRLs, wherever they sit.
//! Nothing in it is trusted for where it was found.

use std::cell::RefCell;
use std::collections::HashMap;
use std::rc::Rc;

use epdf_kernel::Error;
use epdf_kernel::bytes::Bytes;
use epdf_kernel::engine::DocumentHandle;
use serde_json::Value;

use super::addresses::cannot_be_revoked;
use super::answer::{AnswerCheck, AnswerKind, RevocationAnswer, check_revocation_answer};
use super::status::{Status, revocation_status_at};
use crate::cert::Certificate;
use crate::parse::CarriedRevocation;

/// `PooledAnswer`: an answer, and whether the document already holds it.
#[derive(Clone, Debug, PartialEq)]
pub struct PooledAnswer {
    pub answer: RevocationAnswer,
    /// False for an answer from outside the file.
    pub in_file: bool,
}

/// What each answer says about one certificate, keyed by its DER and its issuer's.
type Checked = HashMap<(Vec<u8>, Vec<u8>), Vec<Rc<AnswerCheck>>>;

#[derive(Default)]
struct PoolState {
    /// In the order offered: DER, the certificate, whether it is in the file.
    certificates: Vec<(Vec<u8>, Certificate, bool)>,
    answers: Vec<Rc<PooledAnswer>>,
    /// What each answer says about one certificate, keyed by both DERs.
    checked: Checked,
}

/// `ValidationPool`.
#[derive(Clone, Default)]
pub struct ValidationPool {
    state: Rc<RefCell<PoolState>>,
}

impl ValidationPool {
    pub fn new() -> ValidationPool {
        ValidationPool::default()
    }

    /// Every certificate offered, each once.
    pub fn certificates(&self) -> Vec<Certificate> {
        self.state.borrow().certificates.iter().map(|(_, c, _)| c.clone()).collect()
    }

    /// Whether a certificate with this DER was offered from the file.
    pub fn certificate_in_file(&self, der: &[u8]) -> Option<bool> {
        self.state.borrow().certificates.iter().find(|(d, _, _)| d == der).map(|(_, _, f)| *f)
    }

    /// `addCertificate`: bytes that are not a certificate are left out, and
    /// false comes back.
    pub fn add_certificate_der(&self, der: &[u8], in_file: bool) -> bool {
        if self.state.borrow().certificates.iter().any(|(d, _, _)| d == der) {
            return true;
        }
        match Certificate::from_ber(der) {
            Ok(certificate) => {
                self.state.borrow_mut().certificates.push((der.to_vec(), certificate, in_file));
                true
            }
            Err(_) => false,
        }
    }

    /// `addCertificate` with a parsed certificate.
    pub fn add_certificate(&self, certificate: &Certificate, in_file: bool) {
        let mut state = self.state.borrow_mut();
        if !state.certificates.iter().any(|(d, _, _)| d == &certificate.raw) {
            state.certificates.push((certificate.raw.clone(), certificate.clone(), in_file));
        }
    }

    /// `addAnswer`: an answer as it was stored or sent.
    pub fn add_answer(&self, kind: AnswerKind, der: &[u8], in_file: bool) -> Rc<PooledAnswer> {
        let mut state = self.state.borrow_mut();
        if let Some(found) =
            state.answers.iter().find(|a| a.answer.kind == kind && a.answer.der == der)
        {
            return found.clone();
        }
        let answer =
            Rc::new(PooledAnswer { answer: RevocationAnswer { kind, der: der.to_vec() }, in_file });
        state.answers.push(answer.clone());
        answer
    }

    /// `addCarried`: what a CMS carries itself.
    pub fn add_carried(&self, certificates: &[Certificate], revocation: &CarriedRevocation) {
        for certificate in certificates {
            self.add_certificate(certificate, true);
        }
        for der in &revocation.ocsp_responses {
            self.add_answer(AnswerKind::Ocsp, der, true);
        }
        for der in &revocation.crls {
            self.add_answer(AnswerKind::Crl, der, true);
        }
    }

    /// Every answer offered, in order.
    pub fn answers(&self) -> Vec<Rc<PooledAnswer>> {
        self.state.borrow().answers.clone()
    }

    /// `checksFor`: what each answer says about `certificate`, which
    /// `issuer` issued, each answer checked once.
    pub fn checks_for(
        &self,
        certificate: &Certificate,
        issuer: &Certificate,
    ) -> Vec<(Rc<PooledAnswer>, Rc<AnswerCheck>)> {
        let answers = self.answers();
        let key = (certificate.raw.clone(), issuer.raw.clone());
        let mut checks = self.state.borrow_mut().checked.remove(&key).unwrap_or_default();
        for answer in &answers[checks.len()..] {
            checks.push(Rc::new(check_revocation_answer(&answer.answer, certificate, issuer)));
        }
        let out = answers.into_iter().zip(checks.iter().cloned()).collect();
        self.state.borrow_mut().checked.insert(key, checks);
        out
    }
}

fn byte_list(value: &Value, key: &str) -> Vec<Vec<u8>> {
    value
        .get(key)
        .and_then(Value::as_array)
        .map(|list| {
            list.iter().map(|v| Bytes::from_json(v).map(|b| b.0).unwrap_or_default()).collect()
        })
        .unwrap_or_default()
}

/// `readValidationPool`: what a document's validation data holds. An engine
/// that can't read validation data offers none.
pub async fn read_validation_pool(doc: &Rc<dyn DocumentHandle>) -> Result<ValidationPool, Error> {
    let pool = ValidationPool::new();
    match doc.call("signatures.getValidationInfo", None, vec![]).await {
        Ok(stored) => {
            for der in byte_list(&stored, "certificates") {
                pool.add_certificate_der(&der, true);
            }
            for der in byte_list(&stored, "ocspResponses") {
                pool.add_answer(AnswerKind::Ocsp, &der, true);
            }
            for der in byte_list(&stored, "crls") {
                pool.add_answer(AnswerKind::Crl, &der, true);
            }
        }
        Err(error) if error.code() == Some("NotImplemented") => {}
        Err(error) => return Err(error),
    }
    Ok(pool)
}

/// `CertificateRevocation`: what the pool says about one certificate of a
/// path, at a moment.
#[derive(Clone, Debug)]
pub struct CertificateRevocation {
    pub status: Status,
    /// The answer that decided it; `None` for `unknown`.
    pub answer: Option<Rc<AnswerCheck>>,
    pub certificate: Vec<u8>,
    pub name: Option<String>,
    pub issuer: Vec<u8>,
    pub evidence: Option<Rc<PooledAnswer>>,
}

/// Which answers count: all of them, or only those in the file.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum AnswersFrom {
    Anywhere,
    File,
}

/// `pathRevocation`: what the pool's answers say at `at` about each
/// certificate of a validated path that can be withdrawn.
pub fn path_revocation(
    pool: &ValidationPool,
    path: &[Vec<u8>],
    at: f64,
    from: AnswersFrom,
) -> Result<Vec<CertificateRevocation>, Error> {
    // A certificate that is its own anchor comes back twice in a row.
    let links: Vec<&Vec<u8>> = path
        .iter()
        .enumerate()
        .filter(|(index, der)| *index == 0 || **der != path[index - 1])
        .map(|(_, der)| der)
        .collect();
    let certificates =
        links.iter().map(|der| Certificate::from_ber(der)).collect::<Result<Vec<_>, _>>()?;
    let mut rows = Vec::new();
    for index in 0..certificates.len().saturating_sub(1) {
        if cannot_be_revoked(&certificates[index]) {
            continue;
        }
        let checked: Vec<_> = pool
            .checks_for(&certificates[index], &certificates[index + 1])
            .into_iter()
            .filter(|(answer, _)| from == AnswersFrom::Anywhere || answer.in_file)
            .collect();
        let checks: Vec<&AnswerCheck> = checked.iter().map(|(_, c)| c.as_ref()).collect();
        let (status, decided) = revocation_status_at(&checks, at);
        rows.push(CertificateRevocation {
            status,
            answer: decided.map(|i| checked[i].1.clone()),
            certificate: links[index].clone(),
            name: crate::signers::certificate_common_name(links[index])?,
            issuer: links[index + 1].clone(),
            evidence: decided.map(|i| checked[i].0.clone()),
        });
    }
    Ok(rows)
}

/// `summarizeRevocation`: one answer for a whole path, with the reason
/// naming the certificate that decided it. `when` is "now" or a time.
pub fn summarize_revocation(
    rows: &[CertificateRevocation],
    when: &str,
) -> (Status, Option<String>) {
    let named = |row: &CertificateRevocation| {
        format!("\"{}\"", row.name.as_deref().unwrap_or("a certificate without a name"))
    };
    if let Some(revoked) = rows.iter().find(|r| r.status == Status::Revoked) {
        let (on, why) = match revoked.answer.as_deref() {
            Some(AnswerCheck::Revoked { revoked_at, reason, .. }) => (
                format!(" on {}", crate::der::iso_string(*revoked_at).unwrap_or_default()),
                reason.map(|r| format!(" ({r})")).unwrap_or_default(),
            ),
            _ => (String::new(), String::new()),
        };
        return (Status::Revoked, Some(format!("{} was withdrawn{on}{why}", named(revoked))));
    }
    if let Some(unknown) = rows.iter().find(|r| r.status == Status::Unknown) {
        return (
            Status::Unknown,
            Some(format!("no answer about {} speaks for {when}", named(unknown))),
        );
    }
    (Status::Good, None)
}
