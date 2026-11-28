//! The form plugin through uniffi: typed records read from the plugin's
//! JSON, one handle, one event enum. Every method hops to the kernel thread
//! through `KernelBound`.
//!
//! Migrated by hand in Task 13.18 to the current plugin; compiled and
//! tested since Task 13.33. Refs are written in the engine's current
//! spelling (`objectNumber`).
//!
//! Not exported: the actions plugin's seams (`resetFormAction`,
//! `resolveSubmitDataset`, `commitScriptFormEffects`),
//! which the plugin wires itself, and the painter reads (`getWidgetPainting`,
//! `getTextEditorLayout`, `listShownWidgets`), which a native layer reaches
//! once a host types the scene. Removed with the TypeScript: `setText`,
//! `setChecked`, `setChoice`, `resetAll`, `attachWidget` (now `add_widget`),
//! `detachWidget` (now `remove_widget`), `exportData` and `importData` (now
//! `export` and `import`, with bundles), `getFillItem`, and `flags.raw`.

use std::sync::Arc;

use epdf_ffi::{
    AnnotationRef, CancelToken, FormFieldFamily, FormFieldRef, KernelBound, KernelPost, PageRef,
    PluginErrorInfo, Point, RecordAccent, Rect, RegistrationHandle, ResourceStatus, Subscription,
    ViewerError,
};
use epdf_kernel::error::{code, plugin_error};
use epdf_kernel::{Error, Unsubscribe};
use serde::Deserialize;
use serde::de::{DeserializeOwned, IgnoredAny};
use serde_json::{Map, Value, json};

use crate::FormCapability;

fn decode<T: DeserializeOwned>(value: Value) -> Result<T, Error> {
    serde_json::from_value(value).map_err(decode_error)
}

/// [`decode`] of a value read in place, not copied first.
fn decode_ref<T: DeserializeOwned>(value: &Value) -> Result<T, Error> {
    T::deserialize(value).map_err(decode_error)
}

fn decode_error(e: serde_json::Error) -> Error {
    Error::named("InvalidState", format!("[form] a value does not decode: {e}"))
        .with("code", "invalid-state")
}

fn invalid_state(message: String) -> Error {
    Error::named("InvalidState", message).with("code", "invalid-state")
}

fn open(cap: &FormCapability) -> Result<&FormCapability, Error> {
    if cap.lifetime().aborted() {
        return Err(plugin_error(
            code::INSTANCE_CLOSED,
            "form",
            "the document this capability belongs to is closed",
            None,
            None,
        ));
    }
    Ok(cap)
}

// ── refs, in the engine's current spelling ─────────────────────────────────

fn page_json(page: PageRef) -> Value {
    json!({ "kind": "objectNumber", "objectNumber": page.page_object_number })
}

/// A whole number the plugin wrote, as JavaScript numbers are (`101` or
/// `101.0`), that fits a `u32`.
fn whole(value: Option<&Value>) -> Option<u32> {
    value
        .and_then(Value::as_f64)
        .filter(|n| n.fract() == 0.0 && *n >= 0.0 && *n <= f64::from(u32::MAX))
        .map(|n| n as u32)
}

fn page_of(value: &Value) -> Result<PageRef, Error> {
    let n = whole(value.get("objectNumber"))
        .ok_or_else(|| invalid_state(format!("[form] not a page ref: {value}")))?;
    Ok(PageRef { page_object_number: n })
}

fn field_ref_json(reference: &FormFieldRef) -> Value {
    match reference {
        FormFieldRef::ObjectNumber { field_object_number } => {
            json!({ "kind": "objectNumber", "objectNumber": field_object_number })
        }
        FormFieldRef::Fqn { name } => json!({ "kind": "fqn", "name": name }),
    }
}

fn field_ref_of(value: &Value) -> Result<FormFieldRef, Error> {
    match value.get("kind").and_then(Value::as_str) {
        Some("objectNumber") => Ok(FormFieldRef::ObjectNumber {
            field_object_number: whole(value.get("objectNumber")).unwrap_or(0),
        }),
        Some("fqn") => Ok(FormFieldRef::Fqn {
            name: value.get("name").and_then(Value::as_str).unwrap_or_default().to_string(),
        }),
        _ => Err(invalid_state(format!("[form] not a field ref: {value}"))),
    }
}

fn annotation_ref_json(reference: &AnnotationRef) -> Value {
    reference.to_json()
}

fn annotation_ref_of(value: &Value) -> Result<Option<AnnotationRef>, Error> {
    if value.is_null() {
        return Ok(None);
    }
    AnnotationRef::from_json(value).map(Some)
}

fn rect_of(value: &Value) -> Rect {
    let n = |k: &str| value.get(k).and_then(Value::as_f64).unwrap_or(f64::NAN);
    Rect { x: n("x"), y: n("y"), width: n("width"), height: n("height") }
}

fn rect_json(rect: Rect) -> Value {
    json!({ "x": rect.x, "y": rect.y, "width": rect.width, "height": rect.height })
}

// ── config ───────────────────────────────────────────────────────────────────

#[derive(Clone, Copy, Debug, PartialEq, Eq, uniffi::Enum)]
pub enum FormValidation {
    /// Run the document's keystroke, validate, calculate and format scripts
    /// (needs the actions plugin with scripting on).
    Scripts,
    /// Accept every value.
    None,
}

/// What the app registers over the defaults (`FormConfig`).
#[derive(Clone, Debug, Default, PartialEq, uniffi::Record)]
pub struct FormConfig {
    /// `None` is `Scripts`.
    #[uniffi(default = None)]
    pub validation: Option<FormValidation>,
}

impl From<FormConfig> for Option<Value> {
    fn from(c: FormConfig) -> Self {
        let validation = c.validation?;
        Some(json!({ "validation": match validation {
            FormValidation::Scripts => "scripts",
            FormValidation::None => "none",
        } }))
    }
}

// ── fields ───────────────────────────────────────────────────────────────────

#[derive(Clone, Copy, Debug, PartialEq, Eq, Deserialize, uniffi::Enum)]
#[serde(rename_all = "camelCase")]
pub enum FormKind {
    None,
    Acroform,
    Xfa,
}

/// A checkbox or radio widget's state.
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormToggleWidget {
    pub on_state: String,
    /// What the form data holds while it's checked: what `set_value` takes.
    pub export_value: String,
    pub checked: bool,
}

#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormWidget {
    /// `None` for a widget on no page, or stored as a direct object.
    pub annotation_ref: Option<AnnotationRef>,
    pub object_number: u32,
    pub page: Option<PageRef>,
    pub toggle: Option<FormToggleWidget>,
}

#[derive(Clone, Debug, PartialEq, Deserialize, uniffi::Record)]
pub struct FormChoiceOption {
    pub label: String,
    pub value: String,
    #[serde(default)]
    pub selected: bool,
}

/// The family-specific half of a field.
#[derive(Clone, Debug, PartialEq, uniffi::Enum)]
pub enum FormFieldDetail {
    Text {
        value: String,
        default_value: String,
        max_length: Option<u32>,
        multiline: bool,
        password: bool,
        comb: bool,
    },
    Checkbox {
        checked: bool,
        export_value: String,
    },
    Radio {
        value: String,
        radios_in_unison: bool,
        no_toggle_to_off: bool,
    },
    Combobox {
        value: String,
        default_value: String,
        edit: bool,
        options: Vec<FormChoiceOption>,
    },
    Listbox {
        selected_values: Vec<String>,
        multi_select: bool,
        options: Vec<FormChoiceOption>,
    },
    /// A push button; `has_image` when it holds a picture
    /// (`set_button_image` puts one in or takes it out).
    Pushbutton {
        has_image: bool,
    },
    Signature {
        signed: bool,
    },
    Unknown {
        raw_value: String,
    },
}

/// How a push button's widget lays out its caption and its picture
/// (`WidgetLayout`, `/MK /TP`): the seven layouts of ISO 32000-2 Table 192.
#[derive(Clone, Copy, Debug, PartialEq, Eq, uniffi::Enum)]
pub enum FormWidgetLayout {
    /// The caption alone.
    Caption,
    /// The picture alone.
    Image,
    CaptionBelow,
    CaptionAbove,
    CaptionRight,
    CaptionLeft,
    /// The caption over the picture.
    CaptionOver,
}

impl FormWidgetLayout {
    pub(crate) fn as_str(self) -> &'static str {
        match self {
            FormWidgetLayout::Caption => "caption",
            FormWidgetLayout::Image => "image",
            FormWidgetLayout::CaptionBelow => "caption-below",
            FormWidgetLayout::CaptionAbove => "caption-above",
            FormWidgetLayout::CaptionRight => "caption-right",
            FormWidgetLayout::CaptionLeft => "caption-left",
            FormWidgetLayout::CaptionOver => "caption-over",
        }
    }

    /// A row's layout; `None` for none (every widget but a push button's)
    /// and for a name outside the seven.
    pub(crate) fn from_json(value: Option<&Value>) -> Option<Self> {
        Some(match value?.as_str()? {
            "caption" => FormWidgetLayout::Caption,
            "image" => FormWidgetLayout::Image,
            "caption-below" => FormWidgetLayout::CaptionBelow,
            "caption-above" => FormWidgetLayout::CaptionAbove,
            "caption-right" => FormWidgetLayout::CaptionRight,
            "caption-left" => FormWidgetLayout::CaptionLeft,
            "caption-over" => FormWidgetLayout::CaptionOver,
            _ => return None,
        })
    }
}

/// A text field's or a dropdown's format (`FormFieldFormat`): a date or a
/// time with its pattern, or `Other` for any other format script. A write
/// takes a date or a time; `Other` is read, never written, and a write of it
/// is refused with `invalid-input`.
#[derive(Clone, Debug, PartialEq, Eq, uniffi::Enum)]
pub enum FormFieldFormat {
    /// A date pattern such as `yyyy-mm-dd`.
    Date { pattern: String },
    /// A time pattern such as `HH:MM`.
    Time { pattern: String },
    /// A format script that is neither.
    Other,
}

impl FormFieldFormat {
    fn to_json(&self) -> Value {
        match self {
            FormFieldFormat::Date { pattern } => json!({ "kind": "date", "pattern": pattern }),
            FormFieldFormat::Time { pattern } => json!({ "kind": "time", "pattern": pattern }),
            FormFieldFormat::Other => json!({ "kind": "other" }),
        }
    }

    /// A read's format; a kind this side doesn't know reads as `Other`, as
    /// the engine reads it.
    fn from_json(value: &Value) -> Option<Self> {
        if value.is_null() {
            return None;
        }
        let pattern = || value.get("pattern").and_then(Value::as_str).unwrap_or_default().into();
        Some(match value.get("kind").and_then(Value::as_str) {
            Some("date") => FormFieldFormat::Date { pattern: pattern() },
            Some("time") => FormFieldFormat::Time { pattern: pattern() },
            _ => FormFieldFormat::Other,
        })
    }
}

#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormField {
    pub field_ref: FormFieldRef,
    /// The fully qualified name, such as `billing.name`.
    pub name: String,
    pub family: FormFieldFamily,
    /// `true` when the field was found only through a page's `/Annots`.
    pub recovered: bool,
    pub read_only: bool,
    pub required: bool,
    pub no_export: bool,
    /// `/TU`, the tooltip and accessible name.
    pub alternate_name: Option<String>,
    /// `/TM`, the export name.
    pub mapping_name: Option<String>,
    /// Who fills it in: its group, or `None` for every user.
    pub group_id: Option<String>,
    /// Whether every signer fills it in (`forAnyone`), as initials and a
    /// date beside each signature are: never with a group, never on a
    /// signature field.
    pub for_anyone: bool,
    /// What the value means (`purpose`): `email`, `date-signed`, a name the
    /// app chose; `None` for none.
    pub purpose: Option<String>,
    /// A text field's or a dropdown's format; `None` for none, and for every
    /// other family.
    pub format: Option<FormFieldFormat>,
    pub detail: FormFieldDetail,
    pub widgets: Vec<FormWidget>,
    /// The engine's field record, whole, as JSON: attribution, the value
    /// entries and the actions this record doesn't type.
    pub fields_json: String,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct WidgetDto {
    #[serde(rename = "ref", default)]
    annotation_ref: Value,
    object_number: f64,
    #[serde(default)]
    page: Value,
    #[serde(default)]
    on_state: Option<String>,
    #[serde(default)]
    export_value: Option<String>,
    #[serde(default)]
    checked: Option<bool>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct FieldDto {
    #[serde(rename = "ref")]
    field_ref: Value,
    name: String,
    #[serde(rename = "kind")]
    family: String,
    origin: String,
    read_only: bool,
    required: bool,
    no_export: bool,
    #[serde(default)]
    alternate_name: Option<String>,
    #[serde(default)]
    mapping_name: Option<String>,
    #[serde(default)]
    group_id: Option<String>,
    #[serde(default)]
    for_anyone: bool,
    #[serde(default)]
    has_image: bool,
    #[serde(default)]
    purpose: Option<String>,
    #[serde(default)]
    format: Value,
    #[serde(default)]
    value_entry: Value,
    #[serde(default)]
    widgets: Vec<WidgetDto>,
    #[serde(default)]
    value: Value,
    #[serde(default)]
    default_value: Value,
    #[serde(default)]
    max_length: Option<i64>,
    #[serde(default)]
    multiline: bool,
    #[serde(default)]
    password: bool,
    #[serde(default)]
    comb: bool,
    #[serde(default)]
    checked: bool,
    #[serde(default)]
    export_value: Option<String>,
    #[serde(default)]
    radios_in_unison: bool,
    #[serde(default)]
    no_toggle_to_off: bool,
    #[serde(default)]
    edit: bool,
    #[serde(default)]
    options: Vec<FormChoiceOption>,
    #[serde(default)]
    selected_values: Vec<String>,
    #[serde(default)]
    multi_select: bool,
    #[serde(default)]
    raw_value: Option<String>,
}

impl FormField {
    pub(crate) fn from_json(value: &Value) -> Result<Self, Error> {
        let dto: FieldDto = decode_ref(value)?;
        let family = FormFieldFamily::parse(&dto.family);
        let text = |v: &Value| v.as_str().unwrap_or_default().to_string();
        let detail = match family {
            FormFieldFamily::Text => FormFieldDetail::Text {
                value: text(&dto.value),
                default_value: text(&dto.default_value),
                max_length: dto.max_length.and_then(|n| u32::try_from(n).ok()),
                multiline: dto.multiline,
                password: dto.password,
                comb: dto.comb,
            },
            FormFieldFamily::Checkbox => FormFieldDetail::Checkbox {
                checked: dto.checked,
                export_value: dto.export_value.clone().unwrap_or_default(),
            },
            FormFieldFamily::Radio => FormFieldDetail::Radio {
                value: text(&dto.value),
                radios_in_unison: dto.radios_in_unison,
                no_toggle_to_off: dto.no_toggle_to_off,
            },
            FormFieldFamily::Combobox => FormFieldDetail::Combobox {
                value: text(&dto.value),
                default_value: text(&dto.default_value),
                edit: dto.edit,
                options: dto.options.clone(),
            },
            FormFieldFamily::Listbox => FormFieldDetail::Listbox {
                selected_values: dto.selected_values.clone(),
                multi_select: dto.multi_select,
                options: dto.options.clone(),
            },
            FormFieldFamily::Pushbutton => FormFieldDetail::Pushbutton { has_image: dto.has_image },
            FormFieldFamily::Signature => FormFieldDetail::Signature {
                signed: dto.value_entry.get("kind").and_then(Value::as_str) != Some("none"),
            },
            FormFieldFamily::Unknown => {
                FormFieldDetail::Unknown { raw_value: dto.raw_value.clone().unwrap_or_default() }
            }
        };
        let widgets = dto
            .widgets
            .into_iter()
            .map(|w| {
                let toggle = w.on_state.map(|on_state| FormToggleWidget {
                    on_state,
                    export_value: w.export_value.unwrap_or_default(),
                    checked: w.checked.unwrap_or(false),
                });
                Ok(FormWidget {
                    annotation_ref: annotation_ref_of(&w.annotation_ref)?,
                    object_number: w.object_number as u32,
                    page: if w.page.is_null() { None } else { Some(page_of(&w.page)?) },
                    toggle,
                })
            })
            .collect::<Result<_, Error>>()?;
        Ok(FormField {
            field_ref: field_ref_of(&dto.field_ref)?,
            name: dto.name,
            family,
            recovered: dto.origin == "recovered",
            read_only: dto.read_only,
            required: dto.required,
            no_export: dto.no_export,
            alternate_name: dto.alternate_name,
            mapping_name: dto.mapping_name,
            group_id: dto.group_id,
            for_anyone: dto.for_anyone,
            purpose: dto.purpose,
            format: FormFieldFormat::from_json(&dto.format),
            detail,
            widgets,
            fields_json: value.to_string(),
        })
    }
}

fn fields_out(list: &[Value]) -> Result<Vec<FormField>, Error> {
    list.iter().map(FormField::from_json).collect()
}

fn optional_field(value: Option<&Value>) -> Result<Option<FormField>, Error> {
    match value {
        None | Some(Value::Null) => Ok(None),
        Some(v) => FormField::from_json(v).map(Some),
    }
}

#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormSnapshot {
    pub form_kind: FormKind,
    pub needs_appearances: bool,
    pub fields: Vec<FormField>,
    /// `/CO`. `None` keeps a slot naming a field the model lacks.
    pub calculation_order: Vec<Option<FormFieldRef>>,
    /// Every widget's row, as JSON (`WidgetAnnotation[]`).
    pub widgets_json: String,
}

impl FormSnapshot {
    fn from_json(value: &Value) -> Result<Self, Error> {
        // The head is decoded; the lists are only checked to be lists, and
        // are read in place from `value`.
        #[derive(Deserialize)]
        #[serde(rename_all = "camelCase")]
        struct Dto {
            form_kind: FormKind,
            needs_appearances: bool,
            #[allow(dead_code)]
            fields: Vec<IgnoredAny>,
            #[allow(dead_code)]
            #[serde(default)]
            widgets: Vec<IgnoredAny>,
            #[allow(dead_code)]
            #[serde(default)]
            calculation_order: Vec<IgnoredAny>,
        }
        let dto: Dto = decode_ref(value)?;
        let list =
            |key: &str| value.get(key).and_then(Value::as_array).map_or(&[][..], Vec::as_slice);
        Ok(FormSnapshot {
            form_kind: dto.form_kind,
            needs_appearances: dto.needs_appearances,
            fields: fields_out(list("fields"))?,
            calculation_order: list("calculationOrder")
                .iter()
                .map(|r| if r.is_null() { Ok(None) } else { field_ref_of(r).map(Some) })
                .collect::<Result<_, Error>>()?,
            widgets_json: serde_json::to_string(list("widgets")).expect("JSON values serialise"),
        })
    }
}

// ── values ───────────────────────────────────────────────────────────────────

/// A value to write (`FormFieldValue`): a text field's text, a radio
/// group's, checkbox's or dropdown's choice by export value (`None` clears
/// it); a checkbox by `Checked`; a list's option values.
#[derive(Clone, Debug, PartialEq, uniffi::Enum)]
pub enum FormValueInput {
    Value { value: Option<String> },
    Checked { checked: bool },
    SelectedValues { values: Vec<String> },
}

impl FormValueInput {
    fn to_json(&self) -> Value {
        match self {
            FormValueInput::Value { value } => json!({ "value": value }),
            FormValueInput::Checked { checked } => json!({ "checked": checked }),
            FormValueInput::SelectedValues { values } => json!({ "selectedValues": values }),
        }
    }

    fn from_json(value: &Value) -> Result<Self, Error> {
        if let Some(checked) = value.get("checked") {
            return Ok(FormValueInput::Checked { checked: checked.as_bool().unwrap_or(false) });
        }
        if let Some(values) = value.get("selectedValues") {
            return Ok(FormValueInput::SelectedValues { values: decode(values.clone())? });
        }
        Ok(FormValueInput::Value {
            value: value.get("value").and_then(Value::as_str).map(str::to_string),
        })
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, uniffi::Enum)]
pub enum FormSetValueStatus {
    Applied,
    Unchanged,
    /// The form's own validation refused the value.
    Rejected,
}

/// What `set_value` resolves: the field as it is now, and what the write did.
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormSetValueResult {
    pub field: FormField,
    pub status: FormSetValueStatus,
}

fn set_value_result(value: &Value) -> Result<FormSetValueResult, ViewerError> {
    let status = match value.get("status").and_then(Value::as_str) {
        Some("applied") => FormSetValueStatus::Applied,
        Some("rejected") => FormSetValueStatus::Rejected,
        _ => FormSetValueStatus::Unchanged,
    };
    Ok(FormSetValueResult {
        field: FormField::from_json(value.get("field").unwrap_or(&Value::Null))?,
        status,
    })
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, uniffi::Enum)]
pub enum FormCommitStatus {
    Applied,
    Unchanged,
    Rejected,
    Failed,
}

impl From<crate::FormCommitStatus> for FormCommitStatus {
    fn from(s: crate::FormCommitStatus) -> Self {
        match s {
            crate::FormCommitStatus::Applied => FormCommitStatus::Applied,
            crate::FormCommitStatus::Unchanged => FormCommitStatus::Unchanged,
            crate::FormCommitStatus::Rejected => FormCommitStatus::Rejected,
            crate::FormCommitStatus::Failed => FormCommitStatus::Failed,
        }
    }
}

/// A write through the document's scripts.
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormCommitResult {
    pub status: FormCommitStatus,
    pub scripted: bool,
    pub diagnostics: Vec<String>,
    pub error: Option<String>,
}

impl From<crate::FormCommitResult> for FormCommitResult {
    fn from(r: crate::FormCommitResult) -> Self {
        FormCommitResult {
            status: r.status.into(),
            scripted: r.scripted,
            diagnostics: r.diagnostics.into_iter().map(|d| d.message).collect(),
            error: r.error.map(|e| e.message),
        }
    }
}

#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormBatchFailure {
    pub field_ref: Option<FormFieldRef>,
    /// The entry's name when it was addressed by name (`import_values`).
    pub name: Option<String>,
    pub error: PluginErrorInfo,
}

#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormBatchSkip {
    pub name: String,
    pub reason: String,
}

#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormBatchResult {
    pub applied: Vec<FormFieldRef>,
    pub skipped: Vec<FormBatchSkip>,
    pub failed: Vec<FormBatchFailure>,
}

fn batch_result(value: &Value) -> Result<FormBatchResult, ViewerError> {
    let list = |key: &str| value.get(key).and_then(Value::as_array).cloned().unwrap_or_default();
    let text =
        |v: &Value, key: &str| v.get(key).and_then(Value::as_str).unwrap_or_default().to_string();
    Ok(FormBatchResult {
        applied: list("applied").iter().map(field_ref_of).collect::<Result<_, Error>>()?,
        skipped: list("skipped")
            .iter()
            .map(|s| FormBatchSkip { name: text(s, "ref"), reason: text(s, "reason") })
            .collect(),
        failed: list("failed")
            .iter()
            .map(|f| {
                let entry = f.get("ref").unwrap_or(&Value::Null);
                let error = f.get("error").unwrap_or(&Value::Null);
                FormBatchFailure {
                    field_ref: field_ref_of(entry).ok(),
                    name: entry.as_str().map(str::to_string),
                    error: PluginErrorInfo {
                        code: text(error, "code"),
                        message: text(error, "message"),
                        capability: text(error, "capability"),
                        permission: error
                            .get("permission")
                            .and_then(Value::as_str)
                            .map(str::to_string),
                    },
                }
            })
            .collect(),
    })
}

// ── the fill projection ──────────────────────────────────────────────────────

#[derive(Clone, Debug, PartialEq, uniffi::Enum)]
pub enum FormFillControl {
    Text {
        value: String,
        multiline: bool,
        password: bool,
        max_length: Option<u32>,
        comb: bool,
    },
    Toggle {
        radio: bool,
        checked: bool,
        export_value: String,
        /// A radio button's place among its group's buttons in Tab's order,
        /// for a screen reader's "2 of 3"; `None` for a checkbox, and while
        /// that order isn't known.
        in_group: Option<FormGroupPosition>,
    },
    Choice {
        list: bool,
        edit: bool,
        multi: bool,
        options: Vec<FormChoiceOption>,
        selected: Vec<String>,
    },
    Button,
    Signature {
        signed: bool,
    },
}

/// Where a radio button comes among its group's buttons (`GroupPosition`):
/// `position` of `size`, from 1.
#[derive(Clone, Copy, Debug, PartialEq, Eq, uniffi::Record)]
pub struct FormGroupPosition {
    pub position: u32,
    pub size: u32,
}

/// One widget as a fill control draws it (`FormWidgetItem`).
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormFillItem {
    pub field_ref: FormFieldRef,
    pub annot_object_number: u32,
    pub annotation_ref: Option<AnnotationRef>,
    /// The rect the widget stands in on its page (`rect`), page space. For
    /// a turned widget it holds the turned frame.
    pub bounds: Rect,
    /// The frame the widget's contents are laid out in (`box`), page space,
    /// before its turn; the same as `bounds` for an unturned widget.
    pub frame: Rect,
    /// The widget's quarter turn (`rotation`): 0, 90, 180 or 270 degrees
    /// clockwise about the middle of `frame`.
    pub rotation: f64,
    /// The field's group (`groupId`); `None` for no group.
    pub group_id: Option<String>,
    /// The session may fill the field in, whatever the `fillable` setting
    /// says (`canFill`).
    pub can_fill: bool,
    pub required: bool,
    /// The field holds no value (`empty`).
    pub empty: bool,
    /// The field's group color while this session fills it in, `None`
    /// otherwise and while `fields.tint` is 0.
    pub accent: Option<RecordAccent>,
    /// The widget's own look (`FormWidgetLook`), as JSON.
    pub look_json: String,
    /// Read-only, no permission to fill, or filling turned off: draw it,
    /// don't take input.
    pub disabled: bool,
    pub label: String,
    pub control: FormFillControl,
}

/// A place's `rotation`: a number, else 0.
fn rotation_of(value: Option<&Value>) -> f64 {
    value.and_then(Value::as_f64).unwrap_or(0.0)
}

fn group_position_of(value: Option<&Value>) -> Option<FormGroupPosition> {
    let value = value?;
    Some(FormGroupPosition {
        position: whole(value.get("position"))?,
        size: whole(value.get("size"))?,
    })
}

fn fill_item(item: &Value) -> Result<FormFillItem, Error> {
    let flag = |k: &str| item.get(k).and_then(Value::as_bool).unwrap_or(false);
    let text = |k: &str| item.get(k).and_then(Value::as_str).unwrap_or_default().to_string();
    let rect = rect_of(item.get("rect").unwrap_or(&Value::Null));
    let control = match item.get("control").and_then(Value::as_str) {
        Some("text") => FormFillControl::Text {
            value: text("value"),
            multiline: flag("multiline"),
            password: flag("password"),
            max_length: whole(item.get("maxLength")),
            comb: flag("comb"),
        },
        Some("toggle") => FormFillControl::Toggle {
            radio: item.get("kind").and_then(Value::as_str) == Some("radio"),
            checked: flag("checked"),
            export_value: text("exportValue"),
            in_group: group_position_of(item.get("inGroup")),
        },
        Some("choice") => FormFillControl::Choice {
            list: item.get("kind").and_then(Value::as_str) == Some("list"),
            edit: flag("edit"),
            multi: flag("multi"),
            options: decode(item.get("options").cloned().unwrap_or(json!([])))?,
            selected: decode(item.get("selected").cloned().unwrap_or(json!([])))?,
        },
        Some("button") => FormFillControl::Button,
        Some("signature") => FormFillControl::Signature { signed: flag("signed") },
        other => return Err(invalid_state(format!("[form] a fill control {other:?}"))),
    };
    Ok(FormFillItem {
        field_ref: field_ref_of(item.get("fieldRef").unwrap_or(&Value::Null))?,
        annot_object_number: whole(item.get("annotObjectNumber")).unwrap_or(0),
        annotation_ref: annotation_ref_of(item.get("annotationRef").unwrap_or(&Value::Null))?,
        bounds: rect,
        frame: item.get("box").filter(|b| !b.is_null()).map_or(rect, rect_of),
        rotation: rotation_of(item.get("rotation")),
        group_id: item.get("groupId").and_then(Value::as_str).map(str::to_string),
        can_fill: flag("canFill"),
        required: flag("required"),
        empty: flag("empty"),
        accent: RecordAccent::from_json(item.get("accent")),
        look_json: item.get("look").cloned().unwrap_or(Value::Null).to_string(),
        disabled: flag("disabled"),
        label: text("label"),
        control,
    })
}

#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormWidgetHit {
    pub annot_object_number: u32,
    pub field: FormField,
    /// The rect the widget stands in (`rect`).
    pub bounds: Rect,
    /// Its frame before its turn (`box`), as in [`FormFillItem::frame`].
    pub frame: Rect,
    /// Its quarter turn, degrees clockwise.
    pub rotation: f64,
}

// ── groups ───────────────────────────────────────────────────────────────────

/// One of the document's groups as people see them (`FormGroup`).
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormGroup {
    /// The id its fields carry as `group_id`.
    pub id: String,
    /// Who the group is, for people; `None` when the app gave it no label.
    pub label: Option<String>,
    /// The CSS color its fields show in: the app's, else one of eight by
    /// the group's place in the list.
    pub color: String,
}

impl FormGroup {
    fn from_json(value: &Value) -> FormGroup {
        let text = |k: &str| value.get(k).and_then(Value::as_str).map(str::to_string);
        FormGroup {
            id: text("id").unwrap_or_default(),
            label: text("label"),
            color: text("color").unwrap_or_default(),
        }
    }
}

fn groups_out(list: &[Value]) -> Vec<FormGroup> {
    list.iter().map(FormGroup::from_json).collect()
}

/// One of the document's groups as the app names it with `set_groups`
/// (`FormGroupInput`).
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormGroupInput {
    /// The id its fields carry, and the one the permissions name.
    pub id: String,
    /// Who the group is, for people.
    #[uniffi(default = None)]
    pub label: Option<String>,
    /// A CSS color; `None` gives the group one of eight by its place.
    #[uniffi(default = None)]
    pub color: Option<String>,
}

impl FormGroupInput {
    fn to_json(&self) -> Value {
        let mut group = Map::new();
        group.insert("id".into(), json!(self.id));
        if let Some(label) = &self.label {
            group.insert("label".into(), json!(label));
        }
        if let Some(color) = &self.color {
            group.insert("color".into(), json!(color));
        }
        Value::Object(group)
    }
}

/// Where UI attaches to one box of a field (`getWidgetAnchor`): its page and
/// where the box stands, following the box as it is moved in design mode.
#[derive(Clone, Copy, Debug, PartialEq, uniffi::Record)]
pub struct FormWidgetAnchor {
    pub page: PageRef,
    pub bounds: Rect,
}

#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormValidationResult {
    pub valid: bool,
    /// The required fields you may fill in that are empty, in page order.
    pub missing: Vec<FormField>,
}

// ── design ───────────────────────────────────────────────────────────────────

#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormChoiceOptionInput {
    pub label: String,
    pub value: String,
}

/// A new field with one widget. Use `create_draft` for the whole engine draft.
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormCreateFieldInput {
    pub family: FormFieldFamily,
    /// A fully qualified name, such as `billing.name`.
    pub name: String,
    pub page: PageRef,
    /// The widget's page-space box.
    pub bounds: Rect,
    #[uniffi(default = None)]
    pub options: Option<Vec<FormChoiceOptionInput>>,
    /// A radio button's or checkbox's export value.
    #[uniffi(default = None)]
    pub export_value: Option<String>,
    /// What the value means; `None` for none.
    #[uniffi(default = None)]
    pub purpose: Option<String>,
    /// A text field's or a dropdown's date or time format; `None` for none.
    #[uniffi(default = None)]
    pub format: Option<FormFieldFormat>,
    /// The group that fills it in; `None` for no group. Refused with
    /// `permission-denied` for a group the session may not put a field in.
    #[uniffi(default = None)]
    pub group_id: Option<String>,
    /// `true` for a field every signer fills in; `None` is `false`.
    /// Refused with `invalid-input` with a group or on a signature field.
    #[uniffi(default = None)]
    pub for_anyone: Option<bool>,
    /// A push button's widget layout; `None` is `Caption`. Refused for any
    /// other family.
    #[uniffi(default = None)]
    pub layout: Option<FormWidgetLayout>,
}

impl FormCreateFieldInput {
    fn to_json(&self) -> Value {
        let mut widget = Map::new();
        widget.insert("page".into(), page_json(self.page));
        widget.insert("rect".into(), rect_json(self.bounds));
        if let Some(export_value) = &self.export_value {
            widget.insert("exportValue".into(), json!(export_value));
        }
        if let Some(layout) = self.layout {
            widget.insert("layout".into(), json!(layout.as_str()));
        }
        let mut draft = Map::new();
        draft.insert("kind".into(), json!(self.family.as_str()));
        draft.insert("name".into(), json!(self.name));
        draft.insert("widgets".into(), json!([widget]));
        if let Some(options) = &self.options {
            let options: Vec<Value> =
                options.iter().map(|o| json!({ "label": o.label, "value": o.value })).collect();
            draft.insert("options".into(), Value::Array(options));
        }
        if let Some(purpose) = &self.purpose {
            draft.insert("purpose".into(), json!(purpose));
        }
        if let Some(format) = &self.format {
            draft.insert("format".into(), format.to_json());
        }
        if let Some(group_id) = &self.group_id {
            draft.insert("groupId".into(), json!(group_id));
        }
        if let Some(for_anyone) = self.for_anyone {
            draft.insert("forAnyone".into(), json!(for_anyone));
        }
        Value::Object(draft)
    }
}

/// Where a field's new widget goes (`Partial<WidgetPlacement>`): without a
/// box, next in line after the field's widgets.
#[derive(Clone, Debug, Default, PartialEq, uniffi::Record)]
pub struct FormWidgetPlacement {
    #[uniffi(default = None)]
    pub page: Option<PageRef>,
    #[uniffi(default = None)]
    pub bounds: Option<Rect>,
    #[uniffi(default = None)]
    pub export_value: Option<String>,
    /// A push button's widget layout; `None` is `Caption`. Refused for any
    /// other family.
    #[uniffi(default = None)]
    pub layout: Option<FormWidgetLayout>,
}

impl FormWidgetPlacement {
    fn to_json(&self) -> Value {
        let mut placement = Map::new();
        if let Some(page) = self.page {
            placement.insert("page".into(), page_json(page));
        }
        if let Some(bounds) = self.bounds {
            placement.insert("rect".into(), rect_json(bounds));
        }
        if let Some(export_value) = &self.export_value {
            placement.insert("exportValue".into(), json!(export_value));
        }
        if let Some(layout) = self.layout {
            placement.insert("layout".into(), json!(layout.as_str()));
        }
        Value::Object(placement)
    }
}

/// What `add_widget` resolves: the field, and its new widget's row as JSON.
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormWidgetAdded {
    pub field: FormField,
    pub widget_json: String,
}

/// A field edit (`FormFieldPatch`). `None` leaves a member alone; a key in
/// `clear` removes it.
#[derive(Clone, Debug, Default, PartialEq, uniffi::Record)]
pub struct FormFieldPatch {
    /// The field's own segment of its dotted name.
    #[uniffi(default = None)]
    pub name: Option<String>,
    #[uniffi(default = None)]
    pub read_only: Option<bool>,
    #[uniffi(default = None)]
    pub required: Option<bool>,
    #[uniffi(default = None)]
    pub no_export: Option<bool>,
    #[uniffi(default = None)]
    pub alternate_name: Option<String>,
    #[uniffi(default = None)]
    pub mapping_name: Option<String>,
    #[uniffi(default = None)]
    pub default_value: Option<String>,
    #[uniffi(default = None)]
    pub max_length: Option<u32>,
    #[uniffi(default = None)]
    pub options: Option<Vec<FormChoiceOptionInput>>,
    /// What the value means.
    #[uniffi(default = None)]
    pub purpose: Option<String>,
    /// A text field's or a dropdown's date or time format, with the two
    /// scripts it is made of.
    #[uniffi(default = None)]
    pub format: Option<FormFieldFormat>,
    /// The group that fills it in. Refused with `permission-denied` for a
    /// group the session may not put a field in.
    #[uniffi(default = None)]
    pub group_id: Option<String>,
    /// Whether every signer fills it in. `true` is refused with
    /// `invalid-input` for a field in a group or a signature field.
    #[uniffi(default = None)]
    pub for_anyone: Option<bool>,
    #[uniffi(default = [])]
    pub clear: Vec<FormFieldPatchKey>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, uniffi::Enum)]
pub enum FormFieldPatchKey {
    AlternateName,
    MappingName,
    DefaultValue,
    MaxLength,
    /// The field has no purpose.
    Purpose,
    /// The field has no format, and loses its format's two scripts.
    Format,
    /// The field is in no group.
    GroupId,
}

impl FormFieldPatch {
    fn to_json(&self) -> Result<Value, Error> {
        let mut patch = Map::new();
        let set: [(&str, Option<Value>); 13] = [
            ("name", self.name.clone().map(Value::from)),
            ("readOnly", self.read_only.map(Value::from)),
            ("required", self.required.map(Value::from)),
            ("noExport", self.no_export.map(Value::from)),
            ("alternateName", self.alternate_name.clone().map(Value::from)),
            ("mappingName", self.mapping_name.clone().map(Value::from)),
            ("defaultValue", self.default_value.clone().map(Value::from)),
            ("maxLength", self.max_length.map(Value::from)),
            (
                "options",
                self.options.as_ref().map(|o| {
                    Value::Array(
                        o.iter().map(|o| json!({ "label": o.label, "value": o.value })).collect(),
                    )
                }),
            ),
            ("purpose", self.purpose.clone().map(Value::from)),
            ("format", self.format.as_ref().map(FormFieldFormat::to_json)),
            ("groupId", self.group_id.clone().map(Value::from)),
            ("forAnyone", self.for_anyone.map(Value::from)),
        ];
        for (key, value) in set {
            if let Some(value) = value {
                patch.insert(key.into(), value);
            }
        }
        for key in &self.clear {
            let name = match key {
                FormFieldPatchKey::AlternateName => "alternateName",
                FormFieldPatchKey::MappingName => "mappingName",
                FormFieldPatchKey::DefaultValue => "defaultValue",
                FormFieldPatchKey::MaxLength => "maxLength",
                FormFieldPatchKey::Purpose => "purpose",
                FormFieldPatchKey::Format => "format",
                FormFieldPatchKey::GroupId => "groupId",
            };
            if patch.insert(name.into(), Value::Null).is_some_and(|v| !v.is_null()) {
                return Err(plugin_error(
                    code::INVALID_INPUT,
                    "form",
                    format!("field patch both sets and clears \"{name}\""),
                    None,
                    None,
                ));
            }
        }
        Ok(Value::Object(patch))
    }
}

/// How `duplicate` and `duplicate_widgets` copy (`FormDuplicateOptions`).
#[derive(Clone, Debug, Default, PartialEq, uniffi::Record)]
pub struct FormDuplicateOptions {
    /// The page the copied widgets go on; `None`: each on its original's page.
    #[uniffi(default = None)]
    pub page: Option<PageRef>,
    /// How far each copied widget moves, in page points: `x` to the right, `y` down.
    #[uniffi(default = None)]
    pub offset: Option<Point>,
    /// Whether a new field gets its original's value; `false`: it starts empty.
    #[uniffi(default = false)]
    pub values: bool,
}

impl FormDuplicateOptions {
    /// The options as the plugin reads them. A NaN or an infinity in the
    /// offset is `invalid-input`: JSON would carry it as `null` and drop it.
    fn to_json(&self) -> Result<Value, Error> {
        let mut options = Map::new();
        if let Some(page) = self.page {
            options.insert("page".into(), page_json(page));
        }
        if let Some(offset) = self.offset {
            if !offset.x.is_finite() || !offset.y.is_finite() {
                return Err(plugin_error(
                    code::INVALID_INPUT,
                    "form",
                    format!("an offset is not finite: ({}, {})", offset.x, offset.y),
                    None,
                    None,
                ));
            }
            options.insert("offset".into(), json!({ "x": offset.x, "y": offset.y }));
        }
        options.insert("values".into(), json!(self.values));
        Ok(Value::Object(options))
    }
}

/// What a duplicate made (`FormDuplicateResult`).
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormDuplicateResult {
    /// The fields made, and each field that got a new button, as they are now.
    pub fields: Vec<FormField>,
    /// The new widgets; for `duplicate_widgets`, in the order of the widgets given.
    pub widgets: Vec<AnnotationRef>,
    /// The new widgets as the engine wrote them, a JSON array in the same order.
    pub widgets_json: String,
    /// What was left out, and why, as JSON.
    pub dropped_json: String,
}

fn duplicate_result(result: &Value) -> Result<FormDuplicateResult, ViewerError> {
    let widgets = result.get("widgets").and_then(Value::as_array).map_or(&[][..], Vec::as_slice);
    Ok(FormDuplicateResult {
        fields: fields_out(
            result.get("fields").and_then(Value::as_array).map_or(&[], Vec::as_slice),
        )?,
        widgets: widgets
            .iter()
            .map(|w| {
                annotation_ref_of(w.get("ref").unwrap_or(&Value::Null))?
                    .ok_or_else(|| invalid_state("a copied widget has no ref".into()))
            })
            .collect::<Result<_, Error>>()?,
        widgets_json: Value::Array(widgets.to_vec()).to_string(),
        dropped_json: result.get("dropped").cloned().unwrap_or_else(|| json!([])).to_string(),
    })
}

/// What `import` did (`FormImportResult`).
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormImportResult {
    pub fields: Vec<FormField>,
    /// Each imported field's ref in the bundle and here, as JSON.
    pub ref_map_json: String,
    /// What was left out, and why, as JSON.
    pub dropped_json: String,
}

#[derive(Clone, Debug, PartialEq, uniffi::Enum)]
pub enum FormActivationResult {
    /// The form's own scripts ran.
    Form { result: FormCommitResult },
    /// The widget's action ran.
    Dispatched,
}

/// Options for moving the cursor to one field.
#[derive(Clone, Debug, Default, uniffi::Record)]
pub struct FormFocusOptions {
    #[uniffi(default = false)]
    pub select: bool,
    #[uniffi(default = None)]
    pub widget: Option<AnnotationRef>,
}
/// Options for moving as Tab does.
#[derive(Clone, Debug, Default, uniffi::Record)]
pub struct FormFocusNextOptions {
    #[uniffi(default = false)]
    pub backward: bool,
    #[uniffi(default = false)]
    pub empty: bool,
    #[uniffi(default = false)]
    pub wrap: bool,
    #[uniffi(default = None)]
    pub from: Option<FormFieldRef>,
}
/// A control lends a target while mounted (`FormFocusTarget`). Called on the
/// kernel thread; never wait on it.
#[uniffi::export(with_foreign)]
pub trait FormFocusTarget: Send + Sync {
    /// Asked each time the cursor is about to move: `false` for a box that
    /// holds nothing to focus (a signature field nobody here can sign), so
    /// `focus`, `focus_next` and Tab pass over its field.
    fn takes_cursor(&self) -> bool;
    /// Take the cursor (`select` selects a text box's text). `false` when
    /// nothing took it after all: the cursor then passes over the box.
    fn focus(&self, select: bool) -> bool;
}

/// The plugin's target for a native one: both calls go to it.
fn focus_target_of(target: Arc<dyn FormFocusTarget>) -> crate::services::cursor::FocusTarget {
    let takes = target.clone();
    crate::services::cursor::FocusTarget {
        takes_cursor: Some(std::rc::Rc::new(move || takes.takes_cursor())),
        focus: std::rc::Rc::new(move |select| target.focus(select)),
    }
}
#[derive(Clone, Copy, Debug, uniffi::Enum)]
pub enum FormWidgetEvent {
    CursorEnter,
    CursorExit,
    MouseDown,
    MouseUp,
    Focus,
    Blur,
}
impl From<FormWidgetEvent> for epdf_plugin_actions::PdfAnnotationEventKind {
    fn from(event: FormWidgetEvent) -> Self {
        match event {
            FormWidgetEvent::CursorEnter => Self::CursorEnter,
            FormWidgetEvent::CursorExit => Self::CursorExit,
            FormWidgetEvent::MouseDown => Self::MouseDown,
            FormWidgetEvent::MouseUp => Self::MouseUp,
            FormWidgetEvent::Focus => Self::Focus,
            FormWidgetEvent::Blur => Self::Blur,
        }
    }
}

// ── events ───────────────────────────────────────────────────────────────────

#[derive(Clone, Debug, PartialEq, uniffi::Enum)]
pub enum FormEvent {
    FocusChanged {
        field: Option<FormField>,
        widget: Option<AnnotationRef>,
    },
    ValueChanged {
        field: FormField,
        origin_json: String,
    },
    FieldCreated {
        field: FormField,
        origin_json: String,
    },
    FieldUpdated {
        field: FormField,
        origin_json: String,
    },
    FieldDeleted {
        field_ref: FormFieldRef,
        origin_json: String,
    },
    ValidationRejected {
        field: FormField,
        messages: Vec<String>,
    },
    /// The whole form was read.
    Resynced,
    /// `set_design_group` changed the group fields placed with a tool go in.
    DesignGroupChanged {
        group_id: Option<String>,
    },
    /// `set_groups` changed who the document's groups are: the groups as
    /// `groups` gives them now.
    GroupsChanged {
        groups: Vec<FormGroup>,
    },
}

#[uniffi::export(with_foreign)]
pub trait FormEventListener: Send + Sync {
    fn on_event(&self, event: FormEvent);
    /// The document closed: no event follows.
    fn on_end(&self);
}

// ── the handle ───────────────────────────────────────────────────────────────

#[derive(uniffi::Object)]
pub struct FormHandle {
    cap: KernelBound<FormCapability>,
    post: KernelPost,
}

impl FormHandle {
    /// On the kernel thread.
    pub fn new(cap: FormCapability, post: KernelPost) -> Arc<Self> {
        Arc::new(FormHandle { cap: KernelBound::new(cap, post.clone()), post })
    }
}

fn field_result(value: &Value) -> Result<FormField, ViewerError> {
    Ok(FormField::from_json(value.get("field").unwrap_or(&Value::Null))?)
}

#[uniffi::export]
impl FormHandle {
    pub async fn focus(
        &self,
        field: FormFieldRef,
        options: Option<FormFocusOptions>,
    ) -> Result<bool, ViewerError> {
        self.cap.call(move |c| {
            let options=options.map(|o|json!({"select":o.select,"widget":o.widget.map(|w|annotation_ref_json(&w))}));
            Ok(open(c)?.focus(&field_ref_json(&field),options.as_ref()))
        }).await
    }
    pub async fn focus_next(
        &self,
        options: Option<FormFocusNextOptions>,
    ) -> Result<bool, ViewerError> {
        self.cap
            .call(move |c| {
                let options = options.map(|o| {
                    let mut options = json!({"backward":o.backward,"empty":o.empty,"wrap":o.wrap});
                    if let Some(from) = o.from {
                        options["from"] = field_ref_json(&from);
                    }
                    options
                });
                Ok(open(c)?.focus_next(options.as_ref()))
            })
            .await
    }
    pub async fn focused_field(&self) -> Result<Option<FormField>, ViewerError> {
        self.cap
            .call(|c| open(c)?.get_focused_field().map(|f| FormField::from_json(&f)).transpose())
            .await
    }
    pub async fn move_in_group(
        &self,
        widget: AnnotationRef,
        backward: bool,
    ) -> Result<bool, ViewerError> {
        self.cap
            .call(move |c| Ok(open(c)?.move_in_group(&annotation_ref_json(&widget), backward)))
            .await
    }
    pub async fn attach_focus_target(
        &self,
        widget: AnnotationRef,
        target: Arc<dyn FormFocusTarget>,
    ) -> Result<Arc<RegistrationHandle>, ViewerError> {
        let post = self.post.clone();
        self.cap
            .call(move |c| {
                let off = open(c)?
                    .attach_focus_target(&annotation_ref_json(&widget), focus_target_of(target));
                Ok(RegistrationHandle::new(post, move || off.call()))
            })
            .await
    }
    pub async fn notify_widget_event(
        &self,
        field: FormFieldRef,
        widget: AnnotationRef,
        event: FormWidgetEvent,
    ) -> Result<(), ViewerError> {
        self.cap
            .call(move |c| {
                open(c)?.notify_widget_event(
                    &field_ref_json(&field),
                    &annotation_ref_json(&widget),
                    event.into(),
                )
            })
            .await
    }

    /// `None` until the form is read, or while reading is forbidden.
    pub async fn snapshot(&self) -> Result<Option<FormSnapshot>, ViewerError> {
        self.cap
            .call(|c| open(c)?.get_snapshot().map(|s| FormSnapshot::from_json(&s)).transpose())
            .await
    }

    pub async fn status(&self) -> Result<ResourceStatus, ViewerError> {
        self.cap
            .call(|c| {
                use epdf_kernel::mirror::ResourceStatus as Kernel;
                Ok(match open(c)?.get_status() {
                    Kernel::Idle => ResourceStatus::Idle,
                    Kernel::Loading => ResourceStatus::Loading,
                    Kernel::Ready => ResourceStatus::Ready,
                    Kernel::Forbidden => ResourceStatus::Forbidden,
                    Kernel::Error => ResourceStatus::Error,
                })
            })
            .await
    }

    pub async fn refresh(&self, cancel: Arc<CancelToken>) -> Result<(), ViewerError> {
        self.cap.call_promise_cancellable(cancel, |c| Ok(open(c)?.refresh(None)), |()| ()).await
    }

    pub async fn field(&self, field_ref: FormFieldRef) -> Result<Option<FormField>, ViewerError> {
        self.cap
            .call(move |c| optional_field(open(c)?.get(&field_ref_json(&field_ref)).as_deref()))
            .await
    }

    pub async fn field_for_widget(
        &self,
        widget: AnnotationRef,
    ) -> Result<Option<FormField>, ViewerError> {
        self.cap
            .call(move |c| {
                optional_field(
                    open(c)?.get_field_for_widget(&annotation_ref_json(&widget)).as_deref(),
                )
            })
            .await
    }

    pub async fn fields(&self, filter: Option<FormFilter>) -> Result<Vec<FormField>, ViewerError> {
        self.cap
            .call(move |c| {
                let filter = filter.map(|f| f.to_json());
                open(c)?.list(filter.as_ref()).iter().map(|f| FormField::from_json(f)).collect()
            })
            .await
    }

    pub async fn value(
        &self,
        field_ref: FormFieldRef,
    ) -> Result<Option<FormValueInput>, ViewerError> {
        self.cap
            .call(move |c| {
                open(c)?
                    .get_value(&field_ref_json(&field_ref))
                    .as_deref()
                    .map(FormValueInput::from_json)
                    .transpose()
            })
            .await
    }

    /// Every value by full name, as plain data, JSON.
    pub async fn export_values(&self) -> Result<String, ViewerError> {
        self.cap.call(|c| Ok(open(c)?.export_values().to_string())).await
    }

    pub async fn validate(&self) -> Result<FormValidationResult, ViewerError> {
        self.cap
            .call(|c| {
                let result = open(c)?.validate();
                Ok(FormValidationResult {
                    valid: result.valid,
                    missing: result
                        .missing
                        .iter()
                        .map(|f| FormField::from_json(f))
                        .collect::<Result<_, Error>>()?,
                })
            })
            .await
    }

    /// Each widget on a page, as a fill control draws it.
    pub async fn widgets(&self, page: PageRef) -> Result<Vec<FormFillItem>, ViewerError> {
        self.cap
            .call(move |c| open(c)?.list_widgets(&page_json(page)).iter().map(fill_item).collect())
            .await
    }

    /// Every page's widgets come with the form: resolves once it is read.
    pub async fn ensure_loaded(
        &self,
        page: PageRef,
        cancel: Arc<CancelToken>,
    ) -> Result<(), ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| Ok(open(c)?.ensure_loaded(&page_json(page))),
                |()| (),
            )
            .await
    }

    pub async fn widget_at(
        &self,
        page: PageRef,
        x: f64,
        y: f64,
    ) -> Result<Option<FormWidgetHit>, ViewerError> {
        self.cap
            .call(move |c| {
                open(c)?
                    .get_widget_at(&page_json(page), x, y)
                    .map(|hit| {
                        let bounds = rect_of(&hit.place.rect);
                        Ok(FormWidgetHit {
                            annot_object_number: hit.annot_object_number as u32,
                            field: FormField::from_json(&hit.field)?,
                            bounds,
                            frame: if hit.place.bx.is_null() {
                                bounds
                            } else {
                                rect_of(&hit.place.bx)
                            },
                            rotation: rotation_of(Some(&hit.place.rotation)),
                        })
                    })
                    .transpose()
            })
            .await
    }

    pub async fn page_box(&self, page: PageRef) -> Result<Option<Rect>, ViewerError> {
        self.cap
            .call(move |c| {
                Ok(open(c)?.get_page_box(&page_json(page)).map(|b| Rect {
                    x: b.x,
                    y: b.y,
                    width: b.width,
                    height: b.height,
                }))
            })
            .await
    }

    pub async fn set_value(
        &self,
        field_ref: FormFieldRef,
        value: FormValueInput,
        cancel: Arc<CancelToken>,
    ) -> Result<FormSetValueResult, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| Ok(open(c)?.set_value(field_ref_json(&field_ref), value.to_json(), None)),
                |result| set_value_result(&result),
            )
            .await?
    }

    pub async fn set_values(
        &self,
        entries: Vec<FormValueEntry>,
        cancel: Arc<CancelToken>,
    ) -> Result<FormBatchResult, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    let entries = entries
                        .iter()
                        .map(|e| (field_ref_json(&e.field_ref), e.value.to_json()))
                        .collect();
                    Ok(open(c)?.set_values(entries, None))
                },
                |result| batch_result(&result),
            )
            .await?
    }

    /// Plain values by full name (`exportValues()`' JSON).
    pub async fn import_values(
        &self,
        values_json: String,
        cancel: Arc<CancelToken>,
    ) -> Result<FormBatchResult, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    let values: Value = decode(
                        serde_json::from_str(&values_json)
                            .map_err(|e| invalid_state(e.to_string()))?,
                    )?;
                    Ok(open(c)?.import_values(values, None))
                },
                |result| batch_result(&result),
            )
            .await?
    }

    /// Each field's default, for the fields given, or every field you may
    /// fill in. Resolves the fields it changed.
    pub async fn reset(
        &self,
        fields: Option<Vec<FormFieldRef>>,
        cancel: Arc<CancelToken>,
    ) -> Result<Vec<FormField>, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    Ok(open(c)?.reset(fields.map(|f| f.iter().map(field_ref_json).collect()), None))
                },
                |result| {
                    Ok::<_, ViewerError>(fields_out(
                        result.get("fields").and_then(Value::as_array).map_or(&[], Vec::as_slice),
                    )?)
                },
            )
            .await?
    }

    pub async fn create(
        &self,
        input: FormCreateFieldInput,
        cancel: Arc<CancelToken>,
    ) -> Result<FormField, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| Ok(open(c)?.create(input.to_json(), None)),
                |result| field_result(&result),
            )
            .await?
    }

    /// The field in one more place (`addWidget`).
    pub async fn add_widget(
        &self,
        field_ref: FormFieldRef,
        placement: FormWidgetPlacement,
        cancel: Arc<CancelToken>,
    ) -> Result<FormWidgetAdded, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    Ok(open(c)?.add_widget(field_ref_json(&field_ref), placement.to_json(), None))
                },
                |result| {
                    Ok::<_, ViewerError>(FormWidgetAdded {
                        field: field_result(&result)?,
                        widget_json: result
                            .get("widget")
                            .cloned()
                            .unwrap_or(Value::Null)
                            .to_string(),
                    })
                },
            )
            .await?
    }

    pub async fn update(
        &self,
        field_ref: FormFieldRef,
        patch: FormFieldPatch,
        cancel: Arc<CancelToken>,
    ) -> Result<FormField, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| Ok(open(c)?.update(field_ref_json(&field_ref), patch.to_json()?, None)),
                |result| field_result(&result),
            )
            .await?
    }

    pub async fn delete(
        &self,
        field_ref: FormFieldRef,
        cancel: Arc<CancelToken>,
    ) -> Result<(), ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| Ok(open(c)?.delete(field_ref_json(&field_ref), None)),
                |_| (),
            )
            .await
    }

    /// One widget out of a field: it stays on its page, in no field.
    pub async fn remove_widget(
        &self,
        field_ref: FormFieldRef,
        widget: AnnotationRef,
        cancel: Arc<CancelToken>,
    ) -> Result<FormField, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    Ok(open(c)?.remove_widget(
                        field_ref_json(&field_ref),
                        annotation_ref_json(&widget),
                        None,
                    ))
                },
                |result| field_result(&result),
            )
            .await?
    }

    /// Fields out as a bundle (`FormBundle`), JSON: every field, or the
    /// ones named.
    pub async fn export(
        &self,
        fields: Option<Vec<FormFieldRef>>,
        cancel: Arc<CancelToken>,
    ) -> Result<String, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    let selection = fields.map(
                        |f| json!({ "fields": f.iter().map(field_ref_json).collect::<Vec<_>>() }),
                    );
                    Ok(open(c)?.export(selection, None))
                },
                |bundle| bundle.to_string(),
            )
            .await
    }

    /// A bundle's fields copied into this document. `options_json` is
    /// `FormImportOptions` (`{}` for the defaults).
    pub async fn import(
        &self,
        bundle_json: String,
        options_json: String,
        cancel: Arc<CancelToken>,
    ) -> Result<FormImportResult, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    let bundle: Value = serde_json::from_str(&bundle_json)
                        .map_err(|e| invalid_state(e.to_string()))?;
                    let options: Value = serde_json::from_str(&options_json)
                        .map_err(|e| invalid_state(e.to_string()))?;
                    Ok(open(c)?.import(bundle, options, None))
                },
                |result| {
                    Ok::<_, ViewerError>(FormImportResult {
                        fields: fields_out(
                            result
                                .get("fields")
                                .and_then(Value::as_array)
                                .map_or(&[], Vec::as_slice),
                        )?,
                        ref_map_json: result
                            .get("refMap")
                            .cloned()
                            .unwrap_or(Value::Null)
                            .to_string(),
                        dropped_json: result
                            .get("dropped")
                            .cloned()
                            .unwrap_or(Value::Null)
                            .to_string(),
                    })
                },
            )
            .await?
    }

    /// Copy whole fields (`duplicate`): each a new field named after its
    /// original (`email` becomes `email_2`), with its settings, scripts and
    /// widgets, as one step to undo. Rejects `permission-denied` without
    /// `doc.forms.modify`, `not-found` for a field the form doesn't have.
    pub async fn duplicate(
        &self,
        fields: Vec<FormFieldRef>,
        options: FormDuplicateOptions,
        cancel: Arc<CancelToken>,
    ) -> Result<FormDuplicateResult, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    let fields = fields.iter().map(field_ref_json).collect();
                    let c = open(c)?;
                    let options = options.to_json()?;
                    epdf_ffi::signalled(|signal| Ok(c.duplicate(fields, options, Some(signal))))
                },
                |result| duplicate_result(&result),
            )
            .await?
    }

    /// Copy widgets as Duplicate does in a form designer (`duplicateWidgets`):
    /// a radio button's copy joins its group, any other widget's copy is a new
    /// field. Rejects `permission-denied` without `doc.forms.modify`,
    /// `not-found` for a widget the form doesn't show.
    pub async fn duplicate_widgets(
        &self,
        widgets: Vec<AnnotationRef>,
        options: FormDuplicateOptions,
        cancel: Arc<CancelToken>,
    ) -> Result<FormDuplicateResult, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    let widgets = widgets.iter().map(annotation_ref_json).collect();
                    let c = open(c)?;
                    let options = options.to_json()?;
                    epdf_ffi::signalled(|signal| {
                        Ok(c.duplicate_widgets(widgets, options, Some(signal)))
                    })
                },
                |result| duplicate_result(&result),
            )
            .await?
    }

    /// Fix a form other apps read differently. Resolves the repair's result
    /// as JSON (`FormRepairResult`).
    pub async fn repair(
        &self,
        bake_appearances: bool,
        cancel: Arc<CancelToken>,
    ) -> Result<String, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    Ok(open(c)?.repair(Some(json!({ "bakeAppearances": bake_appearances })), None))
                },
                |result| result.to_string(),
            )
            .await
    }

    /// What a click on a widget does.
    pub async fn activate_widget(
        &self,
        widget: AnnotationRef,
        cancel: Arc<CancelToken>,
    ) -> Result<FormActivationResult, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| Ok(open(c)?.activate_widget(annotation_ref_json(&widget), None)),
                |r| match r {
                    crate::WidgetActivationResult::Form(result) => {
                        FormActivationResult::Form { result: result.into() }
                    }
                    crate::WidgetActivationResult::Dispatched(_) => {
                        FormActivationResult::Dispatched
                    }
                },
            )
            .await
    }

    /// The document's groups (`listGroups`): the ones `set_groups` named, in
    /// that order, then the other groups the form's fields are in.
    pub async fn groups(&self) -> Result<Vec<FormGroup>, ViewerError> {
        self.cap.call(|c| Ok(groups_out(&open(c)?.list_groups()))).await
    }

    /// Say who this document's groups are, the whole list (`setGroups`); an
    /// empty list clears it. Fires `GroupsChanged`; the same list again
    /// changes nothing. Session state: nothing is written into the PDF.
    pub async fn set_groups(&self, groups: Vec<FormGroupInput>) -> Result<(), ViewerError> {
        self.cap
            .call(move |c| {
                let groups: Vec<Value> = groups.iter().map(FormGroupInput::to_json).collect();
                open(c)?.set_groups(&groups);
                Ok(())
            })
            .await
    }

    /// The group fields placed with a tool go in (`getDesignGroup`); `None`,
    /// where it starts, names none.
    pub async fn design_group(&self) -> Result<Option<String>, ViewerError> {
        self.cap.call(|c| Ok(open(c)?.get_design_group())).await
    }

    /// Fields placed with a tool go into this group; `None` names none
    /// (`setDesignGroup`). Fires `DesignGroupChanged`. Refused with
    /// `permission-denied` for a group the session may not put a field in.
    pub async fn set_design_group(&self, group_id: Option<String>) -> Result<(), ViewerError> {
        self.cap.call(move |c| open(c)?.set_design_group(group_id.as_deref())).await
    }

    /// Whether the session may design the form and put a field in this
    /// group (`canSetGroup`).
    pub async fn can_set_group(&self, group_id: String) -> Result<bool, ViewerError> {
        self.cap.call(move |c| Ok(open(c)?.can_set_group(&group_id))).await
    }

    /// Put fields in a group, or with `None` take them out of theirs, as one
    /// step to undo (`moveToGroup`). Resolves the fields as they are now, in
    /// the order given. Rejects `permission-denied` without
    /// `doc.forms.modify` or for a group the session may not put a field in,
    /// `not-found` for a field the form doesn't have.
    pub async fn move_to_group(
        &self,
        fields: Vec<FormFieldRef>,
        group_id: Option<String>,
        cancel: Arc<CancelToken>,
    ) -> Result<Vec<FormField>, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    let refs = fields.iter().map(field_ref_json).collect();
                    let c = open(c)?;
                    epdf_ffi::signalled(|signal| Ok(c.move_to_group(refs, group_id, Some(signal))))
                },
                |result| {
                    Ok::<_, ViewerError>(fields_out(
                        result.get("fields").and_then(Value::as_array).map_or(&[], Vec::as_slice),
                    )?)
                },
            )
            .await?
    }

    /// Every field with a selected widget in design mode, in the
    /// selection's order, each once (`listSelectedFields`).
    pub async fn selected_fields(&self) -> Result<Vec<FormField>, ViewerError> {
        self.cap
            .call(|c| {
                open(c)?.list_selected_fields().iter().map(|f| FormField::from_json(f)).collect()
            })
            .await
    }

    /// Where UI attaches to one box of a field (`getWidgetAnchor`); `None`
    /// for a box that isn't there, or shows nowhere.
    pub async fn widget_anchor(
        &self,
        widget: AnnotationRef,
    ) -> Result<Option<FormWidgetAnchor>, ViewerError> {
        self.cap
            .call(move |c| {
                open(c)?
                    .get_widget_anchor(&annotation_ref_json(&widget))
                    .map(|anchor| {
                        Ok(FormWidgetAnchor {
                            page: page_of(anchor.get("page").unwrap_or(&Value::Null))?,
                            bounds: rect_of(anchor.get("bounds").unwrap_or(&Value::Null)),
                        })
                    })
                    .transpose()
            })
            .await
    }

    /// Whether the cursor is in this box (`isWidgetFocused`).
    pub async fn is_widget_focused(&self, widget: AnnotationRef) -> Result<bool, ViewerError> {
        self.cap.call(move |c| Ok(open(c)?.is_widget_focused(&annotation_ref_json(&widget)))).await
    }

    pub async fn can_read(&self) -> Result<bool, ViewerError> {
        self.cap.call(|c| Ok(open(c)?.can_read())).await
    }

    pub async fn can_fill(&self, field_ref: FormFieldRef) -> Result<bool, ViewerError> {
        self.cap.call(move |c| Ok(open(c)?.can_fill(&field_ref_json(&field_ref)))).await
    }

    pub async fn can_design(&self) -> Result<bool, ViewerError> {
        self.cap.call(|c| Ok(open(c)?.can_design())).await
    }

    pub async fn can_restore_attribution(&self) -> Result<bool, ViewerError> {
        self.cap.call(|c| Ok(open(c)?.can_restore_attribution())).await
    }

    pub async fn can_write_scripts(&self) -> Result<bool, ViewerError> {
        self.cap.call(|c| Ok(open(c)?.can_write_scripts())).await
    }

    pub fn subscribe(&self, listener: Arc<dyn FormEventListener>) -> Arc<Subscription> {
        let (subscription, slot) = Subscription::pending(self.post.clone());
        self.cap.fire(move |c| {
            let ending = listener.clone();
            slot.fill_until_end(
                c.lifetime(),
                || {
                    let origin =
                        |e: &Value| e.get("origin").cloned().unwrap_or(Value::Null).to_string();
                    let field =
                        |e: &Value| FormField::from_json(e.get("field").unwrap_or(&Value::Null));
                    let l = listener.clone();
                    let value = c.on_value_changed(move |e| {
                        l.on_event(FormEvent::ValueChanged {
                            field: field(e)?,
                            origin_json: origin(e),
                        });
                        Ok(())
                    });
                    let l = listener.clone();
                    let created = c.on_field_created(move |e| {
                        l.on_event(FormEvent::FieldCreated {
                            field: field(e)?,
                            origin_json: origin(e),
                        });
                        Ok(())
                    });
                    let l = listener.clone();
                    let updated = c.on_field_updated(move |e| {
                        l.on_event(FormEvent::FieldUpdated {
                            field: field(e)?,
                            origin_json: origin(e),
                        });
                        Ok(())
                    });
                    let l = listener.clone();
                    let deleted = c.on_field_deleted(move |e| {
                        l.on_event(FormEvent::FieldDeleted {
                            field_ref: field_ref_of(e.get("ref").unwrap_or(&Value::Null))?,
                            origin_json: origin(e),
                        });
                        Ok(())
                    });
                    let l = listener.clone();
                    let rejected = c.on_validation_rejected(move |e| {
                        let messages = e
                            .get("issues")
                            .and_then(Value::as_array)
                            .into_iter()
                            .flatten()
                            .filter_map(|d| {
                                d.get("message").and_then(Value::as_str).map(str::to_string)
                            })
                            .collect();
                        l.on_event(FormEvent::ValidationRejected { field: field(e)?, messages });
                        Ok(())
                    });
                    let l = listener.clone();
                    let resynced = c.on_resynced(move |_| {
                        l.on_event(FormEvent::Resynced);
                        Ok(())
                    });
                    let l = listener.clone();
                    let focused = c.on_focus_changed(move |event| {
                        let field = event
                            .get("field")
                            .filter(|f| !f.is_null())
                            .map(FormField::from_json)
                            .transpose()?;
                        let widget =
                            annotation_ref_of(event.get("widget").unwrap_or(&Value::Null))?;
                        l.on_event(FormEvent::FocusChanged { field, widget });
                        Ok(())
                    });
                    let l = listener.clone();
                    let design_group = c.on_design_group_changed(move |event| {
                        let group_id =
                            event.get("groupId").and_then(Value::as_str).map(str::to_string);
                        l.on_event(FormEvent::DesignGroupChanged { group_id });
                        Ok(())
                    });
                    let l = listener.clone();
                    let groups = c.on_groups_changed(move |event| {
                        let list = event.get("groups").and_then(Value::as_array);
                        let groups = groups_out(list.map_or(&[], Vec::as_slice));
                        l.on_event(FormEvent::GroupsChanged { groups });
                        Ok(())
                    });
                    let offs = [
                        value,
                        created,
                        updated,
                        deleted,
                        rejected,
                        resynced,
                        focused,
                        design_group,
                        groups,
                    ];
                    Unsubscribe::new(move || offs.iter().for_each(Unsubscribe::call))
                },
                move || ending.on_end(),
            );
        });
        subscription
    }
}

/// One entry of `set_values`.
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct FormValueEntry {
    pub field_ref: FormFieldRef,
    pub value: FormValueInput,
}

/// Which fields `fields` returns: every member optional.
#[derive(Clone, Debug, Default, PartialEq, uniffi::Record)]
pub struct FormFilter {
    #[uniffi(default = None)]
    pub family: Option<FormFieldFamily>,
    /// Fields with a widget on this page.
    #[uniffi(default = None)]
    pub page: Option<PageRef>,
    /// One full name.
    #[uniffi(default = None)]
    pub name: Option<String>,
}

impl FormFilter {
    fn to_json(&self) -> Value {
        let mut filter = Map::new();
        if let Some(family) = self.family {
            filter.insert("kind".into(), json!(family.as_str()));
        }
        if let Some(page) = self.page {
            filter.insert("page".into(), page_json(page));
        }
        if let Some(name) = &self.name {
            filter.insert("name".into(), json!(name));
        }
        Value::Object(filter)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use epdf_acrojs::{ScriptDiagnostic, ScriptDiagnosticCode};

    /// A refusal by the form's own scripts is a result, not an error.
    #[test]
    fn a_rejected_commit_is_a_result_not_an_error() {
        let rejected = crate::FormCommitResult {
            diagnostics: vec![ScriptDiagnostic {
                code: ScriptDiagnosticCode::InvalidFieldValue,
                message: "Enter a number".into(),
            }],
            ..crate::FormCommitResult::bare(crate::FormCommitStatus::Rejected, true)
        };
        assert_eq!(
            FormCommitResult::from(rejected),
            FormCommitResult {
                status: FormCommitStatus::Rejected,
                scripted: true,
                diagnostics: vec!["Enter a number".into()],
                error: None,
            }
        );
    }

    /// `FormSnapshot::from_json` as it decoded a copy of the whole value.
    fn snapshot_from_a_copy(value: &Value) -> Result<FormSnapshot, Error> {
        #[derive(Deserialize)]
        #[serde(rename_all = "camelCase")]
        struct Dto {
            form_kind: FormKind,
            needs_appearances: bool,
            fields: Vec<Value>,
            #[serde(default)]
            widgets: Vec<Value>,
            #[serde(default)]
            calculation_order: Vec<Value>,
        }
        let dto: Dto = decode(value.clone())?;
        Ok(FormSnapshot {
            form_kind: dto.form_kind,
            needs_appearances: dto.needs_appearances,
            fields: fields_out(&dto.fields)?,
            calculation_order: dto
                .calculation_order
                .iter()
                .map(|r| if r.is_null() { Ok(None) } else { field_ref_of(r).map(Some) })
                .collect::<Result<_, Error>>()?,
            widgets_json: Value::Array(dto.widgets).to_string(),
        })
    }

    fn a_field() -> Value {
        json!({
            "ref": { "kind": "objectNumber", "objectNumber": 4 }, "name": "f", "kind": "text",
            "origin": "acroform", "readOnly": false, "required": true, "noExport": false,
            "value": "typed", "maxLength": 9,
            "widgets": [{ "ref": { "kind": "objectNumber", "objectNumber": 5,
                                   "page": { "kind": "objectNumber", "objectNumber": 1 } },
                          "objectNumber": 5, "page": { "kind": "objectNumber", "objectNumber": 1 } }],
        })
    }

    /// Read in place, a snapshot and a field decode as a copy of them did:
    /// the same records, and the same refusals word for word.
    #[test]
    fn decoding_in_place_accepts_and_refuses_as_a_copy_did() {
        let whole = json!({
            "formKind": "acroform", "needsAppearances": false, "fields": [a_field()],
            "widgets": [{ "subtype": "widget", "rect": { "x": 1.5 } }],
            "calculationOrder": [null, { "kind": "objectNumber", "objectNumber": 4 }],
        });
        let with = |key: &str, member: Value| {
            let mut value = whole.clone();
            value[key] = member;
            value
        };
        let without = |key: &str| {
            let mut value = whole.clone();
            value.as_object_mut().unwrap().shift_remove(key);
            value
        };
        let mut bad_field = a_field();
        bad_field["readOnly"] = json!("no");
        let snapshots = [
            whole.clone(),
            without("widgets"),
            without("calculationOrder"),
            without("fields"),
            with("fields", json!({})),
            with("fields", json!([bad_field])),
            with("widgets", Value::Null),
            with("widgets", json!("rows")),
            with("calculationOrder", json!([7])),
            with("formKind", json!("pdf")),
            with("needsAppearances", json!(1)),
            json!([]),
            Value::Null,
        ];
        for value in &snapshots {
            assert_eq!(
                format!("{:?}", FormSnapshot::from_json(value)),
                format!("{:?}", snapshot_from_a_copy(value)),
                "{value}"
            );
        }
        assert!(FormSnapshot::from_json(&whole).is_ok(), "{:?}", FormSnapshot::from_json(&whole));
        let mut fields = vec![a_field(), json!(7), json!({ "name": "f" })];
        for (key, member) in
            [("widgets", json!([{}])), ("maxLength", json!(1.5)), ("kind", json!(3))]
        {
            let mut field = a_field();
            field[key] = member;
            fields.push(field);
        }
        for field in &fields {
            let in_place = FieldDto::deserialize(field).map(|_| ()).map_err(|e| e.to_string());
            let copied = serde_json::from_value::<FieldDto>(field.clone())
                .map(|_| ())
                .map_err(|e| e.to_string());
            assert_eq!(in_place, copied, "{field}");
            assert_eq!(
                format!("{:?}", FormField::from_json(field)),
                format!(
                    "{:?}",
                    decode::<FieldDto>(field.clone()).and_then(|_| FormField::from_json(field))
                ),
            );
        }
    }

    /// A radio button's item says where it comes in its group; an item with
    /// no frame (`box: null`) is laid out in the rect it stands in, unturned.
    #[test]
    fn a_radio_item_reads_its_place_in_the_group() {
        let item = json!({
            "fieldRef": { "kind": "objectNumber", "objectNumber": 4 }, "annotObjectNumber": 5,
            "annotationRef": null, "groupId": null, "canFill": true, "required": true,
            "empty": true, "accent": null, "box": null, "rotation": null,
            "rect": { "x": 1, "y": 2, "width": 3, "height": 4 }, "look": {}, "disabled": false,
            "label": "Plan: monthly", "control": "toggle", "kind": "radio", "checked": false,
            "exportValue": "monthly", "inGroup": { "position": 2, "size": 3 },
        });
        let read = fill_item(&item).unwrap();
        assert_eq!(read.frame, read.bounds);
        assert_eq!(read.rotation, 0.0);
        assert!(read.required && read.empty && read.accent.is_none());
        assert!(matches!(
            read.control,
            FormFillControl::Toggle {
                radio: true,
                in_group: Some(FormGroupPosition { position: 2, size: 3 }),
                ..
            }
        ));
        let mut unknown = item.clone();
        unknown["inGroup"] = Value::Null;
        assert!(matches!(
            fill_item(&unknown).unwrap().control,
            FormFillControl::Toggle { in_group: None, .. }
        ));
    }

    #[test]
    fn a_format_reads_and_writes_in_the_engines_spelling() {
        for (format, written) in [
            (
                FormFieldFormat::Date { pattern: "d/m/yy".into() },
                json!({ "kind": "date", "pattern": "d/m/yy" }),
            ),
            (
                FormFieldFormat::Time { pattern: "h:MM tt".into() },
                json!({ "kind": "time", "pattern": "h:MM tt" }),
            ),
            (FormFieldFormat::Other, json!({ "kind": "other" })),
        ] {
            assert_eq!(format.to_json(), written);
            assert_eq!(FormFieldFormat::from_json(&written), Some(format));
        }
        assert_eq!(FormFieldFormat::from_json(&Value::Null), None);
        assert_eq!(
            FormFieldFormat::from_json(&json!({ "kind": "number" })),
            Some(FormFieldFormat::Other)
        );
    }

    #[test]
    fn refs_are_written_in_the_current_spelling() {
        let field = FormFieldRef::ObjectNumber { field_object_number: 7 };
        assert_eq!(field_ref_json(&field), json!({ "kind": "objectNumber", "objectNumber": 7 }));
        assert_eq!(field_ref_of(&field_ref_json(&field)).unwrap(), field);
    }
}

mod settings;
pub use settings::*;

mod editing;
pub use editing::{
    FormCalculationPosition, FormFieldPanelSettings, FormSettingControl, FormSettingProperty,
    FormWidgetRecord,
};
