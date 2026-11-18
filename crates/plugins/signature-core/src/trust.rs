//! `trust.ts`, and the part of pkijs it hands the work to:
//! `CertificateChainValidationEngine` (pkijs 4.0.1, unchanged in what it
//! decides from 3.4.0), ported rule for rule.
//!
//! `validatePath` (upstream `30b269418`) hands the engine the signer LAST
//! and alone, since pkijs validates the last certificate it is handed, and
//! the path that comes back must begin with the signer. Two engine rules
//! still decide many outcomes:
//!
//! - A certificate that is itself an anchor is a path of itself twice.
//! - Only the certificates after the first are checked for CA-ness, and
//!   every such failure reads "One of intermediate certificates is not a
//!   CA certificate".

use std::rc::Rc;

use epdf_kernel::Error;
use epdf_kernel::rt::Promise;
use serde_json::{Value, json};

use crate::cert::Certificate;
use crate::crypto;
use crate::oids;
use crate::parse::{Internal, parse_cms_internal};

/// `TrustPort`: where anchors come from.
pub trait TrustPort {
    fn anchors(&self) -> Promise<Vec<Vec<u8>>>;
    /// `timestampAnchors?()`: anchors for timestamp authorities alone.
    /// `None` when the port has no such member: authorities are then judged
    /// against `anchors()`.
    fn timestamp_anchors(&self) -> Option<Promise<Vec<Vec<u8>>>> {
        None
    }
    /// Whether the port stands for a `trust` setting at all. A host's slot
    /// that holds no trust yet is no setting, as upstream's `trust: null`.
    fn is_set(&self) -> bool {
        true
    }
}

/// `ValidationTime`: now, or a verified RFC 3161 token's time. The token's
/// time is `stampedAt`, read under its older name `genTime` too.
#[derive(Clone, Debug, PartialEq)]
pub enum ValidationTime {
    Now,
    /// A verified RFC 3161 token's time, as an ISO 8601 string.
    Timestamp {
        stamped_at: String,
    },
}

impl ValidationTime {
    /// Read from a caller's `{ kind, stampedAt?, genTime? }`: `stampedAt`
    /// wins when both are given.
    pub fn from_json(value: &Value) -> ValidationTime {
        match value.get("kind").and_then(Value::as_str) {
            Some("timestamp") => {
                let stamped_at = match value.get("stampedAt") {
                    Some(v) if !v.is_null() && !epdf_kernel::is_undefined(v) => v,
                    _ => &value["genTime"],
                };
                ValidationTime::Timestamp {
                    stamped_at: stamped_at.as_str().unwrap_or_default().into(),
                }
            }
            _ => ValidationTime::Now,
        }
    }

    /// `appliedValidationTime`: a timestamp's time under both its names.
    pub fn to_json(&self) -> Value {
        match self {
            ValidationTime::Now => json!({ "kind": "now" }),
            ValidationTime::Timestamp { stamped_at } => json!({
                "kind": "timestamp",
                "stampedAt": stamped_at,
                "genTime": stamped_at,
                "tokenVerified": true,
            }),
        }
    }

    /// `timeOf`: the moment the time names, in epoch milliseconds.
    pub fn time(&self) -> f64 {
        match self {
            ValidationTime::Now => epdf_kernel::rt::now(),
            ValidationTime::Timestamp { stamped_at } => crate::der::date_from_iso(stamped_at),
        }
    }

    /// The time in words, for a reason: "now", or the time a timestamp proves.
    pub fn name(&self) -> String {
        match self {
            ValidationTime::Now => "now".into(),
            ValidationTime::Timestamp { stamped_at } => stamped_at.clone(),
        }
    }
}

/// `TrustStatus`.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum TrustStatus {
    Trusted,
    Untrusted,
    Unknown,
}

impl TrustStatus {
    pub fn name(self) -> &'static str {
        match self {
            TrustStatus::Trusted => "trusted",
            TrustStatus::Untrusted => "untrusted",
            TrustStatus::Unknown => "unknown",
        }
    }
}

/// `TrustVerdict`.
#[derive(Clone, Debug, PartialEq)]
pub struct TrustVerdict {
    pub status: TrustStatus,
    pub reason: Option<String>,
    pub path: Option<Vec<Vec<u8>>>,
    /// DER of the certificate whose issuer was not found among those
    /// offered, when that is why the path is not trusted.
    pub without_issuer: Option<Vec<u8>>,
}

impl TrustVerdict {
    pub fn untrusted(reason: impl Into<String>) -> TrustVerdict {
        TrustVerdict {
            status: TrustStatus::Untrusted,
            reason: Some(reason.into()),
            path: None,
            without_issuer: None,
        }
    }

    pub fn unknown(reason: impl Into<String>) -> TrustVerdict {
        TrustVerdict { status: TrustStatus::Unknown, ..TrustVerdict::untrusted(reason) }
    }

    fn trusted(path: Vec<Vec<u8>>) -> TrustVerdict {
        TrustVerdict {
            status: TrustStatus::Trusted,
            reason: None,
            path: Some(path),
            without_issuer: None,
        }
    }

    pub fn to_json(&self) -> Value {
        let mut out = serde_json::Map::new();
        out.insert("status".into(), json!(self.status.name()));
        if let Some(reason) = &self.reason {
            out.insert("reason".into(), json!(reason));
        }
        if let Some(path) = &self.path {
            out.insert("path".into(), path.iter().map(|c| crate::parse::bytes_json(c)).collect());
        }
        if let Some(der) = &self.without_issuer {
            out.insert("withoutIssuer".into(), crate::parse::bytes_json(der));
        }
        Value::Object(out)
    }
}

/// A CMS as `validateChain` takes it: bytes, or an earlier parse.
#[derive(Clone)]
pub enum CmsInput {
    Bytes(Vec<u8>),
    Parsed(Rc<Internal>),
}

const NO_ANCHORS: &str = "no trust anchors configured";

/// `validateChain`.
pub async fn validate_chain(
    cms: CmsInput,
    trust: Option<Rc<dyn TrustPort>>,
    at: ValidationTime,
) -> Result<TrustVerdict, Error> {
    validate_chain_among(cms, trust, &at, &[]).await
}

/// `validateChainAmong`: `validateChain` with more certificates a path may
/// pass through (a document's validation data). They are candidates only.
pub async fn validate_chain_among(
    cms: CmsInput,
    trust: Option<Rc<dyn TrustPort>>,
    at: &ValidationTime,
    known: &[Certificate],
) -> Result<TrustVerdict, Error> {
    let Some(trust) = trust else { return Ok(TrustVerdict::unknown(NO_ANCHORS)) };
    let anchors = trust.anchors().await?;
    if anchors.is_empty() {
        return Ok(TrustVerdict::unknown(NO_ANCHORS));
    }
    let internal = match cms {
        CmsInput::Parsed(internal) => internal,
        CmsInput::Bytes(bytes) => match parse_cms_internal(&bytes) {
            Ok(internal) => Rc::new(internal),
            Err(e) => return Ok(TrustVerdict::untrusted(e.message())),
        },
    };
    let mut candidates = internal.certificates.clone();
    candidates.extend(known.iter().cloned());
    Ok(validate_path(&internal.signer_certificate, &candidates, &anchors, at.time()))
}

/// `validateTimestampAuthority`: is a token's authority one the port trusts
/// to say what time it was? Its certificate must be made for timestamping
/// alone, in a critical extension; the token must be dated inside its
/// validity; and it must be a timestamp anchor or lead to one at `at`.
pub async fn validate_timestamp_authority(
    token: &Internal,
    certificates: &[Certificate],
    trust: Option<Rc<dyn TrustPort>>,
    at: &ValidationTime,
    gen_time: f64,
) -> Result<TrustVerdict, Error> {
    let Some(trust) = trust else { return Ok(TrustVerdict::unknown(NO_ANCHORS)) };
    let (anchors, own) = match trust.timestamp_anchors() {
        Some(anchors) => (anchors.await?, true),
        None => (trust.anchors().await?, false),
    };
    if anchors.is_empty() {
        return Ok(TrustVerdict::unknown(if own {
            "no timestamp anchors configured"
        } else {
            NO_ANCHORS
        }));
    }
    let authority = &token.signer_certificate;
    if let Some(problem) = timestamping_purpose_problem(authority) {
        return Ok(TrustVerdict::untrusted(problem));
    }
    if !valid_at(authority, gen_time) {
        return Ok(TrustVerdict::untrusted(
            "the token is dated outside its authority certificate's validity",
        ));
    }
    let at = at.time();
    if anchors.iter().any(|anchor| anchor == &authority.raw) {
        return Ok(if valid_at(authority, at) {
            TrustVerdict::trusted(vec![authority.raw.clone()])
        } else {
            TrustVerdict::untrusted(
                "the authority's certificate is not valid at the time it is judged",
            )
        });
    }
    let mut candidates = token.certificates.clone();
    candidates.extend(certificates.iter().cloned());
    Ok(validate_path(authority, &candidates, &anchors, at))
}

/// `validAt`. A date pkijs could not read compares false either way.
fn valid_at(certificate: &Certificate, at: f64) -> bool {
    certificate.not_before <= at && at <= certificate.not_after
}

/// `timestampingPurposeProblem`.
fn timestamping_purpose_problem(certificate: &Certificate) -> Option<&'static str> {
    let Some(extension) = certificate.extension(oids::EXTENDED_KEY_USAGE) else {
        return Some(
            "the authority's certificate has no extended key usage, so it is not made for timestamping",
        );
    };
    let purposes: Vec<String> = extension
        .parsed()
        .filter(|n| n.is_sequence())
        .map(|n| n.children.iter().filter_map(|c| c.oid()).collect())
        .unwrap_or_default();
    if !purposes.iter().any(|p| p == oids::TIME_STAMPING) {
        return Some("the authority's certificate does not allow timestamping");
    }
    if purposes.len() > 1 {
        return Some("the authority's certificate allows other uses besides timestamping");
    }
    if !extension.critical {
        return Some(
            "the authority's certificate does not mark its timestamping purpose as critical",
        );
    }
    None
}

/// `validatePath`: does a path lead from `target` to one of `anchors`,
/// valid at `at`? The target goes to the engine last and alone, and the
/// path that comes back must begin with it.
pub fn validate_path(
    target: &Certificate,
    candidates: &[Certificate],
    anchors: &[Vec<u8>],
    at: f64,
) -> TrustVerdict {
    let is_target = |der: &[u8]| der == target.raw.as_slice();
    // A certificate may be offered twice, by the CMS and by the document's
    // validation data.
    let mut others: Vec<Certificate> = Vec::new();
    for candidate in candidates {
        if !is_target(&candidate.raw) && !others.iter().any(|o| o.raw == candidate.raw) {
            others.push(candidate.clone());
        }
    }
    let target_is_anchor = anchors.iter().any(|a| is_target(a));
    let mut other_anchors = Vec::new();
    for anchor in anchors.iter().filter(|a| !is_target(a)) {
        match Certificate::from_ber(anchor) {
            Ok(certificate) => other_anchors.push(certificate),
            Err(error) => return TrustVerdict::untrusted(error.message),
        }
    }
    // pkijs compares certificates by identity: the target is one object,
    // whichever list it is in.
    let mut pool = other_anchors.clone();
    let anchor_count = pool.len();
    pool.extend(others.iter().cloned());
    let target_id = pool.len();
    pool.push(target.clone());
    let (trusted, certs): (Vec<usize>, Vec<usize>) = if target_is_anchor {
        ((0..anchor_count).chain([target_id]).collect(), vec![target_id])
    } else {
        ((0..anchor_count).collect(), (anchor_count..=target_id).collect())
    };
    let engine = ChainEngine { pool, trusted, certs, check_date: at };
    match engine.verify() {
        Err(message) => {
            let mut search = others.clone();
            search.extend(other_anchors.iter().cloned());
            match first_without_issuer(target, &search) {
                Some(orphan) => TrustVerdict {
                    reason: Some(format!(
                        "no certificate was found that issued \"{}\"",
                        name_of(orphan)
                    )),
                    without_issuer: Some(orphan.raw.clone()),
                    ..TrustVerdict::untrusted("")
                },
                None => TrustVerdict::untrusted(message),
            }
        }
        Ok(path) => {
            if path.first().is_none_or(|c| !is_target(&c.raw)) {
                return TrustVerdict::untrusted(
                    "the validated path does not begin with the certificate that signed",
                );
            }
            TrustVerdict::trusted(path.iter().map(|c| c.raw.clone()).collect())
        }
    }
}

/// `firstWithoutIssuer`: the first certificate on the way up from `target`
/// whose issuer is not in `pool`, or `None` when the way ends at a
/// certificate that issued itself.
fn first_without_issuer<'a>(
    target: &'a Certificate,
    pool: &'a [Certificate],
) -> Option<&'a Certificate> {
    // Identity: the target, or an index into `pool`.
    let mut visited: Vec<Option<usize>> = Vec::new();
    let mut current: (Option<usize>, &Certificate) = (None, target);
    while !visited.contains(&current.0) {
        visited.push(current.0);
        let cert = current.1;
        if cert.issuer.is_equal(&cert.subject) {
            return None;
        }
        match pool.iter().position(|c| c.subject.is_equal(&cert.issuer)) {
            None => return Some(cert),
            Some(index) => current = (Some(index), &pool[index]),
        }
    }
    None
}

fn name_of(certificate: &Certificate) -> String {
    crate::signers::certificate_common_name(&certificate.raw)
        .ok()
        .flatten()
        .unwrap_or_else(|| "a certificate without a common name".into())
}

// ── the chain engine ─────────────────────────────────────────────────────────

/// Certificates are identified by their position in `trusted ++ certs`, as
/// pkijs compares them by object identity.
struct ChainEngine {
    pool: Vec<Certificate>,
    trusted: Vec<usize>,
    certs: Vec<usize>,
    check_date: f64,
}

const NO_VALID_PATH: &str = "No valid certificate paths found";
const ISSUER_CYCLE: &str = "Certificate issuers form a cycle";

impl ChainEngine {
    fn tbs_equal(&self, a: usize, b: usize) -> bool {
        self.pool[a].tbs == self.pool[b].tbs
    }

    fn is_trusted(&self, id: usize) -> bool {
        self.trusted.iter().any(|&t| self.tbs_equal(id, t))
    }

    /// `certificate.verify(issuer?)`.
    fn verify_signature(&self, subject: usize, issuer: Option<usize>) -> Result<bool, String> {
        let cert = &self.pool[subject];
        let spki = match issuer {
            Some(i) => &self.pool[i].spki,
            None if cert.issuer.is_equal(&cert.subject) => &cert.spki,
            None => return Err("Please provide issuer certificate as a parameter".into()),
        };
        crypto::verify_with_public_key(
            &cert.tbs,
            &cert.signature,
            spki,
            &cert.signature_algorithm,
            None,
        )
    }

    /// `defaultFindIssuer`.
    fn find_issuer(&self, id: usize) -> Vec<usize> {
        let cert = &self.pool[id];
        if cert.subject.is_equal(&cert.issuer) && self.verify_signature(id, None) == Ok(true) {
            return vec![id];
        }
        let aki = cert.authority_key_identifier();
        let key_identifier = aki.as_ref().and_then(|a| a.key_identifier.clone());
        // An identifier without a key id matches on `authorityCertIssuer`, a
        // GeneralNames array that no name equals, so it matches nothing. Its
        // serial number only ever counts alongside that issuer.
        let authority_issuer =
            aki.as_ref().is_some_and(|a| a.key_identifier.is_none() && a.authority_cert_issuer);
        let mut result = Vec::new();
        let candidates: Vec<usize> = self.trusted.iter().chain(&self.certs).copied().collect();
        for possible in candidates {
            let issuer = &self.pool[possible];
            if let Some(key_identifier) = &key_identifier
                && let Some(extensions) = &issuer.extensions
            {
                let ski = extensions
                    .iter()
                    .find(|e| e.oid == oids::SUBJECT_KEY_IDENTIFIER && e.parsed().is_some());
                if let Some(ski) = ski {
                    if ski.parsed().map(|n| n.octets()).as_ref() == Some(key_identifier) {
                        result.push(possible);
                    }
                    continue;
                }
            }
            if authority_issuer {
                continue;
            }
            if cert.issuer.is_equal(&issuer.subject) {
                result.push(possible);
            }
        }
        let mut i = result.len();
        while i > 0 {
            i -= 1;
            if self.verify_signature(id, Some(result[i])) != Ok(true) {
                result.remove(i);
            }
        }
        result
    }

    /// `buildPath`. `below` holds the certificates this one was reached
    /// from. Deliberate deviation: pkijs has no cycle check, so two CAs that
    /// issue each other recurse until it never returns; here an issuer that
    /// is already below fails the validation instead of the stack.
    fn build_path(&self, id: usize, below: &mut Vec<usize>) -> Result<Vec<Vec<usize>>, String> {
        if self.is_trusted(id) {
            return Ok(vec![vec![id]]);
        }
        let issuers = self.find_issuer(id);
        if issuers.is_empty() {
            return Err(NO_VALID_PATH.into());
        }
        below.push(id);
        let mut result = Vec::new();
        for issuer in issuers {
            if self.tbs_equal(issuer, id) {
                result.push(vec![issuer]);
                continue;
            }
            if below.iter().any(|&b| self.tbs_equal(b, issuer)) {
                return Err(ISSUER_CYCLE.into());
            }
            for path in self.build_path(issuer, below)? {
                let mut copy = vec![issuer];
                copy.extend(&path);
                let unique =
                    copy.iter().enumerate().all(|(i, a)| copy.iter().skip(i + 1).all(|b| a != b));
                result.push(if unique { copy } else { path });
            }
        }
        below.pop();
        Ok(result)
    }

    /// `checkForCA`: `Err` with pkijs's reason.
    fn check_for_ca(&self, id: usize) -> Result<(), String> {
        let cert = &self.pool[id];
        let mut is_ca = false;
        let mut must_be_ca = false;
        let mut key_usage = false;
        if let Some(extensions) = &cert.extensions {
            for extension in extensions {
                let parsed = extension.parsed();
                if extension.critical && parsed.is_none() {
                    return Err(format!(
                        "Unable to parse critical certificate extension: {}",
                        extension.oid
                    ));
                }
                if extension.oid == oids::KEY_USAGE {
                    key_usage = true;
                    let first = parsed
                        .clone()
                        .map(|n| n.bits())
                        .and_then(|b| b.first().copied())
                        .unwrap_or(0);
                    if first & 0x04 != 0 {
                        must_be_ca = true;
                    }
                }
                if extension.oid == oids::BASIC_CONSTRAINTS
                    && parsed.and_then(|n| n.children.first().cloned()).is_some_and(|b| {
                        b.is_universal(1) && b.content.first().is_some_and(|&v| v != 0)
                    })
                {
                    is_ca = true;
                }
            }
            if must_be_ca && !is_ca {
                return Err("keyCertSign without BasicConstraints".into());
            }
            if key_usage && is_ca && !must_be_ca {
                return Err("keyCertSign not set".into());
            }
        }
        if !is_ca {
            return Err("more than one possible end-user certificate".into());
        }
        Ok(())
    }

    fn basic_check(&self, path: &[usize]) -> Result<(), String> {
        for &id in path {
            let cert = &self.pool[id];
            if cert.not_before > self.check_date || cert.not_after < self.check_date {
                return Err("The certificate is either not yet valid or expired".into());
            }
        }
        if path.len() < 2 {
            return Err("Too short certificate path".into());
        }
        for i in (0..path.len() - 1).rev() {
            let cert = &self.pool[path[i]];
            if !cert.issuer.is_equal(&cert.subject)
                && !cert.issuer.is_equal(&self.pool[path[i + 1]].subject)
            {
                return Err("Incorrect name chaining".into());
            }
        }
        for &id in &path[1..] {
            if self.check_for_ca(id).is_err() {
                return Err("One of intermediate certificates is not a CA certificate".into());
            }
        }
        Ok(())
    }

    /// `sort()`: the path, or the message the engine reports.
    fn sort(&self) -> Result<Vec<usize>, String> {
        let mut local: Vec<usize> = self.trusted.iter().chain(&self.certs).copied().collect();
        // pkijs's de-duplication, restart quirk and all: after a removal the
        // outer loop resumes at index 1.
        let mut i = 0;
        while i < local.len() {
            let mut j = 0;
            while j < local.len() {
                if i != j && self.tbs_equal(local[i], local[j]) {
                    local.remove(j);
                    i = 0;
                    break;
                }
                j += 1;
            }
            i += 1;
        }
        let leaf = *local.last().ok_or("Empty certificate array")?;
        let mut result = self.build_path(leaf, &mut Vec::new())?;
        if result.is_empty() {
            return Err("Unable to find certificate path".into());
        }
        result.retain(|path| path.iter().any(|&id| self.is_trusted(id)));
        if result.is_empty() {
            return Err(NO_VALID_PATH.into());
        }
        let mut shortest = 0;
        for (index, path) in result.iter().enumerate() {
            if path.len() < result[shortest].len() {
                shortest = index;
            }
        }
        let mut path = vec![leaf];
        path.extend(&result[shortest]);
        self.basic_check(&path)?;
        Ok(path)
    }

    /// `verify()`: the certificate path, leaf first, or the reason.
    fn verify(&self) -> Result<Vec<Certificate>, String> {
        if self.certs.is_empty() {
            return Err("Empty certificate array".into());
        }
        let path = self.sort()?;
        crate::policy::check(&path.iter().map(|&id| &self.pool[id]).collect::<Vec<_>>())?;
        Ok(path.iter().map(|&id| self.pool[id].clone()).collect())
    }
}
