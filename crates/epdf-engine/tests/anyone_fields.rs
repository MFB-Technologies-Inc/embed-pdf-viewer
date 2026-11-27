//! `runAnyoneFieldsConformance.ts` (b45d916cb, 4c911282d): the tests that
//! sign, hand a document on to the next person, or export and import. The
//! tests one session runs are change vectors (`vectors/changes/anyone-fields.json`,
//! replayed by `change_ops.rs`). "An image field is every signer's to fill
//! in" waits for `forms.setButtonImage` (CU-C5).
//!
//! Each person opens the file the last one left, as `handOn` does with
//! `download()`: an incremental save, so earlier signatures stay whole.

use epdf_engine::host::Host;
use serde_json::{Value, json};

/// A DER `SEQUENCE`: what a completion takes as a CMS, without judging it.
const PLACEHOLDER_CMS: [u8; 8] = [0x30, 0x06, 0x02, 0x01, 0x01, 0x02, 0x01, 0x02];

/// `ANYONE_FIELDS_PDF`, as the change vectors record it.
fn anyone_fields_pdf() -> Vec<u8> {
    let path = test_support::corpus::vectors_dir("changes").join("anyone-fields.json");
    let vectors: Value = serde_json::from_str(&std::fs::read_to_string(path).unwrap()).unwrap();
    assert_eq!(vectors["fixture"], "ANYONE_FIELDS_PDF");
    let text = vectors["fixtureHex"].as_str().unwrap();
    (0..text.len()).step_by(2).map(|i| u8::from_str_radix(&text[i..i + 2], 16).unwrap()).collect()
}

/// `pdfOf`: a PDF of `objects`, numbered from 1.
fn pdf_of(objects: &[String]) -> Vec<u8> {
    let mut out = b"%PDF-1.7\n".to_vec();
    let mut offsets = Vec::new();
    for (i, object) in objects.iter().enumerate() {
        offsets.push(out.len());
        out.extend(format!("{} 0 obj\n{object}\nendobj\n", i + 1).as_bytes());
    }
    let xref = out.len();
    out.extend(format!("xref\n0 {}\n0000000000 65535 f \n", objects.len() + 1).as_bytes());
    for offset in offsets {
        out.extend(format!("{offset:010} 00000 n \n").as_bytes());
    }
    out.extend(
        format!(
            "trailer\n<< /Size {} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n",
            objects.len() + 1
        )
        .as_bytes(),
    );
    out
}

/// `OWN_LOCK_PDF`: signature fields that carry a lock of their own: `all_sig`,
/// in no group, locks every field; `dana_sig`, in Dana's group, locks
/// `dana_name` alone. `reference` is a field anyone fills in.
fn own_lock_pdf() -> Vec<u8> {
    let meta = |entries: &str| format!("/EMBD_Metadata << /SchemaVersion 1 {entries} >>");
    let widget = |kind: &str, name: &str, rect: &str, extra: &str| {
        format!(
            "<< /Type /Annot /Subtype /Widget /FT /{kind} /T ({name}) /Rect [{rect}] /F 4 /P 3 0 R \
             {extra} >>"
        )
    };
    let lock = |entries: &str| format!("/Lock << /Type /SigFieldLock {entries} >>");
    let fields = "4 0 R 5 0 R 6 0 R 7 0 R";
    pdf_of(&[
        format!(
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [{fields}] /DA (/Helv 10 Tf 0 g) \
             /DR << /Font << /Helv << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> >> >>"
        ),
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>".into(),
        format!(
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 400] /Contents 8 0 R /Resources << >> \
             /Annots [{fields}] >>"
        ),
        widget("Tx", "dana_name", "20 360 180 380", &meta("/GroupID (dana)")),
        widget("Tx", "reference", "20 320 180 340", &meta("/ForAnyone true")),
        widget("Sig", "all_sig", "20 20 120 60", &lock("/Action /All")),
        widget(
            "Sig",
            "dana_sig",
            "140 20 240 60",
            &format!(
                "{} {}",
                meta("/GroupID (dana)"),
                lock("/Action /Include /Fields [(dana_name)]")
            ),
        ),
        "<< /Length 0 >>\nstream\n\nendstream".into(),
    ])
}

/// `ANYONE_FIELD_TOKENS`: a token's scope and identity.
fn token(name: &str) -> (Vec<&'static str>, Value) {
    let base = ["doc.open", "doc.render", "doc.download"];
    match name {
        "sender" => (
            [&base[..], &["doc.forms.modify", "doc.sign", "fields:set-group:all"]].concat(),
            json!({ "user_id": "sam", "display_name": "Sam" }),
        ),
        "dana" => (
            [&base[..], &["fields:fill:group=dana", "fields:sign:group=dana"]].concat(),
            json!({ "user_id": "dana", "display_name": "Dana Smith" }),
        ),
        "ada" => (
            [&base[..], &["fields:fill:group=ada", "fields:sign:group=ada"]].concat(),
            json!({ "user_id": "ada", "display_name": "Ada Lovelace" }),
        ),
        "filler" => (vec!["doc.open", "doc.render", "doc.forms.fill", "doc.sign"], json!({})),
        "reader" => (vec!["doc.open", "doc.render", "doc.forms.read"], json!({})),
        "designer" => (
            vec!["doc.open", "doc.render", "doc.forms.modify", "fields:set-group:group=dana"],
            json!({ "user_id": "lisa" }),
        ),
        "everything" => (vec!["*"], json!({})),
        other => panic!("no token {other}"),
    }
}

fn fqn(name: &str) -> Value {
    json!({ "kind": "fqn", "name": name })
}

/// One person's session on a copy of a file.
struct Doc {
    host: Host,
    job: u64,
}

impl Doc {
    /// `as(token, …, bytes)`: open `bytes` with the token's scope and identity.
    fn open_as(name: &str, bytes: Vec<u8>) -> Self {
        Self::open_with(name, bytes, json!({}))
    }

    fn open_with(name: &str, bytes: Vec<u8>, extra: Value) -> Self {
        let (scope, identity) = token(name);
        let mut doc = Doc { host: Host::new(), job: 1 };
        let mut request = json!({ "kind": "open.fatMem", "effect": "open", "scope": scope,
            "identity": identity, "reserveObjectNumbers": 64 });
        for (key, value) in extra.as_object().unwrap() {
            request[key] = value.clone();
        }
        let (answer, _) = doc.send(request, vec![bytes]);
        assert!(answer.get("error").is_none(), "{answer}");
        doc
    }

    /// `download()`: the file as this person leaves it.
    fn download(&mut self) -> Vec<u8> {
        let (answer, mut payloads) =
            self.send(json!({ "kind": "document.saveBuffer", "mode": "incremental" }), Vec::new());
        assert!(answer.get("error").is_none(), "{answer}");
        payloads.remove(0)
    }

    fn send(&mut self, mut request: Value, payloads: Vec<Vec<u8>>) -> (Value, Vec<Vec<u8>>) {
        self.job += 1;
        request["jobId"] = json!(self.job);
        request["docId"] = json!("d");
        if request.get("effect").is_none() {
            let kind = request["kind"].as_str().unwrap().to_owned();
            request["effect"] =
                json!(epdf_engine::host::RequestEffect::of(&kind).expect("a kind").as_str());
        }
        if request.get("opId").is_none() && request["effect"] == "write" {
            request["opId"] = json!(format!("op-{}", self.job));
        }
        let response = self.host.call_filled(&request.to_string(), payloads);
        (serde_json::from_str(&response.json).unwrap(), response.payloads)
    }

    /// A request's `result`, or its error.
    fn call(&mut self, request: Value) -> Result<Value, Value> {
        let (mut answer, _) = self.send(request, Vec::new());
        match answer.get_mut("error") {
            Some(error) => Err(error.take()),
            None => Ok(answer["result"].take()),
        }
    }

    fn ok(&mut self, request: Value) -> Value {
        let shown = request.to_string();
        self.call(request).unwrap_or_else(|e| panic!("{shown}: {e}"))
    }

    /// `fill`: what filling `name` in with `value` gives, `"resolved"` or the
    /// refusal's code.
    fn fill(&mut self, name: &str, value: Value) -> String {
        match self.call(json!({ "kind": "forms.setValue", "ref": fqn(name), "value": value })) {
            Ok(_) => "resolved".into(),
            Err(error) => error["code"].as_str().unwrap_or("?").to_owned(),
        }
    }

    fn fill_text(&mut self, name: &str, text: &str) -> String {
        self.fill(name, json!({ "value": text }))
    }

    fn list(&mut self) -> Value {
        self.ok(json!({ "kind": "forms.list" }))
    }

    fn field(&mut self, name: &str) -> Value {
        self.list()["fields"]
            .as_array()
            .unwrap()
            .iter()
            .find(|f| f["name"] == name)
            .cloned()
            .unwrap_or_else(|| panic!("no field '{name}'"))
    }

    fn value_of(&mut self, name: &str) -> Value {
        self.field(name)["value"].clone()
    }

    /// Prepare and complete a signing, the CMS a placeholder.
    fn sign(&mut self, name: &str, lock: Option<Value>) {
        let mut input = json!({ "field": fqn(name) });
        if let Some(lock) = lock {
            input["lock"] = lock;
        }
        let prepared = self.ok(json!({ "kind": "signatures.prepare", "input": input }));
        let result = &prepared["result"];
        let (completed, _) = self.send(
            json!({ "kind": "signatures.complete",
                "input": { "signingId": result["signingId"],
                    "expectedVersion": result["expectedVersion"] } }),
            vec![PLACEHOLDER_CMS.to_vec()],
        );
        let completed = completed["result"].clone();
        assert_eq!(completed["result"]["status"], "completed", "{completed}");
    }

    fn signatures(&mut self) -> Value {
        self.ok(json!({ "kind": "signatures.list" }))["snapshot"].clone()
    }

    /// `lockOf`: what the signature in the field `name` locks.
    fn lock_of(&mut self, name: &str) -> Value {
        self.signatures()["signatures"]
            .as_array()
            .unwrap()
            .iter()
            .find(|s| s["signed"] == true && s["fieldName"] == name)
            .map_or(Value::Null, |s| s["fieldMdp"].clone())
    }

    /// The fields what `name`'s signature locks names, sorted.
    fn locked_by(&mut self, name: &str) -> Vec<String> {
        let mut fields: Vec<String> = self.lock_of(name)["fields"]
            .as_array()
            .map(|names| names.iter().map(|n| n.as_str().unwrap().to_owned()).collect())
            .unwrap_or_default();
        fields.sort();
        fields
    }
}

fn sorted(names: &[&str]) -> Vec<String> {
    let mut names: Vec<String> = names.iter().map(|n| (*n).to_owned()).collect();
    names.sort();
    names
}

#[test]
fn a_token_that_may_fill_in_no_field_is_refused_naming_the_capability() {
    let mut doc = Doc::open_as("reader", anyone_fields_pdf());
    let refused = doc
        .call(
            json!({ "kind": "forms.setValue", "ref": fqn("reference"), "value": { "value": "x" } }),
        )
        .unwrap_err();
    assert_eq!(refused["code"], "Forbidden", "{refused}");
    assert_eq!(refused["details"]["required"], "doc.forms.fill", "{refused}");
}

#[test]
fn a_groups_token_resets_imports_values_into_and_undoes_its_fill_of_such_a_field() {
    let mut everything = Doc::open_as("everything", anyone_fields_pdf());
    everything.fill_text("reference", "R-7");
    everything.fill_text("ada_name", "Ada");
    let bundle =
        everything.ok(json!({ "kind": "forms.export", "selection": {} }))["bundle"].clone();

    let mut doc = Doc::open_as("dana", anyone_fields_pdf());
    let imported = doc.ok(json!({ "kind": "forms.importValues", "bundle": bundle,
        "attribution": "stamp" }));
    let names: Vec<&str> = imported["result"]["fields"]
        .as_array()
        .unwrap()
        .iter()
        .map(|f| f["name"].as_str().unwrap())
        .collect();
    assert!(names.contains(&"reference"), "{names:?}");
    assert!(!names.contains(&"ada_name"), "{names:?}");
    assert_eq!(doc.value_of("reference"), "R-7");
    let filled = doc.ok(json!({ "kind": "forms.setValue", "ref": fqn("reference"),
        "value": { "value": "R-8" } }));
    doc.ok(json!({ "kind": "document.apply",
        "change": { "undoOf": filled["result"]["meta"]["opId"] } }));
    assert_eq!(doc.value_of("reference"), "R-7");
    doc.ok(json!({ "kind": "forms.reset", "refs": [fqn("reference")] }));
    assert_eq!(doc.value_of("reference"), "");
}

#[test]
fn dana_fills_one_in_corrects_it_and_signs_it_locks_at_her_value_an_empty_one_stays_open() {
    let mut dana = Doc::open_as("dana", anyone_fields_pdf());
    dana.fill_text("reference", "R-1");
    dana.fill_text("reference", "R-2");
    dana.fill_text("dana_name", "Dana Smith");
    dana.sign("dana_sig", None);
    assert_eq!(dana.lock_of("dana_sig")["action"], "include");
    // Her group, the filled fields anyone fills in (hers and the sender's), not the empty one.
    assert_eq!(dana.locked_by("dana_sig"), sorted(&["both", "dana_name", "delivery", "reference"]));
    assert_eq!(dana.fill_text("reference", "R-3"), "ProtectedDocument");
    assert_eq!(dana.value_of("reference"), "R-2");
    let signed_by_dana = dana.download();

    let mut ada = Doc::open_as("ada", signed_by_dana);
    // Locked for Ada too, as for everyone.
    assert_eq!(ada.fill_text("reference", "R-9"), "ProtectedDocument");
    assert_eq!(ada.fill_text("delivery", "soon"), "ProtectedDocument");
    // The empty one is still open: Ada fills it in, and her signing locks it.
    assert_eq!(ada.fill_text("notes", "Leave at the door"), "resolved");
    ada.sign("ada_sig", None);
    assert_eq!(ada.locked_by("ada_sig"), sorted(&["ada_name", "delivery", "notes", "reference"]));
    assert_eq!(ada.fill_text("notes", "x"), "ProtectedDocument");
    let signed_by_ada = ada.download();

    let mut everything = Doc::open_as("everything", signed_by_ada);
    assert_eq!(everything.value_of("reference"), "R-2");
    assert_eq!(everything.value_of("notes"), "Leave at the door");
    assert_eq!(everything.fill_text("notes", "y"), "ProtectedDocument");
}

#[test]
fn a_required_one_is_every_signers_to_fill_in_and_locks_once_it_holds_a_value() {
    let mut sender = Doc::open_as("sender", anyone_fields_pdf());
    let made = sender.ok(json!({ "kind": "forms.createField", "draft": { "kind": "checkbox",
        "name": "agree", "required": true, "forAnyone": true,
        "widgets": [{ "page": { "kind": "objectNumber", "objectNumber": 3 },
            "rect": { "x": 20, "y": 200, "width": 100, "height": 20 } }] } }))["result"]["field"]
        .clone();
    assert_eq!(
        (&made["required"], &made["forAnyone"], &made["groupId"]),
        (&json!(true), &json!(true), &Value::Null)
    );
    let prepared = sender.download();

    // Empty, Dana's signing leaves it open; Ada checks it and her signing locks it.
    let mut dana = Doc::open_as("dana", prepared);
    dana.sign("dana_sig", None);
    assert!(!dana.locked_by("dana_sig").contains(&"agree".to_owned()));
    let after_dana = dana.download();

    let mut ada = Doc::open_as("ada", after_dana);
    assert_eq!(ada.fill("agree", json!({ "checked": true })), "resolved");
    ada.sign("ada_sig", None);
    assert!(ada.locked_by("ada_sig").contains(&"agree".to_owned()));
    assert_eq!(ada.fill("agree", json!({ "checked": false })), "ProtectedDocument");
}

#[test]
fn the_senders_value_locks_at_the_first_signature() {
    let mut sender = Doc::open_as("sender", anyone_fields_pdf());
    sender.fill_text("notes", "Fragile");
    let prepared = sender.download();

    let mut dana = Doc::open_as("dana", prepared);
    dana.sign("dana_sig", None);
    let locked = dana.locked_by("dana_sig");
    assert!(locked.contains(&"notes".to_owned()), "{locked:?}");
    assert!(locked.contains(&"delivery".to_owned()), "{locked:?}");
    assert!(!locked.contains(&"reference".to_owned()), "{locked:?}");
    assert_eq!(dana.fill_text("notes", "x"), "ProtectedDocument");
    assert_eq!(dana.fill_text("reference", "R-1"), "resolved");
}

#[test]
fn a_signature_field_in_no_group_locks_the_filled_ones_and_with_none_filled_locks_nothing() {
    // Only the sender's value: the signing locks it and nothing else.
    let mut doc = Doc::open_as("filler", anyone_fields_pdf());
    doc.fill_text("reference", "R-1");
    doc.sign("witness_sig", None);
    assert_eq!(doc.lock_of("witness_sig")["action"], "include");
    assert_eq!(doc.locked_by("witness_sig"), sorted(&["delivery", "reference"]));
    assert_eq!(doc.fill_text("notes", "x"), "resolved");
    assert_eq!(doc.fill_text("sender_note", "x"), "resolved");

    // With every one empty, such a signing locks nothing, as it always did.
    let mut doc = Doc::open_as("filler", anyone_fields_pdf());
    doc.fill_text("delivery", "");
    doc.sign("witness_sig", None);
    assert_eq!(doc.lock_of("witness_sig"), Value::Null);
    assert_eq!(doc.signatures()["protection"]["fieldLocks"], json!([]));
}

#[test]
fn a_signing_that_names_its_own_lock_decides_for_itself() {
    let mut doc = Doc::open_as("dana", anyone_fields_pdf());
    doc.fill_text("reference", "R-1");
    doc.sign("dana_sig", Some(json!({ "action": "include", "fields": ["dana_name"] })));
    assert_eq!(doc.lock_of("dana_sig")["fields"], json!(["dana_name"]));
    assert_eq!(doc.fill_text("reference", "R-2"), "resolved");
    assert_eq!(doc.fill_text("delivery", "later"), "resolved");
    assert_eq!(doc.fill_text("dana_name", "x"), "ProtectedDocument");
}

#[test]
fn a_signature_fields_own_lock_decides_what_its_signing_locks() {
    // The field already locks every field: a filled one adds nothing, and the signing goes on.
    let mut doc = Doc::open_as("filler", own_lock_pdf());
    doc.fill_text("reference", "PO-42");
    doc.sign("all_sig", None);
    assert_eq!(doc.lock_of("all_sig")["action"], "all");
    assert_eq!(doc.fill_text("reference", "PO-43"), "ProtectedDocument");

    // A narrower lock of its own stays as it is: what it leaves open stays open.
    let mut doc = Doc::open_as("dana", own_lock_pdf());
    doc.fill_text("reference", "PO-42");
    doc.sign("dana_sig", None);
    let lock = doc.lock_of("dana_sig");
    assert_eq!((&lock["action"], &lock["fields"]), (&json!("include"), &json!(["dana_name"])));
    assert_eq!(doc.fill_text("reference", "PO-43"), "resolved");
}

#[test]
fn an_export_carries_the_fact_and_an_import_keeps_it_under_the_right_rights() {
    let mut everything = Doc::open_as("everything", anyone_fields_pdf());
    let everyone =
        everything.ok(json!({ "kind": "forms.export", "selection": {} }))["bundle"].clone();
    let plain =
        everything
            .ok(json!({ "kind": "forms.export", "selection": { "fields": [fqn("sender_note")] } }))
            ["bundle"]
            .clone();
    let marked: serde_json::Map<String, Value> = everyone["fields"]
        .as_array()
        .unwrap()
        .iter()
        .map(|row| {
            (row["data"]["name"].as_str().unwrap().to_owned(), row["data"]["forAnyone"].clone())
        })
        .collect();
    assert_eq!(
        (&marked["reference"], &marked["notes"], &marked["sender_note"]),
        (&json!(true), &json!(true), &json!(false))
    );

    let mut emptied = Doc::open_as("everything", anyone_fields_pdf());
    let refs: Vec<Value> =
        emptied.list()["fields"].as_array().unwrap().iter().map(|f| f["ref"].clone()).collect();
    for reference in refs {
        emptied.ok(json!({ "kind": "document.apply",
            "change": { "ops": [{ "type": "forms.delete", "field": reference }] } }));
    }
    let blank = emptied.download();

    let facts = |result: &Value| -> serde_json::Map<String, Value> {
        result["result"]["fields"]
            .as_array()
            .unwrap()
            .iter()
            .map(|f| (f["name"].as_str().unwrap().to_owned(), f["forAnyone"].clone()))
            .collect()
    };
    let import = |bundle: &Value, attribution: &str| {
        json!({ "kind": "forms.import", "bundle": bundle, "attribution": attribution,
            "values": true, "mayScript": false })
    };

    let mut sender = Doc::open_as("sender", blank.clone());
    let copied = facts(&sender.ok(import(&everyone, "stamp")));
    assert_eq!(
        (&copied["reference"], &copied["delivery"], &copied["sender_note"]),
        (&json!(true), &json!(true), &json!(false))
    );

    let mut designer = Doc::open_as("designer", blank.clone());
    // A field of no group, not for anyone, she may copy in.
    let copied = facts(&designer.ok(import(&plain, "stamp")));
    assert_eq!(copied.values().collect::<Vec<_>>(), [&json!(false)]);
    let refused = designer.call(import(&everyone, "stamp")).unwrap_err();
    assert_eq!(refused["code"], "Forbidden", "{refused}");

    let mut restorer = Doc::open_as("everything", blank);
    let restored = facts(&restorer.ok(import(&everyone, "restore")));
    assert_eq!(
        (&restored["reference"], &restored["notes"], &restored["sender_note"]),
        (&json!(true), &json!(true), &json!(false))
    );
}

#[test]
fn after_a_signature_filling_one_it_left_open_is_permitted_changing_one_it_locked_is_forbidden() {
    let mut dana = Doc::open_as("dana", anyone_fields_pdf());
    dana.fill_text("reference", "R-1");
    dana.sign("dana_sig", None);
    let signed = dana.download();
    let since = json!({ "since": { "signatureIndex": 0 }, "until": "working-copy" });

    // Ada, the next signer, fills in a field for anyone that Dana's signature left open.
    let mut ada = Doc::open_as("ada", signed.clone());
    ada.fill_text("notes", "later");
    let analysis =
        ada.ok(json!({ "kind": "signatures.analyze", "input": since }))["analysis"].clone();
    assert_eq!(analysis["verdict"], "permitted", "{analysis}");

    // Judged, not enforced: the write goes through, and the signature's lock forbids it.
    let mut permitted =
        Doc::open_with("everything", signed, json!({ "signedDocumentPolicy": "permit" }));
    assert_eq!(permitted.fill_text("reference", "R-2"), "resolved");
    let analysis =
        permitted.ok(json!({ "kind": "signatures.analyze", "input": since }))["analysis"].clone();
    assert_eq!(analysis["verdict"], "forbidden", "{analysis}");
    let found = analysis["steps"].as_array().unwrap().iter().any(|step| {
        step["findings"].as_array().unwrap().iter().any(|f| {
            f["rule"] == "field-lock"
                && f["verdict"] == "forbidden"
                && f["detail"].as_str().unwrap_or_default().contains("\"reference\"")
        })
    });
    assert!(found, "{analysis}");
}
