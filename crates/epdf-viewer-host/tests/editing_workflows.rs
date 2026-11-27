mod common;

use common::{NullDelegate, options};
use epdf_ffi::{CancelToken, FormFieldFamily, Point, Rect, ViewerError};
use epdf_plugin_annotation::ffi::{
    AnnotationAttachmentFile, AnnotationStampPlacement, AnnotationStampSource,
};
use epdf_plugin_form::ffi::{
    FormCalculationPosition, FormConfig, FormCreateFieldInput, FormWidgetPlacement,
};
use epdf_viewer_host::{SaveMode, ViewerConfig, ViewerHost};
use futures::executor::block_on;
use serde_json::{Value, json};
use std::sync::Arc;

fn config() -> ViewerConfig {
    ViewerConfig {
        interaction: true,
        annotation: Some(String::new()),
        form: Some(FormConfig::default()),
        ..Default::default()
    }
}

fn host(config: ViewerConfig) -> Arc<ViewerHost> {
    let host = ViewerHost::new(config, Arc::new(NullDelegate)).unwrap();
    block_on(host.start()).unwrap();
    host
}

fn value(text: &str) -> Value {
    serde_json::from_str(text).unwrap()
}

#[test]
fn absent_plugins_and_permission_refusals_are_truthful() {
    let missing = host(ViewerConfig::default());
    let d = block_on(missing.open_document(test_support::fixture(test_support::REPORT), options()))
        .unwrap();
    assert!(
        matches!(block_on(missing.annotation(d.id.clone())),Err(ViewerError::Kernel {code:Some(code),..}) if code == "unsupported")
    );
    assert!(
        matches!(block_on(missing.form(d.id)),Err(ViewerError::Kernel {code:Some(code),..}) if code == "unsupported")
    );
    let host = host(ViewerConfig {
        scope: Some(vec!["doc.open".into(), "doc.forms.read".into()]),
        ..config()
    });
    let d = block_on(host.open_document(test_support::fixture(test_support::REPORT), options()))
        .unwrap();
    let annotations = block_on(host.annotation(d.id.clone())).unwrap();
    let form = block_on(host.form(d.id.clone())).unwrap();
    let page = block_on(host.pages(d.id)).unwrap()[0].page;
    let widget = epdf_ffi::AnnotationRef::ObjectNumber { page, annot_object_number: 42 };
    let denied = |error| {
        assert!(
            matches!(error,ViewerError::Kernel {code:Some(code),..} if code == "permission-denied")
        )
    };
    denied(block_on(annotations.export(None, None, CancelToken::new())).unwrap_err());
    denied(block_on(annotations.import(json!({"format":"embedpdf/annotations","version":1,"pages":[],"items":[],"resources":{}}).to_string(),"{}".into(),CancelToken::new())).unwrap_err());
    denied(
        block_on(form.update_widget(widget.clone(), "{}".into(), CancelToken::new())).unwrap_err(),
    );
    denied(block_on(form.delete_widget(widget, CancelToken::new())).unwrap_err());
    denied(
        block_on(form.reorder_calculations(
            vec![],
            FormCalculationPosition::Start,
            CancelToken::new(),
        ))
        .unwrap_err(),
    );
}

#[test]
fn selected_link_update_commits_the_selection_and_clear_without_crossing_documents() {
    let host = host(config());
    let a = block_on(host.open_document(test_support::fixture(test_support::REPORT), options()))
        .unwrap();
    let b = block_on(host.open_document(test_support::fixture(test_support::REPORT), options()))
        .unwrap();
    let annotation = block_on(host.annotation(a.id.clone())).unwrap();
    let other = block_on(host.annotation(b.id)).unwrap();
    let page = block_on(host.pages(a.id.clone())).unwrap()[0].page;
    let square = block_on(annotation.create(
        page,
        json!({"subtype":"square","box":{"x":30,"y":60,"width":40,"height":20}}).to_string(),
        None,
        None,
        CancelToken::new(),
    ))
    .unwrap();
    block_on(annotation.selection().select(vec![square.clone()], None)).unwrap();
    let result = block_on(annotation.selection().update_link(
        Some(epdf_ffi::PdfLinkTarget::Uri { uri: "https://example.test/selected".into() }),
        CancelToken::new(),
    ))
    .unwrap();
    assert!(result.failed.is_empty());
    assert_eq!(
        value(&block_on(annotation.threads().link(square.clone())).unwrap().unwrap())["uri"],
        "https://example.test/selected"
    );
    assert!(block_on(other.selected()).unwrap().is_empty());
    block_on(annotation.selection().update_link(None, CancelToken::new())).unwrap();
    assert!(block_on(annotation.threads().link(square)).unwrap().is_none());
    block_on(host.close_document(a.id)).unwrap();
    assert!(block_on(annotation.selection().update_link(None, CancelToken::new())).is_err());
}

#[test]
fn annotation_bundles_copy_mixed_resources_and_typed_identities_between_documents() {
    use base64::Engine as _;
    let host = host(config());
    let a = block_on(host.open_document(test_support::fixture(test_support::REPORT), options()))
        .unwrap();
    let b = block_on(host.open_document(test_support::fixture(test_support::REPORT), options()))
        .unwrap();
    let source = block_on(host.annotation(a.id.clone())).unwrap();
    let target = block_on(host.annotation(b.id.clone())).unwrap();
    let page = block_on(host.pages(a.id.clone())).unwrap()[0].page;
    let file_bytes = vec![0, 255, 128, 1, 0, 240];
    let file = block_on(source.drafts().create_attachment(
        page,
        Point { x: 40.0, y: 40.0 },
        AnnotationAttachmentFile {
            bytes: file_bytes.clone(),
            name: "résumé.bin".into(),
            mime_type: None,
            description: None,
        },
        CancelToken::new(),
    ))
    .unwrap();
    let png = base64::engine::general_purpose::STANDARD.decode("iVBORw0KGgoAAAANSUhEUgAAAB4AAAAKCAIAAAAsFXl4AAAAGklEQVR42mP4z8CABzFQIj1q9KjRo0aTIg0AQXcq5OunV0AAAAAASUVORK5CYII=").unwrap();
    let stamp = block_on(source.drafts().place_stamp(
        AnnotationStampSource {
            bytes: png,
            mime_type: Some("image/png".into()),
            name: None,
            subject: None,
        },
        AnnotationStampPlacement { page, center: Point { x: 150.0, y: 100.0 }, rotation: None },
        Some(60.0),
        CancelToken::new(),
    ))
    .unwrap();
    let square = block_on(source.create(
        page,
        json!({"subtype":"square","box":{"x":30,"y":60,"width":40,"height":20}}).to_string(),
        None,
        None,
        CancelToken::new(),
    ))
    .unwrap();
    let empty = value(&block_on(source.export(Some(vec![]), None, CancelToken::new())).unwrap());
    assert_eq!(empty["items"], json!([]));
    let refs_only = value(
        &block_on(source.export(Some(vec![square.clone()]), Some(vec![]), CancelToken::new()))
            .unwrap(),
    );
    assert_eq!(refs_only["items"].as_array().unwrap().len(), 1);
    assert_eq!(
        epdf_ffi::AnnotationRef::from_json(&refs_only["items"][0]["data"]["ref"]).unwrap(),
        square
    );
    let page_only = value(
        &block_on(source.export(Some(vec![]), Some(vec![page]), CancelToken::new())).unwrap(),
    );
    for reference in [&file, &stamp, &square] {
        assert!(page_only["items"].as_array().unwrap().iter().any(|item| {
            epdf_ffi::AnnotationRef::from_json(&item["data"]["ref"]).unwrap() == *reference
        }));
    }
    let text = block_on(source.export(
        Some(vec![file.clone(), stamp.clone(), square.clone()]),
        None,
        CancelToken::new(),
    ))
    .unwrap();
    assert!(!text.contains("$host"));
    let bundle = value(&text);
    assert_eq!(bundle["items"].as_array().unwrap().len(), 3);
    let resources = bundle["resources"].as_object().unwrap();
    assert_eq!(resources.len(), 2);
    assert!(
        resources
            .values()
            .any(|r| epdf_kernel::bytes::Bytes::from_json(r).unwrap().0 == file_bytes)
    );
    let before = block_on(target.list(None)).unwrap();
    let imported = block_on(target.import(
        text.clone(),
        json!({"pages":"by-position","attribution":"stamp"}).to_string(),
        CancelToken::new(),
    ))
    .unwrap();
    assert_eq!(imported.annotations.len(), 3);
    assert_eq!(imported.ref_map.len(), 3);
    assert_eq!(value(&imported.dropped_json), json!([]));
    assert!(imported.ref_map.iter().any(|m| m.from == file));
    for annotation in &imported.annotations {
        assert!(block_on(target.get(annotation.annotation_ref.clone())).unwrap().is_some());
    }
    assert_eq!(block_on(target.list(None)).unwrap().len(), before.len() + 3);
    assert!(block_on(source.get(square)).unwrap().is_some());
    let copied = value(
        &block_on(target.export(
            Some(imported.annotations.iter().map(|a| a.annotation_ref.clone()).collect()),
            None,
            CancelToken::new(),
        ))
        .unwrap(),
    );
    assert_eq!(copied["resources"], bundle["resources"]);
    let saved =
        block_on(host.save(b.id.clone(), SaveMode::Incremental, CancelToken::new())).unwrap();
    let reopened = block_on(host.open_document(saved, options())).unwrap();
    let reopened = block_on(host.annotation(reopened.id)).unwrap();
    let again = value(
        &block_on(reopened.export(
            Some(imported.annotations.iter().map(|a| a.annotation_ref.clone()).collect()),
            None,
            CancelToken::new(),
        ))
        .unwrap(),
    );
    assert_eq!(again["resources"], bundle["resources"]);
    let cancel = CancelToken::new();
    cancel.cancel();
    assert!(block_on(source.export(None, None, cancel)).is_err());
    block_on(host.close_document(a.id)).unwrap();
    assert!(
        matches!(block_on(source.export(None, None, CancelToken::new())), Err(ViewerError::Kernel { code:Some(code),..}) if code == "instance-closed")
    );
    assert!(block_on(source.import(text, "{}".into(), CancelToken::new())).is_err());
    assert!(block_on(target.export(Some(vec![]), None, CancelToken::new())).is_ok());
}

#[test]
fn form_widget_changes_and_calculation_order_survive_save_and_reopen() {
    let host = host(config());
    let a = block_on(host.open_document(test_support::fixture(test_support::REPORT), options()))
        .unwrap();
    let b = block_on(host.open_document(test_support::fixture(test_support::REPORT), options()))
        .unwrap();
    let form = block_on(host.form(a.id.clone())).unwrap();
    let other = block_on(host.form(b.id)).unwrap();
    let page = block_on(host.pages(a.id.clone())).unwrap()[0].page;
    block_on(form.refresh(CancelToken::new())).unwrap();
    let make = |name: &str| {
        block_on(form.create(
            FormCreateFieldInput {
                family: FormFieldFamily::Text,
                name: name.into(),
                page,
                bounds: Rect { x: 20.0, y: 30.0, width: 140.0, height: 24.0 },
                options: None,
                export_value: None,
                purpose: None,
                format: None,
                group_id: None,
            },
            CancelToken::new(),
        ))
        .unwrap()
    };
    let first = make("first");
    let second = make("second");
    // Existing interchange carries the engine's open action payload.
    let mut bundle = value(
        &block_on(form.export(
            Some(vec![first.field_ref.clone(), second.field_ref.clone()]),
            CancelToken::new(),
        ))
        .unwrap(),
    );
    for (index, field) in bundle["fields"].as_array_mut().unwrap().iter_mut().enumerate() {
        field["data"]["name"] = json!(format!("copy_{index}"));
        field["data"]["actions"] = json!({"calculate":{"root":{"type":"javascript","subtype":"JavaScript","script":"event.value = 1","next":[]},"incomplete":false,"warnings":[]}});
    }
    bundle["calculationOrder"] =
        json!([bundle["fields"][0]["data"]["ref"], bundle["fields"][1]["data"]["ref"]]);
    let imported =
        block_on(form.import(bundle.to_string(), "{}".into(), CancelToken::new())).unwrap();
    assert_eq!(imported.fields.len(), 2, "{imported:?}");
    assert_eq!(
        block_on(form.snapshot()).unwrap().unwrap().calculation_order.len(),
        2,
        "{imported:?}"
    );
    let f = &imported.fields[0];
    let g = &imported.fields[1];
    let widget = f.widgets[0].annotation_ref.clone().unwrap();
    let annotations = block_on(host.annotation(a.id.clone())).unwrap();
    let interaction = block_on(host.interaction(a.id.clone())).unwrap();
    block_on(interaction.activate_tool("form-edit".into())).unwrap();
    block_on(annotations.selection().select(vec![widget.clone()], None)).unwrap();
    assert_eq!(block_on(form.selected_field()).unwrap().unwrap().field_ref, f.field_ref);
    block_on(annotations.selection().clear()).unwrap();
    assert_eq!(block_on(form.field_widgets(f.field_ref.clone())).unwrap().len(), 1);
    let changed = block_on(form.update_widget(
        widget.clone(),
        json!({"rect":{"x":60,"y":90,"width":160,"height":28},"color":"#123456"}).to_string(),
        CancelToken::new(),
    ))
    .unwrap();
    assert_eq!(changed.bounds.x, 60.0);
    assert_eq!(block_on(form.widget(widget.clone())).unwrap(), Some(changed));
    let added = block_on(form.add_widget(
        f.field_ref.clone(),
        FormWidgetPlacement {
            page: Some(page),
            bounds: Some(Rect { x: 70.0, y: 130.0, width: 120.0, height: 24.0 }),
            export_value: None,
        },
        CancelToken::new(),
    ))
    .unwrap();
    let extra = epdf_ffi::AnnotationRef::from_json(&value(&added.widget_json)["ref"]).unwrap();
    let remaining =
        block_on(form.delete_widget(extra.clone(), CancelToken::new())).unwrap().unwrap();
    assert_eq!(remaining.widgets.len(), 1);
    assert!(block_on(form.widget(extra)).unwrap().is_none());
    let order = block_on(form.reorder_calculations(
        vec![g.field_ref.clone()],
        FormCalculationPosition::Start,
        CancelToken::new(),
    ))
    .unwrap();
    assert_eq!(order, vec![Some(g.field_ref.clone()), Some(f.field_ref.clone())]);
    assert_eq!(
        block_on(form.reorder_calculations(
            vec![g.field_ref.clone()],
            FormCalculationPosition::After { field: f.field_ref.clone() },
            CancelToken::new()
        ))
        .unwrap(),
        vec![Some(f.field_ref.clone()), Some(g.field_ref.clone())]
    );
    assert_eq!(
        block_on(form.reorder_calculations(
            vec![g.field_ref.clone()],
            FormCalculationPosition::Before { field: f.field_ref.clone() },
            CancelToken::new()
        ))
        .unwrap(),
        order
    );
    assert_eq!(block_on(form.snapshot()).unwrap().unwrap().calculation_order, order);
    assert!(block_on(other.fields(None)).unwrap().is_empty());
    assert!(block_on(form.selected_field()).unwrap().is_none());
    let unknown = epdf_ffi::AnnotationRef::ObjectNumber { page, annot_object_number: 999_999 };
    assert!(block_on(form.widget(unknown.clone())).unwrap().is_none());
    assert!(block_on(form.update_widget(unknown, "[]".into(), CancelToken::new())).is_err());
    let saved =
        block_on(host.save(a.id.clone(), SaveMode::Incremental, CancelToken::new())).unwrap();
    let reopened = block_on(host.open_document(saved, options())).unwrap();
    let saved = block_on(host.form(reopened.id)).unwrap();
    block_on(saved.refresh(CancelToken::new())).unwrap();
    assert_eq!(block_on(saved.snapshot()).unwrap().unwrap().calculation_order, order);
    assert_eq!(block_on(saved.widget(widget.clone())).unwrap().unwrap().bounds.x, 60.0);
    assert_eq!(block_on(saved.field_widgets(f.field_ref.clone())).unwrap().len(), 1);
    block_on(host.close_document(a.id)).unwrap();
    assert!(block_on(form.widget(widget.clone())).is_err());
    assert!(block_on(form.update_widget(widget.clone(), "{}".into(), CancelToken::new())).is_err());
    assert!(block_on(form.delete_widget(widget, CancelToken::new())).is_err());
    assert!(
        block_on(form.reorder_calculations(
            vec![],
            FormCalculationPosition::End,
            CancelToken::new()
        ))
        .is_err()
    );
    assert!(block_on(form.selected_field()).is_err());
    assert!(block_on(form.field_widgets(f.field_ref.clone())).is_err());
}

#[test]
fn direct_form_payloads_preserve_options_scripts_nulls_and_save_reopen() {
    let host = host(config());
    let document =
        block_on(host.open_document(test_support::fixture(test_support::REPORT), options()))
            .unwrap();
    let form = block_on(host.form(document.id.clone())).unwrap();
    block_on(form.refresh(CancelToken::new())).unwrap();
    let page = block_on(host.pages(document.id.clone())).unwrap()[0].page;
    let action = json!({"type":"javascript","script":"event.value = 7","next":[]});
    let field = block_on(form.create_draft(json!({
        "kind":"combobox", "name":"direct_choice", "edit":true,
        "alternateName":"Keep me", "mappingName":"Clear me", "defaultValue":"a",
        "options":[{"label":"Alpha","value":"a"},{"label":"Beta","value":"b"}],
        "actions":{"calculate":action},
        "widgets":[{"page":{"kind":"objectNumber","objectNumber":page.page_object_number},"rect":{"x":20,"y":30,"width":120,"height":24}}]
    }).to_string(), CancelToken::new())).unwrap();
    assert!(
        matches!(&field.detail, epdf_plugin_form::ffi::FormFieldDetail::Combobox { edit:true, options,.. } if options.len()==2)
    );
    assert_eq!(
        value(&field.fields_json)["actions"]["calculate"]["root"]["script"],
        "event.value = 7"
    );
    let updated = block_on(form.update_patch(
        field.field_ref.clone(),
        json!({"mappingName":null,"edit":false,"actions":{"validate":action}}).to_string(),
        CancelToken::new(),
    ))
    .unwrap();
    assert_eq!(updated.alternate_name.as_deref(), Some("Keep me"));
    assert_eq!(updated.mapping_name, None);
    assert_eq!(
        value(&updated.fields_json)["actions"]["validate"]["root"]["script"],
        "event.value = 7"
    );
    let panel = block_on(form.field_settings(field.field_ref.clone())).unwrap().unwrap();
    let choice = panel.properties.iter().find(|property| property.key == "defaultValue").unwrap();
    assert_eq!(choice.free, Some(false));
    assert_eq!(choice.options, Some(vec!["a".into(), "b".into()]));
    let added = block_on(form.add_widget_placement(field.field_ref.clone(),json!({"rect":{"x":20,"y":70,"width":120,"height":24},"fontSize":19,"actions":{"focus":action}}).to_string(),CancelToken::new())).unwrap();
    let widget = value(&added.widget_json);
    assert_eq!(widget["fontSize"].as_f64(), Some(19.0));
    assert_eq!(widget["actions"]["focus"]["root"]["script"], "event.value = 7");
    let saved =
        block_on(host.save(document.id, SaveMode::Incremental, CancelToken::new())).unwrap();
    let reopened = block_on(host.open_document(saved, options())).unwrap();
    let reopened = block_on(host.form(reopened.id)).unwrap();
    block_on(reopened.refresh(CancelToken::new())).unwrap();
    let found = block_on(reopened.field(field.field_ref)).unwrap().unwrap();
    assert_eq!(found.alternate_name.as_deref(), Some("Keep me"));
    assert_eq!(found.mapping_name, None);
    assert_eq!(
        value(&found.fields_json)["actions"]["validate"]["root"]["script"],
        "event.value = 7"
    );
    let widgets = block_on(reopened.field_widgets(found.field_ref)).unwrap();
    assert_eq!(widgets.len(), 2);
    assert!(
        widgets.iter().any(|widget| value(&widget.fields_json)["fontSize"].as_f64() == Some(19.0))
    );
}

#[test]
fn direct_form_payloads_enforce_design_and_script_permissions() {
    for scope in
        [vec!["doc.open", "doc.forms.read"], vec!["doc.open", "doc.forms.read", "doc.forms.modify"]]
    {
        let host = host(ViewerConfig {
            scope: Some(scope.iter().map(|value| (*value).into()).collect()),
            ..config()
        });
        let document =
            block_on(host.open_document(test_support::fixture(test_support::REPORT), options()))
                .unwrap();
        let form = block_on(host.form(document.id)).unwrap();
        block_on(form.refresh(CancelToken::new())).unwrap();
        let action =
            json!({"calculate":{"type":"javascript","script":"event.value = 1","next":[]}});
        let reference = epdf_ffi::FormFieldRef::Fqn { name: "missing".into() };
        for result in [
            block_on(form.create_draft(
                json!({"kind":"text","name":"denied","widgets":[],"actions":action}).to_string(),
                CancelToken::new(),
            ))
            .map(|_| ()),
            block_on(form.update_patch(
                reference.clone(),
                json!({"actions":action}).to_string(),
                CancelToken::new(),
            ))
            .map(|_| ()),
            block_on(form.add_widget_placement(
                reference,
                json!({"actions":action}).to_string(),
                CancelToken::new(),
            ))
            .map(|_| ()),
        ] {
            assert!(
                matches!(result.unwrap_err(),ViewerError::Kernel {code:Some(code),..} if code=="permission-denied")
            );
        }
    }
}

/// CU-C8: the 2026-11-27 catch-up's form surface through the native engine:
/// a field's purpose, format and group written by the typed records, a
/// widget given a quarter turn, the groups the app names, and all of it
/// read back after a save.
#[test]
fn typed_purpose_format_groups_and_a_turned_widget_survive_save_and_reopen() {
    use epdf_plugin_form::ffi::{
        FormFieldFormat, FormFieldPatch, FormFieldPatchKey, FormGroupInput,
    };
    let host = host(config());
    let document =
        block_on(host.open_document(test_support::fixture(test_support::REPORT), options()))
            .unwrap();
    let form = block_on(host.form(document.id.clone())).unwrap();
    block_on(form.refresh(CancelToken::new())).unwrap();
    let page = block_on(host.pages(document.id.clone())).unwrap()[0].page;
    assert!(block_on(form.can_set_group("dana".into())).unwrap());
    let field = block_on(form.create(
        FormCreateFieldInput {
            family: FormFieldFamily::Text,
            name: "signed_on".into(),
            page,
            bounds: Rect { x: 40.0, y: 60.0, width: 120.0, height: 24.0 },
            options: None,
            export_value: None,
            purpose: Some("date-signed".into()),
            format: Some(FormFieldFormat::Date { pattern: "yyyy-mm-dd".into() }),
            group_id: Some("dana".into()),
        },
        CancelToken::new(),
    ))
    .unwrap();
    assert_eq!(field.purpose.as_deref(), Some("date-signed"));
    assert_eq!(field.format, Some(FormFieldFormat::Date { pattern: "yyyy-mm-dd".into() }));
    assert_eq!(field.group_id.as_deref(), Some("dana"));
    // The format is made of the two scripts it names.
    let actions = &value(&field.fields_json)["actions"];
    assert!(actions["format"]["root"]["script"].as_str().is_some_and(|s| s.contains("yyyy-mm-dd")));
    // A format of kind `other` is read, never written.
    let error = block_on(form.update(
        field.field_ref.clone(),
        FormFieldPatch { format: Some(FormFieldFormat::Other), ..Default::default() },
        CancelToken::new(),
    ))
    .unwrap_err();
    assert!(
        matches!(&error, ViewerError::Kernel { code: Some(code), .. } if code == "invalid-input"),
        "{error:?}"
    );
    // The widget turns a quarter: its frame stays, the rect it stands in turns.
    let widget = field.widgets[0].annotation_ref.clone().unwrap();
    block_on(form.update_widget(
        widget.clone(),
        json!({ "rotation": 90 }).to_string(),
        CancelToken::new(),
    ))
    .unwrap();
    let items = block_on(form.widgets(page)).unwrap();
    let item = items.iter().find(|i| i.field_ref == field.field_ref).unwrap();
    assert_eq!(item.rotation, 90.0, "{item:?}");
    assert_eq!((item.frame.width, item.frame.height), (120.0, 24.0));
    assert_eq!((item.bounds.width, item.bounds.height), (24.0, 120.0));
    assert_eq!(item.group_id.as_deref(), Some("dana"));
    let anchor = block_on(form.widget_anchor(widget)).unwrap().unwrap();
    assert_eq!(anchor.bounds, item.bounds);
    block_on(form.set_groups(vec![FormGroupInput {
        id: "dana".into(),
        label: Some("Dana Smith".into()),
        color: Some("#0055aa".into()),
    }]))
    .unwrap();
    let groups = block_on(form.groups()).unwrap();
    assert_eq!(groups.len(), 1);
    assert_eq!(
        (groups[0].label.as_deref(), groups[0].color.as_str()),
        (Some("Dana Smith"), "#0055aa")
    );
    let items = block_on(form.widgets(page)).unwrap();
    let item = items.iter().find(|i| i.field_ref == field.field_ref).unwrap();
    assert_eq!(item.accent.as_ref().map(|a| a.color.as_str()), Some("#0055aa"));
    // The purpose is cleared; the format and the group stay.
    let updated = block_on(form.update(
        field.field_ref.clone(),
        FormFieldPatch { clear: vec![FormFieldPatchKey::Purpose], ..Default::default() },
        CancelToken::new(),
    ))
    .unwrap();
    assert_eq!(updated.purpose, None);
    let saved =
        block_on(host.save(document.id, SaveMode::Incremental, CancelToken::new())).unwrap();
    let reopened = block_on(host.open_document(saved, options())).unwrap();
    let reopened_form = block_on(host.form(reopened.id.clone())).unwrap();
    block_on(reopened_form.refresh(CancelToken::new())).unwrap();
    let found = block_on(reopened_form.field(field.field_ref.clone())).unwrap().unwrap();
    assert_eq!(found.purpose, None);
    assert_eq!(found.format, Some(FormFieldFormat::Date { pattern: "yyyy-mm-dd".into() }));
    assert_eq!(found.group_id.as_deref(), Some("dana"));
    let page = block_on(host.pages(reopened.id)).unwrap()[0].page;
    let items = block_on(reopened_form.widgets(page)).unwrap();
    let item = items.iter().find(|i| i.field_ref == field.field_ref).unwrap();
    assert_eq!(item.rotation, 90.0);
    // The groups are session state: the reopened document has none named.
    assert_eq!(block_on(reopened_form.groups()).unwrap()[0].label, None);
}

/// CU-C8 (CU-C6-D5, #1118, #1141): in design mode the form lends its widgets
/// to the annotation plugin. A lent widget shows its group's accent, selects
/// as its field, offers Orientation, and turns by quarter turns and back.
#[test]
fn a_lent_widget_shows_its_group_accent_and_turns_from_the_selection() {
    use epdf_plugin_form::ffi::FormGroupInput;
    let host = host(config());
    let document =
        block_on(host.open_document(test_support::fixture(test_support::REPORT), options()))
            .unwrap();
    let form = block_on(host.form(document.id.clone())).unwrap();
    block_on(form.refresh(CancelToken::new())).unwrap();
    let page = block_on(host.pages(document.id.clone())).unwrap()[0].page;
    let field = block_on(form.create(
        FormCreateFieldInput {
            family: FormFieldFamily::Text,
            name: "dana_name".into(),
            page,
            bounds: Rect { x: 40.0, y: 60.0, width: 120.0, height: 24.0 },
            options: None,
            export_value: None,
            purpose: None,
            format: None,
            group_id: Some("dana".into()),
        },
        CancelToken::new(),
    ))
    .unwrap();
    let widget = field.widgets[0].annotation_ref.clone().unwrap();
    block_on(form.set_groups(vec![FormGroupInput {
        id: "dana".into(),
        label: None,
        color: Some("#0055aa".into()),
    }]))
    .unwrap();
    // A drawing tool is design mode: the form lends its widgets.
    let interaction = block_on(host.interaction(document.id.clone())).unwrap();
    block_on(interaction.activate_tool("square".into())).unwrap();
    let annotations = block_on(host.annotation(document.id.clone())).unwrap();
    block_on(annotations.refresh(CancelToken::new())).unwrap();
    let lent = block_on(annotations.get(widget.clone())).unwrap().expect("the lent widget");
    assert_eq!(
        lent.accent,
        Some(epdf_ffi::RecordAccent { color: "#0055aa".into(), strength: 0.16 }),
        "{lent:?}"
    );
    let selection = annotations.selection();
    block_on(selection.select(vec![widget.clone()], None)).unwrap();
    let selected = block_on(form.selected_fields()).unwrap();
    assert_eq!(selected.iter().map(|f| f.name.as_str()).collect::<Vec<_>>(), ["dana_name"]);
    let props = block_on(selection.props()).unwrap();
    assert!(props.properties.iter().any(|p| p.key == "orientation"), "{props:?}");
    block_on(selection.rotate_by(90.0, CancelToken::new())).unwrap();
    let rotation = |form: &epdf_plugin_form::ffi::FormHandle| {
        let items = block_on(form.widgets(page)).unwrap();
        items.iter().find(|i| i.field_ref == field.field_ref).unwrap().rotation
    };
    assert_eq!(rotation(&form), 90.0);
    block_on(selection.reset_rotation(CancelToken::new())).unwrap();
    assert_eq!(rotation(&form), 0.0);
}
