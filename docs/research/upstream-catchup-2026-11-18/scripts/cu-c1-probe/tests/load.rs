// CU-C1 probe: the library pdfium-sys links (PDF_RUNTIME_LIB_DIR) initialises,
// makes a document, and answers the four exports new in runtime f2ea75972.
use pdfium_sys_probe::*;
use std::ptr;

#[test]
fn library_loads_and_the_new_dss_exports_answer() {
    unsafe {
        FPDF_InitLibrary();
        let doc = FPDF_CreateNewDocument();
        assert!(!doc.is_null(), "FPDF_CreateNewDocument returned null");
        let page = FPDFPage_New(doc, 0, 612.0, 792.0);
        assert!(!page.is_null());
        assert_eq!(FPDF_GetPageCount(doc), 1);
        FPDF_ClosePage(page);

        // EPDFDss_GetCount: no store yet, so 0; unknown kind is -1.
        assert_eq!(EPDFDss_GetCount(doc, 0), 0);
        assert_eq!(EPDFDss_GetCount(doc, 99), -1);
        // EPDFDss_Add: store 5 bytes as a certificate, get an object number back.
        let der = [0x30u8, 0x03, 0x02, 0x01, 0x05];
        let obj = EPDFDss_Add(doc, 0, der.as_ptr().cast(), der.len() as _);
        assert!(obj > 0, "EPDFDss_Add returned 0");
        assert_eq!(EPDFDss_GetCount(doc, 0), 1);
        // EPDFDss_GetData: two-call protocol reads the same bytes back.
        let len = EPDFDss_GetData(doc, 0, 0, ptr::null_mut(), 0);
        assert_eq!(len as usize, der.len());
        let mut buf = vec![0u8; len as usize];
        EPDFDss_GetData(doc, 0, 0, buf.as_mut_ptr().cast(), len);
        assert_eq!(buf, der);
        // EPDFDoc_GetLoadedObjectValue: a new document has no loaded bytes,
        // so the call returns 0 with a status it set.
        let mut status = 7;
        let mut truncated = 0;
        let n = EPDFDoc_GetLoadedObjectValue(doc, 1, ptr::null_mut(), 0, &mut status, &mut truncated);
        println!("EPDFDss_Add -> obj {obj}; EPDFDoc_GetLoadedObjectValue -> {n}, status {status}, truncated {truncated}");

        FPDF_CloseDocument(doc);
        FPDF_DestroyLibrary();
    }
}
