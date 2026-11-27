//! What a signature field's mark looks like (`runSignatureMarkConformance.ts`
//! and the mark tests of `runWidgetRotationConformance.ts`, fcb69611f and
//! c76603e3f): the pixels `vectors/changes/signature-mark.json` and
//! `widget-rotation.json` don't record. A PNG and a JPEG fill a field with
//! their picture, fitted whole; a mark turns with its frame; a turn of a
//! marked widget turns the mark and undo puts it back; no mark takes it out.
//!
//! The marks are the generator's: the bytes recorded in the vector's
//! `fixtures`.

use epdf_engine::host::Host;
use serde_json::{Value, json};

const DOC: &str = "marks";
/// A DER `SEQUENCE`: what a completion takes as a CMS, without judging it.
const PLACEHOLDER_CMS: [u8; 8] = [0x30, 0x06, 0x02, 0x01, 0x01, 0x02, 0x01, 0x02];
/// Bytes that are no PNG, JPEG or PDF (`runSignatureMarkConformance.ts`).
const NOT_A_MARK: &[u8] = b"GIF89a: not a format a mark is read from";

fn unhex(text: &str) -> Vec<u8> {
    (0..text.len()).step_by(2).map(|i| u8::from_str_radix(&text[i..i + 2], 16).unwrap()).collect()
}

/// The vector's page fixture and its marks.
struct Fixtures {
    page: Vec<u8>,
    png: Vec<u8>,
    jpeg: Vec<u8>,
    pdf: Vec<u8>,
    mark_pdf: Vec<u8>,
}

fn fixtures() -> Fixtures {
    let path = test_support::corpus::vectors_dir("changes").join("signature-mark.json");
    let vectors: Value = serde_json::from_str(&std::fs::read_to_string(path).unwrap()).unwrap();
    let mark = |key: &str| unhex(vectors["fixtures"][key].as_str().expect(key));
    Fixtures {
        page: unhex(vectors["fixtureHex"].as_str().unwrap()),
        png: mark("png"),
        jpeg: mark("jpeg"),
        pdf: mark("pdf"),
        mark_pdf: mark("markPdf"),
    }
}

struct Doc {
    host: Host,
    job: u64,
}

impl Doc {
    fn open(bytes: Vec<u8>) -> Self {
        let mut doc = Doc { host: Host::new(), job: 1 };
        let opened = doc.send(
            json!({ "kind": "open.fatMem", "effect": "open", "docId": DOC, "password": null,
                "reserveObjectNumbers": 64 }),
            vec![bytes],
        );
        assert!(opened.0.get("error").is_none(), "{}", opened.0);
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
        let response = self.host.call_filled(&request.to_string(), payloads);
        (serde_json::from_str(&response.json).unwrap(), response.payloads)
    }

    /// The payload a request answered, which must not be an error.
    fn ok(&mut self, request: Value, payloads: Vec<Vec<u8>>) -> Value {
        let (answer, _) = self.send(request, payloads);
        assert!(answer.get("error").is_none(), "{answer}");
        answer["result"].clone()
    }

    /// A new signature field with one box at `box`, turned `rotation`.
    fn signature_field(&mut self, name: &str, frame: Value, rotation: Option<u32>) {
        let mut placement = json!({ "page": page(), "box": frame });
        if let Some(rotation) = rotation {
            placement["rotation"] = json!(rotation);
        }
        self.ok(
            json!({ "kind": "forms.createField", "opId": format!("create-{name}"),
                "draft": { "kind": "signature", "name": name, "widgets": [placement] } }),
            Vec::new(),
        );
    }

    /// `forms.setSignatureAppearance` as `LocalDocumentFormsService` sends a
    /// mark: its bytes as `appearance`, `{ pdf }` as `pdf`, none as `null`.
    fn mark(&mut self, name: &str, mark: Mark<'_>, op_id: &str) -> Value {
        let mut request = json!({ "kind": "forms.setSignatureAppearance", "opId": op_id,
            "ref": { "kind": "fqn", "name": name } });
        let payloads = match mark {
            Mark::Bytes(bytes) => {
                request["appearance"] = json!({ "payload": 0 });
                vec![bytes.to_vec()]
            }
            Mark::Pdf(bytes) => {
                request["pdf"] = json!({ "payload": 0 });
                vec![bytes.to_vec()]
            }
            Mark::None => {
                request["appearance"] = Value::Null;
                Vec::new()
            }
        };
        self.send(request, payloads).0
    }

    /// The widget row of field `name`'s first widget.
    fn row_of(&mut self, name: &str) -> Value {
        let form = self.ok(json!({ "kind": "forms.list" }), Vec::new());
        let snapshot = &form;
        let field = snapshot["fields"]
            .as_array()
            .unwrap()
            .iter()
            .find(|f| f["name"] == name)
            .unwrap_or_else(|| panic!("no field {name}"));
        let reference = &field["widgets"][0]["ref"];
        snapshot["widgets"]
            .as_array()
            .unwrap()
            .iter()
            .find(|row| &row["ref"] == reference)
            .cloned()
            .expect("the widget's row")
    }

    /// `signatures.prepare` of field `name` with `mark` as its appearance,
    /// as `LocalDocumentSignaturesService` sends it: the envelope.
    fn prepare(&mut self, name: &str, mark: Mark<'_>) -> Value {
        let mut input = json!({ "field": { "kind": "fqn", "name": name },
            "signer": { "name": "Signer" } });
        let payloads = match mark {
            Mark::Bytes(bytes) => {
                input["appearance"] = json!({ "payload": 0 });
                vec![bytes.to_vec()]
            }
            Mark::Pdf(bytes) => {
                input["appearance"] = json!({ "pdf": { "payload": 0 } });
                vec![bytes.to_vec()]
            }
            Mark::None => Vec::new(),
        };
        self.send(json!({ "kind": "signatures.prepare", "input": input }), payloads).0
    }

    /// Prepare and complete a signing of field `name` with `mark`, the CMS a
    /// placeholder DER `SEQUENCE` that a completion takes without judging.
    fn sign(&mut self, name: &str, mark: Mark<'_>) {
        let prepared = self.prepare(name, mark);
        assert!(prepared.get("error").is_none(), "{prepared}");
        let result = &prepared["result"]["result"];
        let completed = self.ok(
            json!({ "kind": "signatures.complete", "opId": format!("sign-{name}"),
                "input": { "signingId": result["signingId"],
                    "expectedVersion": result["expectedVersion"] } }),
            vec![PLACEHOLDER_CMS.to_vec()],
        );
        assert_eq!(completed["result"]["status"], "completed", "{completed}");
    }

    /// The first widget of field `name`, as the page shows it.
    fn drawn_of(&mut self, name: &str) -> Raster {
        let row = self.row_of(name);
        self.drawn(&row)
    }

    /// The widget as the page shows it: the pixels of its normal appearance
    /// at scale 1 (`appearanceRaster(page, ref, 'forms')`).
    fn drawn(&mut self, row: &Value) -> Raster {
        self.rendered(row, "widgets")
    }

    /// An annotation as the page shows it (`appearanceRaster(page, ref)`).
    fn drawn_annotation(&mut self, row: &Value) -> Raster {
        self.rendered(row, "annotations")
    }

    fn rendered(&mut self, row: &Value, family: &str) -> Raster {
        let (answer, payloads) = self.send(
            json!({ "kind": "annotations.renderAppearances", "effect": "read", "page": page(),
                "family": family, "options": { "viewport": { "kind": "scale", "scale": 1 } } }),
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
}

#[derive(Clone, Copy)]
enum Mark<'a> {
    Bytes(&'a [u8]),
    Pdf(&'a [u8]),
    None,
}

fn page() -> Value {
    json!({ "kind": "objectNumber", "objectNumber": 3 })
}

/// `BANDS_BOX`: as wide and as tall as the bands, three to one.
fn bands_box(y: u32) -> Value {
    json!({ "x": 30, "y": y, "width": 150, "height": 50 })
}

struct Raster {
    width: usize,
    height: usize,
    rgba: Vec<u8>,
}

impl Raster {
    /// The pixel at a place, each coordinate a share of its side.
    fn at(&self, x: f64, y: f64) -> [u8; 4] {
        let column = ((self.width as f64 * x) as usize).min(self.width - 1);
        let row = ((self.height as f64 * y) as usize).min(self.height - 1);
        let i = (row * self.width + column) * 4;
        [self.rgba[i], self.rgba[i + 1], self.rgba[i + 2], self.rgba[i + 3]]
    }

    /// Whether the pixel there is `color`, give or take what a JPEG loses.
    fn shows(&self, x: f64, y: f64, color: [u8; 3]) -> bool {
        let [r, g, b, alpha] = self.at(x, y);
        alpha > 200 && [r, g, b].iter().zip(color).all(|(c, want)| c.abs_diff(want) <= 40)
    }

    /// Whether the three bands fill it from side to side at mid height.
    fn shows_bands(&self) -> bool {
        self.shows(1.0 / 6.0, 0.5, [255, 0, 0])
            && self.shows(3.0 / 6.0, 0.5, [0, 255, 0])
            && self.shows(5.0 / 6.0, 0.5, [0, 0, 255])
    }

    /// The raster a quarter turn clockwise (`quarterTurned`).
    fn quarter_turned(&self) -> Raster {
        let mut rgba = vec![0; self.rgba.len()];
        for y in 0..self.height {
            for x in 0..self.width {
                let from = (y * self.width + x) * 4;
                let to = (x * self.height + (self.height - 1 - y)) * 4;
                rgba[to..to + 4].copy_from_slice(&self.rgba[from..from + 4]);
            }
        }
        Raster { width: self.height, height: self.width, rgba }
    }
}

/// Channel `channel` as drawn: color times alpha, or the alpha.
fn drawn_channel(rgba: &[u8], at: usize, channel: usize) -> i32 {
    let alpha = i32::from(rgba[at + 3]);
    if channel == 3 {
        alpha
    } else {
        (f64::from(i32::from(rgba[at + channel]) * alpha) / 255.0).round() as i32
    }
}

/// The largest difference between a pixel of one raster and the closest of
/// the other within one pixel, both ways (`maxShiftedDifference`).
fn max_shifted_difference(a: &Raster, b: &Raster) -> i32 {
    let one_way = |from: &Raster, to: &Raster| {
        let mut max = 0;
        for y in 0..from.height as i64 {
            for x in 0..from.width as i64 {
                let mut best = 255;
                for dy in -1..=1i64 {
                    for dx in -1..=1i64 {
                        let (ny, nx) = (y + dy, x + dx);
                        if ny < 0 || nx < 0 || ny >= to.height as i64 || nx >= to.width as i64 {
                            continue;
                        }
                        let i = ((y * from.width as i64 + x) * 4) as usize;
                        let j = ((ny * to.width as i64 + nx) * 4) as usize;
                        let difference = (0..4)
                            .map(|c| {
                                (drawn_channel(&from.rgba, i, c) - drawn_channel(&to.rgba, j, c))
                                    .abs()
                            })
                            .max()
                            .unwrap();
                        best = best.min(difference);
                    }
                }
                max = max.max(best);
            }
        }
        max
    };
    one_way(a, b).max(one_way(b, a))
}

#[test]
fn a_png_and_a_jpeg_fill_a_signature_field_and_the_field_shows_the_picture() {
    let marks = fixtures();
    for (name, bytes) in [("png", &marks.png), ("jpeg", &marks.jpeg)] {
        let mut doc = Doc::open(marks.page.clone());
        doc.signature_field("sig", bands_box(30), None);
        let filled = doc.mark("sig", Mark::Bytes(bytes), "fill");
        // Nothing is signed: the field has no value, only a mark.
        assert_eq!(
            filled["result"]["result"]["field"]["valueEntry"],
            json!({ "kind": "none" }),
            "{name}"
        );
        let row = doc.row_of("sig");
        assert_eq!(row["hasMark"], true, "{name}");
        assert!(doc.drawn(&row).shows_bands(), "{name}: the bands");
    }
}

#[test]
fn an_image_is_fitted_whole_into_a_box_of_another_shape() {
    let marks = fixtures();
    let mut doc = Doc::open(marks.page.clone());
    // A square box: the bands, three to one, take the middle third of its height.
    doc.signature_field("square", json!({ "x": 30, "y": 30, "width": 150, "height": 150 }), None);
    doc.mark("square", Mark::Bytes(&marks.png), "fill");
    let raster = doc.drawn_of("square");
    assert!(raster.shows_bands());
    assert_eq!(raster.at(0.5, 0.1)[3], 0);
    assert_eq!(raster.at(0.5, 0.9)[3], 0);
}

#[test]
fn a_pdf_under_its_key_draws_what_the_pdf_draws() {
    let marks = fixtures();
    let mut doc = Doc::open(marks.page.clone());
    doc.signature_field("keyed", bands_box(100), None);
    doc.mark("keyed", Mark::Pdf(&marks.pdf), "keyed");
    doc.signature_field("bare", bands_box(170), None);
    doc.mark("bare", Mark::Bytes(&marks.pdf), "bare");
    let (keyed, bare) = (doc.row_of("keyed"), doc.row_of("bare"));
    assert_eq!([&keyed["hasMark"], &bare["hasMark"]], [&json!(true), &json!(true)]);
    // The PDF is two to one: fitted whole, it leaves the box's sides clear.
    let keyed = doc.drawn(&keyed);
    assert!(keyed.shows(0.3, 0.5, [255, 0, 0]) && keyed.shows(0.7, 0.5, [0, 0, 255]));
    assert_eq!(keyed.at(0.05, 0.5)[3], 0);
    assert_eq!(keyed.rgba, doc.drawn(&bare).rgba);
}

#[test]
fn an_image_reads_upright_in_a_turned_field() {
    let marks = fixtures();
    let mut doc = Doc::open(marks.page.clone());
    doc.signature_field("plain", bands_box(30), None);
    doc.mark("plain", Mark::Bytes(&marks.png), "plain");
    let upright = doc.drawn_of("plain");
    doc.signature_field("turned", bands_box(160), Some(90));
    doc.mark("turned", Mark::Bytes(&marks.png), "turned");
    let row = doc.row_of("turned");
    assert_eq!(row["hasMark"], true);
    let raster = doc.drawn(&row);
    assert_eq!((raster.width, raster.height), (upright.height, upright.width));
    assert!(max_shifted_difference(&raster, &upright.quarter_turned()) <= 3);
}

#[test]
fn no_mark_takes_the_mark_out_and_the_field_shows_its_box_again() {
    let marks = fixtures();
    let mut doc = Doc::open(marks.page.clone());
    doc.signature_field("sig", bands_box(30), None);
    let empty = doc.drawn_of("sig");
    doc.mark("sig", Mark::Bytes(&marks.png), "fill");
    assert!(doc.drawn_of("sig").shows_bands());
    let cleared = doc.mark("sig", Mark::None, "clear");
    assert!(cleared.get("error").is_none(), "{cleared}");
    let row = doc.row_of("sig");
    assert_eq!(row["hasMark"], false);
    let shown = doc.drawn(&row);
    assert!(!shown.shows_bands());
    assert_eq!((shown.width, shown.height), (empty.width, empty.height));
    assert!(max_shifted_difference(&shown, &empty) <= 3, "the box as it was before the mark");
}

#[test]
fn a_turn_turns_the_mark_a_new_frame_fits_it_again_and_undo_puts_it_back() {
    let marks = fixtures();
    let mut doc = Doc::open(marks.page.clone());
    let frame = json!({ "x": 150, "y": 20, "width": 120, "height": 40 });
    doc.signature_field("marked", frame, None);
    doc.mark("marked", Mark::Pdf(&marks.mark_pdf), "mark");
    let row = doc.row_of("marked");
    let upright = doc.drawn(&row);

    let turned = doc.ok(
        json!({ "kind": "forms.updateWidget", "opId": "turn", "widget": row["ref"],
            "patch": { "rotation": 90 } }),
        Vec::new(),
    );
    assert_eq!(turned["result"]["appearance"]["changed"], true, "{turned}");
    assert_eq!(turned["result"]["widget"]["hasMark"], true);
    assert!(max_shifted_difference(&doc.drawn(&row), &upright.quarter_turned()) <= 3);

    // Acrobat's Orientation: the frame swaps its sides where the widget
    // stands, and the mark is fitted into it as into a new field's.
    let rect = turned["result"]["widget"]["rect"].clone();
    assert!(rect.is_object(), "{turned}");
    doc.ok(
        json!({ "kind": "forms.updateWidget", "opId": "orient", "widget": row["ref"],
            "patch": { "rotation": null, "box": rect } }),
        Vec::new(),
    );
    let mut fresh_frame = rect;
    fresh_frame["x"] = json!(40);
    doc.signature_field("fresh", fresh_frame, None);
    doc.mark("fresh", Mark::Pdf(&marks.mark_pdf), "fresh");
    let fresh = doc.drawn_of("fresh");
    assert!(max_shifted_difference(&doc.drawn(&row), &fresh) <= 3);

    doc.ok(json!({ "kind": "document.apply", "opId": "undo-orient", "change": { "undoOf": "orient" } }), Vec::new());
    doc.ok(
        json!({ "kind": "document.apply", "opId": "undo-turn", "change": { "undoOf": "turn" } }),
        Vec::new(),
    );
    assert_eq!(doc.row_of("marked")["rotation"], Value::Null);
    assert!(max_shifted_difference(&doc.drawn(&row), &upright) <= 3);
}

/// How many images a full rewrite of the document holds: its streams whose
/// dictionary says `/Subtype /Image` (`images(rewrite(doc))`).
fn images_in_a_rewrite(doc: &mut Doc) -> usize {
    let (answer, payloads) =
        doc.send(json!({ "kind": "document.saveBuffer", "mode": "rewrite" }), Vec::new());
    assert!(answer.get("error").is_none(), "{answer}");
    let text: String = payloads[0].iter().map(|b| *b as char).collect();
    let mut count = 0;
    let mut rest = text.as_str();
    while let Some(at) = rest.find("/Subtype") {
        rest = &rest[at + "/Subtype".len()..];
        let name = rest.trim_start();
        if name.starts_with("/Image") && !name[6..].starts_with(|c: char| c.is_alphanumeric()) {
            count += 1;
        }
    }
    count
}

#[test]
fn a_stamp_of_the_picture_a_mark_shows_places_the_marks_drawing() {
    // `runAnnotationResourceConformance.ts` (fcb69611f): the mark and a stamp
    // of the same PNG are one image in a saved file, and taking the mark out
    // leaves the stamp what they shared.
    let marks = fixtures();
    let mut doc = Doc::open(marks.page.clone());
    doc.signature_field("marked", json!({ "x": 20, "y": 120, "width": 150, "height": 50 }), None);
    let marked = doc.mark("marked", Mark::Bytes(&marks.png), "mark");
    assert!(marked.get("error").is_none(), "{marked}");
    assert_eq!(doc.row_of("marked")["hasMark"], true);
    let created = doc.ok(
        json!({ "kind": "annotations.create", "opId": "stamp", "page": page(),
            "draft": { "subtype": "stamp", "box": { "x": 20, "y": 20, "width": 150, "height": 50 } },
            "resources": { "appearance": { "payload": 0 } } }),
        vec![marks.png.clone()],
    );
    let stamp = created["result"]["annotation"].clone();
    assert_eq!(images_in_a_rewrite(&mut doc), 1);
    let shown = doc.drawn_annotation(&stamp);
    assert!(shown.rgba.chunks(4).any(|pixel| pixel[3] > 200));
    let cleared = doc.mark("marked", Mark::None, "clear");
    assert!(cleared.get("error").is_none(), "{cleared}");
    assert_eq!(doc.row_of("marked")["hasMark"], false);
    let after = doc.drawn_annotation(&stamp);
    assert_eq!((after.width, after.height), (shown.width, shown.height));
    assert!(max_shifted_difference(&after, &shown) <= 3);
    assert_eq!(images_in_a_rewrite(&mut doc), 1);
}

#[test]
fn bytes_that_are_no_png_jpeg_or_pdf_are_refused_and_nothing_is_written() {
    // The native path sends a mark's bytes unsniffed (`epdf-viewer-host`), so
    // this check is the only gate there is.
    let marks = fixtures();
    let mut doc = Doc::open(marks.page.clone());
    doc.signature_field("sig", bands_box(30), None);
    let before = doc.drawn_of("sig");
    let refused = doc.mark("sig", Mark::Bytes(NOT_A_MARK), "unknown");
    assert_eq!(refused["error"]["code"], "InvalidArg", "{refused}");
    let message = refused["error"]["message"].as_str().unwrap();
    assert!(message.contains("PNG, JPEG or one-page PDF bytes"), "{message}");
    let row = doc.row_of("sig");
    assert_eq!(row["hasMark"], false);
    assert_eq!(doc.drawn(&row).rgba, before.rgba);
}

#[test]
fn a_signing_takes_an_image_as_its_mark_and_a_signed_field_keeps_it() {
    let marks = fixtures();
    let mut doc = Doc::open(marks.page.clone());
    doc.signature_field("signed", bands_box(30), None);
    doc.sign("signed", Mark::Bytes(&marks.png));
    let row = doc.row_of("signed");
    assert_eq!(row["hasMark"], true);
    assert!(doc.drawn(&row).shows_bands());
    // Its mark is sealed with the signature: it takes no other, and gives
    // none up.
    for (mark, op) in [(Mark::Bytes(&marks.jpeg), "again"), (Mark::None, "clear")] {
        let refused = doc.mark("signed", mark, op);
        assert_eq!(refused["error"]["code"], "ProtectedDocument", "{op}: {refused}");
    }
    assert!(doc.drawn_of("signed").shows_bands());
}

#[test]
fn a_signing_refuses_bytes_that_are_no_png_jpeg_or_pdf() {
    let marks = fixtures();
    let mut doc = Doc::open(marks.page.clone());
    doc.signature_field("sig", bands_box(30), None);
    let refused = doc.prepare("sig", Mark::Bytes(NOT_A_MARK));
    assert_eq!(refused["error"]["code"], "InvalidArg", "{refused}");
    let message = refused["error"]["message"].as_str().unwrap();
    assert!(message.contains("PNG, JPEG or one-page PDF bytes"), "{message}");
}

#[test]
fn a_signed_fields_mark_turns_with_it_as_well() {
    // `runWidgetRotationConformance.ts`: the mark a signing drew is fitted
    // again, turned, when its widget turns.
    let marks = fixtures();
    let mut doc = Doc::open(marks.page.clone());
    doc.signature_field("signed", json!({ "x": 150, "y": 20, "width": 120, "height": 40 }), None);
    doc.sign("signed", Mark::Pdf(&marks.mark_pdf));
    let row = doc.row_of("signed");
    assert_eq!(row["hasMark"], true);
    let upright = doc.drawn(&row);
    doc.ok(
        json!({ "kind": "forms.updateWidget", "opId": "turn", "widget": row["ref"],
            "patch": { "rotation": 90 } }),
        Vec::new(),
    );
    assert!(max_shifted_difference(&doc.drawn(&row), &upright.quarter_turned()) <= 3);
}
