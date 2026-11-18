//! `revocation/answer.ts`: the one verifier of revocation answers, an OCSP
//! response or a CRL asked about one certificate. Offline and
//! deterministic; it says nothing about time.
//!
//! The OCSP and CRL structures are read as pkijs's schemas read them; a
//! structure the schema refuses is `unreadable` with pkijs's schema text.

use crate::cert::{AlgId, Certificate, Name};
use crate::crypto;
use crate::der::{self, Node};
use crate::oids::{self, Digest};

/// `RevocationAnswer['kind']`.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum AnswerKind {
    Ocsp,
    Crl,
}

impl AnswerKind {
    pub fn name(self) -> &'static str {
        match self {
            AnswerKind::Ocsp => "ocsp",
            AnswerKind::Crl => "crl",
        }
    }
}

/// `RevocationAnswer`: DER, and which of the two kinds it is.
#[derive(Clone, Debug, PartialEq)]
pub struct RevocationAnswer {
    pub kind: AnswerKind,
    pub der: Vec<u8>,
}

/// `RevocationProblem`.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum RevocationProblem {
    Unreadable,
    NotSuccessful,
    OtherCertificate,
    UnknownStatus,
    WrongSigner,
    Signature,
    Unsupported,
    IndirectCrl,
    DeltaCrl,
    OutOfScope,
}

impl RevocationProblem {
    pub fn name(self) -> &'static str {
        match self {
            RevocationProblem::Unreadable => "unreadable",
            RevocationProblem::NotSuccessful => "not-successful",
            RevocationProblem::OtherCertificate => "other-certificate",
            RevocationProblem::UnknownStatus => "unknown-status",
            RevocationProblem::WrongSigner => "wrong-signer",
            RevocationProblem::Signature => "signature",
            RevocationProblem::Unsupported => "unsupported",
            RevocationProblem::IndirectCrl => "indirect-crl",
            RevocationProblem::DeltaCrl => "delta-crl",
            RevocationProblem::OutOfScope => "out-of-scope",
        }
    }
}

/// `REASONS`: why a certificate was withdrawn (RFC 5280 5.3.1).
fn reason_name(code: i64) -> Option<&'static str> {
    Some(match code {
        0 => "unspecified",
        1 => "key-compromise",
        2 => "ca-compromise",
        3 => "affiliation-changed",
        4 => "superseded",
        5 => "cessation-of-operation",
        6 => "certificate-hold",
        8 => "remove-from-crl",
        9 => "privilege-withdrawn",
        10 => "aa-compromise",
        _ => return None,
    })
}

/// `AnswerWindow`: the time an answer speaks for.
#[derive(Clone, Debug, PartialEq)]
pub struct AnswerWindow {
    pub kind: AnswerKind,
    /// Epoch milliseconds.
    pub this_update: f64,
    pub next_update: Option<f64>,
    /// DER of the certificate that signed an OCSP answer in the issuer's
    /// place, when one did.
    pub responder: Option<Vec<u8>>,
}

/// `RevocationAnswerCheck`: what one answer says about one certificate.
#[derive(Clone, Debug, PartialEq)]
pub enum AnswerCheck {
    Good(AnswerWindow),
    Revoked { revoked_at: f64, reason: Option<&'static str>, window: AnswerWindow },
    Nothing { kind: AnswerKind, problem: RevocationProblem, detail: String },
}

impl AnswerCheck {
    pub fn says(&self) -> &'static str {
        match self {
            AnswerCheck::Good(_) => "good",
            AnswerCheck::Revoked { .. } => "revoked",
            AnswerCheck::Nothing { .. } => "nothing",
        }
    }

    pub fn window(&self) -> Option<&AnswerWindow> {
        match self {
            AnswerCheck::Good(window) | AnswerCheck::Revoked { window, .. } => Some(window),
            AnswerCheck::Nothing { .. } => None,
        }
    }
}

/// `checkRevocationAnswer`: an answer about `certificate`, which `issuer`
/// issued. It must be about that certificate, signed by the issuer or a
/// responder the issuer certified, and its signature must verify.
pub fn check_revocation_answer(
    answer: &RevocationAnswer,
    certificate: &Certificate,
    issuer: &Certificate,
) -> AnswerCheck {
    let nothing = |problem, detail: &str| AnswerCheck::Nothing {
        kind: answer.kind,
        problem,
        detail: detail.to_string(),
    };
    if !certificate.issuer.is_equal(&issuer.subject) {
        return nothing(
            RevocationProblem::OtherCertificate,
            "the certificate was not issued by the issuer given",
        );
    }
    let checked = match answer.kind {
        AnswerKind::Ocsp => check_ocsp(&answer.der, certificate, issuer),
        AnswerKind::Crl => check_crl(&answer.der, certificate, issuer),
    };
    checked.unwrap_or_else(|message| nothing(RevocationProblem::Unreadable, &message))
}

fn schema(target: &str) -> String {
    der::schema_error(target).message
}

fn ocsp_error(status: i64) -> String {
    match status {
        1 => "malformed request".into(),
        2 => "internal error".into(),
        3 => "try later".into(),
        5 => "a signed request is required".into(),
        6 => "unauthorized".into(),
        _ => format!("status {status}"),
    }
}

/// An INTEGER or ENUMERATED's value as `valueDec` reads it (two's
/// complement, big-endian).
fn small_integer(node: &Node) -> i64 {
    let mut value: i64 = if node.content.first().is_some_and(|b| b & 0x80 != 0) { -1 } else { 0 };
    for &byte in node.content.iter().take(8) {
        value = (value << 8) | i64::from(byte);
    }
    value
}

/// A primitive OCTET STRING's bytes, or the schema error.
fn octets_of(node: Option<&Node>, target: &str) -> Result<Vec<u8>, String> {
    node.filter(|n| n.is_octet_string() && !n.constructed)
        .map(|n| n.content.clone())
        .ok_or_else(|| schema(target))
}

/// pkijs `CertID`.
#[derive(Clone, Debug, PartialEq)]
pub struct CertId {
    pub hash_algorithm: AlgId,
    pub issuer_name_hash: Vec<u8>,
    pub issuer_key_hash: Vec<u8>,
    pub serial: Vec<u8>,
}

impl CertId {
    fn read(node: &Node) -> Result<CertId, String> {
        let error = || schema("CertID");
        if !node.is_sequence() || node.children.len() != 4 {
            return Err(error());
        }
        let hash_algorithm = AlgId::read(&node.children[0]).ok_or_else(error)?;
        let issuer_name_hash = octets_of(node.children.get(1), "CertID")?;
        let issuer_key_hash = octets_of(node.children.get(2), "CertID")?;
        let serial = node.children[3].clone();
        if !serial.is_universal(der::INTEGER) {
            return Err(error());
        }
        Ok(CertId { hash_algorithm, issuer_name_hash, issuer_key_hash, serial: serial.content })
    }

    /// `CertID.createForCertificate`: `certificate` named under `hash`.
    pub fn for_certificate(
        certificate: &Certificate,
        issuer: &Certificate,
        hash: Digest,
    ) -> CertId {
        CertId {
            hash_algorithm: AlgId {
                oid: hash.oid().to_string(),
                params: der::from_ber(&der::null()),
            },
            issuer_name_hash: hash.hash(&issuer.subject.raw),
            issuer_key_hash: hash.hash(&issuer.spki.key),
            serial: certificate.serial.clone(),
        }
    }

    /// `CertID.toSchema().toBER()`.
    pub fn encode(&self) -> Vec<u8> {
        der::sequence(&[
            der::sequence(&[der::oid(&self.hash_algorithm.oid), der::null()]),
            der::octet_string(&self.issuer_name_hash),
            der::octet_string(&self.issuer_key_hash),
            der::integer_bytes(&self.serial),
        ])
    }

    /// `CertID.isEqual`.
    fn is_equal(&self, other: &CertId) -> bool {
        self.hash_algorithm.oid == other.hash_algorithm.oid
            && self.issuer_name_hash == other.issuer_name_hash
            && self.issuer_key_hash == other.issuer_key_hash
            && self.serial == other.serial
    }
}

/// The responder identifier: by name, or by the SHA-1 of its key.
enum ResponderId {
    Name(Name),
    KeyHash(Vec<u8>),
}

struct SingleResponse {
    cert_id: CertId,
    status: Node,
    this_update: f64,
    next_update: Option<f64>,
}

fn generalized_time(node: Option<&Node>, target: &str) -> Result<f64, String> {
    node.filter(|n| n.is_universal(der::GENERALIZED_TIME))
        .and_then(Node::time)
        .ok_or_else(|| schema(target))
}

impl SingleResponse {
    fn read(node: &Node) -> Result<SingleResponse, String> {
        let error = || schema("SingleResponse");
        if !node.is_sequence() || node.children.len() < 3 {
            return Err(error());
        }
        let cert_id = CertId::read(&node.children[0])?;
        let status = node.children[1].clone();
        if status.class != der::CONTEXT || status.tag > 2 {
            return Err(error());
        }
        let this_update = generalized_time(node.children.get(2), "SingleResponse")?;
        let mut next_update = None;
        for member in &node.children[3..] {
            if member.is_context(0) && member.constructed {
                next_update = Some(generalized_time(member.children.first(), "SingleResponse")?);
            } else if !(member.is_context(1) && member.constructed) {
                return Err(error());
            }
        }
        Ok(SingleResponse { cert_id, status, this_update, next_update })
    }
}

struct BasicResponse {
    tbs: Vec<u8>,
    responder: ResponderId,
    produced_at: f64,
    responses: Vec<SingleResponse>,
    signature_algorithm: AlgId,
    signature: Vec<u8>,
    certs: Vec<Certificate>,
}

impl BasicResponse {
    fn read(node: &Node) -> Result<BasicResponse, String> {
        let error = || schema("BasicOCSPResponse");
        if !node.is_sequence() || node.children.len() < 3 {
            return Err(error());
        }
        let data = &node.children[0];
        if !data.is_sequence() {
            return Err(error());
        }
        let mut members = data.children.iter().peekable();
        if members.peek().is_some_and(|m| m.is_context(0) && m.constructed) {
            members.next();
        }
        let id = members.next().ok_or_else(error)?;
        let responder = if id.is_context(1) && id.constructed {
            ResponderId::Name(id.children.first().and_then(Name::read).ok_or_else(error)?)
        } else if id.is_context(2) && id.constructed {
            ResponderId::KeyHash(octets_of(id.children.first(), "BasicOCSPResponse")?)
        } else {
            return Err(error());
        };
        let produced_at = generalized_time(members.next(), "BasicOCSPResponse")?;
        let list = members.next().filter(|n| n.is_sequence()).ok_or_else(error)?;
        let responses =
            list.children.iter().map(SingleResponse::read).collect::<Result<Vec<_>, _>>()?;
        let signature_algorithm = AlgId::read(&node.children[1]).ok_or_else(error)?;
        let signature = node.children[2].clone();
        if !signature.is_universal(der::BIT_STRING) {
            return Err(error());
        }
        let mut certs = Vec::new();
        if let Some(wrapper) = node.children.get(3) {
            let list = wrapper
                .children
                .first()
                .filter(|n| wrapper.is_context(0) && n.is_sequence())
                .ok_or_else(error)?;
            for cert in &list.children {
                certs.push(Certificate::read(cert).ok_or_else(error)?);
            }
        }
        Ok(BasicResponse {
            tbs: data.raw.clone(),
            responder,
            produced_at,
            responses,
            signature_algorithm,
            signature: signature.bits(),
            certs,
        })
    }
}

fn check_ocsp(
    der: &[u8],
    certificate: &Certificate,
    issuer: &Certificate,
) -> Result<AnswerCheck, String> {
    let nothing = |problem, detail: String| {
        Ok(AnswerCheck::Nothing { kind: AnswerKind::Ocsp, problem, detail })
    };
    let Some(outer) = parse_ber(der) else {
        return nothing(RevocationProblem::Unreadable, "the OCSP response is not valid DER".into());
    };
    // OCSPResponse ::= sequence { responseStatus enumerated, responseBytes [0] explicit optional }
    let error = || schema("OCSPResponse");
    if !outer.is_sequence() {
        return Err(error());
    }
    let status = outer.children.first().filter(|n| n.is_universal(10)).ok_or_else(error)?;
    let status = small_integer(status);
    if status != 0 {
        return nothing(
            RevocationProblem::NotSuccessful,
            format!("the responder answered \"{}\"", ocsp_error(status)),
        );
    }
    let Some(wrapper) = outer.children.get(1) else {
        return nothing(
            RevocationProblem::Unreadable,
            "the OCSP response carries no answer".into(),
        );
    };
    let bytes = wrapper
        .children
        .first()
        .filter(|n| wrapper.is_context(0) && n.is_sequence() && n.children.len() == 2)
        .ok_or_else(error)?;
    let response_type = bytes.children[0].oid().ok_or_else(error)?;
    let response = octets_of(bytes.children.get(1), "ResponseBytes")?;
    if response_type != oids::OCSP_BASIC {
        return nothing(
            RevocationProblem::Unsupported,
            format!("OCSP response type {response_type}"),
        );
    }
    let Some(inner) = parse_ber(&response) else {
        return nothing(RevocationProblem::Unreadable, "the OCSP answer is not valid DER".into());
    };
    let basic = BasicResponse::read(&inner)?;

    let mut single = None;
    let mut unknown_hash = None;
    for candidate in &basic.responses {
        let Some(hash) = Digest::from_oid(&candidate.cert_id.hash_algorithm.oid) else {
            unknown_hash = Some(candidate.cert_id.hash_algorithm.oid.clone());
            continue;
        };
        if candidate.cert_id.is_equal(&CertId::for_certificate(certificate, issuer, hash)) {
            single = Some(candidate);
            break;
        }
    }
    let Some(single) = single else {
        return match unknown_hash {
            Some(oid) => nothing(
                RevocationProblem::Unsupported,
                format!("the answer identifies its certificate with hash {oid}"),
            ),
            None => nothing(
                RevocationProblem::OtherCertificate,
                "the OCSP response is about another certificate".into(),
            ),
        };
    };

    let mut candidates = vec![issuer.clone()];
    candidates.extend(basic.certs.iter().cloned());
    let Some(signer) = find_responder(&basic.responder, &candidates) else {
        return nothing(
            RevocationProblem::WrongSigner,
            "the responder's certificate is not in the response".into(),
        );
    };
    let own = signer.raw == issuer.raw;
    if !own && let Some(problem) = delegation_problem(signer, issuer, basic.produced_at) {
        return nothing(RevocationProblem::WrongSigner, problem.into());
    }
    match verifies(&basic.tbs, &basic.signature, signer, &basic.signature_algorithm) {
        Verified::Unsupported => {
            return nothing(
                RevocationProblem::Unsupported,
                format!(
                    "signature algorithm {} of the OCSP response",
                    basic.signature_algorithm.oid
                ),
            );
        }
        Verified::Invalid => {
            return nothing(
                RevocationProblem::Signature,
                "the OCSP response's signature does not verify".into(),
            );
        }
        Verified::Valid => {}
    }
    let window = AnswerWindow {
        kind: AnswerKind::Ocsp,
        this_update: single.this_update,
        next_update: single.next_update,
        responder: (!own).then(|| signer.raw.clone()),
    };
    match single.status.tag {
        0 => Ok(AnswerCheck::Good(window)),
        1 => {
            let members = &single.status.children;
            let Some(time) = members.first().filter(|n| n.is_universal(der::GENERALIZED_TIME))
            else {
                return nothing(
                    RevocationProblem::Unreadable,
                    "the OCSP response names no revocation time".into(),
                );
            };
            let revoked_at = time.time().ok_or_else(error)?;
            let code = members.get(1).and_then(|r| r.children.first()).map(small_integer);
            Ok(AnswerCheck::Revoked { revoked_at, reason: code.and_then(reason_name), window })
        }
        _ => nothing(
            RevocationProblem::UnknownStatus,
            "the responder does not know the certificate".into(),
        ),
    }
}

/// `asn1js.fromBER`, `None` where its offset is -1.
fn parse_ber(bytes: &[u8]) -> Option<Node> {
    match crate::asn1js::from_ber(bytes) {
        crate::asn1js::Verdict::Accepted => der::from_ber(bytes),
        _ => None,
    }
}

/// `findResponder`.
fn find_responder<'a>(id: &ResponderId, candidates: &'a [Certificate]) -> Option<&'a Certificate> {
    candidates.iter().find(|candidate| match id {
        ResponderId::Name(name) => candidate.subject.is_equal(name),
        ResponderId::KeyHash(hash) => &Digest::Sha1.hash(&candidate.spki.key) == hash,
    })
}

/// `delegationProblem` (RFC 6960 4.2.2.2).
fn delegation_problem(
    responder: &Certificate,
    issuer: &Certificate,
    produced_at: f64,
) -> Option<&'static str> {
    if !responder.issuer.is_equal(&issuer.subject) {
        return Some("the responder's certificate was not issued by the certificate's issuer");
    }
    let issued = crypto::verify_with_public_key(
        &responder.tbs,
        &responder.signature,
        &issuer.spki,
        &responder.signature_algorithm,
        None,
    ) == Ok(true);
    if !issued {
        return Some("the responder's certificate is not signed by the certificate's issuer");
    }
    if !extended_key_usage(responder).iter().any(|p| p == oids::OCSP_SIGNING) {
        return Some("the responder's certificate is not made for answering about certificates");
    }
    if produced_at < responder.not_before || produced_at > responder.not_after {
        return Some("the responder's certificate was outside its dates when it answered");
    }
    None
}

/// The key purposes of a certificate's extended key usage, empty without one.
pub fn extended_key_usage(certificate: &Certificate) -> Vec<String> {
    certificate
        .extension(oids::EXTENDED_KEY_USAGE)
        .and_then(|e| e.parsed())
        .filter(|n| n.is_sequence())
        .map(|n| n.children.iter().filter_map(Node::oid).collect())
        .unwrap_or_default()
}

/// Whether a certificate's basic constraints say `cA: true`.
pub fn is_authority(certificate: &Certificate) -> bool {
    certificate
        .extension(oids::BASIC_CONSTRAINTS)
        .and_then(|e| e.parsed())
        .and_then(|n| n.children.first().cloned())
        .is_some_and(|b| b.is_universal(1) && b.content.first().is_some_and(|&v| v != 0))
}

/// One extension of a CRL or an entry.
struct CrlExtension {
    oid: String,
    critical: bool,
    value: Vec<u8>,
}

fn read_extensions(node: &Node, target: &str) -> Result<Vec<CrlExtension>, String> {
    if !node.is_sequence() {
        return Err(schema(target));
    }
    let mut out = Vec::new();
    for ext in &node.children {
        let oid = ext.children.first().and_then(Node::oid).ok_or_else(|| schema(target))?;
        let (critical, value) = match ext.children.get(1) {
            Some(b) if b.is_universal(1) => (
                b.content.first().is_some_and(|&v| v != 0),
                ext.children.get(2).ok_or_else(|| schema(target))?,
            ),
            Some(v) => (false, v),
            None => return Err(schema(target)),
        };
        if !value.is_octet_string() || value.constructed {
            return Err(schema(target));
        }
        out.push(CrlExtension { oid, critical, value: value.content.clone() });
    }
    Ok(out)
}

struct RevokedEntry {
    serial: Vec<u8>,
    revocation_date: f64,
    extensions: Vec<CrlExtension>,
}

struct Crl {
    tbs: Vec<u8>,
    issuer: Name,
    this_update: f64,
    next_update: Option<f64>,
    revoked: Vec<RevokedEntry>,
    extensions: Vec<CrlExtension>,
    signature_algorithm: AlgId,
    signature: Vec<u8>,
}

fn is_time(node: &Node) -> bool {
    node.is_universal(der::UTC_TIME) || node.is_universal(der::GENERALIZED_TIME)
}

impl Crl {
    fn read(node: &Node) -> Result<Crl, String> {
        let error = || schema("CertificateRevocationList");
        if !node.is_sequence() || node.children.len() != 3 {
            return Err(error());
        }
        let tbs = &node.children[0];
        if !tbs.is_sequence() {
            return Err(error());
        }
        let mut members = tbs.children.iter().peekable();
        if members.peek().is_some_and(|m| m.is_universal(der::INTEGER)) {
            members.next();
        }
        AlgId::read(members.next().ok_or_else(error)?).ok_or_else(error)?;
        let issuer = members.next().and_then(Name::read).ok_or_else(error)?;
        let this_update =
            members.next().filter(|n| is_time(n)).and_then(Node::time).ok_or_else(error)?;
        let mut next_update = None;
        if members.peek().is_some_and(|n| is_time(n)) {
            next_update = Some(members.next().and_then(Node::time).ok_or_else(error)?);
        }
        let mut revoked = Vec::new();
        if members.peek().is_some_and(|n| n.is_sequence()) {
            for entry in &members.next().ok_or_else(error)?.children {
                if !entry.is_sequence() || entry.children.len() < 2 {
                    return Err(error());
                }
                let serial = entry.children[0].clone();
                if !serial.is_universal(der::INTEGER) || !is_time(&entry.children[1]) {
                    return Err(error());
                }
                let revocation_date = entry.children[1].time().ok_or_else(error)?;
                let extensions = match entry.children.get(2) {
                    Some(list) => read_extensions(list, "CertificateRevocationList")?,
                    None => Vec::new(),
                };
                revoked.push(RevokedEntry { serial: serial.content, revocation_date, extensions });
            }
        }
        let mut extensions = Vec::new();
        if let Some(wrapper) = members.next() {
            let list = wrapper
                .children
                .first()
                .filter(|_| wrapper.is_context(0) && wrapper.constructed)
                .ok_or_else(error)?;
            extensions = read_extensions(list, "CertificateRevocationList")?;
        }
        if members.next().is_some() {
            return Err(error());
        }
        let signature_algorithm = AlgId::read(&node.children[1]).ok_or_else(error)?;
        let signature = &node.children[2];
        if !signature.is_universal(der::BIT_STRING) {
            return Err(error());
        }
        Ok(Crl {
            tbs: tbs.raw.clone(),
            issuer,
            this_update,
            next_update,
            revoked,
            extensions,
            signature_algorithm,
            signature: signature.bits(),
        })
    }
}

/// `KNOWN_CRL_EXTENSIONS`: extensions that change nothing about what a full
/// list from the issuer says.
const KNOWN_CRL_EXTENSIONS: [&str; 4] = [
    oids::CRL_NUMBER,
    oids::AUTHORITY_KEY_IDENTIFIER,
    oids::AUTHORITY_INFO_ACCESS,
    oids::FRESHEST_CRL,
];

/// `IssuingDistributionPoint`'s scope flags.
#[derive(Default)]
struct Scope {
    only_user: bool,
    only_ca: bool,
    some_reasons: bool,
    indirect: bool,
    only_attribute: bool,
}

fn read_scope(value: &[u8]) -> Option<Scope> {
    let node = parse_ber(value)?;
    if !node.is_sequence() {
        return None;
    }
    let flag = |n: &Node| n.content.first().is_some_and(|&b| b != 0);
    let mut scope = Scope::default();
    for member in &node.children {
        if member.class != der::CONTEXT {
            return None;
        }
        match member.tag {
            0 => {}
            1 => scope.only_user = flag(member),
            2 => scope.only_ca = flag(member),
            3 => scope.some_reasons = true,
            4 => scope.indirect = flag(member),
            5 => scope.only_attribute = flag(member),
            _ => return None,
        }
    }
    Some(scope)
}

fn check_crl(
    der: &[u8],
    certificate: &Certificate,
    issuer: &Certificate,
) -> Result<AnswerCheck, String> {
    let nothing = |problem, detail: String| {
        Ok(AnswerCheck::Nothing { kind: AnswerKind::Crl, problem, detail })
    };
    let Some(node) = parse_ber(der) else {
        return nothing(RevocationProblem::Unreadable, "the CRL is not valid DER".into());
    };
    let crl = Crl::read(&node)?;
    if !crl.issuer.is_equal(&issuer.subject) {
        return nothing(
            RevocationProblem::OtherCertificate,
            "the CRL is not from the certificate's issuer".into(),
        );
    }
    for extension in &crl.extensions {
        if extension.oid == oids::DELTA_CRL_INDICATOR {
            return nothing(
                RevocationProblem::DeltaCrl,
                "the CRL lists only what changed since another list".into(),
            );
        }
        if extension.oid == oids::ISSUING_DISTRIBUTION_POINT {
            let Some(scope) = read_scope(&extension.value) else {
                return nothing(
                    RevocationProblem::Unreadable,
                    "the CRL's scope cannot be read".into(),
                );
            };
            if scope.indirect {
                return nothing(
                    RevocationProblem::IndirectCrl,
                    "the CRL lists certificates of other issuers".into(),
                );
            }
            let authority = is_authority(certificate);
            if scope.only_attribute
                || (scope.only_user && authority)
                || (scope.only_ca && !authority)
                || scope.some_reasons
            {
                return nothing(
                    RevocationProblem::OutOfScope,
                    "the CRL does not cover this kind of certificate".into(),
                );
            }
        } else if extension.critical && !KNOWN_CRL_EXTENSIONS.contains(&extension.oid.as_str()) {
            return nothing(
                RevocationProblem::Unsupported,
                format!("critical CRL extension {}", extension.oid),
            );
        }
    }
    if !may_sign_crls(issuer) {
        return nothing(
            RevocationProblem::WrongSigner,
            "the issuer's certificate is not made for signing CRLs".into(),
        );
    }
    match verifies(&crl.tbs, &crl.signature, issuer, &crl.signature_algorithm) {
        Verified::Unsupported => {
            return nothing(
                RevocationProblem::Unsupported,
                format!("signature algorithm {}", crl.signature_algorithm.oid),
            );
        }
        Verified::Invalid => {
            return nothing(
                RevocationProblem::Signature,
                "the CRL's signature does not verify".into(),
            );
        }
        Verified::Valid => {}
    }
    let window = AnswerWindow {
        kind: AnswerKind::Crl,
        this_update: crl.this_update,
        next_update: crl.next_update,
        responder: None,
    };
    let serial = integer_bytes(&certificate.serial);
    let Some(entry) = crl.revoked.iter().find(|e| integer_bytes(&e.serial) == serial) else {
        return Ok(AnswerCheck::Good(window));
    };
    let code = entry
        .extensions
        .iter()
        .find(|e| e.oid == oids::CRL_REASON)
        .and_then(|e| parse_ber(&e.value))
        .filter(|n| n.is_universal(10))
        .map(|n| small_integer(&n));
    Ok(AnswerCheck::Revoked {
        revoked_at: entry.revocation_date,
        reason: code.and_then(reason_name),
        window,
    })
}

/// `maySignCrls`: a certificate that names its key's uses must name CRL
/// signing among them (RFC 5280 4.2.1.3).
fn may_sign_crls(issuer: &Certificate) -> bool {
    let Some(usage) = issuer.extension(oids::KEY_USAGE) else { return true };
    let bits = usage.parsed().filter(|n| n.is_universal(der::BIT_STRING)).map(|n| n.bits());
    bits.is_some_and(|b| b.first().is_some_and(|&first| first & 0x02 != 0))
}

/// `integerBytes`: an integer's bytes without the leading zeros.
fn integer_bytes(bytes: &[u8]) -> &[u8] {
    let mut start = 0;
    while start + 1 < bytes.len() && bytes[start] == 0 {
        start += 1;
    }
    &bytes[start..]
}

enum Verified {
    Valid,
    Invalid,
    Unsupported,
}

/// `verifies`: a signature over `signed` with `signer`'s key.
fn verifies(signed: &[u8], signature: &[u8], signer: &Certificate, algorithm: &AlgId) -> Verified {
    match crypto::verify_with_public_key(signed, signature, &signer.spki, algorithm, None) {
        Ok(true) => Verified::Valid,
        Ok(false) => Verified::Invalid,
        Err(message) => {
            let lower = message.to_lowercase();
            if ["unsupported", "unknown", "not supported", "not implemented", "incorrect"]
                .iter()
                .any(|w| lower.contains(w))
            {
                Verified::Unsupported
            } else {
                Verified::Invalid
            }
        }
    }
}
