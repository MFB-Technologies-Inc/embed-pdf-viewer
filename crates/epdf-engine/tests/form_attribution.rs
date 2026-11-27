//! `runFormAttributionConformance.ts` as 445e34a69 left it: a fill records
//! nobody, named or not, and neither does a reset, so a signature made before
//! a fill stays permitted, and so does one made before a mark is drawn in and
//! signed. Each session continues the last one's document from its saved
//! bytes, as `form-attribution.test.ts` does with `download()`.
//!
//! The read half ("a widget row reports no attribution") is in
//! `form_reader.rs`.

use epdf_engine::host::Host;
use serde_json::{Value, json};

const FILL: &[&str] = &["doc.open", "doc.render", "doc.download", "doc.forms.fill"];
const SIGNER: &[&str] =
    &["doc.open", "doc.render", "doc.download", "doc.forms.fill", "doc.forms.modify", "doc.sign"];
const EVERYTHING: &[&str] = &["*"];

/// A DER `SEQUENCE`: what a completion takes as a CMS, without judging it.
const PLACEHOLDER_CMS: [u8; 8] = [0x30, 0x06, 0x02, 0x01, 0x01, 0x02, 0x01, 0x02];

fn alice() -> Value {
    json!({ "user_id": "alice", "display_name": "Alice Author", "group_id": "legal" })
}

fn bob() -> Value {
    json!({ "user_id": "bob", "display_name": "Bob Builder" })
}

fn anonymous() -> Value {
    json!({})
}

/// `CHANGE_FIXTURE_PDF`: two pages; the first (3) holds annotations and the
/// text field `name` (9, merged with its widget) holding `Ann`.
fn change_fixture() -> Vec<u8> {
    let objects = [
        "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [9 0 R] /DA (/Helv 0 Tf 0 g) \
         /DR << /Font << /Helv << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> >> >>",
        "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Contents 10 0 R /Resources << >> \
         /Annots [5 0 R 6 0 R 7 0 R 8 0 R 9 0 R] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Contents 10 0 R /Resources << >> >>",
        "<< /Type /Annot /Subtype /Square /Rect [20 200 80 260] /C [0 0 1] /T (Ann) /P 3 0 R >>",
        "<< /Type /Annot /Subtype /Text /Rect [120 220 140 240] /Contents (Note) /T (Ann) \
         /Popup 7 0 R /P 3 0 R >>",
        "<< /Type /Annot /Subtype /Popup /Rect [150 180 250 240] /Parent 6 0 R /P 3 0 R >>",
        "<< /Type /Annot /Subtype /Text /Rect [120 220 140 240] /Contents (Reply) /T (Bea) \
         /IRT 6 0 R /P 3 0 R >>",
        "<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /V (Ann) /Rect [20 20 180 44] \
         /P 3 0 R >>",
        "<< /Length 0 >>\nstream\n\nendstream",
    ];
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

/// `BANDS_PNG`, as the signature-mark vectors record it.
fn bands_png() -> Vec<u8> {
    let path = test_support::corpus::vectors_dir("changes").join("signature-mark.json");
    let vectors: Value = serde_json::from_str(&std::fs::read_to_string(path).unwrap()).unwrap();
    let text = vectors["fixtures"]["png"].as_str().unwrap();
    (0..text.len()).step_by(2).map(|i| u8::from_str_radix(&text[i..i + 2], 16).unwrap()).collect()
}

fn name_ref() -> Value {
    json!({ "kind": "fqn", "name": "name" })
}

/// One session's document, opened from bytes with a scope and an identity.
struct Doc {
    host: Host,
    job: u64,
}

impl Doc {
    fn open(identity: Value, scope: &[&str], bytes: Vec<u8>) -> Self {
        let mut doc = Doc { host: Host::new(), job: 1 };
        let (answer, _) = doc.send(
            json!({ "kind": "open.fatMem", "effect": "open", "scope": scope,
                "identity": identity, "reserveObjectNumbers": 64 }),
            vec![bytes],
        );
        assert!(answer.get("error").is_none(), "{answer}");
        doc
    }

    /// `continueAs`: this document's saved bytes, opened as `identity`.
    fn continue_as(mut self, identity: Value, scope: &[&str]) -> Self {
        let (answer, mut payloads) =
            self.send(json!({ "kind": "document.saveBuffer", "mode": "incremental" }), Vec::new());
        assert!(answer.get("error").is_none(), "{answer}");
        Doc::open(identity, scope, payloads.remove(0))
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
        let response = self.host.call_filled(&request.to_string(), payloads);
        (serde_json::from_str(&response.json).unwrap(), response.payloads)
    }

    fn ok(&mut self, request: Value, payloads: Vec<Vec<u8>>) -> Value {
        let shown = request.to_string();
        let (answer, _) = self.send(request, payloads);
        assert!(answer.get("error").is_none(), "{shown}: {answer}");
        answer["result"].clone()
    }

    fn set_value(&mut self, value: &str) -> Value {
        let op = format!("fill-{}", self.job);
        self.ok(
            json!({ "kind": "forms.setValue", "opId": op, "ref": name_ref(),
                "value": { "value": value } }),
            Vec::new(),
        )["result"]
            .take()
    }

    fn reset(&mut self) -> Value {
        let op = format!("reset-{}", self.job);
        self.ok(json!({ "kind": "forms.reset", "opId": op, "refs": [name_ref()] }), Vec::new())
            ["result"]
            .take()
    }

    fn list(&mut self) -> Value {
        self.ok(json!({ "kind": "forms.list" }), Vec::new())
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

    fn signature_field(&mut self, name: &str, x: u32) {
        self.ok(
            json!({ "kind": "forms.createField", "opId": format!("create-{name}"),
                "draft": { "kind": "signature", "name": name, "widgets": [{
                    "page": { "kind": "objectNumber", "objectNumber": 3 },
                    "box": { "x": x, "y": 100, "width": 120, "height": 40 } }] } }),
            Vec::new(),
        );
    }

    /// Prepare and complete a signing of `name` with a placeholder CMS.
    fn sign(&mut self, name: &str) {
        let (prepared, _) = self.send(
            json!({ "kind": "signatures.prepare",
                "input": { "field": { "kind": "fqn", "name": name } } }),
            Vec::new(),
        );
        assert!(prepared.get("error").is_none(), "{prepared}");
        let result = &prepared["result"]["result"];
        let completed = self.ok(
            json!({ "kind": "signatures.complete", "opId": format!("sign-{name}"),
                "input": { "signingId": result["signingId"],
                    "expectedVersion": result["expectedVersion"] } }),
            vec![PLACEHOLDER_CMS.to_vec()],
        );
        assert_eq!(completed["result"]["status"], "completed", "{completed}");
    }

    /// What changed since field `name`'s signature, up to the working copy.
    fn verdict_since(&mut self, name: &str) -> Value {
        let listed = self.ok(json!({ "kind": "signatures.list" }), Vec::new());
        let index = listed["snapshot"]["signatures"]
            .as_array()
            .unwrap()
            .iter()
            .find(|s| s["fieldName"] == name && s["signed"] == true)
            .unwrap_or_else(|| panic!("'{name}' is not signed: {listed}"))["index"]
            .clone();
        let analysis = self.ok(
            json!({ "kind": "signatures.analyze",
                "input": { "since": { "signatureIndex": index }, "until": "working-copy" } }),
            Vec::new(),
        );
        analysis["analysis"]["verdict"].clone()
    }
}

fn filled(field: &Value) -> [&Value; 3] {
    [&field["filledBy"], &field["filledByName"], &field["filledAt"]]
}

const NOBODY: [&Value; 3] = [&Value::Null, &Value::Null, &Value::Null];

#[test]
fn a_fill_records_nobody_named_or_not_and_neither_does_a_reset() {
    let mut doc = Doc::open(alice(), FILL, change_fixture());
    let answer = doc.set_value("Cleo");
    assert_eq!(answer["field"]["value"], "Cleo", "{answer}");
    assert_eq!(filled(&answer["field"]), NOBODY, "{answer}");

    let mut doc = doc.continue_as(bob(), FILL);
    assert_eq!(doc.field("name")["filledBy"], Value::Null);
    doc.set_value("Bea");
    let reset = doc.reset();
    let cleared = reset["fields"].as_array().unwrap().iter().find(|f| f["name"] == "name").unwrap();
    assert_eq!(filled(cleared), NOBODY, "{reset}");
}

#[test]
fn a_named_fill_after_a_signature_leaves_it_permitted_and_so_does_a_reset() {
    let mut doc = Doc::open(alice(), SIGNER, change_fixture());
    doc.signature_field("approval", 20);
    doc.set_value("Cleo");
    doc.sign("approval");

    let mut doc = doc.continue_as(bob(), FILL);
    doc.set_value("Bea");
    doc.reset();
    doc.set_value("Dee");

    let mut doc = doc.continue_as(anonymous(), EVERYTHING);
    assert_eq!(doc.verdict_since("approval"), "permitted");
}

#[test]
fn a_mark_drawn_in_and_then_signed_leaves_an_earlier_signature_permitted() {
    let mut doc = Doc::open(alice(), SIGNER, change_fixture());
    doc.signature_field("first", 20);
    doc.signature_field("second", 160);
    doc.sign("first");

    let mut doc = doc.continue_as(bob(), SIGNER);
    doc.ok(
        json!({ "kind": "forms.setSignatureAppearance", "opId": "mark",
            "ref": { "kind": "fqn", "name": "second" }, "appearance": { "payload": 0 } }),
        vec![bands_png()],
    );
    doc.sign("second");

    let mut doc = doc.continue_as(anonymous(), EVERYTHING);
    assert_eq!(doc.verdict_since("first"), "permitted");
    let form = doc.list();
    let second = form["fields"].as_array().unwrap().iter().find(|f| f["name"] == "second").unwrap();
    let row = form["widgets"]
        .as_array()
        .unwrap()
        .iter()
        .find(|row| row["ref"] == second["widgets"][0]["ref"])
        .expect("second's row");
    assert_eq!(row["hasMark"], true, "{row}");
}
