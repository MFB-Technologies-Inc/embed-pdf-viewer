//! `tools/handlers.ts`: the four pointer handlers the plugin registers on
//! the interaction hub. Click-to-place (95), ambient editing (100), empty
//! page marquee (50) and drawing (55).

use std::cell::{Cell, RefCell};
use std::rc::{Rc, Weak};

use epdf_annotation_core::types::Point;
use epdf_plugin_interaction::{
    InteractionCapability, InteractionHandler, PointerKind, PointerSample, Tool, sample_point_on,
};
use serde_json::Value;

use crate::controller::Core;
use crate::host_contract::{
    ANNOTATION_DRAW_PRIORITY, ANNOTATION_EDIT_PRIORITY, ANNOTATION_MARQUEE_PRIORITY,
    ANNOTATION_PLACE_PRIORITY,
};
use crate::js::page_object_number;
use crate::read::chrome::HitView;

const MARQUEE_DRAG_THRESHOLD_PX: f64 = 4.0;

fn is_poly_tool(subtype: &str) -> bool {
    subtype == "polygon" || subtype == "polyline"
}

fn is_callout_tool(subtype: &str) -> bool {
    subtype == "free-text-callout"
}

fn kernel_page(page: &Value) -> epdf_kernel::geometry::PageRef {
    epdf_kernel::geometry::PageRef::from_json(page)
        .unwrap_or(epdf_kernel::geometry::PageRef::new(page_object_number(page) as u32))
}

/// `pointOn(sample, page)`: a sample against a gesture's home page.
fn point_on(sample: &PointerSample, page: &Value) -> Option<Point> {
    sample_point_on(sample, kernel_page(page))
}

fn rotation_of(sample: &PointerSample) -> Option<f64> {
    sample.page.as_ref().and_then(|h| h.rotation).map(|r| r.0)
}

fn view_of(sample: &PointerSample) -> HitView {
    let hit = sample.page.as_ref();
    HitView {
        scale: hit.and_then(|h| h.scale),
        rotation: hit.and_then(|h| h.rotation).map(|r| r.0),
        zoom: hit.and_then(|h| h.zoom),
    }
}

/// `followGhost(anno, interaction, sample)`.
fn follow_ghost(core: &Core, interaction: &InteractionCapability, sample: &PointerSample) {
    match &sample.page {
        Some(hit) if core.can_create() && !interaction.has_cursor_claim() => core.hover_ghost_at(
            &interaction.get_active_tool_id(),
            &hit.page.to_json(),
            hit.point,
            hit.rotation.map(|r| r.0),
            hit.zoom,
        ),
        _ => core.clear_ghost(),
    }
}

/// `createPlaceHandler(anno, interaction)`.
pub struct PlaceHandler {
    core: Weak<Core>,
    interaction: InteractionCapability,
}

impl InteractionHandler for PlaceHandler {
    fn id(&self) -> &str {
        "annotation-place"
    }
    fn priority(&self) -> f64 {
        ANNOTATION_PLACE_PRIORITY
    }
    fn enabled_for(&self, tool: &Tool) -> bool {
        tool.enables("annotation-place") || tool.enables("annotation-stamp")
    }
    fn on_pointer_down(&self, sample: &PointerSample) -> bool {
        let (Some(core), Some(hit)) = (self.core.upgrade(), sample.page.as_ref()) else {
            return false;
        };
        core.place_at(
            hit.page.page_object_number as f64,
            hit.point,
            hit.rotation.map(|r| r.0),
            hit.zoom,
        )
    }
    fn on_pointer_up(&self, _sample: &PointerSample) {
        if let Some(core) = self.core.upgrade() {
            core.clear_ghost();
        }
    }
    fn on_pointer_cancel(&self, _sample: &PointerSample) {
        if let Some(core) = self.core.upgrade() {
            core.clear_ghost();
        }
    }
    fn on_hover(&self, sample: &PointerSample) {
        if let Some(core) = self.core.upgrade() {
            follow_ghost(&core, &self.interaction, sample);
        }
    }
}

/// The edit gesture's home page, its last point, its down point, and whether it is touch.
struct EditOrigin {
    page: Value,
    point: Point,
    down_point: Point,
    touch: bool,
}

/// `createEditHandler(anno, interaction)`.
pub struct EditHandler {
    core: Weak<Core>,
    interaction: InteractionCapability,
    origin: RefCell<Option<EditOrigin>>,
}

impl InteractionHandler for EditHandler {
    fn id(&self) -> &str {
        "annotation-edit"
    }
    fn priority(&self) -> f64 {
        ANNOTATION_EDIT_PRIORITY
    }
    fn enabled_for(&self, tool: &Tool) -> bool {
        tool.enables("annotation-edit")
    }
    fn claims_touch(&self, sample: &PointerSample) -> bool {
        let (Some(core), Some(hit)) = (self.core.upgrade(), sample.page.as_ref()) else {
            return false;
        };
        core.claims_touch_at(&hit.page.to_json(), hit.point, view_of(sample))
    }
    fn on_pointer_down(&self, sample: &PointerSample) -> bool {
        let (Some(core), Some(hit)) = (self.core.upgrade(), sample.page.as_ref()) else {
            return false;
        };
        // A shape that is made click by click owns its next click, also where
        // it lands on an existing annotation: the draw handler takes it.
        if core.has_creation_awaiting_click() {
            return false;
        }
        let touch = sample.pointer_type == Some(PointerKind::Touch);
        let page = hit.page.to_json();
        let was_editing = core.get_editing_id().is_some();
        if was_editing {
            let _ = core.text_end(None);
        }
        if core.get_hit_kind(&page, hit.point, view_of(sample), Some(touch)) == "empty" {
            if !sample.modifiers.shift {
                core.selection_clear();
            }
            return was_editing;
        }
        if sample.click_count.unwrap_or(1.0) >= 2.0
            && core.begin_text_edit_at(&page, hit.point, view_of(sample))
        {
            return true;
        }
        let view = view_of(sample);
        let _ = core.edit_pointer(
            "down",
            &page,
            hit.point,
            sample.modifiers.shift,
            view.scale,
            view.rotation,
            view.zoom,
            Some(touch),
        );
        *self.origin.borrow_mut() =
            Some(EditOrigin { page, point: hit.point, down_point: hit.point, touch });
        true
    }
    fn on_pointer_move(&self, sample: &PointerSample) {
        let Some(core) = self.core.upgrade() else { return };
        let (page, touch) = {
            let origin = self.origin.borrow();
            let Some(origin) = origin.as_ref() else { return };
            (origin.page.clone(), origin.touch)
        };
        let Some(point) = point_on(sample, &page) else { return };
        if let Some(origin) = self.origin.borrow_mut().as_mut() {
            origin.point = point;
        }
        let view = view_of(sample);
        let _ = core.edit_pointer(
            "move",
            &page,
            point,
            sample.modifiers.shift,
            view.scale,
            view.rotation,
            view.zoom,
            Some(touch),
        );
    }
    fn on_pointer_up(&self, sample: &PointerSample) {
        let Some(core) = self.core.upgrade() else { return };
        let Some(origin) = self.origin.borrow_mut().take() else { return };
        let point = point_on(sample, &origin.page).unwrap_or(origin.point);
        let _ = core.edit_pointer(
            "up",
            &origin.page,
            point,
            false,
            None,
            None,
            None,
            Some(origin.touch),
        );
    }
    fn on_pointer_cancel(&self, _sample: &PointerSample) {
        let Some(core) = self.core.upgrade() else { return };
        let Some(origin) = self.origin.borrow_mut().take() else { return };
        let _ = core.edit_pointer(
            "move",
            &origin.page,
            origin.down_point,
            false,
            None,
            None,
            None,
            Some(origin.touch),
        );
        let _ = core.edit_pointer(
            "up",
            &origin.page,
            origin.down_point,
            false,
            None,
            None,
            None,
            Some(origin.touch),
        );
    }
    fn on_hover(&self, sample: &PointerSample) {
        let Some(core) = self.core.upgrade() else { return };
        // While a shape waits for its next click, a click here is the shape's:
        // no annotation under the pointer shows as one to grab or as hovered.
        let page = if core.has_creation_awaiting_click() { None } else { sample.page.as_ref() };
        let cursor = match page {
            Some(hit) => core.get_cursor_at(&hit.page.to_json(), hit.point, view_of(sample)),
            None => Value::Null,
        };
        self.interaction.claim_cursor("annotation", cursor.as_str(), 20.0);
        let at = page.map(|hit| (hit.page.to_json(), hit.point, view_of(sample)));
        let _ = core.hover_at(at.as_ref().map(|(p, point, v)| (p, *point, *v)));
    }
}

/// The marquee's press.
struct MarqueeAnchor {
    page: Value,
    point: Point,
    vx: f64,
    vy: f64,
    shift: bool,
}

/// `createMarqueeHandler(anno)`.
pub struct MarqueeHandler {
    core: Weak<Core>,
    anchor: RefCell<Option<MarqueeAnchor>>,
    last: RefCell<Option<Point>>,
    dragging: Cell<bool>,
    view: Cell<HitView>,
}

impl InteractionHandler for MarqueeHandler {
    fn id(&self) -> &str {
        "annotation-marquee"
    }
    fn priority(&self) -> f64 {
        ANNOTATION_MARQUEE_PRIORITY
    }
    fn enabled_for(&self, tool: &Tool) -> bool {
        tool.enables("annotation-marquee")
    }
    fn on_pointer_down(&self, sample: &PointerSample) -> bool {
        let Some(hit) = sample.page.as_ref() else { return false };
        *self.anchor.borrow_mut() = Some(MarqueeAnchor {
            page: hit.page.to_json(),
            point: hit.point,
            vx: sample.viewport.x,
            vy: sample.viewport.y,
            shift: sample.modifiers.shift,
        });
        *self.last.borrow_mut() = Some(hit.point);
        self.view.set(view_of(sample));
        self.dragging.set(false);
        true
    }
    fn on_pointer_move(&self, sample: &PointerSample) {
        let Some(core) = self.core.upgrade() else { return };
        let (page, start, vx, vy, shift) = {
            let anchor = self.anchor.borrow();
            let Some(a) = anchor.as_ref() else { return };
            (a.page.clone(), a.point, a.vx, a.vy, a.shift)
        };
        let Some(point) = point_on(sample, &page) else { return };
        *self.last.borrow_mut() = Some(point);
        let view = self.view.get();
        if !self.dragging.get() {
            if epdf_geometry::js::hypot(sample.viewport.x - vx, sample.viewport.y - vy)
                < MARQUEE_DRAG_THRESHOLD_PX
            {
                return;
            }
            self.dragging.set(true);
            let _ = core.marquee_pointer("down", &page, start, shift, view.rotation, view.zoom);
        }
        let _ = core.marquee_pointer("move", &page, point, shift, view.rotation, view.zoom);
    }
    fn on_pointer_up(&self, _sample: &PointerSample) {
        let anchor = self.anchor.borrow_mut().take();
        let last = self.last.borrow_mut().take();
        let dragging = self.dragging.replace(false);
        if let (true, Some(anchor), Some(last), Some(core)) =
            (dragging, anchor, last, self.core.upgrade())
        {
            let view = self.view.get();
            let _ = core.marquee_pointer(
                "up",
                &anchor.page,
                last,
                anchor.shift,
                view.rotation,
                view.zoom,
            );
        }
    }
    fn on_pointer_cancel(&self, _sample: &PointerSample) {
        *self.anchor.borrow_mut() = None;
        *self.last.borrow_mut() = None;
        self.dragging.set(false);
        if let Some(core) = self.core.upgrade() {
            core.cancel();
        }
    }
}

/// The draw gesture's home page and last point.
struct DrawOrigin {
    page: Value,
    point: Point,
}

/// `createDrawHandler(anno, interaction)`.
pub struct DrawHandler {
    core: Weak<Core>,
    interaction: InteractionCapability,
    drawing_poly: Cell<bool>,
    drawing_callout: Cell<bool>,
    follow_page: RefCell<Option<Value>>,
    origin: RefCell<Option<DrawOrigin>>,
}

impl DrawHandler {
    fn tool_id(&self) -> String {
        self.interaction.get_active_tool_id()
    }

    fn reset(&self) {
        self.drawing_poly.set(false);
        self.drawing_callout.set(false);
        *self.follow_page.borrow_mut() = None;
        *self.origin.borrow_mut() = None;
    }
}

impl InteractionHandler for DrawHandler {
    fn id(&self) -> &str {
        "annotation-draw"
    }
    fn priority(&self) -> f64 {
        ANNOTATION_DRAW_PRIORITY
    }
    fn enabled_for(&self, tool: &Tool) -> bool {
        tool.enables("annotation-draw")
    }
    fn on_pointer_down(&self, sample: &PointerSample) -> bool {
        let Some(core) = self.core.upgrade() else { return false };
        let tool = self.tool_id();
        let subtype = core.get_tool_subtype(&tool);
        if let Some(distance_page) = core.distance_creation_page() {
            if let Some(point) = point_on(sample, &distance_page) {
                let _ = core.create_pointer(&tool, "down", &distance_page, point, false, None);
            }
            *self.origin.borrow_mut() = None;
            return true;
        }
        let Some(hit) = sample.page.as_ref() else { return false };
        let page = hit.page.to_json();
        *self.origin.borrow_mut() = Some(DrawOrigin { page: page.clone(), point: hit.point });
        if is_poly_tool(&subtype) {
            let finish = sample.click_count.unwrap_or(1.0) >= 2.0;
            let _ = core.create_pointer(&tool, "down", &page, hit.point, finish, None);
            self.drawing_poly.set(!finish);
            *self.follow_page.borrow_mut() = if finish { None } else { Some(page) };
            return true;
        }
        self.drawing_poly.set(false);
        if is_callout_tool(&subtype) {
            self.drawing_callout.set(true);
            *self.follow_page.borrow_mut() = Some(page.clone());
        } else {
            *self.follow_page.borrow_mut() = None;
        }
        let _ = core.create_pointer(&tool, "down", &page, hit.point, false, rotation_of(sample));
        true
    }
    fn on_pointer_move(&self, sample: &PointerSample) {
        let Some(core) = self.core.upgrade() else { return };
        let tool = self.tool_id();
        let subtype = core.get_tool_subtype(&tool);
        let page = match self.origin.borrow().as_ref() {
            Some(o) => o.page.clone(),
            None => return,
        };
        if is_poly_tool(&subtype) && !self.drawing_poly.get() {
            return;
        }
        let Some(point) = point_on(sample, &page) else { return };
        if let Some(origin) = self.origin.borrow_mut().as_mut() {
            origin.point = point;
        }
        let _ = core.create_pointer(&tool, "move", &page, point, false, None);
    }
    fn on_pointer_up(&self, sample: &PointerSample) {
        let Some(core) = self.core.upgrade() else { return };
        let tool = self.tool_id();
        let subtype = core.get_tool_subtype(&tool);
        let origin = self.origin.borrow_mut().take();
        if let Some(origin) = origin
            && !is_poly_tool(&subtype)
        {
            let point = point_on(sample, &origin.page).unwrap_or(origin.point);
            let _ = core.create_pointer(&tool, "up", &origin.page, point, false, None);
        }
        *self.origin.borrow_mut() = None;
        core.clear_ghost();
    }
    fn on_pointer_cancel(&self, _sample: &PointerSample) {
        self.drawing_poly.set(false);
        self.drawing_callout.set(false);
        *self.follow_page.borrow_mut() = None;
        *self.origin.borrow_mut() = None;
        if let Some(core) = self.core.upgrade() {
            core.cancel();
            core.clear_ghost();
        }
    }
    fn on_hover(&self, sample: &PointerSample) {
        let Some(core) = self.core.upgrade() else { return };
        follow_ghost(&core, &self.interaction, sample);
        let tool = self.tool_id();
        let subtype = core.get_tool_subtype(&tool);
        if let Some(distance_page) = core.distance_creation_page() {
            if let Some(point) = point_on(sample, &distance_page) {
                let _ = core.create_pointer(&tool, "move", &distance_page, point, false, None);
            }
            return;
        }
        let follow = self.follow_page.borrow().clone();
        if let Some(page) = follow
            && ((self.drawing_poly.get() && is_poly_tool(&subtype))
                || (self.drawing_callout.get() && is_callout_tool(&subtype)))
            && let Some(point) = point_on(sample, &page)
        {
            let _ = core.create_pointer(&tool, "move", &page, point, false, None);
        }
    }
}

/// Build the four handlers.
pub fn create_handlers(
    core: &Core,
    interaction: &InteractionCapability,
) -> (Rc<PlaceHandler>, Rc<EditHandler>, Rc<MarqueeHandler>, Rc<DrawHandler>) {
    let place = Rc::new(PlaceHandler { core: core.weak(), interaction: interaction.clone() });
    let edit = Rc::new(EditHandler {
        core: core.weak(),
        interaction: interaction.clone(),
        origin: RefCell::new(None),
    });
    let marquee = Rc::new(MarqueeHandler {
        core: core.weak(),
        anchor: RefCell::new(None),
        last: RefCell::new(None),
        dragging: Cell::new(false),
        view: Cell::new(HitView::default()),
    });
    let draw = Rc::new(DrawHandler {
        core: core.weak(),
        interaction: interaction.clone(),
        drawing_poly: Cell::new(false),
        drawing_callout: Cell::new(false),
        follow_page: RefCell::new(None),
        origin: RefCell::new(None),
    });
    (place, edit, marquee, draw)
}

impl DrawHandler {
    /// A tool change ends the gesture this handler follows.
    pub(crate) fn wire(self: &Rc<Self>, core: &Core) {
        let me = Rc::downgrade(self);
        let off = self.interaction.on_tool_changed(move |_| {
            if let Some(handler) = me.upgrade() {
                handler.reset();
            }
            Ok(())
        });
        core.cx.listen(off);
    }
}
