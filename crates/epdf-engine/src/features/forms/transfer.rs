//! A form's fields out of a document as a bundle
//! (`services/src/features/forms/FormExporter.ts`, 7f0b73a3c).
//! Operation: `forms.export`. The imports are change ops
//! ([`crate::features::changes::fields::import_form`],
//! [`crate::features::changes::forms::import_form_values`]), as upstream's
//! `FormImporter` runs inside `formChanges.ts`.

use std::collections::{HashMap, HashSet};

use serde_json::{Map, Value, json};

use super::button_image::read_widget_icon;
use super::snapshot::{FormWidgetRef, PageIndexLookup};
use crate::access::OwnedDocument;
use crate::cancel::CancelToken;
use crate::error::{EngineError, EngineErrorCode};
use crate::features::transfer::bundle::{Bundle, BundleKind};
use crate::features::transfer::form_bundle::{FORM_BUNDLE_FORMAT, FORM_BUNDLE_VERSION};
use crate::features::transfer::form_export::annotation_key_of;
use crate::features::transfer::form_export::{FormExportSelection, form_export_rows_of};
use crate::features::transfer::limits::{
    BundleLimits, Limit, assert_within_limit, manifest_bytes_of,
};
use crate::features::transfer::resources::{BundleResources, bundle_pages_of, import_targets};
use crate::identity::{AnnotationRef, PageRef};
use crate::session::document::DocumentSession;

fn unserializable(e: impl std::fmt::Display) -> EngineError {
    EngineError::with_message(
        EngineErrorCode::Unknown,
        format!("a form read did not serialize: {e}"),
    )
}

/// `forms.export`: the fields a selection takes, each whole with all of its
/// widgets, as one bundle in page space (`FormExporter.export`). It reads
/// the form as `forms.list` does and writes nothing. A widget is drawn from
/// its data, so its only resource is a push button's image: the drawing its
/// icon names, as a one-page PDF (`image`), once however many widgets show
/// it, and none for a field the form doesn't export. It is checked against
/// `limits` as it is built, so an export never makes one an import refuses.
pub fn export_form(
    session: &DocumentSession,
    selection: &FormExportSelection,
    limits: &BundleLimits,
    cancel: &CancelToken,
) -> Result<Bundle, EngineError> {
    cancel.check()?;
    let owned = session.document()?;
    let document = owned.document();
    let model = super::write::load_model(document)?;
    let form = super::widgets::read_form(document, &model, cancel)?;
    drop(model);
    let mut box_of = crate::features::pages::visible_box_reader(document);
    let snapshot = crate::page_space::forms::page_form_snapshot_of(form, &mut box_of)?;
    let snapshot = serde_json::to_value(snapshot).map_err(unserializable)?;
    let pages: Vec<PageRef> = import_targets(owned).into_iter().map(|target| target.page).collect();
    let rows = form_export_rows_of(&snapshot, selection, &pages)?;
    let kind = BundleKind::Form;
    assert_within_limit(
        kind,
        limits,
        Limit::Items,
        (rows.fields.len() + rows.widgets.len()) as u64,
    )?;
    let bundle_pages =
        serde_json::to_value(bundle_pages_of(owned, &rows.widgets)?).map_err(unserializable)?;
    let page_count = bundle_pages.as_array().map_or(0, Vec::len) as u64;
    // A widget's picture goes with its field's: none for a field the export
    // takes without one, as one the form doesn't export (bfbaac1f2).
    let without_picture: HashSet<String> = rows
        .fields
        .iter()
        .filter(|field| field["kind"] == "pushbutton" && field["hasImage"] != true)
        .flat_map(|field| field["widgets"].as_array().cloned().unwrap_or_default())
        .filter_map(|widget| annotation_key_of(&widget["ref"]))
        .collect();
    let mut images = ButtonImageCollector::new(*limits);
    let fields: Vec<Value> = rows.fields.into_iter().map(|data| json!({ "data": data })).collect();
    let widgets: Vec<Value> = rows
        .widgets
        .into_iter()
        .map(|data| {
            let resources = if annotation_key_of(&data["ref"])
                .is_some_and(|key| without_picture.contains(&key))
            {
                json!({})
            } else {
                images.of(owned, &data)?
            };
            Ok(json!({ "data": data, "resources": resources }))
        })
        .collect::<Result<_, EngineError>>()?;
    let calculation_order = Value::Array(rows.calculation_order);
    assert_within_limit(kind, limits, Limit::Pages, page_count)?;
    let manifest_bytes = manifest_bytes_of(&json!({
        "pages": bundle_pages, "fields": fields, "widgets": widgets,
        "calculationOrder": calculation_order,
    }));
    assert_within_limit(kind, limits, Limit::ManifestBytes, manifest_bytes)?;
    assert_within_limit(
        kind,
        limits,
        Limit::BundleBytes,
        manifest_bytes + images.resources.total_bytes,
    )?;
    let mut manifest = Map::new();
    manifest.insert("format".into(), json!(FORM_BUNDLE_FORMAT));
    manifest.insert("version".into(), json!(FORM_BUNDLE_VERSION));
    manifest.insert("pages".into(), bundle_pages);
    manifest.insert("fields".into(), Value::Array(fields));
    manifest.insert("widgets".into(), Value::Array(widgets));
    manifest.insert("calculationOrder".into(), calculation_order);
    let Value::Object(manifest) =
        crate::features::transfer::bundle::js_numbers(Value::Object(manifest))
    else {
        unreachable!("an object stays an object")
    };
    Ok(Bundle { manifest, resources: images.resources.by_id })
}

/// The images an export's push buttons show: each drawing once, as a
/// one-page PDF (`ButtonImageCollector`, 7d5691a5a).
struct ButtonImageCollector {
    resources: BundleResources,
    /// The id each drawing (by object number) was exported under.
    drawings: HashMap<u32, String>,
    pages: PageIndexLookup,
}

impl ButtonImageCollector {
    fn new(limits: BundleLimits) -> Self {
        Self {
            resources: BundleResources::new(BundleKind::Form, limits),
            drawings: HashMap::new(),
            pages: PageIndexLookup::default(),
        }
    }

    /// The resources a widget row (in page space) names: `{ image }` for a
    /// push button's that shows one.
    fn of(&mut self, owned: &OwnedDocument, row: &Value) -> Result<Value, EngineError> {
        if row["fieldKind"] != "pushbutton" {
            return Ok(json!({}));
        }
        let reference: AnnotationRef = serde_json::from_value(row["ref"].clone())
            .map_err(|e| unserializable(format!("a widget row's ref: {e}")))?;
        let AnnotationRef::ObjectNumber { page, annot } = reference else {
            return Ok(json!({}));
        };
        let document = owned.document();
        let widget = FormWidgetRef { annot_object_number: annot, page_object_number: page.get() };
        let Some(drawing) = read_widget_icon(document, &mut self.pages, &widget)? else {
            return Ok(json!({}));
        };
        if let Some(known) = self.drawings.get(&drawing) {
            return Ok(json!({ "image": known }));
        }
        let Some(exported) = document.export_drawing(drawing) else { return Ok(json!({})) };
        let bytes = exported.save_to_buffer(pdfium::PdfSaveMode::Full).map_err(|_| {
            EngineError::with_message(EngineErrorCode::DocOpenFailed, "failed to save a drawing")
        })?;
        let id = self.resources.keep(bytes)?;
        self.drawings.insert(drawing, id.clone());
        Ok(json!({ "image": id }))
    }
}
