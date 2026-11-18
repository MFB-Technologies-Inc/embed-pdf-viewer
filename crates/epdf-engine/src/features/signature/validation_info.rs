//! A document's validation data: its security store (`/DSS`), the
//! certificates, OCSP responses and CRLs a later reader needs to check its
//! signatures with no network.
//!
//! `packages/engine/core/src/signature/validationInfo.ts` (the entry check)
//! and `packages/engine/services/src/features/signature/`
//! `ValidationInfoReader.ts`, `ValidationInfoMutator.ts` and
//! `internal/validationStore.ts` (67ed292a6, #1088). Unlike the signature
//! model, both read and write the document as it is now: an entry added and
//! not yet saved is part of it.

use serde::Serialize;

use crate::cancel::CancelToken;
use crate::error::{EngineError, EngineErrorCode};
use crate::mutation::MutationMeta;
use crate::session::document::DocumentSession;

/// `ValidationInfoKind`: the lists of a [`ValidationInfo`].
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ValidationInfoKind {
    Certificates,
    OcspResponses,
    Crls,
}

impl ValidationInfoKind {
    /// The wire's spelling.
    pub fn as_str(self) -> &'static str {
        match self {
            Self::Certificates => "certificates",
            Self::OcspResponses => "ocspResponses",
            Self::Crls => "crls",
        }
    }

    /// `KIND_CODE`: `EPDF_DSS_*` in `public/epdf_signature.h`.
    fn code(self) -> i32 {
        match self {
            Self::Certificates => pdfium::dss_kind::CERTIFICATES,
            Self::OcspResponses => pdfium::dss_kind::OCSP_RESPONSES,
            Self::Crls => pdfium::dss_kind::CRLS,
        }
    }
}

/// `VALIDATION_INFO_KINDS`: the lists, in the order they are read and
/// written.
pub const VALIDATION_INFO_KINDS: [ValidationInfoKind; 3] =
    [ValidationInfoKind::Certificates, ValidationInfoKind::OcspResponses, ValidationInfoKind::Crls];

/// `VALIDATION_INFO_LIMITS.entryBytes`: the largest entry
/// `addValidationInfo` stores.
pub const VALIDATION_INFO_ENTRY_BYTES: usize = 16 * 1024 * 1024;

/// How deep the elements of an entry may nest (`MAX_DEPTH`).
const MAX_DEPTH: usize = 64;

/// `ValidationInfo`: each entry one DER object, as stored.
#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct ValidationInfo {
    pub certificates: Vec<Vec<u8>>,
    pub ocsp_responses: Vec<Vec<u8>>,
    pub crls: Vec<Vec<u8>>,
}

impl ValidationInfo {
    pub fn get(&self, kind: ValidationInfoKind) -> &Vec<Vec<u8>> {
        match kind {
            ValidationInfoKind::Certificates => &self.certificates,
            ValidationInfoKind::OcspResponses => &self.ocsp_responses,
            ValidationInfoKind::Crls => &self.crls,
        }
    }

    pub fn get_mut(&mut self, kind: ValidationInfoKind) -> &mut Vec<Vec<u8>> {
        match kind {
            ValidationInfoKind::Certificates => &mut self.certificates,
            ValidationInfoKind::OcspResponses => &mut self.ocsp_responses,
            ValidationInfoKind::Crls => &mut self.crls,
        }
    }
}

/// `ValidationInfoCounts`: how many entries of each kind a write stored.
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ValidationInfoCounts {
    pub certificates: u32,
    pub ocsp_responses: u32,
    pub crls: u32,
}

impl ValidationInfoCounts {
    fn bump(&mut self, kind: ValidationInfoKind) {
        match kind {
            ValidationInfoKind::Certificates => self.certificates += 1,
            ValidationInfoKind::OcspResponses => self.ocsp_responses += 1,
            ValidationInfoKind::Crls => self.crls += 1,
        }
    }

    /// Whether the write stored anything.
    pub fn any(&self) -> bool {
        self.certificates > 0 || self.ocsp_responses > 0 || self.crls > 0
    }
}

/// `ValidationInfoAddResult`.
#[derive(Debug, Clone, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ValidationInfoAddResult {
    /// The entries that were new: one the document already held is not
    /// stored again.
    pub added: ValidationInfoCounts,
    pub meta: MutationMeta,
}

/// `validationInfoEntryProblem`: why `bytes` can't be stored as an entry,
/// or `None` when it can: one DER sequence that spans the bytes exactly,
/// every element inside it filling its parent, and no larger than the limit.
/// What the entry says is not looked at.
pub fn validation_info_entry_problem(bytes: &[u8]) -> Option<String> {
    if bytes.is_empty() {
        return Some("it is empty".into());
    }
    if bytes.len() > VALIDATION_INFO_ENTRY_BYTES {
        return Some(format!("it is larger than {VALIDATION_INFO_ENTRY_BYTES} bytes"));
    }
    if bytes[0] != 0x30 {
        return Some("it is not a DER SEQUENCE".into());
    }
    let Some(outer) = element_at(bytes, 0, bytes.len()) else {
        return Some("its length is not a DER length, or names more bytes than there are".into());
    };
    if outer.end != bytes.len() {
        return Some("bytes follow the object".into());
    }
    if fills(bytes, outer.contents, outer.end, 1) {
        None
    } else {
        Some("an element inside it is malformed".into())
    }
}

/// One element's extent: where its contents begin and where it ends.
struct Element {
    constructed: bool,
    contents: usize,
    end: usize,
}

/// `elementAt`: the element that starts at `at`, or `None` when its header
/// is not one or runs past `limit`.
fn element_at(bytes: &[u8], at: usize, limit: usize) -> Option<Element> {
    let mut cursor = at;
    if cursor >= limit {
        return None;
    }
    let identifier = bytes[cursor];
    cursor += 1;
    if identifier & 0x1f == 0x1f {
        // A tag number of 31 or more takes further bytes, the last without
        // the high bit.
        loop {
            if cursor >= limit {
                return None;
            }
            let b = bytes[cursor];
            cursor += 1;
            if b & 0x80 == 0 {
                break;
            }
        }
    }
    if cursor >= limit {
        return None;
    }
    let first = bytes[cursor];
    cursor += 1;
    let mut length = first as usize;
    if first & 0x80 != 0 {
        let count = (first & 0x7f) as usize;
        // 0 is the indefinite form, which DER does not have.
        if count == 0 || count > 4 || cursor + count > limit {
            return None;
        }
        length = 0;
        for _ in 0..count {
            length = length * 256 + bytes[cursor] as usize;
            cursor += 1;
        }
    }
    let end = cursor.checked_add(length)?;
    if end > limit {
        return None;
    }
    Some(Element { constructed: identifier & 0x20 != 0, contents: cursor, end })
}

/// `fills`: whether `[from, to)` is a run of elements, each of whose own
/// elements fills it in turn.
fn fills(bytes: &[u8], from: usize, to: usize, depth: usize) -> bool {
    if depth > MAX_DEPTH {
        return false;
    }
    let mut cursor = from;
    while cursor < to {
        let Some(element) = element_at(bytes, cursor, to) else { return false };
        if element.constructed && !fills(bytes, element.contents, element.end, depth + 1) {
            return false;
        }
        cursor = element.end;
    }
    cursor == to
}

/// `readValidationStore`: every entry of the document's security store, as
/// stored: each kind's list, then what only the store's index names, each
/// stream once. A stream whose data can't be decoded reads as nothing.
pub fn read_validation_store(doc: &pdfium::Document) -> Result<ValidationInfo, EngineError> {
    let mut out = ValidationInfo::default();
    for kind in VALIDATION_INFO_KINDS {
        let count = doc.dss_count(kind.code());
        let entries = out.get_mut(kind);
        for index in 0..count.max(0) {
            match doc.dss_data(kind.code(), index) {
                Ok(Some(bytes)) => entries.push(bytes),
                Ok(None) => {}
                Err(_) => {
                    return Err(EngineError::with_message(
                        EngineErrorCode::Unknown,
                        format!("failed to read entry {index} of the document's {}", kind.as_str()),
                    ));
                }
            }
        }
    }
    Ok(out)
}

/// `addToValidationStore`: store `entry` as a stream of its own named in the
/// list of `kind`. The caller checked the entry and that the document doesn't
/// hold it yet.
fn add_to_validation_store(
    doc: &pdfium::Document,
    kind: ValidationInfoKind,
    entry: &[u8],
) -> Result<(), EngineError> {
    if doc.dss_add(kind.code(), entry) == 0 {
        // The fork refuses a store it can't extend: a /DSS or a list of
        // another type.
        return Err(EngineError::with_message(
            EngineErrorCode::MalformedPdf,
            format!(
                "the document's security store can't be extended: its /DSS or its list of {} is not what the PDF standard says it is",
                kind.as_str()
            ),
        ));
    }
    Ok(())
}

/// `ValidationInfoReader.read`.
pub fn read(session: &DocumentSession) -> Result<ValidationInfo, EngineError> {
    read_validation_store(session.document()?.document())
}

/// `ValidationInfoMutator.add`: every entry is checked before the first one
/// is written, and an entry the document already holds, or one named twice,
/// is stored once. Runs inside the request's write transaction.
pub fn add(
    session: &DocumentSession,
    input: &ValidationInfo,
    cancel: &CancelToken,
) -> Result<ValidationInfoAddResult, EngineError> {
    cancel.check()?;
    for kind in VALIDATION_INFO_KINDS {
        for (index, entry) in input.get(kind).iter().enumerate() {
            if let Some(problem) = validation_info_entry_problem(entry) {
                let field = format!("{}[{index}]", kind.as_str());
                return Err(EngineError::with_message(
                    EngineErrorCode::InvalidArg,
                    format!("{field}: {problem}"),
                )
                .with_details(serde_json::json!({ "field": field })));
            }
        }
    }
    let doc = session.document()?.document();
    let mut held = read_validation_store(doc)?;
    let mut added = ValidationInfoCounts::default();
    for kind in VALIDATION_INFO_KINDS {
        for entry in input.get(kind) {
            cancel.check()?;
            // Byte for byte, among the entries of its kind.
            if held.get(kind).iter().any(|other| other == entry) {
                continue;
            }
            add_to_validation_store(doc, kind, entry)?;
            // The next entry is compared with this one too.
            held.get_mut(kind).push(entry.clone());
            added.bump(kind);
        }
    }
    Ok(ValidationInfoAddResult {
        added,
        meta: MutationMeta::local(Vec::new(), &session.write_stamp()?),
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn der(label: &str) -> Vec<u8> {
        let text = label.as_bytes();
        let mut out = vec![0x30, (text.len() + 2) as u8, 0x04, text.len() as u8];
        out.extend_from_slice(text);
        out
    }

    #[test]
    fn an_entry_is_one_der_sequence_spanning_the_bytes() {
        assert_eq!(validation_info_entry_problem(&der("certificate a")), None);
        assert_eq!(validation_info_entry_problem(&[]).as_deref(), Some("it is empty"));
        assert_eq!(
            validation_info_entry_problem(b"-----BEGIN CERTIFICATE-----").as_deref(),
            Some("it is not a DER SEQUENCE")
        );
        let mut short = der("crl one");
        short.pop();
        assert_eq!(
            validation_info_entry_problem(&short).as_deref(),
            Some("its length is not a DER length, or names more bytes than there are")
        );
        let mut twice = der("ocsp response");
        twice.extend(der("ocsp response"));
        assert_eq!(
            validation_info_entry_problem(&twice).as_deref(),
            Some("bytes follow the object")
        );
        // An inner element whose length runs past its parent.
        assert_eq!(
            validation_info_entry_problem(&[0x30, 0x03, 0x04, 0x05, 0x61]).as_deref(),
            Some("an element inside it is malformed")
        );
        // The indefinite form is not DER.
        assert!(validation_info_entry_problem(&[0x30, 0x80, 0x00, 0x00]).is_some());
        // A long-form length and a high tag number read through.
        assert_eq!(validation_info_entry_problem(&[0x30, 0x81, 0x03, 0x9f, 0x21, 0x00]), None);
    }

    #[test]
    fn an_entry_past_the_limit_is_refused() {
        let mut big = vec![0x30, 0x84];
        let length = VALIDATION_INFO_ENTRY_BYTES - 6 + 1;
        big.extend_from_slice(&(length as u32).to_be_bytes());
        big.resize(6 + length, 0);
        assert_eq!(
            validation_info_entry_problem(&big),
            Some(format!("it is larger than {VALIDATION_INFO_ENTRY_BYTES} bytes"))
        );
    }
}
