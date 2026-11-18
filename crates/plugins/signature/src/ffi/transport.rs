//! D-1, the host's HTTP door (CU-C4b-D2): the timestamp authority and the
//! revocation source send their one POST through a [`SignatureTransport`]
//! the host implements on its own HTTP stack (`URLSession`, OkHttp,
//! `HttpURLConnection`). Everything around the request, the RFC 3161 query,
//! the route's JSON, retries, what a status means, stays in Rust.
//!
//! `post` is called on the kernel thread and answers through its
//! completion, from any thread, as `SignatureSigner::sign` does.

use std::rc::Rc;
use std::sync::{Arc, Mutex};

use epdf_ffi::{ForeignSettle, KernelPost};
use epdf_kernel::rt::Promise;
use epdf_signature_core::transport::{
    HttpTransport, TransportHeader, TransportOutcome, TransportRequest,
};
use epdf_signature_core::{
    CmsSigner, HttpRevocationSourceOptions, HttpTimestampAuthorityOptions, RevocationSource,
};

use super::ports::SignatureDigest;
use crate::transport::{NonceSource, revocation_source, timestamp_authority};

/// One request header, in the order sent.
#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct SignatureTransportHeader {
    pub name: String,
    pub value: String,
}

impl From<TransportHeader> for SignatureTransportHeader {
    fn from(h: TransportHeader) -> Self {
        SignatureTransportHeader { name: h.name, value: h.value }
    }
}

impl From<SignatureTransportHeader> for TransportHeader {
    fn from(h: SignatureTransportHeader) -> Self {
        TransportHeader { name: h.name, value: h.value }
    }
}

/// One HTTP POST.
#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct SignatureTransportRequest {
    pub url: String,
    /// The `content-type` header: `application/timestamp-query` or
    /// `application/json`.
    pub content_type: String,
    /// More headers, after `content-type`: an API key, a session token.
    pub headers: Vec<SignatureTransportHeader>,
    pub body: Vec<u8>,
    /// Give up after this long and answer `timed_out`.
    pub timeout_ms: u32,
    /// Stop reading past this many bytes and answer `too_large`. A longer
    /// body answered through `replied` counts as `too_large` too.
    pub max_reply_bytes: u32,
}

impl From<TransportRequest> for SignatureTransportRequest {
    fn from(r: TransportRequest) -> Self {
        SignatureTransportRequest {
            url: r.url,
            content_type: r.content_type,
            headers: r.headers.into_iter().map(Into::into).collect(),
            body: r.body,
            timeout_ms: r.timeout_ms,
            max_reply_bytes: r.max_reply_bytes,
        }
    }
}

/// The host's HTTP stack. `post` is called on the kernel thread, must
/// return at once, and answers through `completion` from any thread. The
/// host enforces `timeout_ms` and `max_reply_bytes`.
#[uniffi::export(with_foreign)]
pub trait SignatureTransport: Send + Sync {
    fn post(
        &self,
        request: SignatureTransportRequest,
        completion: Arc<SignatureTransportCompletion>,
    );
}

/// The answer to one `SignatureTransport::post`. The first call wins;
/// dropping it unanswered counts as a failure, `fail`'s words being that
/// it was dropped.
#[derive(uniffi::Object)]
pub struct SignatureTransportCompletion {
    settle: ForeignSettle<TransportOutcome>,
}

#[uniffi::export]
impl SignatureTransportCompletion {
    /// A reply came, whatever its status.
    pub fn replied(&self, status: u16, body: Vec<u8>) {
        self.settle.settle(move || Ok(TransportOutcome::Replied { status, body }));
    }

    /// No reply within `timeout_ms`.
    pub fn timed_out(&self) {
        self.settle.settle(|| Ok(TransportOutcome::TimedOut));
    }

    /// The reply ran past `max_reply_bytes`.
    pub fn too_large(&self) {
        self.settle.settle(|| Ok(TransportOutcome::TooLarge));
    }

    /// No reply: the connection failed, the name did not resolve, the
    /// request was refused before it left. `message` is the host's words.
    pub fn failed(&self, message: String) {
        self.settle.settle(move || Ok(TransportOutcome::Failed { message }));
    }
}

/// The transport a host has set, shared with every document's handle.
pub type SignatureTransportSlot = Arc<Mutex<Option<Arc<dyn SignatureTransport>>>>;

/// `HttpTransport` over a host's slot, read per request: with the slot
/// empty, every request fails with no reply.
pub struct SlotTransport {
    pub slot: SignatureTransportSlot,
    pub post: KernelPost,
}

impl HttpTransport for SlotTransport {
    fn post(&self, request: TransportRequest) -> Promise<TransportOutcome> {
        let transport = self.slot.lock().unwrap().clone();
        let Some(transport) = transport else {
            return Promise::resolved(TransportOutcome::Failed {
                message: "no transport: set one on the viewer first".into(),
            });
        };
        let (promise, resolver) = Promise::pending();
        let completion = Arc::new(SignatureTransportCompletion {
            settle: ForeignSettle::new(
                resolver,
                self.post.clone(),
                "a SignatureTransport completion",
            ),
        });
        transport.post(request.into(), completion);
        promise
    }
}

/// A timestamp authority reached over the host's transport
/// (`httpTimestampAuthority`).
#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct SignatureTimestampAuthority {
    /// The authority's address, or the app's own route in front of it.
    pub url: String,
    /// What `sign` stamps with. `None` is sha256.
    #[uniffi(default = None)]
    pub hash: Option<SignatureDigest>,
    /// The policy to ask for (an OID).
    #[uniffi(default = None)]
    pub policy: Option<String>,
    #[uniffi(default = [])]
    pub headers: Vec<SignatureTransportHeader>,
    /// One attempt's limit. `None` is 10000.
    #[uniffi(default = None)]
    pub timeout_ms: Option<u32>,
    /// Further attempts when no answer came. `None` is 1.
    #[uniffi(default = None)]
    pub retries: Option<u32>,
    /// Tests only: every octet of the request's nonce is this one, so a
    /// recorded reply answers it. `None` draws a fresh nonce.
    #[uniffi(default = None)]
    pub fixed_nonce_octet: Option<u8>,
}

impl SignatureTimestampAuthority {
    /// The client over `transport`.
    pub fn signer(self, transport: Rc<dyn HttpTransport>) -> CmsSigner {
        let nonce = self.fixed_nonce_octet.map_or(NonceSource::Random, NonceSource::Fixed);
        let options = HttpTimestampAuthorityOptions {
            url: self.url,
            hash: self.hash.map(SignatureDigest::core),
            policy: self.policy,
            headers: self.headers.into_iter().map(Into::into).collect(),
            timeout_ms: self.timeout_ms,
            retries: self.retries,
        };
        timestamp_authority(transport, options, nonce)
    }
}

/// A revocation source asked through the app's own route
/// (`httpRevocationSource`, D-2).
#[derive(Clone, Debug, PartialEq, Eq, uniffi::Record)]
pub struct SignatureRevocationRoute {
    /// The route's address.
    pub url: String,
    #[uniffi(default = [])]
    pub headers: Vec<SignatureTransportHeader>,
    /// One attempt's limit. `None` is 10000.
    #[uniffi(default = None)]
    pub timeout_ms: Option<u32>,
    /// Further attempts when no answer came. `None` is 1.
    #[uniffi(default = None)]
    pub retries: Option<u32>,
    /// The largest answer read. `None` is 2 MiB, the route's default.
    #[uniffi(default = None)]
    pub max_reply_bytes: Option<u32>,
}

impl SignatureRevocationRoute {
    /// The source over `transport`.
    pub fn source(self, transport: Rc<dyn HttpTransport>) -> Rc<dyn RevocationSource> {
        revocation_source(
            transport,
            HttpRevocationSourceOptions {
                url: self.url,
                headers: self.headers.into_iter().map(Into::into).collect(),
                timeout_ms: self.timeout_ms,
                retries: self.retries,
                max_reply_bytes: self.max_reply_bytes,
            },
        )
    }
}
