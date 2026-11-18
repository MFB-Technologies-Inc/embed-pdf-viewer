//! `services/pictures.ts`: the engine's pictures of each page's widgets,
//! through the render plugin's one door. The form layer draws them, and the
//! annotation plugin draws the widgets it borrows from the same ones
//! (design mode). A page's pictures depend on its widgets' picture versions
//! (the confirmed form's) and nothing else.

use std::rc::Rc;

use epdf_kernel::rt::Promise;
use epdf_kernel::signal::AbortSignal;
use epdf_plugin_render::{AppearanceFamily, AppearanceItem, AppearancePicture, AppearancesOptions};
use serde_json::Value;

use crate::model::{FieldIndex, picture_version_of};
use crate::value::{get, number_key, str_of};

/// One page's `itemsOn` and `epochOn` answers and the confirmed form they
/// were read from.
pub(crate) struct ItemsOn {
    truth: Rc<FieldIndex>,
    items: Rc<Vec<AppearanceItem>>,
    epoch: Rc<str>,
}

/// The page's widgets in `index`, each at its picture version.
fn items_on(index: &FieldIndex, page: f64) -> Vec<AppearanceItem> {
    index
        .widgets()
        .iter()
        .filter_map(|row| {
            let reference = get(row, "ref")?;
            let on_page =
                get(row, "page").and_then(|p| get(p, "objectNumber")).and_then(Value::as_f64)
                    == Some(page);
            if !on_page || str_of(reference, "kind") != Some("objectNumber") {
                return None;
            }
            let n = get(reference, "objectNumber").and_then(Value::as_f64).unwrap_or(f64::NAN);
            Some(AppearanceItem {
                reference: reference.clone(),
                version: picture_version_of(index, n),
            })
        })
        .collect()
}

/// `epochOn`'s text for `items`: `5@2|6@7`.
fn epoch_of(items: &[AppearanceItem]) -> Rc<str> {
    let mut parts: Vec<String> = items
        .iter()
        .map(|item| {
            let number = if str_of(&item.reference, "kind") == Some("objectNumber") {
                crate::value::string_of(get(&item.reference, "objectNumber"))
            } else {
                String::new()
            };
            format!("{number}@{}", epdf_geometry::js::number_to_string(item.version))
        })
        .collect();
    // `Array#sort` with no comparator: by UTF-16 code units.
    parts.sort_by(|a, b| a.encode_utf16().cmp(b.encode_utf16()));
    parts.join("|").into()
}

impl crate::controller::Core {
    /// `itemsOn(pageObjectNumber)` and `epochOn(pageObjectNumber)`: the
    /// page's widgets as the engine confirmed them, each at its picture
    /// version, and what their pictures depend on. The same answers while
    /// the confirmed form stays the same (`memoByKey` in the TypeScript).
    fn pictures_on(&self, page: f64) -> (Rc<Vec<AppearanceItem>>, Rc<str>) {
        let truth = self.truth();
        let key = number_key(page);
        if let Some(kept) = self.picture_items.borrow().get(&key)
            && Rc::ptr_eq(&kept.truth, &truth)
        {
            return (kept.items.clone(), kept.epoch.clone());
        }
        let items = Rc::new(items_on(&truth, page));
        let epoch = epoch_of(&items);
        self.picture_items
            .borrow_mut()
            .insert(key, ItemsOn { truth, items: items.clone(), epoch: epoch.clone() });
        (items, epoch)
    }

    /// `getAppearanceEpoch(page)`: what the page's pictures depend on,
    /// `5@2|6@7`.
    pub fn get_appearance_epoch(&self, page: &Value) -> Rc<str> {
        let page = get(page, "objectNumber").and_then(Value::as_f64).unwrap_or(f64::NAN);
        self.pictures_on(page).1
    }

    /// `renderAppearances(page, scale, signal)`: every picture of the page's
    /// widgets, at rest, in every state; none without the render plugin.
    pub fn render_appearances(
        &self,
        page: &Value,
        scale: f64,
        signal: Option<AbortSignal>,
    ) -> Promise<Rc<Vec<AppearancePicture>>> {
        let Some(render) = self.render() else { return Promise::resolved(Rc::new(Vec::new())) };
        let number = get(page, "objectNumber").and_then(Value::as_f64).unwrap_or(f64::NAN);
        let items = (*self.pictures_on(number).0).clone();
        let Some(page) = epdf_kernel::geometry::PageRef::from_json(page) else {
            return Promise::resolved(Rc::new(Vec::new()));
        };
        render.render_appearances(
            page,
            AppearancesOptions { family: AppearanceFamily::Widgets, scale, items, signal },
        )
    }

    /// `pictures.versionOf(widget)`: the widget's confirmed picture version.
    pub fn picture_version_of_ref(&self, widget: &Value) -> f64 {
        if str_of(widget, "kind") != Some("objectNumber") {
            return 0.0;
        }
        let n = get(widget, "objectNumber").and_then(Value::as_f64).unwrap_or(f64::NAN);
        picture_version_of(&self.truth(), n)
    }
}
