//! The D-1 transport as a host binding builds on it: the timestamp
//! authority and the revocation source of `epdf-signature-core`
//! (`httpTimestampAuthority`, `httpRevocationSource`) over a host's
//! [`HttpTransport`], and the flat JSON a host function speaks
//! (`epdf-web`, through the conformance harness).
//!
//! Upstream's clients call `fetch` themselves and draw the nonce from
//! `globalThis.crypto`. A native host has neither: it hands the request to
//! its own HTTP stack through the transport, and the nonce comes from
//! [`NonceSource`].

use std::rc::Rc;

use epdf_kernel::Error;
use epdf_kernel::bytes::Bytes;
use epdf_kernel::rt::Promise;
use epdf_signature_core::signers::{CryptoKey, CryptoKeyPair, WebCrypto};
use epdf_signature_core::transport::{
    HttpTransport, TransportHeader, TransportOutcome, TransportRequest,
};
use epdf_signature_core::{
    CmsSigner, HttpRevocationSourceOptions, HttpTimestampAuthorityOptions, RevocationSource,
    http_revocation_source, http_timestamp_authority,
};
use serde_json::{Value, json};

/// Where a timestamp request's nonce comes from.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum NonceSource {
    /// Fresh octets for every request, from the process's hashing keys
    /// (the kernel's UUID source does the same).
    #[default]
    Random,
    /// Every octet is this one, so a request is the same each time and a
    /// recorded reply can answer it. For tests only.
    Fixed(u8),
}

/// The only part of WebCrypto a timestamp authority uses: the nonce.
struct NonceCrypto(NonceSource);

fn not_here() -> Error {
    Error::new("[signature] a timestamp authority's crypto only draws nonces")
}

impl WebCrypto for NonceCrypto {
    fn generate_key(&self, _: Value, _: bool, _: Vec<String>) -> Promise<CryptoKeyPair> {
        Promise::rejected(not_here())
    }

    fn sign(&self, _: Value, _: &CryptoKey, _: Vec<u8>) -> Promise<Vec<u8>> {
        Promise::rejected(not_here())
    }

    fn export_jwk(&self, _: &CryptoKey) -> Promise<Value> {
        Promise::rejected(not_here())
    }

    fn get_random_values(&self, buffer: &mut [u8]) {
        match self.0 {
            NonceSource::Fixed(octet) => buffer.fill(octet),
            NonceSource::Random => {
                use std::hash::{BuildHasher, Hasher};
                use std::sync::atomic::{AtomicU64, Ordering};
                static DRAWN: AtomicU64 = AtomicU64::new(0);
                for chunk in buffer.chunks_mut(8) {
                    let mut hash = std::collections::hash_map::RandomState::new().build_hasher();
                    hash.write_u64(DRAWN.fetch_add(1, Ordering::Relaxed));
                    let octets = hash.finish().to_be_bytes();
                    chunk.copy_from_slice(&octets[..chunk.len()]);
                }
            }
        }
    }
}

/// `httpTimestampAuthority(options)` over a host's transport.
pub fn timestamp_authority(
    transport: Rc<dyn HttpTransport>,
    options: HttpTimestampAuthorityOptions,
    nonce: NonceSource,
) -> CmsSigner {
    http_timestamp_authority(transport, Rc::new(NonceCrypto(nonce)), options)
}

/// `httpRevocationSource(options)` over a host's transport.
pub fn revocation_source(
    transport: Rc<dyn HttpTransport>,
    options: HttpRevocationSourceOptions,
) -> Rc<dyn RevocationSource> {
    http_revocation_source(transport, options)
}

/// A request as a host function gets it: `{ url, contentType, headers:
/// [{ name, value }], body: {"$bytes"}, timeoutMs, maxReplyBytes }`.
pub fn request_json(request: &TransportRequest) -> Value {
    json!({
        "url": request.url,
        "contentType": request.content_type,
        "headers": request.headers.iter()
            .map(|h| json!({ "name": h.name, "value": h.value }))
            .collect::<Vec<_>>(),
        "body": Bytes(request.body.clone()).to_json(),
        "timeoutMs": request.timeout_ms,
        "maxReplyBytes": request.max_reply_bytes,
    })
}

fn bad_outcome(value: &Value) -> Error {
    Error::new(format!("[signature] a transport answered {value}, not a transport outcome"))
}

/// A host function's answer: `{ kind: 'replied', status, body }`,
/// `{ kind: 'timedOut' }`, `{ kind: 'tooLarge' }` or `{ kind: 'failed',
/// message }`. Anything else is an error, which the client counts as a
/// failure.
pub fn outcome_from_json(value: &Value) -> Result<TransportOutcome, Error> {
    match value.get("kind").and_then(Value::as_str) {
        Some("replied") => {
            let status = value
                .get("status")
                .and_then(Value::as_u64)
                .and_then(|s| u16::try_from(s).ok())
                .ok_or_else(|| bad_outcome(value))?;
            let body =
                value.get("body").and_then(Bytes::from_json).ok_or_else(|| bad_outcome(value))?;
            Ok(TransportOutcome::Replied { status, body: body.0 })
        }
        Some("timedOut") => Ok(TransportOutcome::TimedOut),
        Some("tooLarge") => Ok(TransportOutcome::TooLarge),
        Some("failed") => Ok(TransportOutcome::Failed {
            message: value.get("message").and_then(Value::as_str).unwrap_or_default().to_string(),
        }),
        _ => Err(bad_outcome(value)),
    }
}

/// `{ name, value }` headers out of JSON; anything else is left out.
pub fn headers_from_json(value: Option<&Value>) -> Vec<TransportHeader> {
    value
        .and_then(Value::as_array)
        .map(|headers| {
            headers
                .iter()
                .filter_map(|h| {
                    Some(TransportHeader {
                        name: h.get("name")?.as_str()?.to_string(),
                        value: h.get("value")?.as_str()?.to_string(),
                    })
                })
                .collect()
        })
        .unwrap_or_default()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_request_and_each_outcome_cross_as_flat_json() {
        let request = TransportRequest {
            url: "https://tsa.example".into(),
            content_type: "application/timestamp-query".into(),
            headers: vec![TransportHeader { name: "x-key".into(), value: "k".into() }],
            body: vec![1, 2],
            timeout_ms: 10_000,
            max_reply_bytes: 1_048_576,
        };
        assert_eq!(
            request_json(&request),
            json!({ "url": "https://tsa.example", "contentType": "application/timestamp-query",
                    "headers": [{ "name": "x-key", "value": "k" }], "body": { "$bytes": "AQI=" },
                    "timeoutMs": 10000, "maxReplyBytes": 1048576 })
        );
        assert_eq!(
            outcome_from_json(
                &json!({ "kind": "replied", "status": 200, "body": { "$bytes": "AQI=" } })
            )
            .unwrap(),
            TransportOutcome::Replied { status: 200, body: vec![1, 2] }
        );
        assert_eq!(
            outcome_from_json(&json!({ "kind": "timedOut" })).unwrap(),
            TransportOutcome::TimedOut
        );
        assert_eq!(
            outcome_from_json(&json!({ "kind": "tooLarge" })).unwrap(),
            TransportOutcome::TooLarge
        );
        assert_eq!(
            outcome_from_json(&json!({ "kind": "failed", "message": "offline" })).unwrap(),
            TransportOutcome::Failed { message: "offline".into() }
        );
        assert!(
            outcome_from_json(
                &json!({ "kind": "replied", "status": 70000, "body": { "$bytes": "" } })
            )
            .is_err()
        );
        assert!(outcome_from_json(&json!(null)).is_err());
    }

    #[test]
    fn a_fixed_nonce_source_fills_every_octet() {
        let mut octets = [0u8; 8];
        NonceCrypto(NonceSource::Fixed(0x5a)).get_random_values(&mut octets);
        assert_eq!(octets, [0x5a; 8]);
        let (mut a, mut b) = ([0u8; 8], [0u8; 8]);
        NonceCrypto(NonceSource::Random).get_random_values(&mut a);
        NonceCrypto(NonceSource::Random).get_random_values(&mut b);
        assert_ne!(a, b);
    }
}
