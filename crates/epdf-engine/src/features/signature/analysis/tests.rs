//! `packages/engine/core/test/signature/analysis.test.ts` and
//! `resolve.test.ts`: the rule engine on hand-built steps, case for case.
//!
//! The inputs are built as the TypeScript builds them, as JSON (`change`,
//! `signature`, `structure`), and read into the Rust types, so each case
//! reads as its TypeScript does. Results are compared as JSON.

use std::collections::{HashMap, HashSet};

use serde_json::{Value, json};

use super::types::{ObjectChange, ObjectReferrer, RevisionStructure, Sides, StepInput};
use super::{
    DEFAULT_EDGE_RESOLVER_BUDGET, EdgeResolver, EdgeResolverBudget, evaluate_step, parse_pdf_value,
    restrictions_of, stable_stringify,
};
use crate::features::signature::analysis::pdf_value::changed_keys;

// ── parsePdfValue ─────────────────────────────────────────────────────────

#[test]
fn parse_pdf_value_reads_the_forks_shallow_serialisation() {
    let v = parse_pdf_value(
        "<</A 1/B [2 0 R 3 -4.5 true null]/C (hi \\(there\\))/D /Na#20me/E <</F 6 0 R>>>>",
    )
    .unwrap();
    assert_eq!(
        serde_json::to_value(&v).unwrap(),
        json!({ "t": "dict", "entries": {
            "A": { "t": "number", "v": 1 },
            "B": { "t": "array", "items": [
                { "t": "ref", "num": 2 },
                { "t": "number", "v": 3 },
                { "t": "number", "v": -4.5 },
                { "t": "bool", "v": true },
                { "t": "null" },
            ] },
            "C": { "t": "string", "v": "hi (there)" },
            "D": { "t": "name", "v": "Na me" },
            "E": { "t": "dict", "entries": { "F": { "t": "ref", "num": 6 } } },
        } })
    );
    let s = parse_pdf_value("stream(12,abcd)<</Length 12/Type /XObject>>").unwrap();
    assert_eq!(
        serde_json::to_value(&s).unwrap(),
        json!({ "t": "stream", "length": 12, "sha256": "abcd",
            "dict": { "Length": { "t": "number", "v": 12 }, "Type": { "t": "name", "v": "XObject" } } })
    );
    let text = stable_stringify(Some(&v));
    assert_eq!(text, stable_stringify(Some(&parse_pdf_value(&text).unwrap())));
    let mut keys = changed_keys(
        Some(&parse_pdf_value("<</A 1/B 2>>").unwrap()),
        Some(&parse_pdf_value("<</A 1/B 3/C 4>>").unwrap()),
    );
    keys.sort();
    assert_eq!(keys, ["B", "C"]);
}

// ── builders ──────────────────────────────────────────────────────────────

fn edge(parent: u32, label: &str) -> Value {
    json!({ "parent": parent, "label": label })
}

fn edge_via(parent: u32, label: &str, via: &[u32]) -> Value {
    json!({ "parent": parent, "label": label, "via": via })
}

/// `change`: one changed object, its kind read off its raw text, its
/// values parsed from it.
fn change(object_number: u32, old: Option<&str>, new: Option<&str>, usage: Usage) -> Value {
    let kind_of = |s: &str| {
        if s.starts_with("stream(") {
            "stream"
        } else if s.starts_with("<<") {
            "dictionary"
        } else if s.starts_with('[') {
            "array"
        } else {
            "scalar"
        }
    };
    let value = |raw: Option<&str>| {
        raw.map_or(Value::Null, |raw| serde_json::to_value(parse_pdf_value(raw).unwrap()).unwrap())
    };
    let generation = |raw: Option<&str>| {
        if raw.is_none() || object_number == 0 { Value::Null } else { json!(0) }
    };
    json!({
        "objectNumber": object_number,
        "change": if old.is_none() { "added" } else if new.is_none() { "freed" } else { "modified" },
        "kind": if object_number == 0 { "trailer" } else { kind_of(new.or(old).unwrap_or("")) },
        "present": { "old": old.is_some(), "new": new.is_some() },
        "generation": { "old": generation(old), "new": generation(new) },
        "value": { "old": value(old), "new": value(new), "truncated": false },
        "raw": { "old": old, "new": new },
        "streamDataChanged": false,
        "usage": { "old": usage.old, "new": usage.new },
    })
}

/// `{ old?, new? }` usage of a change.
#[derive(Default)]
struct Usage {
    old: Vec<Value>,
    new: Vec<Value>,
}

fn both(edges: &[Value]) -> Usage {
    Usage { old: edges.to_vec(), new: edges.to_vec() }
}

fn old_only(edges: &[Value]) -> Usage {
    Usage { old: edges.to_vec(), new: Vec::new() }
}

fn new_only(edges: &[Value]) -> Usage {
    Usage { old: Vec::new(), new: edges.to_vec() }
}

/// `{ ...change, ...extra }`.
fn with(mut change: Value, extra: Value) -> Value {
    for (k, v) in extra.as_object().unwrap() {
        change[k] = v.clone();
    }
    change
}

/// `signature(index, fieldObjectNumber, extra)`: a signed approval.
fn signature(index: u32, field_object_number: u32, extra: Value) -> Value {
    let mut sig = json!({
        "index": index,
        "field": { "kind": "objectNumber", "objectNumber": field_object_number },
        "fieldName": format!("sig{index}"),
        "widget": null,
        "signed": true,
        "kind": "signature",
        "filter": "Adobe.PPKLite",
        "subFilter": "ETSI.CAdES.detached",
        "byteRange": [0, 100, 200, 100],
        "contentsSizeBytes": 8,
        "coverage": "whole-revision",
        "revisionIndex": 1,
        "signer": { "name": null, "reason": null, "location": null, "contactInfo": null,
            "signedAt": null },
        "docMdp": null,
        "catalogCertification": false,
        "fieldMdp": null,
        "lock": null,
        "seedValue": null,
    });
    for (k, v) in extra.as_object().unwrap() {
        sig[k] = v.clone();
    }
    sig
}

fn approval() -> Value {
    signature(0, 30, json!({}))
}

fn certification(permission: u8) -> Value {
    signature(0, 30, json!({ "docMdp": permission, "catalogCertification": true }))
}

fn unsigned(index: u32, field: u32, extra: Value) -> Value {
    let mut sig = signature(
        index,
        field,
        json!({ "signed": false, "revisionIndex": null, "coverage": null, "byteRange": null }),
    );
    for (k, v) in extra.as_object().unwrap() {
        sig[k] = v.clone();
    }
    sig
}

fn base_fields() -> Value {
    json!([
        { "objectNumber": 20, "name": "name", "kind": "text", "widgets": [21] },
        { "objectNumber": 30, "name": "sig0", "kind": "signature", "widgets": [30] },
    ])
}

/// `structure(overrides)`: catalog 1, pages 2, page 3, AcroForm 10, text
/// field 20 (widget 21), signature field 30 (merged widget).
fn structure(overrides: Value) -> Value {
    let mut s = json!({
        "root": 1,
        "acroForm": 10,
        "pagesRoot": 2,
        "pages": [3],
        "fields": base_fields(),
        "signatures": [approval()],
    });
    for (k, v) in overrides.as_object().unwrap() {
        s[k] = v.clone();
    }
    s
}

fn signed_with(signatures: Value) -> Value {
    structure(json!({ "signatures": signatures }))
}

fn healthy() -> Value {
    json!({ "sparseXref": false, "bareReferences": 0, "referrersComplete": true })
}

/// `evaluateStep({ older, newer, changes, before, after, health })`, as JSON.
fn step(older: u32, newer: u32, changes: &[Value], before: &Value, after: &Value) -> Value {
    step_with(older, newer, changes, before, after, None)
}

fn step_with(
    older: u32,
    newer: u32,
    changes: &[Value],
    before: &Value,
    after: &Value,
    health: Option<(Value, Value)>,
) -> Value {
    step_full(older, newer, changes, &[], before, after, health)
}

/// `evaluateStep` with `unwritten` objects handed over.
fn step_full(
    older: u32,
    newer: u32,
    changes: &[Value],
    unwritten: &[Value],
    before: &Value,
    after: &Value,
    health: Option<(Value, Value)>,
) -> Value {
    let changes: Vec<ObjectChange> =
        changes.iter().map(|c| serde_json::from_value(c.clone()).unwrap()).collect();
    let structure = |v: &Value| -> RevisionStructure { serde_json::from_value(v.clone()).unwrap() };
    let health = health.map(|(old, new)| Sides {
        old: serde_json::from_value(old).unwrap(),
        new: serde_json::from_value(new).unwrap(),
    });
    let analysis = evaluate_step(StepInput {
        older,
        newer,
        changes,
        unwritten: unwritten.iter().map(|u| serde_json::from_value(u.clone()).unwrap()).collect(),
        before: structure(before),
        after: structure(after),
        health,
        restrictions: None,
        level_override: None,
    });
    serde_json::to_value(analysis).unwrap()
}

fn verdict(step: &Value) -> &str {
    step["verdict"].as_str().unwrap()
}

fn findings(step: &Value) -> &Vec<Value> {
    step["findings"].as_array().unwrap()
}

fn rules(step: &Value) -> Vec<&str> {
    findings(step).iter().map(|f| f["rule"].as_str().unwrap()).collect()
}

fn any(step: &Value, test: impl Fn(&Value) -> bool) -> bool {
    findings(step).iter().any(test)
}

fn forbidden(step: &Value) -> Vec<&Value> {
    findings(step).iter().filter(|f| f["verdict"] == "forbidden").collect()
}

fn fill_change() -> Value {
    change(
        20,
        Some("<</FT /Tx/T (name)/V (a)>>"),
        Some("<</FT /Tx/T (name)/V (b)>>"),
        both(&[edge(10, "Fields/[0]")]),
    )
}

// ── evaluateStep: the edge-claim law ──────────────────────────────────────

#[test]
fn restrictions_come_from_the_older_revision() {
    // Nothing signed: nothing forbids. An approval signature: judged the way
    // Acrobat reads it, form fill-in, signing and commenting keep it.
    let of = |v: Value| restrictions_of(&serde_json::from_value(v).unwrap());
    let level = |v: Value| serde_json::to_value(of(v).level).unwrap();
    assert_eq!(level(structure(json!({ "signatures": [] }))), "annotate");
    assert_eq!(level(structure(json!({}))), "annotate");
    assert_eq!(level(signed_with(json!([certification(2)]))), "fill");
    let locked = of(signed_with(json!([signature(
        0,
        30,
        json!({ "fieldMdp": { "action": "include", "fields": ["name"] } })
    )])));
    assert_eq!(locked.locks.len(), 1);
}

#[test]
fn a_failed_read_is_missing_evidence_never_a_value() {
    let before = structure(json!({}));
    // Both sides serialise as "null", but the new side could not be read.
    let unreadable = with(
        change(20, Some("null"), Some("null"), both(&[edge(10, "Fields/[0]")])),
        json!({ "read": { "old": "ok", "new": "failed" } }),
    );
    let s = step(1, 2, &[unreadable], &before, &before);
    assert_eq!(verdict(&s), "indeterminate");
    assert!(!rules(&s).contains(&"identical-rewrite"));
    assert!(any(&s, |f| f["verdict"] == "incomplete"));
    // A real null object rewritten as null, both sides read: an identical
    // rewrite.
    let real_null = with(
        change(20, Some("null"), Some("null"), both(&[edge(10, "Fields/[0]")])),
        json!({ "read": { "old": "ok", "new": "ok" } }),
    );
    assert!(rules(&step(1, 2, &[real_null], &before, &before)).contains(&"identical-rewrite"));
}

#[test]
fn a_proven_violation_decides_even_when_other_evidence_is_incomplete() {
    let before = structure(json!({}));
    let page_content = change(
        3,
        Some("<</Type /Page/Parent 2 0 R/Contents 40 0 R>>"),
        Some("<</Type /Page/Parent 2 0 R/Contents 41 0 R>>"),
        both(&[edge(2, "Kids/[0]")]),
    );
    let too_big = with(
        change(50, Some("<</X 1>>"), Some("<</X 2>>"), both(&[edge(1, "Big")])),
        json!({ "value": { "old": null, "new": null, "truncated": true } }),
    );
    let s = step(1, 2, &[page_content, too_big], &before, &before);
    assert!(any(&s, |f| f["verdict"] == "forbidden"));
    assert!(any(&s, |f| f["verdict"] == "incomplete"));
    assert_eq!(verdict(&s), "forbidden");
}

#[test]
fn a_sealed_revision_acrobat_cannot_verify_is_judged_indeterminate() {
    let before = structure(json!({}));
    let fill = fill_change();
    let run = |old: Value, new: Value| {
        step_with(1, 2, std::slice::from_ref(&fill), &before, &before, Some((old, new)))
    };
    assert_eq!(verdict(&run(healthy(), healthy())), "permitted");
    let mut sparse = healthy();
    sparse["sparseXref"] = json!(true);
    let mut bare = healthy();
    bare["bareReferences"] = json!(1);
    for sick in [sparse.clone(), bare] {
        let s = run(sick, healthy());
        assert_eq!(verdict(&s), "indeterminate");
        let finding = findings(&s).iter().find(|f| f["rule"] == "base-unverifiable").unwrap();
        assert_eq!(finding["verdict"], "incomplete");
        assert_eq!(finding["objectNumber"], 0);
        assert!(finding["detail"].as_str().unwrap().contains("Acrobat"), "{finding}");
    }
    // The newer revision's health is not the sealed revision's problem...
    assert_eq!(verdict(&run(healthy(), sparse)), "permitted");
    // ...but an incomplete reachability walk on either side leaves every
    // use unproven.
    let mut walked_health = healthy();
    walked_health["referrersComplete"] = json!(false);
    let walked = run(healthy(), walked_health);
    assert_eq!(verdict(&walked), "indeterminate");
    assert!(any(&walked, |f| f["rule"] == "unexplained" && f["verdict"] == "incomplete"));
}

#[test]
fn no_changes_is_unchanged_and_under_p1_an_identical_rewrite_is_still_forbidden() {
    let before = signed_with(json!([certification(1)]));
    assert_eq!(verdict(&step(1, 2, &[], &before, &before)), "unchanged");
    let same = change(
        20,
        Some("<</FT /Tx/T (name)/V (x)>>"),
        Some("<</FT /Tx/T (name)/V (x)>>"),
        both(&[edge(10, "Fields/[0]")]),
    );
    let s = step(1, 2, &[same], &before, &before);
    assert_eq!(s["levelInForce"], "lta");
    assert_eq!(verdict(&s), "forbidden");
    assert_eq!(findings(&s)[0]["rule"], "identical-rewrite");
    assert!(
        findings(&s)[0]["detail"].as_str().unwrap().contains("sealed value under level 'lta'"),
        "{s}"
    );
}

fn same_page() -> Value {
    change(
        3,
        Some("<</Type /Page/Parent 2 0 R/Annots [30 0 R]>>"),
        Some("<</Type /Page/Parent 2 0 R/Annots [30 0 R]>>"),
        both(&[edge(2, "Kids/[0]")]),
    )
}

#[test]
fn a_page_rewritten_identically_after_an_approval_signature_is_no_change() {
    let before = structure(json!({}));
    let s = step(1, 2, &[same_page()], &before, &before);
    assert_eq!(s["levelInForce"], "annotate");
    assert_eq!(verdict(&s), "unchanged");
    assert_eq!(findings(&s).len(), 1);
    let only = &findings(&s)[0];
    assert_eq!(
        (&only["rule"], &only["verdict"], &only["objectNumber"]),
        (&json!("identical-rewrite"), &json!("permitted"), &json!(3))
    );
    // Under a P=2 or P=3 certification as well (v3/70, 71).
    for permission in [2, 3] {
        let certified = signed_with(json!([certification(permission)]));
        assert_eq!(verdict(&step(1, 2, &[same_page()], &certified, &certified)), "unchanged");
    }
}

#[test]
fn an_identical_rewrite_before_the_first_signature_is_no_change_either() {
    let unsigned = signed_with(json!([signature(0, 30, json!({ "signed": false }))]));
    let s = step(0, 1, &[same_page()], &unsigned, &unsigned);
    assert_eq!(verdict(&s), "unchanged");
    assert_eq!(findings(&s).len(), 1);
    assert_eq!(findings(&s)[0]["rule"], "identical-rewrite");
    assert_eq!(findings(&s)[0]["verdict"], "permitted");
    assert_eq!(findings(&s)[0]["objectNumber"], 3);
}

#[test]
fn a_form_fill_is_permitted_under_fill_and_annotate_forbidden_under_lta() {
    let fill = [
        fill_change(),
        change(
            21,
            Some("<</AP <</N 22 0 R>>/Subtype /Widget>>"),
            Some("<</AP <</N 40 0 R>>/Subtype /Widget>>"),
            both(&[edge(3, "Annots/[0]"), edge(20, "Kids/[0]")]),
        ),
        change(
            40,
            None,
            Some("stream(20,ff)<</Resources <</Font <</Helv 41 0 R>>>>/Subtype /Form>>"),
            new_only(&[edge(21, "AP/N")]),
        ),
        change(
            41,
            None,
            Some("<</BaseFont /Helvetica/Type /Font>>"),
            new_only(&[edge(40, "Resources/Font/Helv")]),
        ),
        change(22, Some("stream(10,aa)<</Subtype /Form>>"), None, old_only(&[edge(21, "AP/N")])),
        change(
            0,
            Some("<</Root 1 0 R/Size 42>>"),
            Some("<</Prev 1234/Root 1 0 R/Size 43>>"),
            Usage::default(),
        ),
    ];
    let approval = structure(json!({}));
    let permitted = step(1, 2, &fill, &approval, &approval);
    assert_eq!(verdict(&permitted), "permitted");
    assert!(forbidden(&permitted).is_empty());
    let certified_fill = signed_with(json!([certification(2)]));
    assert_eq!(verdict(&step(1, 2, &fill, &certified_fill, &certified_fill)), "permitted");
    let lta = signed_with(json!([certification(1)]));
    let refused = step(1, 2, &fill, &lta, &lta);
    assert_eq!(verdict(&refused), "forbidden");
    // One cause, named once per object: nothing may follow such a
    // certification (policy 5, 462380c7a).
    let mut numbers: Vec<u64> =
        forbidden(&refused).iter().map(|f| f["objectNumber"].as_u64().unwrap()).collect();
    numbers.sort_unstable();
    assert_eq!(numbers, [20, 21, 22, 40, 41]);
    assert!(forbidden(&refused).iter().all(|f| f["rule"] == "no-changes-certification"));
}

#[test]
fn a_shared_resource_keeps_its_other_reference_unexplained() {
    let fill = [
        change(
            21,
            Some("<</AP <</N 22 0 R>>/Subtype /Widget>>"),
            Some("<</AP <</N 40 0 R>>/Subtype /Widget>>"),
            both(&[edge(3, "Annots/[0]")]),
        ),
        change(
            40,
            None,
            Some("stream(20,ff)<</Resources <</Font <</Helv 41 0 R>>>>>>"),
            new_only(&[edge(21, "AP/N")]),
        ),
        // The font is also referenced by the page content resources: not a
        // fill.
        change(
            41,
            None,
            Some("<</Type /Font>>"),
            new_only(&[edge(40, "Resources/Font/Helv"), edge(3, "Resources/Font/Helv")]),
        ),
    ];
    let s = structure(json!({}));
    let analysed = step(1, 2, &fill, &s, &s);
    assert_eq!(verdict(&analysed), "forbidden");
    let unexplained = findings(&analysed)
        .iter()
        .find(|f| f["rule"] == "unexplained" && f["objectNumber"] == 41)
        .unwrap();
    assert_eq!(unexplained["edge"], "3:Resources/Font/Helv");
}

#[test]
fn a_page_content_change_a_catalog_change_and_a_deleted_page_are_forbidden() {
    let s = structure(json!({}));
    let content = [with(
        change(
            4,
            Some("stream(10,aa)<</Length 10>>"),
            Some("stream(12,bb)<</Length 12>>"),
            both(&[edge(3, "Contents")]),
        ),
        json!({ "streamDataChanged": true }),
    )];
    assert_eq!(verdict(&step(1, 2, &content, &s, &s)), "forbidden");
    let catalog = [change(
        1,
        Some("<</Pages 2 0 R/Type /Catalog>>"),
        Some("<</Pages 2 0 R/Type /Catalog/OpenAction 9 0 R>>"),
        both(&[edge(0, "Root")]),
    )];
    let cat = step(1, 2, &catalog, &s, &s);
    assert_eq!(verdict(&cat), "forbidden");
    assert_eq!(findings(&cat)[0]["rule"], "catalog-housekeeping");
    let deleted = [
        change(
            2,
            Some("<</Count 2/Kids [3 0 R 5 0 R]/Type /Pages>>"),
            Some("<</Count 1/Kids [3 0 R]/Type /Pages>>"),
            both(&[edge(1, "Pages")]),
        ),
        change(5, Some("<</Parent 2 0 R/Type /Page>>"), None, old_only(&[edge(2, "Kids/[1]")])),
    ];
    let before = structure(json!({ "pages": [3, 5] }));
    assert_eq!(verdict(&step(1, 2, &deleted, &before, &s)), "forbidden");
}

#[test]
fn metadata_info_trailer_housekeeping_and_dss_permitted_after_p2_forbidden_after_p1() {
    let fill = signed_with(json!([certification(2)]));
    let lta = signed_with(json!([certification(1)]));
    let changes = [
        change(
            1,
            Some("<</Metadata 7 0 R/Pages 2 0 R/Type /Catalog>>"),
            Some("<</DSS 50 0 R/Metadata 8 0 R/Pages 2 0 R/Type /Catalog>>"),
            both(&[edge(0, "Root")]),
        ),
        change(
            8,
            None,
            Some("stream(100,cc)<</Type /Metadata>>"),
            new_only(&[edge(1, "Metadata")]),
        ),
        change(7, Some("stream(90,dd)<</Type /Metadata>>"), None, old_only(&[edge(1, "Metadata")])),
        change(
            6,
            Some("<</Producer (a)>>"),
            Some("<</ModDate (D:2026)/Producer (a)>>"),
            both(&[edge(0, "Info")]),
        ),
        change(50, None, Some("<</Certs [51 0 R]>>"), new_only(&[edge(1, "DSS")])),
        change(51, None, Some("stream(400,ee)<</Length 400>>"), new_only(&[edge(50, "Certs/[0]")])),
        change(
            0,
            Some("<</Info 6 0 R/Root 1 0 R/Size 60>>"),
            Some("<</Info 6 0 R/Prev 999/Root 1 0 R/Size 61>>"),
            Usage::default(),
        ),
    ];
    let s = step(1, 2, &changes, &fill, &fill);
    assert!(forbidden(&s).is_empty(), "{s}");
    assert_eq!(verdict(&s), "permitted");

    // Acrobat reports a no-changes certification as altered for each of
    // these alone, so none is housekeeping there. The trailer is the
    // revision's own bookkeeping and stays so.
    let strict = step(1, 2, &changes, &lta, &lta);
    assert_eq!(verdict(&strict), "forbidden");
    let mut numbers: Vec<u64> =
        forbidden(&strict).iter().map(|f| f["objectNumber"].as_u64().unwrap()).collect();
    numbers.sort_unstable();
    assert_eq!(numbers, [1, 6, 7, 8, 50, 51]);
    assert!(forbidden(&strict).iter().all(|f| f["rule"] == "no-changes-certification"));
    let trailer = findings(&strict).iter().find(|f| f["objectNumber"] == 0).unwrap();
    assert_eq!((&trailer["rule"], &trailer["verdict"]), (&json!("trailer"), &json!("permitted")));
}

fn with_sig1() -> Value {
    let mut fields = base_fields();
    fields
        .as_array_mut()
        .unwrap()
        .push(json!({ "objectNumber": 60, "name": "sig1", "kind": "signature", "widgets": [60] }));
    fields
}

fn signature_value(object_number: u32, contents: &str) -> Value {
    change(object_number, None, Some(contents), new_only(&[edge(60, "V")]))
}

#[test]
fn a_second_signature_with_a_field_mdp_lock_is_permitted_and_touching_the_locked_field_is_not() {
    let before = structure(json!({ "fields": with_sig1(),
        "signatures": [approval(), unsigned(1, 60, json!({ "fieldName": "sig1" }))] }));
    let after = structure(json!({ "fields": with_sig1(), "signatures": [approval(),
        signature(1, 60, json!({ "fieldName": "sig1", "revisionIndex": 2,
            "fieldMdp": { "action": "include", "fields": ["name"] } }))] }));
    let signing = [
        change(
            60,
            Some("<</FT /Sig/Subtype /Widget/T (sig1)>>"),
            Some("<</AP <</N 62 0 R>>/FT /Sig/Subtype /Widget/T (sig1)/V 61 0 R>>"),
            both(&[edge(10, "Fields/[2]"), edge(3, "Annots/[2]")]),
        ),
        signature_value(
            61,
            "<</ByteRange [0 1 2 3]/Contents (x)/Filter /Adobe.PPKLite/Type /Sig>>",
        ),
        change(62, None, Some("stream(5,ab)<</Subtype /Form>>"), new_only(&[edge(60, "AP/N")])),
        // The lock sets ReadOnly on the covered field.
        change(
            20,
            Some("<</FT /Tx/T (name)/V (a)>>"),
            Some("<</FT /Tx/Ff 1/T (name)/V (a)>>"),
            both(&[edge(10, "Fields/[0]")]),
        ),
        change(
            10,
            Some("<</Fields [20 0 R 30 0 R 60 0 R]>>"),
            Some("<</Fields [20 0 R 30 0 R 60 0 R]/SigFlags 3>>"),
            both(&[edge(1, "AcroForm")]),
        ),
    ];
    let s = step(1, 2, &signing, &before, &after);
    assert!(forbidden(&s).is_empty(), "{s}");
    assert_eq!(verdict(&s), "permitted");
    // Next step: the locked field changes, forbidden by the lock whatever
    // the level.
    let later = [change(
        20,
        Some("<</FT /Tx/Ff 1/T (name)/V (a)>>"),
        Some("<</FT /Tx/Ff 1/T (name)/V (b)>>"),
        both(&[edge(10, "Fields/[0]")]),
    )];
    let locked = step(2, 3, &later, &after, &after);
    assert_eq!(verdict(&locked), "forbidden");
    let on_field = findings(&locked).iter().find(|f| f["objectNumber"] == 20).unwrap();
    assert_eq!(on_field["rule"], "field-lock");
    assert_eq!(locked["locks"].as_array().unwrap().len(), 1);
}

#[test]
fn a_lock_added_to_an_existing_signature_field_while_signing_it_depends_on_the_level() {
    let sig1_unsigned = unsigned(1, 60, json!({ "fieldName": "sig1" }));
    let sig1_signed = signature(
        1,
        60,
        json!({ "fieldName": "sig1", "revisionIndex": 2,
            "fieldMdp": { "action": "include", "fields": ["name"] },
            "lock": { "action": "include", "fields": ["name"] } }),
    );
    let before =
        structure(json!({ "fields": with_sig1(), "signatures": [approval(), sig1_unsigned] }));
    let after =
        structure(json!({ "fields": with_sig1(), "signatures": [approval(), sig1_signed] }));
    let signing = [
        change(
            60,
            Some("<</FT /Sig/Subtype /Widget/T (sig1)>>"),
            Some("<</FT /Sig/Lock 63 0 R/Subtype /Widget/T (sig1)/V 61 0 R>>"),
            both(&[edge(10, "Fields/[2]"), edge(3, "Annots/[2]")]),
        ),
        signature_value(
            61,
            "<</ByteRange [0 1 2 3]/Contents (x)/Filter /Adobe.PPKLite/Type /Sig>>",
        ),
        change(
            63,
            None,
            Some("<</Action /Include/Fields [(name)]/Type /SigFieldLock>>"),
            new_only(&[edge(60, "Lock")]),
        ),
    ];
    let s = step(1, 2, &signing, &before, &after);
    assert_eq!(verdict(&s), "permitted", "{s}");
    assert!(any(&s, |f| f["objectNumber"] == 63 && f["verdict"] == "permitted"));

    // The same signing under a P=2 certification: the lock is not form
    // fill-in.
    let certified_before = structure(json!({ "fields": with_sig1(),
        "signatures": [certification(2), before["signatures"][1]] }));
    let certified_after = structure(json!({ "fields": with_sig1(),
        "signatures": [certification(2), after["signatures"][1]] }));
    let certified = step(1, 2, &signing, &certified_before, &certified_after);
    assert_eq!(verdict(&certified), "forbidden");
    let on_widget = findings(&certified)
        .iter()
        .find(|f| f["objectNumber"] == 60 && f["verdict"] == "forbidden")
        .unwrap();
    assert!(on_widget["detail"].as_str().unwrap().contains("Lock"), "{on_widget}");
    // One cause, not a cascade: the /V and /Lock edges are still claimed.
    assert_eq!(forbidden(&certified).len(), 1, "{certified}");
}

#[test]
fn a_certification_after_an_existing_signature_or_perms_out_of_place_is_forbidden() {
    let before = structure(
        json!({ "fields": with_sig1(), "signatures": [approval(), unsigned(1, 60, json!({}))] }),
    );
    let after = structure(json!({ "fields": with_sig1(), "signatures": [approval(),
        signature(1, 60, json!({ "docMdp": 2, "catalogCertification": true, "revisionIndex": 2 }))] }));
    let changes = [
        change(
            60,
            Some("<</FT /Sig/T (sig1)>>"),
            Some("<</FT /Sig/T (sig1)/V 61 0 R>>"),
            both(&[edge(10, "Fields/[2]")]),
        ),
        change(61, None, Some("<</Type /Sig>>"), new_only(&[edge(60, "V"), edge(70, "DocMDP")])),
        change(70, None, Some("<</DocMDP 61 0 R>>"), new_only(&[edge(1, "Perms")])),
        change(
            1,
            Some("<</Pages 2 0 R/Type /Catalog>>"),
            Some("<</Pages 2 0 R/Perms 70 0 R/Type /Catalog>>"),
            both(&[edge(0, "Root")]),
        ),
    ];
    let s = step(1, 2, &changes, &before, &after);
    assert_eq!(verdict(&s), "forbidden");
    assert!(any(&s, |f| f["rule"] == "signature-added"
        && f["detail"].as_str().is_some_and(|d| d.contains("certification"))));
}

#[test]
fn a_truncated_value_makes_the_step_indeterminate_never_permitted() {
    let s = structure(json!({}));
    let mut c = change(20, Some("<</V (a)>>"), Some("<</V (b)>>"), both(&[edge(10, "Fields/[0]")]));
    c["value"]["truncated"] = json!(true);
    assert_eq!(verdict(&step(1, 2, &[c], &s, &s)), "indeterminate");
}

#[test]
fn annotations_are_permitted_at_annotate_forbidden_at_fill_and_widgets_are_never_annotations() {
    let changes = [
        change(
            3,
            Some("<</Annots [21 0 R]/Type /Page>>"),
            Some("<</Annots [21 0 R 80 0 R]/Type /Page>>"),
            both(&[edge(2, "Kids/[0]")]),
        ),
        change(
            80,
            None,
            Some("<</AP <</N 81 0 R>>/Rect [0 0 1 1]/Subtype /Square/Type /Annot>>"),
            new_only(&[edge(3, "Annots/[1]")]),
        ),
        change(81, None, Some("stream(3,cd)<</Subtype /Form>>"), new_only(&[edge(80, "AP/N")])),
    ];
    // A P=3 certification declared annotations allowed.
    let annotate = signed_with(json!([certification(3)]));
    assert_eq!(verdict(&step(1, 2, &changes, &annotate, &annotate)), "permitted");
    // An approval signature is judged at the baseline (annotate).
    let approval = structure(json!({}));
    assert_eq!(verdict(&step(1, 2, &changes, &approval, &approval)), "permitted");
    // A P=2 certification declared only form fill-in.
    let certified = signed_with(json!([certification(2)]));
    assert_eq!(verdict(&step(1, 2, &changes, &certified, &certified)), "forbidden");
    // Removing a widget through /Annots is a form structure change, not an
    // annotation edit.
    let remove_widget = [change(
        3,
        Some("<</Annots [21 0 R]/Type /Page>>"),
        Some("<</Annots []/Type /Page>>"),
        both(&[edge(2, "Kids/[0]")]),
    )];
    assert_eq!(verdict(&step(1, 2, &remove_widget, &approval, &approval)), "forbidden");
}

// ── evaluateStep: hardening ───────────────────────────────────────────────

#[test]
fn a_present_side_whose_value_is_missing_is_incomplete() {
    let lta = signed_with(json!([certification(1)]));
    let mut c = change(
        1,
        Some("<</Pages 2 0 R/Type /Catalog>>"),
        Some("<</Pages 2 0 R/Type /Catalog>>"),
        both(&[edge(0, "Root")]),
    );
    c["value"] = json!({ "old": null, "new": null, "truncated": false });
    let s = step(1, 2, &[c], &lta, &lta);
    assert_eq!(verdict(&s), "indeterminate");
    assert!(any(&s, |f| f["objectNumber"] == 1 && f["verdict"] == "incomplete"));
}

#[test]
fn the_trailer_is_covered_by_the_evidence_check() {
    let fill = signed_with(json!([certification(2)]));
    let mut c =
        change(0, Some("<</Root 1 0 R/Size 5>>"), Some("<</Root 1 0 R/Size 5>>"), Usage::default());
    c["value"] = json!({
        "old": serde_json::to_value(parse_pdf_value("<</Root 1 0 R/Size 5>>").unwrap()).unwrap(),
        "new": null, "truncated": false });
    assert_eq!(verdict(&step(1, 2, &[c], &fill, &fill)), "indeterminate");
}

#[test]
fn unresolvable_references_are_incomplete_never_permitted() {
    let fill = signed_with(json!([certification(2)]));
    let c = with(
        change(
            9,
            Some("<</JS (a)/S /JavaScript>>"),
            Some("<</JS (b)/S /JavaScript>>"),
            Usage::default(),
        ),
        json!({ "usageIncomplete": true }),
    );
    assert_eq!(verdict(&step(1, 2, &[c], &fill, &fill)), "indeterminate");
}

#[test]
fn a_dss_update_cannot_vouch_for_an_unrelated_catalog_edge() {
    // After a certification that allows form filling: the level at which a
    // DSS update is allowed (policy 5).
    let lta = signed_with(json!([certification(2)]));
    let changes = [
        change(
            9,
            Some("<</JS (a)/S /JavaScript>>"),
            Some("<</JS (b)/S /JavaScript>>"),
            both(&[edge(1, "OpenAction")]),
        ),
        change(50, Some("<</Certs []>>"), Some("<</Certs []/VRI <<>>>>"), both(&[edge(1, "DSS")])),
    ];
    let s = step(1, 2, &changes, &lta, &lta);
    assert_eq!(verdict(&s), "forbidden");
    assert!(any(&s, |f| f["objectNumber"] == 9 && f["verdict"] == "forbidden"));
    assert!(any(&s, |f| f["objectNumber"] == 50 && f["verdict"] == "permitted"));
}

#[test]
fn an_appearance_stream_joined_through_a_fill_cannot_vouch_for_a_widget_action() {
    let fill = signed_with(json!([certification(2)]));
    let changes = [
        fill_change(),
        change(
            23,
            Some("stream(3,aa)<</Length 3>>"),
            Some("stream(9,bb)<</Length 9>>"),
            both(&[edge(20, "AP/N")]),
        ),
        change(
            24,
            Some("<</JS (a)/S /JavaScript>>"),
            Some("<</JS (b)/S /JavaScript>>"),
            both(&[edge(20, "A")]),
        ),
    ];
    let s = step(1, 2, &changes, &fill, &fill);
    assert_eq!(verdict(&s), "forbidden");
    assert!(any(&s, |f| f["objectNumber"] == 24 && f["verdict"] == "forbidden"));
    assert!(any(&s, |f| f["objectNumber"] == 20 && f["verdict"] == "permitted"));
}

#[test]
fn a_field_added_and_signed_in_one_step_is_permitted_its_v_and_indirect_lock_claimed() {
    let fill = signed_with(json!([certification(2)]));
    let after = structure(json!({ "fields": with_sig1(), "signatures": [certification(2),
        signature(1, 60, json!({ "fieldName": "sig1", "revisionIndex": 2,
            "fieldMdp": { "action": "include", "fields": ["name"] },
            "lock": { "action": "include", "fields": ["name"] } }))] }));
    let changes = [
        change(
            3,
            Some("<</Annots [21 0 R 30 0 R]/Type /Page>>"),
            Some("<</Annots [21 0 R 30 0 R 60 0 R]/Type /Page>>"),
            both(&[edge(2, "Kids/[0]")]),
        ),
        change(
            10,
            Some("<</Fields [20 0 R 30 0 R]>>"),
            Some("<</Fields [20 0 R 30 0 R 60 0 R]/SigFlags 3>>"),
            both(&[edge(1, "AcroForm")]),
        ),
        change(
            60,
            None,
            Some(
                "<</FT /Sig/Lock 63 0 R/P 3 0 R/Rect [0 0 1 1]/Subtype /Widget/T (sig1)/Type /Annot/V 61 0 R>>",
            ),
            new_only(&[edge(3, "Annots/[2]"), edge(10, "Fields/[2]")]),
        ),
        signature_value(61, "<</ByteRange [0 1 2 3]/Contents (x)/Type /Sig>>"),
        change(
            63,
            None,
            Some("<</Action /Include/Fields [(name)]/Type /SigFieldLock>>"),
            new_only(&[edge(60, "Lock")]),
        ),
    ];
    let s = step(1, 2, &changes, &fill, &after);
    let not_permitted: Vec<&Value> =
        findings(&s).iter().filter(|f| f["verdict"] != "permitted").collect();
    assert!(not_permitted.is_empty(), "{s}");
    assert_eq!(verdict(&s), "permitted");
}

#[test]
fn a_lock_landing_on_a_terminal_without_its_own_ff_is_judged_against_the_effective_flags() {
    let fields = json!([
        { "objectNumber": 20, "name": "name", "kind": "text", "widgets": [21], "flags": 4096 },
        { "objectNumber": 30, "name": "sig0", "kind": "signature", "widgets": [30] },
    ]);
    let before = structure(json!({ "fields": fields, "signatures": [] }));
    let after = structure(json!({ "fields": fields, "signatures": [signature(0, 30, json!({
        "fieldMdp": { "action": "include", "fields": ["name"] },
        "lock": { "action": "include", "fields": ["name"] } }))] }));
    let changes = [
        change(
            20,
            Some("<</FT /Tx/T (name)/V (x)>>"),
            Some("<</FT /Tx/Ff 4097/T (name)/V (x)>>"),
            both(&[edge(10, "Fields/[0]")]),
        ),
        change(
            30,
            Some("<</FT /Sig/Subtype /Widget/T (sig0)>>"),
            Some("<</FT /Sig/Lock 32 0 R/Subtype /Widget/T (sig0)/V 31 0 R>>"),
            both(&[edge(10, "Fields/[1]"), edge(3, "Annots/[1]")]),
        ),
        change(
            31,
            None,
            Some("<</ByteRange [0 1 2 3]/Contents (x)/Type /Sig>>"),
            new_only(&[edge(30, "V")]),
        ),
        change(
            32,
            None,
            Some("<</Action /Include/Fields [(name)]/Type /SigFieldLock>>"),
            new_only(&[edge(30, "Lock")]),
        ),
    ];
    let s = step(0, 1, &changes, &before, &after);
    let not_permitted: Vec<&Value> =
        findings(&s).iter().filter(|f| f["verdict"] != "permitted").collect();
    assert!(not_permitted.is_empty(), "{s}");
    assert_eq!(verdict(&s), "permitted");
}

// ── evaluateStep: appearance regeneration ─────────────────────────────────

#[test]
fn an_appearance_stream_regenerated_under_an_untouched_field_is_form_filling_at_p2() {
    let fill = signed_with(json!([certification(2)]));
    let lta = signed_with(json!([certification(1)]));
    let changes = [change(
        23,
        Some("stream(3,aa)<</Length 3>>"),
        Some("stream(9,bb)<</Length 9>>"),
        both(&[edge_via(21, "AP/N", &[22])]),
    )];
    let filled = step(1, 2, &changes, &fill, &fill);
    assert_eq!(verdict(&filled), "permitted");
    let on_stream = findings(&filled).iter().find(|f| f["objectNumber"] == 23).unwrap();
    assert_eq!(on_stream["rule"], "form-fill");
    assert_eq!(verdict(&step(1, 2, &changes, &lta, &lta)), "forbidden");
}

#[test]
fn an_appearance_stream_shared_between_two_fields_is_nobodys_regeneration() {
    let mut fields = base_fields();
    fields
        .as_array_mut()
        .unwrap()
        .push(json!({ "objectNumber": 40, "name": "other", "kind": "text", "widgets": [40] }));
    let two = structure(json!({ "fields": fields, "signatures": [certification(2)] }));
    let changes = [
        fill_change(),
        change(
            23,
            Some("stream(3,aa)<</Length 3>>"),
            Some("stream(9,bb)<</Length 9>>"),
            both(&[edge_via(21, "AP/N", &[22]), edge_via(40, "AP/N", &[22])]),
        ),
    ];
    let s = step(1, 2, &changes, &two, &two);
    assert_eq!(verdict(&s), "forbidden");
    assert!(any(&s, |f| f["objectNumber"] == 23
        && f["verdict"] == "forbidden"
        && f["edge"] == "40:AP/N"));
}

// ── evaluateStep: a new appearance on a signature field that is not signed (3f54d7016) ──

/// `withOpenField`: `structure` with a second signature field, `open` (31), that nobody signed.
fn with_open_field(first: Value, widgets: &[u32]) -> Value {
    let mut fields = base_fields().as_array().unwrap().clone();
    fields.push(json!({ "objectNumber": 31, "name": "open", "kind": "signature",
        "widgets": widgets }));
    structure(json!({
        "fields": fields,
        "signatures": [signature(0, 30, first), unsigned(1, 31, json!({ "fieldName": "open" }))],
    }))
}

fn listed() -> Vec<Value> {
    vec![edge(10, "Fields/[2]"), edge(3, "Annots/[2]")]
}

/// `redrawn`: object `n` written with `/AP` pointing at 40 instead of 32, and `extra` keys.
fn redrawn(n: u32, name: &str, uses: &[Value], extra: &str) -> Value {
    change(
        n,
        Some(&format!("<</AP <</N 32 0 R>>/FT /Sig/T ({name})>>")),
        Some(&format!("<</AP <</N 40 0 R>>{extra}/FT /Sig/T ({name})>>")),
        both(uses),
    )
}

/// The wrapper EmbedPDF draws a picture with: a form, the form it places, and the image.
fn wrapper(owner: u32) -> Vec<Value> {
    vec![
        change(
            40,
            None,
            Some("stream(41,aa)<</Resources <</XObject <</EPDFWRAP 41 0 R>>>>>>"),
            new_only(&[edge(owner, "AP/N")]),
        ),
        change(
            41,
            None,
            Some("stream(58,bb)<</Resources <</XObject <</FXX1 42 0 R>>>>>>"),
            new_only(&[edge(40, "Resources/XObject/EPDFWRAP")]),
        ),
        change(
            42,
            None,
            Some("stream(26,cc)<</Subtype /Image/Width 30/Height 10>>"),
            new_only(&[edge(41, "Resources/XObject/FXX1")]),
        ),
    ]
}

fn open_redrawn(extra: &str) -> Vec<Value> {
    let mut changes = vec![redrawn(31, "open", &listed(), extra)];
    changes.extend(wrapper(31));
    changes
}

fn judge_at(changes: &[Value], at: &Value) -> Value {
    step(1, 2, changes, at, at)
}

fn rule_of(step: &Value, object_number: u32) -> Option<&str> {
    findings(step)
        .iter()
        .find(|f| f["objectNumber"] == object_number)
        .map(|f| f["rule"].as_str().unwrap())
}

#[test]
fn after_an_approval_a_new_appearance_on_an_unsigned_signature_field_is_permitted() {
    // Corpus v4/93, 94, 97: the field changes /AP alone, and all under it is new.
    let s = judge_at(&open_redrawn(""), &with_open_field(json!({}), &[31]));
    assert_eq!(verdict(&s), "permitted");
    assert!(forbidden(&s).is_empty());
    assert_eq!(rule_of(&s, 31), Some("signature-appearance"));
}

#[test]
fn a_widget_of_its_own_changes_ap_and_the_field_nothing() {
    // Corpus v4/99.
    let separate = with_open_field(json!({}), &[33]);
    let mut changes = vec![redrawn(33, "open", &[edge(31, "Kids/[0]"), edge(3, "Annots/[2]")], "")];
    changes.extend(wrapper(33));
    let s = judge_at(&changes, &separate);
    assert_eq!(verdict(&s), "permitted");
    assert_eq!(rule_of(&s, 33), Some("signature-appearance"));
}

#[test]
fn the_old_appearance_may_go_and_an_object_there_before_may_not_join() {
    let approval = with_open_field(json!({}), &[31]);
    let freed =
        change(32, Some("stream(9,dd)<</Subtype /Form>>"), None, old_only(&[edge(31, "AP/N")]));
    let mut changes = open_redrawn("");
    changes.push(freed);
    assert_eq!(verdict(&judge_at(&changes, &approval)), "permitted");
    // A font that was in the file, changed and drawn by the new appearance: not part of it.
    let font = change(
        50,
        Some("<</BaseFont /Helvetica/Type /Font>>"),
        Some("<</BaseFont /Courier/Type /Font>>"),
        Usage { old: vec![edge(10, "DR/Font/Helv")], new: vec![edge(40, "Resources/Font/F1")] },
    );
    let mut changes = open_redrawn("");
    changes.push(font);
    let s = judge_at(&changes, &approval);
    assert_eq!(verdict(&s), "forbidden");
    assert!(forbidden(&s).iter().any(|f| f["objectNumber"] == 50 && f["rule"] == "unexplained"));
}

#[test]
fn under_a_certification_that_allows_form_filling_it_is_permitted_too() {
    // Corpus v4/101, 102.
    let certified = with_open_field(json!({ "docMdp": 2, "catalogCertification": true }), &[31]);
    let s = judge_at(&open_redrawn(""), &certified);
    assert_eq!(s["levelInForce"], "fill");
    assert_eq!(verdict(&s), "permitted");
    // After one that allows no changes, nothing is permitted.
    let none = with_open_field(json!({ "docMdp": 1, "catalogCertification": true }), &[31]);
    assert_eq!(verdict(&judge_at(&open_redrawn(""), &none)), "forbidden");
}

#[test]
fn the_signed_fields_own_appearance_stays_forbidden() {
    // Corpus v4/95: Acrobat accepts it, the analysis does not.
    let mut changes = vec![redrawn(30, "sig0", &[edge(10, "Fields/[1]")], "")];
    changes.extend(wrapper(30));
    let s = judge_at(&changes, &with_open_field(json!({}), &[31]));
    assert_eq!(verdict(&s), "forbidden");
    assert!(!rules(&s).contains(&"signature-appearance"));
}

#[test]
fn a_key_beyond_ap_is_named_as_the_cause() {
    // Acrobat: "Form Fields with Property Changes".
    let s = judge_at(
        &open_redrawn("/EMBD_Metadata <</AppearanceFit (contain)>>"),
        &with_open_field(json!({}), &[31]),
    );
    assert_eq!(verdict(&s), "forbidden");
    let causes: Vec<&Value> =
        forbidden(&s).into_iter().filter(|f| f["objectNumber"] == 31).collect();
    // One cause for the field: its references are explained by it.
    assert_eq!(causes.len(), 1);
    assert_eq!(causes[0]["rule"], "signature-appearance");
    assert!(causes[0]["detail"].as_str().unwrap().contains("keys changed: EMBD_Metadata"));
}

#[test]
fn a_field_signed_in_the_same_step_is_the_signing_rules_and_a_lock_condemns_before_it() {
    let approval = with_open_field(json!({}), &[31]);
    let signed_now = structure(json!({
        "fields": approval["fields"].clone(),
        "signatures": [
            signature(0, 30, json!({})),
            signature(1, 31, json!({ "fieldName": "open", "revisionIndex": 2 })),
        ],
    }));
    let s = step(1, 2, &open_redrawn(""), &approval, &signed_now);
    assert!(!rules(&s).contains(&"signature-appearance"));
    let locked =
        with_open_field(json!({ "fieldMdp": { "action": "include", "fields": ["open"] } }), &[31]);
    let s = judge_at(&open_redrawn(""), &locked);
    assert_eq!(verdict(&s), "forbidden");
    assert_eq!(
        forbidden(&s).iter().find(|f| f["objectNumber"] == 31).map(|f| f["rule"].clone()),
        Some(json!("field-lock"))
    );
}

// ── EdgeResolver (`resolve.test.ts`) ──────────────────────────────────────

type Index = HashMap<u32, Vec<ObjectReferrer>>;

fn referrer(parent: u32, label: &str) -> ObjectReferrer {
    ObjectReferrer { parent, label: label.to_owned(), via: None }
}

fn reader(
    index: Index,
) -> (impl FnMut(u32) -> Vec<ObjectReferrer>, std::rc::Rc<std::cell::Cell<usize>>) {
    let reads = std::rc::Rc::new(std::cell::Cell::new(0));
    let counter = reads.clone();
    let read = move |n: u32| {
        counter.set(counter.get() + 1);
        index.get(&n).cloned().unwrap_or_default()
    };
    (read, reads)
}

fn anchors(of: &[u32]) -> HashSet<u32> {
    of.iter().copied().collect()
}

fn resolved(parent: u32, label: &str, via: &[u32]) -> ObjectReferrer {
    ObjectReferrer { parent, label: label.to_owned(), via: Some(via.to_vec()) }
}

fn budget(max_depth: usize, max_edges_per_object: usize, max_reads: usize) -> EdgeResolverBudget {
    EdgeResolverBudget { max_depth, max_edges_per_object, max_reads }
}

#[test]
fn an_edge_from_an_unchanged_object_is_resolved_with_joined_labels() {
    // widget 20 --AP--> apDict 22 --N--> stream 23 (changed). Anchors: {20}.
    let (read, _) =
        reader(Index::from([(23, vec![referrer(22, "N")]), (22, vec![referrer(20, "AP")])]));
    let mut r = EdgeResolver::new(read, anchors(&[20, 23]));
    assert_eq!(r.resolve(23), Some(vec![resolved(20, "AP/N", &[22])]));
}

#[test]
fn a_shared_unchanged_dictionary_yields_one_anchored_edge_per_anchor() {
    let (read, _) = reader(Index::from([
        (23, vec![referrer(22, "N")]),
        (22, vec![referrer(20, "AP"), referrer(40, "AP")]),
    ]));
    let mut r = EdgeResolver::new(read, anchors(&[20, 40, 23]));
    assert_eq!(r.resolve(23), Some(vec![resolved(20, "AP/N", &[22]), resolved(40, "AP/N", &[22])]));
}

/// Layers of two unchanged nodes, each referenced by both nodes of the
/// layer above; the top, 1000, is an anchor, and 3000 the leaf.
fn lattice(depth: u32) -> Index {
    let mut index = Index::new();
    let mut ids = vec![1000];
    for d in 0..depth {
        let next = vec![2000 + d * 2, 2001 + d * 2];
        for n in &next {
            index.insert(*n, ids.iter().map(|p| referrer(*p, &format!("k{d}"))).collect());
        }
        ids = next;
    }
    index.insert(3000, ids.iter().map(|p| referrer(*p, "leaf")).collect());
    index
}

#[test]
fn a_diamond_lattice_costs_its_size_not_its_path_count() {
    let depth = 12;
    let (read, reads) = reader(lattice(depth));
    let mut r =
        EdgeResolver::with_budget(read, anchors(&[1000, 3000]), budget(64, 1_000_000, 1_000_000));
    let out = r.resolve(3000).expect("within budget");
    // 2^depth paths, every one ending at the single anchor; reads stay
    // linear in nodes.
    assert!(reads.get() <= (depth * 2 + 2) as usize, "{} reads", reads.get());
    assert_eq!(out.len(), 1 << depth);
}

#[test]
fn exceeding_the_per_object_edge_budget_reports_incomplete() {
    let (read, _) = reader(lattice(10));
    let mut r = EdgeResolver::with_budget(read, anchors(&[1000, 3000]), budget(64, 256, 1_000_000));
    assert_eq!(r.resolve(3000), None);
}

#[test]
fn exceeding_the_depth_budget_reports_incomplete() {
    let chain = || {
        let mut index = Index::new();
        for n in 1..=40 {
            index.insert(n, vec![referrer(n + 1, "p")]);
        }
        index.insert(41, vec![referrer(0, "Root")]);
        index
    };
    let (read, _) = reader(chain());
    let mut shallow = EdgeResolver::with_budget(read, anchors(&[0]), budget(16, 256, 1_000_000));
    assert_eq!(shallow.resolve(1), None);
    let (read, _) = reader(chain());
    let mut deep = EdgeResolver::with_budget(read, anchors(&[0]), budget(64, 256, 1_000_000));
    // `via` runs from the anchor's side down to the object: 41, 40, ..., 2.
    let via: Vec<u32> = (0..40).map(|i| 41 - i).collect();
    let label = format!("Root/{}p", "p/".repeat(39));
    assert_eq!(deep.resolve(1), Some(vec![resolved(0, &label, &via)]));
}

#[test]
fn exhausting_the_read_budget_makes_every_later_resolution_incomplete() {
    let (read, _) = reader(Index::from([
        (5, vec![referrer(6, "a")]),
        (6, vec![referrer(0, "b")]),
        (7, vec![referrer(0, "c")]),
    ]));
    let mut r = EdgeResolver::with_budget(read, anchors(&[0]), budget(16, 256, 1));
    assert_eq!(r.resolve(5), None);
    assert_eq!(r.resolve(7), None);
}

#[test]
fn a_cycle_among_unchanged_objects_contributes_no_anchors_and_does_not_poison_the_memo() {
    // 10 <-> 11 reference each other; 11 is also referenced from the page
    // (anchor 3); 12 is under 10.
    let (read, _) = reader(Index::from([
        (12, vec![referrer(10, "x")]),
        (10, vec![referrer(11, "y")]),
        (11, vec![referrer(10, "z"), referrer(3, "Annots/[0]")]),
    ]));
    let mut r = EdgeResolver::new(read, anchors(&[3, 12]));
    assert_eq!(r.resolve(12), Some(vec![resolved(3, "Annots/[0]/y/x", &[11, 10])]));
    // Resolving 11 afresh must not reuse a result computed while 10 was on
    // the active chain.
    assert_eq!(r.resolve(11), Some(vec![referrer(3, "Annots/[0]")]));
}

#[test]
fn the_default_budget_is_upstreams() {
    assert_eq!(
        (
            DEFAULT_EDGE_RESOLVER_BUDGET.max_depth,
            DEFAULT_EDGE_RESOLVER_BUDGET.max_edges_per_object,
            DEFAULT_EDGE_RESOLVER_BUDGET.max_reads
        ),
        (16, 256, 20_000)
    );
}

// ── evaluateStep: a signature field added by Acrobat (92db1287a) ─────────

/// What Acrobat writes when it adds a document timestamp to a page whose
/// /Annots was an array inside the page: a new field that is its own widget,
/// the AcroForm's list grown, and the page's /Annots moved into a new object
/// that holds the old entry and the new widget.
fn moved(holder: &str, holder_usage: &[Value]) -> Vec<Value> {
    vec![
        change(
            3,
            Some("<</Annots [30 0 R]/Parent 2 0 R/Type /Page>>"),
            Some("<</Annots 60 0 R/Parent 2 0 R/Type /Page>>"),
            Usage {
                old: vec![edge(2, "Kids/[0]"), edge(30, "P")],
                new: vec![edge(2, "Kids/[0]"), edge(30, "P"), edge(31, "P")],
            },
        ),
        change(60, None, Some(holder), new_only(holder_usage)),
        change(
            31,
            None,
            Some(
                "<</F 132/FT /Sig/P 3 0 R/Rect [0 0 0 0]/Subtype /Widget/T (Signature3)/V 32 0 R>>",
            ),
            new_only(&[edge(10, "Fields/[2]"), edge(60, "[1]")]),
        ),
        change(
            32,
            None,
            Some("<</SubFilter /ETSI.RFC3161/Type /DocTimeStamp>>"),
            new_only(&[edge(31, "V")]),
        ),
        change(
            10,
            Some("<</Fields [20 0 R 30 0 R]/SigFlags 3>>"),
            Some("<</Fields [20 0 R 30 0 R 31 0 R]/SigFlags 3>>"),
            both(&[edge(1, "AcroForm")]),
        ),
        change(
            0,
            Some("<</Root 1 0 R/Size 60>>"),
            Some("<</Prev 999/Root 1 0 R/Size 61>>"),
            Usage::default(),
        ),
    ]
}

/// The structure after a document timestamp in a new field 31.
fn with_timestamp_field() -> Value {
    let mut fields = base_fields();
    fields.as_array_mut().unwrap().push(
        json!({ "objectNumber": 31, "name": "Signature3", "kind": "signature", "widgets": [31] }),
    );
    structure(json!({
        "fields": fields,
        "signatures": [approval(), signature(1, 31, json!({ "kind": "timestamp",
            "subFilter": "ETSI.RFC3161", "fieldName": "Signature3", "revisionIndex": 2 }))],
    }))
}

fn finding_for(step: &Value, n: u32) -> Option<&Value> {
    findings(step).iter().find(|f| f["objectNumber"] == n)
}

fn permitted_for(step: &Value, n: u32) -> bool {
    any(step, |f| f["objectNumber"] == n && f["verdict"] == "permitted")
}

#[test]
fn annots_moved_into_an_object_of_its_own_with_the_new_widget_is_permitted() {
    let s = step(
        1,
        2,
        &moved("[30 0 R 31 0 R]", &[edge(3, "Annots")]),
        &structure(json!({})),
        &with_timestamp_field(),
    );
    assert!(findings(&s).iter().all(|f| f["verdict"] == "permitted"), "{s}");
    assert_eq!(verdict(&s), "permitted");
    let f = finding_for(&s, 60).unwrap();
    assert_eq!((&f["rule"], &f["verdict"]), (&json!("signature-field-added"), &json!("permitted")));
}

#[test]
fn a_moved_annots_that_drops_the_old_entry_or_holds_an_inline_annotation_is_forbidden() {
    for holder in ["[31 0 R]", "[30 0 R 31 0 R <</Subtype /Link>>]"] {
        let s = step(
            1,
            2,
            &moved(holder, &[edge(3, "Annots")]),
            &structure(json!({})),
            &with_timestamp_field(),
        );
        assert_eq!(verdict(&s), "forbidden", "{holder}");
        assert!(!permitted_for(&s, 60), "{holder}");
    }
}

#[test]
fn a_moved_annots_out_of_order_with_another_annotation_or_unchanged_is_not_explained() {
    for holder in ["[31 0 R 30 0 R]", "[30 0 R 31 0 R 70 0 R]", "[30 0 R]"] {
        let certified = certification(2);
        let mut after = with_timestamp_field();
        let later = after["signatures"][1].clone();
        after["signatures"] = json!([certified.clone(), later]);
        let changes = moved(holder, &[edge(3, "Annots")]);
        let s = step(1, 2, &changes, &signed_with(json!([certified])), &after);
        assert_eq!(verdict(&s), "forbidden", "{holder}");
        assert!(!permitted_for(&s, 60), "{holder}");
        let approval = step(1, 2, &changes, &structure(json!({})), &with_timestamp_field());
        assert!(
            !any(&approval, |f| f["objectNumber"] == 60 && f["rule"] == "signature-field-added"),
            "{holder}"
        );
    }
}

#[test]
fn a_new_array_that_something_else_also_points_at_is_not_the_pages_list_alone() {
    let changes = moved("[30 0 R 31 0 R]", &[edge(3, "Annots"), edge(1, "Names/Extra")]);
    let s = step(1, 2, &changes, &structure(json!({})), &with_timestamp_field());
    assert_eq!(verdict(&s), "forbidden");
}

// ── evaluateStep: a page whose /Annots is an object of its own ───────────

/// Object 60 is page 3's list: the text field's widget and the signature
/// field.
fn own_list(after: &str, uses: &[Value]) -> Value {
    change(
        60,
        Some("[21 0 R 30 0 R]"),
        Some(after),
        Usage { old: vec![edge(3, "Annots")], new: uses.to_vec() },
    )
}

fn list_trailer() -> Value {
    change(
        0,
        Some("<</Root 1 0 R/Size 60>>"),
        Some("<</Prev 999/Root 1 0 R/Size 80>>"),
        Usage::default(),
    )
}

/// A document timestamp in a new field, 31, whose widget joins list `holder`.
fn new_field(holder: u32) -> Vec<Value> {
    vec![
        change(
            31,
            None,
            Some(
                "<</F 132/FT /Sig/P 3 0 R/Rect [0 0 0 0]/Subtype /Widget/T (Signature3)/V 32 0 R>>",
            ),
            new_only(&[edge(10, "Fields/[2]"), edge(holder, "[2]")]),
        ),
        change(
            32,
            None,
            Some("<</SubFilter /ETSI.RFC3161/Type /DocTimeStamp>>"),
            new_only(&[edge(31, "V")]),
        ),
        change(
            10,
            Some("<</Fields [20 0 R 30 0 R]/SigFlags 3>>"),
            Some("<</Fields [20 0 R 30 0 R 31 0 R]/SigFlags 3>>"),
            both(&[edge(1, "AcroForm")]),
        ),
    ]
}

/// A square comment with its popup and its appearance, joining the list.
fn list_comment() -> Vec<Value> {
    vec![
        change(
            70,
            None,
            Some("<</AP <</N 72 0 R>>/P 3 0 R/Popup 71 0 R/Subtype /Square/Type /Annot>>"),
            new_only(&[edge(60, "[2]"), edge(71, "Parent")]),
        ),
        change(
            71,
            None,
            Some("<</Parent 70 0 R/Subtype /Popup/Type /Annot>>"),
            new_only(&[edge(60, "[3]"), edge(70, "Popup")]),
        ),
        change(
            72,
            None,
            Some("stream(27,ab)<</Subtype /Form/Type /XObject>>"),
            new_only(&[edge(70, "AP/N")]),
        ),
    ]
}

fn not_permitted(step: &Value) -> Vec<&Value> {
    findings(step).iter().filter(|f| f["verdict"] != "permitted").collect()
}

#[test]
fn the_list_grows_there_by_a_new_signature_field_permitted_the_page_not_written() {
    let mut changes = vec![own_list("[21 0 R 30 0 R 31 0 R]", &[edge(3, "Annots")])];
    changes.extend(new_field(60));
    changes.push(list_trailer());
    let s = step(1, 2, &changes, &structure(json!({})), &with_timestamp_field());
    assert!(not_permitted(&s).is_empty(), "{s}");
    assert_eq!(verdict(&s), "permitted");
    let f = finding_for(&s, 60).unwrap();
    assert_eq!((&f["rule"], &f["verdict"]), (&json!("signature-field-added"), &json!("permitted")));
}

#[test]
fn the_list_grows_there_by_a_comment_permitted_for_an_approval_not_after_p2() {
    let mut changes = vec![own_list("[21 0 R 30 0 R 70 0 R 71 0 R]", &[edge(3, "Annots")])];
    changes.extend(list_comment());
    changes.push(list_trailer());
    let approval = step(1, 2, &changes, &structure(json!({})), &structure(json!({})));
    assert!(not_permitted(&approval).is_empty(), "{approval}");
    let f = finding_for(&approval, 60).unwrap();
    assert_eq!((&f["rule"], &f["verdict"]), (&json!("annotation"), &json!("permitted")));
    let fill = signed_with(json!([certification(2)]));
    let certified = step(1, 2, &changes, &fill, &fill);
    assert_eq!(verdict(&certified), "forbidden");
    assert!(any(&certified, |f| f["objectNumber"] == 70
        && f["rule"] == "annotation"
        && f["verdict"] == "forbidden"));
}

#[test]
fn a_list_that_drops_a_form_widget_is_forbidden() {
    for after in ["[30 0 R]", "[30 0 R 70 0 R 71 0 R]"] {
        let mut changes = vec![own_list(after, &[edge(3, "Annots")])];
        changes.extend(list_comment());
        changes.push(list_trailer());
        let s = step(1, 2, &changes, &structure(json!({})), &structure(json!({})));
        assert_eq!(verdict(&s), "forbidden", "{after}");
        let f = finding_for(&s, 60).unwrap();
        assert_eq!(f["rule"], "annotation", "{after}");
        assert_eq!(f["verdict"], "forbidden", "{after}");
        assert_eq!(f["detail"], "page /Annots added or removed a form widget", "{after}");
    }
}

#[test]
fn a_new_signature_field_does_not_explain_a_list_that_also_lost_an_entry() {
    let mut changes = vec![own_list("[30 0 R 31 0 R]", &[edge(3, "Annots")])];
    changes.extend(new_field(60));
    changes.push(list_trailer());
    let s = step(1, 2, &changes, &structure(json!({})), &with_timestamp_field());
    assert_eq!(verdict(&s), "forbidden");
    assert!(!any(&s, |f| f["objectNumber"] == 60 && f["rule"] == "signature-field-added"));
}

#[test]
fn an_array_something_else_also_points_at_is_not_a_page_list() {
    let mut changes =
        vec![own_list("[21 0 R 30 0 R 31 0 R]", &[edge(3, "Annots"), edge(1, "Names/Extra")])];
    changes.extend(new_field(60));
    changes.push(list_trailer());
    let s = step(1, 2, &changes, &structure(json!({})), &with_timestamp_field());
    assert_eq!(verdict(&s), "forbidden");
    assert!(!permitted_for(&s, 60));
}

// The page points at another list, and the old one stays in the file
// unwritten: what Acrobat writes for a second timestamp.

fn replaced(new_list: &str) -> Vec<Value> {
    let mut out = vec![
        change(
            3,
            Some("<</Annots 60 0 R/Parent 2 0 R/Type /Page>>"),
            Some("<</Annots 61 0 R/Parent 2 0 R/Type /Page>>"),
            Usage {
                old: vec![edge(2, "Kids/[0]"), edge(30, "P")],
                new: vec![edge(2, "Kids/[0]"), edge(30, "P"), edge(31, "P")],
            },
        ),
        change(61, None, Some(new_list), new_only(&[edge(3, "Annots")])),
    ];
    out.extend(new_field(61));
    out.push(list_trailer());
    out
}

fn old_list(text: Option<&str>, read: &str) -> Value {
    json!({ "objectNumber": 60,
        "value": text.map(|t| serde_json::to_value(parse_pdf_value(t).unwrap()).unwrap()),
        "read": read })
}

fn judge_replaced(new_list: &str, unwritten: &[Value]) -> Value {
    step_full(
        1,
        2,
        &replaced(new_list),
        unwritten,
        &structure(json!({})),
        &with_timestamp_field(),
        None,
    )
}

/// The one refusal a list that could not be read gives: its detail.
fn list_refusal(step: &Value) -> String {
    assert_eq!(verdict(step), "forbidden", "{step}");
    let refused = forbidden(step);
    assert_eq!(refused.len(), 1, "{step}");
    assert_eq!(refused[0]["rule"], "annotation");
    assert_eq!(refused[0]["objectNumber"], 3);
    refused[0]["detail"].as_str().unwrap().to_owned()
}

#[test]
fn the_rules_ask_for_the_old_list_and_for_nothing_a_step_wrote() {
    let asked = |changes: &[Value]| {
        let changes: Vec<ObjectChange> =
            changes.iter().map(|c| serde_json::from_value(c.clone()).unwrap()).collect();
        let before: RevisionStructure = serde_json::from_value(structure(json!({}))).unwrap();
        let after: RevisionStructure = serde_json::from_value(with_timestamp_field()).unwrap();
        super::unwritten_objects_to_read(&changes, &before, &after)
    };
    assert_eq!(asked(&replaced("[21 0 R 30 0 R 31 0 R]")), [60]);
    // A list the step wrote is read from the step.
    let mut written = vec![own_list("[21 0 R 30 0 R 31 0 R]", &[edge(3, "Annots")])];
    written.extend(new_field(60));
    written.push(list_trailer());
    assert!(asked(&written).is_empty());
    // A page that is not written names nothing, whatever else changed.
    let mut unwritten_page = new_field(60);
    unwritten_page.push(list_trailer());
    assert!(asked(&unwritten_page).is_empty());
}

#[test]
fn the_new_list_keeps_every_old_entry_and_adds_the_new_widget_permitted() {
    let s = judge_replaced("[21 0 R 30 0 R 31 0 R]", &[old_list(Some("[21 0 R 30 0 R]"), "ok")]);
    assert_eq!(verdict(&s), "permitted", "{s}");
    let page: Vec<&Value> = findings(&s).iter().filter(|f| f["objectNumber"] == 3).collect();
    assert_eq!(page.len(), 1);
    assert_eq!(
        (&page[0]["rule"], &page[0]["verdict"]),
        (&json!("signature-field-added"), &json!("permitted"))
    );
    let list: Vec<&Value> = findings(&s).iter().filter(|f| f["objectNumber"] == 61).collect();
    assert_eq!(list.len(), 1);
    assert_eq!(list[0]["rule"], "signature-field-added");
    assert!(list[0]["detail"].as_str().unwrap().contains("another list object"));
}

#[test]
fn the_new_list_drops_an_old_entry_forbidden() {
    let s = judge_replaced("[21 0 R 31 0 R]", &[old_list(Some("[21 0 R 30 0 R]"), "ok")]);
    assert_eq!(verdict(&s), "forbidden");
    assert!(!any(&s, |f| f["rule"] == "signature-field-added" && f["objectNumber"] == 3));
}

#[test]
fn the_new_list_puts_the_old_entries_in_another_order_not_explained_by_the_new_field() {
    let s = judge_replaced("[30 0 R 21 0 R 31 0 R]", &[old_list(Some("[21 0 R 30 0 R]"), "ok")]);
    assert!(!any(&s, |f| f["rule"] == "signature-field-added" && f["objectNumber"] == 3));
}

#[test]
fn the_old_list_was_not_read_one_refusal_that_says_so() {
    assert!(
        list_refusal(&judge_replaced("[21 0 R 30 0 R 31 0 R]", &[])).contains("cannot be compared")
    );
}

#[test]
fn the_old_list_does_not_parse_or_is_absent_the_same_refusal() {
    for read in ["failed", "absent"] {
        let s = judge_replaced("[21 0 R 30 0 R 31 0 R]", &[old_list(None, read)]);
        assert!(list_refusal(&s).contains("cannot be compared"), "{read}");
    }
}

#[test]
fn the_old_object_is_not_a_list_of_references_the_same_refusal() {
    for text in ["<</Type /Annot>>", "[21 0 R (text)]"] {
        let s = judge_replaced("[21 0 R 30 0 R 31 0 R]", &[old_list(Some(text), "ok")]);
        assert!(list_refusal(&s).contains("cannot be compared"), "{text}");
    }
}

#[test]
fn a_value_handed_over_for_an_object_the_step_wrote_is_not_read() {
    let other = json!({ "objectNumber": 61,
        "value": serde_json::to_value(parse_pdf_value("[99 0 R]").unwrap()).unwrap(), "read": "ok" });
    let s =
        judge_replaced("[21 0 R 30 0 R 31 0 R]", &[old_list(Some("[21 0 R 30 0 R]"), "ok"), other]);
    assert_eq!(verdict(&s), "permitted", "{s}");
}

// ── restrictionsFor: a document timestamp after a no-changes certification ──

fn restricted(before: &Value, judged: Option<&Value>) -> super::Restrictions {
    let before: RevisionStructure = serde_json::from_value(before.clone()).unwrap();
    let judged: Option<crate::features::signature::types::SignatureDto> =
        judged.map(|j| serde_json::from_value(j.clone()).unwrap());
    super::restrictions_for(&before, judged.as_ref())
}

fn timestamp_31() -> Value {
    signature(
        1,
        31,
        json!({ "kind": "timestamp", "subFilter": "ETSI.RFC3161",
        "fieldName": "Timestamp", "revisionIndex": 2 }),
    )
}

fn certified_then(permission: u8, later: Value) -> Value {
    signed_with(json!([certification(permission), later]))
}

#[test]
fn the_timestamp_is_judged_on_its_own_as_acrobat_judges_it() {
    let own = restricted(&certified_then(1, timestamp_31()), Some(&timestamp_31()));
    assert_eq!(own.level, crate::features::signature::types::ModificationLevel::Annotate);
    assert!(own.anchors.is_empty());
}

#[test]
fn the_certification_itself_and_a_later_approval_stay_bound_by_it() {
    use crate::features::signature::types::ModificationLevel::Lta;
    let before = certified_then(1, timestamp_31());
    assert_eq!(restricted(&before, Some(&before["signatures"][0])).level, Lta);
    let approval = signature(1, 31, json!({ "fieldName": "second", "revisionIndex": 2 }));
    assert_eq!(restricted(&certified_then(1, approval.clone()), Some(&approval)).level, Lta);
    // No signature in particular: every restriction in the file applies.
    assert_eq!(restricted(&before, None).level, Lta);
}

#[test]
fn a_certification_that_allows_form_filling_still_binds_a_later_timestamp() {
    let own = restricted(&certified_then(2, timestamp_31()), Some(&timestamp_31()));
    assert_eq!(own.level, crate::features::signature::types::ModificationLevel::Fill);
}
