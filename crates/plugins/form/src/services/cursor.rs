//! The cursor and the controls currently able to receive it.
use crate::{
    controller::Core,
    model::{field_by_ref, field_key_of, shared_field_by_key, widget_row_of},
    read::fields::widget_object_of,
    value::number_key,
};
use epdf_kernel::{Error, Unsubscribe};
use epdf_plugin_stage::{PageTarget, RevealOptions, STAGE};
use serde_json::{Value, json};
use std::{
    cell::{Cell, RefCell},
    collections::HashMap,
    rc::Rc,
};

/// `FormFocusTarget`: a box's control, lent while it's on screen.
///
/// `takes_cursor` is asked each time the cursor is about to move: a box that
/// holds nothing to focus says no (a signature field with nothing to sign
/// it, a control of an app's own with no element Tab stops at), and
/// `focus()`, `focusNext()` and Tab pass over its field. A target without it
/// takes the cursor. `focus` takes the cursor (`select` selects a text box's
/// text) and answers `false` when nothing took it after all: the cursor
/// then passes over the box, as over one that says it takes none.
#[derive(Clone)]
pub struct FocusTarget {
    pub takes_cursor: Option<Rc<dyn Fn() -> bool>>,
    pub focus: Rc<dyn Fn(bool) -> bool>,
}

impl FocusTarget {
    /// A target that always takes the cursor, `focus` its one call.
    pub fn new(focus: impl Fn(bool) + 'static) -> FocusTarget {
        FocusTarget {
            takes_cursor: None,
            focus: Rc::new(move |select| {
                focus(select);
                true
            }),
        }
    }

    /// Whether two targets are the one lent target.
    fn same(&self, other: &FocusTarget) -> bool {
        Rc::ptr_eq(&self.focus, &other.focus)
    }
}

/// `takes(target)`: every control takes the cursor that doesn't say it
/// takes none.
fn takes(target: &FocusTarget) -> bool {
    target.takes_cursor.as_ref().is_none_or(|takes| takes())
}

/// `enter(target, select)`: put the cursor in a control; `false` when it
/// takes none (it says so, or nothing in it took it).
fn enter(target: &FocusTarget, select: bool) -> bool {
    takes(target) && (target.focus)(select)
}

/// `CursorMove`: what became of a move to a box.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum CursorMove {
    /// The cursor is in the box, or goes in when its control arrives.
    Taken,
    /// Its control takes no cursor: the move passes over the box.
    Declined,
    /// It isn't on screen, and no Stage can bring it in.
    Unreachable,
}

/// A box asked for that isn't on screen yet.
struct Pending {
    widget: String,
    select: bool,
    /// Where the cursor goes if the box comes in holding nothing to focus.
    onward: Option<Rc<dyn Fn()>>,
    /// Ends the wait for its page.
    expire: RefCell<Option<Unsubscribe>>,
    /// Ends the turn a box that came in with nothing to focus is given,
    /// while it has one.
    settle: RefCell<Option<Unsubscribe>>,
}

#[derive(Default)]
pub(crate) struct Cursor {
    targets: RefCell<HashMap<String, FocusTarget>>,
    pending: RefCell<Option<Rc<Pending>>>,
    leaving: RefCell<Option<Unsubscribe>>,
    last: Cell<Option<f64>>,
}
impl Core {
    pub(crate) fn wire_cursor(&self) -> Result<(), Error> {
        let weak = self.weak();
        self.cx.on_state_change(move |next, previous| {
            if next.get("focused") == previous.get("focused") {
                return Ok(());
            }
            if let Some(core) = weak.upgrade() {
                let index = core.view();
                let focused = next.get("focused");
                let field = focused
                    .and_then(|f| f.get("field"))
                    .and_then(Value::as_str)
                    .and_then(|key| shared_field_by_key(&index, key))
                    .map(|f| (**f).clone());
                let row = focused
                    .and_then(|f| f.get("widget"))
                    .and_then(Value::as_f64)
                    .and_then(|n| widget_row_of(&index, n));
                core.events
                    .focus_changed
                    .emit(&json!({"field":field,"widget":row.and_then(|r|r.get("ref"))}));
            }
            Ok(())
        });
        let weak = self.weak();
        self.cx.cleanup_fn(move || {
            if let Some(core) = weak.upgrade() {
                core.clear_pending_focus();
                core.cursor.targets.borrow_mut().clear();
                let cancel = core.cursor.leaving.borrow_mut().take();
                if let Some(cancel) = cancel {
                    cancel.call();
                }
            }
        });
        Ok(())
    }
    pub(crate) fn clear_pending_focus(&self) {
        let pending = self.cursor.pending.borrow_mut().take();
        if let Some(pending) = pending {
            let expire = pending.expire.borrow_mut().take();
            if let Some(expire) = expire {
                expire.call();
            }
            let settle = pending.settle.borrow_mut().take();
            if let Some(settle) = settle {
                settle.call();
            }
        }
    }

    /// Whether `waiting` is the box asked for now.
    fn is_pending(&self, waiting: &Rc<Pending>) -> bool {
        self.cursor.pending.borrow().as_ref().is_some_and(|p| Rc::ptr_eq(p, waiting))
    }

    /// `settleLater(waiting)`: a box asked for came in holding nothing to
    /// focus. It is given the turn to draw what does (a control of an app's
    /// own may render after its box), and if nothing in it takes the cursor
    /// then, the cursor goes on.
    fn settle_later(&self, waiting: &Rc<Pending>) {
        if waiting.settle.borrow().is_some() {
            return;
        }
        let weak = self.weak();
        let held = Rc::downgrade(waiting);
        let cancel = self.cx.clock().after(0.0, move || {
            let Some(held) = held.upgrade() else { return };
            held.settle.borrow_mut().take();
            let Some(core) = weak.upgrade() else { return };
            if !core.is_pending(&held) {
                return;
            }
            let now = core.cursor.targets.borrow().get(&held.widget).cloned();
            // Gone again within the turn: it's still awaited.
            let Some(now) = now else { return };
            core.clear_pending_focus();
            if !enter(&now, held.select)
                && let Some(onward) = &held.onward
            {
                onward();
            }
        });
        *waiting.settle.borrow_mut() = Some(cancel);
    }

    /// `arrive(waiting, target)`: the box asked for has come in: its
    /// control takes the cursor, or gets its turn to.
    fn arrive(&self, waiting: &Rc<Pending>, target: &FocusTarget) {
        if !enter(target, waiting.select) {
            self.settle_later(waiting);
        } else if self.is_pending(waiting) {
            self.clear_pending_focus();
        }
    }
    pub(crate) fn focused_widget(&self) -> Option<Value> {
        self.cx.state().get("focused").filter(|f| !f.is_null()).cloned()
    }
    pub(crate) fn last_focused_widget(&self) -> Option<f64> {
        self.cursor.last.get()
    }
    fn set_focused_widget(&self, next: Option<Value>) {
        self.cx.update_state(|state| crate::model::set_focused_widget(state, next));
    }
    pub(crate) fn note_widget_focus(&self, event: &str, field_ref: &Value, widget_ref: &Value) {
        let widget = widget_object_of(widget_ref);
        if event == "focus" {
            let leaving = self.cursor.leaving.borrow_mut().take();
            if let Some(leaving) = leaving {
                leaving.call();
            }
            let pending_elsewhere = self
                .cursor
                .pending
                .borrow()
                .as_ref()
                .is_some_and(|p| p.widget != number_key(widget));
            if pending_elsewhere {
                self.clear_pending_focus();
            }
            let index = self.view();
            let field = field_by_ref(&index, field_ref);
            self.cursor.last.set(Some(widget));
            self.set_focused_widget(
                field.map(|f| json!({"field":field_key_of(f),"widget":widget})),
            );
        } else if self.focused_widget().and_then(|f| f["widget"].as_f64()) == Some(widget) {
            let leaving = self.cursor.leaving.borrow_mut().take();
            if let Some(leaving) = leaving {
                leaving.call();
            }
            let weak = self.weak();
            let cancel = self.cx.clock().after(0.0, move || {
                if let Some(core) = weak.upgrade() {
                    core.cursor.leaving.borrow_mut().take();
                    core.set_focused_widget(None);
                }
            });
            *self.cursor.leaving.borrow_mut() = Some(cancel);
        }
    }
    /// `cursor.goTo(row, select, onward)`: bring a box into view and put
    /// the cursor in it, now if its control is there, else when it comes. A
    /// box on screen whose control says it takes no cursor is declined
    /// before the view moves. `onward` runs if the box comes in holding
    /// nothing to focus: where the cursor goes instead.
    pub(crate) fn go_to(
        &self,
        row: &Value,
        select: bool,
        onward: Option<Rc<dyn Fn()>>,
    ) -> CursorMove {
        let widget = number_key(widget_object_of(&row["ref"]));
        let shown = self.cursor.targets.borrow().get(&widget).cloned();
        // On screen, and it says it holds nothing to focus: the view stays
        // where it is.
        if shown.as_ref().is_some_and(|shown| !takes(shown)) {
            return CursorMove::Declined;
        }
        let stage = self.cx.try_get(&STAGE).ok().flatten();
        if let Some(stage) = &stage
            && let (Some(page), Some(rect)) =
                (row["page"]["objectNumber"].as_u64(), row.get("rect"))
        {
            let rect = epdf_kernel::engine::decode(rect.clone()).ok();
            stage.reveal(
                PageTarget::Ref(epdf_kernel::geometry::PageRef::new(page as u32)),
                Some(RevealOptions { rect, ..Default::default() }),
            );
        }
        self.clear_pending_focus();
        // Read again: a host may draw the box, and lend its control, as the
        // view moves.
        let target = self.cursor.targets.borrow().get(&widget).cloned();
        if let Some(target) = &target
            && enter(target, select)
        {
            return CursorMove::Taken;
        }
        // It was on screen, said it takes the cursor, and nothing in it did.
        if let (Some(target), Some(shown)) = (&target, &shown)
            && target.same(shown)
        {
            return CursorMove::Declined;
        }
        if stage.is_none() {
            return CursorMove::Unreachable;
        }
        let waiting = Rc::new(Pending {
            widget,
            select,
            onward,
            expire: RefCell::new(None),
            settle: RefCell::new(None),
        });
        let weak = self.weak();
        let held = Rc::downgrade(&waiting);
        let expire = self.cx.clock().after(2000.0, move || {
            if let (Some(core), Some(held)) = (weak.upgrade(), held.upgrade())
                && core.is_pending(&held)
            {
                core.clear_pending_focus();
            }
        });
        *waiting.expire.borrow_mut() = Some(expire);
        *self.cursor.pending.borrow_mut() = Some(waiting.clone());
        // Lent as the view moved, with nothing to focus yet: it gets its
        // turn, as one that comes later.
        if target.is_some() {
            self.settle_later(&waiting);
        }
        CursorMove::Taken
    }
    /// `attachFocusTarget(widget, target)`: lend a control while its widget
    /// is mounted; a box asked for before its page came in takes the cursor
    /// now. Replacement releases are identity-safe.
    pub fn attach_focus_target(&self, widget: &Value, target: FocusTarget) -> Unsubscribe {
        let widget = number_key(widget_object_of(widget));
        self.cursor.targets.borrow_mut().insert(widget.clone(), target.clone());
        let pending = self.cursor.pending.borrow().as_ref().filter(|p| p.widget == widget).cloned();
        if let Some(pending) = pending {
            self.arrive(&pending, &target);
        }
        let weak = self.weak();
        Unsubscribe::new(move || {
            if let Some(core) = weak.upgrade() {
                let current =
                    core.cursor.targets.borrow().get(&widget).is_some_and(|t| t.same(&target));
                if current {
                    core.cursor.targets.borrow_mut().remove(&widget);
                }
            }
        })
    }
}
