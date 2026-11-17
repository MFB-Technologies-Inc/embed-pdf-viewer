//! `read/scale.ts`: the page scale, the vocabulary, readouts, and pure
//! conversions in the page's scale.

use std::cell::{Cell, RefCell};
use std::collections::HashMap;
use std::rc::{Rc, Weak};

use epdf_annotation_core::engine::measure::{DimensionInput, measurement_readout};
use epdf_annotation_core::json::Object;
use epdf_kernel::Error;
use epdf_kernel::geometry::Point;
use serde_json::{Map, Value, json};

use crate::contract::{
    CalibrationRequest, PageInput, PageScale, ScaleChangeReport, ScalePreset, no_readout,
};
use crate::controller::MeasurementCapability;
use crate::units::{METRES, square_of};

/// `UNITS`: `Object.keys(METRES)`.
pub fn units() -> Vec<&'static str> {
    METRES.iter().map(|(unit, _)| *unit).collect()
}

/// `AREA_UNITS`: each unit squared, then `ha` and `acre`.
pub fn area_units() -> Vec<String> {
    let mut list: Vec<String> = units().into_iter().map(square_of).collect();
    list.extend(["ha".to_string(), "acre".to_string()]);
    list
}

fn point(p: Point) -> Value {
    json!({ "x": p.x, "y": p.y })
}

#[cfg(feature = "harness")]
thread_local! {
    static COMPUTATIONS: Cell<usize> = const { Cell::new(0) };
}

/// How many readouts this thread computed. A record's readout is computed
/// once while the record is the same. For the tests, with `harness`.
#[cfg(feature = "harness")]
#[doc(hidden)]
pub fn readout_computations() -> usize {
    COMPUTATIONS.with(Cell::get)
}

/// The readouts: one per annotation object, as the TypeScript `WeakMap`.
/// Each entry holds its annotation weakly, so the address is not reused
/// while the entry is there and `Rc::make_mut` moves a changed annotation to
/// a new one. Entries whose annotation is gone are dropped once the map has
/// doubled since the last sweep.
#[derive(Default)]
pub(crate) struct Readouts {
    by_object: RefCell<HashMap<*const Object, (Weak<Object>, Value)>>,
    sweep_at: Cell<usize>,
}

impl Readouts {
    fn get_or_compute(&self, annotation: &Rc<Object>) -> Value {
        let key = Rc::as_ptr(annotation);
        if let Some((_, readout)) = self.by_object.borrow().get(&key) {
            return readout.clone();
        }
        let answer = readout(annotation);
        let mut by_object = self.by_object.borrow_mut();
        by_object.insert(key, (Rc::downgrade(annotation), answer.clone()));
        if by_object.len() > self.sweep_at.get().max(64) {
            by_object.retain(|_, (held, _)| held.strong_count() > 0);
            self.sweep_at.set(by_object.len() * 2);
        }
        answer
    }
}

/// `measurementReadout(dto)` over a JSON object.
fn readout(object: &Map<String, Value>) -> Value {
    #[cfg(feature = "harness")]
    COMPUTATIONS.with(|n| n.set(n.get() + 1));
    measurement_readout(&DimensionInput::of(object)).to_value()
}

impl MeasurementCapability {
    /// May measure on the page: `annotations:create`, and the page is there.
    pub fn can_measure(&self, page: impl Into<PageInput>) -> Result<bool, Error> {
        Ok(self.core().cx.allows("annotations:create") && self.scale_of(page.into())?.ready)
    }

    /// The resolved scale, its source and readiness. The same `Rc` while
    /// unchanged; a page that isn't there is not ready and has no measure.
    pub fn get_page_scale(&self, page: impl Into<PageInput>) -> Result<Rc<PageScale>, Error> {
        self.scale_of(page.into())
    }

    /// A scale change is in flight.
    pub fn is_busy(&self) -> bool {
        self.core().cx.state().pending > 0.0
    }

    /// The reports of the last scale change, one per page.
    pub fn list_last_reports(&self) -> Rc<Vec<ScaleChangeReport>> {
        self.core().cx.state().reports.clone()
    }

    /// The `presets` setting; `Err` as [`get_settings`](Self::get_settings).
    pub fn list_presets(&self) -> Result<Vec<ScalePreset>, Error> {
        Ok(self.get_settings()?.presets)
    }

    /// The `presets` setting as the store holds it.
    pub fn list_presets_json(&self) -> Value {
        self.settings_json().get("presets").cloned().unwrap_or(Value::Null)
    }

    pub fn list_units(&self) -> Vec<&'static str> {
        units()
    }

    pub fn list_area_units(&self) -> Vec<String> {
        area_units()
    }

    /// The formatted measurement of a measurement annotation, or why there
    /// is none (`NO_READOUT` for an annotation that isn't there).
    pub fn get_readout(&self, reference: &Value) -> Value {
        let core = self.core();
        match core.annotation.get_shared(reference) {
            Some(raw) => core.readouts.get_or_compute(&raw),
            None => no_readout(),
        }
    }

    /// A distance between two page-space points in the page's scale: a pure
    /// conversion, no annotation.
    pub fn measure_distance(
        &self,
        page: impl Into<PageInput>,
        from: Point,
        to: Point,
    ) -> Result<Value, Error> {
        let measure = self.scale_of(page.into())?.measure.clone();
        let mut dto = Map::new();
        dto.insert("subtype".into(), json!("line"));
        dto.insert("intent".into(), json!("line-dimension"));
        dto.insert("measure".into(), measure);
        dto.insert("linePoints".into(), json!({ "start": point(from), "end": point(to) }));
        Ok(readout(&dto))
    }

    /// The area of a page-space polygon in the page's scale: a pure
    /// conversion, no annotation.
    pub fn measure_area(
        &self,
        page: impl Into<PageInput>,
        vertices: &[Point],
    ) -> Result<Value, Error> {
        let measure = self.scale_of(page.into())?.measure.clone();
        let mut dto = Map::new();
        dto.insert("subtype".into(), json!("polygon"));
        dto.insert("intent".into(), json!("polygon-dimension"));
        dto.insert("measure".into(), measure);
        dto.insert("vertices".into(), Value::Array(vertices.iter().copied().map(point).collect()));
        Ok(readout(&dto))
    }

    /// Two points captured, awaiting a length.
    pub fn get_calibration_request(&self) -> Option<CalibrationRequest> {
        self.core().cx.state().calibration.clone()
    }
}
