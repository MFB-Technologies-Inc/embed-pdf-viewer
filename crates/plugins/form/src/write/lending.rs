//! `write/lending.ts`: design mode, the form's widgets lent to the
//! annotation plugin (`AnnotationLender`, its host contract). While the
//! session may design and the active tool doesn't fill, the annotation
//! plugin shows the widgets among its records and edits them with its
//! tools: select, move, resize, restyle, reorder, delete. Each edit comes
//! back here as the form's ops, in the same change as the rest of it. A
//! widget of no size is never lent: nobody can see it, so nobody should be
//! able to select it by hand.

use std::cell::RefCell;
use std::rc::{Rc, Weak};

use epdf_annotation_core::json::Object;
use epdf_annotation_core::object::Json;
use epdf_annotation_core::record::ref_of;
use epdf_annotation_core::types::{Authority, Effect, Model, Painter, RecordAccent};
use epdf_kernel::Error;
use epdf_kernel::rt::Promise;
use epdf_kernel::signal::AbortSignal;
use epdf_plugin_annotation::AnnotationLender;
use epdf_plugin_annotation::services::lending::LentRecords;
use indexmap::IndexMap;
use serde_json::{Value, json};

use crate::controller::Core;
use crate::model::{FieldIndex, field_for_widget, field_key_of, has_area};
use crate::value::{get, items, str_of};

/// `objectNumberOf(ref)`.
fn object_number_of(reference: &Value) -> f64 {
    if str_of(reference, "kind") == Some("objectNumber") {
        get(reference, "objectNumber").and_then(Value::as_f64).unwrap_or(f64::NAN)
    } else {
        -1.0
    }
}

/// What the lent lists are made from: the lending, the confirmed and the
/// shown form, the groups and the tint. The groups and the tint are there for
/// the accents (`accentOf`), which the annotation plugin reads again only
/// when the lists are new.
struct LentKey {
    lending: bool,
    confirmed: Rc<FieldIndex>,
    shown: Rc<FieldIndex>,
    groups: Rc<Vec<Value>>,
    tint: Value,
}

impl LentKey {
    fn same(&self, other: &LentKey) -> bool {
        self.lending == other.lending
            && Rc::ptr_eq(&self.confirmed, &other.confirmed)
            && Rc::ptr_eq(&self.shown, &other.shown)
            && Rc::ptr_eq(&self.groups, &other.groups)
            && self.tint == other.tint
    }
}

/// The form plugin's lender. The lent lists keep their identity while the
/// form, the lending, the groups and the tint don't change, as the
/// TypeScript's `memo` does.
pub struct FormLender {
    core: Weak<Core>,
    memo: RefCell<Option<(LentKey, Option<LentRecords>)>>,
}

impl FormLender {
    pub fn new(core: &Core) -> Rc<FormLender> {
        Rc::new(FormLender { core: core.weak(), memo: RefCell::new(None) })
    }

    fn core(&self) -> Option<Rc<Core>> {
        self.core.upgrade()
    }
}

/// The rows lent: every widget with an area.
fn rows(index: &FieldIndex) -> Rc<Vec<Rc<Value>>> {
    Rc::new(index.widgets().iter().filter(|row| has_area(row)).cloned().collect())
}

impl Core {
    /// `deleteOps(deleted)`: deleted widgets as the form's ops: a widget
    /// leaves its field, and a field whose every widget goes is deleted whole.
    fn delete_ops(&self, deleted: &[Value]) -> Vec<Value> {
        let index = self.view();
        let mut ops: Vec<Value> = Vec::new();
        let mut by_field: IndexMap<String, (Value, Vec<Value>)> = IndexMap::new();
        for row in deleted {
            let reference = get(row, "ref").cloned().unwrap_or(Value::Null);
            match field_for_widget(&index, object_number_of(&reference)) {
                None => ops.push(json!({ "type": "forms.deleteWidget", "widget": reference })),
                Some(field) => {
                    by_field
                        .entry(field_key_of(field))
                        .or_insert_with(|| (field.clone(), Vec::new()))
                        .1
                        .push(row.clone());
                }
            }
        }
        for (field, widgets) in by_field.into_values() {
            let going: Vec<f64> = widgets
                .iter()
                .map(|row| object_number_of(get(row, "ref").unwrap_or(&Value::Null)))
                .collect();
            let all = items(&field, "widgets").iter().all(|w| {
                get(w, "objectNumber").and_then(Value::as_f64).is_some_and(|n| going.contains(&n))
            });
            if all {
                ops.push(json!({ "type": "forms.delete", "field": get(&field, "ref").cloned().unwrap_or(Value::Null) }));
            } else {
                ops.extend(widgets.iter().map(|row| {
                    json!({ "type": "forms.deleteWidget", "widget": get(row, "ref").cloned().unwrap_or(Value::Null) })
                }));
            }
        }
        ops
    }
}

impl AnnotationLender for FormLender {
    fn owner(&self) -> String {
        "form".into()
    }

    fn records(&self) -> Option<LentRecords> {
        let core = self.core()?;
        let key = LentKey {
            lending: core.lending(),
            confirmed: core.truth(),
            shown: core.view(),
            groups: core.list_groups(),
            tint: core
                .settings_now()
                .get("fields")
                .and_then(|f| f.get("tint"))
                .cloned()
                .unwrap_or(Value::Null),
        };
        if let Some((seen, records)) = &*self.memo.borrow()
            && seen.same(&key)
        {
            return records.clone();
        }
        let (confirmed, shown) = (&key.confirmed, &key.shown);
        let records = (key.lending && confirmed.snapshot.is_some() && shown.snapshot.is_some())
            .then(|| LentRecords { confirmed: rows(confirmed), shown: rows(shown) });
        *self.memo.borrow_mut() = Some((key, records.clone()));
        records
    }

    fn ops_of(&self, effects: &[Effect], model: &Model, before: &Model) -> Vec<Value> {
        let Some(core) = self.core() else { return Vec::new() };
        let mut ops: Vec<Value> = Vec::new();
        let mut deleted: Vec<Value> = Vec::new();
        for effect in effects {
            match effect {
                Effect::Patch(patch) => {
                    let Some(reference) = ref_of(model.by_id.get(&patch.id).map(|r| &**r)) else {
                        continue;
                    };
                    let mut widget_patch = patch.patch.clone();
                    widget_patch.shift_remove("subtype");
                    ops.push(json!({ "type": "forms.updateWidget", "widget": reference, "patch": widget_patch }));
                }
                Effect::Delete(delete) => {
                    if let Some(record) = before.by_id.get(&delete.id) {
                        deleted.push(Value::Object((*record.annotation).clone()));
                    }
                }
                _ => {}
            }
        }
        ops.extend(core.delete_ops(&deleted));
        ops
    }

    fn undo_of(&self, ops: &[Value]) -> Vec<Value> {
        match self.core() {
            Some(core) => crate::sync::predict::undo_look_of(&core.view(), ops),
            None => Vec::new(),
        }
    }

    fn reorder_ops(&self, page: &Value, refs: &[Value], position: &Value) -> Vec<Value> {
        vec![
            json!({ "type": "forms.reorderWidgets", "page": page, "widgets": refs, "position": position }),
        ]
    }

    fn authority(&self, _record: &Object) -> Authority {
        let may = self.core().is_some_and(|core| core.allows("doc.forms.modify"));
        Authority { update: may, delete: may, order: Default::default() }
    }

    fn may_copy(&self, _record: &Object) -> bool {
        self.core().is_some_and(|core| core.allows("doc.forms.modify"))
    }

    /// `copyOps(refs, placement)`: by hand, a copy is the question, not
    /// what someone filled in.
    fn copy_ops(
        &self,
        refs: &[Value],
        page: Option<Value>,
        offset: Option<Value>,
    ) -> Promise<(Vec<Value>, Vec<Value>)> {
        let Some(core) = self.core() else {
            return Promise::rejected(Error::new("[form] closed"));
        };
        core.copy_ops(refs.to_vec(), page, offset, false).map(|(ops, copies, _)| Ok((ops, copies)))
    }

    fn picture_epoch(&self, page: &Value) -> String {
        self.core().map(|core| core.get_appearance_epoch(page).to_string()).unwrap_or_default()
    }

    fn picture_version(&self, reference: &Value) -> f64 {
        self.core().map_or(0.0, |core| core.picture_version_of_ref(reference))
    }

    fn pending(&self, reference: &Value) -> bool {
        self.core().is_some_and(|core| core.widget_is_pending(reference))
    }

    /// `accentOf(record)`: the accent of the group of the record's field,
    /// `fields.tint` strong; `None` for a widget in no group, and at tint 0.
    fn accent_of(&self, record: &Object) -> Option<RecordAccent> {
        let core = self.core()?;
        let index = core.view();
        let reference = record.get("ref").unwrap_or(&Value::Null);
        let group_id = field_for_widget(&index, object_number_of(reference))
            .and_then(|field| get(field, "groupId"))
            .and_then(Value::as_str)
            .map(str::to_string);
        let look = core.group_look_of(group_id.as_deref())?;
        RecordAccent::from_json(Some(&look.accent)).ok()
    }

    fn painter(&self, record: &Rc<Object>) -> Painter {
        let Some(core) = self.core() else { return Painter(None) };
        if record.get("subtype").and_then(Value::as_str) != Some("widget") {
            return Painter(None);
        }
        core.painter_of(&Value::Object((**record).clone()))
    }

    fn render_appearances(
        &self,
        page: &Value,
        scale: f64,
        signal: Option<AbortSignal>,
    ) -> Promise<Vec<Value>> {
        let Some(core) = self.core() else { return Promise::resolved(Vec::new()) };
        let pictures = core.render_appearances(page, scale, signal);
        pictures.convert(|list| {
            // A picture that does not serialise rejects the read, rather
            // than lending a `null` picture.
            list.iter()
                .map(|p| {
                    serde_json::to_value(p).map_err(|e| {
                        Error::new(format!("[form] a widget picture did not serialise: {e}"))
                    })
                })
                .collect()
        })
    }
}
