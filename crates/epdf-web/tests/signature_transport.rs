//! D-1 in the browser build: a JavaScript function returning a promise is
//! the signature plugin's HTTP transport. It arrives in a plugin config as
//! `{"$fn": "js:<id>"}` under `transport`, beside `httpTimestampAuthority`'s
//! or `httpRevocationSource`'s options, takes the flat request and answers
//! the flat outcome (`epdf_plugin_signature::transport`). These build the
//! harness as `WasmKernel` does and replay recorded replies through it.

use std::collections::HashMap;

use epdf_signature_core::{CmsSignRequest, RevocationRequest};
use epdf_web::runtime::{entry, reset, start_clock, to_js};
use plugin_conformance::Harness;
use serde_json::{Value, json};
use wasm_bindgen::prelude::*;
use wasm_bindgen_futures::JsFuture;
use wasm_bindgen_test::wasm_bindgen_test;

/// A `call_js` that logs `[id, argsJson]` into `log` and answers the next
/// of `replies` in turn.
fn transport(log: &js_sys::Array, replies: &str) -> js_sys::Function {
    let make = js_sys::Function::new_with_args(
        "log",
        &format!(
            "const replies = {replies}; return (id, args) => {{ log.push([id, args]); return Promise.resolve(replies.shift()); }};"
        ),
    );
    make.call1(&JsValue::NULL, log).unwrap().into()
}

fn harness(call_js: js_sys::Function) -> Harness {
    Harness::live(epdf_web::adapters::adapters(), HashMap::new(), epdf_web::exports::live(call_js))
}

fn js_json(value: &JsValue) -> Value {
    serde_json::from_str(&js_sys::JSON::stringify(value).unwrap().as_string().unwrap()).unwrap()
}

/// The request the transport was handed on call `n`.
fn sent(log: &js_sys::Array, n: u32) -> Value {
    let call: js_sys::Array = log.get(n).into();
    assert_eq!(call.get(0).as_string().as_deref(), Some("t"));
    let args: Value = serde_json::from_str(&call.get(1).as_string().unwrap()).unwrap();
    args[0].clone()
}

#[wasm_bindgen_test]
async fn a_revocation_route_answers_a_recorded_reply_through_a_javascript_transport() {
    reset();
    let log = js_sys::Array::new();
    let h = harness(transport(
        &log,
        "[{ kind: 'replied', status: 200, body: { $bytes: 'MAMBAgM=' } }]",
    ));
    let spec = json!({
        "transport": { "$fn": "js:t" }, "url": "https://app.example/revocation",
        "headers": { "x-session": "t" }, "retries": 0,
    });
    let fetched = entry(|| {
        let source = epdf_plugin_signature::harness::revocation_source_of(&spec, &h).unwrap();
        let fetched =
            source.fetch(RevocationRequest::Crl { url: "http://crl.example/a.crl".into() });
        to_js(fetched.map(|bytes| Ok(json!(bytes))))
    });
    let bytes = JsFuture::from(fetched).await.unwrap();
    assert_eq!(js_json(&bytes), json!([0x30, 0x03, 1, 2, 3]));
    assert_eq!(log.length(), 1);
    let request = sent(&log, 0);
    assert_eq!(request["url"], "https://app.example/revocation");
    assert_eq!(request["contentType"], "application/json");
    assert_eq!(request["headers"], json!([{ "name": "x-session", "value": "t" }]));
    assert_eq!(request["timeoutMs"], 10_000);
    assert_eq!(request["maxReplyBytes"], 2 * 1024 * 1024);
    // `{"kind":"crl","url":"http://crl.example/a.crl"}`, base64.
    assert_eq!(
        request["body"],
        json!({ "$bytes": "eyJraW5kIjoiY3JsIiwidXJsIjoiaHR0cDovL2NybC5leGFtcGxlL2EuY3JsIn0=" })
    );
}

#[wasm_bindgen_test]
async fn a_timestamp_authority_reads_each_recorded_outcome_as_the_client_does() {
    reset();
    // As `WasmKernel::new` does: the kernel's byte source is Web Crypto.
    start_clock();
    let log = js_sys::Array::new();
    let h = harness(transport(
        &log,
        "[{ kind: 'replied', status: 503, body: { $bytes: '' } }, { kind: 'timedOut' }]",
    ));
    let spec = json!({
        "transport": { "$fn": "js:t" }, "url": "https://tsa.example/stamp",
        "retries": 1,
    });
    let stamped = entry(|| {
        let authority = epdf_plugin_signature::harness::timestamp_authority_of(&spec, &h).unwrap();
        let request = CmsSignRequest {
            digest: vec![7; 32],
            algorithm: "sha256".into(),
            sub_filter: "ETSI.RFC3161".into(),
        };
        to_js((authority.sign)(request).map(|token| Ok(json!(token))))
    });
    let error = JsFuture::from(stamped).await.unwrap_err();
    let error = js_json(&error);
    assert_eq!(
        error["$error"]["message"],
        "the timestamp authority at https://tsa.example/stamp did not answer: no reply within 10000 ms",
        "{error}"
    );
    // One retry after the 503, the same query both times.
    assert_eq!(log.length(), 2);
    let (first, second) = (sent(&log, 0), sent(&log, 1));
    assert_eq!(first, second);
    assert_eq!(first["contentType"], "application/timestamp-query");
    assert_eq!(first["maxReplyBytes"], 1024 * 1024);
}

/// Two nonces in the browser build differ: they come from Web Crypto, not
/// from hashing keys a page load can repeat.
#[wasm_bindgen_test]
async fn browser_nonces_come_from_web_crypto() {
    reset();
    start_clock();
    let mut a = [0u8; 8];
    let mut b = [0u8; 8];
    epdf_kernel::identity::fill_random(&mut a);
    epdf_kernel::identity::fill_random(&mut b);
    assert_ne!(a, b);
}

#[wasm_bindgen_test]
async fn an_answer_that_is_not_an_outcome_is_a_failure() {
    reset();
    let log = js_sys::Array::new();
    let h = harness(transport(&log, "[{ status: 200 }]"));
    let spec = json!({ "transport": { "$fn": "js:t" }, "url": "/revocation", "retries": 0 });
    let fetched = entry(|| {
        let source = epdf_plugin_signature::harness::revocation_source_of(&spec, &h).unwrap();
        to_js(
            source
                .fetch(RevocationRequest::Crl { url: "http://crl.example/a.crl".into() })
                .map(|bytes| Ok(json!(bytes))),
        )
    });
    let error = js_json(&JsFuture::from(fetched).await.unwrap_err());
    let message = error["$error"]["message"].as_str().unwrap();
    assert!(message.contains("not a transport outcome"), "{message}");
}
