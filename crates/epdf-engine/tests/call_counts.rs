//! PDFium call counts for the host calls a viewer makes most, on a 400-page
//! document with text, highlights and a text field on every page.
//!
//! A measurement, not a check: it prints the counts so two builds can be
//! compared, and asserts only that each call succeeds. Run it with
//! `cargo test -p epdf-engine --test call_counts -- --ignored --nocapture`.
//!
//! The counters are thread-local, and the host makes its PDFium calls on the
//! thread that calls it, so this thread's counters see every call.

use epdf_engine::host::Host;
use pdfium::call_count::{self, Call};
use serde_json::{Value, json};

const PAGES: usize = 400;
const LINES: usize = 40;
const HIGHLIGHTS: usize = 3;
/// The page (0-based) whose highlight and field the writes change.
const TARGET: usize = 200;
const DOC: &str = "counts";

/// The counted calls, in the order they print.
const CALLS: [(&str, Call); 6] = [
    ("LoadPage", Call::LoadPage),
    ("FormLoadModel", Call::FormLoadModel),
    ("GetAnnotRaw", Call::GetAnnotRaw),
    ("GetAnnotIndexRaw", Call::GetAnnotIndexRaw),
    ("TextLoadPage", Call::TextLoadPage),
    ("SigLoadModel", Call::SigLoadModel),
];

/// The generated file and the object numbers the requests name.
struct Fixture {
    bytes: Vec<u8>,
    pages: Vec<u32>,
    highlights: Vec<Vec<u32>>,
    fields: Vec<u32>,
}

fn line(page: usize, n: usize) -> String {
    format!("Page {page} line {n}: the quick fox and the lazy dog")
}

fn add(objects: &mut Vec<String>, body: String) -> u32 {
    objects.push(body);
    objects.len() as u32
}

fn stream(objects: &mut Vec<String>, dict: &str, body: &str) -> u32 {
    add(objects, format!("<< {dict}/Length {} >>\nstream\n{body}endstream", body.len()))
}

fn refs(numbers: &[u32]) -> String {
    numbers.iter().map(|n| format!("{n} 0 R")).collect::<Vec<_>>().join(" ")
}

/// Hand-written objects: 40 lines of Courier text a page, three highlights
/// on lines 1, 5 and 9, and one text field with its own appearance stream.
fn build() -> Fixture {
    // The catalog and the page tree come first and are written last.
    let mut objects = vec![String::new(), String::new()];
    let courier = add(&mut objects, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>".into());
    let helv = add(
        &mut objects,
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>".into(),
    );
    let (mut pages, mut highlights, mut fields) = (Vec::new(), Vec::new(), Vec::new());
    for p in 1..=PAGES {
        let page = add(&mut objects, String::new());
        let mut text = String::from("BT /F1 11 Tf 13 TL 54 730 Td\n");
        for n in 1..=LINES {
            text += &format!("({}) Tj T*\n", line(p, n));
        }
        text += "ET\n";
        let content = stream(&mut objects, "", &text);
        let mut on_page = Vec::new();
        for i in 0..HIGHLIGHTS {
            let n = 1 + 4 * i;
            let base = 730 - 13 * (n as i64 - 1);
            let (y1, y2) = (base - 3, base + 9);
            let x2 = 54.0 + 6.6 * line(p, n).len() as f64;
            on_page.push(add(
                &mut objects,
                format!(
                    "<< /Type /Annot /Subtype /Highlight /Rect [54 {y1} {x2:.2} {y2}] \
                     /QuadPoints [54 {y2} {x2:.2} {y2} 54 {y1} {x2:.2} {y1}] /C [1 0.9 0] \
                     /F 4 /NM (hl-{p}-{n}) /T (counts) >>"
                ),
            ));
        }
        let look = stream(
            &mut objects,
            "/Type /XObject /Subtype /Form /BBox [0 0 240 20] ",
            "q 0.5 G 1 w 0.5 0.5 239 19 re S Q\n",
        );
        let field = add(
            &mut objects,
            format!(
                "<< /Type /Annot /Subtype /Widget /FT /Tx /T (f{p}) /V () /DA (/Helv 10 Tf 0 g) \
                 /Rect [320 40 560 60] /P {page} 0 R /F 4 /AP << /N {look} 0 R >> >>"
            ),
        );
        let annots = [on_page.as_slice(), &[field]].concat();
        objects[page as usize - 1] = format!(
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] \
             /Resources << /Font << /F1 {courier} 0 R >> >> /Contents {content} 0 R \
             /Annots [{}] >>",
            refs(&annots)
        );
        pages.push(page);
        highlights.push(on_page);
        fields.push(field);
    }
    objects[0] = format!(
        "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [{}] /DA (/Helv 10 Tf 0 g) \
         /DR << /Font << /Helv {helv} 0 R >> >> >> >>",
        refs(&fields)
    );
    objects[1] = format!("<< /Type /Pages /Count {PAGES} /Kids [{}] >>", refs(&pages));

    let mut pdf = String::from("%PDF-1.4\n");
    let mut offsets = Vec::new();
    for (i, object) in objects.iter().enumerate() {
        offsets.push(pdf.len());
        pdf += &format!("{} 0 obj\n{object}\nendobj\n", i + 1);
    }
    let xref = pdf.len();
    pdf += &format!("xref\n0 {}\n0000000000 65535 f \n", objects.len() + 1);
    for offset in offsets {
        pdf += &format!("{offset:010} 00000 n \n");
    }
    pdf += &format!(
        "trailer\n<< /Size {} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n",
        objects.len() + 1
    );
    Fixture { bytes: pdf.into_bytes(), pages, highlights, fields }
}

/// Send `request` and answer its `result`, failing on an error envelope.
fn send(host: &mut Host, request: &Value, payloads: Vec<Vec<u8>>) -> Value {
    let response = host.call_filled(&request.to_string(), payloads);
    let mut envelope: Value = serde_json::from_str(&response.json).expect("every response is JSON");
    assert!(envelope.get("error").is_none(), "{request} failed: {envelope}");
    envelope["result"].take()
}

/// Print what the calls since the last [`call_count::reset`] cost.
fn print(label: &str) {
    let counts: Vec<String> =
        CALLS.iter().map(|(name, call)| format!("{name}={}", call_count::get(*call))).collect();
    println!("{label:<24} {}", counts.join(" "));
}

/// Send `request` with counters zeroed, print what it cost, and answer its
/// `result`.
fn measure(host: &mut Host, label: &str, request: Value, payloads: Vec<Vec<u8>>) -> Value {
    call_count::reset();
    let result = send(host, &request, payloads);
    print(label);
    result
}

/// Search the whole document for `text`, following the cursor from slice to
/// slice as a viewer does, with counters zeroed once for the whole chain.
/// Answers how many slices it took and how many matches they found.
fn measure_search(host: &mut Host, label: &str, text: &str) -> (usize, usize) {
    call_count::reset();
    let (mut slices, mut matches, mut cursor) = (0, 0, Value::Null);
    loop {
        let mut request = json!({ "text": text, "limit": { "pages": PAGES, "matches": 1_000 } });
        if !cursor.is_null() {
            request["cursor"] = cursor;
        }
        let mut slice = send(
            host,
            &json!({ "kind": "search.query", "docId": DOC, "request": request }),
            vec![],
        );
        slices += 1;
        matches += slice["matches"].as_array().map_or(0, Vec::len);
        cursor = slice["nextCursor"].take();
        if cursor.is_null() {
            assert_eq!(slice["pagesSearched"], PAGES, "{label}: {slice}");
            break;
        }
    }
    print(label);
    (slices, matches)
}

fn page_ref(page: u32) -> Value {
    json!({ "kind": "objectNumber", "objectNumber": page })
}

#[test]
#[ignore = "a measurement: run it with --ignored --nocapture"]
fn hot_host_calls() {
    let fixture = build();
    let mut host = Host::new();
    println!("{PAGES} pages, {} bytes", fixture.bytes.len());

    measure(
        &mut host,
        "open.fatMem",
        json!({ "kind": "open.fatMem", "docId": DOC, "password": null }),
        vec![fixture.bytes.clone()],
    );
    let listed =
        measure(&mut host, "pages.list", json!({ "kind": "pages.list", "docId": DOC }), vec![]);
    assert_eq!(listed["pages"].as_array().map(Vec::len), Some(PAGES), "{listed}");

    // A needle no page holds reads every page's text and loads no page for
    // match geometry; one that holds on a single page adds that page's.
    for (label, text, want) in [
        ("search.query (miss)", "zqxjv", 0),
        ("search.query (repeat)", "zqxjv", 0),
        ("search.query (1 page)", "Page 200 line", LINES),
    ] {
        let (slices, matches) = measure_search(&mut host, label, text);
        assert_eq!(matches, want, "{label} found {matches} matches");
        println!("{:<24} {slices} slices, {matches} matches", "");
    }

    measure(
        &mut host,
        "forms.setValue",
        json!({ "kind": "forms.setValue", "docId": DOC,
            "ref": { "kind": "objectNumber", "objectNumber": fixture.fields[TARGET] },
            "value": { "value": "Grace Hopper" } }),
        vec![],
    );
    measure(
        &mut host,
        "annotations.update",
        json!({ "kind": "annotations.update", "docId": DOC,
            "ref": { "kind": "objectNumber", "page": page_ref(fixture.pages[TARGET]),
                     "objectNumber": fixture.highlights[TARGET][1] },
            "patch": { "contents": "counted" } }),
        vec![],
    );
}
