//! A form bundle's checks (`core/src/transfer/FormBundle.ts`).
//!
//! Form fields and their widgets: the design (`forms.import`) or only the
//! values (`forms.importValues`). Each row is what a read returns: a field
//! whole, and each of its widgets as the form's rows read them.

use std::collections::HashSet;

use serde_json::{Map, Value, json};

use super::bundle::{
    Bundle, BundleKind, assert_bundle_pages, assert_known_fields, assert_resource_ids,
    assert_resources_named, assert_row_resources, bundle_page_ref, invalid_bundle, json_text,
    page_key, record, whole_number,
};
use super::limits::{BundleLimits, Limit, assert_within_limit, manifest_bytes_of};
use crate::error::EngineError;

pub const FORM_BUNDLE_FORMAT: &str = "embedpdf/form";
pub const FORM_BUNDLE_VERSION: u64 = 1;

/// The manifest's keys, in the order a file writes them
/// (`FORM_BUNDLE_KEYS`).
pub const FORM_BUNDLE_KEYS: &[&str] =
    &["format", "version", "pages", "fields", "widgets", "calculationOrder"];

/// The resource roles a widget names: none yet (`WIDGET_RESOURCE_ROLES`).
const WIDGET_RESOURCE_ROLES: &[&str] = &[];

const BUNDLE_FIELDS: &[&str] =
    &["format", "version", "pages", "fields", "widgets", "calculationOrder", "resources"];
const FIELD_ROW_FIELDS: &[&str] = &["data"];
const WIDGET_ROW_FIELDS: &[&str] = &["data", "resources"];

const KIND: BundleKind = BundleKind::Form;

/// `completeFormBundle`: `bundle` with every row whole. A bundle written
/// before a field had a `purpose` and a `format` (a file kept from an
/// earlier version, an export from an older server) holds fields without
/// them: each gets both, as a read gives them (`withPurposeAndFormat`), and
/// `forAnyone: false` when it is older than that too (`withForAnyone`). One
/// written before a push button had `hasImage` and a widget its `layout`
/// gets those as such a read gives them too (`withButtonImage`,
/// `withWidgetLayout`). A row that has them all is left as it is.
pub fn complete_form_bundle(manifest: &mut Map<String, Value>) {
    if let Some(Value::Array(fields)) = manifest.get_mut("fields") {
        for row in fields {
            if let Some(Value::Object(data)) = row.get_mut("data") {
                with_purpose_and_format(data);
                with_button_image(data);
                crate::features::forms::anyone::with_for_anyone(data);
            }
        }
    }
    if let Some(Value::Array(widgets)) = manifest.get_mut("widgets") {
        for row in widgets {
            if let Some(Value::Object(data)) = row.get_mut("data") {
                with_widget_layout(data);
            }
        }
    }
}

/// `withPurposeAndFormat`: no purpose reads as none, and a text field's or a
/// dropdown's missing format as the one its scripts give.
fn with_purpose_and_format(field: &mut Map<String, Value>) {
    use crate::features::forms::format::{format_of_actions, takes_format};
    use crate::features::forms::snapshot::{FieldActions, FormFieldFamily};
    field.entry("purpose").or_insert(Value::Null);
    let family = field
        .get("kind")
        .and_then(|kind| serde_json::from_value::<FormFieldFamily>(kind.clone()).ok());
    if field.contains_key("format") || !family.is_some_and(takes_format) {
        return;
    }
    let actions = field
        .get("actions")
        .and_then(|actions| serde_json::from_value::<FieldActions<Value>>(actions.clone()).ok());
    let format = format_of_actions(actions.as_ref());
    field.insert("format".into(), serde_json::to_value(format).unwrap_or(Value::Null));
}

/// `withButtonImage`: a push button from before `hasImage` holds no image.
fn with_button_image(field: &mut Map<String, Value>) {
    if field.get("kind").and_then(Value::as_str) == Some("pushbutton") {
        field.entry("hasImage").or_insert(Value::Bool(false));
    }
}

/// `withWidgetLayout`: a widget row from before `layout`: a push button's
/// widget shows its caption alone, the PDF's default, and every other widget
/// has no layout.
fn with_widget_layout(row: &mut Map<String, Value>) {
    if row.contains_key("layout") {
        return;
    }
    let button = row.get("fieldKind").and_then(Value::as_str) == Some("pushbutton");
    row.insert("layout".into(), if button { json!("caption") } else { Value::Null });
}

/// Check a bundle before anything uses it (`assertFormBundle`): its
/// manifest, then every resource against its id.
pub fn assert_form_bundle(bundle: &Bundle, limits: &BundleLimits) -> Result<(), EngineError> {
    assert_form_bundle_manifest(&bundle.manifest, &bundle.sizes(), limits)?;
    assert_resource_ids(KIND, &bundle.resources)
}

/// The checks that need no resource bytes, only their sizes by id: format
/// and version, counts, sizes, the shape of pages, fields and widgets, and
/// that widgets and resources name each other exactly. That a widget's field
/// is among the bundle's is the import's to decide: one that isn't is left
/// out (`assertFormBundleManifest`).
pub fn assert_form_bundle_manifest(
    manifest: &Map<String, Value>,
    sizes: &[(String, usize)],
    limits: &BundleLimits,
) -> Result<(), EngineError> {
    assert_known_fields(KIND, "the bundle", manifest, BUNDLE_FIELDS)?;
    if manifest.get("format").and_then(Value::as_str) != Some(FORM_BUNDLE_FORMAT) {
        return Err(invalid_bundle(
            KIND,
            &format!("unknown format {}", json_text(manifest.get("format"))),
        ));
    }
    if manifest.get("version").and_then(whole_number) != Some(FORM_BUNDLE_VERSION) {
        return Err(invalid_bundle(
            KIND,
            &format!("unknown version {}", json_text(manifest.get("version"))),
        ));
    }
    let list = |key: &str| manifest.get(key).and_then(Value::as_array);
    let Some(pages) = list("pages") else {
        return Err(invalid_bundle(KIND, "pages is not a list"));
    };
    let Some(fields) = list("fields") else {
        return Err(invalid_bundle(KIND, "fields is not a list"));
    };
    let Some(widgets) = list("widgets") else {
        return Err(invalid_bundle(KIND, "widgets is not a list"));
    };
    let Some(calculation_order) = list("calculationOrder") else {
        return Err(invalid_bundle(KIND, "calculationOrder is not a list"));
    };

    assert_within_limit(KIND, limits, Limit::Items, (fields.len() + widgets.len()) as u64)?;
    assert_within_limit(KIND, limits, Limit::Pages, pages.len() as u64)?;
    assert_within_limit(KIND, limits, Limit::Resources, sizes.len() as u64)?;
    let mut bundle_bytes = manifest_bytes_of(&json!({
        "pages": pages, "fields": fields, "widgets": widgets,
        "calculationOrder": calculation_order,
    }));
    assert_within_limit(KIND, limits, Limit::ManifestBytes, bundle_bytes)?;
    for (_, size) in sizes {
        assert_within_limit(KIND, limits, Limit::ResourceBytes, *size as u64)?;
        bundle_bytes += *size as u64;
    }
    assert_within_limit(KIND, limits, Limit::BundleBytes, bundle_bytes)?;

    let page_keys = assert_bundle_pages(KIND, pages)?;
    for (index, row) in fields.iter().enumerate() {
        let data = record(row).and_then(|row| row.get("data")).and_then(record);
        let field = data.filter(|data| {
            data.get("kind").is_some_and(Value::is_string)
                && data.get("name").is_some_and(Value::is_string)
                && data.get("ref").is_some_and(Value::is_object)
        });
        if field.is_none() {
            return Err(invalid_bundle(KIND, &format!("field {index} has no field data")));
        }
        let row = record(row).expect("checked above");
        assert_known_fields(KIND, &format!("field {index}"), row, FIELD_ROW_FIELDS)?;
    }
    let mut named = HashSet::new();
    for (index, row) in widgets.iter().enumerate() {
        let data = record(row).and_then(|row| row.get("data")).and_then(record);
        let Some(data) =
            data.filter(|data| data.get("subtype").and_then(Value::as_str) == Some("widget"))
        else {
            return Err(invalid_bundle(KIND, &format!("widget {index} has no widget data")));
        };
        let row = record(row).expect("checked above");
        assert_known_fields(KIND, &format!("widget {index}"), row, WIDGET_ROW_FIELDS)?;
        let page = data.get("ref").and_then(record).and_then(|r| bundle_page_ref(r.get("page")));
        if !page.is_some_and(|page| page_keys.contains(&page_key(page))) {
            return Err(invalid_bundle(
                KIND,
                &format!("widget {index} is on a page the bundle doesn't list"),
            ));
        }
        assert_row_resources(
            KIND,
            &format!("widget {index}"),
            row.get("resources"),
            WIDGET_RESOURCE_ROLES,
            sizes,
            &mut named,
        )?;
    }
    for (index, reference) in calculation_order.iter().enumerate() {
        if !reference.is_object() {
            return Err(invalid_bundle(
                KIND,
                &format!("calculation order entry {index} is not a field ref"),
            ));
        }
    }
    assert_resources_named(KIND, "widget", sizes, &named)
}
