//! The document security store (`/DSS`, ISO 32000-2 12.8.4.3): the
//! certificates, OCSP responses and CRLs a document holds so that its
//! signatures can be checked later with no network.
//!
//! Wraps `EPDFDss_GetCount`, `EPDFDss_GetData` and `EPDFDss_Add` from
//! `epdf_signature.h`. They move bytes and nothing more: what is worth
//! storing, and whether an entry is held already, is the engine's to decide
//! (`validationStore.ts`). Unlike the revision calls, they read and write the
//! document as it is now, so an entry added and not yet saved reads back.

use std::os::raw::c_ulong;

use crate::{Document, PdfiumError};

/// `EPDF_DSS_*`: which list of the store.
pub mod dss_kind {
    /// `/Certs`, and `/Cert` of an index entry.
    pub const CERTIFICATES: i32 = 0;
    /// `/OCSPs`, and `/OCSP` of an index entry.
    pub const OCSP_RESPONSES: i32 = 1;
    /// `/CRLs`, and `/CRL` of an index entry.
    pub const CRLS: i32 = 2;
}

impl Document {
    /// `EPDFDss_GetCount`: how many streams of `kind` the store references,
    /// each once. 0 without a store; -1 for an unknown kind.
    pub fn dss_count(&self, kind: i32) -> i32 {
        // SAFETY: live document.
        unsafe { pdfium_sys::EPDFDss_GetCount(self.as_raw(), kind) }
    }

    /// `EPDFDss_GetData`, two-call: the decoded data of stream `index` of
    /// `kind`. `Ok(None)` when the index is out of range or the stream's data
    /// can't be decoded (the call answers 0); an error when the second call
    /// writes another length than the first reported.
    pub fn dss_data(&self, kind: i32, index: i32) -> Result<Option<Vec<u8>>, PdfiumError> {
        // SAFETY: a NULL buffer of length 0 asks for the length only.
        let length = unsafe {
            pdfium_sys::EPDFDss_GetData(self.as_raw(), kind, index, std::ptr::null_mut(), 0)
        };
        if length == 0 {
            return Ok(None);
        }
        let size = usize::try_from(length).map_err(|_| PdfiumError::OutOfMemory)?;
        let mut out: Vec<u8> = Vec::new();
        out.try_reserve_exact(size).map_err(|_| PdfiumError::OutOfMemory)?;
        out.resize(size, 0);
        // SAFETY: `out` holds `length` writable bytes.
        let written = unsafe {
            pdfium_sys::EPDFDss_GetData(self.as_raw(), kind, index, out.as_mut_ptr().cast(), length)
        };
        if written != length {
            return Err(PdfiumError::InvalidArgument(
                "the store entry changed length between reads",
            ));
        }
        Ok(Some(out))
    }

    /// `EPDFDss_Add`: store `data` as a new stream at the end of the list of
    /// `kind`, making the store and the list when they are missing. The new
    /// stream's object number; 0 when refused (no catalog, a `/DSS` or list
    /// of another type, an unknown kind, empty data), and nothing is written
    /// then.
    pub fn dss_add(&self, kind: i32, data: &[u8]) -> u32 {
        if data.is_empty() {
            return 0;
        }
        // SAFETY: `data` is `data.len()` readable bytes for the call.
        unsafe {
            pdfium_sys::EPDFDss_Add(
                self.as_raw(),
                kind,
                data.as_ptr().cast(),
                data.len() as c_ulong,
            )
        }
    }
}
