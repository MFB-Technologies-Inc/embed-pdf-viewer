//! The D-1 transport's limits, which no vector can show: upstream's browser
//! clients leave them to the route (CU-C4b-D3).

use std::rc::Rc;

use epdf_kernel::rt::Promise;
use epdf_signature_core::revocation::http_source::{
    HttpRevocationSourceOptions, http_revocation_source,
};
use epdf_signature_core::revocation::source::RevocationRequest;
use epdf_signature_core::transport::{RecordedTransport, TransportOutcome};

fn block_on<T: Clone + 'static>(promise: Promise<T>) -> Result<T, epdf_kernel::Error> {
    epdf_kernel::rt::run_until_stalled();
    promise.peek().expect("settled")
}

fn fetch(max_reply_bytes: Option<u32>, body: Vec<u8>) -> Result<Vec<u8>, epdf_kernel::Error> {
    let transport =
        Rc::new(RecordedTransport::new(vec![TransportOutcome::Replied { status: 200, body }]));
    let source = http_revocation_source(
        transport,
        HttpRevocationSourceOptions {
            url: "https://app.example/revocation".into(),
            headers: vec![],
            timeout_ms: None,
            retries: Some(0),
            max_reply_bytes,
        },
    );
    block_on(source.fetch(RevocationRequest::Crl { url: "http://crl.example/a.crl".into() }))
}

#[test]
fn a_revocation_answer_over_its_limit_is_no_answer() {
    assert_eq!(fetch(Some(4), vec![1, 2, 3, 4]).unwrap(), vec![1, 2, 3, 4]);
    let error = fetch(Some(4), vec![1, 2, 3, 4, 5]).unwrap_err();
    assert_eq!(error.code(), Some("Network"));
    assert_eq!(
        error.message,
        "http://crl.example/a.crl did not answer through https://app.example/revocation: the reply broke off: it is over 4 bytes"
    );
    // The default is the revocation route's 2 MiB.
    assert!(fetch(None, vec![0; 2 * 1024 * 1024]).is_ok());
    assert!(fetch(None, vec![0; 2 * 1024 * 1024 + 1]).is_err());
}

/// A host that reads past `max_reply_bytes` and hands the whole body over.
struct OverReading(Vec<u8>);

impl epdf_signature_core::transport::HttpTransport for OverReading {
    fn post(
        &self,
        _: epdf_signature_core::transport::TransportRequest,
    ) -> Promise<TransportOutcome> {
        Promise::resolved(TransportOutcome::Replied { status: 200, body: self.0.clone() })
    }
}

#[test]
fn a_reply_over_the_limit_is_no_answer_even_when_the_host_read_it_all() {
    let source = http_revocation_source(
        Rc::new(OverReading(vec![1, 2, 3, 4, 5])),
        HttpRevocationSourceOptions {
            url: "https://app.example/revocation".into(),
            headers: vec![],
            timeout_ms: None,
            retries: Some(0),
            max_reply_bytes: Some(4),
        },
    );
    let error =
        block_on(source.fetch(RevocationRequest::Crl { url: "http://crl.example/a.crl".into() }))
            .unwrap_err();
    assert_eq!(error.code(), Some("Network"));
    assert_eq!(
        error.message,
        "http://crl.example/a.crl did not answer through https://app.example/revocation: the reply broke off: it is over 4 bytes"
    );
}

/// A host that reads past `max_reply_bytes` and answers `status` with the
/// whole body, counting its requests.
struct OverReadingWith(u16, Vec<u8>, std::cell::Cell<u32>);

impl epdf_signature_core::transport::HttpTransport for OverReadingWith {
    fn post(
        &self,
        _: epdf_signature_core::transport::TransportRequest,
    ) -> Promise<TransportOutcome> {
        self.2.set(self.2.get() + 1);
        Promise::resolved(TransportOutcome::Replied { status: self.0, body: self.1.clone() })
    }
}

fn source_over(
    transport: Rc<dyn epdf_signature_core::transport::HttpTransport>,
) -> Result<Vec<u8>, epdf_kernel::Error> {
    let source = http_revocation_source(
        transport,
        HttpRevocationSourceOptions {
            url: "https://app.example/revocation".into(),
            headers: vec![],
            timeout_ms: None,
            retries: Some(1),
            max_reply_bytes: Some(4),
        },
    );
    block_on(source.fetch(RevocationRequest::Crl { url: "http://crl.example/a.crl".into() }))
}

/// M1: a refusal whose body runs past the limit is still a refusal, given
/// at once, not a retried no-answer: the client decides by the status.
#[test]
fn a_refusal_over_the_limit_is_its_status_with_no_body() {
    let host =
        Rc::new(OverReadingWith(404, b"{\"message\":\"gone\"}".to_vec(), Default::default()));
    let error = source_over(host.clone()).unwrap_err();
    assert_eq!(error.code(), Some("SignatureRefused"));
    assert_eq!(
        error.message,
        "the route at https://app.example/revocation refused to ask http://crl.example/a.crl: HTTP 404"
    );
    assert_eq!(host.2.get(), 1, "a 404 is not tried again");

    // The same through the recorded transport.
    let recorded = Rc::new(RecordedTransport::new(vec![TransportOutcome::Replied {
        status: 404,
        body: b"{\"message\":\"gone\"}".to_vec(),
    }]));
    let error = source_over(recorded.clone()).unwrap_err();
    assert_eq!(error.code(), Some("SignatureRefused"));
    assert_eq!(recorded.sent().len(), 1);

    // A 2xx past the limit is still no answer, and is tried again.
    let host = Rc::new(OverReadingWith(200, vec![1, 2, 3, 4, 5], Default::default()));
    let source = http_revocation_source(
        host.clone(),
        HttpRevocationSourceOptions {
            url: "https://app.example/revocation".into(),
            headers: vec![],
            timeout_ms: None,
            retries: Some(1),
            max_reply_bytes: Some(4),
        },
    );
    let fetched = source.fetch(RevocationRequest::Crl { url: "http://crl.example/a.crl".into() });
    epdf_kernel::rt::run_until_stalled();
    epdf_kernel::rt::advance(epdf_signature_core::transport::RETRY_DELAY_MS);
    let error = block_on(fetched).unwrap_err();
    assert_eq!(error.code(), Some("Network"));
    assert_eq!(host.2.get(), 2);
}
