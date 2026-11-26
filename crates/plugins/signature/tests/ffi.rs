#![cfg(feature = "uniffi")]
//! Migrated by hand to the Task 13.25 plugin: the engine's current ref
//! spelling, the
//! `cancelPending` and per-field `can_sign`/`can_fill` verbs, and
//! `SignatureEvent::InvalidationPredicted`.

use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::{Arc, Mutex};

use epdf_ffi::{CancelToken, FormFieldRef, KernelThread, ResourceStatus, ViewerError};
use epdf_plugin_form::form_plugin;
use epdf_plugin_interaction::{InteractionConfig, interaction_plugin};
use epdf_plugin_signature::ffi::{
    SignatureCoverage, SignatureEvent, SignatureEventListener, SignatureHandle, SignatureMode,
    SignatureModificationLevel, SignatureModificationsBasis, SignatureSummary,
    SignatureValidateRequest,
};
use epdf_plugin_signature::{SIGNATURE, SignatureConfig, signature_plugin};
use futures::executor::block_on;
use plugin_conformance::test_kernel::{Reply, TestKernel, TestSpec};
use serde_json::{Value, json};

#[derive(Default)]
struct Events(Mutex<Vec<SignatureEvent>>, AtomicUsize);

impl SignatureEventListener for Events {
    fn on_event(&self, event: SignatureEvent) {
        self.0.lock().unwrap().push(event);
    }
    fn on_end(&self) {
        self.1.fetch_add(1, Ordering::SeqCst);
    }
}

fn field(n: u32) -> Value {
    json!({ "kind": "objectNumber", "objectNumber": n })
}

fn page() -> Value {
    json!({ "kind": "objectNumber", "objectNumber": 1 })
}

/// One signed field (12) over revision 0 of two, one unsigned (14).
fn snapshot() -> Value {
    let signer = json!({ "name": "Ana", "reason": null, "location": null, "contactInfo": null,
                         "signedAt": "2026-09-01T12:00:00Z" });
    let base = |n: u32, signed: bool| {
        json!({
            "index": if n == 12 { 0 } else { 1 }, "field": field(n), "fieldName": format!("Sig{n}"),
            "widget": { "ref": { "kind": "objectNumber", "page": page(), "objectNumber": n + 1 },
                        "objectNumber": n + 1, "page": page() },
            "signed": signed, "kind": "signature",
            "filter": if signed { json!("Adobe.PPKLite") } else { Value::Null },
            "subFilter": if signed { json!("ETSI.CAdES.detached") } else { Value::Null },
            "byteRange": if signed { json!([0, 100, 200, 50]) } else { Value::Null },
            "contentsSizeBytes": if signed { 8192 } else { 0 },
            "coverage": if signed { json!("whole-revision") } else { Value::Null },
            "revisionIndex": if signed { json!(0) } else { Value::Null },
            "signer": signer, "docMdp": null, "catalogCertification": false,
            "fieldMdp": null, "lock": null, "seedValue": null
        })
    };
    json!({
        "chainValid": true,
        "revisions": [
            { "index": 0, "end": 250, "xrefOffset": 180, "signatureIndex": 0 },
            { "index": 1, "end": 400, "xrefOffset": 330, "signatureIndex": null }
        ],
        "signatures": [base(12, true), base(14, false)],
        "protection": { "enforced": null, "judged": "annotate", "certification": null,
                        "fieldLocks": [], "policyVersion": 5 }
    })
}

fn analysis() -> Value {
    json!({ "mode": "authoritative", "policyVersion": 5,
            "basis": { "version": { "sha256": "x", "sizeBytes": 400 }, "editsVersion": 0, "source": "persisted" },
            "since": { "revisionIndex": 0, "signatureIndex": 0 }, "until": { "revisionIndex": 1 },
            "restrictions": [],
            "current": { "verdict": "permitted", "complete": true, "findings": [], "method": "net-state",
                         "primary": { "rule": "annotation", "verdict": "permitted", "objectNumber": 9 } },
            "later": { "revisionCount": 1, "undoneObjectNumbers": [] },
            "verdict": "permitted", "steps": [] })
}

fn spec() -> TestSpec {
    spec_listing(snapshot())
}

/// The form's one signature field, 14 (widget 15), as `forms.list` answers
/// it.
fn form_field_14() -> Value {
    json!({
        "ref": field(14), "name": "Sig14", "origin": "acroform", "readOnly": false,
        "required": false, "noExport": false, "alternateName": null, "mappingName": null,
        "valueEntry": { "kind": "none" }, "defaultValueEntry": { "kind": "none" },
        "groupId": null, "createdBy": null, "createdAt": null, "filledBy": null,
        "filledByName": null, "filledAt": null, "importedBy": null,
        "widgets": [{ "ref": { "kind": "objectNumber", "page": page(), "objectNumber": 15 },
                      "objectNumber": 15, "page": page() }],
        "family": "signature"
    })
}

/// `spec()`, with `signatures.list` answering `listed`.
fn spec_listing(listed: Value) -> TestSpec {
    spec_with(listed, json!("*"))
}

/// `spec_listing`, the session granted `allows` (`"*"` for everything).
fn spec_with(listed: Value, allows: Value) -> TestSpec {
    let bx = json!({ "left": 0, "bottom": 0, "right": 600, "top": 800 });
    TestSpec::new(json!({
        "documents": { "a": {
            "id": "a",
            "pages": [{ "index": 0, "ref": page(), "label": null,
                        "size": { "width": 600, "height": 800 }, "rotation": 0, "userUnit": 1,
                        "boxes": { "media": bx, "crop": bx } }],
            "security": { "allows": allows },
            "services": ["signatures"]
        } }
    }))
    .respond("signatures.list", move |_| Ok(Reply::value(listed.clone())))
    .respond("signatures.contents", |_| Ok(Reply::value(json!({ "$bytes": "MAA=" }))))
    .respond("signatures.digest", |_| Ok(Reply::value(json!({ "$bytes": "AA==" }))))
    .respond("signatures.analyze", |_| Ok(Reply::value(analysis())))
    // Every validation reads the document validation data first (upstream 67ed292a6).
    .respond("signatures.getValidationInfo", |_| {
        Ok(Reply::value(json!({ "certificates": [], "ocspResponses": [], "crls": [] })))
    })
    .respond("signatures.downloadRevision", |_| Ok(Reply::value(json!({ "$bytes": "JVBERi0=" }))))
    .respond("forms.list", |_| {
        Ok(Reply::value(json!({ "formKind": "acroform", "needsAppearances": false,
                                "calculationOrder": [], "fields": [form_field_14()] })))
    })
}

/// A kernel with interaction, form and signature, built on the kernel
/// thread (the plugin's config and the spec hold `Rc`s).
fn handle_with(
    config: impl FnOnce() -> SignatureConfig + Send + 'static,
    spec: impl FnOnce() -> TestSpec + Send + 'static,
) -> (KernelThread<TestKernel>, Arc<SignatureHandle>) {
    let thread = KernelThread::spawn(move |_| {
        TestKernel::open(
            vec![
                interaction_plugin(InteractionConfig::default()),
                form_plugin(None),
                signature_plugin(config()),
            ],
            spec(),
        )
    })
    .unwrap();
    let post = thread.post();
    let handle = block_on(
        thread.call(move |k| SignatureHandle::new(k.capability(&SIGNATURE).unwrap(), post)),
    )
    .unwrap();
    (thread, handle)
}

fn handle() -> (KernelThread<TestKernel>, Arc<SignatureHandle>) {
    handle_with(SignatureConfig::default, spec)
}

fn code(error: &ViewerError) -> Option<&str> {
    match error {
        ViewerError::Kernel { code, .. } => code.as_deref(),
        ViewerError::Closed => None,
    }
}

#[test]
fn refresh_decodes_the_snapshot() {
    let (_thread, s) = handle();
    let snapshot = block_on(s.refresh(CancelToken::new())).unwrap().unwrap();
    assert!(snapshot.chain_valid);
    assert_eq!(snapshot.revisions.len(), 2);
    assert_eq!(snapshot.protection.judged, Some(SignatureModificationLevel::Annotate));
    assert_eq!(block_on(s.status()).unwrap(), ResourceStatus::Ready);
    let signed = block_on(s.signatures()).unwrap();
    assert_eq!(signed.len(), 1);
    assert_eq!(signed[0].coverage, Some(SignatureCoverage::WholeRevision));
    assert_eq!(signed[0].signer.name.as_deref(), Some("Ana"));
    assert_eq!(signed[0].byte_range, Some(vec![0, 100, 200, 50]));
    let widget = signed[0].widget.clone().unwrap();
    assert_eq!(widget.annot_object_number, 13);
    assert!(widget.annotation.is_some());
    assert_eq!(block_on(s.unsigned_fields()).unwrap().len(), 1);
    let one =
        block_on(s.signature(FormFieldRef::ObjectNumber { field_object_number: 14 })).unwrap();
    assert_eq!(one.map(|s| s.signed), Some(false));
    assert!(block_on(s.snapshot()).unwrap().is_some());
}

#[test]
fn a_snapshot_the_record_cannot_hold_is_invalid_state() {
    let bad = || spec_listing(json!({ "chainValid": "yes" }));
    let (_thread, s) = handle_with(SignatureConfig::default, bad);
    let error = block_on(s.refresh(CancelToken::new())).unwrap_err();
    assert_eq!(code(&error), Some("invalid-state"), "{error:?}");
}

#[test]
fn without_anchors_a_good_signature_is_at_best_valid_untrusted() {
    let (_thread, s) = handle();
    block_on(s.refresh(CancelToken::new())).unwrap();
    let verdicts =
        block_on(s.validate(SignatureValidateRequest::default(), CancelToken::new())).unwrap();
    assert_eq!(verdicts.len(), 1);
    // The fake CMS is not a real signature, so this one is indeterminate or
    // invalid; it is never `Valid` without anchors.
    assert_ne!(verdicts[0].summary, SignatureSummary::Valid);
    assert_eq!(verdicts[0].modifications.basis, SignatureModificationsBasis::Persisted);
    assert_eq!(block_on(s.verdicts()).unwrap().map(|v| v.len()), Some(1));
    let field = FormFieldRef::ObjectNumber { field_object_number: 12 };
    assert!(block_on(s.verdict(field)).unwrap().is_some());
}

#[test]
fn analyze_changes_decodes_the_analysis() {
    let (_thread, s) = handle();
    block_on(s.refresh(CancelToken::new())).unwrap();
    let analysis =
        block_on(s.analyze_changes(0, SignatureModificationsBasis::Persisted, CancelToken::new()))
            .unwrap();
    assert_eq!(analysis.later_revisions, 1);
    assert_eq!(analysis.primary.unwrap().object_number, 9);
}

#[test]
fn mode_defaults_to_visual_without_a_key() {
    // Filling granted, signing not: `canSign` and `canFill` read the
    // session's grants for the form's field 14 (`services/authority.ts`).
    // A mark in a signature field is its signer's to place, so `canFill`
    // asks what signing asks (upstream ceeb36bae).
    let grants =
        || spec_with(snapshot(), json!(["doc.forms.read", "doc.forms.fill", "doc.download"]));
    let (_thread, s) = handle_with(SignatureConfig::default, grants);
    assert_eq!(block_on(s.mode()).unwrap(), SignatureMode::Visual);
    let field = FormFieldRef::ObjectNumber { field_object_number: 14 };
    assert!(!block_on(s.can_sign(field.clone())).unwrap());
    assert!(!block_on(s.can_fill(field)).unwrap());
    assert!(!block_on(s.can_certify()).unwrap());
    assert!(!block_on(s.is_busy()).unwrap());
    assert_eq!(block_on(s.pending()).unwrap(), None);
}

#[test]
fn set_target_is_observed() {
    let (_thread, s) = handle();
    let events = Arc::new(Events::default());
    let _sub = s.subscribe(events.clone());
    block_on(s.refresh(CancelToken::new())).unwrap();
    let unsigned = block_on(s.unsigned_fields()).unwrap();
    block_on(s.set_target(Some(unsigned[0].field.clone()))).unwrap();
    assert_eq!(block_on(s.target()).unwrap(), Some(unsigned[0].field.clone()));
    block_on(s.request_inspection(unsigned[0].field.clone())).unwrap();
    let seen = events.0.lock().unwrap();
    assert!(
        seen.iter().any(|e| matches!(e, SignatureEvent::TargetChanged { field: Some(_) })),
        "{seen:?}"
    );
    assert!(
        seen.iter().any(|e| matches!(e, SignatureEvent::InspectionRequested { .. })),
        "{seen:?}"
    );
}

#[test]
fn read_revision_answers_bytes() {
    let (_thread, s) = handle();
    block_on(s.refresh(CancelToken::new())).unwrap();
    let field = block_on(s.signatures()).unwrap()[0].field.clone();
    let bytes = block_on(s.read_revision(field, CancelToken::new())).unwrap();
    assert!(bytes.starts_with(b"%PDF-"));
}

#[test]
fn a_closed_documents_capability_refuses_with_instance_closed() {
    let (thread, s) = handle();
    block_on(thread.call(|k| k.close())).unwrap().unwrap();
    for error in [
        block_on(s.snapshot()).map(|_| ()).unwrap_err(),
        block_on(s.status()).map(|_| ()).unwrap_err(),
        block_on(s.refresh(CancelToken::new())).map(|_| ()).unwrap_err(),
    ] {
        assert_eq!(code(&error), Some("instance-closed"), "{error:?}");
    }
}

#[test]
fn closing_the_document_ends_its_subscriptions() {
    let (thread, s) = handle();
    let events = Arc::new(Events::default());
    let _sub = s.subscribe(events.clone());
    block_on(thread.call(|k| k.close())).unwrap().unwrap();
    assert_eq!(events.1.load(Ordering::SeqCst), 1);
}

// ── signing (Task 8) ─────────────────────────────────────────────────────────

use epdf_plugin_signature::ffi::{
    SignatureDigest, SignatureKeyAlgorithm, SignatureMark, SignatureSignCompletion,
    SignatureSignInput, SignatureSigner, SignatureSignerInput,
};
use rsa::pkcs8::DecodePrivateKey;
use rsa::{Pkcs1v15Sign, PrivateKey};
use sha2::Digest as _;

const KEYS: &str = "packages/engine/main/test/fixtures/signature-compat/keys";

fn repo_file(path: &str) -> Vec<u8> {
    let root = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../../..");
    std::fs::read(root.join(path)).unwrap()
}

fn pem_body(pem: &[u8]) -> Vec<u8> {
    use base64::Engine as _;
    let body: String =
        String::from_utf8_lossy(pem).lines().filter(|l| !l.starts_with("-----")).collect();
    base64::engine::general_purpose::STANDARD.decode(body).unwrap()
}

struct TestSigner {
    key: PrivateKey,
    chain: Vec<Vec<u8>>,
    fail: bool,
}

impl TestSigner {
    fn load(fail: bool) -> Self {
        let key = PrivateKey::from_pkcs8_der(&pem_body(&repo_file(&format!(
            "{KEYS}/TEST-ONLY-private-key.pem"
        ))))
        .unwrap();
        let cert = pem_body(&repo_file(&format!("{KEYS}/TEST-ONLY-signer.pem")));
        TestSigner { key, chain: vec![cert], fail }
    }
}

impl SignatureSigner for TestSigner {
    fn certificate_chain(&self) -> Result<Vec<Vec<u8>>, ViewerError> {
        Ok(self.chain.clone())
    }
    fn algorithm(&self) -> SignatureKeyAlgorithm {
        SignatureKeyAlgorithm::RsaPkcs1
    }
    fn digest(&self) -> SignatureDigest {
        SignatureDigest::Sha256
    }
    fn sign(&self, message: Vec<u8>, completion: Arc<SignatureSignCompletion>) {
        if self.fail {
            return completion.fail_with(epdf_ffi::HostFailure {
                name: "UserCancelled".into(),
                code: Some("operation-cancelled".into()),
                detail: "the user cancelled".into(),
            });
        }
        completion.finish(
            self.key
                .sign(Pkcs1v15Sign::new::<sha2::Sha256>(), &sha2::Sha256::digest(&message))
                .unwrap(),
        )
    }
}

/// The unsigned field (14), signed.
fn signed_14() -> Value {
    let mut s = snapshot()["signatures"][1].clone();
    s["signed"] = json!(true);
    s["coverage"] = json!("whole-revision");
    s["revisionIndex"] = json!(1);
    s["byteRange"] = json!([0, 300, 400, 100]);
    s
}

thread_local! {
    /// The input `signatures.prepare` was last sent, on the kernel thread.
    static PREPARE_INPUT: std::cell::RefCell<Value> = const { std::cell::RefCell::new(Value::Null) };
}

/// `spec()` plus prepare and complete responders that record the CMS.
fn signing_spec(cms: Arc<Mutex<Option<Value>>>) -> TestSpec {
    spec()
        .respond("signatures.prepare", |args| {
            PREPARE_INPUT.with(|p| *p.borrow_mut() = args.first().cloned().unwrap_or(Value::Null));
            let prepared = json!({
                "signingId": "s1", "digest": { "$bytes": "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=" },
                "algorithm": "sha256", "byteRange": [0, 100, 200, 50], "contentsSizeBytes": 8192,
                "subFilter": "ETSI.CAdES.detached",
                "expectedVersion": { "baseSha256": "x", "editsVersion": 0 }, "expiresAt": null
            });
            // The engine publishes the parked signing before it answers;
            // the plugin's mirror folds it (`model.ts:143-144`).
            let mut event = prepared.clone();
            event["type"] = json!("signatures.prepared");
            event["field"] = field(14);
            event["origin"] = origin();
            Ok(Reply { value: prepared, events: vec![event] })
        })
        .respond("signatures.complete", move |args| {
            *cms.lock().unwrap() = args.first().and_then(|a| a.get("cms")).cloned();
            let completed = json!({
                "status": "completed", "signature": signed_14(),
                "version": { "sha256": "y", "sizeBytes": 500 },
                "previous": { "baseSha256": "x", "editsVersion": 0 },
                "protection": snapshot()["protection"].clone(),
                "meta": { "affectedPages": [], "cacheDelta": null, "opId": "s1", "undoable": false }
            });
            // The completion clears the parked signing and fires `onSigned`
            // through the mirror (`model.ts:147-148`, `sync/signatures.ts:69-75`).
            let mut event = completed.clone();
            event["type"] = json!("signatures.completed");
            event["signingId"] = json!("s1");
            event["origin"] = origin();
            Ok(Reply { value: completed, events: vec![event] })
        })
        // A cancel that discards the parked candidate publishes its notice,
        // as the engine's host does; the mirror clears `pending` on it
        // (`model.ts:145-146`).
        .respond("signatures.cancel", |args| {
            let signing_id = args.first().cloned().unwrap_or(Value::Null);
            let event = json!({
                "type": "signatures.cancelled", "signingId": signing_id, "origin": origin()
            });
            Ok(Reply { value: json!({ "status": "cancelled" }), events: vec![event] })
        })
}

/// A local session's event origin.
fn origin() -> Value {
    json!({ "kind": "local", "sessionId": "s1",
        "subject": null, "at": "1970-01-01T00:00:00.000Z", "serverSequence": null })
}

fn unsigned_input(s: &SignatureHandle) -> SignatureSignInput {
    block_on(s.refresh(CancelToken::new())).unwrap();
    let field = block_on(s.unsigned_fields()).unwrap()[0].field.clone();
    SignatureSignInput {
        field,
        mark: None,
        signer: None,
        certify: None,
        lock: None,
        timestamp_authority: None,
    }
}

#[test]
fn signing_hands_the_engine_a_cms_from_the_foreign_signer() {
    let cms = Arc::new(Mutex::new(None));
    let seen = cms.clone();
    let (_thread, s) = handle_with(SignatureConfig::default, move || signing_spec(seen));
    let events = Arc::new(Events::default());
    let _sub = s.subscribe(events.clone());
    let input = unsigned_input(&s);
    let done = block_on(s.sign(input, Some(Arc::new(TestSigner::load(false))), CancelToken::new()))
        .unwrap();
    assert!(done.signature.signed);
    let cms = cms.lock().unwrap().clone().unwrap();
    let der = epdf_kernel::bytes::Bytes::from_json(&cms).unwrap().0;
    assert_eq!(der[0], 0x30);
    assert_eq!(block_on(s.pending()).unwrap(), None);
    assert!(events.0.lock().unwrap().iter().any(|e| matches!(e, SignatureEvent::Signed { .. })));
}

#[test]
fn a_throwing_signer_leaves_nothing_pending() {
    let (_thread, s) = handle_with(SignatureConfig::default, || signing_spec(Arc::default()));
    let input = unsigned_input(&s);
    let error = block_on(s.sign(input, Some(Arc::new(TestSigner::load(true))), CancelToken::new()))
        .unwrap_err();
    assert_eq!(code(&error), Some("operation-cancelled"), "{error:?}");
    assert_eq!(block_on(s.pending()).unwrap(), None);
    assert!(!block_on(s.is_busy()).unwrap());
}

#[test]
fn signing_without_a_signer_is_invalid_input() {
    let (_thread, s) = handle();
    let input = unsigned_input(&s);
    let error = block_on(s.sign(input, None, CancelToken::new())).unwrap_err();
    assert_eq!(code(&error), Some("invalid-input"), "{error:?}");
}

#[test]
fn a_two_phase_signing_parks_and_a_refused_cms_leaves_it_parked() {
    let (thread, s) = handle_with(SignatureConfig::default, || signing_spec(Arc::default()));
    let input = unsigned_input(&s);
    let prepared = block_on(s.prepare_signature(
        epdf_plugin_signature::ffi::SignaturePrepareInput {
            field: input.field.clone(),
            mark: None,
            signer: Some(SignatureSignerInput {
                name: Some("Ada".into()),
                reason: Some("Approved".into()),
                ..Default::default()
            }),
            certify: None,
            lock: None,
            sub_filter: None,
            digest: Some(SignatureDigest::Sha256),
            timestamp_authority: None,
        },
        CancelToken::new(),
    ))
    .unwrap();
    assert_eq!(prepared.digest.len(), 32);
    // The signer's facts go to the engine as the input's `signer`.
    let sent = block_on(thread.call(|_| PREPARE_INPUT.with(|p| p.borrow().clone()))).unwrap();
    assert_eq!(sent["signer"], json!({ "name": "Ada", "reason": "Approved" }), "{sent}");
    assert_eq!(block_on(s.pending()).unwrap().map(|p| p.signing_id), Some("s1".to_string()));
    // The CMS is checked against what was prepared before anything is
    // written (upstream b08c0075d): one that is not SignedData is refused,
    // and the signing stays prepared.
    let error =
        block_on(s.complete_signature("s1".into(), vec![0x30, 0], CancelToken::new())).unwrap_err();
    assert_eq!(code(&error), Some("operation-failed"), "{error:?}");
    assert_eq!(block_on(s.pending()).unwrap().map(|p| p.signing_id), Some("s1".to_string()));
}

#[test]
fn a_visual_fill_reaches_the_engine() {
    let filled = Arc::new(Mutex::new(false));
    let saw = filled.clone();
    let (_thread, s) = handle_with(SignatureConfig::default, move || {
        spec().respond("forms.setSignatureAppearance", move |_| {
            *saw.lock().unwrap() = true;
            Ok(Reply::value(json!({})))
        })
    });
    let input = unsigned_input(&s);
    let artwork = repo_file("packages/engine/main/test/fixtures/signature_artwork.pdf");
    block_on(s.fill_field(
        input.field,
        SignatureMark::Source { bytes: artwork },
        CancelToken::new(),
    ))
    .unwrap();
    assert!(*filled.lock().unwrap());
}

#[test]
fn placing_a_mark_in_visual_mode_fills_the_field() {
    let (_thread, s) = handle_with(SignatureConfig::default, || {
        spec().respond("forms.setSignatureAppearance", |_| Ok(Reply::value(json!({}))))
    });
    let input = unsigned_input(&s);
    let artwork = repo_file("packages/engine/main/test/fixtures/signature_artwork.pdf");
    let result = block_on(s.place_mark(
        SignatureMark::Source { bytes: artwork },
        epdf_plugin_signature::ffi::SignaturePlaceTarget::Field { field: input.field.clone() },
        CancelToken::new(),
    ))
    .unwrap();
    assert_eq!(
        result,
        epdf_plugin_signature::ffi::SignaturePlaceResult::Filled { field: input.field }
    );
}

// ── D-1: the foreign transport ──────────────────────────────────────────────

use epdf_plugin_signature::ffi::{
    SignatureCallAuthority, SignatureRevocationRoute, SignatureSettingsPatch,
    SignatureTimestampAuthority, SignatureTransport, SignatureTransportCompletion,
    SignatureTransportHeader, SignatureTransportRequest, SignatureTransportSlot, SlotTransport,
};

/// What a recorded transport answers, in order.
enum Canned {
    Replied(u16, Vec<u8>),
    TimedOut,
    TooLarge,
    Failed(&'static str),
    /// The completion is dropped unanswered.
    Dropped,
}

/// A host transport that answers recorded replies from another thread and
/// keeps every request.
#[derive(Default)]
struct Recorded {
    replies: Mutex<std::collections::VecDeque<Canned>>,
    sent: Mutex<Vec<SignatureTransportRequest>>,
}

impl Recorded {
    fn new(replies: Vec<Canned>) -> Arc<Self> {
        Arc::new(Recorded { replies: Mutex::new(replies.into()), sent: Mutex::default() })
    }
    fn sent(&self) -> Vec<SignatureTransportRequest> {
        self.sent.lock().unwrap().clone()
    }
}

impl SignatureTransport for Recorded {
    fn post(
        &self,
        request: SignatureTransportRequest,
        completion: Arc<SignatureTransportCompletion>,
    ) {
        self.sent.lock().unwrap().push(request);
        let reply = self.replies.lock().unwrap().pop_front();
        std::thread::spawn(move || match reply {
            Some(Canned::Replied(status, body)) => completion.replied(status, body),
            Some(Canned::TimedOut) => completion.timed_out(),
            Some(Canned::TooLarge) => completion.too_large(),
            Some(Canned::Failed(message)) => completion.failed(message.into()),
            Some(Canned::Dropped) | None => drop(completion),
        });
    }
}

/// Fetch `request` through a revocation route over `slot`, on the kernel
/// thread, and wait for the answer here.
fn fetch_through(
    slot: SignatureTransportSlot,
    route: SignatureRevocationRoute,
) -> Result<Vec<u8>, ViewerError> {
    let thread = KernelThread::spawn(|_| Ok(())).unwrap();
    let post = thread.post();
    let (pending, reply) = epdf_ffi::PromiseSlot::pending(post.clone(), |bytes: Vec<u8>| bytes);
    block_on(thread.call(move |_| {
        let transport = std::rc::Rc::new(SlotTransport { slot, post });
        let source = route.source(transport);
        pending.start(Ok(source.fetch(epdf_signature_core::RevocationRequest::Crl {
            url: "http://crl.example/a.crl".into(),
        })));
    }))
    .unwrap();
    block_on(reply)
}

fn route() -> SignatureRevocationRoute {
    SignatureRevocationRoute {
        url: "https://app.example/revocation".into(),
        headers: vec![SignatureTransportHeader { name: "x-session".into(), value: "t".into() }],
        timeout_ms: None,
        retries: Some(0),
        max_reply_bytes: None,
    }
}

fn detail(error: &ViewerError) -> &str {
    match error {
        ViewerError::Kernel { detail, .. } => detail,
        ViewerError::Closed => "closed",
    }
}

#[test]
fn a_revocation_route_round_trips_a_recorded_reply_through_the_foreign_transport() {
    let host = Recorded::new(vec![Canned::Replied(200, vec![0x30, 0x03, 1, 2, 3])]);
    let slot: SignatureTransportSlot = Arc::new(Mutex::new(Some(host.clone())));
    assert_eq!(fetch_through(slot, route()).unwrap(), vec![0x30, 0x03, 1, 2, 3]);
    let sent = host.sent();
    assert_eq!(sent.len(), 1);
    assert_eq!(sent[0].url, "https://app.example/revocation");
    assert_eq!(sent[0].content_type, "application/json");
    assert_eq!(sent[0].headers, route().headers);
    assert_eq!(sent[0].timeout_ms, 10_000);
    assert_eq!(sent[0].max_reply_bytes, 2 * 1024 * 1024);
    let body: Value = serde_json::from_slice(&sent[0].body).unwrap();
    assert_eq!(body, json!({ "kind": "crl", "url": "http://crl.example/a.crl" }));
}

#[test]
fn each_outcome_the_foreign_transport_gives_reads_as_the_client_reads_it() {
    let cases = [
        (Canned::TimedOut, "no reply within 10000 ms"),
        (Canned::TooLarge, "the reply broke off: it is over 2097152 bytes"),
        // A body over the limit is no answer, however much the host read.
        (
            Canned::Replied(200, vec![0; 2 * 1024 * 1024 + 1]),
            "the reply broke off: it is over 2097152 bytes",
        ),
        (Canned::Failed("the name did not resolve"), "the name did not resolve"),
        (Canned::Dropped, "a SignatureTransport completion was dropped without an answer"),
    ];
    for (canned, why) in cases {
        let slot: SignatureTransportSlot = Arc::new(Mutex::new(Some(Recorded::new(vec![canned]))));
        let error = fetch_through(slot, route()).unwrap_err();
        assert_eq!(
            detail(&error),
            format!(
                "http://crl.example/a.crl did not answer through https://app.example/revocation: {why}"
            ),
            "{error:?}"
        );
    }
    let error = fetch_through(SignatureTransportSlot::default(), route()).unwrap_err();
    assert!(detail(&error).ends_with("no transport: set one on the viewer first"), "{error:?}");
}

/// A kernel with interaction, form and the host's signature plugin over
/// `transport`, and its handle.
fn host_handle(
    config: epdf_plugin_signature::ffi::SignatureConfig,
    transport: Option<Arc<dyn SignatureTransport>>,
    spec: impl FnOnce() -> TestSpec + Send + 'static,
) -> (KernelThread<TestKernel>, Arc<SignatureHandle>) {
    host_handle_trusting(config, transport, None, spec)
}

/// A trust a host set that names no anchors: a `trust` setting all the same.
struct NoAnchors;

impl epdf_plugin_signature::ffi::SignatureTrust for NoAnchors {
    fn anchors(&self) -> Result<Vec<Vec<u8>>, ViewerError> {
        Ok(vec![])
    }
}

fn host_handle_trusting(
    config: epdf_plugin_signature::ffi::SignatureConfig,
    transport: Option<Arc<dyn SignatureTransport>>,
    trust: Option<Arc<dyn epdf_plugin_signature::ffi::SignatureTrust>>,
    spec: impl FnOnce() -> TestSpec + Send + 'static,
) -> (KernelThread<TestKernel>, Arc<SignatureHandle>) {
    let trust = Arc::new(Mutex::new(trust));
    let slot: SignatureTransportSlot = Arc::new(Mutex::new(transport));
    let plugin_slot = slot.clone();
    let thread = KernelThread::spawn(move |post| {
        TestKernel::open(
            vec![
                interaction_plugin(InteractionConfig::default()),
                form_plugin(None),
                epdf_plugin_signature::ffi::host_plugin(
                    config,
                    Arc::default(),
                    trust,
                    plugin_slot,
                    post,
                ),
            ],
            spec(),
        )
    })
    .unwrap();
    let post = thread.post();
    let handle = block_on(thread.call(move |k| {
        SignatureHandle::with_transport(k.capability(&SIGNATURE).unwrap(), post, slot)
    }))
    .unwrap();
    (thread, handle)
}

fn authority() -> SignatureTimestampAuthority {
    SignatureTimestampAuthority {
        url: "https://tsa.example/stamp".into(),
        hash: None,
        policy: None,
        headers: vec![SignatureTransportHeader { name: "x-api-key".into(), value: "k".into() }],
        timeout_ms: Some(5_000),
        retries: Some(1),
    }
}

#[test]
fn a_calls_timestamp_authority_asks_through_the_foreign_transport() {
    let host = Recorded::new(vec![Canned::Replied(503, b"busy".to_vec()), Canned::TimedOut]);
    let (_thread, s) =
        host_handle(Default::default(), Some(host.clone()), || signing_spec(Arc::default()));
    let input = SignatureSignInput {
        timestamp_authority: Some(SignatureCallAuthority::Http { authority: authority() }),
        ..unsigned_input(&s)
    };
    let error =
        block_on(s.sign(input, Some(Arc::new(TestSigner::load(false))), CancelToken::new()))
            .unwrap_err();
    assert!(
        detail(&error).contains("the timestamp authority at https://tsa.example/stamp did not answer: no reply within 5000 ms"),
        "{error:?}"
    );
    let sent = host.sent();
    // One retry after the 503.
    assert_eq!(sent.len(), 2);
    for request in &sent {
        assert_eq!(request.url, "https://tsa.example/stamp");
        assert_eq!(request.content_type, "application/timestamp-query");
        assert_eq!(request.headers, authority().headers);
        assert_eq!(request.timeout_ms, 5_000);
        assert_eq!(request.max_reply_bytes, 1024 * 1024);
        // A TimeStampReq with upstream's nonce: INTEGER, 8 octets, the first
        // `01xxxxxx`.
        assert_eq!(request.body[0], 0x30);
        assert!(
            request.body.windows(3).any(|w| w[0] == 0x02 && w[1] == 0x08 && w[2] & 0xc0 == 0x40),
            "{:02x?}",
            request.body
        );
    }
    assert_eq!(sent[0].body, sent[1].body);
    assert_eq!(block_on(s.pending()).unwrap(), None);
}

#[test]
fn a_call_without_an_authority_does_not_ask_the_settings_one() {
    let host = Recorded::new(vec![]);
    let config = epdf_plugin_signature::ffi::SignatureConfig {
        timestamp_authority: Some(authority()),
        ..Default::default()
    };
    let (_thread, s) = host_handle(config, Some(host.clone()), || signing_spec(Arc::default()));
    let input = SignatureSignInput {
        timestamp_authority: Some(SignatureCallAuthority::Without),
        ..unsigned_input(&s)
    };
    let done = block_on(s.sign(input, Some(Arc::new(TestSigner::load(false))), CancelToken::new()))
        .unwrap();
    assert!(done.signature.signed);
    assert!(host.sent().is_empty());
    // No `revocation` setting: nothing was asked for after the seal.
    assert_eq!(done.validation_info, None);
    assert_eq!(done.validation_info_error, None);
}

#[test]
fn the_new_settings_cross_and_gate_add_validation_info() {
    let (_thread, s) = host_handle(Default::default(), None, spec);
    let settings = block_on(s.settings()).unwrap();
    assert!(!settings.has_timestamp_authority && !settings.has_revocation);
    assert!(!settings.document_timestamp);
    assert!(!block_on(s.can_add_validation_info()).unwrap());
    let error = block_on(s.add_validation_info(None, CancelToken::new())).unwrap_err();
    assert_eq!(code(&error), Some("invalid-input"), "{error:?}");
    block_on(s.update_settings(SignatureSettingsPatch {
        timestamp_authority: Some(authority()),
        revocation: Some(route()),
        document_timestamp: Some(true),
        ..Default::default()
    }))
    .unwrap();
    let settings = block_on(s.settings()).unwrap();
    assert!(settings.has_timestamp_authority && settings.has_revocation);
    assert!(settings.document_timestamp);
    // The host plugin's trust is its slot, and the slot is empty: no
    // `trust` setting, as upstream's `trust: null`.
    assert!(!block_on(s.can_add_validation_info()).unwrap());
    let error = block_on(s.add_validation_info(None, CancelToken::new())).unwrap_err();
    assert_eq!(code(&error), Some("invalid-input"), "{error:?}");
    assert_eq!(
        detail(&error),
        "adding validation data needs the `revocation` and `trust` settings"
    );
    block_on(
        s.update_settings(SignatureSettingsPatch { clear_revocation: true, ..Default::default() }),
    )
    .unwrap();
    assert!(!block_on(s.settings()).unwrap().has_revocation);
    assert!(!block_on(s.can_add_validation_info()).unwrap());
}

#[test]
fn add_validation_info_reaches_the_plugin_and_refuses_as_it_does() {
    let config = epdf_plugin_signature::ffi::SignatureConfig {
        revocation: Some(route()),
        ..Default::default()
    };
    let host = Recorded::new(vec![]);
    let (_thread, s) =
        host_handle_trusting(config, Some(host.clone()), Some(Arc::new(NoAnchors)), || {
            // The engine refuses nothing for the probe the client sends first.
            spec().respond("signatures.addValidationInfo", |_| {
                Ok(Reply::value(
                    json!({ "added": { "certificates": 0, "ocspResponses": 0, "crls": 0 } }),
                ))
            })
        });
    block_on(s.refresh(CancelToken::new())).unwrap();
    // All or nothing: the fake signature has no trusted timestamp, so no
    // time is proven to ask about, and nothing is fetched or stored.
    let error = block_on(s.add_validation_info(
        Some(FormFieldRef::ObjectNumber { field_object_number: 12 }),
        CancelToken::new(),
    ))
    .unwrap_err();
    assert_eq!(code(&error), Some("operation-failed"), "{error:?}");
    assert!(detail(&error).contains("has no timestamp you trust"), "{error:?}");
    assert!(host.sent().is_empty());
}

#[test]
fn a_verdict_carries_revocation_timestamp_and_long_term() {
    let (_thread, s) = handle();
    block_on(s.refresh(CancelToken::new())).unwrap();
    let request = SignatureValidateRequest {
        validation_time: Some(
            epdf_plugin_signature::ffi::SignatureRequestedTime::EarliestTimestamp,
        ),
        until: None,
    };
    let verdicts = block_on(s.validate(request, CancelToken::new())).unwrap();
    let verdict = &verdicts[0];
    assert_eq!(verdict.revocation, epdf_plugin_signature::ffi::SignatureRevocationStatus::Unknown);
    assert_eq!(verdict.timestamp, None);
    assert!(!verdict.long_term.enabled);
    let now = SignatureValidateRequest {
        validation_time: Some(epdf_plugin_signature::ffi::SignatureRequestedTime::Now),
        until: None,
    };
    let verdicts = block_on(s.validate(now, CancelToken::new())).unwrap();
    assert_eq!(
        verdicts[0].validation_time,
        epdf_plugin_signature::ffi::SignatureValidationTime::Now
    );
}
