//! The typed API a Rust caller sees, beyond what the vectors reach.
//! `ScaleChangeReport::page` has no TypeScript counterpart: the TypeScript
//! reads the `page` member both report shapes carry.

use std::rc::Rc;

use epdf_kernel::geometry::PageRef;
use epdf_kernel::kernel::{OpenOptions, OpenSource};
use epdf_kernel::{AnyPlugin, Kernel, KernelOptions, rt};
use epdf_plugin_annotation::annotation_plugin;
use epdf_plugin_interaction::{InteractionConfig, interaction_plugin};
use epdf_plugin_measurement::read::scale::readout_computations;
use epdf_plugin_measurement::{
    DefaultScale, MEASUREMENT, MeasurementConfig, MeasurementSettings, ScaleChangeReport,
    measurement_plugin,
};
use plugin_conformance::fake_engine::{AsEngine, FakeEngine};
use serde_json::{Value, json};

#[test]
fn a_report_names_its_page() {
    let page = PageRef::new(3);
    let failed =
        ScaleChangeReport::ScaleError { page, scale_error: json!({ "message": "refused" }) };
    assert_eq!(failed.page(), Some(page));
    let recalibrated = ScaleChangeReport::Recalibrated(json!({
        "page": page,
        "scale": null,
        "updated": [],
        "skipped": [],
        "failed": [],
    }));
    assert_eq!(recalibrated.page(), Some(page));
}

#[test]
fn a_presets_setting_the_typed_read_cannot_hold_is_refused() {
    let settings = MeasurementSettings::of(&json!({ "defaultScale": "imperial", "presets": [] }))
        .expect("an empty list is a list");
    assert_eq!(settings.default_scale, DefaultScale::Imperial);
    assert!(settings.presets.is_empty());
    for presets in [json!("metric-1"), json!([{ "id": "x" }]), json!(null)] {
        let error =
            MeasurementSettings::of(&json!({ "defaultScale": "metric", "presets": presets }))
                .expect_err("not a list of presets");
        assert_eq!(error.name, "TypeError");
        assert!(
            error.message.starts_with("[measurement] the presets setting"),
            "{}",
            error.message
        );
    }
}

/// A kernel with the interaction hub, the annotation plugin and this plugin
/// over the conformance fake engine; page 1 lists `annotations`.
fn kernel_listing(annotations: Vec<Value>) -> Kernel {
    rt::reset();
    let b = json!({ "x": 0, "y": 0, "width": 600, "height": 800 });
    let fake = FakeEngine::new(json!({ "documents": { "a": {
        "pages": [{
            "index": 0, "ref": PageRef::new(1), "label": null,
            "size": { "width": 600, "height": 800 }, "rotation": 0, "userUnit": 1,
            "tabOrder": null,
            "pdfCropBox": { "left": 0, "bottom": 0, "right": 600, "top": 800 },
            "boxes": { "media": b, "crop": b, "bleed": b, "trim": b, "art": b },
        }],
        "security": { "allows": "*" },
        "services": ["page.measure"],
        "responses": [
            { "path": "annotations.list", "value": { "annotations": annotations } },
            { "path": "page.measure.listViewports", "pon": 1, "value": { "viewports": [] } },
        ],
    } } }));
    let plugins: Vec<AnyPlugin> = vec![
        interaction_plugin(InteractionConfig::default()),
        annotation_plugin(None),
        measurement_plugin(MeasurementConfig::default()),
    ];
    let kernel =
        Kernel::new(Rc::new(AsEngine(fake)), plugins, KernelOptions::default()).expect("kernel");
    kernel.start();
    rt::run_until_stalled();
    kernel.documents().open(
        OpenSource::Input(json!({ "kind": "bytes", "id": "a", "bytes": { "$bytes": "" } })),
        OpenOptions::default(),
    );
    rt::run_until_stalled();
    kernel
}

/// A line dimension on page 1, 144 points long in metres at 1 in = 1 m.
fn dimension(object_number: u32) -> Value {
    json!({
        "subtype": "line", "intent": "line-dimension",
        "ref": { "kind": "objectNumber", "page": PageRef::new(1), "objectNumber": object_number },
        "page": PageRef::new(1),
        "rect": { "x": 0, "y": 0, "width": 144, "height": 1 },
        "linePoints": { "start": { "x": 0, "y": 0 }, "end": { "x": 144, "y": 0 } },
        "measure": {
            "subtype": "rectilinear",
            "ratio": "1 in = 1 m",
            "x": [{ "unit": "m", "conversion": 1.0 / 72.0, "precision": 100 }],
            "distance": [{ "unit": "m", "conversion": 1, "precision": 100 }],
            "area": [{ "unit": "m²", "conversion": 1, "precision": 100 }],
        },
    })
}

#[test]
fn readout_computed_once_over_two_reads() {
    let kernel = kernel_listing(vec![dimension(7), dimension(8)]);
    let measurement = kernel.capability(&MEASUREMENT, Some("a")).expect("measurement");
    let first = dimension(7)["ref"].clone();
    let before = readout_computations();
    let once = measurement.get_readout(&first);
    assert_eq!(once["label"], "2.00 m", "{once}");
    assert_eq!(measurement.get_readout(&first), once);
    assert_eq!(readout_computations() - before, 1, "the second read is the first answer");
    let second = measurement.get_readout(&dimension(8)["ref"]);
    assert_eq!(second, once);
    assert_eq!(readout_computations() - before, 2, "another record has its own readout");
}
