//! `hit.ts`: paint order, what is under a point, and the hover cursor.

use std::borrow::Cow;

use crate::anchor::{
    AnchorMode, anchor_mode_as, anchor_mode_of, anchored_geom, anchored_stroke_width,
};
use crate::editable::is_transformable_by_hand;
use crate::flags::{annot_interactive_as, viewable};
use crate::geometry::{
    geom_handles, geom_painted, place_rotate_knob, point_in_quad, rect_handles_for,
};
use crate::group::group_caps;
use crate::js;
use crate::js::IdSet;
use crate::kinds::AnnotationKind;
use crate::measurement::{
    caption_painted, distance_caption_hit, distance_handles, distance_layout, distance_painted,
    measurement_of,
};
use crate::measurement_shape::measurement_layout;
use crate::painted::{PaintedPiece, painted_near};
use crate::plane::is_substrate_only;
use crate::record::{kind_of, shape_of, style_of};
use crate::rect::{cursor_on_screen, union_rect};
use crate::selection::{Live, annotation_selection_frame, annotation_turn_pivot};
use crate::shapes::boxes::is_in_turned_box;
use crate::types::{
    ChromeGeometry, Cursor, GroupHandleTarget, HandleTarget, Id, Model, ModelAnnotation, PageRef,
    Point, Rect, RotateTarget, Shape, Stroke, Style, Target, ViewEnv,
};

/// A record in paint order, with its kind.
pub(crate) type PaintedRecord<'a> = (&'a Id, &'a ModelAnnotation, Cow<'static, AnnotationKind>);

/// [`paint_order`]'s records, each with the kind it was ordered by.
pub(crate) fn painted_records<'a>(model: &'a Model, page: &PageRef) -> Vec<PaintedRecord<'a>> {
    let mut markup = Vec::new();
    let mut other = Vec::new();
    for id in model.order.iter() {
        let Some(record) = model.record(id) else { continue };
        if record.page_number() != Some(page.object_number) {
            continue;
        }
        if !viewable(&record.annotation, model.is_selected(id)) {
            continue;
        }
        if is_substrate_only(&record.annotation) {
            continue;
        }
        let kind = kind_of(&record.annotation);
        if kind.caps.paints_beneath {
            markup.push((id, record, kind))
        } else {
            other.push((id, record, kind))
        }
    }
    markup.extend(other);
    markup
}

/// `paintOrder(model, page)`: page ids back to front, markup first, culled by `/F` and plane.
pub fn paint_order(model: &Model, page: &PageRef) -> Vec<Id> {
    painted_records(model, page).into_iter().map(|(id, _, _)| id.clone()).collect()
}

/// `isSelectable(model, id)`.
pub fn is_selectable(model: &Model, id: &str) -> bool {
    model.record(id).is_some_and(|r| is_selectable_as(r, &kind_of(&r.annotation)))
}

/// [`is_selectable`] for a record whose kind is `kind`.
fn is_selectable_as(record: &ModelAnnotation, kind: &AnnotationKind) -> bool {
    annot_interactive_as(record, kind) && kind.caps.selectable
}

fn text_bound(record: &ModelAnnotation) -> bool {
    kind_of(&record.annotation).caps.anchored
        && matches!(shape_of(&record.annotation), Shape::Quads(_))
}

/// `isMovable(record)`: an annotation people move at all: its kind moves,
/// and it isn't bound to text. Whether it can be moved now is [`can_move`].
pub fn is_movable(record: &ModelAnnotation) -> bool {
    kind_of(&record.annotation).caps.movable && !text_bound(record)
}

/// `canMove(model, id)`.
pub fn can_move(model: &Model, id: &str) -> bool {
    model.record(id).is_some_and(|r| is_transformable_by_hand(&model.editable, r) && is_movable(r))
}

fn has_handles(model: &Model, record: &ModelAnnotation) -> bool {
    if !is_transformable_by_hand(&model.editable, record) || text_bound(record) {
        return false;
    }
    let kind = kind_of(&record.annotation);
    kind.caps.resizable || kind.caps.vertex_editable
}

fn hit_geom_of(record: &ModelAnnotation, view: Option<&ViewEnv>) -> Shape {
    anchored_geom(&shape_of(&record.annotation), anchor_mode_of(record).as_ref(), view)
}

fn hit_stroke_of(style: &Style, mode: Option<&AnchorMode>, view: Option<&ViewEnv>) -> f64 {
    anchored_stroke_width(style.stroke_width, mode, view)
}

fn is_filled(kind: &AnnotationKind, shape: &Shape, style: &Style) -> bool {
    !style.interior_color.is_null() || matches!(shape, Shape::Quads(_)) || kind.caps.opaque_body
}

/// `paintedOf(record, view)`: what the record paints where the pointer meets it.
pub fn painted_of(record: &ModelAnnotation, view: Option<&ViewEnv>) -> Vec<PaintedPiece> {
    painted_as(record, &kind_of(&record.annotation), view)
}

/// [`painted_of`] for a record whose kind is `kind`: its style, shape and
/// anchor mode are read once.
fn painted_as(
    record: &ModelAnnotation,
    kind: &AnnotationKind,
    view: Option<&ViewEnv>,
) -> Vec<PaintedPiece> {
    let annotation = &record.annotation;
    let style = kind.style_of(annotation);
    let shape = kind.family.read(annotation);
    let mode = anchor_mode_as(annotation, kind);
    let measure = measurement_of(annotation);
    let geometry = anchored_geom(&shape, mode.as_ref(), view);
    let filled = is_filled(kind, &shape, &style);
    let mut stroked = style;
    stroked.stroke_width = hit_stroke_of(&stroked, mode.as_ref(), view);
    if let Some(m) = measure.as_ref().filter(|m| m.is_distance())
        && let Some(distance) = distance_layout(&geometry, m, stroked.stroke_width)
    {
        return distance_painted(&distance, stroked.stroke_width);
    }
    let caption = measure
        .as_ref()
        .and_then(|m| measurement_layout(&geometry, m, &stroked))
        .and_then(|l| l.caption().cloned());
    let shape = geom_painted(&geometry, stroked.stroke(), filled);
    match caption {
        Some(c) => {
            let mut out = caption_painted(Some(&c));
            out.extend(shape);
            out
        }
        None => shape,
    }
}

/// `isOnTextBox(record, point, margin, view)`.
pub fn is_on_text_box(
    record: &ModelAnnotation,
    point: Point,
    margin: f64,
    view: Option<&ViewEnv>,
) -> bool {
    match hit_geom_of(record, view) {
        Shape::TextBox(s) => is_in_turned_box(s.box_, s.rotation, point, margin),
        _ => false,
    }
}

fn in_rect(rect: Rect, p: Point) -> bool {
    p.x >= rect.x && p.x <= rect.x + rect.width && p.y >= rect.y && p.y <= rect.y + rect.height
}

fn in_frame(record: &ModelAnnotation, point: Point, view: Option<&ViewEnv>) -> bool {
    point_in_quad(point, &annotation_selection_frame(record, view, &Live::default()).corners)
}

fn on_page(record: &ModelAnnotation, page: &PageRef) -> bool {
    record.page_number() == Some(page.object_number)
}

fn selection_union_bounds(model: &Model, page: &PageRef, view: Option<&ViewEnv>) -> Option<Rect> {
    let selection: Vec<&Id> = model
        .selected
        .iter()
        .filter(|id| {
            model.record(id).is_some_and(|r| on_page(r, page))
                && is_selectable(model, id)
                && can_move(model, id)
        })
        .collect();
    if selection.len() < 2 {
        return None;
    }
    let corners: Vec<Point> = selection
        .iter()
        .filter_map(|id| model.record(id))
        .flat_map(|r| annotation_selection_frame(r, view, &Live::default()).corners)
        .collect();
    Some(union_rect(&corners))
}

/// `groupUnionBounds(model, page, view)`.
pub fn group_union_bounds(model: &Model, page: &PageRef, view: Option<&ViewEnv>) -> Option<Rect> {
    let corners: Vec<Point> = model
        .selected
        .iter()
        .filter_map(|id| model.record(id))
        .filter(|r| on_page(r, page))
        .flat_map(|r| annotation_selection_frame(r, view, &Live::default()).corners)
        .collect();
    (!corners.is_empty()).then(|| union_rect(&corners))
}

fn within(at: Point, point: Point, tol: f64) -> bool {
    (at.x - point.x).abs() <= tol && (at.y - point.y).abs() <= tol
}

fn ids_on_page(model: &Model, page: &PageRef) -> Vec<Id> {
    model
        .selected
        .iter()
        .filter(|id| model.record(id).is_some_and(|r| on_page(r, page)))
        .cloned()
        .collect()
}

/// `hitTest(model, page, point, chrome, strokeMargin, pageBox, inert, view)`.
#[allow(clippy::too_many_arguments)]
pub fn hit_test(
    model: &Model,
    page: &PageRef,
    point: Point,
    chrome: &ChromeGeometry,
    stroke_margin: f64,
    page_box: Option<Rect>,
    inert: Option<&IdSet>,
    view: Option<&ViewEnv>,
) -> Target {
    let rotation_handle = chrome.rotation_handle != Some(false);
    if model.selected.len() == 1 && is_selectable(model, &model.selected[0]) {
        let record = model.record(&model.selected[0]).expect("a selectable record");
        if on_page(record, page) {
            if kind_of(&record.annotation).caps.rotatable
                && is_transformable_by_hand(&model.editable, record)
            {
                let frame = annotation_selection_frame(record, view, &Live::default());
                let knob = place_rotate_knob(&frame.corners, chrome.knob_offset, page_box);
                if rotation_handle && within(knob.at, point, chrome.knob_tol) {
                    return Target::Rotate(RotateTarget {
                        ids: vec![record.id.clone()],
                        pivot: annotation_turn_pivot(record, view),
                        order: Default::default(),
                    });
                }
            }
            if has_handles(model, record) {
                let style = style_of(&record.annotation);
                let measure = measurement_of(&record.annotation);
                let geometry = hit_geom_of(record, view);
                let stroke = hit_stroke_of(&style, anchor_mode_of(record).as_ref(), view);
                let distance = measure
                    .as_ref()
                    .filter(|m| m.is_distance())
                    .and_then(|m| distance_layout(&geometry, m, stroke));
                let mut handles = match &distance {
                    Some(d) => distance_handles(d),
                    None => geom_handles(&geometry),
                };
                if distance.is_some() {
                    let distance_to =
                        |h: &crate::types::Handle| js::hypot(h.at.x - point.x, h.at.y - point.y);
                    js::sort_by(&mut handles, |l, r| distance_to(l) - distance_to(r));
                }
                let mut stroked = style.clone();
                stroked.stroke_width = stroke;
                let layout =
                    measure.as_ref().and_then(|m| measurement_layout(&geometry, m, &stroked));
                if let Some(layout) = &layout
                    && distance_caption_hit(
                        layout.caption(),
                        point,
                        js::min(2.0, chrome.handle_tol / 3.0),
                    )
                {
                    return Target::Handle(HandleTarget {
                        id: record.id.clone(),
                        handle: "caption".into(),
                        cursor: Some("move".into()),
                        order: Default::default(),
                    });
                }
                for handle in &handles {
                    if within(handle.at, point, chrome.handle_tol) {
                        return Target::Handle(HandleTarget {
                            id: record.id.clone(),
                            handle: handle.id.clone(),
                            cursor: cursor_on_screen(
                                handle.cursor.as_deref(),
                                view.map_or(0.0, |v| v.rotation),
                            ),
                            order: Default::default(),
                        });
                    }
                }
            }
        }
    } else if model.selected.len() > 1 {
        let caps = group_caps(model, &model.selected);
        if caps.rotatable
            && let Some(union) = group_union_bounds(model, page, view)
        {
            let corners = [
                Point { x: union.x, y: union.y },
                Point { x: union.x + union.width, y: union.y },
                Point { x: union.x + union.width, y: union.y + union.height },
                Point { x: union.x, y: union.y + union.height },
            ];
            let knob = place_rotate_knob(&corners, chrome.knob_offset, page_box);
            if rotation_handle && within(knob.at, point, chrome.knob_tol) {
                return Target::Rotate(RotateTarget {
                    ids: ids_on_page(model, page),
                    pivot: Point {
                        x: union.x + union.width / 2.0,
                        y: union.y + union.height / 2.0,
                    },
                    order: Default::default(),
                });
            }
        }
        if caps.resizable
            && let Some(union) = group_union_bounds(model, page, view)
        {
            for handle in rect_handles_for(union) {
                if within(handle.at, point, chrome.handle_tol) {
                    return Target::GroupHandle(GroupHandleTarget {
                        ids: ids_on_page(model, page),
                        handle: handle.id.clone(),
                        cursor: cursor_on_screen(
                            handle.cursor.as_deref(),
                            view.map_or(0.0, |v| v.rotation),
                        ),
                        box_: union,
                        order: Default::default(),
                    });
                }
            }
        }
    }
    let order = painted_records(model, page);
    for (id, record, kind) in order.iter().rev() {
        if inert.is_some_and(|set| set.has(id)) || !is_selectable_as(record, kind) {
            continue;
        }
        if model.is_selected(id) && can_move(model, id) && in_frame(record, point, view) {
            return Target::annot(id);
        }
        if painted_near(&painted_as(record, kind, view), point, stroke_margin) {
            return Target::annot(id);
        }
    }
    if let Some(union) = selection_union_bounds(model, page, view)
        && in_rect(union, point)
    {
        for (id, _, _) in order.iter().rev() {
            if model.is_selected(id) && can_move(model, id) {
                return Target::annot(id);
            }
        }
    }
    Target::empty()
}

/// `cursorAt(...)`: the hover cursor.
#[allow(clippy::too_many_arguments)]
pub fn cursor_at(
    model: &Model,
    page: &PageRef,
    point: Point,
    chrome: &ChromeGeometry,
    stroke_margin: f64,
    page_box: Option<Rect>,
    inert: Option<&IdSet>,
    view: Option<&ViewEnv>,
) -> Option<Cursor> {
    match hit_test(model, page, point, chrome, stroke_margin, page_box, inert, view) {
        Target::Handle(t) => t.cursor,
        Target::GroupHandle(t) => t.cursor,
        Target::Rotate(_) => Some("grab".into()),
        Target::Annot(t) => Some(
            if model.is_selected(&t.id) && can_move(model, &t.id) { "move" } else { "pointer" }
                .into(),
        ),
        Target::Empty(_) => None,
    }
}

/// A stroke at a width, for callers that only have one.
pub fn stroke_of_width(width: f64) -> Stroke {
    Stroke::width(width)
}
