//! `read/fill-items.ts`: the widget projection, what a framework draws for
//! one page's fields. Pure data in page space. Where a widget is and how it
//! looks come from the form's widget rows (`/Rect` and `/MK`), identity,
//! value and behavior from its field. A widget's place is its frame, its
//! quarter turn and the rect it stands in (`WidgetPlace`). Items are JSON in
//! the TypeScript's member order.
//!
//! A widget's `label` is its accessible name. A radio button's names its
//! option too (`Plan: monthly`), and each says where it comes in its group
//! (`inGroup`). A field of a group the app gave a label says whose it is
//! last: `Signature, Dana Smith`. A widget's `accent` is its group's color,
//! and how strongly it shows, while the session fills the field in.

use serde_json::{Map, Value, json};

use crate::model::{FieldIndex, PageWidget, PageWidgets, field_key_of};
use crate::read::fields::is_empty;
use crate::services::groups::GroupLook;
use crate::value::{get, items, number, str_of, truthy};

fn widget_of(field: &Value, annot_object_number: f64) -> Option<&Value> {
    items(field, "widgets")
        .iter()
        .find(|w| get(w, "objectNumber").and_then(Value::as_f64) == Some(annot_object_number))
}

/// `radioLabel(group, option)`: a radio button's accessible name, its
/// group's, then the option it stands for.
fn radio_label(group: &str, option: &str) -> String {
    if option.is_empty() { group.to_string() } else { format!("{group}: {option}") }
}

/// `ownedLabel(name, owner)`: a box's accessible name, then whose field it
/// is, when the app says who the group is.
fn owned_label(name: &str, owner: Option<&str>) -> String {
    match owner {
        Some(owner) => format!("{name}, {owner}"),
        None => name.to_string(),
    }
}

/// `takesValue(field)`: it holds a value (a push button doesn't), it isn't
/// read-only, nor a signed signature.
fn takes_value(field: &Value) -> bool {
    let kind = str_of(field, "kind");
    let signed = kind == Some("signature")
        && get(field, "valueEntry").and_then(|e| str_of(e, "kind")) != Some("none");
    kind != Some("pushbutton") && !truthy(get(field, "readOnly")) && !signed
}

/// A field's name as people read it: its tooltip, else its full name.
fn shown_name(field: &Value) -> String {
    match get(field, "alternateName").filter(|n| !n.is_null()).or_else(|| get(field, "name")) {
        Some(Value::String(name)) => name.clone(),
        Some(other) => crate::value::string_of(Some(other)),
        None => "undefined".into(),
    }
}

/// `projectWidget(field, annotObjectNumber, widget, fillable, canFill,
/// inGroup, group)`: `fillable`, people fill the field in by hand, which the
/// box is `disabled` without; `canFill`, the session's permission alone;
/// `inGroup`, where a radio button comes among its group's; `group`, how the
/// field's group shows, `None` for a field in none.
pub fn project_widget(
    field: &Value,
    annot_object_number: f64,
    widget: &PageWidget,
    fillable: bool,
    can_fill: bool,
    in_group: Option<Value>,
    group: Option<&GroupLook>,
) -> Option<Value> {
    let owner = group.and_then(|g| g.label.as_deref());
    let mut item = Map::new();
    let mut put = |key: &str, value: Value| {
        item.insert(key.to_string(), value);
    };
    put("key", json!(field_key_of(field)));
    put("fieldRef", get(field, "ref").cloned().unwrap_or(Value::Null));
    put("annotObjectNumber", number(annot_object_number));
    put(
        "annotationRef",
        widget_of(field, annot_object_number)
            .and_then(|w| get(w, "ref").filter(|r| !r.is_null()).cloned())
            .unwrap_or(Value::Null),
    );
    put("ref", widget.reference.clone());
    put("groupId", get(field, "groupId").cloned().unwrap_or(Value::Null));
    put("canFill", json!(can_fill));
    put("required", get(field, "required").cloned().unwrap_or(Value::Null));
    put("empty", json!(is_empty(field)));
    put(
        "accent",
        if can_fill && takes_value(field) {
            group.map_or(Value::Null, |g| g.accent.clone())
        } else {
            Value::Null
        },
    );
    put("box", widget.place.bx.clone());
    put("rotation", widget.place.rotation.clone());
    put("rect", widget.place.rect.clone());
    put("look", widget.look.clone());
    put("disabled", json!(truthy(get(field, "readOnly")) || !fillable));
    put("label", json!(owned_label(&shown_name(field), owner)));
    let member = |key: &str| get(field, key).cloned().unwrap_or(Value::Null);
    match str_of(field, "kind") {
        Some("text") => {
            put("control", json!("text"));
            put("value", member("value"));
            put("multiline", member("multiline"));
            put("password", member("password"));
            put("maxLength", member("maxLength"));
            put("comb", member("comb"));
        }
        Some("checkbox") => {
            let toggle = widget_of(field, annot_object_number);
            put("control", json!("toggle"));
            put("kind", json!("checkbox"));
            put("checked", member("checked"));
            put(
                "exportValue",
                match toggle {
                    Some(t) if t.get("exportValue").is_some() => member_of(t, "exportValue"),
                    _ => member("exportValue"),
                },
            );
        }
        Some("radio") => {
            let toggle = widget_of(field, annot_object_number);
            let export_value = match toggle {
                Some(t) if t.get("exportValue").is_some() => member_of(t, "exportValue"),
                _ => json!(""),
            };
            let option = crate::value::string_of(Some(&export_value));
            put("label", json!(owned_label(&radio_label(&shown_name(field), &option), owner)));
            put("control", json!("toggle"));
            put("kind", json!("radio"));
            put(
                "checked",
                match toggle {
                    Some(t) if t.get("checked").is_some() => member_of(t, "checked"),
                    _ => json!(false),
                },
            );
            put("exportValue", export_value);
            put("inGroup", in_group.unwrap_or(Value::Null));
        }
        Some("combobox") => {
            let value = member("value");
            put("control", json!("choice"));
            put("kind", json!("combo"));
            put("edit", member("edit"));
            put("multi", json!(false));
            put("options", member("options"));
            put("selected", if value == json!("") { json!([]) } else { json!([value]) });
        }
        Some("listbox") => {
            put("control", json!("choice"));
            put("kind", json!("list"));
            put("edit", json!(false));
            put("multi", member("multiSelect"));
            put("options", member("options"));
            put("selected", member("selectedValues"));
        }
        Some("pushbutton") => put("control", json!("button")),
        Some("signature") => {
            put("control", json!("signature"));
            let kind = get(field, "valueEntry").and_then(|e| str_of(e, "kind"));
            put("signed", json!(kind != Some("none")));
        }
        _ => return None,
    }
    Some(Value::Object(item))
}

fn member_of(v: &Value, key: &str) -> Value {
    v.get(key).cloned().unwrap_or(Value::Null)
}

/// `fillItems(index, pageObjectNumber, widgets, mayFillByHand, mayFill,
/// groupPositionOf, groupLookOf)`: each widget disabled when people don't
/// fill its field in by hand, each saying whether the session may fill it
/// in (its permissions alone), each radio button with where it comes in its
/// group, and each with how its field's group shows.
pub fn fill_items(
    index: &FieldIndex,
    page: f64,
    widgets: Option<&PageWidgets>,
    may_fill_by_hand: &dyn Fn(&Value) -> bool,
    may_fill: &dyn Fn(&Value) -> bool,
    group_position_of: &dyn Fn(f64) -> Option<Value>,
    group_look_of: &dyn Fn(&str) -> Option<GroupLook>,
) -> Vec<Value> {
    let (Some(_), Some(widgets)) = (&index.snapshot, widgets) else { return Vec::new() };
    let mut out = Vec::new();
    for field in index.fields() {
        for widget in items(field, "widgets") {
            let on_page =
                get(widget, "page").and_then(|p| get(p, "objectNumber")).and_then(Value::as_f64)
                    == Some(page);
            if !on_page {
                continue;
            }
            let n = get(widget, "objectNumber").and_then(Value::as_f64).unwrap_or(f64::NAN);
            let Some(placed) = widgets.get(&crate::value::number_key(n)) else { continue };
            let look = get(field, "groupId").and_then(Value::as_str).and_then(group_look_of);
            if let Some(item) = project_widget(
                field,
                n,
                placed,
                may_fill_by_hand(field),
                may_fill(field),
                group_position_of(n),
                look.as_ref(),
            ) {
                out.push(item);
            }
        }
    }
    out
}
