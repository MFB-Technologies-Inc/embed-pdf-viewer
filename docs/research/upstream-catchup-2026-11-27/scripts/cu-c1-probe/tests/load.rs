// CU-C1 (2026-11-27) probe: the library pdfium-sys links (PDF_RUNTIME_LIB_DIR)
// initialises, makes a document, and answers the exports runtime 7f399f838
// renamed or added: EPDFDoc_GetDrawings, EPDFAnnot_GetDrawing,
// EPDFAnnot_HasMKIcon, EPDFAnnot_GetMKIcon, EPDFAnnot_ClearMKIcon and
// EPDFText_GetPageGeometry.
use pdfium_sys_probe::*;
use std::ptr;

#[test]
fn library_loads_and_the_renamed_and_new_exports_answer() {
    unsafe {
        FPDF_InitLibrary();
        let doc = FPDF_CreateNewDocument();
        assert!(!doc.is_null(), "FPDF_CreateNewDocument returned null");
        let page = FPDFPage_New(doc, 0, 612.0, 792.0);
        assert!(!page.is_null());
        assert_eq!(FPDF_GetPageCount(doc), 1);

        // EPDFDoc_GetDrawings (was EPDFDoc_GetStampDrawings): a new document
        // has no drawings.
        assert_eq!(EPDFDoc_GetDrawings(doc, ptr::null_mut(), 0), 0);

        // A stamp: no drawing yet, and no MK icon (only push buttons have one).
        let annot = FPDFPage_CreateAnnot(page, FPDF_ANNOT_STAMP as _);
        assert!(!annot.is_null(), "FPDFPage_CreateAnnot returned null");
        let drawing = EPDFAnnot_GetDrawing(annot);
        let has_icon = EPDFAnnot_HasMKIcon(annot, 0);
        let icon = EPDFAnnot_GetMKIcon(annot, 0);
        let cleared = EPDFAnnot_ClearMKIcon(annot, 0);
        assert_eq!(drawing, 0);
        assert_eq!(has_icon, 0);
        assert_eq!(icon, 0);
        FPDFPage_CloseAnnot(annot);

        // EPDFText_GetPageGeometry: an empty page has no glyphs.
        let text_page = FPDFText_LoadPage(page);
        assert!(!text_page.is_null());
        assert_eq!(FPDFText_CountChars(text_page), 0);
        let written = EPDFText_GetPageGeometry(text_page, 0, 0, ptr::null_mut());
        FPDFText_ClosePage(text_page);
        println!(
            "GetDrawing -> {drawing}; HasMKIcon -> {has_icon}; GetMKIcon -> {icon}; \
             ClearMKIcon -> {cleared}; GetPageGeometry(empty) -> {written}"
        );
        assert!(written <= 0);

        FPDF_ClosePage(page);
        FPDF_CloseDocument(doc);
        FPDF_DestroyLibrary();
    }
}
