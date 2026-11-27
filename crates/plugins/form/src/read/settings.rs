//! `read/settings.ts`: a field's settings, described for a settings panel
//! the way an annotation kind describes its look: which settings the
//! field's family has, how each is edited, and its value. `update()`
//! writes them. While the document has groups, a field's group is one of
//! them: the choices are the groups the user may put a field in, and the
//! one the field is in.

use serde_json::{Map, Value, json};

use crate::model::{canonical_key, field_by_key};
use crate::value::{get, items, str_of};

fn setting(key: &str, control: &str, label: &str, label_key: &str) -> Value {
    json!({ "key": key, "control": control, "label": label, "labelKey": label_key })
}

/// What every field has.
fn common() -> Vec<Value> {
    vec![
        setting("name", "text", "Name", "form.settings.name"),
        setting("alternateName", "text", "Tooltip", "form.settings.tooltip"),
        setting("required", "flag", "Required", "form.settings.required"),
        setting("readOnly", "flag", "Read-only", "form.settings.readOnly"),
        setting("noExport", "flag", "Don't export", "form.settings.noExport"),
    ]
}

fn options_setting() -> Value {
    setting("options", "options", "Options", "form.settings.options")
}

fn option_values(field: &Value) -> Vec<Value> {
    items(field, "options")
        .iter()
        .map(|o| get(o, "value").cloned().unwrap_or(Value::Null))
        .collect()
}

/// What each family adds.
fn family_settings(field: &Value) -> Vec<Value> {
    match str_of(field, "kind") {
        Some("text") => vec![
            setting("defaultValue", "text", "Default value", "form.settings.defaultValue"),
            json!({
                "key": "maxLength", "control": "number", "label": "Maximum length",
                "labelKey": "form.settings.maxLength", "min": 1,
            }),
            setting("multiline", "flag", "Several lines", "form.settings.multiline"),
            setting("password", "flag", "Password", "form.settings.password"),
            setting("comb", "flag", "One character per box", "form.settings.comb"),
        ],
        Some("radio") => vec![
            setting(
                "radiosInUnison",
                "flag",
                "Buttons with one value turn on together",
                "form.settings.radiosInUnison",
            ),
            setting("noToggleToOff", "flag", "Keep one on", "form.settings.keepOneOn"),
        ],
        Some("combobox") => vec![
            options_setting(),
            setting("edit", "flag", "Allow typing", "form.settings.allowTyping"),
            json!({
                "key": "defaultValue", "control": "choice", "label": "Default value",
                "labelKey": "form.settings.defaultValue", "options": option_values(field),
                "free": get(field, "edit").cloned().unwrap_or(Value::Null),
            }),
        ],
        Some("listbox") => vec![
            options_setting(),
            setting("multiSelect", "flag", "Several choices", "form.settings.multiSelect"),
            json!({
                "key": "defaultValue", "control": "choices", "label": "Default value",
                "labelKey": "form.settings.defaultValue", "options": option_values(field),
            }),
        ],
        _ => Vec::new(),
    }
}

/// `groupSetting(field, groups, maySet)`: who fills the field in, among the
/// document's groups: the ones the user may put a field in, and the field's
/// own. Nothing while the document has no groups.
fn group_setting(field: &Value, groups: &[Value], may_set: &dyn Fn(&str) -> bool) -> Vec<Value> {
    if groups.is_empty() {
        return Vec::new();
    }
    let own = get(field, "groupId");
    let options: Vec<Value> = groups
        .iter()
        .filter(|group| Some(&group["id"]) == own || group["id"].as_str().is_some_and(may_set))
        .map(|group| group["id"].clone())
        .collect();
    vec![json!({
        "key": "groupId", "control": "choice", "label": "Filled in by",
        "labelKey": "form.settings.group", "options": options, "free": false,
    })]
}

/// `valuesOf(field, settings)`: each setting's value, as the field holds it.
fn values_of(field: &Value, settings: &[Value]) -> Value {
    let mut values = Map::new();
    for setting in settings {
        let key = str_of(setting, "key").unwrap_or("").to_string();
        let value = match key.as_str() {
            "name" => {
                let name = crate::value::string_of(get(field, "name"));
                json!(name.rsplit('.').next().unwrap_or(&name))
            }
            "alternateName" => match get(field, "alternateName") {
                Some(v) if !v.is_null() => v.clone(),
                _ => json!(""),
            },
            other => match get(field, other) {
                Some(v) if !v.is_null() => v.clone(),
                _ => Value::Null,
            },
        };
        values.insert(key, value);
    }
    Value::Object(values)
}

impl crate::controller::Core {
    /// `getFieldSettings(ref)`: a field's settings for a settings panel, or
    /// `None` for a field the form doesn't have.
    pub fn get_field_settings(&self, reference: &Value) -> Option<Value> {
        let index = self.view();
        let field = field_by_key(&index, &canonical_key(&index, reference))?;
        let mut properties = common();
        properties.extend(group_setting(field, &self.list_groups(), &|id| self.can_set_group(id)));
        properties.extend(family_settings(field));
        let values = values_of(field, &properties);
        Some(json!({ "properties": properties, "values": values }))
    }
}
