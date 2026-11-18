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
