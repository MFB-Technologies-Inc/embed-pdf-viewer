//! Thread-local counters of the PDFium calls that dominate engine cost.
//!
//! Compiled only with the `call-count` feature. Tests read them to prove a
//! change cuts PDFium work; release builds carry none of this. The counters
//! are per thread because PDFium state is: each `Library` and every handle
//! built from it stays on one thread.

use std::cell::Cell;

/// A counted PDFium entry point.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum Call {
    /// A page load: `FPDF_LoadPage` or
    /// `EPDFDoc_LoadPageByObjectNumberNormalized`.
    LoadPage,
    /// `EPDFForm_LoadModel`.
    FormLoadModel,
    /// `EPDFPage_GetAnnotRaw`.
    GetAnnotRaw,
    /// `FPDFText_LoadPage`.
    TextLoadPage,
    /// `EPDFSig_LoadModel`.
    SigLoadModel,
}

const CALLS: usize = 5;

thread_local! {
    static COUNTS: [Cell<u64>; CALLS] = const { [const { Cell::new(0) }; CALLS] };
}

/// Zeroes every counter on this thread.
pub fn reset() {
    COUNTS.with(|counts| counts.iter().for_each(|count| count.set(0)));
}

/// How many times this thread has made `call` since the last [`reset`].
pub fn get(call: Call) -> u64 {
    COUNTS.with(|counts| counts[call as usize].get())
}

pub(crate) fn record(call: Call) {
    COUNTS.with(|counts| {
        let count = &counts[call as usize];
        count.set(count.get() + 1);
    });
}
