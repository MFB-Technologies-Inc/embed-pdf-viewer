//! Long-term validation and the signing fixes upstream added in
//! `ed064e0c8..c4e744345`: `vectors/signature/{validation-info,
//! timestamp-field,long-term}.json` replayed through `Host::call`.
//!
//! Regenerate the vectors, deliberately, with:
//!   pnpm --filter @embedpdf/tooling-engine-conformance run signature-ltv
//!
//! Each case opens one fixture with a scope and runs steps against it, each
//! step one call of the TypeScript API. This sends the wire request the call
//! stands for (`LocalDocumentSignaturesService`, `LocalDocumentFormsService`
//! and the rest, whose gates the `Host` keeps) and takes the same view of the
//! answer the generator took: the fields the upstream tests assert, a
//! refusal's code and its `details.field`, and the events of the two kinds
//! the range added or changed. An entry the engine read back is compared as
//! its SHA-256.

use epdf_engine::host::{Host, RequestEffect};
use serde::Deserialize;
use serde_json::{Map, Value, json};

const SCHEMA_VERSION: u32 = 1;
const ARTWORK: &str = "packages/engine/main/test/fixtures/signature_artwork.pdf";
const KINDS: [&str; 3] = ["certificates", "ocspResponses", "crls"];

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct VectorFile {
    schema_version: u32,
    kind: String,
    cases: Vec<Case>,
}

#[derive(Debug, Deserialize)]
struct Case {
    name: String,
    covers: String,
    fixture: Value,
    open: Value,
    steps: Vec<Value>,
}

fn load(file: &str, kind: &str) -> VectorFile {
    let path = test_support::corpus::vectors_dir("signature").join(file);
    let text =
        std::fs::read_to_string(&path).unwrap_or_else(|e| panic!("read {}: {e}", path.display()));
    let vectors: VectorFile = serde_json::from_str(&text).unwrap_or_else(|e| panic!("{file}: {e}"));
    assert_eq!(vectors.schema_version, SCHEMA_VERSION, "{file}: vector envelope moved");
    assert_eq!(vectors.kind, kind, "{file}");
    vectors
}

fn unhex(text: &str) -> Vec<u8> {
    (0..text.len()).step_by(2).map(|i| u8::from_str_radix(&text[i..i + 2], 16).unwrap()).collect()
}

fn sha256(bytes: &[u8]) -> String {
    pdfium::sha256(bytes).expect("sha256").iter().map(|b| format!("{b:02x}")).collect()
}

/// One case's sessions on one host.
struct Run<'a> {
    host: Host,
    case: &'a Case,
    doc: String,
    docs: usize,
    job: u64,
    loaded: Vec<u8>,
    /// The parked signing: its id and the version it was prepared on.
    parked: Option<(Value, Value)>,
}

/// What one request answered: its result or its error, the notice beside
/// it, and its payloads.
struct Answer {
    outcome: Result<Value, Value>,
    notice: Option<Value>,
    payloads: Vec<Vec<u8>>,
}

impl<'a> Run<'a> {
    fn new(case: &'a Case) -> Self {
        let loaded = match &case.fixture {
            Value::String(path) => test_support::fixture(path),
            fixture => unhex(fixture["hex"].as_str().expect("a fixture path or its hex")),
        };
        let mut run = Run {
            host: Host::new(),
            case,
            doc: String::new(),
            docs: 0,
            job: 1,
            loaded,
            parked: None,
        };
        let loaded = run.loaded.clone();
        run.open(loaded);
        run
    }

    /// `engine.open` as `LocalEngine` opens: the case's scope, identity and
    /// signed-document policy, 64 object numbers reserved.
    fn open(&mut self, bytes: Vec<u8>) {
        let doc = format!("ltv:{}:{}", self.case.name, self.docs);
        self.docs += 1;
        let mut request = json!({ "kind": "open.fatMem", "effect": "open", "docId": doc,
            "password": null, "reserveObjectNumbers": 64 });
        for key in ["scope", "identity", "signedDocumentPolicy"] {
            if let Some(value) = self.case.open.get(key) {
                request[key] = value.clone();
            }
        }
        let opened = self.send(request, vec![bytes]);
        assert!(opened.outcome.is_ok(), "{}: the fixture would not open", self.case.name);
        self.doc = doc;
    }

    fn send(&mut self, mut request: Value, payloads: Vec<Vec<u8>>) -> Answer {
        self.job += 1;
        request["jobId"] = json!(self.job);
        if request.get("docId").is_none() {
            request["docId"] = json!(self.doc);
        }
        if request.get("effect").is_none() {
            let kind = request["kind"].as_str().unwrap().to_owned();
            request["effect"] = json!(RequestEffect::of(&kind).expect("a job kind").as_str());
        }
        let response = self.host.call_filled(&request.to_string(), payloads);
        let mut envelope: Value =
            serde_json::from_str(&response.json).expect("every response is JSON");
        let notice = envelope.get_mut("event").map(Value::take);
        let outcome = match envelope.get_mut("error") {
            Some(error) => Err(error.take()),
            None => Ok(envelope.get_mut("result").map(Value::take).unwrap_or(Value::Null)),
        };
        Answer { outcome, notice, payloads: response.payloads }
    }

    /// The view of a refusal: its code, and the field its details name.
    fn refusal(error: &Value) -> Value {
        let mut view = json!({ "reject": error["code"] });
        if let Some(field) = error.pointer("/details/field") {
            view["field"] = field.clone();
        }
        view
    }

    fn op_id(&self, step: &Value) -> String {
        step["opId"].as_str().map_or_else(|| format!("op-{}", self.job + 1), str::to_owned)
    }

    /// One step: its outcome's view and the events it published.
    fn step(&mut self, step: &Value) -> (Value, Vec<Value>) {
        let mut events = Vec::new();
        let named = step.get("opId").is_some();
        let call = step["call"].as_str().unwrap();
        let viewed = match call {
            "getValidationInfo" => {
                let answer = self.send(json!({ "kind": "signatures.validationInfo" }), Vec::new());
                answer.outcome.map(|result| {
                    let lists: Map<String, Value> = KINDS
                        .iter()
                        .map(|kind| {
                            let digests = result[*kind]
                                .as_array()
                                .unwrap()
                                .iter()
                                .map(|entry| {
                                    let index = entry["payload"].as_u64().unwrap() as usize;
                                    json!(sha256(&answer.payloads[index]))
                                })
                                .collect();
                            ((*kind).to_owned(), Value::Array(digests))
                        })
                        .collect();
                    Value::Object(lists)
                })
            }
            "addValidationInfo" => {
                let mut request =
                    json!({ "kind": "signatures.addValidationInfo", "opId": self.op_id(step) });
                let mut payloads = Vec::new();
                if step["input"] == "notBytes" {
                    request["certificates"] = json!(["MIIB"]);
                } else {
                    for (kind, entries) in step["input"].as_object().unwrap() {
                        let refs: Vec<Value> = entries
                            .as_array()
                            .unwrap()
                            .iter()
                            .map(|entry| {
                                payloads.push(unhex(entry.as_str().unwrap()));
                                json!({ "payload": payloads.len() - 1 })
                            })
                            .collect();
                        request[kind] = Value::Array(refs);
                    }
                }
                let answer = self.send(request, payloads);
                answer.outcome.map(|mut result| {
                    if let Some(event) = result.get("event") {
                        let mut seen = json!({ "type": event["type"], "added": event["added"] });
                        if named {
                            seen["opId"] = event["meta"]["opId"].clone();
                        }
                        events.push(seen);
                    }
                    let result = result["result"].take();
                    let mut view = json!({ "added": result["added"],
                        "undoable": result["meta"]["undoable"],
                        "affectedPages": result["meta"]["affectedPages"] });
                    if named {
                        view["opId"] = result["meta"]["opId"].clone();
                    }
                    view
                })
            }
            "prepare" => {
                let mut input = step["input"].clone();
                let mut payloads = Vec::new();
                if step.get("artwork").is_some() {
                    input["appearance"] = json!({ "pdf": { "payload": 0 } });
                    payloads.push(test_support::fixture(ARTWORK));
                }
                let answer =
                    self.send(json!({ "kind": "signatures.prepare", "input": input }), payloads);
                if let Some(notice) = &answer.notice
                    && notice["type"] == "signatures.prepared"
                {
                    events.push(json!({ "type": notice["type"], "field": notice["field"] }));
                }
                answer.outcome.map(|mut result| {
                    let prepared = result["result"].take();
                    self.parked =
                        Some((prepared["signingId"].clone(), prepared["expectedVersion"].clone()));
                    let mut view = json!({ "subFilter": prepared["subFilter"] });
                    if let Some(field) = prepared.get("field") {
                        view["field"] = field.clone();
                    }
                    view
                })
            }
            "complete" => {
                let (signing_id, version) = self.parked.clone().expect("a parked signing");
                let request = json!({ "kind": "signatures.complete", "opId": self.op_id(step),
                    "input": { "signingId": signing_id, "expectedVersion": version } });
                let cms = unhex(step["cms"].as_str().unwrap());
                self.send(request, vec![cms]).outcome.map(|mut result| {
                    let result = result["result"].take();
                    let signature = &result["signature"];
                    json!({ "status": result["status"], "kind": signature["kind"],
                        "fieldName": signature["fieldName"], "coverage": signature["coverage"],
                        "revisionIndex": signature["revisionIndex"] })
                })
            }
            "cancel" => {
                let (signing_id, _) = self.parked.clone().expect("a parked signing");
                let request = json!({ "kind": "signatures.cancel", "signingId": signing_id });
                self.send(request, Vec::new())
                    .outcome
                    .map(|result| json!({ "status": result["result"]["status"] }))
            }
            "listSignatures" => {
                self.send(json!({ "kind": "signatures.list" }), Vec::new()).outcome.map(|result| {
                    let snapshot = &result["snapshot"];
                    let signatures: Vec<Value> = snapshot["signatures"]
                        .as_array()
                        .unwrap()
                        .iter()
                        .map(|s| {
                            let coverage = if s["signed"] == true {
                                s["coverage"].as_str().unwrap().to_owned()
                            } else {
                                "unsigned".to_owned()
                            };
                            json!(format!(
                                "{} {} {coverage}",
                                s["fieldName"].as_str().unwrap(),
                                s["kind"].as_str().unwrap()
                            ))
                        })
                        .collect();
                    json!({ "revisions": snapshot["revisions"].as_array().unwrap().len(),
                        "enforced": snapshot["protection"]["enforced"], "signatures": signatures })
                })
            }
            "listFields" => {
                let answer = self.send(json!({ "kind": "forms.list" }), Vec::new());
                let first_page =
                    self.send(json!({ "kind": "pages.list" }), Vec::new()).outcome.ok().and_then(
                        |pages| {
                            let pages = pages.get("pages").cloned().unwrap_or(pages);
                            pages[0]["ref"].clone().into()
                        },
                    );
                answer.outcome.map(|result| {
                    let form = result.get("snapshot").cloned().unwrap_or(result);
                    let fields: Vec<Value> = form["fields"]
                        .as_array()
                        .unwrap()
                        .iter()
                        .map(|f| {
                            json!(format!(
                                "{} {}",
                                f["name"].as_str().unwrap(),
                                f["kind"].as_str().unwrap()
                            ))
                        })
                        .collect();
                    let mut view = json!({ "fields": fields });
                    if let Some(name) = step["widgetOf"].as_str() {
                        let field = form["fields"]
                            .as_array()
                            .unwrap()
                            .iter()
                            .find(|f| f["name"] == name)
                            .expect("the field");
                        let widget = form["widgets"]
                            .as_array()
                            .unwrap()
                            .iter()
                            .find(|w| w["field"] == field["ref"])
                            .expect("its widget");
                        view["widget"] = json!({
                            "onFirstPage": Some(&widget["page"]) == first_page.as_ref(),
                            "width": widget["rect"]["width"], "height": widget["rect"]["height"],
                            "print": widget["print"], "locked": widget["locked"],
                            "hidden": widget["hidden"] });
                    }
                    view
                })
            }
            "analyze" => {
                let request = json!({ "kind": "signatures.analyze", "input": step["input"] });
                self.send(request, Vec::new()).outcome.map(|result| {
                    let analysis = &result["analysis"];
                    let findings: Vec<&Value> = analysis["steps"]
                        .as_array()
                        .unwrap()
                        .iter()
                        .flat_map(|s| s["findings"].as_array().unwrap())
                        .collect();
                    let mut refused: Vec<String> = findings
                        .iter()
                        .filter(|f| f["verdict"] != "permitted")
                        .map(|f| {
                            format!(
                                "{} {} {}",
                                f["rule"].as_str().unwrap(),
                                f["objectNumber"],
                                f["verdict"].as_str().unwrap()
                            )
                        })
                        .collect();
                    refused.sort();
                    let mut rules: Vec<&str> =
                        findings.iter().map(|f| f["rule"].as_str().unwrap()).collect();
                    rules.sort_unstable();
                    rules.dedup();
                    json!({ "verdict": analysis["verdict"],
                        "current": analysis["current"]["verdict"],
                        "policyVersion": analysis["policyVersion"],
                        "steps": analysis["steps"].as_array().unwrap().len(),
                        "refused": refused, "rules": rules })
                })
            }
            "createTextField" => {
                let name = step["name"].as_str().unwrap();
                let request = json!({ "kind": "forms.createField", "opId": format!("field:{name}"),
                    "draft": { "kind": "text", "name": name } });
                self.send(request, Vec::new()).outcome.map(|_| json!({}))
            }
            "apply" => {
                let request = json!({ "kind": "document.apply", "opId": step["opId"],
                    "change": step["change"] });
                self.send(request, Vec::new()).outcome.map(|result| {
                    let result = &result["result"];
                    let items: Vec<Value> = result["items"]
                        .as_array()
                        .unwrap()
                        .iter()
                        .map(|i| i["type"].clone())
                        .collect();
                    json!({ "items": items, "undoable": result["meta"]["undoable"] })
                })
            }
            "reopen" => {
                let answer = self.send(
                    json!({ "kind": "document.saveBuffer", "mode": "incremental" }),
                    Vec::new(),
                );
                match answer.outcome {
                    Ok(_) => {
                        let saved = answer.payloads.into_iter().next().expect("the saved bytes");
                        let kept = saved.starts_with(&self.loaded);
                        self.open(saved);
                        Ok(json!({ "prefixKept": kept }))
                    }
                    Err(error) => Err(error),
                }
            }
            other => panic!("{}: no call {other}", self.case.name),
        };
        let view = match viewed {
            Ok(view) => json!({ "resolve": view }),
            Err(error) => Self::refusal(&error),
        };
        (view, events)
    }
}

fn replay_file(file: &str, kind: &str, cases: usize) {
    let vectors = load(file, kind);
    assert_eq!(vectors.cases.len(), cases, "{file}: the case list moved");
    let mut failures = Vec::new();
    for case in &vectors.cases {
        let mut run = Run::new(case);
        for (s, step) in case.steps.iter().enumerate() {
            let (outcome, events) = run.step(step);
            let outcome = test_support::canonical_numbers(&outcome);
            let expected = test_support::canonical_numbers(&step["outcome"]);
            if outcome != expected {
                failures.push(format!(
                    "{} step {s} {}: {}\n    got      {outcome}\n    expected {expected}",
                    case.name, step["call"], case.covers
                ));
                break;
            }
            let events = test_support::canonical_numbers(&Value::Array(events));
            if events != test_support::canonical_numbers(&step["events"]) {
                failures.push(format!(
                    "{} step {s} {} events:\n    got      {events}\n    expected {}",
                    case.name, step["call"], step["events"]
                ));
                break;
            }
        }
    }
    assert!(
        failures.is_empty(),
        "{} of {cases} cases differ:\n{}",
        failures.len(),
        failures.join("\n")
    );
}

#[test]
fn validation_data_is_read_and_stored_as_the_typescript_does() {
    replay_file("validation-info.json", "signatures.validationInfo", 10);
}

#[test]
fn a_document_timestamp_gets_a_field_of_its_own() {
    replay_file("timestamp-field.json", "signatures.prepare.timestamp", 7);
}

#[test]
fn the_long_term_files_read_and_judge_as_the_typescript_does() {
    replay_file("long-term.json", "signatures.longTerm", 22);
}
