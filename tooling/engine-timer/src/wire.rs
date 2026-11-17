//! One open document behind a [`Host`], spoken to in wire JSON.

use std::time::{Duration, Instant};

use epdf_engine::clock::{FixedClock, Timestamp};
use epdf_engine::host::{Host, Payloads};
use epdf_engine::ids::SeededIdSource;
use serde_json::{Value, json};

/// A request's answer: its `result` or its `error`, and its payloads.
pub struct Answer {
    pub outcome: Result<Value, Value>,
    pub payloads: Payloads,
    /// The response exactly as the host wrote it, for the answer digest.
    pub raw: String,
}

/// A host with one document open as `doc_id`.
pub struct Session {
    pub host: Host,
    pub doc_id: String,
    next_job: u64,
}

impl Session {
    /// Opens `bytes` in a fresh host. Panics, naming `what`, when the open is
    /// refused: nothing after it would mean anything.
    ///
    /// The host's clock and ids are fixed, so a build answers the same bytes
    /// on every run and two builds' answers can be compared.
    pub fn open(bytes: Vec<u8>, password: Option<&str>, what: &str) -> Self {
        let host = Host::new().with_determinism(
            Box::new(FixedClock(Timestamp::utc(1_700_000_000_000))),
            Box::new(SeededIdSource::new(1)),
        );
        let mut session = Self { host, doc_id: "timed".to_owned(), next_job: 1 };
        let answer =
            session.send(json!({ "kind": "open.fatMem", "password": password }), vec![bytes]);
        if let Err(error) = answer.outcome {
            panic!("{what} would not open: {error}");
        }
        session
    }

    /// `body` with this session's `docId`, a fresh `jobId` and `opId`, and the
    /// rest of the envelope a client sends ([`Host::fill_envelope`]), as text.
    pub fn request(&mut self, mut body: Value) -> String {
        let job = self.next_job;
        self.next_job += 1;
        if let Value::Object(fields) = &mut body {
            fields.entry("docId").or_insert_with(|| json!(self.doc_id));
            fields.insert("jobId".into(), json!(job));
            fields.entry("opId").or_insert_with(|| json!(format!("op-{job}")));
        }
        self.host.fill_envelope(&mut body);
        body.to_string()
    }

    /// Sends `body` untimed.
    pub fn send(&mut self, body: Value, payloads: Payloads) -> Answer {
        let request = self.request(body);
        self.call(&request, payloads).0
    }

    /// Sends an already written request, timing [`Host::call`] alone.
    pub fn call(&mut self, request: &str, payloads: Payloads) -> (Answer, Duration) {
        let start = Instant::now();
        let response = self.host.call(request, payloads);
        let elapsed = start.elapsed();
        let mut envelope: Value =
            serde_json::from_str(&response.json).expect("every response is JSON");
        let outcome = match envelope.get_mut("error") {
            Some(error) => Err(error.take()),
            None => Ok(envelope.get_mut("result").map(Value::take).unwrap_or(Value::Null)),
        };
        (Answer { outcome, payloads: response.payloads, raw: response.json }, elapsed)
    }

    /// Sends `body` untimed, panicking with `what` on a refusal.
    pub fn expect(&mut self, body: Value, what: &str) -> Value {
        match self.send(body, Vec::new()).outcome {
            Ok(result) => result,
            Err(error) => panic!("{what}: {error}"),
        }
    }
}

/// FNV-1a, 64-bit: enough to tell two builds' answers apart, with no
/// dependency.
#[derive(Clone, Copy)]
pub struct Digest(u64);

impl Default for Digest {
    fn default() -> Self {
        Self(0xcbf2_9ce4_8422_2325)
    }
}

impl Digest {
    pub fn add(&mut self, bytes: &[u8]) {
        for byte in bytes {
            self.0 ^= u64::from(*byte);
            self.0 = self.0.wrapping_mul(0x0100_0000_01b3);
        }
        // A separator, so ["ab", "c"] and ["a", "bc"] differ.
        self.0 ^= 0xff;
        self.0 = self.0.wrapping_mul(0x0100_0000_01b3);
    }

    /// The answer's JSON at the corpus quantum (`test_support::corpus::quantized`),
    /// and its payloads exactly. Two builds can round a coordinate's last
    /// bit differently, and the vectors don't pin that either.
    pub fn add_answer(&mut self, answer: &Answer) {
        let envelope: Value = serde_json::from_str(&answer.raw).expect("every response is JSON");
        let quantized = test_support::corpus::quantized(&envelope);
        self.add(quantized.to_string().as_bytes());
        for payload in &answer.payloads {
            self.add(payload);
        }
    }

    pub fn hex(self) -> String {
        format!("{:016x}", self.0)
    }
}
