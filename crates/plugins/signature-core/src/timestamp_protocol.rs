//! `cms/timestamp-protocol.ts`: the two messages of the RFC 3161 protocol,
//! the request a client sends and the response it reads back.

use crate::der::{self, Node};
use crate::oids::Digest;
use crate::parse::ParseError;
use crate::splice::{children_of, read_element};
use crate::timestamp::{CONTEXT, Slot, matches, optional, required, sequence, universal};

/// `TimeStampResp.schema()`: `PKIStatusInfo.schema()`, then an optional
/// `ContentInfo.schema()`.
fn time_stamp_resp_schema() -> Slot {
    let status = sequence(vec![
        required(universal(der::INTEGER)),
        optional(Slot::Repeat(0, der::SEQUENCE, Box::new(universal(der::UTF8_STRING)))),
        optional(universal(der::BIT_STRING)),
    ]);
    let content_info = sequence(vec![
        required(universal(der::OID)),
        required(Slot::Cons(CONTEXT, 0, vec![required(Slot::Any)])),
    ]);
    sequence(vec![required(status), optional(content_info)])
}

/// `buildTimestampRequest`: a TimeStampReq (RFC 3161 2.4.1) asking for the
/// authority's certificate in the token, with `nonce`.
pub fn build_timestamp_request(
    digest: &[u8],
    algorithm: Digest,
    policy: Option<&str>,
    nonce: &[u8],
) -> Vec<u8> {
    let mut members = vec![
        der::integer(1),
        der::sequence(&[der::sequence(&[der::oid(algorithm.oid())]), der::octet_string(digest)]),
    ];
    if let Some(policy) = policy.filter(|p| !p.is_empty()) {
        members.push(der::oid(policy));
    }
    members.push(der::integer_bytes(nonce));
    members.push(der::boolean(true));
    der::sequence(&members)
}

/// `FAILURES`: what an authority can name (PKIFailureInfo, RFC 3161 2.4.2).
fn failure_name(bit: usize) -> String {
    match bit {
        0 => "badAlg".into(),
        2 => "badRequest".into(),
        5 => "badDataFormat".into(),
        14 => "timeNotAvailable".into(),
        15 => "unacceptedPolicy".into(),
        16 => "unacceptedExtension".into(),
        17 => "addInfoNotAvailable".into(),
        25 => "systemFailure".into(),
        other => format!("failure {other}"),
    }
}

/// `TimestampResponse`.
#[derive(Clone, Debug, PartialEq)]
pub struct TimestampResponse {
    pub granted: bool,
    pub status: i64,
    pub fail_info: Option<String>,
    pub status_text: Option<String>,
    /// The token in the bytes the authority wrote.
    pub token: Option<Vec<u8>>,
}

fn status_of(info: &Node) -> Result<(i64, Option<String>, Option<String>), String> {
    let schema = || der::schema_error("PKIStatusInfo").message;
    if !info.is_sequence() {
        return Err(schema());
    }
    let status =
        info.children.first().filter(|n| n.is_universal(der::INTEGER)).ok_or_else(schema)?;
    // asn1js reads a value of more than four octets as hex only.
    let value = if status.content.len() > 4 || status.content.is_empty() {
        None
    } else {
        let mut v: i64 = if status.content[0] & 0x80 != 0 { -1 } else { 0 };
        for &b in &status.content {
            v = (v << 8) | i64::from(b);
        }
        Some(v)
    };
    let Some(value) = value.filter(|v| (0..=5).contains(v)) else {
        return Err("PKIStatusInfo \"status\" has invalid value".into());
    };
    let mut status_text = None;
    let mut fail_info = None;
    for member in &info.children[1..] {
        if member.is_sequence() && status_text.is_none() && fail_info.is_none() {
            let texts: Vec<String> =
                member.children.iter().filter_map(Node::text).filter(|t| !t.is_empty()).collect();
            if !texts.is_empty() {
                status_text = Some(texts.join("; "));
            }
        } else if member.is_universal(der::BIT_STRING) && fail_info.is_none() {
            let bits = member.bits();
            for bit in 0..bits.len() * 8 {
                if bits[bit >> 3] & (0x80 >> (bit & 7)) != 0 {
                    fail_info = Some(failure_name(bit));
                    break;
                }
            }
            if fail_info.is_none() {
                fail_info = Some(String::new());
            }
        } else {
            // Past what the schema placed: never looked at.
            break;
        }
    }
    Ok((value, fail_info.filter(|f| !f.is_empty()), status_text))
}

/// `readTimestampResponse`: a TimeStampResp (RFC 3161 2.4.2). Fails with
/// `CmsError` for bytes that aren't one.
pub fn read_timestamp_response(bytes: &[u8]) -> Result<TimestampResponse, ParseError> {
    let wrap = |message: String| ParseError::Cms {
        reason: "malformed",
        message: format!("the reply is not a timestamp response: {message}"),
    };
    let root = match crate::asn1js::from_ber(bytes) {
        crate::asn1js::Verdict::Accepted => der::from_ber(bytes),
        _ => None,
    }
    .ok_or_else(|| wrap("not valid BER".into()))?;
    if !matches(&time_stamp_resp_schema(), &root) {
        return Err(wrap(der::schema_error("TimeStampResp").message));
    }
    let (status, fail_info, status_text) = status_of(&root.children[0]).map_err(wrap)?;
    let outer = read_element(bytes, 0, bytes.len()).map_err(|e| wrap(e.message().to_string()))?;
    let members = children_of(bytes, &outer).map_err(|e| wrap(e.message().to_string()))?;
    let token = members.get(1).map(|m| bytes[m.start..m.end].to_vec());
    Ok(TimestampResponse {
        granted: status == 0 || status == 1,
        status,
        fail_info,
        status_text,
        token,
    })
}
