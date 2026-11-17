//! `Store::state_text` is `state_json().to_string()`, byte for byte, through
//! every kind of change, and `revision` moves exactly when it might differ.

use std::any::Any;
use std::rc::Rc;

use epdf_kernel::error::Error;
use epdf_kernel::js;
use epdf_kernel::page_view::{PendingPage, no_pending_pages};
use epdf_kernel::store::{SliceJson, SliceReducer, SliceState, Store};
use epdf_kernel::types::{DocumentMeta, PendingMeta, PendingStatus};
use indexmap::IndexMap;
use serde_json::{Value, json};

#[derive(serde::Serialize)]
struct Add {
    r#type: &'static str,
    by: i64,
}

fn counter() -> (SliceReducer, SliceJson) {
    let reducer: SliceReducer = Rc::new(|state: &SliceState, action| {
        let add = action.as_any().downcast_ref::<Add>()?;
        let n = *state.downcast_ref::<i64>().expect("an i64 slice");
        (add.by != 0).then(|| Rc::new(n + add.by) as SliceState)
    });
    let to_json: SliceJson =
        Rc::new(|state: &dyn Any| json!({ "n": state.downcast_ref::<i64>(), "tag": "a\"b" }));
    (reducer, to_json)
}

fn same(store: &Store) {
    assert_eq!(&*store.state_text(), store.state_json().to_string().as_str());
}

#[test]
fn state_text_matches_state_json_through_every_change() {
    let store = Store::new(Rc::new(|_| {}));
    same(&store);
    let r0 = store.revision();

    let (reducer, to_json) = counter();
    let a = store.lease("zeta", reducer.clone(), Rc::new(0_i64), to_json.clone(), None);
    let r1 = store.revision();
    assert!(r1 > r0, "a new slice moves the revision");
    same(&store);
    store.lease("alpha \u{e9}", reducer, Rc::new(5_i64), to_json, None);
    same(&store);

    let before = store.revision();
    a.commit(&Add { r#type: "add", by: 0 });
    assert_eq!(store.revision(), before, "a reducer that returns None changes nothing");
    assert_eq!(&*store.state_text(), store.state_json().to_string().as_str());

    a.commit(&Add { r#type: "add", by: 3 });
    assert!(store.revision() > before);
    same(&store);
    let parsed: Value = serde_json::from_str(&store.state_text()).unwrap();
    assert_eq!(parsed["plugins"]["zeta"]["n"], 3);

    store.set_core(|core| core.active_id = Some("doc".into()), &Add { r#type: "core", by: 0 });
    same(&store);

    store.remove_slice("zeta");
    same(&store);
    assert!(
        serde_json::from_str::<Value>(&store.state_text()).unwrap()["plugins"]["zeta"].is_null()
    );

    store.destroy();
    same(&store);
}

#[test]
fn an_unchanged_revision_returns_the_same_text() {
    let store = Store::new(Rc::new(|_| {}));
    let (reducer, to_json) = counter();
    store.lease("k", reducer, Rc::new(1_i64), to_json, None);
    let first = store.state_text();
    let second = store.state_text();
    assert!(Rc::ptr_eq(&first, &second), "no rebuild while the revision holds");
}

#[test]
fn a_slice_text_writer_stands_in_for_the_value_and_none_falls_back() {
    use epdf_kernel::store::SliceText;
    let store = Store::new(Rc::new(|_| {}));
    let (reducer, to_json) = counter();
    let written: SliceText = Rc::new(|state: &dyn Any| {
        state.downcast_ref::<i64>().map(|n| format!(r#"{{"n":{n},"tag":"a\"b"}}"#))
    });
    store.lease_with_text(
        "w",
        reducer.clone(),
        Rc::new(4_i64),
        to_json.clone(),
        Some(written),
        None,
    );
    let declined: SliceText = Rc::new(|_: &dyn Any| None);
    store.lease_with_text("d", reducer, Rc::new(6_i64), to_json, Some(declined), None);
    same(&store);
    assert_eq!(&*store.slice_text("w").unwrap(), r#"{"n":4,"tag":"a\"b"}"#);
    assert_eq!(&*store.slice_text("d").unwrap(), r#"{"n":6,"tag":"a\"b"}"#);
}

fn page(index: u32, width: f64, height: f64) -> Value {
    json!({
        "index": index,
        "size": { "width": width, "height": height },
        "rotation": 0,
        "objectNumber": index + 2,
        "scale": 2.0,
        "tiny": 1.5e-7,
        "big": 1e21,
        "label": "p\u{e9}\"",
        "nan": { "$number": "NaN" },
    })
}

fn meta(id: &str, pages: Vec<Value>, unsaved: bool) -> Rc<DocumentMeta> {
    Rc::new(DocumentMeta {
        id: id.into(),
        instance_id: format!("{id}#1"),
        name: Some(format!("{id}.pdf")),
        page_count: pages.len() as u32,
        pages: Rc::new(Value::Array(pages)),
        page_labels: Rc::new(json!([{ "start": 0, "style": "r" }])),
        pending_pages: no_pending_pages(),
        pages_sequence: 3,
        render_policy: json!({ "kind": "continuous" }),
        has_unsaved_changes: unsaved,
    })
}

/// `core_text` prints core straight to text; it is byte for byte the text
/// of `js::to_value(core)`, the `core` member of `state_json`.
#[test]
fn core_text_byte_identical() {
    let store = Store::new(Rc::new(|_| {}));
    let reference = |store: &Store| js::to_value(&*store.core()).to_string();
    assert_eq!(&*store.core_text(), reference(&store));
    store.set_core(
        |core| {
            let pages = (0..40).map(|i| page(i, 612.0 + f64::from(i) / 3.0, 792.25)).collect();
            core.documents.insert("b".into(), meta("b", pages, false));
            core.documents.insert("a".into(), meta("a", vec![page(0, 0.1, -0.0)], true));
            core.pending.insert(
                "c".into(),
                PendingMeta {
                    id: "c".into(),
                    name: None,
                    status: PendingStatus::Error,
                    password_provided: Some(false),
                    error: Some(Error::new("bad \"file\"")),
                },
            );
            core.pending.insert(
                "d".into(),
                PendingMeta {
                    id: "d".into(),
                    name: Some("d".into()),
                    status: PendingStatus::Loading,
                    password_provided: None,
                    error: None,
                },
            );
            core.order = vec!["b".into(), "c".into(), "a".into(), "d".into()];
            core.active_id = Some("a".into());
        },
        &Add { r#type: "core", by: 0 },
    );
    assert_eq!(&*store.core_text(), reference(&store));
    same(&store);
}

#[derive(serde::Serialize)]
struct Floats {
    narrow: f32,
    narrow_nan: f32,
    wide: f64,
    whole: f64,
    infinite: f64,
    negative_zero: f64,
    absent: Option<f64>,
    present: Option<f32>,
    pair: (f32, Value),
    map: IndexMap<String, f64>,
    shared: Rc<Value>,
    looks: Vec<(Value, PendingPage)>,
}

/// Printing through the canonical serializer gives the bytes printing its
/// `Value` gives, floats of either width and non-finite numbers included.
#[test]
fn js_text_matches_the_text_of_its_value() {
    let mut map = IndexMap::new();
    map.insert("z".to_string(), 0.30000000000000004);
    map.insert("a".to_string(), f64::NEG_INFINITY);
    let value = Floats {
        narrow: 0.1,
        narrow_nan: f32::NAN,
        wide: 1.0 / 3.0,
        whole: 2.0,
        infinite: f64::INFINITY,
        negative_zero: -0.0,
        absent: None,
        present: Some(16_777_217.0),
        pair: (f32::MIN_POSITIVE, json!({ "k": [1, 2.5, null] })),
        map,
        shared: Rc::new(json!([{ "w": 612.5 }, { "w": 1e300 }])),
        looks: vec![
            (json!(2.0), PendingPage::Copy { of: json!({ "objectNumber": 7 }) }),
            (json!(3), PendingPage::Blank),
        ],
    };
    assert_eq!(js::to_string(&value), js::to_value(&value).to_string());
}
