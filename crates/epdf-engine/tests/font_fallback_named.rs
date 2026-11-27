//! A page's own text in a font the PDF only names, drawn in the registered
//! fallback font that has it (`runDocumentFontsConformance.ts`, 405059fc3,
//! #1159): runtime `7f399f838` draws it, each character in the width the PDF
//! gives it, where `f2ea75972` crashed. The fallback fonts are the ones
//! upstream's engine comes with (`packages/default-fallback-fonts`), added as
//! `LocalEngine` adds them: registered, then fallbacks.

use epdf_engine::host::Host;
use serde_json::{Value, json};

const FONTS: &str = "packages/default-fallback-fonts/fonts";

fn send(host: &mut Host, job: &mut u64, mut request: Value, payloads: Vec<Vec<u8>>) -> Value {
    *job += 1;
    request["jobId"] = json!(*job);
    let response = host.call_filled(&request.to_string(), payloads);
    let answer: Value = serde_json::from_str(&response.json).expect("JSON");
    assert!(answer.get("error").is_none(), "{request}: {answer}");
    answer
}

/// `pdfNamingFont`: one 200 × 100 page that writes `text` at 48 points from
/// (20, 30), each character 600 thousandths wide, in a font the document
/// names but does not embed, simple (TrueType) or composite (Type 0).
fn pdf_naming_font(composite: bool, text: &str) -> Vec<u8> {
    let characters: Vec<char> = text.chars().collect();
    let unicode: Vec<String> = characters.iter().map(|c| format!("{:04X}", *c as u32)).collect();
    let code = |index: usize| {
        if composite { format!("{:04X}", 1 + index) } else { format!("{:02X}", 65 + index) }
    };
    let codes: String = (0..characters.len()).map(code).collect();
    let mut objects: Vec<Vec<u8>> = vec![
        b"<< /Type /Catalog /Pages 2 0 R >>".to_vec(),
        b"<< /Type /Pages /Kids [3 0 R] /Count 1 >>".to_vec(),
        b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>".to_vec(),
        stream(&format!("BT /F1 48 Tf 20 30 Td <{codes}> Tj ET")),
    ];
    let descriptor = "<< /Type /FontDescriptor /FontName /David /Flags 32 /FontBBox [0 -300 1000 900] /ItalicAngle 0 /Ascent 900 /Descent -300 /CapHeight 700 /StemV 80 >>";
    if composite {
        let map: String = unicode
            .iter()
            .enumerate()
            .map(|(index, value)| format!("<{}> <{value}>\n", code(index)))
            .collect();
        objects.push(b"<< /Type /Font /Subtype /Type0 /BaseFont /David /Encoding /Identity-H /DescendantFonts [6 0 R] /ToUnicode 8 0 R >>".to_vec());
        objects.push(b"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /David /DW 600 /CIDToGIDMap /Identity /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor 7 0 R >>".to_vec());
        objects.push(descriptor.as_bytes().to_vec());
        objects.push(stream(&format!(
            "/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CMapName /ToUnicode def /CMapType 2 def 1 begincodespacerange <0000> <FFFF> endcodespacerange\n{} beginbfchar\n{map}endbfchar endcmap end end",
            characters.len()
        )));
    } else {
        let widths = vec!["600"; characters.len()].join(" ");
        let names: Vec<String> = unicode.iter().map(|u| format!("/uni{u}")).collect();
        objects.push(
            format!(
                "<< /Type /Font /Subtype /TrueType /BaseFont /David /FirstChar 65 /LastChar {} /Widths [{widths}] /FontDescriptor 6 0 R /Encoding << /Type /Encoding /Differences [65 {}] >> >>",
                64 + characters.len(),
                names.join(" ")
            )
            .into_bytes(),
        );
        objects.push(descriptor.as_bytes().to_vec());
    }
    pdf_of(&objects)
}

fn stream(content: &str) -> Vec<u8> {
    format!("<< /Length {} >>\nstream\n{content}\nendstream", content.len()).into_bytes()
}

/// `pdfOfParts`: the objects numbered from 1, with an xref table.
fn pdf_of(objects: &[Vec<u8>]) -> Vec<u8> {
    let mut out = b"%PDF-1.7\n".to_vec();
    let mut offsets = Vec::new();
    for (i, object) in objects.iter().enumerate() {
        offsets.push(out.len());
        out.extend(format!("{} 0 obj\n", i + 1).as_bytes());
        out.extend(object);
        out.extend(b"\nendobj\n");
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

#[test]
fn a_page_draws_a_script_in_a_font_it_only_names_in_the_fallback_that_has_it() {
    let mut host = Host::new();
    let mut job = 0;
    for (key, file) in [
        ("noto-sans-hebrew", "NotoSansHebrew-Regular.ttf"),
        ("noto-naskh-arabic", "NotoNaskhArabic-Regular.ttf"),
    ] {
        let bytes = test_support::fixture(&format!("{FONTS}/{file}"));
        send(
            &mut host,
            &mut job,
            json!({ "kind": "fonts.register", "fontKey": key, "familyName": "", "weight": 0,
                "italic": -1, "builtIn": true }),
            vec![bytes],
        );
        send(&mut host, &mut job, json!({ "kind": "fonts.addFallback", "fontKey": key }), vec![]);
    }
    for (script, text) in [("Hebrew", "שלום"), ("Arabic", "سلام")] {
        for composite in [false, true] {
            let doc = format!("{script}-{composite}");
            send(
                &mut host,
                &mut job,
                json!({ "kind": "open.fatMem", "effect": "open", "docId": doc, "password": null }),
                vec![pdf_naming_font(composite, text)],
            );
            let pages =
                send(&mut host, &mut job, json!({ "kind": "pages.list", "docId": doc }), vec![]);
            let page = pages["result"]["pages"][0]["ref"].clone();
            *(&mut job) += 1;
            let response = host.call_filled(
                &json!({ "kind": "pages.render", "jobId": job, "docId": doc, "page": page,
                    "options": { "viewport": { "kind": "scale", "scale": 1 } } })
                .to_string(),
                Vec::new(),
            );
            let answer: Value = serde_json::from_str(&response.json).unwrap();
            assert!(answer.get("error").is_none(), "{answer}");
            let raster = &answer["result"]["raster"];
            let (width, height, stride) = (
                raster["width"].as_u64().unwrap() as usize,
                raster["height"].as_u64().unwrap() as usize,
                raster["stride"].as_u64().unwrap() as usize,
            );
            let pixels = &response.payloads[0];
            // Each character, in its own place: more than 20 dark pixels in
            // the 28 columns the PDF gives it.
            let drawn: Vec<bool> = (0..text.chars().count())
                .map(|index| {
                    let from = (20.0 + index as f64 * 28.8).round() as usize;
                    let mut dark = 0;
                    for x in from..(from + 28).min(width) {
                        for y in 0..height {
                            let at = y * stride + x * 4;
                            if pixels[at + 3] > 127 && pixels[at] < 128 {
                                dark += 1;
                            }
                        }
                    }
                    dark > 20
                })
                .collect();
            assert_eq!(drawn, vec![true; drawn.len()], "{script}, composite {composite}");
        }
    }
}
