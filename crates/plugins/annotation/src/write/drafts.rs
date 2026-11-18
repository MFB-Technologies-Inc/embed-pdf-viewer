//! `write/drafts.ts`: creation drafts. The polygon or polyline a click per
//! point builds, its finish and cancel doors, and the captured-draft seam the
//! measurement plugin calibrates from. Also the pen drawing that waits for its
//! next stroke: the wait, and everything that ends it.

use std::cell::RefCell;
use std::rc::Rc;

use epdf_annotation_core::object::Json;
use epdf_annotation_core::types::{CapturedEffect, Draft, Model, Shape, UpdateResult};
use epdf_annotation_core::view::creation_draft_anchor;
use epdf_kernel::clock::Cancel;
use epdf_kernel::rt::Promise;
use epdf_kernel::signal::AbortSignal;
use serde_json::{Value, json};

use crate::controller::Core;
use crate::services::store::Adjust;
use crate::write::outcomes::created_ref_of;

/// The wait that is running: the draft as the pen left it, the tool that
/// drew it, the tool that was active then, and the wait's cancel. It speaks
/// for the drawing only while the model's draft is that same object: the
/// next stroke, and whatever ends the drawing, put another draft there.
struct Wait {
    draft: Rc<Draft>,
    tool_id: Option<String>,
    active_tool_id: Option<String>,
    cancel: Cancel,
}

#[derive(Default)]
pub struct DraftsState {
    anchor: RefCell<Option<(Rc<Model>, Value)>>,
    wait: RefCell<Option<Wait>>,
}

impl Core {
    /// The `captured` effect: a capture-only tool reports its line.
    pub(crate) fn capture(&self, effect: &CapturedEffect) {
        if let Shape::Line(line) = &effect.geometry {
            self.events.draft_captured.emit(&json!({
                "tool": effect.tool,
                "page": effect.page.json(),
                "from": line.line_points.start.json(),
                "to": line.line_points.end.json(),
            }));
        }
    }

    /// `draft.get()`: the shape being drawn, the same object until it changes.
    pub fn draft_get(&self) -> Value {
        let model = self.model();
        if let Some((m, v)) = &*self.drafts.anchor.borrow()
            && Rc::ptr_eq(m, &model)
        {
            return v.clone();
        }
        let anchor = creation_draft_anchor(&model).map_or(Value::Null, |a| a.json());
        *self.drafts.anchor.borrow_mut() = Some((model, anchor.clone()));
        anchor
    }

    /// `draft.finish(options)`: end the draft as a double-click does.
    pub fn draft_finish(&self, signal: Option<AbortSignal>) -> Promise<Value> {
        let model = self.model();
        let Some(draft) = model.draft() else { return Promise::resolved(Value::Null) };
        if !draft.is_create() {
            return Promise::resolved(Value::Null);
        }
        let ink = matches!(draft, Draft::CreateInk(_));
        let tool_id = self.active_tool().map(|t| t.id.clone());
        let message = json!({ "type": if ink { "finishInkDraft" } else { "finishCreationDraft" } });
        let commit =
            match self.commit(message, Some(self.after_create_shape(tool_id.clone())), None) {
                Ok(c) => c,
                Err(e) => return Promise::rejected(e),
            };
        self.after_create_done(tool_id.as_deref(), &commit.effects);
        if !commit
            .effects
            .iter()
            .any(|e| matches!(e, epdf_annotation_core::types::Effect::Create(_)))
        {
            return Promise::resolved(Value::Null);
        }
        let created = self.cancellable(signal, created_ref_of(&commit));
        let me = self.weak();
        Promise::spawn(async move {
            let reference = created.await?;
            let Some(core) = me.upgrade() else { return Ok(Value::Null) };
            Ok(match core.get(&reference) {
                Some(annotation) => json!({ "annotation": annotation }),
                None => Value::Null,
            })
        })
    }

    /// `draft.cancel()` and the capability's `cancel()`.
    pub fn cancel(&self) {
        self.commit_or_log(json!({ "type": "cancel" }));
    }

    /// `finishInkDraft()`: make the pen drawing that waits for its next
    /// stroke an annotation now, as its tool's `afterCreate` says. A stroke
    /// still being drawn is left alone. A commit that throws rejects here,
    /// as it does in the TypeScript.
    pub fn finish_ink_draft(&self) -> Result<(), epdf_kernel::Error> {
        self.finish_waiting_ink(true)
    }

    // ── the pen drawing that waits for its next stroke ───────────────────

    /// `waitingInk()`: the ink draft whose pen is up. A stroke still being
    /// drawn is not waiting.
    fn waiting_ink(&self) -> Option<Rc<Draft>> {
        let draft = self.model().draft.clone()?;
        match &*draft {
            Draft::CreateInk(ink) if ink.drawing != Some(true) => Some(draft),
            _ => None,
        }
    }

    /// `finishWaitingInk({ selects })`: make the drawing that waits an
    /// annotation now, as its tool's `afterCreate` says. With `selects` false
    /// the selection stays as it is: what ended the wait decides it.
    pub(crate) fn finish_waiting_ink(&self, selects: bool) -> Result<(), epdf_kernel::Error> {
        let Some(draft) = self.waiting_ink() else { return Ok(()) };
        let wait = self.drafts.wait.borrow_mut().take();
        if let Some(w) = &wait {
            w.cancel.call();
        }
        let running = wait.filter(|w| Rc::ptr_eq(&w.draft, &draft));
        let tool_id = match &running {
            Some(w) => w.tool_id.clone(),
            None => self.active_tool().map(|t| t.id.clone()),
        };
        let adjust: Adjust = if selects {
            self.after_create_shape(tool_id.clone())
        } else {
            let selected = self.model().selected.clone();
            Rc::new(move |mut result: UpdateResult| {
                result.session.selected = selected.clone();
                result
            })
        };
        // A commit that throws goes to the caller: the store's commit then
        // never runs the message that would have taken the drawing's place.
        let commit = self.commit(json!({ "type": "finishInkDraft" }), Some(adjust), None)?;
        // Going back to the default tool is for a user still on the tool they
        // drew with. After a tool change the choice is theirs.
        let active = self.active_tool().map(|t| t.id.clone());
        if running.as_ref().is_none_or(|w| w.active_tool_id == active) {
            self.after_create_done(tool_id.as_deref(), &commit.effects);
        }
        Ok(())
    }

    /// `waitForNextStroke(toolId, delayMs)`: the pen is up and its stroke is
    /// held back: wait `delay_ms` for the next stroke of the same drawing.
    /// When none comes, the drawing is finished.
    pub(crate) fn wait_for_next_stroke(&self, tool_id: Option<String>, delay_ms: f64) {
        let Some(draft) = self.waiting_ink() else { return };
        if let Some(w) = self.drafts.wait.borrow_mut().take() {
            w.cancel.call();
        }
        let me = self.weak();
        let mine = draft.clone();
        let cancel = self.cx.clock().after(delay_ms, move || {
            let Some(core) = me.upgrade() else { return };
            // Only the drawing this wait was started for.
            if core.model().draft.as_ref().is_some_and(|d| Rc::ptr_eq(d, &mine)) {
                // A timer callback has no caller to throw to.
                let _ = core.finish_waiting_ink(true);
            }
        });
        let active_tool_id = self.active_tool().map(|t| t.id.clone());
        *self.drafts.wait.borrow_mut() = Some(Wait { draft, tool_id, active_tool_id, cancel });
    }

    /// `createDrafts`' listeners: a message that takes the drawing's place in
    /// the draft finishes it first (the store asks `finish_waiting_ink`), and
    /// a download finishes it too. A stroke still being drawn is left alone:
    /// it is not what the user sees as done yet.
    pub(crate) fn wire_drafts(&self) {
        let me = self.weak();
        let _ = self.cx.on_settle(Rc::new(move |_signal| {
            if let Some(core) = me.upgrade() {
                core.finish_waiting_ink(true)?;
            }
            Ok(None)
        }));
    }

    /// `hasCreationAwaitingClick()`: whether a shape that is made click by
    /// click waits for its next click: a polygon's or polyline's next corner,
    /// a callout's knee or box, a distance line's placement. That click is
    /// the shape's, also where it lands on an existing annotation.
    pub fn has_creation_awaiting_click(&self) -> bool {
        let model = self.model();
        match model.draft() {
            Some(Draft::CreatePoly(_) | Draft::CreateCallout(_)) => true,
            Some(Draft::CreateDistance(d)) => d.step == "offset",
            _ => false,
        }
    }

    /// `distanceCreationPage()`: the page a distance draft's offset step is on.
    pub fn distance_creation_page(&self) -> Option<Value> {
        let model = self.model();
        match model.draft() {
            Some(Draft::CreateDistance(d)) if d.step == "offset" => Some(d.page.json()),
            _ => None,
        }
    }
}
