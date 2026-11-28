//! The 2026-11-27b catch-up's form surface as JavaScript reaches it through
//! `WasmKernel` (CU-C7): a field every signer fills in (`forAnyone`, #1163),
//! a push button that holds a picture (`hasImage`) and its widget's `layout`
//! (#1165), read by name; and the design writes that carry them, sent to the
//! engine's `apply` as the plugin wrote them. The picture itself is the
//! engine's verb, `doc.forms.setButtonImage` in `packages/engine/core`, which
//! a page calls on the engine; the form plugin has none (CU-C7-D2).

use epdf_web::exports::WasmKernel;
use epdf_web::runtime::reset;
use serde_json::{Value, json};
use wasm_bindgen::prelude::*;
use wasm_bindgen_futures::JsFuture;
use wasm_bindgen_test::wasm_bindgen_test;

/// A one-page document whose form has `initials`, a text field every signer
/// fills in, widget 101, and `stamp`, a push button holding a picture,
/// widget 102 laid out `image`. Object numbers are reserved from 20 up.
/// Every `apply` is pushed onto `globalThis.applied` and answered with no
/// items. The session may do anything.
const STUB_ENGINE: &str = r#"
const page = { kind: 'objectNumber', objectNumber: 1 };
const box = { x: 0, y: 0, width: 600, height: 800 };
const pages = { pageCount: 1, labels: [], namedPages: [], pages: [{
  index: 0, ref: page, label: null, size: { width: 600, height: 800 }, rotation: 0, userUnit: 1,
  tabOrder: null, pdfCropBox: { left: 0, bottom: 0, right: 600, top: 800 },
  boxes: { media: box, crop: box, bleed: box, trim: box, art: box } }] };
const widgetRef = (objectNumber) => ({ kind: 'objectNumber', page, objectNumber });
const base = (objectNumber, name, widget) => ({
  ref: { kind: 'objectNumber', objectNumber }, name, origin: 'acroform',
  readOnly: false, required: false, noExport: false, alternateName: null, mappingName: null,
  defaultValueEntry: { kind: 'none' }, groupId: null, forAnyone: true, purpose: null,
  format: null, createdBy: null, createdAt: null, importedBy: null,
  widgets: [{ ref: widgetRef(widget), objectNumber: widget, page }],
});
const initials = { ...base(1, 'initials', 101), valueEntry: { kind: 'scalar', value: 'AL' },
  kind: 'text', value: 'AL', defaultValue: '', maxLength: null, multiline: false,
  password: false, comb: false };
const stamp = { ...base(2, 'stamp', 102), valueEntry: { kind: 'none' }, kind: 'pushbutton',
  hasImage: true };
const row = (objectNumber, field, fieldKind, layout) => ({
  subtype: 'widget', ref: widgetRef(objectNumber), page,
  rect: { x: 10, y: 20 + (objectNumber - 101) * 40, width: 90, height: 30 },
  hidden: false, noView: false, strokeWidth: 1, borderStyle: 'solid', textAlign: 'left',
  field: { kind: 'objectNumber', objectNumber: field }, fieldKind, layout,
});
globalThis.applied = [];
const held = [];
let next = 20;
return {
  open(input) { return Promise.resolve(JSON.stringify(JSON.parse(input).id)); },
  call(doc, path, pon, args) {
    if (path === 'pages.list') return Promise.resolve(JSON.stringify(pages));
    if (path === 'forms.list') return Promise.resolve(JSON.stringify({
      formKind: 'acroform', needsAppearances: false, calculationOrder: [],
      fields: [initials, stamp],
      widgets: [row(101, 1, 'text', null), row(102, 2, 'pushbutton', 'image')] }));
    if (path === 'objectNumbers.reserve') {
      for (let i = 0; i < JSON.parse(args)[0]; i++) held.push(next++);
      return Promise.resolve('{"$undefined":true}');
    }
    if (path === 'apply') {
      const [request, options] = JSON.parse(args);
      globalThis.applied.push(request.ops);
      return Promise.resolve(JSON.stringify({ items: [], meta: { affectedPages: [],
        cacheDelta: null, opId: options.opId, undoable: true } }));
    }
    return Promise.resolve('{"$undefined":true}');
  },
  heldObjectNumbers() { return held.length; },
  takeObjectNumber() { return held.length ? held.shift() : null; },
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

fn widget(object_number: u32) -> Value {
    json!({ "kind": "objectNumber", "page": page(), "objectNumber": object_number })
}

fn field(object_number: u32) -> Value {
    json!({ "kind": "objectNumber", "objectNumber": object_number })
}

#[wasm_bindgen_test]
async fn a_field_reads_for_anyone_a_push_button_its_picture_and_a_widget_its_layout() {
    let log = js_sys::Array::new();
    let (kernel, form) = form_kernel(&log).await;
    let initials = invoke(&kernel, &form, "get", json!([field(1)]));
    assert_eq!(initials["returned"]["forAnyone"], true, "{initials}");
    let stamp = invoke(&kernel, &form, "get", json!([field(2)]));
    assert_eq!(stamp["returned"]["kind"], "pushbutton", "{stamp}");
    assert_eq!(stamp["returned"]["hasImage"], true, "{stamp}");
    let row = invoke(&kernel, &form, "getWidget", json!([widget(102)]));
    assert_eq!(row["returned"]["layout"], "image", "{row}");
    let row = invoke(&kernel, &form, "getWidget", json!([widget(101)]));
    assert_eq!(row["returned"]["layout"], Value::Null, "{row}");
}

#[wasm_bindgen_test]
async fn for_anyone_and_a_layout_go_to_the_engine_as_written() {
    let log = js_sys::Array::new();
    let (kernel, form) = form_kernel(&log).await;
    settle(&kernel, &form, "update", json!([field(1), { "forAnyone": false }])).await;
    settle(&kernel, &form, "updateWidget", json!([widget(102), { "layout": "caption-below" }]))
        .await;
    let ops: Vec<Value> = applied()
        .as_array()
        .unwrap()
        .iter()
        .flat_map(|ops| ops.as_array().unwrap().clone())
        .collect();
    assert_eq!(
        ops,
        [
            json!({ "type": "forms.update", "field": field(1), "patch": { "forAnyone": false } }),
            json!({ "type": "forms.updateWidget", "widget": widget(102),
                "patch": { "layout": "caption-below" } }),
        ]
    );
    let draft = json!({ "kind": "pushbutton", "name": "seal", "forAnyone": true,
        "widgets": [{ "page": page(), "rect": { "x": 200, "y": 20, "width": 90, "height": 30 },
            "layout": "caption-over" }] });
    settle(&kernel, &form, "create", json!([draft])).await;
    let ops: Vec<Value> = applied()
        .as_array()
        .unwrap()
        .iter()
        .flat_map(|ops| ops.as_array().unwrap().clone())
        .collect();
    let created = ops.iter().find(|op| op["type"] == "forms.create").expect("a create was sent");
    assert_eq!(created["draft"]["forAnyone"], true, "{created}");
    assert_eq!(created["draft"]["widgets"][0]["layout"], "caption-over", "{created}");
}
