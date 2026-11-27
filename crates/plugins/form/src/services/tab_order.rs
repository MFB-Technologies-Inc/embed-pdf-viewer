//! `services/tab-order.ts`: the order Tab visits the widgets in: every
//! widget that shows, page by page in the document's order, each page by its
//! own tab order (`tabOrderOf()`). The cursor goes this way
//! (`write/focus.rs`), and so do the arrow keys round a radio group's
//! buttons, so a button's place among its group's is its place in this order.

use std::collections::HashMap;
use std::rc::Rc;

use serde_json::{Value, json};

use crate::model::{FieldIndex, field_key_of, shared_field_for_widget, shown_on_screen};
use crate::read::fields::widget_object_of;
use crate::value::{number_key, str_of};

/// The memo of `rows()` and `groupPositionOf()`: what they were made from.
pub(crate) struct TabOrderMemo {
    index: Rc<FieldIndex>,
    pages: Rc<Value>,
    rows: Rc<Vec<Rc<Value>>>,
    positions: Rc<HashMap<String, Value>>,
}

impl crate::controller::Core {
    fn tab_order_memo(&self) -> (Rc<Vec<Rc<Value>>>, Rc<HashMap<String, Value>>) {
        let index = self.view();
        let pages = self.pages();
        if let Some(memo) = &*self.tab_order.borrow()
            && Rc::ptr_eq(&memo.index, &index)
            && Rc::ptr_eq(&memo.pages, &pages)
        {
            return (memo.rows.clone(), memo.positions.clone());
        }
        let shown: Vec<Rc<Value>> =
            index.widgets().iter().filter(|row| shown_on_screen(row)).cloned().collect();
        let rows = Rc::new(crate::engine::tab_order_of(
            &shown,
            pages.as_array().map(Vec::as_slice).unwrap_or(&[]),
        ));
        // Each radio group's buttons that show, in Tab's order.
        let mut buttons: Vec<(String, Vec<f64>)> = Vec::new();
        for row in rows.iter() {
            let widget = widget_object_of(&row["ref"]);
            let Some(field) = shared_field_for_widget(&index, widget) else { continue };
            if str_of(field, "kind") != Some("radio") {
                continue;
            }
            let key = field_key_of(field);
            match buttons.iter_mut().find(|(k, _)| *k == key) {
                Some((_, group)) => group.push(widget),
                None => buttons.push((key, vec![widget])),
            }
        }
        let mut positions = HashMap::new();
        for (_, group) in &buttons {
            for (at, widget) in group.iter().enumerate() {
                positions.insert(
                    number_key(*widget),
                    json!({ "position": at + 1, "size": group.len() }),
                );
            }
        }
        let positions = Rc::new(positions);
        *self.tab_order.borrow_mut() =
            Some(TabOrderMemo { index, pages, rows: rows.clone(), positions: positions.clone() });
        (rows, positions)
    }

    /// `tabOrder.rows()`: every widget that shows, in Tab's order. The same
    /// list while the form and the pages stay the same.
    pub(crate) fn tab_rows(&self) -> Rc<Vec<Rc<Value>>> {
        self.tab_order_memo().0
    }

    /// `tabOrder.groupPositionOf(widget)`: where a radio button comes among
    /// its group's buttons that show, in Tab's order (`{ position, size }`).
    /// `None` for any other widget, and for one that doesn't show.
    pub(crate) fn group_position_of(&self, widget: f64) -> Option<Value> {
        self.tab_order_memo().1.get(&number_key(widget)).cloned()
    }
}
