//! `revocation/*`: revocation answers (OCSP responses and CRLs), where they
//! come from, and what they say about a certificate at a time.

pub mod addresses;
pub mod answer;
pub mod fill;
pub mod http_source;
pub mod pool;
pub mod source;
pub mod status;
