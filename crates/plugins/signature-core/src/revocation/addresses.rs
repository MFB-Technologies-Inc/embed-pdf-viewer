//! `revocation/addresses.ts`: the addresses a certificate names for asking
//! about it, and whether it says it can't be withdrawn.

use crate::cert::Certificate;
use crate::der::Node;
use crate::oids;

/// `RevocationAddresses`: the `http` and `https` addresses a certificate names.
#[derive(Clone, Debug, Default, PartialEq)]
pub struct RevocationAddresses {
    pub ocsp: Vec<String>,
    pub crl: Vec<String>,
    pub issuer: Vec<String>,
}

/// A general name that is a URI (RFC 5280 4.2.1.6).
const URI: u32 = 6;

fn parsed(certificate: &Certificate, oid: &str) -> Option<Node> {
    certificate.extension(oid).and_then(|e| e.parsed())
}

/// `webAddress`: a URI general name with an `http` or `https` scheme.
fn web_address(name: &Node) -> Option<String> {
    if !name.is_context(URI) || name.constructed {
        return None;
    }
    let value = String::from_utf8_lossy(&name.content).into_owned();
    let lower = value.to_ascii_lowercase();
    (lower.starts_with("http://") || lower.starts_with("https://")).then_some(value)
}

/// The access descriptions of an authority information access extension:
/// `(method, location)`.
fn access_descriptions(certificate: &Certificate) -> Vec<(String, Node)> {
    let Some(access) = parsed(certificate, oids::AUTHORITY_INFO_ACCESS) else { return Vec::new() };
    access
        .children
        .iter()
        .filter_map(|d| Some((d.children.first()?.oid()?, d.children.get(1)?.clone())))
        .collect()
}

/// `revocationAddresses`. A CRL address that covers only some reasons, or
/// names another issuer, is left out.
pub fn revocation_addresses(certificate: &Certificate) -> RevocationAddresses {
    let mut out = RevocationAddresses::default();
    for (method, location) in access_descriptions(certificate) {
        let Some(address) = web_address(&location) else { continue };
        if method == oids::ACCESS_OCSP {
            out.ocsp.push(address.clone());
        }
        if method == oids::ACCESS_ISSUERS {
            out.issuer.push(address);
        }
    }
    // DistributionPoint ::= sequence { distributionPoint [0] optional,
    //   reasons [1] optional, cRLIssuer [2] optional }
    // DistributionPointName ::= choice { fullName [0] GeneralNames, ... }
    if let Some(points) = parsed(certificate, oids::CRL_DISTRIBUTION_POINTS) {
        for point in &points.children {
            if point.children.iter().any(|m| m.is_context(1) || m.is_context(2)) {
                continue;
            }
            let full_name = point
                .children
                .iter()
                .find(|m| m.is_context(0))
                .and_then(|name| name.children.first())
                .filter(|n| n.is_context(0) && n.constructed);
            for name in full_name.map(|n| n.children.as_slice()).unwrap_or_default() {
                if let Some(address) = web_address(name) {
                    out.crl.push(address);
                }
            }
        }
    }
    out
}

/// `cannotBeRevoked`: whether a certificate that issues nothing says of
/// itself that it can't be withdrawn: "validity assured, short term", or
/// `noRevAvail` without an address to ask at.
pub fn cannot_be_revoked(certificate: &Certificate) -> bool {
    if super::answer::is_authority(certificate) {
        return false;
    }
    if certificate.extension(oids::VALIDITY_ASSURED_SHORT_TERM).is_some() {
        return true;
    }
    if certificate.extension(oids::NO_REV_AVAIL).is_none() {
        return false;
    }
    let names_where_to_ask = certificate.extension(oids::CRL_DISTRIBUTION_POINTS).is_some()
        || certificate.extension(oids::FRESHEST_CRL).is_some()
        || access_descriptions(certificate).iter().any(|(method, _)| method == oids::ACCESS_OCSP);
    !names_where_to_ask
}
