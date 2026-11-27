//! The 2026-11-27 catch-up's form surface as JavaScript reaches it through
//! `WasmKernel` (CU-C8): the groups the app names and the events they fire,
//! the design group, a fill item's frame, turn, group and accent, a field's
//! purpose and format, a widget's anchor and focus, and a move into a group,
//! over a stub engine whose form has one turned, grouped text field.

use epdf_web::exports::WasmKernel;
use epdf_web::runtime::reset;
use serde_json::{Value, json};
use wasm_bindgen::prelude::*;
use wasm_bindgen_futures::JsFuture;
use wasm_bindgen_test::wasm_bindgen_test;

/// A one-page document whose form has the field `name`, in Dana's group,
/// with a purpose and a date format, and its widget 101 a quarter turned: a
/// 100 by 24 frame standing as 24 by 100. Every `apply` is pushed onto
/// `globalThis.applied` and answered with no items. The session may do
/// anything, put a field in any group included.
const STUB_ENGINE: &str = r#"
const page = { kind: 'objectNumber', objectNumber: 1 };
const box = { x: 0, y: 0, width: 600, height: 800 };
const pages = { pageCount: 1, labels: [], namedPages: [], pages: [{
  index: 0, ref: page, label: null, size: { width: 600, height: 800 }, rotation: 0, userUnit: 1,
  tabOrder: null, pdfCropBox: { left: 0, bottom: 0, right: 600, top: 800 },
  boxes: { media: box, crop: box, bleed: box, trim: box, art: box } }] };
const widgetRef = { kind: 'objectNumber', page, objectNumber: 101 };
const field = {
  ref: { kind: 'objectNumber', objectNumber: 1 }, name: 'name', origin: 'acroform',
  readOnly: false, required: true, noExport: false, alternateName: null, mappingName: null,
  valueEntry: { kind: 'scalar', value: 'Ada' }, defaultValueEntry: { kind: 'none' },
  groupId: 'dana', purpose: 'date-signed', format: { kind: 'date', pattern: 'yyyy-mm-dd' },
  createdBy: null, createdAt: null, filledBy: null, filledByName: null, filledAt: null,
  importedBy: null, widgets: [{ ref: widgetRef, objectNumber: 101, page }],
  kind: 'text', value: 'Ada', defaultValue: '', maxLength: null, multiline: false,
  password: false, comb: false,
};
const widget = {
  subtype: 'widget', ref: widgetRef, page, rect: { x: 48, y: -18, width: 24, height: 100 },
  box: { x: 10, y: 20, width: 100, height: 24 }, rotation: 90, hidden: false, noView: false,
  strokeWidth: 1, borderStyle: 'solid', textAlign: 'left',
  field: { kind: 'objectNumber', objectNumber: 1 }, fieldKind: 'text',
};
globalThis.applied = [];
return {
  open(input) { return Promise.resolve(JSON.stringify(JSON.parse(input).id)); },
  call(doc, path, pon, args) {
    if (path === 'pages.list') return Promise.resolve(JSON.stringify(pages));
    if (path === 'forms.list') return Promise.resolve(JSON.stringify({
      formKind: 'acroform', needsAppearances: false, calculationOrder: [],
      fields: [field], widgets: [widget] }));
    if (path === 'apply') {
      const [request, options] = JSON.parse(args);
      globalThis.applied.push(request.ops);
      return Promise.resolve(JSON.stringify({ items: [], meta: { affectedPages: [],
        cacheDelta: null, opId: options.opId, undoable: true } }));
    }
    return Promise.resolve('{"$undefined":true}');
  },
  has() { return false; },
  capabilities() { return '{}'; },
  security(doc, member) {
    if (member === 'passwordPrompt') return '{"state":"none"}';
    if (member === 'identity' || member === 'current') return 'null';
    if (member === 'effectiveScope') return '["*"]';
    return 'true';
  },
  pageIndex() { return 0; },
  lastServerSequence() { return null; },
  subscribe() { return () => {}; },
  destroy() { return Promise.resolve('{"$undefined":true}'); },
};
"#;

fn stub_engine() -> JsValue {
    js_sys::Function::new_no_args(STUB_ENGINE).call0(&JsValue::NULL).unwrap()
}

fn applied() -> Value {
    let applied = js_sys::Reflect::get(&js_sys::global(), &"applied".into()).unwrap();
    js_json(&applied)
}

/// A `call_js` that logs `[id, argsJson]` into `log` and answers `undefined`.
fn call_js(log: &js_sys::Array) -> js_sys::Function {
    let make = js_sys::Function::new_with_args(
        "log",
        "return (id, args) => { log.push([id, args]); return undefined; };",
    );
    make.call1(&JsValue::NULL, log).unwrap().into()
}

fn parse(text: &str) -> Value {
    serde_json::from_str(text).unwrap()
}

/// A JavaScript value as JSON; `undefined` as `null`.
fn js_json(value: &JsValue) -> Value {
    js_sys::JSON::stringify(value).unwrap().as_string().map_or(Value::Null, |text| parse(&text))
}

fn ref_of(capability: &Value, member: &str) -> u32 {
    capability[member]["$ref"].as_u64().unwrap_or_else(|| panic!("{member}: {capability}")) as u32
}

/// Call a member; the outcome as JSON.
fn invoke(kernel: &WasmKernel, form: &Value, member: &str, args: Value) -> Value {
    parse(&kernel.invoke_ref(ref_of(form, member), args.to_string()).unwrap())
}

/// Call a member that answers a promise; what it resolves.
async fn settle(kernel: &WasmKernel, form: &Value, member: &str, args: Value) -> Value {
    let outcome = invoke(kernel, form, member, args);
    let id = outcome["promise"].as_u64().unwrap_or_else(|| panic!("{member}: {outcome}")) as u32;
    js_json(&JsFuture::from(kernel.promise(id).unwrap()).await.unwrap())
}

async fn form_kernel(log: &js_sys::Array) -> (WasmKernel, Value) {
    reset();
    let plugins = json!([
        { "factory": "interaction" },
        { "factory": "form-current", "config": { "validation": "none" } },
    ])
    .to_string();
    let release = js_sys::Function::new_no_args("");
    let kernel =
        WasmKernel::new(&plugins, stub_engine(), call_js(log), release, None, None, None).unwrap();
    JsFuture::from(kernel.start()).await.unwrap();
    let documents = parse(&kernel.documents());
    let outcome = parse(
        &kernel.invoke_ref(ref_of(&documents, "open"), r#"[{"id":"doc-1"}]"#.into()).unwrap(),
    );
    let id = outcome["promise"].as_u64().unwrap_or_else(|| panic!("open: {outcome}")) as u32;
    JsFuture::from(kernel.promise(id).unwrap()).await.unwrap();
    let form = parse(&kernel.capability("form".into(), None).unwrap());
    settle(&kernel, &form, "refresh", json!([])).await;
    (kernel, form)
}

fn page() -> Value {
    json!({ "kind": "objectNumber", "objectNumber": 1 })
}

fn widget() -> Value {
    json!({ "kind": "objectNumber", "page": page(), "objectNumber": 101 })
}

#[wasm_bindgen_test]
async fn a_field_reads_its_purpose_and_format_and_fills_as_a_turned_grouped_item() {
    let log = js_sys::Array::new();
    let (kernel, form) = form_kernel(&log).await;
    let field =
        invoke(&kernel, &form, "get", json!([{ "kind": "objectNumber", "objectNumber": 1 }]));
    assert_eq!(field["returned"]["purpose"], "date-signed", "{field}");
    assert_eq!(field["returned"]["format"], json!({ "kind": "date", "pattern": "yyyy-mm-dd" }));
    let items = invoke(&kernel, &form, "listWidgets", json!([page()]));
    let item = &items["returned"][0];
    assert_eq!(item["box"], json!({ "x": 10, "y": 20, "width": 100, "height": 24 }), "{items}");
    assert_eq!(item["rotation"], 90);
    assert_eq!(item["rect"], json!({ "x": 48, "y": -18, "width": 24, "height": 100 }));
    assert_eq!(item["groupId"], "dana");
    assert_eq!(
        (&item["canFill"], &item["required"], &item["empty"]),
        (&json!(true), &json!(true), &json!(false))
    );
    assert_eq!(item["accent"], json!({ "color": "#ea580c", "strength": 0.16 }));
    let anchor = invoke(&kernel, &form, "getWidgetAnchor", json!([widget()]));
    assert_eq!(anchor["returned"], json!({ "page": page(), "bounds": item["rect"] }));
    assert_eq!(invoke(&kernel, &form, "isWidgetFocused", json!([widget()]))["returned"], false);
    assert_eq!(invoke(&kernel, &form, "listSelectedFields", json!([]))["returned"], json!([]));
}

#[wasm_bindgen_test]
async fn groups_are_named_announced_designed_for_and_moved_into() {
    let log = js_sys::Array::new();
    let (kernel, form) = form_kernel(&log).await;
    assert_eq!(
        invoke(&kernel, &form, "listGroups", json!([]))["returned"],
        json!([{ "id": "dana", "label": null, "color": "#ea580c" }])
    );
    invoke(&kernel, &form, "onGroupsChanged", json!([{ "$fn": "js:groups" }]));
    invoke(&kernel, &form, "onDesignGroupChanged", json!([{ "$fn": "js:design" }]));
    invoke(
        &kernel,
        &form,
        "setGroups",
        json!([[{ "id": "ari", "label": "Ari Cole" }, { "id": "dana", "color": "var(--signer-2)" }]]),
    );
    let groups = json!([
        { "id": "ari", "label": "Ari Cole", "color": "#ea580c" },
        { "id": "dana", "label": null, "color": "var(--signer-2)" },
    ]);
    assert_eq!(invoke(&kernel, &form, "listGroups", json!([]))["returned"], groups);
    assert_eq!(invoke(&kernel, &form, "canSetGroup", json!(["ari"]))["returned"], true);
    invoke(&kernel, &form, "setDesignGroup", json!(["ari"]));
    assert_eq!(invoke(&kernel, &form, "getDesignGroup", json!([]))["returned"], "ari");
    let calls: Vec<(String, Value)> = (0..log.length())
        .map(|i| {
            let call: js_sys::Array = log.get(i).into();
            (call.get(0).as_string().unwrap(), parse(&call.get(1).as_string().unwrap()))
        })
        .collect();
    assert_eq!(
        calls,
        [
            ("groups".to_string(), json!([{ "groups": groups }])),
            ("design".to_string(), json!([{ "groupId": "ari" }])),
        ]
    );
    let moved = settle(
        &kernel,
        &form,
        "moveToGroup",
        json!([[{ "kind": "objectNumber", "objectNumber": 1 }], "ari"]),
    )
    .await;
    assert_eq!(moved["fields"][0]["name"], "name", "{moved}");
    assert_eq!(
        applied(),
        json!([[{ "type": "forms.update", "field": { "kind": "objectNumber", "objectNumber": 1 },
            "patch": { "groupId": "ari" } }]])
    );
}
