//! The timed rows: one operation on one fixture, run `warmup` times untimed
//! and then `iterations` times timed, in a fresh host per row.
//!
//! Only [`Host::call`] is inside the clock. Writing the request and reading
//! the response are outside it, and so is any untimed setup a row needs.
//! Every answer feeds the row's digest, which `compare.mjs` requires to be
//! the same for both builds.
//!
//! [`Host::call`]: epdf_engine::host::Host::call

use std::path::Path;
use std::time::Duration;

use serde_json::{Value, json};

use crate::wire::{Answer, Digest, Session};

/// What one row produced in one process.
pub struct RowResult {
    pub row: String,
    pub samples: Vec<Duration>,
    pub digest: Digest,
    /// Every answer as text, then a tab and its payloads' digest, when the
    /// round keeps them.
    pub answers: Vec<String>,
}

/// A row's answers: always digested, and kept as text when asked.
#[derive(Default)]
struct Answers {
    digest: Digest,
    kept: Option<Vec<String>>,
}

impl Answers {
    fn add(&mut self, answer: &Answer) {
        self.digest.add_answer(answer);
        if let Some(kept) = &mut self.kept {
            let mut payloads = Digest::default();
            for payload in &answer.payloads {
                payloads.add(payload);
            }
            kept.push(format!("{}\t{}", answer.raw, payloads.hex()));
        }
    }
}

#[derive(Clone, Copy)]
enum Op {
    Open,
    PagesList,
    PagesGeometry,
    PagesRender,
    SearchQuery,
    AnnotationsList,
    FormsSetValue,
}

impl Op {
    fn name(self) -> &'static str {
        match self {
            Op::Open => "open",
            Op::PagesList => "pages.list",
            Op::PagesGeometry => "pages.geometry",
            Op::PagesRender => "pages.render",
            Op::SearchQuery => "search.query",
            Op::AnnotationsList => "annotations.list",
            Op::FormsSetValue => "forms.setValue",
        }
    }
}

/// One row: the operation, the fixture, and how many calls each round makes.
/// The counts keep a round of every row under ten seconds.
struct Row {
    fixture: &'static str,
    op: Op,
    warmup: usize,
    iterations: usize,
}

const fn row(fixture: &'static str, op: Op, warmup: usize, iterations: usize) -> Row {
    Row { fixture, op, warmup, iterations }
}

const ROWS: &[Row] = &[
    row("large", Op::Open, 3, 30),
    row("large", Op::PagesList, 3, 30),
    row("large", Op::PagesGeometry, 10, 100),
    row("large", Op::PagesRender, 5, 40),
    row("large", Op::SearchQuery, 1, 5),
    row("large", Op::AnnotationsList, 3, 30),
    row("annots", Op::Open, 2, 20),
    row("annots", Op::PagesList, 3, 30),
    row("annots", Op::PagesGeometry, 10, 100),
    row("annots", Op::PagesRender, 5, 40),
    row("annots", Op::SearchQuery, 1, 5),
    row("annots", Op::AnnotationsList, 2, 10),
    row("forms", Op::Open, 2, 20),
    row("forms", Op::PagesList, 3, 30),
    row("forms", Op::PagesGeometry, 10, 80),
    row("forms", Op::PagesRender, 5, 40),
    row("forms", Op::AnnotationsList, 3, 30),
    row("forms", Op::FormsSetValue, 4, 20),
];

/// The search every round makes. Each line of `large.pdf` and `annots.pdf`
/// holds it twice; `forms.pdf` has no text to search.
const SEARCH_TEXT: &str = "the";

/// Runs every row once, in order, on the fixtures in `dir`. `scale` divides
/// every row's call counts, for a smoke run. `keep` keeps every answer.
pub fn run(dir: &Path, scale: usize, keep: bool, mut report: impl FnMut(RowResult)) {
    for row in ROWS {
        let path = dir.join(format!("{}.pdf", row.fixture));
        let bytes = std::fs::read(&path).unwrap_or_else(|e| panic!("read {}: {e}", path.display()));
        let warmup = (row.warmup / scale).max(1);
        let iterations = (row.iterations / scale).max(2);
        let name = format!("{}/{}", row.fixture, row.op.name());
        let mut answers = Answers { digest: Digest::default(), kept: keep.then(Vec::new) };
        let samples = time(row.op, bytes, warmup, iterations, &name, &mut answers);
        let digest = answers.digest;
        let answers = answers.kept.unwrap_or_default();
        report(RowResult { row: name, samples, digest, answers });
    }
}

fn time(
    op: Op,
    bytes: Vec<u8>,
    warmup: usize,
    iterations: usize,
    name: &str,
    answers: &mut Answers,
) -> Vec<Duration> {
    let mut session = Session::open(bytes.clone(), None, name);
    let calls = warmup + iterations;
    let requests: Vec<(String, Vec<Vec<u8>>)> = match op {
        Op::Open => (0..calls)
            .map(|i| {
                let body = json!({ "kind": "open.fatMem", "docId": format!("open-{i}"), "password": null });
                (session.request(body), vec![bytes.clone()])
            })
            .collect(),
        Op::PagesList => repeat(&mut session, json!({ "kind": "pages.list" }), calls),
        Op::SearchQuery => return search(&mut session, warmup, iterations, name, answers),
        Op::AnnotationsList => repeat(&mut session, json!({ "kind": "annotations.list" }), calls),
        // A different page each call: a page the engine has already read is
        // a cache lookup, not the work a viewer waits for on scroll.
        Op::PagesGeometry => per_page(&mut session, calls, name, |page| {
            json!({ "kind": "pages.geometry", "page": page })
        }),
        Op::PagesRender => per_page(&mut session, calls, name, |page| {
            json!({ "kind": "pages.render", "page": page,
                "options": { "viewport": { "kind": "scale", "scale": 1 } } })
        }),
        Op::FormsSetValue => {
            let form = session.expect(json!({ "kind": "forms.list" }), name);
            let text: Vec<Value> = form["fields"]
                .as_array()
                .expect("fields")
                .iter()
                .filter(|field| field["kind"] == "text")
                .map(|field| field["ref"].clone())
                .collect();
            assert!(!text.is_empty(), "{name}: no text fields");
            (0..calls)
                .map(|i| {
                    let body = json!({ "kind": "forms.setValue", "ref": text[i % text.len()],
                        "value": { "value": format!("value {i}") } });
                    (session.request(body), Vec::new())
                })
                .collect()
        }
    };

    let mut samples = Vec::with_capacity(iterations);
    for (i, (request, payloads)) in requests.into_iter().enumerate() {
        let (answer, elapsed) = session.call(&request, payloads);
        if let Err(error) = &answer.outcome {
            panic!("{name}: call {i} refused: {error}");
        }
        answers.add(&answer);
        if matches!(op, Op::Open) {
            let doc = format!("open-{i}");
            let close = json!({ "kind": "close", "docId": doc });
            let closed = session.send(close, Vec::new());
            if let Err(error) = closed.outcome {
                panic!("{name}: close {i} refused: {error}");
            }
        }
        if i >= warmup {
            samples.push(elapsed);
        }
    }
    samples
}

/// A whole-document search, which is a chain of budgeted slices: each sample
/// is the chain's [`Host::call`] time, summed, from the first slice to the
/// one that answers no `nextCursor`.
///
/// [`Host::call`]: epdf_engine::host::Host::call
fn search(
    session: &mut Session,
    warmup: usize,
    iterations: usize,
    name: &str,
    answers: &mut Answers,
) -> Vec<Duration> {
    let mut samples = Vec::with_capacity(iterations);
    for i in 0..warmup + iterations {
        let mut total = Duration::ZERO;
        let mut cursor = Value::Null;
        loop {
            let mut query = json!({ "text": SEARCH_TEXT });
            if !cursor.is_null() {
                query["cursor"] = cursor;
            }
            let request = session.request(json!({ "kind": "search.query", "request": query }));
            let (answer, elapsed) = session.call(&request, Vec::new());
            total += elapsed;
            answers.add(&answer);
            match answer.outcome {
                Ok(slice) => cursor = slice["nextCursor"].clone(),
                Err(error) => panic!("{name}: search {i} refused: {error}"),
            }
            if cursor.is_null() {
                break;
            }
        }
        if i >= warmup {
            samples.push(total);
        }
    }
    samples
}

fn repeat(session: &mut Session, body: Value, calls: usize) -> Vec<(String, Vec<Vec<u8>>)> {
    (0..calls).map(|_| (session.request(body.clone()), Vec::new())).collect()
}

/// One request per page, in document order, for `calls` pages.
fn per_page(
    session: &mut Session,
    calls: usize,
    name: &str,
    body: impl Fn(&Value) -> Value,
) -> Vec<(String, Vec<Vec<u8>>)> {
    let listing = session.expect(json!({ "kind": "pages.list" }), name);
    let pages = listing["pages"].as_array().expect("pages");
    assert!(
        pages.len() >= calls,
        "{name}: {calls} calls need as many pages, the fixture has {}",
        pages.len()
    );
    pages.iter().take(calls).map(|page| (session.request(body(&page["ref"])), Vec::new())).collect()
}
