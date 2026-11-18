//! The signature model and revision calls against the signing fixtures.

use pdfium::{DigestAlgorithm, Document, Library, string_key};

const FIX: &str = "packages/engine/main/test/fixtures";

fn open(name: &str) -> Document {
    Document::from_bytes(Library::acquire(), &test_support::fixture(&format!("{FIX}/{name}")), None)
        .unwrap_or_else(|e| panic!("open {name}: {e}"))
}

#[test]
fn two_approvals_have_two_signed_fields_and_three_revisions() {
    let doc = open("signed_two_approvals.pdf");
    let model = doc.signature_model().unwrap();
    assert_eq!(model.count(), 2);
    assert!(model.is_revision_chain_valid());
    assert_eq!(doc.revision_count(), 3);
    for i in 0..2 {
        assert!(model.is_signed(i));
        assert_eq!(model.coverage(i), 0, "whole-revision");
        let contents = model.contents(i).unwrap();
        assert_eq!(contents[0], 0x30);
        assert_eq!(contents.len() as u64, model.contents_len(i));
        assert!(model.field_name(i).unwrap().is_some());
        let field = model.field_object_number(i);
        assert_eq!(model.index_by_field_object_number(field), i);
    }
    assert_eq!(model.string(0, string_key::FILTER).unwrap().as_deref(), Some("Adobe.PPKLite"));
    let (end, _) = doc.revision(0).unwrap();
    let first = doc.read_loaded_bytes(0, end).unwrap();
    assert!(first.starts_with(b"%PDF-"));
    let size = doc.loaded_bytes_size();
    let digest = doc.digest_byte_range([0, size, size, 0], DigestAlgorithm::Sha256).unwrap();
    assert_eq!(digest.len(), 32);
}

#[test]
fn an_unsigned_field_is_listed_unsigned() {
    let doc = open("unsigned_sigfield.pdf");
    let model = doc.signature_model().unwrap();
    assert_eq!(model.count(), 1);
    assert!(!model.is_signed(0));
    assert_eq!(model.contents_len(0), 0);
    assert!(model.contents(0).unwrap().is_empty());
}

// ── The document security store and loaded object values (runtime 8e2c3fd5f) ──

#[test]
fn the_security_store_stores_bytes_and_reads_them_back_as_stored() {
    use pdfium::dss_kind::{CERTIFICATES, CRLS, OCSP_RESPONSES};
    let doc = open("unsigned_sigfield.pdf");
    for kind in [CERTIFICATES, OCSP_RESPONSES, CRLS] {
        assert_eq!(doc.dss_count(kind), 0, "no store yet");
    }
    assert_eq!(doc.dss_count(3), -1, "an unknown kind");
    let first = [0x30, 0x03, 0x04, 0x01, 0x61];
    let second = [0x30, 0x03, 0x04, 0x01, 0x62];
    let a = doc.dss_add(CERTIFICATES, &first);
    let b = doc.dss_add(CERTIFICATES, &second);
    assert!(a > 0 && b > a, "{a} {b}");
    assert_eq!(doc.dss_add(CRLS, &[]), 0, "empty data is refused");
    assert_eq!(doc.dss_add(3, &first), 0, "an unknown kind is refused");
    assert_eq!(doc.dss_count(CERTIFICATES), 2);
    assert_eq!(doc.dss_count(CRLS), 0);
    assert_eq!(doc.dss_data(CERTIFICATES, 0).unwrap().as_deref(), Some(&first[..]));
    assert_eq!(doc.dss_data(CERTIFICATES, 1).unwrap().as_deref(), Some(&second[..]));
    assert_eq!(doc.dss_data(CERTIFICATES, 2).unwrap(), None, "out of range");
}

#[test]
fn a_long_term_file_holds_validation_data() {
    use pdfium::dss_kind::CERTIFICATES;
    let doc = open("signature-long-term/acrobat-lta.pdf");
    let count = doc.dss_count(CERTIFICATES);
    assert!(count > 0, "{count}");
    for index in 0..count {
        let entry = doc.dss_data(CERTIFICATES, index).unwrap().expect("a certificate");
        assert_eq!(entry[0], 0x30, "one DER sequence");
    }
}

#[test]
fn a_loaded_object_value_reads_any_object_of_the_revision() {
    let doc = open("signed_two_approvals.pdf");
    let trailer = doc.loaded_object_value(0);
    assert_eq!(trailer.read_status, 0, "{trailer:?}");
    assert!(!trailer.truncated);
    assert!(trailer.text.as_deref().is_some_and(|t| t.starts_with("<<")), "{trailer:?}");
    let (root, _, _) = doc.structure_object_numbers();
    let catalog = doc.loaded_object_value(root);
    assert!(catalog.text.as_deref().is_some_and(|t| t.contains("/Type /Catalog")), "{catalog:?}");
    let absent = doc.loaded_object_value(999_999);
    assert_eq!(absent.read_status, 2, "{absent:?}");
    assert_eq!(absent.text, None);
}
