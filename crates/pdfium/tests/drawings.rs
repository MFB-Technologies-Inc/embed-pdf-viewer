//! A document's drawings in an annotation: a stamp's (`EPDFAnnot_GetDrawing`,
//! `EPDFAnnot_SetDrawing`, `EPDFDoc_GetDrawings`, renamed from their stamp
//! spelling in runtime `7f399f838`) and a push button's `/MK` icons
//! (`EPDFAnnot_{Get,Set,Has,Clear}MKIcon`, the same runtime).

use pdfium::{Document, Library, MkIcon, RectF, StampFit};

const ARTWORK: &str = "packages/engine/main/test/fixtures/signature_artwork.pdf";
/// `FPDF_ANNOT_TEXT`, `FPDF_ANNOT_STAMP` and `FPDF_ANNOT_WIDGET`.
const TEXT: i32 = 1;
const STAMP: i32 = 13;
const WIDGET: i32 = 20;

fn report(lib: std::rc::Rc<Library>) -> Document {
    Document::from_bytes(lib, &test_support::fixture(test_support::REPORT), None)
        .expect("open report.pdf")
}

/// The artwork's page as a drawing of `doc`, by object number.
fn artwork_drawing(doc: &Document) -> u32 {
    let canonical =
        doc.canonical_drawing_of_pdf(&test_support::fixture(ARTWORK)).expect("a drawing");
    doc.import_drawing(&canonical).expect("imported")
}

#[test]
fn a_stamp_places_a_drawing_and_the_document_lists_it() {
    let lib = Library::acquire();
    let doc = report(lib);
    let drawing = artwork_drawing(&doc);
    let page = doc.page(0).expect("page 0");
    let stamp = page.create_annotation(STAMP).expect("a stamp");
    // A drawing is fitted into the box, so the stamp needs one first.
    assert!(stamp.set_rect(RectF { left: 100.0, bottom: 100.0, right: 200.0, top: 150.0 }));
    assert_eq!(stamp.drawing(), None, "a new stamp places nothing");
    assert!(stamp.set_drawing(drawing, StampFit::Contain));
    assert_eq!(stamp.drawing(), Some(drawing));
    assert!(doc.drawings().contains(&drawing), "{:?}", doc.drawings());
}

#[test]
fn a_widget_names_a_drawing_as_each_of_its_three_icons() {
    let lib = Library::acquire();
    let doc = report(lib);
    let drawing = artwork_drawing(&doc);
    let page = doc.page(0).expect("page 0");
    let widget = page.create_annotation(WIDGET).expect("a widget");

    for which in [MkIcon::Normal, MkIcon::Rollover, MkIcon::Down] {
        assert!(!widget.has_mk_icon(which), "{which:?}: a new widget has no icon");
        assert_eq!(widget.mk_icon(which), None, "{which:?}");
        // Clearing an entry that isn't there is no failure.
        assert!(widget.clear_mk_icon(which), "{which:?}");
    }

    assert!(widget.set_mk_icon(MkIcon::Normal, drawing));
    assert!(widget.has_mk_icon(MkIcon::Normal));
    assert_eq!(widget.mk_icon(MkIcon::Normal), Some(drawing));
    // Only that entry is written.
    assert!(!widget.has_mk_icon(MkIcon::Rollover));
    assert!(!widget.has_mk_icon(MkIcon::Down));
    // Naming the drawing again writes nothing, and answers true.
    assert!(widget.set_mk_icon(MkIcon::Normal, drawing));

    assert!(widget.set_mk_icon(MkIcon::Down, drawing));
    assert_eq!(widget.mk_icon(MkIcon::Down), Some(drawing));

    assert!(widget.clear_mk_icon(MkIcon::Normal));
    assert!(!widget.has_mk_icon(MkIcon::Normal));
    assert_eq!(widget.mk_icon(MkIcon::Normal), None);
    assert_eq!(widget.mk_icon(MkIcon::Down), Some(drawing), "the other icon stays");
    // The icon itself is not deleted: the document can still export it.
    assert!(doc.export_drawing(drawing).is_some());
}

#[test]
fn an_icon_that_is_no_drawing_of_the_document_is_refused() {
    let lib = Library::acquire();
    let doc = report(lib);
    let page = doc.page(0).expect("page 0");
    let widget = page.create_annotation(WIDGET).expect("a widget");
    // The page's own object is no form XObject.
    assert!(!widget.set_mk_icon(MkIcon::Normal, page.object_number()));
    assert!(!widget.set_mk_icon(MkIcon::Normal, 0));
    assert!(!widget.has_mk_icon(MkIcon::Normal));
}

#[test]
fn an_annotation_that_is_no_widget_has_no_icons() {
    let lib = Library::acquire();
    let doc = report(lib);
    let drawing = artwork_drawing(&doc);
    let page = doc.page(0).expect("page 0");
    let note = page.create_annotation(TEXT).expect("a note");
    assert!(!note.set_mk_icon(MkIcon::Normal, drawing));
    assert!(!note.has_mk_icon(MkIcon::Normal));
    assert_eq!(note.mk_icon(MkIcon::Normal), None);
    assert!(!note.clear_mk_icon(MkIcon::Normal));
}
