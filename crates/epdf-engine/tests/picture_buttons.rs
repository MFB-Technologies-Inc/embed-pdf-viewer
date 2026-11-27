//! A push button that holds a picture (`runPictureButtonConformance.ts`,
//! 7d5691a5a and bfbaac1f2): what `vectors/changes/picture-buttons.json`
//! doesn't record. What each widget draws, what a saved file holds (one
//! image, however many fields show it), the lock a signing puts on an image
//! field, and an export carried into another document. The single-session
//! answers (layouts, `hasImage`, refusals, rights, undo, new places, copies)
//! are the vector's, replayed by `change_ops.rs`.
//!
//! The fixture and the pictures are the generator's: the bytes recorded in
//! the vector's `fixtureHex` and `fixtures`.

use epdf_engine::host::Host;
use serde_json::{Value, json};

const DOC: &str = "pictures";
/// A DER `SEQUENCE`: what a completion takes as a CMS, without judging it.
const PLACEHOLDER_CMS: [u8; 8] = [0x30, 0x06, 0x02, 0x01, 0x01, 0x02, 0x01, 0x02];

const RED: [u8; 3] = [255, 0, 0];
const GREEN: [u8; 3] = [0, 255, 0];
const BLUE: [u8; 3] = [0, 0, 255];

const LAYOUTS: [&str; 7] = [
    "caption",
    "image",
    "caption-below",
    "caption-above",
    "caption-right",
    "caption-left",
    "caption-over",
];

fn unhex(text: &str) -> Vec<u8> {
    (0..text.len()).step_by(2).map(|i| u8::from_str_radix(&text[i..i + 2], 16).unwrap()).collect()
}

/// `PICTURE_BUTTON_PDF` and the pictures, as the change vectors record them.
struct Fixtures {
    pdf: Vec<u8>,
    png: Vec<u8>,
    jpeg: Vec<u8>,
    bands_pdf: Vec<u8>,
}

fn fixtures() -> Fixtures {
    let path = test_support::corpus::vectors_dir("changes").join("picture-buttons.json");
    let vectors: Value = serde_json::from_str(&std::fs::read_to_string(path).unwrap()).unwrap();
    assert_eq!(vectors["fixture"], "PICTURE_BUTTON_PDF");
    let picture = |key: &str| unhex(vectors["fixtures"][key].as_str().expect(key));
    Fixtures {
        pdf: unhex(vectors["fixtureHex"].as_str().unwrap()),
        png: picture("png"),
        jpeg: picture("jpeg"),
        bands_pdf: picture("pdf"),
    }
}

fn page() -> Value {
    json!({ "kind": "objectNumber", "objectNumber": 3 })
}

fn fqn(name: &str) -> Value {
    json!({ "kind": "fqn", "name": name })
}

/// A free box in the right column, as wide as the bands are, three to one.
fn free_box(row: u32) -> Value {
    json!({ "x": 300, "y": 40 + row * 40, "width": 90, "height": 30 })
}

/// How many image XObjects a saved file holds, each counted once.
fn images_in(pdf: &[u8]) -> usize {
    let text: String = pdf.iter().map(|b| char::from(*b)).collect();
    let mut count = 0;
    let mut rest = text.as_str();
    while let Some(at) = rest.find("/Subtype") {
        rest = &rest[at + "/Subtype".len()..];
        let value = rest.trim_start();
        if value.starts_with("/Image")
            && !value["/Image".len()..].starts_with(|c: char| c.is_ascii_alphanumeric())
        {
            count += 1;
        }
    }
    count
}

struct Raster {
    width: usize,
    height: usize,
    rgba: Vec<u8>,
}

impl Raster {
    /// Whether the pixel at a share of each side is drawn in `color`, give
    /// or take what a JPEG loses.
    fn shows(&self, x: f64, y: f64, color: [u8; 3]) -> bool {
        let column = ((self.width as f64 * x) as usize).min(self.width - 1);
        let row = ((self.height as f64 * y) as usize).min(self.height - 1);
        let i = (row * self.width + column) * 4;
        let [r, g, b, alpha] = [self.rgba[i], self.rgba[i + 1], self.rgba[i + 2], self.rgba[i + 3]];
        alpha > 200 && [r, g, b].iter().zip(color).all(|(c, want)| c.abs_diff(want) <= 40)
    }

    /// Whether the three bands fill it from side to side, at half its height.
    fn shows_bands(&self) -> bool {
        self.shows(1.0 / 6.0, 0.5, RED)
            && self.shows(3.0 / 6.0, 0.5, GREEN)
            && self.shows(5.0 / 6.0, 0.5, BLUE)
    }

    /// Whether any of the three bands shows where it would.
    fn shows_any_band(&self) -> bool {
        self.shows(1.0 / 6.0, 0.5, RED)
            || self.shows(3.0 / 6.0, 0.5, GREEN)
            || self.shows(5.0 / 6.0, 0.5, BLUE)
    }
}

struct Doc {
    host: Host,
    job: u64,
}

impl Doc {
    /// A fresh copy of `bytes`, opened with every scope.
    fn open(bytes: Vec<u8>) -> Self {
        Self::open_as(bytes, None)
    }

    fn open_as(bytes: Vec<u8>, scope: Option<&[&str]>) -> Self {
        let mut doc = Doc { host: Host::new(), job: 1 };
        let mut request = json!({ "kind": "open.fatMem", "effect": "open", "docId": DOC,
            "password": null, "reserveObjectNumbers": 64 });
        if let Some(scope) = scope {
            request["scope"] = json!(scope);
        }
        let (opened, _) = doc.send(request, vec![bytes]);
        assert!(opened.get("error").is_none(), "{opened}");
        doc
    }

    fn send(&mut self, mut request: Value, payloads: Vec<Vec<u8>>) -> (Value, Vec<Vec<u8>>) {
        self.job += 1;
        request["jobId"] = json!(self.job);
        request["docId"] = json!(DOC);
        if request.get("effect").is_none() {
            let kind = request["kind"].as_str().unwrap().to_owned();
            request["effect"] =
                json!(epdf_engine::host::RequestEffect::of(&kind).expect("a kind").as_str());
        }
        if request.get("opId").is_none() && request["effect"] == "write" {
            request["opId"] = json!(format!("op-{}", self.job));
        }
        let response = self.host.call_filled(&request.to_string(), payloads);
        (serde_json::from_str(&response.json).unwrap(), response.payloads)
    }

    /// A request's `result`, or its error.
    fn call(&mut self, request: Value, payloads: Vec<Vec<u8>>) -> Result<Value, Value> {
        let (mut answer, _) = self.send(request, payloads);
        match answer.get_mut("error") {
            Some(error) => Err(error.take()),
            None => Ok(answer["result"].take()),
        }
    }

    fn ok(&mut self, request: Value) -> Value {
        let shown = request.to_string();
        self.call(request, Vec::new()).unwrap_or_else(|e| panic!("{shown}: {e}"))
    }

    /// `doc.apply(change)`, bytes as its payloads: the `ChangeResult`.
    fn apply(&mut self, change: Value, payloads: Vec<Vec<u8>>) -> Result<Value, Value> {
        self.call(json!({ "kind": "document.apply", "change": change }), payloads)
            .map(|answer| answer["result"].clone())
    }

    /// `doc.forms.setButtonImage(field, image)`: the item it answers, or the
    /// refusal.
    fn picture(&mut self, name: &str, image: Option<&[u8]>) -> Result<Value, Value> {
        let (slot, payloads) = match image {
            Some(bytes) => (json!({ "payload": 0 }), vec![bytes.to_vec()]),
            None => (Value::Null, Vec::new()),
        };
        self.apply(
            json!({ "ops": [{ "type": "forms.setButtonImage", "field": fqn(name),
                "image": slot }] }),
            payloads,
        )
        .map(|result| result["items"][0].clone())
    }

    /// `download()`: the file as it is now, saved on top of the one opened.
    fn download(&mut self) -> Vec<u8> {
        let (answer, mut payloads) =
            self.send(json!({ "kind": "document.saveBuffer", "mode": "incremental" }), Vec::new());
        assert!(answer.get("error").is_none(), "{answer}");
        payloads.remove(0)
    }

    fn list(&mut self) -> Value {
        self.ok(json!({ "kind": "forms.list" }))
    }

    fn field(&mut self, name: &str) -> Value {
        self.list()["fields"]
            .as_array()
            .unwrap()
            .iter()
            .find(|f| f["name"] == name)
            .cloned()
            .unwrap_or_else(|| panic!("no field '{name}'"))
    }

    /// The rows of every widget of field `name`, in the field's order.
    fn rows_of(&mut self, name: &str) -> Vec<Value> {
        let form = self.list();
        let field = form["fields"]
            .as_array()
            .unwrap()
            .iter()
            .find(|f| f["name"] == name)
            .unwrap_or_else(|| panic!("no field '{name}'"));
        let rows = form["widgets"].as_array().unwrap();
        field["widgets"]
            .as_array()
            .unwrap()
            .iter()
            .map(|widget| {
                rows.iter()
                    .find(|row| row["ref"] == widget["ref"])
                    .cloned()
                    .unwrap_or_else(|| panic!("no row for a widget of '{name}'"))
            })
            .collect()
    }

    fn layouts_of(&mut self, name: &str) -> Vec<Value> {
        self.rows_of(name).iter().map(|row| row["layout"].clone()).collect()
    }

    /// The widget as the page shows it: the pixels of its normal appearance
    /// at scale 1 (`appearanceRaster(page, ref, 'forms')`).
    fn drawn(&mut self, row: &Value) -> Raster {
        let (answer, payloads) = self.send(
            json!({ "kind": "annotations.renderAppearances", "effect": "read", "page": page(),
                "family": "widgets", "options": { "viewport": { "kind": "scale", "scale": 1 } } }),
            Vec::new(),
        );
        let appearances = answer["result"]["result"]["appearances"]
            .as_array()
            .unwrap_or_else(|| panic!("{answer}"));
        for (appearance, pixels) in appearances.iter().zip(payloads) {
            if appearance["mode"] != "normal" || appearance["ref"] != row["ref"] {
                continue;
            }
            let raster = &appearance["raster"];
            let (width, height, stride) = (
                raster["width"].as_u64().unwrap() as usize,
                raster["height"].as_u64().unwrap() as usize,
                raster["stride"].as_u64().unwrap() as usize,
            );
            let mut rgba = Vec::with_capacity(width * height * 4);
            for line in 0..height {
                rgba.extend_from_slice(&pixels[line * stride..line * stride + width * 4]);
            }
            return Raster { width, height, rgba };
        }
        panic!("no normal appearance for {}", row["ref"]);
    }

    /// Every widget of field `name` shows the bands.
    fn all_show_bands(&mut self, name: &str) -> bool {
        let rows = self.rows_of(name);
        !rows.is_empty() && rows.iter().all(|row| self.drawn(row).shows_bands())
    }

    /// A new push button with a widget per layout given, each in its own
    /// free box.
    fn button(&mut self, name: &str, layouts: &[&str], first: u32) -> Value {
        let widgets: Vec<Value> = layouts
            .iter()
            .zip(first..)
            .map(|(layout, row)| json!({ "page": page(), "box": free_box(row), "layout": layout }))
            .collect();
        self.ok(json!({ "kind": "forms.createField",
            "draft": { "kind": "pushbutton", "name": name, "widgets": widgets } }))["result"]
            ["field"]
            .clone()
    }

    fn add_widget(&mut self, name: &str, placement: Value, op_id: &str) -> Value {
        self.ok(json!({ "kind": "forms.addWidget", "opId": op_id, "ref": fqn(name),
            "placement": placement }))["result"]
            .clone()
    }

    /// `doc.forms.export({ fields })`: the bundle and its resources' bytes.
    fn export(&mut self, fields: Value) -> (Value, Vec<Vec<u8>>) {
        let (answer, payloads) =
            self.send(json!({ "kind": "forms.export", "selection": { "fields": fields } }), vec![]);
        assert!(answer.get("error").is_none(), "{answer}");
        (answer["result"]["bundle"].clone(), payloads)
    }

    /// `doc.forms.import(bundle, { values })`: the import's result.
    fn import(&mut self, (bundle, payloads): &(Value, Vec<Vec<u8>>), values: bool) -> Value {
        let request = json!({ "kind": "forms.import", "bundle": bundle, "attribution": "stamp",
            "values": values, "mayScript": false });
        let shown = request.to_string();
        self.call(request, payloads.clone()).unwrap_or_else(|e| panic!("{shown}: {e}"))["result"]
            .clone()
    }
}

/// `[name, hasImage]` of each field a result names.
fn names_and_pictures(fields: &Value) -> Vec<(String, Value)> {
    fields
        .as_array()
        .unwrap()
        .iter()
        .map(|f| (f["name"].as_str().unwrap().to_owned(), f["hasImage"].clone()))
        .collect()
}

#[test]
fn another_apps_icon_shows_and_exports_as_a_one_page_pdf() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf);
    assert!(doc.all_show_bands("acrobat_image"));
    let (bundle, payloads) = doc.export(json!([fqn("acrobat_image")]));
    let id = bundle["widgets"][0]["resources"]["image"].as_str().expect("an image").to_owned();
    assert!(id.starts_with("sha256-"), "{id}");
    let slot = bundle["resources"][&id]["payload"].as_u64().expect("a payload") as usize;
    assert_eq!(&payloads[slot][..5], b"%PDF-");
}

#[test]
fn a_create_and_a_widget_update_write_any_layout_and_a_save_keeps_it() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf);
    doc.button("made", &LAYOUTS, 0);
    assert_eq!(doc.layouts_of("made"), LAYOUTS.map(|l| json!(l)));
    let first = doc.rows_of("made")[0]["ref"].clone();
    let patched = doc.ok(json!({ "kind": "forms.updateWidget", "widget": first,
        "patch": { "layout": "caption-left" } }));
    assert_eq!(patched["result"]["widget"]["layout"], "caption-left");
    let saved = doc.download();

    let mut reader = Doc::open_as(saved, Some(&["doc.open", "doc.render", "doc.forms.read"]));
    let mut expected = LAYOUTS.map(|l| json!(l));
    expected[0] = json!("caption-left");
    assert_eq!(reader.layouts_of("made"), expected);
}

#[test]
fn a_png_a_jpeg_and_a_one_page_pdf_become_the_picture_of_every_widget() {
    let f = fixtures();
    for (format, bytes) in [("PNG", &f.png), ("JPEG", &f.jpeg), ("PDF", &f.bands_pdf)] {
        let mut doc = Doc::open(f.pdf.clone());
        let field = doc.button("initials", &["image", "image"], 0);
        assert_eq!(field["hasImage"], false, "{format}");
        let item = doc.picture("initials", Some(bytes)).unwrap();
        assert_eq!(item["field"]["hasImage"], true, "{format}");
        let layouts: Vec<&Value> =
            item["widgets"].as_array().unwrap().iter().map(|w| &w["layout"]).collect();
        assert_eq!(layouts, [&json!("image"), &json!("image")], "{format}");
        assert_eq!(doc.field("initials")["hasImage"], true, "{format}");
        if format == "PDF" {
            // The PDF's red and blue bands, two to one, fitted whole in the
            // middle of the box (88 × 28 inside the border): 56 wide, from 17
            // to 73.
            let row = doc.rows_of("initials")[0].clone();
            let raster = doc.drawn(&row);
            assert!(raster.shows(0.35, 0.5, RED), "{format}");
            assert!(raster.shows(0.7, 0.5, BLUE), "{format}");
            assert!(!(raster.shows(0.1, 0.5, RED) || raster.shows(0.9, 0.5, BLUE)), "{format}");
        } else {
            assert!(doc.all_show_bands("initials"), "{format}");
        }
    }
}

#[test]
fn null_takes_the_picture_out_and_the_box_is_drawn_again() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf);
    doc.button("initials", &["image"], 0);
    doc.picture("initials", Some(&f.png)).unwrap();
    let emptied = doc.picture("initials", None).unwrap();
    assert_eq!(emptied["field"]["hasImage"], false);
    let row = doc.rows_of("initials")[0].clone();
    assert!(!doc.drawn(&row).shows_any_band());
}

#[test]
fn each_widget_shows_the_picture_as_its_layout_says() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf);
    doc.button("mixed", &["image", "caption"], 0);
    doc.picture("mixed", Some(&f.png)).unwrap();
    let rows = doc.rows_of("mixed");
    assert!(doc.drawn(&rows[0]).shows_bands());
    // The caption-only widget keeps the picture and shows its caption alone.
    assert!(!doc.drawn(&rows[1]).shows_any_band());
    assert_eq!(doc.field("mixed")["hasImage"], true);
}

#[test]
fn the_picture_is_stored_once_however_many_buttons_signature_fields_and_stamps_show_it() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf);
    for (at, name) in ["one", "two", "three"].into_iter().enumerate() {
        doc.button(name, &["image"], at as u32);
        doc.picture(name, Some(&f.png)).unwrap();
    }
    for (at, name) in ["mark_one", "mark_two"].into_iter().enumerate() {
        doc.ok(json!({ "kind": "forms.createField", "draft": { "kind": "signature",
            "name": name, "widgets": [{ "page": page(), "box": free_box(4 + at as u32) }] } }));
        let (answer, _) = doc.send(
            json!({ "kind": "forms.setSignatureAppearance", "ref": fqn(name),
                "appearance": { "payload": 0 } }),
            vec![f.png.clone()],
        );
        assert!(answer.get("error").is_none(), "{answer}");
    }
    let stamp = doc.call(
        json!({ "kind": "annotations.create", "page": page(),
            "draft": { "subtype": "stamp", "box": { "x": 420, "y": 40, "width": 90, "height": 30 } },
            "resources": { "appearance": { "payload": 0 } } }),
        vec![f.png.clone()],
    );
    assert!(stamp.is_ok(), "{stamp:?}");
    assert_eq!(images_in(&doc.download()), 1);
}

/// Bytes that are no PNG, JPEG or PDF reach a Rust host's change unchecked
/// (upstream refuses them on the main thread, `resolvePictureBytes`): the
/// write refuses them too, before anything is written.
#[test]
fn bytes_that_are_no_picture_are_refused_by_the_write_too() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf);
    let refused =
        doc.picture("layout_1", Some(b"GIF89a: not a format a picture is read from")).unwrap_err();
    assert_eq!(refused["code"], "InvalidArg", "{refused}");
    assert_eq!(doc.field("layout_1")["hasImage"], false);
}

#[test]
fn an_undo_takes_the_picture_out_and_its_undo_puts_it_back_drawn() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf);
    let (answer, _) = doc.send(
        json!({ "kind": "document.apply", "opId": "picture-1", "change": { "ops": [
            { "type": "forms.setButtonImage", "field": fqn("layout_1"),
              "image": { "payload": 0 } }] } }),
        vec![f.png.clone()],
    );
    let events = answer["result"]["events"].as_array().unwrap_or_else(|| panic!("{answer}"));
    assert_eq!(events.len(), 1, "{answer}");
    assert_eq!(events[0]["type"], "forms.updated");
    let undo = doc.apply(json!({ "undoOf": "picture-1" }), Vec::new()).unwrap();
    assert_eq!(undo["items"][0]["type"], "forms.setButtonImage");
    assert_eq!(doc.field("layout_1")["hasImage"], false);
    doc.apply(json!({ "undoOf": undo["meta"]["opId"] }), Vec::new()).unwrap();
    assert_eq!(doc.field("layout_1")["hasImage"], true);
    assert!(doc.all_show_bands("layout_1"));
}

#[test]
fn a_field_a_signature_locked_takes_no_picture() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf);
    let prepared = doc.ok(json!({ "kind": "signatures.prepare",
        "input": { "field": fqn("buyer_sig") } }));
    let result = &prepared["result"];
    let (completed, _) = doc.send(
        json!({ "kind": "signatures.complete",
            "input": { "signingId": result["signingId"],
                "expectedVersion": result["expectedVersion"] } }),
        vec![PLACEHOLDER_CMS.to_vec()],
    );
    assert_eq!(completed["result"]["result"]["status"], "completed", "{completed}");
    // The signature locks its group: the buyer's initials, not the seller's.
    let refused = doc.picture("buyer_initials", Some(&f.png)).unwrap_err();
    assert_eq!(refused["code"], "ProtectedDocument", "{refused}");
    assert!(doc.picture("seller_initials", Some(&f.png)).is_ok());
}

#[test]
fn a_save_keeps_the_picture_and_the_file_reads_it_again() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf);
    doc.picture("layout_1", Some(&f.png)).unwrap();
    let saved = doc.download();
    let mut reader = Doc::open_as(saved, Some(&["doc.open", "doc.render", "doc.forms.read"]));
    assert_eq!(reader.field("layout_1")["hasImage"], true);
    assert_eq!(reader.layouts_of("layout_1"), [json!("image")]);
    assert!(reader.all_show_bands("layout_1"));
}

#[test]
fn a_place_added_later_shows_the_fields_picture_and_the_file_holds_it_once() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf);
    doc.button("initials", &["image"], 0);
    doc.picture("initials", Some(&f.png)).unwrap();
    let added = doc.add_widget(
        "initials",
        json!({ "page": page(), "box": free_box(1), "layout": "image" }),
        "add-1",
    );
    assert_eq!(added["field"]["hasImage"], true);
    assert_eq!(doc.layouts_of("initials"), [json!("image"), json!("image")]);
    let rows = doc.rows_of("initials");
    assert!(doc.drawn(&rows[1]).shows_bands());
    assert_eq!(images_in(&doc.download()), 1);
}

#[test]
fn a_place_added_without_a_layout_keeps_the_picture_and_shows_its_caption() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf);
    doc.button("initials", &["image"], 0);
    doc.picture("initials", Some(&f.png)).unwrap();
    doc.add_widget("initials", json!({ "page": page(), "box": free_box(1) }), "add-1");
    let added = doc.rows_of("initials")[1].clone();
    assert_eq!(added["layout"], "caption");
    assert!(!doc.drawn(&added).shows_any_band());
    // It holds the picture: a layout that shows one shows it.
    doc.ok(json!({ "kind": "forms.updateWidget", "widget": added["ref"],
        "patch": { "layout": "image" } }));
    let shown = doc.rows_of("initials")[1].clone();
    assert!(doc.drawn(&shown).shows_bands());
}

#[test]
fn undoing_the_added_place_takes_it_away_and_the_picture_stays_in_the_others() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf);
    doc.button("initials", &["image"], 0);
    doc.picture("initials", Some(&f.png)).unwrap();
    doc.add_widget(
        "initials",
        json!({ "page": page(), "box": free_box(1), "layout": "image" }),
        "place-2",
    );
    doc.apply(json!({ "undoOf": "place-2" }), Vec::new()).unwrap();
    assert_eq!(doc.field("initials")["hasImage"], true);
    let rows = doc.rows_of("initials");
    assert_eq!(rows.len(), 1);
    assert!(doc.drawn(&rows[0]).shows_bands());
}

#[test]
fn a_field_and_widget_in_one_object_split_for_a_new_place_and_both_show_the_picture() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf);
    doc.picture("layout_1", Some(&f.png)).unwrap();
    doc.add_widget(
        "layout_1",
        json!({ "page": page(), "box": free_box(1), "layout": "image" }),
        "add-1",
    );
    assert_eq!(doc.rows_of("layout_1").len(), 2);
    assert!(doc.all_show_bands("layout_1"));
    assert_eq!(images_in(&doc.download()), 1);
}

#[test]
fn a_copy_keeps_the_picture_with_its_value_and_its_layout_always() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf);
    let field = doc.button("initials", &["image"], 0);
    doc.picture("initials", Some(&f.png)).unwrap();
    let copy = |offset: f64, values: bool| {
        json!({ "ops": [{ "type": "forms.copy", "fields": [field["ref"]],
            "offset": { "x": 0, "y": offset }, "values": values }] })
    };
    let copied = doc.apply(copy(40.0, true), Vec::new()).unwrap()["items"][0].clone();
    assert_eq!(names_and_pictures(&copied["fields"]), [("initials_2".into(), json!(true))]);
    let layouts: Vec<&Value> =
        copied["widgets"].as_array().unwrap().iter().map(|w| &w["layout"]).collect();
    assert_eq!(layouts, [&json!("image")]);
    assert!(doc.all_show_bands("initials_2"));
    let plain = doc.apply(copy(80.0, false), Vec::new()).unwrap()["items"][0].clone();
    assert_eq!(names_and_pictures(&plain["fields"]), [("initials_3".into(), json!(false))]);
    let layouts: Vec<&Value> =
        plain["widgets"].as_array().unwrap().iter().map(|w| &w["layout"]).collect();
    assert_eq!(layouts, [&json!("image")]);
    assert_eq!(images_in(&doc.download()), 1);
}

#[test]
fn an_export_carries_the_picture_once_and_an_import_places_it() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf.clone());
    let field = doc.button("initials", &["image", "caption-below"], 0);
    doc.picture("initials", Some(&f.png)).unwrap();
    let exported = doc.export(json!([field["ref"]]));
    let (bundle, _) = &exported;
    let ids: Vec<&Value> = bundle["widgets"]
        .as_array()
        .unwrap()
        .iter()
        .map(|row| &row["resources"]["image"])
        .collect();
    assert_eq!(ids.len(), 2);
    let id = ids[0].as_str().expect("an image id");
    assert!(id.starts_with("sha256-"), "{id}");
    assert_eq!(ids[1], ids[0]);
    let keys: Vec<&String> = bundle["resources"].as_object().unwrap().keys().collect();
    assert_eq!(keys, [id]);
    let layouts: Vec<&Value> =
        bundle["widgets"].as_array().unwrap().iter().map(|row| &row["data"]["layout"]).collect();
    assert_eq!(layouts, [&json!("image"), &json!("caption-below")]);

    let mut other = Doc::open(f.pdf.clone());
    let imported = other.import(&exported, true);
    assert_eq!(imported["dropped"], json!([]));
    assert_eq!(names_and_pictures(&imported["fields"]), [("initials".into(), json!(true))]);
    let layouts: Vec<&Value> =
        imported["widgets"].as_array().unwrap().iter().map(|w| &w["layout"]).collect();
    assert_eq!(layouts, [&json!("image"), &json!("caption-below")]);
    let icon = other.rows_of("initials")[0].clone();
    assert!(other.drawn(&icon).shows_bands());
    // The same bundle back.
    let (again, _) = other.export(json!([fqn("initials")]));
    let resources = |bundle: &Value| -> Vec<Value> {
        bundle["widgets"].as_array().unwrap().iter().map(|row| row["resources"].clone()).collect()
    };
    assert_eq!(resources(&again), resources(bundle));

    let mut without = Doc::open(f.pdf);
    let imported = without.import(&exported, false);
    assert_eq!(names_and_pictures(&imported["fields"]), [("initials".into(), json!(false))]);
}

#[test]
fn a_field_the_form_doesnt_export_goes_without_its_picture_as_without_a_value() {
    let f = fixtures();
    let mut doc = Doc::open(f.pdf.clone());
    let field = doc.ok(json!({ "kind": "forms.createField", "draft": { "kind": "pushbutton",
        "name": "photo", "noExport": true,
        "widgets": [{ "page": page(), "box": free_box(0), "layout": "image" }] } }))["result"]
        ["field"]
        .clone();
    doc.picture("photo", Some(&f.png)).unwrap();
    assert_eq!(doc.field("photo")["hasImage"], true);
    let exported = doc.export(json!([field["ref"]]));
    let (bundle, _) = &exported;
    let fields: Vec<(String, Value)> = bundle["fields"]
        .as_array()
        .unwrap()
        .iter()
        .map(|row| {
            (row["data"]["name"].as_str().unwrap().to_owned(), row["data"]["hasImage"].clone())
        })
        .collect();
    assert_eq!(fields, [("photo".into(), json!(false))]);
    let widgets: Vec<(Value, Value)> = bundle["widgets"]
        .as_array()
        .unwrap()
        .iter()
        .map(|row| (row["data"]["layout"].clone(), row["resources"].clone()))
        .collect();
    assert_eq!(widgets, [(json!("image"), json!({}))]);
    assert_eq!(bundle["resources"], json!({}));
    let mut other = Doc::open(f.pdf);
    let imported = other.import(&exported, true);
    assert_eq!(names_and_pictures(&imported["fields"]), [("photo".into(), json!(false))]);
}

// ── `picture-button-analysis.test.ts`: an image set after another signature ──

/// A copy of the fixture, written whole, signed in `sig_field` (approved,
/// or certified for form filling) after a push button and a signature field
/// of the engine's own were made: its field and widget apart.
fn signed_copy(f: &Fixtures, certify: bool) -> Doc {
    let base = {
        let mut doc = Doc::open(f.pdf.clone());
        let (answer, mut payloads) =
            doc.send(json!({ "kind": "document.saveBuffer", "mode": "rewrite" }), Vec::new());
        assert!(answer.get("error").is_none(), "{answer}");
        payloads.remove(0)
    };
    let mut doc = Doc::open(base);
    doc.ok(json!({ "kind": "forms.createField", "draft": { "kind": "pushbutton",
        "name": "made_initials", "widgets": [{ "page": page(),
        "box": { "x": 300, "y": 40, "width": 90, "height": 30 }, "layout": "image" }] } }));
    doc.ok(json!({ "kind": "forms.createField", "draft": { "kind": "signature",
        "name": "second_signature", "widgets": [{ "page": page(),
        "box": { "x": 300, "y": 100, "width": 90, "height": 30 } }] } }));
    let mut input = json!({ "field": fqn("sig_field") });
    if certify {
        input["certify"] = json!({ "permission": 2 });
    }
    let prepared = doc.ok(json!({ "kind": "signatures.prepare", "input": input }));
    let result = &prepared["result"];
    let (completed, _) = doc.send(
        json!({ "kind": "signatures.complete",
            "input": { "signingId": result["signingId"],
                "expectedVersion": result["expectedVersion"] } }),
        vec![PLACEHOLDER_CMS.to_vec()],
    );
    assert_eq!(completed["result"]["result"]["status"], "completed", "{completed}");
    doc
}

/// The verdict on the saved file since the signature that seals a
/// revision, and the rule (and edge) of each finding that isn't permitted.
fn judged(doc: &mut Doc) -> (Value, Vec<String>) {
    let bytes = doc.download();
    let mut reopened = Doc::open(bytes);
    let signatures = reopened.ok(json!({ "kind": "signatures.list" }))["snapshot"]["signatures"]
        .as_array()
        .unwrap()
        .clone();
    let index = signatures
        .iter()
        .position(|s| s["coverage"] == "whole-revision")
        .expect("a signature that seals a revision");
    let analysis = reopened.ok(json!({ "kind": "signatures.analyze",
        "input": { "since": { "signatureIndex": index } } }))["analysis"]
        .clone();
    let not_permitted = analysis["steps"]
        .as_array()
        .unwrap()
        .iter()
        .flat_map(|step| step["findings"].as_array().unwrap().iter())
        .filter(|finding| finding["verdict"] != "permitted")
        .map(|finding| match finding["edge"].as_str() {
            Some(edge) => format!("{} {edge}", finding["rule"].as_str().unwrap()),
            None => finding["rule"].as_str().unwrap().to_owned(),
        })
        .collect();
    (analysis["verdict"].clone(), not_permitted)
}

/// An image in a push button after an approval signature and after a
/// certification for form filling, in the standard's image field (field and
/// widget one) and in a button the engine made (apart), is filling in; so is
/// a mark drawn into an unsigned signature field, and the control, a value
/// typed into a text field under P=2.
#[test]
fn an_image_set_after_another_signature_is_filling_in_as_the_analysis_judges_it() {
    let f = fixtures();
    for certify in [false, true] {
        let level = if certify { "certified for form filling" } else { "approved" };
        for name in ["layout_1", "made_initials"] {
            let mut doc = signed_copy(&f, certify);
            doc.picture(name, Some(&f.png)).unwrap();
            let (verdict, not_permitted) = judged(&mut doc);
            assert_eq!(
                (verdict, not_permitted),
                (json!("permitted"), Vec::<String>::new()),
                "{level}: {name}"
            );
        }
        let mut doc = signed_copy(&f, certify);
        let (answer, _) = doc.send(
            json!({ "kind": "forms.setSignatureAppearance", "ref": fqn("second_signature"),
                "appearance": { "payload": 0 } }),
            vec![f.png.clone()],
        );
        assert!(answer.get("error").is_none(), "{answer}");
        assert_eq!(judged(&mut doc), (json!("permitted"), Vec::<String>::new()), "{level}: a mark");
    }
    let mut doc = signed_copy(&f, true);
    doc.ok(json!({ "kind": "forms.setValue", "ref": fqn("text_field"),
        "value": { "value": "filled" } }));
    assert_eq!(judged(&mut doc), (json!("permitted"), Vec::<String>::new()), "the control");
}

// ── `shared-drawing-size.test.ts`: one picture, many places, stored once ──

/// `pdfOf`: a PDF of `objects`, numbered from 1.
fn pdf_of(objects: &[&str]) -> Vec<u8> {
    let mut out = b"%PDF-1.7\n".to_vec();
    let mut offsets = Vec::new();
    for (i, object) in objects.iter().enumerate() {
        offsets.push(out.len());
        out.extend(format!("{} 0 obj\n{object}\nendobj\n", i + 1).as_bytes());
    }
    let xref = out.len();
    out.extend(format!("xref\n0 {}\n0000000000 65535 f \n", objects.len() + 1).as_bytes());
    for offset in offsets {
        out.extend(format!("{offset:010} 00000 n \n").as_bytes());
    }
    out.extend(
        format!(
            "trailer\n<< /Size {} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n",
            objects.len() + 1
        )
        .as_bytes(),
    );
    out
}

/// An RGB PNG of noise, which no compression shrinks: a picture whose
/// every copy in a file would show in its size. The noise is upstream's
/// (`noisyPng`); its pixels go in stored deflate blocks, which noise needs
/// anyway, so the file is a little larger than upstream's.
fn noisy_png(width: u32, height: u32) -> Vec<u8> {
    fn crc32(bytes: &[u8]) -> u32 {
        let mut c = 0xffff_ffffu32;
        for b in bytes {
            c ^= u32::from(*b);
            for _ in 0..8 {
                c = if c & 1 == 1 { 0xedb8_8320 ^ (c >> 1) } else { c >> 1 };
            }
        }
        c ^ 0xffff_ffff
    }
    fn chunk(kind: &[u8; 4], data: &[u8]) -> Vec<u8> {
        let mut out = (data.len() as u32).to_be_bytes().to_vec();
        out.extend(kind);
        out.extend(data);
        out.extend(crc32(&out[4..]).to_be_bytes());
        out
    }
    let mut ihdr = width.to_be_bytes().to_vec();
    ihdr.extend(height.to_be_bytes());
    ihdr.extend([8, 2, 0, 0, 0]);
    let mut seed: u32 = 7;
    let mut raw = Vec::new();
    for _ in 0..height {
        // Each row: no filter, then its bytes.
        raw.extend(std::iter::once(0).chain((0..width * 3).map(|_| {
            seed = seed.wrapping_mul(1_103_515_245).wrapping_add(12345);
            (seed >> 24) as u8
        })));
    }
    // zlib: stored blocks, then the Adler-32 of the data.
    let mut zlib = vec![0x78, 0x01];
    let blocks: Vec<&[u8]> = raw.chunks(65_535).collect();
    for (at, block) in blocks.iter().enumerate() {
        zlib.push(u8::from(at + 1 == blocks.len()));
        let len = block.len() as u16;
        zlib.extend(len.to_le_bytes());
        zlib.extend((!len).to_le_bytes());
        zlib.extend(*block);
    }
    let (mut a, mut b) = (1u32, 0u32);
    for byte in &raw {
        a = (a + u32::from(*byte)) % 65_521;
        b = (b + a) % 65_521;
    }
    zlib.extend(((b << 16) | a).to_be_bytes());
    let mut png = vec![0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
    png.extend(chunk(b"IHDR", &ihdr));
    png.extend(chunk(b"IDAT", &zlib));
    png.extend(chunk(b"IEND", &[]));
    png
}

/// Ten boxes on an A4 page, two a row.
fn size_box(at: u32) -> Value {
    json!({ "x": 40 + (at % 2) * 260, "y": 40 + (at / 2) * 70, "width": 200, "height": 60 })
}

#[derive(Clone, Copy, Debug)]
enum Place {
    Mark,
    Button,
    Stamp,
}

/// The file `count` places of `picture` leave, as `place` puts each in.
fn saved_with(place: Place, picture: &[u8], count: u32) -> Vec<u8> {
    let page_pdf = pdf_of(&[
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << >> >>",
        "<< /Length 0 >>\nstream\n\nendstream",
    ]);
    let mut doc = Doc::open(page_pdf);
    for at in 0..count {
        match place {
            Place::Mark => {
                let name = format!("signature_{at}");
                doc.ok(json!({ "kind": "forms.createField", "draft": { "kind": "signature",
                    "name": name, "widgets": [{ "page": page(), "box": size_box(at) }] } }));
                let (answer, _) = doc.send(
                    json!({ "kind": "forms.setSignatureAppearance", "ref": fqn(&name),
                        "appearance": { "payload": 0 } }),
                    vec![picture.to_vec()],
                );
                assert!(answer.get("error").is_none(), "{answer}");
            }
            Place::Button => {
                let name = format!("initials_{at}");
                doc.ok(json!({ "kind": "forms.createField", "draft": { "kind": "pushbutton",
                    "name": name,
                    "widgets": [{ "page": page(), "box": size_box(at), "layout": "image" }] } }));
                doc.picture(&name, Some(picture)).unwrap();
            }
            Place::Stamp => {
                let made = doc.call(
                    json!({ "kind": "annotations.create", "page": page(),
                        "draft": { "subtype": "stamp", "box": size_box(at) },
                        "resources": { "appearance": { "payload": 0 } } }),
                    vec![picture.to_vec()],
                );
                assert!(made.is_ok(), "{made:?}");
            }
        }
    }
    doc.download()
}

#[test]
fn ten_places_of_one_picture_save_it_once_and_little_more_than_one_place_does() {
    let picture = noisy_png(80, 70);
    // Upstream's is about 17 KB, deflated; stored, this one is a little more.
    assert!((16_000..18_000).contains(&picture.len()), "{}", picture.len());
    for place in [Place::Mark, Place::Button, Place::Stamp] {
        let one = saved_with(place, &picture, 1);
        let ten = saved_with(place, &picture, 10);
        assert_eq!(images_in(&one), 1, "{place:?}");
        assert_eq!(images_in(&ten), 1, "{place:?}");
        // Nine more places add their boxes and placements, never the
        // picture again.
        assert!(
            ten.len() - one.len() < picture.len(),
            "{place:?}: {} then {}",
            one.len(),
            ten.len()
        );
    }
}
