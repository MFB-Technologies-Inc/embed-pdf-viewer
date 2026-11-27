//! `read/widgets.ts`: the widgets' reads: each page's widgets (from the
//! form's widget rows), the ones it shows for the form layer to paint, the
//! fill projection with the session's fill permission folded in, and the
//! widget hit test.

use std::rc::Rc;

use epdf_kernel::rt::Promise;
use serde_json::{Value, json};

use crate::model::{Box, FieldIndex, WidgetHit, page_widgets_of, widget_at};
use crate::read::fill_items::fill_items;
use crate::value::{get, number_key};

/// One page's `listWidgets` answer and what it was made from.
pub(crate) struct WidgetItems {
    index: Rc<FieldIndex>,
    rights: String,
    fillable: Value,
    rows: Rc<Vec<Rc<Value>>>,
    groups: Rc<Vec<Value>>,
    tint: Value,
    items: Rc<Vec<Value>>,
}

impl crate::controller::Core {
    /// `ensureLoaded(page)`: every page's widgets come with the form, one
    /// read for the document.
    pub fn ensure_loaded(&self, _page: &Value) -> Promise<()> {
        self.fields.settled()
    }

    /// The page's object number, for a ref or an index the document has.
    fn page_number_of(&self, page: &Value) -> Option<f64> {
        let info = self.get_page(page)?;
        get(&info, "ref").and_then(|r| get(r, "objectNumber")).and_then(Value::as_f64)
    }

    /// `getWidgetAt(page, point)`: the widget under a page point.
    pub fn get_widget_at(&self, page: &Value, x: f64, y: f64) -> Option<WidgetHit> {
        let number = self.page_number_of(page)?;
        let index = self.view();
        let widgets = page_widgets_of(&index, number);
        widget_at(&index, Some(&widgets), x, y)
    }

    /// `listWidgets(page)`: each widget on a page, as a fill control draws
    /// it. The same list while the form, the session's rights, the
    /// `fillable` setting, Tab's order, the groups and the tint stay the
    /// same (`memoByKey` in the TypeScript).
    pub fn list_widgets(&self, page: &Value) -> Rc<Vec<Value>> {
        let Some(number) = self.page_number_of(page) else { return Rc::new(Vec::new()) };
        let index = self.view();
        let rights = self.rights_key();
        let settings = self.settings_now();
        let fillable = settings.get("fillable").cloned().unwrap_or(Value::Null);
        let tint =
            settings.get("fields").and_then(|f| f.get("tint")).cloned().unwrap_or(Value::Null);
        let rows = self.tab_rows();
        let groups = self.list_groups();
        let key = number_key(number);
        if let Some(kept) = self.widget_items.borrow().get(&key)
            && Rc::ptr_eq(&kept.index, &index)
            && kept.rights == rights
            && kept.fillable == fillable
            && Rc::ptr_eq(&kept.rows, &rows)
            && Rc::ptr_eq(&kept.groups, &groups)
            && kept.tint == tint
        {
            return kept.items.clone();
        }
        let widgets = page_widgets_of(&index, number);
        let items = Rc::new(fill_items(
            &index,
            number,
            Some(&widgets),
            &|field| self.may_fill_by_hand(field),
            &|field| self.may_fill_or_sign(field),
            &|widget| self.group_position_of(widget),
            &|group| self.group_look_of(Some(group)),
        ));
        self.widget_items.borrow_mut().insert(
            key,
            WidgetItems { index, rights, fillable, rows, groups, tint, items: items.clone() },
        );
        items
    }

    /// `getWidgetAnchor(widget)`: where UI attaches to one box of a field,
    /// its page and where the box stands (`{ page, bounds }`), following the
    /// box as it is moved in design mode. `None` for a box that isn't there,
    /// or that shows nowhere.
    pub fn get_widget_anchor(&self, widget: &Value) -> Option<Value> {
        let index = self.view();
        let row =
            crate::model::widget_row_of(&index, crate::read::fields::widget_object_of(widget))?;
        // Lent to the annotation plugin, a widget is where its drag has it.
        if self.lending() {
            let annotation = self.annotation()?;
            let anchor =
                annotation.get_annotation_anchor(row.get("ref").unwrap_or(&Value::Null), None);
            return (!anchor.is_null()).then_some(anchor);
        }
        if !crate::model::shown_on_screen(row) {
            return None;
        }
        Some(json!({ "page": row["page"], "bounds": row["rect"] }))
    }

    /// `isWidgetFocused(widget)`: whether the cursor is in this box.
    pub fn is_widget_focused(&self, widget: &Value) -> bool {
        self.focused_widget().and_then(|f| f["widget"].as_f64())
            == Some(crate::read::fields::widget_object_of(widget))
    }

    /// `getPageBox(page)`: the page's content box, for page-bound placement.
    pub fn get_page_box(&self, page: &Value) -> Option<Box> {
        let info = self.get_page(page)?;
        let size = get(&info, "size")?;
        let n = |k: &str| get(size, k).and_then(Value::as_f64).unwrap_or(f64::NAN);
        Some(Box { x: 0.0, y: 0.0, width: n("width"), height: n("height") })
    }

    /// `getPageBox(page)` as JSON.
    pub fn get_page_box_json(&self, page: &Value) -> Value {
        self.get_page_box(page).map_or(Value::Null, |b| json!(b.to_json()))
    }
}
