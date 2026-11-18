//! The catch-up's engine kinds through the one wire call a Swift or Kotlin
//! facade drives: `signatures.validationInfo` (read) and
//! `signatures.addValidationInfo` (write) of #1088, each entry a payload
//! beside the JSON.

use std::sync::Arc;

use epdf_uniffi::{PdfEngine, PdfRequest};
use serde_json::{Value, json};

const DOCUMENT: &str = "packages/engine/main/test/fixtures/unsigned_sigfield.pdf";
const CERTIFICATE: &str =
    "packages/engine/main/test/fixtures/signature-compat/keys/TEST-ONLY-signer.pem";

fn call(engine: &PdfEngine, request: Value, payloads: Vec<Vec<u8>>) -> (Value, Vec<Vec<u8>>) {
    let response = engine
        .call(PdfRequest { json: request.to_string(), payloads })
        .expect("the engine thread answered");
    (serde_json::from_str(&response.json).expect("the envelope is JSON"), response.payloads)
}

fn der_of(pem: &[u8]) -> Vec<u8> {
    use base64::Engine as _;
    let text = String::from_utf8_lossy(pem);
    let body: String = text.lines().filter(|l| !l.starts_with("-----")).collect();
    base64::engine::general_purpose::STANDARD.decode(body).unwrap()
}

fn opened() -> Arc<PdfEngine> {
    let engine = PdfEngine::new();
    let (json, _) = call(
        &engine,
        json!({ "kind": "open.fatMem", "effect": "open", "jobId": 1, "docId": "d" }),
        vec![test_support::fixture(DOCUMENT)],
    );
    assert!(json.get("error").is_none(), "{json}");
    engine
}

#[test]
fn validation_data_is_read_and_stored_through_one_call() {
    let engine = opened();
    let read = || {
        call(
            &engine,
            json!({ "kind": "signatures.validationInfo", "effect": "read", "jobId": 2, "docId": "d" }),
            Vec::new(),
        )
    };
    let (json, payloads) = read();
    assert_eq!(json, json!({ "result": { "certificates": [], "ocspResponses": [], "crls": [] } }));
    assert!(payloads.is_empty());

    let certificate = der_of(&test_support::fixture(CERTIFICATE));
    let (json, _) = call(
        &engine,
        json!({ "kind": "signatures.addValidationInfo", "effect": "write", "jobId": 3,
                "docId": "d", "opId": "op-1", "certificates": [{ "payload": 0 }] }),
        vec![certificate.clone()],
    );
    assert_eq!(json["result"]["result"]["added"]["certificates"], json!(1), "{json}");
    assert_eq!(json["result"]["event"]["type"], json!("signatures.validationInfoAdded"), "{json}");

    // Read back: the certificate is a payload beside the JSON.
    let (json, payloads) = read();
    assert_eq!(json["result"]["certificates"], json!([{ "payload": 0 }]), "{json}");
    assert_eq!(payloads, vec![certificate]);
}

#[test]
fn an_entry_that_is_not_bytes_is_invalid_arg() {
    let engine = opened();
    let (json, _) = call(
        &engine,
        json!({ "kind": "signatures.addValidationInfo", "effect": "write", "jobId": 2,
                "docId": "d", "opId": "op-1", "certificates": ["MIIB"] }),
        Vec::new(),
    );
    assert_eq!(json["error"]["code"], json!("InvalidArg"), "{json}");
}
