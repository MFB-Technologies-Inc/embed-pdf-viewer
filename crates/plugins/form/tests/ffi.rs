#![cfg(feature = "uniffi")]

//! The uniffi handle over the current plugin. Migrated by hand in Task 13.18
//! with `src/ffi.rs`, including the five restored in 13.18's fix round (refresh and status, the widget projection, a closed
//! document's refusal, `on_end` at close, a cancelled write).

use std::cell::RefCell;
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::{Arc, Mutex};

use epdf_ffi::{
    AnnotationRef, CancelToken, FormFieldRef, KernelThread, PageRef, Point, ResourceStatus,
    ViewerError,
};
use epdf_plugin_form::ffi::{
    FormConfig, FormCreateFieldInput, FormDuplicateOptions, FormEvent, FormEventListener,
    FormFieldDetail, FormFieldFormat, FormFieldPatch, FormFieldPatchKey, FormFieldSettingsPatch,
    FormFillControl, FormGroup, FormGroupInput, FormHandle, FormKind, FormSetValueStatus,
    FormSettingsPatch, FormValidation, FormValueInput,
};
use epdf_plugin_form::{FORM, form_plugin};
use epdf_plugin_interaction::{InteractionConfig, interaction_plugin};
use futures::executor::block_on;
use plugin_conformance::test_kernel::{Reply, TestKernel, TestSpec};
use serde_json::{Value, json};

fn page() -> Value {
    json!({ "kind": "objectNumber", "objectNumber": 1 })
}

fn text(name: &str) -> Value {
    json!({
        "ref": { "kind": "objectNumber", "objectNumber": 1 }, "name": "name", "origin": "acroform",
        "readOnly": false, "required": false, "noExport": false,
        "alternateName": "Your name", "mappingName": null,
        "valueEntry": { "kind": "scalar", "value": name }, "defaultValueEntry": { "kind": "none" },
        "groupId": null, "createdBy": null, "createdAt": null, "filledBy": null,
        "filledByName": null, "filledAt": null, "importedBy": null,
        "widgets": [{ "ref": { "kind": "objectNumber", "page": page(), "objectNumber": 101 },
            "objectNumber": 101, "page": page() }],
        "kind": "text", "value": name, "defaultValue": "", "maxLength": 40,
        "multiline": false, "password": false, "comb": false
    })
}

fn row() -> Value {
    json!({
        "subtype": "widget", "ref": { "kind": "objectNumber", "page": page(), "objectNumber": 101 },
        "page": page(), "rect": { "x": 10, "y": 20, "width": 100, "height": 24 },
        "hidden": false, "noView": false, "strokeWidth": 1, "borderStyle": "solid",
        "textAlign": "left", "field": { "kind": "objectNumber", "objectNumber": 1 },
        "fieldKind": "text",
    })
}

#[test]
fn cancelling_running_design_operations_ends_the_wait_and_leaves_admitted_changes_owned() {
    use std::future::Future;
    use std::task::{Context, Poll};
    for operation in ["reorder", "update", "delete", "patch"] {
        let thread = KernelThread::spawn(|_| {
            let mut engine = spec().engine;
            engine["documents"]["a"]["responses"] = json!([{"path":"apply","defer":"design"}]);
            let spec = TestSpec::new(engine).respond("forms.list", |_| {
                let mut first = text("Ada");
                first["actions"] = json!({"calculate":{"root":{"type":"javascript","subtype":"JavaScript","script":"event.value = 1","next":[]},"incomplete":false,"warnings":[]}});
                let mut second = first.clone();
                second["ref"] = json!({"kind":"objectNumber","objectNumber":2});
                second["name"] = json!("second");
                second["widgets"] = json!([]);
                let mut widget = row();
                widget["box"] = widget["rect"].clone();
                widget["rotation"] = json!(0);
                Ok(Reply::value(json!({
                    "formKind":"acroform","needsAppearances":false,
                    "calculationOrder":[first["ref"],second["ref"]],"fields":[first,second],"widgets":[widget]
                })))
            });
            TestKernel::open(vec![interaction_plugin(InteractionConfig::default()), form_plugin(None)], spec)
        }).unwrap();
        let post = thread.post();
        let form =
            block_on(thread.call(move |k| FormHandle::new(k.capability(&FORM).unwrap(), post)))
                .unwrap();
        let token = CancelToken::new();
        let first = FormFieldRef::ObjectNumber { field_object_number: 1 };
        let second = FormFieldRef::ObjectNumber { field_object_number: 2 };
        let original_order = vec![Some(first), Some(second.clone())];
        let original_bounds = epdf_ffi::Rect { x: 10.0, y: 20.0, width: 100.0, height: 24.0 };
        assert_eq!(block_on(form.snapshot()).unwrap().unwrap().calculation_order, original_order);
        let widget = epdf_ffi::AnnotationRef::ObjectNumber {
            page: PageRef { page_object_number: 1 },
            annot_object_number: 101,
        };
        let mut call: std::pin::Pin<Box<dyn Future<Output = Result<(), ViewerError>> + '_>> =
            match operation {
                "reorder" => Box::pin(async {
                    form.reorder_calculations(
                        vec![second.clone()],
                        epdf_plugin_form::ffi::FormCalculationPosition::Start,
                        token.clone(),
                    )
                    .await
                    .map(|_| ())
                }),
                "update" => Box::pin(async {
                    form.update_widget(
                        widget.clone(),
                        json!({"rect":{"x":210,"y":240,"width":100,"height":24}}).to_string(),
                        token.clone(),
                    )
                    .await
                    .map(|_| ())
                }),
                "patch" => Box::pin(async {
                    form.update_patch(
                        text_ref(),
                        json!({"maxLength":41}).to_string(),
                        token.clone(),
                    )
                    .await
                    .map(|_| ())
                }),
                _ => Box::pin(async {
                    form.delete_widget(widget.clone(), token.clone()).await.map(|_| ())
                }),
            };
        let mut cx = Context::from_waker(futures::task::noop_waker_ref());
        assert!(call.as_mut().poll(&mut cx).is_pending());
        assert!(
            block_on(thread.call(|k| k.engine.log_since(0).iter().any(|e| e["path"] == "apply")))
                .unwrap(),
            "{operation}"
        );
        match operation {
            "reorder" => assert_eq!(
                block_on(form.snapshot()).unwrap().unwrap().calculation_order,
                vec![original_order[1].clone(), original_order[0].clone()]
            ),
            "update" => assert_eq!(
                block_on(form.widget(widget.clone())).unwrap().unwrap().bounds,
                epdf_ffi::Rect { x: 210.0, y: 240.0, width: 100.0, height: 24.0 }
            ),
            "patch" => assert!(matches!(
                block_on(form.field(text_ref())).unwrap().unwrap().detail,
                FormFieldDetail::Text { max_length: Some(41), .. }
            )),
            _ => assert!(block_on(form.widget(widget.clone())).unwrap().is_none()),
        }
        token.cancel();
        block_on(thread.call(|_| ())).unwrap();
        assert!(
            matches!(call.as_mut().poll(&mut cx),Poll::Ready(Err(ViewerError::Kernel {code:Some(code),..})) if code == "operation-cancelled"),
            "{operation}"
        );
        assert!(
            !block_on(
                thread.call(|k| k.engine.log_since(0).iter().any(|e| e.get("abort").is_some()))
            )
            .unwrap(),
            "{operation}"
        );
        // Staged writes belong to the kernel after admission. A late refusal
        // still removes the prediction after the caller stopped waiting.
        assert!(
            block_on(thread.call(|k| {
                k.engine
                    .settle("design", Err(&json!({"code":"InvalidArg","message":"late refusal"})))
            }))
            .unwrap()
        );
        assert!(matches!(
            block_on(form.field(text_ref())).unwrap().unwrap().detail,
            FormFieldDetail::Text { max_length: Some(40), .. }
        ));
        assert_eq!(block_on(form.snapshot()).unwrap().unwrap().calculation_order, original_order);
        assert_eq!(
            block_on(form.widget(widget.clone())).unwrap().unwrap().bounds,
            original_bounds,
            "{operation}"
        );
    }
}

thread_local! {
    static NAME: RefCell<String> = RefCell::new("Ada".into());
    /// The ops each `apply` the engine was sent carried, in order.
    static SENT: RefCell<Vec<Value>> = const { RefCell::new(Vec::new()) };
}

fn sent(thread: &KernelThread<TestKernel>) -> Vec<Value> {
    block_on(thread.call(|_| SENT.with(|s| s.borrow().clone()))).unwrap()
}

fn spec() -> TestSpec {
    let bx = json!({ "x": 0, "y": 0, "width": 600, "height": 800 });
    TestSpec::new(json!({ "documents": { "a": { "id": "a", "security": { "allows": "*" },
        "pages": [{ "index": 0, "ref": page(), "label": null, "size": { "width": 600, "height": 800 },
            "rotation": 0, "userUnit": 1, "tabOrder": null,
            "pdfCropBox": { "left": 0, "bottom": 0, "right": 600, "top": 800 },
            "boxes": { "media": bx, "crop": bx, "bleed": bx, "trim": bx, "art": bx } }],
        "objectNumbers": [20, 21, 22, 23] } } }))
    .respond("forms.list", |_| {
        let name = NAME.with(|n| n.borrow().clone());
        Ok(Reply::value(json!({
            "formKind": "acroform", "needsAppearances": false, "calculationOrder": [],
            "fields": [text(&name)], "widgets": [row()],
        })))
    })
    .respond("apply", |args| {
        SENT.with(|s| s.borrow_mut().push(args[0]["ops"].clone()));
        if args[0]["ops"][0]["type"] == "forms.copy" {
            return Ok(copied(&args[0]["ops"][0], &args[1]["opId"]));
        }
        let value = args[0]["ops"][0]["value"]["value"].as_str().unwrap_or_default().to_string();
        NAME.with(|n| *n.borrow_mut() = value.clone());
        let field = text(&value);
        let op_id = args[1]["opId"].clone();
        let meta = json!({ "affectedPages": [page()], "cacheDelta": null, "opId": op_id, "undoable": true,
            "changedFields": [field["ref"]], "changedWidgets": field["widgets"], "redrawnWidgets": field["widgets"] });
        let event = json!({ "type": "forms.valueSet", "field": field, "widgets": [], "meta": meta,
            "origin": { "kind": "local", "transaction": { "id": op_id, "index": 0, "count": 1 } } });
        Ok(Reply {
            value: json!({ "items": [{ "type": "forms.setValue", "field": field, "widgets": [], "meta": meta }], "meta": meta }),
            events: vec![event],
        })
    })
}

/// A `forms.copy` answered as the engine answers it: the text field again
/// as `name_2`, under the op's first object number, its widget under the next.
fn copied(op: &Value, op_id: &Value) -> Reply {
    let numbers: Vec<u64> =
        op["objectNumbers"].as_array().into_iter().flatten().filter_map(Value::as_u64).collect();
    let (field_number, widget_number) = (numbers[0], numbers[1]);
    let widget_ref =
        json!({ "kind": "objectNumber", "page": page(), "objectNumber": widget_number });
    let mut field = text("");
    field["ref"] = json!({ "kind": "objectNumber", "objectNumber": field_number });
    field["name"] = json!("name_2");
    field["widgets"] =
        json!([{ "ref": widget_ref, "objectNumber": widget_number, "page": page() }]);
    let mut widget = row();
    widget["ref"] = widget_ref;
    widget["field"] = field["ref"].clone();
    let meta = json!({ "affectedPages": [page()], "cacheDelta": null, "opId": op_id, "undoable": true,
        "changedFields": [field["ref"]], "changedWidgets": [widget["ref"]], "redrawnWidgets": [] });
    let item = json!({ "type": "forms.copy", "fields": [field], "widgets": [widget], "dropped": [],
        "meta": meta });
    Reply { value: json!({ "items": [item], "meta": meta }), events: vec![] }
}

fn handle() -> (KernelThread<TestKernel>, Arc<FormHandle>) {
    let thread = KernelThread::spawn(|_| {
        TestKernel::open(
            vec![
                interaction_plugin(InteractionConfig::default()),
                form_plugin(FormConfig { validation: Some(FormValidation::None) }.into()),
            ],
            spec(),
        )
    })
    .unwrap();
    let post = thread.post();
    let handle =
        block_on(thread.call(move |k| FormHandle::new(k.capability(&FORM).unwrap(), post)))
            .unwrap();
    (thread, handle)
}

#[test]
fn the_snapshot_is_typed() {
    let (_thread, form) = handle();
    let snapshot = block_on(form.snapshot()).unwrap().expect("read");
    let field = &snapshot.fields[0];
    assert_eq!(field.name, "name");
    assert_eq!(field.alternate_name.as_deref(), Some("Your name"));
    assert!(
        matches!(&field.detail, FormFieldDetail::Text { value, max_length: Some(40), .. } if value == "Ada")
    );
    assert_eq!(field.widgets[0].page, Some(PageRef { page_object_number: 1 }));
}

#[derive(Default)]
struct Events(Mutex<Vec<FormEvent>>, AtomicUsize);

impl FormEventListener for Events {
    fn on_event(&self, event: FormEvent) {
        self.0.lock().unwrap().push(event);
    }
    fn on_end(&self) {
        self.1.fetch_add(1, Ordering::SeqCst);
    }
}

fn code(error: &ViewerError) -> Option<&str> {
    match error {
        ViewerError::Kernel { code, .. } => code.as_deref(),
        ViewerError::Closed => None,
    }
}

fn text_ref() -> FormFieldRef {
    FormFieldRef::ObjectNumber { field_object_number: 1 }
}

#[test]
fn a_value_is_written_and_announced() {
    let (_thread, form) = handle();
    let events = Arc::new(Events::default());
    let _subscription = form.subscribe(events.clone());
    let field_ref = FormFieldRef::ObjectNumber { field_object_number: 1 };
    let result = block_on(form.set_value(
        field_ref.clone(),
        FormValueInput::Value { value: Some("Grace".into()) },
        CancelToken::new(),
    ))
    .unwrap();
    assert_eq!(result.status, FormSetValueStatus::Applied);
    assert!(
        matches!(&result.field.detail, FormFieldDetail::Text { value, .. } if value == "Grace")
    );
    let value = block_on(form.value(field_ref)).unwrap();
    assert_eq!(value, Some(FormValueInput::Value { value: Some("Grace".into()) }));
    let events = events.0.lock().unwrap();
    assert!(events.iter().any(|e| matches!(e, FormEvent::ValueChanged { .. })), "{events:?}");
}

#[test]
fn refresh_reads_the_form_again_and_reports_ready() {
    let (_thread, form) = handle();
    block_on(form.refresh(CancelToken::new())).unwrap();
    assert_eq!(block_on(form.status()).unwrap(), ResourceStatus::Ready);
    let snapshot = block_on(form.snapshot()).unwrap().expect("read");
    assert_eq!(snapshot.form_kind, FormKind::Acroform);
    let fields = block_on(form.fields(None)).unwrap();
    assert_eq!(fields.len(), 1);
    assert_eq!(fields[0].name, "name");
}

#[test]
fn the_widget_projection_draws_each_widget_as_a_control() {
    let (_thread, form) = handle();
    block_on(form.ensure_loaded(PageRef { page_object_number: 1 }, CancelToken::new())).unwrap();
    let items = block_on(form.widgets(PageRef { page_object_number: 1 })).unwrap();
    assert_eq!(items.len(), 1);
    assert_eq!(items[0].annot_object_number, 101);
    assert!(items[0].annotation_ref.is_some());
    assert!(
        matches!(&items[0].control, FormFillControl::Text { value, max_length: Some(40), .. } if value == "Ada"),
        "{:?}",
        items[0]
    );
    assert!(!items[0].disabled);
}

#[test]
fn a_closed_documents_capability_refuses_with_instance_closed() {
    let (thread, form) = handle();
    block_on(thread.call(|k| k.close())).unwrap().unwrap();
    for error in [
        block_on(form.fields(None)).map(|_| ()).unwrap_err(),
        block_on(form.can_fill(text_ref())).map(|_| ()).unwrap_err(),
        block_on(form.refresh(CancelToken::new())).unwrap_err(),
    ] {
        assert_eq!(code(&error), Some("instance-closed"), "{error:?}");
    }
}

#[test]
fn closing_the_document_ends_its_subscriptions() {
    let (thread, form) = handle();
    let events = Arc::new(Events::default());
    let _subscription = form.subscribe(events.clone());
    block_on(thread.call(|_| ())).unwrap();
    block_on(thread.call(|k| k.close())).unwrap().unwrap();
    assert_eq!(events.1.load(Ordering::SeqCst), 1);
}

#[test]
fn cancelling_a_set_value_before_it_runs_leaves_the_field_alone() {
    let (thread, form) = handle();
    block_on(form.refresh(CancelToken::new())).unwrap();
    let (release, gate) = std::sync::mpsc::channel::<()>();
    let (parked_tx, parked) = std::sync::mpsc::channel::<()>();
    let blocker = thread.call(move |_| {
        parked_tx.send(()).unwrap();
        gate.recv().unwrap();
    });
    parked.recv().unwrap();
    let cancel = CancelToken::new();
    let write = form.set_value(
        text_ref(),
        FormValueInput::Value { value: Some("Grace".into()) },
        cancel.clone(),
    );
    cancel.cancel();
    release.send(()).unwrap();
    block_on(blocker).unwrap();
    let error = block_on(write).unwrap_err();
    assert_eq!(code(&error), Some("operation-cancelled"), "{error:?}");
    assert!(sent(&thread).is_empty());
    let value = block_on(form.value(text_ref())).unwrap();
    assert_eq!(value, Some(FormValueInput::Value { value: Some("Ada".into()) }));
}

/// A native control: the `select` of each `focus` it was asked, whether it
/// takes the cursor, and whether its `focus` took it.
struct FocusTarget(Mutex<Vec<bool>>, bool, bool);
impl FocusTarget {
    fn taking() -> Arc<Self> {
        Arc::new(FocusTarget(Mutex::new(Vec::new()), true, true))
    }
}
impl epdf_plugin_form::ffi::FormFocusTarget for FocusTarget {
    fn takes_cursor(&self) -> bool {
        self.1
    }
    fn focus(&self, select: bool) -> bool {
        self.0.lock().unwrap().push(select);
        self.2
    }
}
#[test]
fn a_native_focus_target_receives_selection_and_unregisters() {
    let (_thread, form) = handle();
    let events = Arc::new(Events::default());
    let _subscription = form.subscribe(events.clone());
    let target = FocusTarget::taking();
    let widget = epdf_ffi::AnnotationRef::ObjectNumber {
        page: PageRef { page_object_number: 1 },
        annot_object_number: 101,
    };
    let registration = block_on(form.attach_focus_target(widget.clone(), target.clone())).unwrap();
    let reference = FormFieldRef::ObjectNumber { field_object_number: 1 };
    assert!(
        block_on(form.focus(
            reference.clone(),
            Some(epdf_plugin_form::ffi::FormFocusOptions { select: true, widget: None })
        ))
        .unwrap()
    );
    assert_eq!(*target.0.lock().unwrap(), vec![true]);
    block_on(form.notify_widget_event(
        reference.clone(),
        widget,
        epdf_plugin_form::ffi::FormWidgetEvent::Focus,
    ))
    .unwrap();
    assert_eq!(block_on(form.focused_field()).unwrap().unwrap().name, "name");
    assert!(events.0.lock().unwrap().iter().any(|event|matches!(event,FormEvent::FocusChanged {field:Some(field),widget:Some(_)} if field.name=="name")));
    registration.unregister();
    assert!(!block_on(form.focus(reference, None)).unwrap());
}

#[test]
fn open_form_payloads_reject_invalid_objects_without_mutating_and_describe_field_settings() {
    let (thread, form) = handle();
    for bad in ["{", "[]", "null", "42"] {
        for result in [
            block_on(form.create_draft(bad.into(), CancelToken::new())).map(|_| ()),
            block_on(form.update_patch(text_ref(), bad.into(), CancelToken::new())).map(|_| ()),
            block_on(form.add_widget_placement(text_ref(), bad.into(), CancelToken::new()))
                .map(|_| ()),
        ] {
            assert_eq!(code(&result.unwrap_err()), Some("invalid-input"));
        }
    }
    assert!(sent(&thread).is_empty());
    let panel = block_on(form.field_settings(text_ref())).unwrap().unwrap();
    assert!(
        panel
            .properties
            .iter()
            .any(|property| property.key == "maxLength" && property.min == Some(1.0))
    );
    assert_eq!(serde_json::from_str::<Value>(&panel.values_json).unwrap()["maxLength"], 40);
    assert!(
        block_on(form.field_settings(FormFieldRef::Fqn { name: "missing".into() }))
            .unwrap()
            .is_none()
    );
    block_on(thread.call(|k| k.close())).unwrap().unwrap();
    assert_eq!(
        code(&block_on(form.field_settings(text_ref())).unwrap_err()),
        Some("instance-closed")
    );
}

#[test]
fn duplicating_a_field_sends_one_forms_copy_op() {
    let (thread, form) = handle();
    block_on(form.refresh(CancelToken::new())).unwrap();
    let options =
        FormDuplicateOptions { offset: Some(Point { x: 0.0, y: 30.0 }), ..Default::default() };
    let result = block_on(form.duplicate(vec![text_ref()], options, CancelToken::new())).unwrap();
    let ops = sent(&thread);
    assert_eq!(ops.len(), 1, "{ops:?}");
    let op = &ops[0][0];
    assert_eq!(op["type"], "forms.copy");
    assert_eq!(op["fields"], json!([{ "kind": "objectNumber", "objectNumber": 1 }]));
    assert_eq!(op["offset"], json!({ "x": 0.0, "y": 30.0 }));
    assert_eq!(op["values"], false);
    assert_eq!(result.fields.len(), 1, "{result:?}");
    assert_eq!(result.fields[0].name, "name_2");
    let widget = AnnotationRef::ObjectNumber {
        page: PageRef { page_object_number: 1 },
        annot_object_number: op["objectNumbers"][1].as_u64().unwrap() as u32,
    };
    assert_eq!(result.widgets, vec![widget]);
    assert_eq!(result.dropped_json, "[]");
}

#[test]
fn duplicating_no_fields_sends_nothing() {
    let (thread, form) = handle();
    block_on(form.refresh(CancelToken::new())).unwrap();
    let result =
        block_on(form.duplicate(vec![], FormDuplicateOptions::default(), CancelToken::new()))
            .unwrap();
    assert!(result.fields.is_empty() && result.widgets.is_empty());
    assert!(sent(&thread).is_empty());
}

#[test]
fn duplicating_a_widget_the_form_does_not_show_is_not_found() {
    let (thread, form) = handle();
    block_on(form.refresh(CancelToken::new())).unwrap();
    let missing = AnnotationRef::ObjectNumber {
        page: PageRef { page_object_number: 1 },
        annot_object_number: 999,
    };
    let error = block_on(form.duplicate_widgets(
        vec![missing],
        FormDuplicateOptions::default(),
        CancelToken::new(),
    ))
    .unwrap_err();
    assert_eq!(code(&error), Some("not-found"), "{error:?}");
    assert!(sent(&thread).is_empty());
}

/// A form whose engine holds every `apply` until it is settled or aborted.
fn handle_deferring_apply() -> (KernelThread<TestKernel>, Arc<FormHandle>) {
    let thread = KernelThread::spawn(|_| {
        let mut engine = spec().engine;
        engine["documents"]["a"]["responses"] = json!([{ "path": "apply", "defer": "copy" }]);
        let spec = TestSpec::new(engine).respond("forms.list", |_| {
            Ok(Reply::value(json!({
                "formKind": "acroform", "needsAppearances": false, "calculationOrder": [],
                "fields": [text("Ada")], "widgets": [row()],
            })))
        });
        TestKernel::open(
            vec![
                interaction_plugin(InteractionConfig::default()),
                form_plugin(FormConfig { validation: Some(FormValidation::None) }.into()),
            ],
            spec,
        )
    })
    .unwrap();
    let post = thread.post();
    let form = block_on(thread.call(move |k| FormHandle::new(k.capability(&FORM).unwrap(), post)))
        .unwrap();
    (thread, form)
}

// Only the plugin's own signal ends these calls: the export adds no
// rejection of its own, so a cancel that didn't reach the plugin would
// leave the call waiting on the engine.
#[test]
fn cancelling_a_duplicate_while_the_engine_copies_ends_it() {
    use std::future::Future;
    use std::task::{Context, Poll};
    for widgets in [false, true] {
        let (thread, form) = handle_deferring_apply();
        block_on(form.refresh(CancelToken::new())).unwrap();
        let cancel = CancelToken::new();
        let options = FormDuplicateOptions::default();
        let mut call: std::pin::Pin<Box<dyn Future<Output = _>>> = if widgets {
            let widget = AnnotationRef::ObjectNumber {
                page: PageRef { page_object_number: 1 },
                annot_object_number: 101,
            };
            Box::pin(form.duplicate_widgets(vec![widget], options, cancel.clone()))
        } else {
            Box::pin(form.duplicate(vec![text_ref()], options, cancel.clone()))
        };
        let mut cx = Context::from_waker(futures::task::noop_waker_ref());
        assert!(call.as_mut().poll(&mut cx).is_pending());
        let copying =
            block_on(thread.call(|k| k.engine.log_since(0).iter().any(|e| e["path"] == "apply")))
                .unwrap();
        assert!(copying, "widgets {widgets}: the copy never reached the engine");
        cancel.cancel();
        block_on(thread.call(|_| ())).unwrap();
        match call.as_mut().poll(&mut cx) {
            Poll::Ready(Err(error)) => {
                assert_eq!(code(&error), Some("operation-cancelled"), "widgets {widgets}")
            }
            other => panic!("widgets {widgets}: the cancel did not end the call: {other:?}"),
        }
    }
}

#[test]
fn a_duplicate_offset_that_is_not_finite_is_invalid_input() {
    let (thread, form) = handle();
    block_on(form.refresh(CancelToken::new())).unwrap();
    let options =
        FormDuplicateOptions { offset: Some(Point { x: f64::NAN, y: 0.0 }), ..Default::default() };
    let error = block_on(form.duplicate(vec![text_ref()], options.clone(), CancelToken::new()))
        .unwrap_err();
    assert_eq!(code(&error), Some("invalid-input"), "{error:?}");
    let widget = AnnotationRef::ObjectNumber {
        page: PageRef { page_object_number: 1 },
        annot_object_number: 101,
    };
    let error =
        block_on(form.duplicate_widgets(vec![widget], options, CancelToken::new())).unwrap_err();
    assert_eq!(code(&error), Some("invalid-input"), "{error:?}");
    assert!(sent(&thread).is_empty());
}

// ── CU-C8: the 2026-11-27 catch-up's surface ────────────────────────────────

/// The text field again, for Dana's group, with a purpose, a date format
/// and its widget turned a quarter: a 100 by 24 frame standing as 24 by 100.
fn grouped() -> (Value, Value) {
    let mut field = text("Ada");
    field["groupId"] = json!("dana");
    field["purpose"] = json!("date-signed");
    field["format"] = json!({ "kind": "date", "pattern": "yyyy-mm-dd" });
    let mut widget = row();
    widget["rect"] = json!({ "x": 48, "y": -18, "width": 24, "height": 100 });
    widget["box"] = json!({ "x": 10, "y": 20, "width": 100, "height": 24 });
    widget["rotation"] = json!(90);
    (field, widget)
}

/// A form of the one grouped field, whose engine records each `apply`'s ops
/// and answers with no items. The session may put a field in any group.
fn grouped_handle() -> (KernelThread<TestKernel>, Arc<FormHandle>) {
    grouped_handle_with(true)
}

/// [`grouped_handle`], with the engine's answer to `allowsField("set-group")`.
fn grouped_handle_with(may_set_group: bool) -> (KernelThread<TestKernel>, Arc<FormHandle>) {
    let thread = KernelThread::spawn(move |_| {
        let mut engine = spec().engine;
        engine["documents"]["a"]["security"]["allowsField"] = json!({ "set-group": may_set_group });
        let spec = TestSpec::new(engine)
            .respond("forms.list", |_| {
                let (field, widget) = grouped();
                Ok(Reply::value(json!({
                    "formKind": "acroform", "needsAppearances": false, "calculationOrder": [],
                    "fields": [field], "widgets": [widget],
                })))
            })
            .respond("apply", |args| {
                SENT.with(|s| s.borrow_mut().push(args[0]["ops"].clone()));
                let meta = json!({ "affectedPages": [], "cacheDelta": null,
                    "opId": args[1]["opId"], "undoable": true });
                Ok(Reply::value(json!({ "items": [], "meta": meta })))
            });
        TestKernel::open(
            vec![
                interaction_plugin(InteractionConfig::default()),
                form_plugin(FormConfig { validation: Some(FormValidation::None) }.into()),
            ],
            spec,
        )
    })
    .unwrap();
    let post = thread.post();
    let form = block_on(thread.call(move |k| FormHandle::new(k.capability(&FORM).unwrap(), post)))
        .unwrap();
    block_on(form.refresh(CancelToken::new())).unwrap();
    (thread, form)
}

fn widget_101() -> AnnotationRef {
    AnnotationRef::ObjectNumber {
        page: PageRef { page_object_number: 1 },
        annot_object_number: 101,
    }
}

#[test]
fn a_field_reads_its_purpose_and_format() {
    let (_thread, form) = grouped_handle();
    let field = block_on(form.field(text_ref())).unwrap().unwrap();
    assert_eq!(field.purpose.as_deref(), Some("date-signed"));
    assert_eq!(field.format, Some(FormFieldFormat::Date { pattern: "yyyy-mm-dd".into() }));
    assert_eq!(field.group_id.as_deref(), Some("dana"));
    // A field without them reads none.
    let (_thread, plain) = handle();
    let field = block_on(plain.field(text_ref())).unwrap().unwrap();
    assert_eq!((field.purpose, field.format), (None, None));
}

#[test]
fn a_created_field_sends_its_purpose_format_and_group() {
    let (thread, form) = grouped_handle();
    let input = FormCreateFieldInput {
        family: epdf_ffi::FormFieldFamily::Text,
        name: "signed_on".into(),
        page: PageRef { page_object_number: 1 },
        bounds: epdf_ffi::Rect { x: 10.0, y: 200.0, width: 120.0, height: 24.0 },
        options: None,
        export_value: None,
        purpose: Some("date-signed".into()),
        format: Some(FormFieldFormat::Time { pattern: "HH:MM".into() }),
        group_id: Some("dana".into()),
    };
    let _ = block_on(form.create(input, CancelToken::new()));
    let ops = sent(&thread);
    let draft = &ops.last().expect("an apply")[0]["draft"];
    assert_eq!(draft["purpose"], "date-signed", "{ops:?}");
    assert_eq!(draft["format"], json!({ "kind": "time", "pattern": "HH:MM" }));
    assert_eq!(draft["groupId"], "dana");
}

#[test]
fn a_patch_sets_and_clears_purpose_format_and_group() {
    let (thread, form) = grouped_handle();
    let patch = FormFieldPatch {
        purpose: Some("email".into()),
        format: Some(FormFieldFormat::Date { pattern: "dd.mm.yyyy".into() }),
        ..Default::default()
    };
    let _ = block_on(form.update(text_ref(), patch, CancelToken::new()));
    let clear = FormFieldPatch {
        clear: vec![
            FormFieldPatchKey::Purpose,
            FormFieldPatchKey::Format,
            FormFieldPatchKey::GroupId,
        ],
        ..Default::default()
    };
    let _ = block_on(form.update(text_ref(), clear, CancelToken::new()));
    let ops = sent(&thread);
    let patches: Vec<&Value> = ops.iter().map(|o| &o[0]["patch"]).collect();
    assert_eq!(
        patches,
        [
            &json!({ "purpose": "email", "format": { "kind": "date", "pattern": "dd.mm.yyyy" } }),
            &json!({ "purpose": null, "format": null, "groupId": null }),
        ],
        "{ops:?}"
    );
    // Setting and clearing one key is refused before anything is sent.
    let both = FormFieldPatch {
        group_id: Some("dana".into()),
        clear: vec![FormFieldPatchKey::GroupId],
        ..Default::default()
    };
    let error = block_on(form.update(text_ref(), both, CancelToken::new())).unwrap_err();
    assert_eq!(code(&error), Some("invalid-input"));
    assert_eq!(sent(&thread).len(), 2);
}

#[test]
fn a_turned_grouped_widget_fills_with_its_frame_turn_and_accent() {
    let (_thread, form) = grouped_handle();
    let items = block_on(form.widgets(PageRef { page_object_number: 1 })).unwrap();
    let item = &items[0];
    assert_eq!(item.bounds, epdf_ffi::Rect { x: 48.0, y: -18.0, width: 24.0, height: 100.0 });
    assert_eq!(item.frame, epdf_ffi::Rect { x: 10.0, y: 20.0, width: 100.0, height: 24.0 });
    assert_eq!(item.rotation, 90.0);
    assert_eq!(item.group_id.as_deref(), Some("dana"));
    assert!(item.can_fill && !item.required && !item.empty, "{item:?}");
    // The group the field is in shows in the first of the eight colors, at
    // the default tint.
    assert_eq!(
        item.accent,
        Some(epdf_ffi::RecordAccent { color: "#ea580c".into(), strength: 0.16 })
    );
    let hit = block_on(form.widget_at(PageRef { page_object_number: 1 }, 60.0, 30.0))
        .unwrap()
        .expect("a hit inside the standing rect");
    assert_eq!((hit.bounds, hit.frame, hit.rotation), (item.bounds, item.frame, 90.0));
    // At tint 0 no field shows its group.
    block_on(form.update_settings(FormSettingsPatch {
        fields: Some(FormFieldSettingsPatch { tint: Some(0.0), ..Default::default() }),
        ..Default::default()
    }))
    .unwrap();
    assert_eq!(block_on(form.settings()).unwrap().fields.tint, 0.0);
    let items = block_on(form.widgets(PageRef { page_object_number: 1 })).unwrap();
    assert_eq!(items[0].accent, None);
    let error = block_on(form.update_settings(FormSettingsPatch {
        fields: Some(FormFieldSettingsPatch { tint: Some(f64::NAN), ..Default::default() }),
        ..Default::default()
    }))
    .unwrap_err();
    assert_eq!(code(&error), Some("invalid-input"));
}

#[test]
fn groups_are_named_listed_announced_and_moved_into() {
    let (thread, form) = grouped_handle();
    let events = Arc::new(Events::default());
    let _subscription = form.subscribe(events.clone());
    // Before the app names any, the group the field is in is listed.
    assert_eq!(
        block_on(form.groups()).unwrap(),
        [FormGroup { id: "dana".into(), label: None, color: "#ea580c".into() }]
    );
    block_on(form.set_groups(vec![
        FormGroupInput { id: "ari".into(), label: Some("Ari Cole".into()), color: None },
        FormGroupInput {
            id: "dana".into(),
            label: Some("Dana Smith".into()),
            color: Some("var(--signer-2)".into()),
        },
    ]))
    .unwrap();
    let groups = block_on(form.groups()).unwrap();
    assert_eq!(
        groups,
        [
            FormGroup { id: "ari".into(), label: Some("Ari Cole".into()), color: "#ea580c".into() },
            FormGroup {
                id: "dana".into(),
                label: Some("Dana Smith".into()),
                color: "var(--signer-2)".into()
            },
        ]
    );
    // The label says whose the field is; the accent takes the app's color.
    let items = block_on(form.widgets(PageRef { page_object_number: 1 })).unwrap();
    assert!(items[0].label.ends_with(", Dana Smith"), "{:?}", items[0].label);
    assert_eq!(items[0].accent.as_ref().map(|a| a.color.as_str()), Some("var(--signer-2)"));
    assert_eq!(block_on(form.design_group()).unwrap(), None);
    assert!(block_on(form.can_set_group("ari".into())).unwrap());
    block_on(form.set_design_group(Some("ari".into()))).unwrap();
    assert_eq!(block_on(form.design_group()).unwrap().as_deref(), Some("ari"));
    block_on(form.set_design_group(None)).unwrap();
    {
        let events = events.0.lock().unwrap();
        assert!(
            events
                .iter()
                .any(|e| matches!(e, FormEvent::GroupsChanged { groups: g } if *g == groups)),
            "{events:?}"
        );
        let design: Vec<_> = events
            .iter()
            .filter_map(|e| match e {
                FormEvent::DesignGroupChanged { group_id } => Some(group_id.clone()),
                _ => None,
            })
            .collect();
        assert_eq!(design, [Some("ari".to_string()), None]);
    }
    let moved =
        block_on(form.move_to_group(vec![text_ref()], Some("ari".into()), CancelToken::new()))
            .unwrap();
    assert_eq!(moved.len(), 1);
    let ops = sent(&thread);
    assert_eq!(
        ops.last().unwrap()[0],
        json!({ "type": "forms.update", "field": { "kind": "objectNumber", "objectNumber": 1 },
            "patch": { "groupId": "ari" } }),
        "{ops:?}"
    );
    // Nothing is selected without the annotation plugin.
    assert!(block_on(form.selected_fields()).unwrap().is_empty());
}

#[test]
fn a_widget_anchors_where_it_stands_and_says_when_it_has_the_cursor() {
    let (_thread, form) = grouped_handle();
    let anchor = block_on(form.widget_anchor(widget_101())).unwrap().expect("an anchor");
    assert_eq!(anchor.page, PageRef { page_object_number: 1 });
    assert_eq!(anchor.bounds, epdf_ffi::Rect { x: 48.0, y: -18.0, width: 24.0, height: 100.0 });
    let missing = AnnotationRef::ObjectNumber {
        page: PageRef { page_object_number: 1 },
        annot_object_number: 999,
    };
    assert_eq!(block_on(form.widget_anchor(missing)).unwrap(), None);
    assert!(!block_on(form.is_widget_focused(widget_101())).unwrap());
    let target = FocusTarget::taking();
    let _registration = block_on(form.attach_focus_target(widget_101(), target)).unwrap();
    assert!(block_on(form.focus(text_ref(), None)).unwrap());
    // The control reports that it took the cursor.
    block_on(form.notify_widget_event(
        text_ref(),
        widget_101(),
        epdf_plugin_form::ffi::FormWidgetEvent::Focus,
    ))
    .unwrap();
    assert!(block_on(form.is_widget_focused(widget_101())).unwrap());
}

#[test]
fn a_group_the_session_may_not_set_is_refused() {
    let (thread, form) = grouped_handle_with(false);
    assert!(!block_on(form.can_set_group("ari".into())).unwrap());
    let error = block_on(form.set_design_group(Some("ari".into()))).unwrap_err();
    assert_eq!(code(&error), Some("permission-denied"), "{error:?}");
    let error =
        block_on(form.move_to_group(vec![text_ref()], Some("ari".into()), CancelToken::new()))
            .unwrap_err();
    assert_eq!(code(&error), Some("permission-denied"), "{error:?}");
    // Out of every group takes form design alone.
    block_on(form.set_design_group(None)).unwrap();
    let _ = block_on(form.move_to_group(vec![text_ref()], None, CancelToken::new()));
    let ops = sent(&thread);
    assert_eq!(ops.len(), 1, "{ops:?}");
    assert_eq!(ops[0][0]["patch"], json!({ "groupId": null }));
}

/// CU-C5-D4: a native control that holds nothing to focus (a signature
/// field nobody here can sign) says so, and the cursor passes over it.
#[test]
fn a_native_target_that_takes_no_cursor_is_passed_over() {
    let (_thread, form) = grouped_handle();
    let declining = Arc::new(FocusTarget(Mutex::new(Vec::new()), false, true));
    let registration = block_on(form.attach_focus_target(widget_101(), declining.clone())).unwrap();
    assert!(!block_on(form.focus(text_ref(), None)).unwrap());
    assert!(!block_on(form.focus_next(None)).unwrap());
    assert!(declining.0.lock().unwrap().is_empty(), "focus was asked of a target that takes none");
    assert_eq!(block_on(form.focused_field()).unwrap(), None);
    registration.unregister();
    // A target that takes it, but whose focus finds nothing to put it in.
    let empty = Arc::new(FocusTarget(Mutex::new(Vec::new()), true, false));
    let _registration = block_on(form.attach_focus_target(widget_101(), empty.clone())).unwrap();
    assert!(!block_on(form.focus(text_ref(), None)).unwrap());
    assert_eq!(*empty.0.lock().unwrap(), vec![false]);
}
