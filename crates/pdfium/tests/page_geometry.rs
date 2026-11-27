//! `EPDFText_GetPageGeometry` (runtime `7f399f838`, upstream #1157): the
//! geometry of many characters in one call, which must say what
//! `EPDFText_GetCharGeometry`, `FPDFText_GetTextObject` and
//! `FPDFText_GetFontSize` say one character at a time.

use pdfium::{Document, Library, PageCharGeometry};

const FIXTURES: [&str; 3] = [
    test_support::REPORT,
    test_support::ROTATED_TEXT,
    "packages/engine/main/test/fixtures/vertical_text.pdf",
];

#[test]
fn the_record_has_the_layout_the_native_static_asserts_pin() {
    // `EPDF_PAGE_CHAR_GEOMETRY_LAYOUT` of `structs.ts`: 132 bytes, the
    // geometry at 0, the object run at 124 and the font size at 128.
    assert_eq!(std::mem::size_of::<pdfium_sys::EPDF_PAGE_CHAR_GEOMETRY>(), 132);
    assert_eq!(std::mem::offset_of!(pdfium_sys::EPDF_PAGE_CHAR_GEOMETRY, geometry), 0);
    assert_eq!(std::mem::offset_of!(pdfium_sys::EPDF_PAGE_CHAR_GEOMETRY, object_run), 124);
    assert_eq!(std::mem::offset_of!(pdfium_sys::EPDF_PAGE_CHAR_GEOMETRY, font_size), 128);
    assert_eq!(std::mem::size_of::<pdfium_sys::EPDF_CHAR_GEOMETRY>(), 124);
}

#[test]
fn one_call_says_what_the_per_character_calls_say() {
    let lib = Library::acquire();
    for fixture in FIXTURES {
        let doc = Document::from_bytes(lib.clone(), &test_support::fixture(fixture), None)
            .unwrap_or_else(|e| panic!("{fixture}: {e:?}"));
        for index in 0..doc.page_count() {
            let page = doc.page(index).expect("page");
            let text = page.text().expect("text page");
            let count = text.char_count();
            let batch = text.page_geometry(0, count).expect("the page in one call");
            assert_eq!(batch.len(), count as usize, "{fixture} page {index}");
            let mut run = 0u32;
            let mut previous = None;
            for (at, record) in batch.iter().enumerate() {
                let at = at as u32;
                let object = text.text_object(at);
                if previous.is_some_and(|p| p != object) {
                    run += 1;
                }
                previous = Some(object);
                let one = text.char_geometry(at).unwrap_or_default();
                let expected = PageCharGeometry {
                    geometry: one,
                    object_run: run,
                    font_size: text.font_size(at) as f32,
                };
                assert_eq!(*record, expected, "{fixture} page {index} char {at}");
            }
        }
    }
}

#[test]
fn a_page_is_read_in_pieces_from_any_start() {
    let lib = Library::acquire();
    let doc = Document::from_bytes(lib, &test_support::fixture(test_support::REPORT), None)
        .expect("open report.pdf");
    let page = doc.page(0).expect("page 0");
    let text = page.text().expect("text page");
    let count = text.char_count();
    assert!(count > 10, "report.pdf page 0 has text");
    let whole = text.page_geometry(0, count).expect("whole");
    let mut pieces = Vec::new();
    let mut start = 0;
    while start < count {
        let piece = text.page_geometry(start, 7).expect("a piece");
        assert_eq!(piece.len() as u32, 7.min(count - start), "from {start}");
        pieces.extend(piece);
        start += 7;
    }
    assert_eq!(pieces, whole);
    // Past the end, and nothing asked for, read nothing.
    assert_eq!(text.page_geometry(count, 4).expect("past the end"), []);
    assert_eq!(text.page_geometry(0, 0).expect("nothing"), []);
}
