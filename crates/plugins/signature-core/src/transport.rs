//! D-1: the HTTP transport a host implements, the one network door of the
//! package. A timestamp authority (`httpTimestampAuthority`) and a
//! revocation source (`httpRevocationSource`) each send one POST through it
//! and read the reply; everything around the request (the RFC 3161 query,
//! the route's JSON, retries, what a status means) is ported Rust.
//!
//! The types are plain strings, byte lists and integers so a wrapper can
//! export the trait through uniffi (Swift, Kotlin), the C ABI (.NET) and
//! wasm-bindgen (browser `fetch`).

use std::rc::Rc;

use epdf_kernel::Error;
use epdf_kernel::rt::{self, Promise};

/// The most a request may carry: 64 KiB, the routes' limit
/// (`server/timestamp-route.ts`, `server/revocation-route.ts`).
pub const MAX_REQUEST_BYTES: usize = 64 * 1024;
/// The most a timestamp authority's reply may carry: 1 MiB
/// (`server/timestamp-route.ts`).
pub const MAX_TIMESTAMP_REPLY_BYTES: u32 = 1024 * 1024;
/// The most a revocation answer may carry: 2 MiB, the revocation route's
/// default `maxBytes` (`server/revocation-route.ts`).
pub const MAX_REVOCATION_REPLY_BYTES: u32 = 2 * 1024 * 1024;
/// How long one attempt may take by default, in milliseconds.
pub const DEFAULT_TIMEOUT_MS: u32 = 10_000;
/// How long to wait before another attempt, in milliseconds.
pub const RETRY_DELAY_MS: f64 = 250.0;

/// One header, as sent.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct TransportHeader {
    pub name: String,
    pub value: String,
}

/// One HTTP POST.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct TransportRequest {
    pub url: String,
    /// The `content-type` header: `application/timestamp-query` or
    /// `application/json`.
    pub content_type: String,
    /// More headers, in order: an API key, a session token.
    pub headers: Vec<TransportHeader>,
    pub body: Vec<u8>,
    /// The host gives up after this long and answers `TimedOut`.
    pub timeout_ms: u32,
    /// The host stops reading past this many bytes and answers `TooLarge`.
    pub max_reply_bytes: u32,
}

/// What the host got back.
#[derive(Clone, Debug, PartialEq, Eq)]
pub enum TransportOutcome {
    /// A reply came, whatever its status.
    Replied { status: u16, body: Vec<u8> },
    /// No reply within `timeout_ms`.
    TimedOut,
    /// The reply ran past `max_reply_bytes`.
    TooLarge,
    /// No reply: the connection failed, the name did not resolve, the
    /// request was refused before it left. `message` is the host's words.
    Failed { message: String },
}

/// The transport a host implements. A rejected promise counts as
/// `Failed` with the error's message.
pub trait HttpTransport {
    fn post(&self, request: TransportRequest) -> Promise<TransportOutcome>;
}

/// A reply with a status the caller reads (`2xx`, or a `4xx` other than
/// 429), or why no answer came.
pub(crate) enum Exchange {
    Reply { status: u16, body: Vec<u8> },
    NoAnswer(String),
}

/// One attempt: a 5xx or 429 is no answer, as upstream's clients treat it.
async fn attempt(transport: &Rc<dyn HttpTransport>, request: &TransportRequest) -> Exchange {
    let outcome = match transport.post(request.clone()).await {
        Ok(outcome) => outcome,
        Err(error) => TransportOutcome::Failed { message: error.message },
    };
    let outcome = match outcome {
        // The route stops reading past its limit, so a body a host read
        // further than that is the same as one it broke off.
        TransportOutcome::Replied { body, .. } if body.len() > request.max_reply_bytes as usize => {
            TransportOutcome::TooLarge
        }
        outcome => outcome,
    };
    match outcome {
        TransportOutcome::Replied { status, body } => Exchange::Reply { status, body },
        TransportOutcome::TimedOut => {
            Exchange::NoAnswer(format!("no reply within {} ms", request.timeout_ms))
        }
        TransportOutcome::TooLarge => Exchange::NoAnswer(format!(
            "the reply broke off: it is over {} bytes",
            request.max_reply_bytes
        )),
        TransportOutcome::Failed { message } => Exchange::NoAnswer(message),
    }
}

/// Whether a status is one upstream's clients try again after.
pub(crate) fn retried(status: u16) -> bool {
    status >= 500 || status == 429
}

/// Send `request`, trying again `retries` more times while no answer comes
/// (a failure, a timeout, a 5xx or a 429), `RETRY_DELAY_MS` apart. `describe`
/// turns a status the caller retries on into the no-answer text.
pub(crate) async fn post_with_retries(
    transport: &Rc<dyn HttpTransport>,
    request: &TransportRequest,
    retries: u32,
    retried_status: impl Fn(u16, &[u8]) -> String,
) -> Result<(u16, Vec<u8>), String> {
    if request.body.len() > MAX_REQUEST_BYTES {
        return Err(format!(
            "the request is {} bytes, over the {MAX_REQUEST_BYTES} a route accepts",
            request.body.len()
        ));
    }
    let mut attempt_number = 0;
    loop {
        let failed = match attempt(transport, request).await {
            Exchange::Reply { status, body } if retried(status) => retried_status(status, &body),
            Exchange::Reply { status, body } => return Ok((status, body)),
            Exchange::NoAnswer(why) => why,
        };
        if attempt_number >= retries {
            return Err(failed);
        }
        attempt_number += 1;
        rt::sleep(RETRY_DELAY_MS).await.map_err(|e: Error| e.message)?;
    }
}

/// A transport that answers from a list of recorded replies, in order, and
/// keeps every request it was sent: the harness's and the tests'.
#[derive(Default)]
pub struct RecordedTransport {
    replies: std::cell::RefCell<std::collections::VecDeque<TransportOutcome>>,
    sent: std::cell::RefCell<Vec<TransportRequest>>,
}

impl RecordedTransport {
    pub fn new(replies: Vec<TransportOutcome>) -> RecordedTransport {
        RecordedTransport {
            replies: std::cell::RefCell::new(replies.into()),
            sent: Default::default(),
        }
    }

    /// Every request sent, in order.
    pub fn sent(&self) -> Vec<TransportRequest> {
        self.sent.borrow().clone()
    }
}

impl HttpTransport for RecordedTransport {
    fn post(&self, request: TransportRequest) -> Promise<TransportOutcome> {
        let limit = request.max_reply_bytes as usize;
        self.sent.borrow_mut().push(request);
        match self.replies.borrow_mut().pop_front() {
            // A host stops reading past the limit.
            Some(TransportOutcome::Replied { body, .. }) if body.len() > limit => {
                Promise::resolved(TransportOutcome::TooLarge)
            }
            Some(outcome) => Promise::resolved(outcome),
            None => Promise::resolved(TransportOutcome::Failed {
                message: "no recorded reply is left".into(),
            }),
        }
    }
}
