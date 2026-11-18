//! `signers/timestamp-http.ts` (RFC 3161 3.4) over the host transport, and
//! `signers/ecdsa.ts`.

use std::rc::Rc;

use epdf_kernel::Error;
use epdf_kernel::rt::Promise;
use serde_json::{Value, json};

use crate::oids::Digest;
use crate::parse::{CmsContent, read_cms_parts};
use crate::sign::{CmsSignRequest, CmsSigner};
use crate::signers::{WebCrypto, random_positive_integer};
use crate::splice::{children_of, read_element};
use crate::timestamp::{read_timestamp_info, without_leading_zeros};
use crate::timestamp_protocol::{build_timestamp_request, read_timestamp_response};
use crate::transport::{
    DEFAULT_TIMEOUT_MS, HttpTransport, MAX_TIMESTAMP_REPLY_BYTES, TransportHeader,
    TransportRequest, post_with_retries,
};

/// `HttpTimestampAuthorityOptions`.
#[derive(Clone, Debug, PartialEq)]
pub struct HttpTimestampAuthorityOptions {
    /// The authority's address, or the app's own route in front of it.
    pub url: String,
    /// What `sign()` stamps with. Default `sha256`.
    pub hash: Option<Digest>,
    /// The policy to ask for (an OID).
    pub policy: Option<String>,
    pub headers: Vec<TransportHeader>,
    /// Default 10000.
    pub timeout_ms: Option<u32>,
    /// Further attempts when no answer came. Default 1.
    pub retries: Option<u32>,
}

/// `httpTimestampAuthority`: a timestamp authority reached over the host's
/// transport. It asks for exactly the algorithm it is called with, with a
/// nonce and the authority's certificate, and checks that the reply answers
/// this request. Every error carries the authority's `url` in
/// `details.authority`.
pub fn http_timestamp_authority(
    transport: Rc<dyn HttpTransport>,
    crypto: Rc<dyn WebCrypto>,
    options: HttpTimestampAuthorityOptions,
) -> CmsSigner {
    let hash = Some(options.hash.unwrap_or(Digest::Sha256));
    let sign = Rc::new(move |request: CmsSignRequest| {
        let transport = transport.clone();
        let crypto = crypto.clone();
        let options = options.clone();
        Promise::spawn(async move { ask_authority(&transport, &*crypto, &options, request).await })
    });
    CmsSigner { sign, hash }
}

fn details(url: &str, more: Value) -> Value {
    let mut out = json!({ "authority": url });
    if let (Some(out), Some(more)) = (out.as_object_mut(), more.as_object()) {
        out.extend(more.clone());
    }
    out
}

async fn ask_authority(
    transport: &Rc<dyn HttpTransport>,
    crypto: &dyn WebCrypto,
    options: &HttpTimestampAuthorityOptions,
    request: CmsSignRequest,
) -> Result<Vec<u8>, Error> {
    let url = &options.url;
    let refused = |message: String, more: Value| {
        Error::engine("SignatureRefused", message).with("details", details(url, more))
    };
    let wrong_answer = |reason: &str, what: &str| {
        refused(format!("the timestamp authority at {url} {what}"), json!({ "reason": reason }))
    };
    if request.sub_filter != "ETSI.RFC3161" {
        return Err(Error::engine(
            "InvalidArg",
            "a timestamp authority can't sign a document: pass it as `timestampAuthority`, or sign with `kind: 'timestamp'`",
        )
        .with("details", details(url, json!({}))));
    }
    let algorithm = Digest::from_name(&request.algorithm).ok_or_else(|| {
        Error::named("TypeError", "Cannot read properties of undefined (reading 'byteLength')")
    })?;
    // Positive, and without a leading zero octet, so it reads back as it was sent.
    let nonce = random_positive_integer(crypto);
    let query =
        build_timestamp_request(&request.digest, algorithm, options.policy.as_deref(), &nonce);
    let timeout_ms = options.timeout_ms.unwrap_or(DEFAULT_TIMEOUT_MS);
    let post = TransportRequest {
        url: url.clone(),
        content_type: "application/timestamp-query".into(),
        headers: options.headers.clone(),
        body: query,
        timeout_ms,
        max_reply_bytes: MAX_TIMESTAMP_REPLY_BYTES,
    };
    let (status, reply) =
        post_with_retries(transport, &post, options.retries.unwrap_or(1), |status, _| {
            format!("HTTP {status}")
        })
        .await
        .map_err(|why| {
            Error::engine(
                "Network",
                format!("the timestamp authority at {url} did not answer: {why}"),
            )
            .with("details", details(url, json!({})))
        })?;
    if !(200..300).contains(&status) {
        return Err(refused(
            format!("the timestamp authority at {url} refused the request: HTTP {status}"),
            json!({ "httpStatus": status }),
        ));
    }
    let response = read_timestamp_response(&reply).map_err(|e| {
        refused(
            format!(
                "the timestamp authority at {url} sent a reply that can't be read: {}",
                e.message()
            ),
            json!({ "reason": "malformed" }),
        )
    })?;
    if !response.granted {
        let said: Vec<&str> = [response.fail_info.as_deref(), response.status_text.as_deref()]
            .into_iter()
            .flatten()
            .filter(|s| !s.is_empty())
            .collect();
        let said = if said.is_empty() { String::new() } else { format!(" ({})", said.join(": ")) };
        return Err(refused(
            format!("the timestamp authority at {url} refused the request{said}"),
            json!({ "failInfo": response.fail_info }),
        ));
    }
    let Some(token) = response.token else {
        return Err(wrong_answer("malformed", "granted the request but sent no token"));
    };
    let info = read_cms_parts(&token, CmsContent::TstInfo, &[])
        .and_then(|parts| read_timestamp_info(parts.e_content.as_deref().unwrap_or_default()))
        .map_err(|e| {
            refused(
                format!(
                    "the timestamp authority at {url} sent a token that can't be read: {}",
                    e.message()
                ),
                json!({ "reason": "malformed" }),
            )
        })?;
    if info.imprint_algorithm != algorithm {
        return Err(wrong_answer(
            "algorithm-mismatch",
            &format!(
                "stamped a {} digest where {} was asked for",
                info.imprint_algorithm.name(),
                algorithm.name()
            ),
        ));
    }
    if info.imprint != request.digest {
        return Err(wrong_answer(
            "digest-mismatch",
            "stamped another digest than the one it was sent",
        ));
    }
    if info.nonce.as_deref() != Some(without_leading_zeros(&nonce).as_slice()) {
        return Err(wrong_answer(
            "profile-violation",
            "sent a token that doesn't answer this request: its nonce is not the one sent",
        ));
    }
    if let Some(policy) = options.policy.as_deref().filter(|p| !p.is_empty())
        && info.policy != policy
    {
        return Err(wrong_answer(
            "profile-violation",
            &format!("issued the token under policy {} where {policy} was asked for", info.policy),
        ));
    }
    Ok(token)
}

/// `ecdsaRawFromDer`: an ECDSA signature as a `RawSigner` returns it,
/// `r || s` at the curve's size, from the DER most signing services return.
/// `InvalidArg` for bytes that aren't one, or numbers too large.
pub fn ecdsa_raw_from_der(der: &[u8], curve: &str) -> Result<Vec<u8>, Error> {
    let size = match curve {
        "P-256" => 32,
        "P-384" => 48,
        "P-521" => 66,
        other => {
            return Err(Error::named("TypeError", format!("unknown curve {other}")));
        }
    };
    let invalid = |why: &str| {
        Error::engine("InvalidArg", format!("not a DER ECDSA signature: {why}"))
            .with("details", json!({ "field": "der" }))
    };
    let sequence = read_element(der, 0, der.len()).map_err(|_| invalid("it does not parse"))?;
    if sequence.tag != 0x30 {
        return Err(invalid("it is not a sequence"));
    }
    let numbers = children_of(der, &sequence).map_err(|_| invalid("it does not parse"))?;
    if numbers.len() != 2 || numbers.iter().any(|n| n.tag != 0x02) {
        return Err(invalid("it does not hold two integers"));
    }
    let mut raw = vec![0u8; size * 2];
    for (index, number) in numbers.iter().enumerate() {
        let mut start = number.content_start;
        while start + 1 < number.content_end && der[start] == 0 {
            start += 1;
        }
        let length = number.content_end - start;
        if length > size {
            return Err(invalid(&format!("a number is {length} bytes, {curve} takes {size}")));
        }
        let at = (index + 1) * size - length;
        raw[at..at + length].copy_from_slice(&der[start..number.content_end]);
    }
    Ok(raw)
}
