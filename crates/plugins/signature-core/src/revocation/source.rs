//! `revocation/source.ts` and `revocation/request.ts`: one question for a
//! certificate authority, where answers come from, and the OCSP request.

use epdf_kernel::rt::Promise;

use super::answer::CertId;
use crate::cert::Certificate;
use crate::der;
use crate::oids::Digest;

/// `RevocationRequest`: an OCSP request for a responder, or an address to
/// fetch a CRL or an issuer's certificate from.
#[derive(Clone, Debug, PartialEq)]
pub enum RevocationRequest {
    Ocsp { url: String, body: Vec<u8> },
    Crl { url: String },
    Issuer { url: String },
}

impl RevocationRequest {
    pub fn kind(&self) -> &'static str {
        match self {
            RevocationRequest::Ocsp { .. } => "ocsp",
            RevocationRequest::Crl { .. } => "crl",
            RevocationRequest::Issuer { .. } => "issuer",
        }
    }

    pub fn url(&self) -> &str {
        match self {
            RevocationRequest::Ocsp { url, .. }
            | RevocationRequest::Crl { url }
            | RevocationRequest::Issuer { url } => url,
        }
    }
}

/// `RevocationSource`: where revocation answers come from. Whatever comes
/// back is checked before it is used.
pub trait RevocationSource {
    /// The bytes that answer `request`: an OCSP response, a CRL, or a
    /// certificate, each in DER.
    fn fetch(&self, request: RevocationRequest) -> Promise<Vec<u8>>;
}

/// `buildOcspRequest`: the OCSP request that asks about `certificate`,
/// which `issuer` issued (RFC 6960 4.1), identified with SHA-1 and without a
/// nonce, so the same question always has the same bytes.
pub fn build_ocsp_request(certificate: &Certificate, issuer: &Certificate) -> Vec<u8> {
    let cert_id = CertId::for_certificate(certificate, issuer, Digest::Sha1);
    // OCSPRequest ::= sequence { tbsRequest sequence { requestList sequence of
    //   Request sequence { reqCert CertID } } }
    der::sequence(&[der::sequence(&[der::sequence(&[der::sequence(&[cert_id.encode()])])])])
}
