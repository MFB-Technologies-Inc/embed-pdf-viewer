//! `write/focus.ts`: moving the cursor between fields: `focus()`,
//! `focusNext()` (what Tab does), the arrow keys in a radio group, and the
//! field the cursor is in. This area decides where the cursor goes: which
//! field, which of its boxes, in Tab's order (`services/tab_order.rs`); the
//! cursor service (`services/cursor.rs`) brings the box into view and hands
//! it to its control.
//!
//! A stop is a box of a field the user may fill in. The cursor passes over
//! one whose control takes no cursor (`go_to` declines it): a signature
//! field with nothing to sign it. Stops are tried in the order Tab comes to
//! them, so a move asks only the ones it reaches.
//!
//! A radio group is one stop on each page it shows on, where its first
//! button comes in Tab's order. The button that takes the cursor may come
//! later in that order, with other fields before it. From any of its buttons
//! the cursor goes on from the group's stop, so Tab takes what comes after
//! the group and Shift+Tab what comes before it, each field once.
use crate::{
    controller::Core,
    model::{
        FieldIndex, field_by_ref, field_key_of, shared_field_by_key, shared_field_by_ref,
        shared_field_for_widget, widget_row_of,
    },
    read::fields::{is_empty, widget_object_of},
    services::cursor::CursorMove,
    value::str_of,
};
use serde_json::{Value, json};
use std::collections::{HashMap, HashSet};
use std::rc::Rc;

struct Stop {
    row: Rc<Value>,
    field: Rc<Value>,
    at: usize,
}

struct FocusOrder {
    rows: Rc<Vec<Rc<Value>>>,
    position: HashMap<String, usize>,
    stops: Vec<Stop>,
}

pub(crate) struct FocusOrderCache {
    index: Rc<FieldIndex>,
    rows: Rc<Vec<Rc<Value>>>,
    rights: String,
    fillable: Value,
    order: Rc<FocusOrder>,
}

/// How `focusNext()` moves, each of its options given.
#[derive(Clone, Copy)]
struct Step {
    backward: bool,
    empty: bool,
    wrap: bool,
}

fn key(widget: f64) -> String {
    crate::value::number_key(widget)
}

impl Core {
    /// Every box that shows, in Tab's order, and the ones Tab stops at.
    fn focus_order(&self) -> Rc<FocusOrder> {
        let index = self.view();
        let rows = self.tab_rows();
        let rights = self.rights_key();
        let fillable = self.settings_now().get("fillable").cloned().unwrap_or(Value::Null);
        if let Some(cache) = &*self.focus_order_cache.borrow()
            && Rc::ptr_eq(&cache.index, &index)
            && Rc::ptr_eq(&cache.rows, &rows)
            && cache.rights == rights
            && cache.fillable == fillable
        {
            return cache.order.clone();
        }
        let mut position = HashMap::new();
        let mut groups = HashSet::new();
        let mut stops = Vec::new();
        for (at, row) in rows.iter().enumerate() {
            let widget = widget_object_of(&row["ref"]);
            position.insert(key(widget), at);
            let Some(field) = shared_field_for_widget(&index, widget) else { continue };
            if !self.takes_input(field) {
                continue;
            }
            // A radio group is one stop per page: its first button there.
            if str_of(field, "kind") == Some("radio")
                && !groups.insert(format!(
                    "{}@{}",
                    field_key_of(field),
                    row["page"]["objectNumber"]
                ))
            {
                continue;
            }
            stops.push(Stop { row: row.clone(), field: field.clone(), at });
        }
        let order = Rc::new(FocusOrder { rows: rows.clone(), position, stops });
        *self.focus_order_cache.borrow_mut() =
            Some(FocusOrderCache { index, rows, rights, fillable, order: order.clone() });
        order
    }

    /// A radio group's buttons that show, in Tab's order: on one page, or
    /// on every page.
    fn radio_buttons(
        &self,
        rows: &[Rc<Value>],
        field: &Rc<Value>,
        page: Option<&Value>,
    ) -> Vec<Rc<Value>> {
        let index = self.view();
        rows.iter()
            .filter(|row| {
                page.is_none_or(|p| p == &row["page"]["objectNumber"])
                    && shared_field_for_widget(&index, widget_object_of(&row["ref"]))
                        .is_some_and(|f| Rc::ptr_eq(f, field))
            })
            .cloned()
            .collect()
    }

    /// `boxAt(stop, backward)`: the box a stop puts the cursor in. A radio
    /// group's is its checked button, else its first one, or its last one
    /// going backward.
    fn box_at_stop(&self, rows: &[Rc<Value>], stop: &Stop, backward: bool) -> Rc<Value> {
        if str_of(&stop.field, "kind") != Some("radio") {
            return stop.row.clone();
        }
        let buttons =
            self.radio_buttons(rows, &stop.field, Some(&stop.row["page"]["objectNumber"]));
        let checked = buttons.iter().find(|r| {
            radio_button(&stop.field, widget_object_of(&r["ref"]))
                .is_some_and(|b| b["checked"] == true)
        });
        checked
            .or_else(|| if backward { buttons.last() } else { buttons.first() })
            .unwrap_or(&stop.row)
            .clone()
    }

    /// `focus(ref, options)`: bring a fillable field into view and give its
    /// control the cursor: the box `widget` names, else the field's first
    /// stop whose control takes the cursor.
    pub fn focus(&self, reference: &Value, options: Option<&Value>) -> bool {
        let index = self.view();
        let Some(field) = shared_field_by_ref(&index, reference).filter(|f| self.takes_input(f))
        else {
            return false;
        };
        let select = options.and_then(|o| o["select"].as_bool()).unwrap_or(false);
        let order = self.focus_order();
        if let Some(widget) = options.and_then(|o| o.get("widget")).filter(|w| !w.is_null()) {
            let n = widget_object_of(widget);
            if !shared_field_for_widget(&index, n).is_some_and(|f| Rc::ptr_eq(f, field)) {
                return false;
            }
            return order
                .rows
                .iter()
                .find(|r| widget_object_of(&r["ref"]) == n)
                .is_some_and(|row| self.go_to(row, select, None) == CursorMove::Taken);
        }
        // The field's first stop that takes the cursor.
        for stop in order.stops.iter().filter(|s| Rc::ptr_eq(&s.field, field)) {
            let row = self.box_at_stop(&order.rows, stop, false);
            let moved = self.go_to(&row, select, None);
            if moved != CursorMove::Declined {
                return moved == CursorMove::Taken;
            }
        }
        false
    }

    /// `step(from, how, passed)`: move the cursor to the next stop after a
    /// box (`from`; `None`: from the start), as Tab does: the first one on
    /// the way that takes the cursor. A radio button's place is its group's
    /// stop. `passed` are the stops this move already asked for and that
    /// came in holding nothing to focus.
    fn step(&self, from: Option<f64>, how: Step, passed: Rc<HashSet<String>>) -> bool {
        let Step { backward, empty, wrap } = how;
        let index = self.view();
        let order = self.focus_order();
        let field = from.and_then(|n| shared_field_for_widget(&index, n)).cloned();
        let page =
            from.and_then(|n| widget_row_of(&index, n)).map(|r| r["page"]["objectNumber"].clone());
        // A radio group is one stop: from one of its buttons, the group is
        // where the cursor is.
        let group = field.as_ref().filter(|f| str_of(f, "kind") == Some("radio")).and_then(|f| {
            order.stops.iter().position(|s| {
                Rc::ptr_eq(&s.field, f) && Some(&s.row["page"]["objectNumber"]) == page.as_ref()
            })
        });
        // The cursor goes on from the box's place in Tab's order, and from a
        // radio button from its group's stop: the button may come later,
        // with other fields between.
        let own = from.and_then(|n| order.position.get(&key(n)).copied());
        let at = own.map(|own| group.map_or(own, |g| order.stops[g].at));
        let candidates: Vec<usize> = (0..order.stops.len())
            .filter(|&i| {
                let stop = &order.stops[i];
                (!empty || is_empty(&stop.field))
                    && Some(i) != group
                    && !passed.contains(&key(widget_object_of(&stop.row["ref"])))
            })
            .collect();
        let is_ahead = |i: &usize| {
            let stop_at = order.stops[*i].at;
            at.is_none_or(|at| if backward { stop_at < at } else { stop_at > at })
        };
        let ahead: Vec<usize> = candidates.iter().copied().filter(is_ahead).collect();
        let round: Vec<usize> = if wrap {
            candidates.iter().copied().filter(|i| !is_ahead(i)).collect()
        } else {
            Vec::new()
        };
        // The way Tab comes to them: what lies ahead, then, with `wrap`,
        // round from the other end.
        let way: Vec<usize> = if backward {
            ahead.into_iter().rev().chain(round.into_iter().rev()).collect()
        } else {
            ahead.into_iter().chain(round).collect()
        };
        for i in way {
            let stop = &order.stops[i];
            let here = widget_object_of(&stop.row["ref"]);
            // A box that isn't on screen may come in holding nothing to
            // focus. The cursor then goes on from this stop the same way. A
            // stop is asked for once in a move, so a move can't go round a
            // form of such boxes.
            let weak = self.weak();
            let passed_then = passed.clone();
            let onward: Rc<dyn Fn()> = Rc::new(move || {
                if let Some(core) = weak.upgrade() {
                    let mut passed = (*passed_then).clone();
                    passed.insert(key(here));
                    core.step(Some(here), how, Rc::new(passed));
                }
            });
            // Tab selects a text box's text, so typing replaces it.
            let row = self.box_at_stop(&order.rows, stop, backward);
            let moved = self.go_to(&row, true, Some(onward));
            if moved != CursorMove::Declined {
                return moved == CursorMove::Taken;
            }
        }
        false
    }

    /// `focusNext(options)`: move as Tab does, returning false when the
    /// cursor leaves the form. It goes on from the field `from` names (its
    /// first box), the box the cursor is in, or the one it was in last.
    pub fn focus_next(&self, options: Option<&Value>) -> bool {
        let options = options.cloned().unwrap_or(json!({}));
        let order = self.focus_order();
        let from = if let Some(reference) = options.get("from") {
            let index = self.view();
            field_by_ref(&index, reference).and_then(|f| f["widgets"].as_array()).and_then(
                |widgets| {
                    widgets
                        .iter()
                        .filter_map(|w| w["objectNumber"].as_f64())
                        .find(|n| order.position.contains_key(&key(*n)))
                },
            )
        } else {
            self.focused_widget().and_then(|f| f["widget"].as_f64()).or(self.last_focused_widget())
        };
        self.step(
            from,
            Step {
                backward: options["backward"].as_bool().unwrap_or(false),
                empty: options["empty"].as_bool().unwrap_or(false),
                wrap: options["wrap"].as_bool().unwrap_or(false),
            },
            Rc::new(HashSet::new()),
        )
    }

    /// `getFocusedField()`: the field the cursor is in, or `None`.
    pub fn get_focused_field(&self) -> Option<Rc<Value>> {
        let focused = self.focused_widget()?;
        let index = self.view();
        shared_field_by_key(&index, focused["field"].as_str()?).cloned()
    }

    /// `moveInGroup(ref, backward)`: the arrow keys in a radio group, round
    /// its buttons in Tab's order. As a click on it: the value first, then
    /// its action (Acrobat's order).
    pub fn move_in_group(&self, reference: &Value, backward: bool) -> bool {
        let index = self.view();
        let widget = widget_object_of(reference);
        let Some(field) = shared_field_for_widget(&index, widget)
            .filter(|f| str_of(f, "kind") == Some("radio") && self.takes_input(f))
        else {
            return false;
        };
        let order = self.focus_order();
        let buttons = self.radio_buttons(&order.rows, field, None);
        let Some(here) = buttons.iter().position(|r| widget_object_of(&r["ref"]) == widget) else {
            return false;
        };
        if buttons.len() < 2 {
            return false;
        }
        let next = &buttons[(here + if backward { buttons.len() - 1 } else { 1 }) % buttons.len()];
        if self.go_to(next, false, None) != CursorMove::Taken {
            return false;
        }
        if let Some(button) = radio_button(field, widget_object_of(&next["ref"])) {
            let write =
                self.set_value(field["ref"].clone(), json!({"value":button["exportValue"]}), None);
            let weak = self.weak();
            let reference = next["ref"].clone();
            write.on_settle(move |_| {
                if let Some(core) = weak.upgrade() {
                    core.activate_widget(reference.clone(), None).ignore();
                }
            });
        }
        true
    }
}

fn radio_button(field: &Value, widget: f64) -> Option<&Value> {
    field["widgets"].as_array()?.iter().find(|b| b["objectNumber"].as_f64() == Some(widget))
}
