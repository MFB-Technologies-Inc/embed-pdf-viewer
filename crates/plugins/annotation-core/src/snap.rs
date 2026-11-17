//! `snap.ts`: alignment snapping for the move gesture.

use std::cell::RefCell;
use std::collections::HashSet;
use std::rc::{Rc, Weak};

use crate::anchor::anchor_mode_of;
use crate::geometry::selection_quad;
use crate::hit::is_selectable;
use crate::js;
use crate::record::{shape_of, style_of};
use crate::rect::union_rect;
use crate::types::{ById, Guide, Id, Model, PageRef, Point, Rect};

/// A snapped move: the adjusted delta and the guides to draw (`SnapResult`).
#[derive(Clone, Debug, PartialEq)]
pub struct SnapResult {
    pub delta: Point,
    pub guides: Vec<Guide>,
}

const GUIDE_PAD: f64 = 14.0;

/// A move's snap targets: the moving records' bounds and every other
/// record's on the page that it snaps to, in record order. `None` when a
/// moving record is screen-anchored, which turns snapping off.
type Targets = Option<(Bounds, Vec<Bounds>)>;

/// What [`Targets`] are built from: the records and their order, the page
/// and the moving ids. The records and order are held weakly: a weak
/// pointer keeps their allocation, so no other map takes the address, and
/// a write through `Rc::make_mut` or `Rc::get_mut` cannot change the map
/// in place while one is held.
struct TargetKey {
    by_id: Weak<ById>,
    order: Weak<Vec<Id>>,
    page: u64,
    ids: Vec<Id>,
}

impl TargetKey {
    fn of(model: &Model, ids: &[Id], page: &PageRef) -> TargetKey {
        TargetKey {
            by_id: Rc::downgrade(&model.by_id),
            order: Rc::downgrade(&model.order),
            page: page.object_number.to_bits(),
            ids: ids.to_vec(),
        }
    }

    fn holds(&self, model: &Model, ids: &[Id], page: &PageRef) -> bool {
        std::ptr::eq(self.by_id.as_ptr(), Rc::as_ptr(&model.by_id))
            && std::ptr::eq(self.order.as_ptr(), Rc::as_ptr(&model.order))
            && self.page == page.object_number.to_bits()
            && self.ids == ids
    }
}

thread_local! {
    /// The last move's targets: a drag's samples share one model's records.
    static TARGETS: RefCell<Option<(TargetKey, Rc<Targets>)>> = const { RefCell::new(None) };
    static TARGET_BUILDS: std::cell::Cell<usize> = const { std::cell::Cell::new(0) };
}

/// How many times this thread built a move's snap targets. A move drag
/// builds them once, at its first sample.
pub fn snap_target_builds() -> usize {
    TARGET_BUILDS.with(std::cell::Cell::get)
}

/// The targets for moving `ids` on `page`, kept from the last call when
/// its records, order, page and ids are the same.
fn targets_of(model: &Model, ids: &[Id], page: &PageRef) -> Rc<Targets> {
    let kept = TARGETS.with(|t| {
        t.borrow().as_ref().filter(|(key, _)| key.holds(model, ids, page)).map(|(_, t)| t.clone())
    });
    if let Some(targets) = kept {
        return targets;
    }
    let targets = Rc::new(build_targets(model, ids, page));
    TARGETS.with(|t| *t.borrow_mut() = Some((TargetKey::of(model, ids, page), targets.clone())));
    targets
}

fn build_targets(model: &Model, ids: &[Id], page: &PageRef) -> Targets {
    TARGET_BUILDS.with(|c| c.set(c.get() + 1));
    if ids.iter().any(|id| model.record(id).is_some_and(|r| anchor_mode_of(r).is_some())) {
        return None;
    }
    let corners: Vec<Point> = ids.iter().flat_map(|id| annot_quad(model, id)).collect();
    let base = to_bounds(union_rect(&corners));
    let moving: HashSet<&str> = ids.iter().map(Id::as_str).collect();
    let mut targets = Vec::new();
    for id in model.order.iter() {
        if moving.contains(id.as_str()) {
            continue;
        }
        let Some(record) = model.record(id) else { continue };
        if record.page_number() != Some(page.object_number)
            || !is_selectable(model, id)
            || anchor_mode_of(record).is_some()
        {
            continue;
        }
        targets.push(to_bounds(union_rect(&annot_quad(model, id))));
    }
    Some((base, targets))
}

#[derive(Clone, Copy)]
struct Bounds {
    min: Point,
    max: Point,
}

fn to_bounds(rect: Rect) -> Bounds {
    Bounds {
        min: Point { x: rect.x, y: rect.y },
        max: Point { x: rect.x + rect.width, y: rect.y + rect.height },
    }
}

fn keys_x(b: &Bounds) -> [f64; 3] {
    [b.min.x, (b.min.x + b.max.x) / 2.0, b.max.x]
}

fn keys_y(b: &Bounds) -> [f64; 3] {
    [b.min.y, (b.min.y + b.max.y) / 2.0, b.max.y]
}

fn shift(b: &Bounds, p: Point) -> Bounds {
    Bounds {
        min: Point { x: b.min.x + p.x, y: b.min.y + p.y },
        max: Point { x: b.max.x + p.x, y: b.max.y + p.y },
    }
}

/// A record's selection quad; nothing for an id the model doesn't hold
/// (where the TypeScript throws reading it, this snaps to what is there).
fn annot_quad(model: &Model, id: &str) -> Vec<Point> {
    match model.record(id) {
        Some(record) => {
            selection_quad(&shape_of(&record.annotation), style_of(&record.annotation).stroke())
                .to_vec()
        }
        None => Vec::new(),
    }
}

fn guide(axis: &str, at: f64, lo: f64, hi: f64) -> Guide {
    Guide { axis: axis.into(), at, lo, hi, order: Default::default() }
}

/// `computeMoveSnap(model, ids, page, raw, threshold, pageBox)`.
pub fn compute_move_snap(
    model: &Model,
    ids: &[Id],
    page: &PageRef,
    raw: Point,
    threshold: f64,
    page_box: Option<Rect>,
) -> SnapResult {
    let kept = targets_of(model, ids, page);
    let Some((base, records)) = kept.as_ref() else {
        return SnapResult { delta: raw, guides: Vec::new() };
    };
    let base = *base;
    let moving_box = shift(&base, raw);
    let page_target = page_box.map(to_bounds);
    let mut bx: Option<(f64, f64, Bounds)> = None;
    let mut by: Option<(f64, f64, Bounds)> = None;
    for tb in page_target.iter().chain(records) {
        for tk in keys_x(tb) {
            for mk in keys_x(&moving_box) {
                let diff = tk - mk;
                if diff.abs() < threshold
                    && bx.is_none_or(|(adjust, _, _)| diff.abs() < adjust.abs())
                {
                    bx = Some((diff, tk, *tb));
                }
            }
        }
        for tk in keys_y(tb) {
            for mk in keys_y(&moving_box) {
                let diff = tk - mk;
                if diff.abs() < threshold
                    && by.is_none_or(|(adjust, _, _)| diff.abs() < adjust.abs())
                {
                    by = Some((diff, tk, *tb));
                }
            }
        }
    }
    let delta = Point { x: raw.x + bx.map_or(0.0, |b| b.0), y: raw.y + by.map_or(0.0, |b| b.0) };
    let snapped = shift(&base, delta);
    let mut guides = Vec::new();
    if let Some((_, at, tb)) = bx {
        guides.push(guide(
            "x",
            at,
            js::min(snapped.min.y, tb.min.y) - GUIDE_PAD,
            js::max(snapped.max.y, tb.max.y) + GUIDE_PAD,
        ));
    }
    if let Some((_, at, tb)) = by {
        guides.push(guide(
            "y",
            at,
            js::min(snapped.min.x, tb.min.x) - GUIDE_PAD,
            js::max(snapped.max.x, tb.max.x) + GUIDE_PAD,
        ));
    }
    SnapResult { delta, guides }
}
