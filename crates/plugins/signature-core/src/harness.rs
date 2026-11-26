//! The conformance adapter. `tooling/plugin-conformance/src/suites/signature-core.ts`
//! is the other half: the function table for `core/signature-parse.json`,
//! the `signature-core` scenario plugin whose capability wraps each
//! asynchronous export, and the scripted WebCrypto both sides answer from.

use std::any::Any;
use std::rc::Rc;

use base64::Engine as _;
use base64::engine::general_purpose::{STANDARD, URL_SAFE_NO_PAD};
use epdf_kernel::bytes::Bytes;
use epdf_kernel::plugin::{PluginContext, PluginDef};
use epdf_kernel::rt::Promise;
use epdf_kernel::store::{SliceJson, SliceReducer, SliceState};
use epdf_kernel::types::PluginScope;
use epdf_kernel::{AnyPlugin, Error};
use plugin_conformance::{Answer, FunctionTable, Harness, HarnessPlugin, Outcome};
use rsa::pkcs8::DecodePrivateKey as _;
use rsa::{Pkcs1v15Sign, PrivateKey};
use serde_json::{Map, Value, json};

use crate::attach::attach_signature_timestamp;
use crate::cert::Certificate;
use crate::cms::{
    BuildInput, Profile, RawAlgorithm, RawSigner, build_detached_cms, verify_cms_signature,
};
use crate::collect::{AddValidationInfoOptions, add_validation_info};
use crate::der;
use crate::gate::{CompletionProfile, Prepared, verify_for_completion};
use crate::judgement::Requested;
use crate::oids::Digest;
use crate::parse::{bytes_json, parse_detached_cms};
use crate::revocation::answer::{AnswerKind, RevocationAnswer, check_revocation_answer};
use crate::revocation::http_source::{HttpRevocationSourceOptions, http_revocation_source};
use crate::revocation::source::{RevocationRequest, RevocationSource};
use crate::sign::{CmsSignRequest, CmsSigner, SignerPort, profile_for, sign};
use crate::sign::{TimestampOptions, complete_signature};
use crate::signers::{
    CryptoKey, CryptoKeyPair, MemoryKeyStore, PersonalKeyRecord, PersonalKeyStore, WebCrypto,
    certificate_common_name, create_test_signer, generate_signing_key_pair, personal_signer,
    remote_signer, self_signed_certificate, web_crypto_signer,
};
use crate::timestamp_http::{
    HttpTimestampAuthorityOptions, ecdsa_raw_from_der, http_timestamp_authority,
};
use crate::transport::{
    HttpTransport, RecordedTransport, TransportHeader, TransportOutcome, TransportRequest,
};
use crate::trust::{CmsInput, TrustPort, ValidationTime, validate_chain};
use crate::verdict::{ValidateOptions, validate_signatures, verify_timestamp_token};

type Out = Result<Value, Error>;

fn bytes(value: &Value) -> Vec<u8> {
    Bytes::from_json(value).map(|b| b.0).unwrap_or_default()
}

fn present(value: Option<&Value>) -> Option<&Value> {
    value.filter(|v| !v.is_null() && !epdf_kernel::is_undefined(v))
}

fn digest_named(name: &str) -> Result<Digest, Error> {
    Digest::from_name(name)
        .ok_or_else(|| Error::new(format!("[signature-core] unknown digest {name}")))
}

// ── function cases ───────────────────────────────────────────────────────────

pub fn functions() -> FunctionTable {
    vec![
        ("parseDetachedCms", |a: &[Value]| -> Out {
            parse_detached_cms(&bytes(&a[0])).map(|p| p.to_json()).map_err(|e| e.to_error())
        }),
        ("profileFor", |a: &[Value]| -> Out {
            Ok(json!(profile_for(a[0].as_str().unwrap_or_default())?.name()))
        }),
        ("certificateCommonName", |a: &[Value]| -> Out {
            Ok(certificate_common_name(&bytes(&a[0]))?.map_or(Value::Null, Value::String))
        }),
    ]
}

// ── the fixture keys ─────────────────────────────────────────────────────────

fn fixtures() -> Value {
    thread_local! {
        static FIXTURES: Value = {
            let path = plugin_conformance::vectors_root().join("../fixtures/signature/fixtures.json");
            serde_json::from_str(&std::fs::read_to_string(&path).expect("fixtures/signature/fixtures.json")).unwrap()
        };
    }
    FIXTURES.with(Value::clone)
}

fn fixture_key(name: &str, part: &str) -> Vec<u8> {
    STANDARD.decode(fixtures()["keys"][name][part].as_str().unwrap_or_default()).unwrap_or_default()
}

/// The host's handle to a fixture key.
struct FixtureKey {
    name: String,
}

fn rsa_algorithm(name: &str, hash: &str, spki: &[u8]) -> Value {
    let bits = rsa_parts(spki).map_or(2048, |(n, _)| n.len() * 8);
    json!({ "name": name, "modulusLength": bits, "publicExponent": bytes_json(&[1, 0, 1]), "hash": { "name": hash } })
}

/// `(n, e)` of an RSA SubjectPublicKeyInfo, unsigned.
fn rsa_parts(spki: &[u8]) -> Option<(Vec<u8>, Vec<u8>)> {
    let node = der::from_ber(spki)?;
    let key = der::from_ber(&node.children.get(1)?.bits())?;
    let strip = |b: &[u8]| {
        let start = b.iter().position(|&x| x != 0).unwrap_or(b.len());
        b[start..].to_vec()
    };
    Some((strip(&key.children.first()?.content), strip(&key.children.get(1)?.content)))
}

/// The fixture pair for a WebCrypto algorithm: `p256`/`p384`/`p521` for
/// ECDSA, `rsa` (the `leaf` key unless named) for either RSA algorithm.
fn fixture_pair(params: &Value, extractable: bool, rsa: &str) -> CryptoKeyPair {
    let name = params.get("name").and_then(Value::as_str).unwrap_or_default();
    let key = |key_type: &str, extractable: bool, algorithm: Value, usage: &str, fixture: &str| {
        CryptoKey {
            key_type: key_type.into(),
            extractable,
            algorithm,
            usages: vec![usage.into()],
            handle: Rc::new(FixtureKey { name: fixture.into() }),
        }
    };
    if name == "ECDSA" {
        let curve = params.get("namedCurve").and_then(Value::as_str).unwrap_or("P-256");
        let fixture = match curve {
            "P-384" => "p384",
            "P-521" => "p521",
            _ => "p256",
        };
        let algorithm = json!({ "name": "ECDSA", "namedCurve": curve });
        return CryptoKeyPair {
            private_key: key("private", extractable, algorithm.clone(), "sign", fixture),
            public_key: key("public", true, algorithm, "verify", fixture),
        };
    }
    let hash = match params.get("hash") {
        Some(Value::String(h)) => h.clone(),
        Some(h) => h.get("name").and_then(Value::as_str).unwrap_or("SHA-256").to_string(),
        None => "SHA-256".into(),
    };
    let algorithm = rsa_algorithm(name, &hash, &fixture_key(rsa, "spki"));
    CryptoKeyPair {
        private_key: key("private", extractable, algorithm.clone(), "sign", rsa),
        public_key: key("public", true, algorithm, "verify", rsa),
    }
}

/// The scripted WebCrypto: `tooling/plugin-conformance/src/suites/signature-core.ts`
/// answers the same way, and logs every call as a callback.
struct ScriptedWebCrypto {
    harness: Harness,
}

impl WebCrypto for ScriptedWebCrypto {
    fn generate_key(
        &self,
        params: Value,
        extractable: bool,
        usages: Vec<String>,
    ) -> Promise<CryptoKeyPair> {
        self.harness
            .log("webcrypto.generateKey", vec![params.clone(), json!(extractable), json!(usages)]);
        Promise::resolved(fixture_pair(&params, extractable, "leaf"))
    }

    fn sign(&self, params: Value, key: &CryptoKey, data: Vec<u8>) -> Promise<Vec<u8>> {
        self.harness.log("webcrypto.sign", vec![params.clone(), key.describe(), bytes_json(&data)]);
        let name = params.get("name").and_then(Value::as_str).unwrap_or_default();
        let fixture =
            key.handle.downcast_ref::<FixtureKey>().map_or("leaf", |k| k.name.as_str()).to_string();
        if name == "RSASSA-PKCS1-v1_5" {
            let hash = key
                .algorithm
                .get("hash")
                .and_then(|h| h.get("name"))
                .and_then(Value::as_str)
                .unwrap_or("SHA-256");
            let digest = Digest::from_webcrypto(hash).unwrap_or(Digest::Sha256);
            let private = PrivateKey::from_pkcs8_der(&fixture_key(&fixture, "pkcs8"))
                .expect("an RSA fixture key");
            let scheme = match digest {
                Digest::Sha1 => Pkcs1v15Sign::new::<sha1::Sha1>(),
                Digest::Sha256 => Pkcs1v15Sign::new::<sha2::Sha256>(),
                Digest::Sha384 => Pkcs1v15Sign::new::<sha2::Sha384>(),
                Digest::Sha512 => Pkcs1v15Sign::new::<sha2::Sha512>(),
            };
            return match private.sign(scheme, &digest.hash(&data)) {
                Ok(signature) => Promise::resolved(signature),
                Err(e) => Promise::rejected(Error::new(e.to_string())),
            };
        }
        let fake: Vec<u8> = if key.algorithm.get("name").and_then(Value::as_str) == Some("ECDSA") {
            let size = match key.algorithm.get("namedCurve").and_then(Value::as_str) {
                Some("P-384") => 96,
                Some("P-521") => 132,
                _ => 64,
            };
            (0..size).map(|i| ((i * 3 + 1) & 0xff) as u8).collect()
        } else {
            (0..256).map(|i| ((i * 5 + 2) & 0xff) as u8).collect()
        };
        Promise::resolved(fake)
    }

    fn export_jwk(&self, key: &CryptoKey) -> Promise<Value> {
        self.harness.log("webcrypto.exportKey", vec![json!("jwk"), key.describe()]);
        let fixture =
            key.handle.downcast_ref::<FixtureKey>().map_or("leaf", |k| k.name.as_str()).to_string();
        let spki = fixture_key(&fixture, "spki");
        let b64 = |b: &[u8]| URL_SAFE_NO_PAD.encode(b);
        let jwk = if key.algorithm.get("name").and_then(Value::as_str) == Some("ECDSA") {
            let point = der::from_ber(&spki)
                .and_then(|n| n.children.get(1).map(|b| b.bits()))
                .unwrap_or_default();
            let size = (point.len() - 1) / 2;
            json!({
                "kty": "EC",
                "crv": key.algorithm["namedCurve"],
                "x": b64(&point[1..1 + size]),
                "y": b64(&point[1 + size..]),
            })
        } else {
            let (n, e) = rsa_parts(&spki).unwrap_or_default();
            json!({ "kty": "RSA", "n": b64(&n), "e": b64(&e) })
        };
        Promise::resolved(jwk)
    }

    fn get_random_values(&self, buffer: &mut [u8]) {
        self.harness.log("webcrypto.getRandomValues", vec![json!(buffer.len())]);
        buffer.fill(0x5a);
    }
}

// ── ports from `$fn` specs ───────────────────────────────────────────────────

fn answer(answer: Answer) -> Promise<Value> {
    match answer {
        Answer::Returns(value) => Promise::resolved(value),
        Answer::Throws(error) => Promise::rejected(error),
        Answer::Promise(promise) => promise,
    }
}

fn scripted(h: &Harness, spec: &Value, args: Vec<Value>) -> Promise<Value> {
    answer(h.callback(spec).call(args))
}

fn raw_signer(h: &Harness, value: &Value) -> Result<RawSigner, Error> {
    let spec = value.get("sign").cloned().unwrap_or(Value::Null);
    let callback = h.callback(&spec);
    Ok(RawSigner {
        certificate_chain: value
            .get("certificateChain")
            .and_then(Value::as_array)
            .into_iter()
            .flatten()
            .map(bytes)
            .collect(),
        algorithm: RawAlgorithm::from_name(
            value.get("algorithm").and_then(Value::as_str).unwrap_or_default(),
        )
        .ok_or_else(|| Error::new("[signature-core] unknown raw algorithm"))?,
        hash: digest_named(value.get("hash").and_then(Value::as_str).unwrap_or_default())?,
        sign: Rc::new(move |data| {
            answer(callback.call(vec![bytes_json(&data)])).map(|v| Ok(bytes(&v)))
        }),
    })
}

fn cms_signer(h: &Harness, value: &Value) -> CmsSigner {
    let callback = h.callback(&value.get("sign").cloned().unwrap_or(Value::Null));
    CmsSigner {
        sign: Rc::new(move |request: CmsSignRequest| {
            answer(callback.call(vec![request.to_json()])).map(|v| Ok(bytes(&v)))
        }),
        hash: value.get("hash").and_then(Value::as_str).and_then(Digest::from_name),
    }
}

fn signer_port(h: &Harness, value: &Value) -> Result<SignerPort, Error> {
    if value.get("kind").and_then(Value::as_str) == Some("cms") {
        Ok(SignerPort::Cms(cms_signer(h, value)))
    } else {
        raw_signer(h, value).map(SignerPort::Raw)
    }
}

struct ScriptedSource {
    harness: Harness,
    spec: Value,
}

impl RevocationSource for ScriptedSource {
    fn fetch(&self, request: RevocationRequest) -> Promise<Vec<u8>> {
        let mut asked = Map::new();
        asked.insert("kind".into(), json!(request.kind()));
        asked.insert("url".into(), json!(request.url()));
        if let RevocationRequest::Ocsp { body, .. } = &request {
            asked.insert("body".into(), bytes_json(body));
        }
        scripted(&self.harness, &self.spec, vec![Value::Object(asked)]).map(|v| Ok(bytes(&v)))
    }
}

fn revocation_source(h: &Harness, value: Option<&Value>) -> Option<Rc<dyn RevocationSource>> {
    let spec = present(value)?.get("fetch")?.clone();
    Some(Rc::new(ScriptedSource { harness: h.clone(), spec }))
}

struct ScriptedTrust {
    harness: Harness,
    spec: Value,
    /// `timestampAnchors`, when the port has the member.
    timestamp_spec: Option<Value>,
}

impl TrustPort for ScriptedTrust {
    fn anchors(&self) -> Promise<Vec<Vec<u8>>> {
        scripted(&self.harness, &self.spec, vec![])
            .map(|v| Ok(v.as_array().map(|a| a.iter().map(bytes).collect()).unwrap_or_default()))
    }

    fn timestamp_anchors(&self) -> Option<Promise<Vec<Vec<u8>>>> {
        let spec = self.timestamp_spec.as_ref()?;
        Some(
            scripted(&self.harness, spec, vec![]).map(|v| {
                Ok(v.as_array().map(|a| a.iter().map(bytes).collect()).unwrap_or_default())
            }),
        )
    }
}

fn trust_port(h: &Harness, value: Option<&Value>) -> Option<Rc<dyn TrustPort>> {
    let value = present(value)?;
    let spec = value.get("anchors")?.clone();
    let timestamp_spec = present(value.get("timestampAnchors")).cloned();
    Some(Rc::new(ScriptedTrust { harness: h.clone(), spec, timestamp_spec }))
}

fn validation_time(value: Option<&Value>) -> Option<ValidationTime> {
    Some(ValidationTime::from_json(present(value)?))
}

struct ScriptedStore {
    harness: Harness,
    spec: Value,
}

impl PersonalKeyStore for ScriptedStore {
    fn load(&self, subject: &str) -> Promise<Option<PersonalKeyRecord>> {
        scripted(&self.harness, &self.spec["load"], vec![json!(subject)]).map(|v| {
            if v.is_null() || epdf_kernel::is_undefined(&v) {
                Ok(None)
            } else {
                Err(Error::new("[signature-core] a scripted store cannot hand out a key"))
            }
        })
    }

    fn save(&self, subject: &str, record: PersonalKeyRecord) -> Promise<()> {
        scripted(&self.harness, &self.spec["save"], vec![json!(subject), record.to_json()])
            .map(|_| Ok(()))
    }

    fn remove(&self, subject: &str) -> Promise<()> {
        scripted(&self.harness, &self.spec["remove"], vec![json!(subject)]).map(|_| Ok(()))
    }
}

/// A key the vector describes: a fixture key imported with an algorithm,
/// or (`fake`) an object with only `algorithm`.
fn key_spec(spec: &Value) -> CryptoKey {
    let algorithm = spec.get("algorithm").cloned().unwrap_or(Value::Null);
    if spec.get("fake").and_then(Value::as_bool) == Some(true) {
        return CryptoKey {
            key_type: "private".into(),
            extractable: false,
            algorithm,
            usages: vec![],
            handle: Rc::new(()),
        };
    }
    let fixture = spec.get("fixture").and_then(Value::as_str).unwrap_or("leaf").to_string();
    let name = algorithm.get("name").and_then(Value::as_str).unwrap_or_default();
    let reported = if name == "ECDSA" {
        json!({ "name": "ECDSA", "namedCurve": algorithm["namedCurve"] })
    } else {
        rsa_algorithm(
            name,
            algorithm.get("hash").and_then(Value::as_str).unwrap_or("SHA-256"),
            &fixture_key(&fixture, "spki"),
        )
    };
    CryptoKey {
        key_type: "private".into(),
        extractable: false,
        algorithm: reported,
        usages: vec!["sign".into()],
        handle: Rc::new(FixtureKey { name: fixture }),
    }
}

fn describe_signer(signer: &RawSigner) -> Map<String, Value> {
    let mut out = Map::new();
    out.insert("kind".into(), json!("raw"));
    out.insert(
        "certificateChain".into(),
        signer.certificate_chain.iter().map(|c| bytes_json(c)).collect(),
    );
    out.insert("algorithm".into(), json!(signer.algorithm.name()));
    out.insert("hash".into(), json!(signer.hash.name()));
    out
}

// ── the capability ───────────────────────────────────────────────────────────

/// A recorded transport over the case's scripted replies:
/// `{ status, body }`, or `{ fail }` for no answer.
fn recorded_transport(replies: &Value) -> (Rc<dyn HttpTransport>, Rc<RecordedTransport>) {
    let outcomes = replies
        .as_array()
        .map(|list| {
            list.iter()
                .map(|reply| match reply.get("fail").and_then(Value::as_str) {
                    Some(message) => TransportOutcome::Failed { message: message.into() },
                    None => TransportOutcome::Replied {
                        status: reply["status"].as_u64().unwrap_or(0) as u16,
                        body: present(reply.get("body")).map(bytes).unwrap_or_default(),
                    },
                })
                .collect()
        })
        .unwrap_or_default();
    let recorded = Rc::new(RecordedTransport::new(outcomes));
    (recorded.clone(), recorded)
}

/// The headers of a client's options, in order.
fn headers_of(options: &Value) -> Vec<TransportHeader> {
    options
        .get("headers")
        .and_then(Value::as_object)
        .map(|headers| {
            headers
                .iter()
                .map(|(name, value)| TransportHeader {
                    name: name.clone(),
                    value: value.as_str().unwrap_or_default().into(),
                })
                .collect()
        })
        .unwrap_or_default()
}

/// What a transport probe reports: every request sent, then the answer
/// under `key`, or the error's code, message and details.
fn probe_outcome(
    recorded: &RecordedTransport,
    key: &str,
    outcome: Result<Vec<u8>, Error>,
) -> Value {
    let sent: Vec<Value> = recorded
        .sent()
        .iter()
        .map(|request: &TransportRequest| {
            json!({
                "url": request.url,
                "contentType": request.content_type,
                "headers": request.headers.iter().map(|h| json!({ "name": h.name, "value": h.value })).collect::<Vec<_>>(),
                "body": bytes_json(&request.body),
            })
        })
        .collect();
    let mut out = Map::new();
    out.insert("sent".into(), Value::Array(sent));
    match outcome {
        Ok(answer) => {
            out.insert(key.into(), bytes_json(&answer));
        }
        Err(error) => {
            out.insert(
                "error".into(),
                json!({
                    "code": error.code(),
                    "message": error.message,
                    "details": error.member("details").cloned().unwrap_or(Value::Null),
                }),
            );
        }
    }
    Value::Object(out)
}

pub struct SignatureCapability {
    ctx: PluginContext,
    harness: Harness,
}

fn spawn(future: impl std::future::Future<Output = Out> + 'static) -> Outcome {
    Outcome::Promise(Promise::spawn(future))
}

impl SignatureCapability {
    fn crypto(&self) -> Rc<dyn WebCrypto> {
        Rc::new(ScriptedWebCrypto { harness: self.harness.clone() })
    }

    fn call(&self, method: &str, args: Vec<Value>) -> Outcome {
        let h = self.harness.clone();
        let arg = |i: usize| args.get(i).cloned().unwrap_or_else(epdf_kernel::undefined);
        match method {
            "verifyCmsSignature" => {
                let cms = bytes(&arg(0));
                spawn(async move { Ok(json!(verify_cms_signature(&cms).name())) })
            }
            "buildDetachedCms" => {
                let input = arg(0);
                let signer = raw_signer(&h, &input["signer"]);
                spawn(async move {
                    let built = build_detached_cms(BuildInput {
                        digest: bytes(&input["digest"]),
                        hash: digest_named(input["hash"].as_str().unwrap_or_default())?,
                        profile: Profile::from_name(input["profile"].as_str().unwrap_or_default())
                            .unwrap_or(Profile::CadesB),
                        signer: signer?,
                        signing_time: present(input.get("signingTime"))
                            .and_then(Value::as_str)
                            .map(str::to_string),
                    })
                    .await?;
                    Ok(bytes_json(&built))
                })
            }
            "verifyForCompletion" => {
                let input = arg(0);
                let prepared = &input["prepared"];
                let gate = verify_for_completion(
                    &bytes(&input["cms"]),
                    &Prepared {
                        digest: bytes(&prepared["digest"]),
                        algorithm: prepared["algorithm"].as_str().unwrap_or_default().to_string(),
                        contents_size_bytes: prepared["contentsSizeBytes"]
                            .as_f64()
                            .unwrap_or(f64::NAN),
                    },
                    // An unknown profile is neither `cades-b` nor `timestamp`,
                    // so the TypeScript checks it as `pkcs7`.
                    CompletionProfile::from_name(input["profile"].as_str().unwrap_or_default())
                        .unwrap_or(CompletionProfile::Signature(Profile::Pkcs7)),
                );
                spawn(async move { Ok(gate.to_json()) })
            }
            "validateChain" => {
                let cms = bytes(&arg(0));
                let trust = trust_port(&h, args.get(1));
                let at = validation_time(args.get(2)).unwrap_or(ValidationTime::Now);
                spawn(async move {
                    Ok(validate_chain(CmsInput::Bytes(cms), trust, at).await?.to_json())
                })
            }
            "sign" => {
                let doc = self.ctx.document_handle(arg(0).as_str());
                let mut input = arg(1).as_object().cloned().unwrap_or_default();
                let key = input.remove("key").unwrap_or(Value::Null);
                let port = signer_port(&h, &key);
                spawn(async move {
                    let doc = doc.ok_or_else(|| Error::new("[signature-core] no document"))?;
                    sign(doc, input, port?).await
                })
            }
            "validateSignatures" => {
                let doc = self.ctx.document_handle(arg(0).as_str());
                let options = present(args.get(1)).cloned().unwrap_or(Value::Null);
                let options = ValidateOptions {
                    trust: trust_port(&h, options.get("trust")),
                    validation_time: present(options.get("validationTime")).map(|v| {
                        if v.get("kind").and_then(Value::as_str) == Some("earliest-timestamp") {
                            Requested::Earliest
                        } else {
                            Requested::At(ValidationTime::from_json(v))
                        }
                    }),
                    revocation: revocation_source(&h, options.get("revocation")),
                    until: options.get("until").and_then(Value::as_str).map(str::to_string),
                };
                spawn(async move {
                    let doc = doc.ok_or_else(|| Error::new("[signature-core] no document"))?;
                    validate_signatures(doc, options).await
                })
            }
            "attachSignatureTimestamp" => {
                let input = arg(0);
                let authority = cms_signer(&h, &input["authority"]);
                let trust = trust_port(&h, input.get("trust"));
                spawn(async move {
                    Ok(bytes_json(
                        &attach_signature_timestamp(&bytes(&input["cms"]), &authority, trust)
                            .await?,
                    ))
                })
            }
            "completeSignature" => {
                let doc = self.ctx.document_handle(arg(0).as_str());
                let input = arg(1);
                let options = TimestampOptions {
                    timestamp_authority: present(input.get("timestampAuthority"))
                        .map(|a| cms_signer(&h, a)),
                    timestamp_trust: trust_port(&h, input.get("timestampTrust")),
                };
                spawn(async move {
                    let doc = doc.ok_or_else(|| Error::new("[signature-core] no document"))?;
                    complete_signature(
                        doc,
                        input["prepared"].clone(),
                        bytes(&input["cms"]),
                        options,
                    )
                    .await
                })
            }
            "checkRevocationAnswer" => {
                let input = arg(0);
                spawn(async move {
                    let kind = if input["kind"].as_str() == Some("ocsp") {
                        AnswerKind::Ocsp
                    } else {
                        AnswerKind::Crl
                    };
                    let certificate = Certificate::from_ber(&bytes(&input["certificate"]))?;
                    let issuer = Certificate::from_ber(&bytes(&input["issuer"]))?;
                    let answer = RevocationAnswer { kind, der: bytes(&input["der"]) };
                    Ok(check_revocation_answer(&answer, &certificate, &issuer).to_json())
                })
            }
            "addValidationInfo" => {
                let doc = self.ctx.document_handle(arg(0).as_str());
                let options = present(args.get(1)).cloned().unwrap_or(Value::Null);
                let list = |key: &str| -> Vec<Vec<u8>> {
                    options
                        .get("known")
                        .and_then(|k| k.get(key))
                        .and_then(Value::as_array)
                        .map(|l| l.iter().map(bytes).collect())
                        .unwrap_or_default()
                };
                let options = AddValidationInfoOptions {
                    trust: trust_port(&h, options.get("trust")),
                    revocation: revocation_source(&h, options.get("revocation")),
                    known_certificates: list("certificates"),
                    known_ocsp_responses: list("ocspResponses"),
                    known_crls: list("crls"),
                    fields: options.get("fields").and_then(Value::as_array).cloned(),
                    partial: options.get("partial").is_some_and(|v| v.as_bool() == Some(true)),
                    max_crl_bytes: options
                        .get("maxCrlBytes")
                        .and_then(Value::as_u64)
                        .map(|n| n as usize),
                };
                spawn(async move {
                    let doc = doc.ok_or_else(|| Error::new("[signature-core] no document"))?;
                    add_validation_info(doc, options).await
                })
            }
            "remoteSign" => {
                let spec = arg(0)["sign"].clone();
                let callback = h.callback(&spec);
                let signer = remote_signer(Rc::new(move |request: CmsSignRequest| {
                    answer(callback.call(vec![request.to_json()])).map(|v| Ok(bytes(&v)))
                }));
                let request = arg(1);
                spawn(async move {
                    let cms = (signer.sign)(CmsSignRequest {
                        digest: bytes(&request["digest"]),
                        algorithm: request["algorithm"].as_str().unwrap_or_default().into(),
                        sub_filter: request["subFilter"].as_str().unwrap_or_default().into(),
                    })
                    .await?;
                    Ok(bytes_json(&cms))
                })
            }
            "timestampHttp" => {
                let options = arg(0);
                let request = arg(1);
                let (transport, recorded) = recorded_transport(&arg(2));
                let signer = http_timestamp_authority(
                    transport,
                    self.crypto(),
                    HttpTimestampAuthorityOptions {
                        url: options["url"].as_str().unwrap_or_default().into(),
                        hash: options
                            .get("hash")
                            .and_then(Value::as_str)
                            .and_then(Digest::from_name),
                        policy: options.get("policy").and_then(Value::as_str).map(str::to_string),
                        headers: headers_of(&options),
                        timeout_ms: options
                            .get("timeoutMs")
                            .and_then(Value::as_u64)
                            .map(|n| n as u32),
                        retries: options.get("retries").and_then(Value::as_u64).map(|n| n as u32),
                    },
                );
                let request = CmsSignRequest {
                    digest: bytes(&request["digest"]),
                    algorithm: request["algorithm"].as_str().unwrap_or_default().into(),
                    sub_filter: request["subFilter"].as_str().unwrap_or_default().into(),
                };
                spawn(async move {
                    let outcome = (signer.sign)(request).await;
                    Ok(probe_outcome(&recorded, "token", outcome))
                })
            }
            "revocationRoute" => {
                let options = arg(0);
                let request = arg(1);
                let (transport, recorded) = recorded_transport(&arg(2));
                let source = http_revocation_source(
                    transport,
                    HttpRevocationSourceOptions {
                        url: options["url"].as_str().unwrap_or_default().into(),
                        headers: headers_of(&options),
                        timeout_ms: options
                            .get("timeoutMs")
                            .and_then(Value::as_u64)
                            .map(|n| n as u32),
                        retries: options.get("retries").and_then(Value::as_u64).map(|n| n as u32),
                        max_reply_bytes: options
                            .get("maxReplyBytes")
                            .and_then(Value::as_u64)
                            .map(|n| n as u32),
                    },
                );
                let url = request["url"].as_str().unwrap_or_default().to_string();
                let request = match request["kind"].as_str() {
                    Some("ocsp") => RevocationRequest::Ocsp { url, body: bytes(&request["body"]) },
                    Some("crl") => RevocationRequest::Crl { url },
                    _ => RevocationRequest::Issuer { url },
                };
                spawn(async move {
                    let outcome = source.fetch(request).await;
                    Ok(probe_outcome(&recorded, "answer", outcome))
                })
            }
            "verifyTimestampToken" => {
                let input = arg(0);
                let trust = trust_port(&h, input.get("trust"));
                spawn(async move {
                    let algorithm = digest_named(input["algorithm"].as_str().unwrap_or_default())?;
                    verify_timestamp_token(
                        &bytes(&input["token"]),
                        &bytes(&input["digest"]),
                        algorithm,
                        trust,
                    )
                    .await
                })
            }
            "ecdsaRawFromDer" => {
                let der = bytes(&arg(0));
                let curve = arg(1).as_str().unwrap_or_default().to_string();
                // Synchronous in the TypeScript: a value or a throw, no promise.
                match ecdsa_raw_from_der(&der, &curve) {
                    Ok(raw) => Outcome::Returned(bytes_json(&raw)),
                    Err(error) => Outcome::Threw(error),
                }
            }
            "memoryKeyStore" => {
                let ops = arg(0).as_array().cloned().unwrap_or_default();
                spawn(async move {
                    let store = MemoryKeyStore::<Value>::new();
                    let mut results = Vec::new();
                    for op in ops {
                        let subject = op["subject"].as_str().unwrap_or_default().to_string();
                        results.push(match op["op"].as_str() {
                            Some("load") => store.load(&subject).await?.unwrap_or(Value::Null),
                            Some("save") => {
                                store.save(&subject, op["record"].clone()).await?;
                                epdf_kernel::undefined()
                            }
                            _ => {
                                store.remove(&subject).await?;
                                epdf_kernel::undefined()
                            }
                        });
                    }
                    Ok(Value::Array(results))
                })
            }
            "webCryptoSigner" => {
                let crypto = self.crypto();
                let key = key_spec(&arg(0));
                let chain: Vec<Vec<u8>> =
                    arg(1).as_array().into_iter().flatten().map(bytes).collect();
                let hash = present(args.get(2)).and_then(Value::as_str).map(digest_named);
                let data = present(args.get(3)).map(bytes);
                spawn(async move {
                    let signer = web_crypto_signer(crypto, key, chain, hash.transpose()?)?;
                    let mut out = describe_signer(&signer);
                    if let Some(data) = data {
                        out.insert("signature".into(), bytes_json(&(signer.sign)(data).await?));
                    }
                    Ok(Value::Object(out))
                })
            }
            "generateSigningKeyPair" => {
                let crypto = self.crypto();
                let algorithm = RawAlgorithm::from_name(arg(0).as_str().unwrap_or_default());
                let hash = digest_named(arg(1).as_str().unwrap_or_default());
                let extractable = arg(2).as_bool().unwrap_or(false);
                spawn(async move {
                    let algorithm = algorithm
                        .ok_or_else(|| Error::new("[signature-core] unknown algorithm"))?;
                    let pair =
                        generate_signing_key_pair(&*crypto, algorithm, hash?, extractable).await?;
                    Ok(
                        json!({ "privateKey": pair.private_key.describe(), "publicKey": pair.public_key.describe() }),
                    )
                })
            }
            "selfSignedCertificate" => {
                let crypto = self.crypto();
                let input = arg(0);
                let rsa = input["key"]["fixture"].as_str().unwrap_or("leaf");
                let pair = fixture_pair(&input["key"]["algorithm"], false, rsa);
                spawn(async move {
                    let der = self_signed_certificate(
                        crypto,
                        pair.public_key,
                        pair.private_key,
                        input["commonName"].as_str().unwrap_or_default().into(),
                        digest_named(input["hash"].as_str().unwrap_or_default())?,
                        present(input.get("validityDays")).and_then(Value::as_f64),
                    )
                    .await?;
                    Ok(bytes_json(&der))
                })
            }
            "createTestSigner" => {
                let crypto = self.crypto();
                let options = present(args.first()).cloned().unwrap_or(Value::Null);
                let data = present(args.get(1)).map(bytes);
                spawn(async move {
                    let algorithm = options
                        .get("algorithm")
                        .and_then(Value::as_str)
                        .and_then(RawAlgorithm::from_name);
                    let hash = options
                        .get("hash")
                        .and_then(Value::as_str)
                        .map(digest_named)
                        .transpose()?;
                    let common_name =
                        options.get("commonName").and_then(Value::as_str).map(str::to_string);
                    let test = create_test_signer(crypto, common_name, algorithm, hash).await?;
                    let mut out = describe_signer(&test.signer);
                    out.insert("certificate".into(), bytes_json(&test.certificate));
                    out.insert("privateKey".into(), test.private_key.describe());
                    if let Some(data) = data {
                        out.insert(
                            "signature".into(),
                            bytes_json(&(test.signer.sign)(data).await?),
                        );
                    }
                    Ok(Value::Object(out))
                })
            }
            "personalSigner" => {
                let crypto = self.crypto();
                let input = arg(0);
                let times = present(args.get(1)).and_then(Value::as_u64).unwrap_or(1);
                let store: Rc<dyn PersonalKeyStore> = match present(input.get("store")) {
                    Some(spec) => Rc::new(ScriptedStore { harness: h.clone(), spec: spec.clone() }),
                    None => Rc::new(MemoryKeyStore::<PersonalKeyRecord>::new()),
                };
                spawn(async move {
                    let algorithm = input
                        .get("algorithm")
                        .and_then(Value::as_str)
                        .and_then(RawAlgorithm::from_name);
                    let hash =
                        input.get("hash").and_then(Value::as_str).map(digest_named).transpose()?;
                    let subject = input["subject"].as_str().unwrap_or_default().to_string();
                    let mut out = Vec::new();
                    for _ in 0..times {
                        let signer = personal_signer(
                            crypto.clone(),
                            subject.clone(),
                            store.clone(),
                            algorithm,
                            hash,
                        )
                        .await?;
                        let mut described = describe_signer(&signer.signer);
                        described.insert("certificate".into(), bytes_json(&signer.certificate));
                        described.insert("subject".into(), json!(signer.subject));
                        out.push(Value::Object(described));
                    }
                    Ok(Value::Array(out))
                })
            }
            other => {
                Outcome::Threw(Error::new(format!("[scenario] \"{other}\" is not a function")))
            }
        }
    }
}

const MEMBERS: &[&str] = &[
    "verifyCmsSignature",
    "buildDetachedCms",
    "verifyForCompletion",
    "validateChain",
    "sign",
    "validateSignatures",
    "remoteSign",
    "memoryKeyStore",
    "webCryptoSigner",
    "generateSigningKeyPair",
    "selfSignedCertificate",
    "createTestSigner",
    "personalSigner",
];

/// The `signature-core` scenario plugin.
pub struct SignatureHarness;

impl HarnessPlugin for SignatureHarness {
    fn factories(&self) -> Vec<&'static str> {
        vec!["signature-core"]
    }

    fn build(&self, _factory: &str, _config: &Value, h: &Harness) -> Result<AnyPlugin, Error> {
        let harness = h.clone();
        let reducer: SliceReducer = Rc::new(|_: &SliceState, _: &dyn epdf_kernel::Action| None);
        let to_json: SliceJson = Rc::new(|_: &dyn Any| epdf_kernel::undefined());
        Ok(Rc::new(PluginDef {
            id: "signature-core".into(),
            token: Some(h.token("signature-core", None)),
            scope: PluginScope::Workspace,
            requires: vec![],
            optional: vec![],
            initial_state: Rc::new(|| Rc::new(epdf_kernel::undefined()) as SliceState),
            reducer,
            to_json,
            to_text: None,
            capability: Some(Rc::new(move |ctx: PluginContext| {
                Ok(Rc::new(SignatureCapability { ctx, harness: harness.clone() }) as Rc<dyn Any>)
            })),
            init: None,
            effects: None,
            create: None,
            settings: None,
            resolves_any_capability: false,
            in_scope: None,
        }))
    }

    fn call(
        &self,
        capability: &Rc<dyn Any>,
        method: &str,
        args: Vec<Value>,
        _h: &Harness,
    ) -> Option<Outcome> {
        let capability = capability.downcast_ref::<SignatureCapability>()?;
        Some(capability.call(method, args))
    }

    fn members(&self, capability: &Rc<dyn Any>) -> Option<Vec<&'static str>> {
        capability.downcast_ref::<SignatureCapability>().map(|_| MEMBERS.to_vec())
    }
}

/// A CADES-B detached CMS over `digest` (SHA-256), signed with a PKCS#8
/// PEM RSA key and carrying one PEM certificate. For engine tests that seal
/// a prepared signing with the CMS this crate builds, without a kernel.
pub fn test_cms_over_digest(
    digest: &[u8],
    private_key_pem: &str,
    certificate_pem: &str,
) -> Vec<u8> {
    let pem_body = |pem: &str| {
        let body: String = pem.lines().filter(|l| !l.starts_with("-----")).collect();
        STANDARD.decode(body).expect("PEM base64")
    };
    let key = PrivateKey::from_pkcs8_der(&pem_body(private_key_pem)).expect("a PKCS#8 RSA key");
    let signer = crate::cms::RawSigner {
        certificate_chain: vec![pem_body(certificate_pem)],
        algorithm: crate::cms::RawAlgorithm::RsaPkcs1,
        hash: Digest::Sha256,
        sign: std::rc::Rc::new(move |message: Vec<u8>| {
            match key.sign(Pkcs1v15Sign::new::<sha2::Sha256>(), &Digest::Sha256.hash(&message)) {
                Ok(signature) => Promise::resolved(signature),
                Err(e) => Promise::rejected(Error::new(e.to_string())),
            }
        }),
    };
    let build = crate::cms::build_detached_cms(crate::cms::BuildInput {
        digest: digest.to_vec(),
        hash: Digest::Sha256,
        profile: crate::cms::Profile::CadesB,
        signer,
        signing_time: None,
    });
    // The signer answers synchronously; the kernel runtime drains the rest.
    type Built = Option<Result<Vec<u8>, Error>>;
    let out: Rc<std::cell::RefCell<Built>> = Rc::default();
    let slot = out.clone();
    epdf_kernel::rt::spawn(async move {
        *slot.borrow_mut() = Some(build.await);
    });
    epdf_kernel::rt::run_until_stalled();
    let built = out.borrow_mut().take();
    built.expect("the CMS build settled").expect("the CMS builds")
}
