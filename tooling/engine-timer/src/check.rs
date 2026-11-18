//! The answer check: every timed operation, sent through [`Host::call`] on the
//! conformance corpus, answers what `tooling/engine-conformance/vectors`
//! recorded from the TypeScript engine.
//!
//! The comparisons are the engine's own vector tests' (`pages_corpus.rs`,
//! `geometry_corpus.rs`, `annotation_reader.rs`, `form_write.rs`), so a build
//! that fails here fails those. Rendering and search have no wire-level
//! vectors: a render is checked against `render/geometry.json`'s device
//! sizes, and a search against the occurrences in `pages.text`'s recorded
//! text.
//!
//! [`Host::call`]: epdf_engine::host::Host::call

use std::path::{Path, PathBuf};

use serde_json::{Value, json};
use test_support::corpus::{first_difference, quantized};

use crate::wire::Session;

const VECTORS: &str = "tooling/engine-conformance/vectors";

/// The corpus under `root`, which is a checkout or a copy holding the vector
/// files and the fixtures they name at their repository paths.
pub struct Corpus {
    root: PathBuf,
}

impl Corpus {
    pub fn new(root: &Path) -> Self {
        Self { root: root.to_owned() }
    }

    fn vector(&self, relative: &str) -> Value {
        let path = self.root.join(VECTORS).join(relative);
        let text = std::fs::read_to_string(&path)
            .unwrap_or_else(|e| panic!("read {}: {e}", path.display()));
        let file: Value =
            serde_json::from_str(&text).unwrap_or_else(|e| panic!("parse {}: {e}", path.display()));
        assert_eq!(file["schemaVersion"], 1, "{} has another envelope", path.display());
        file
    }

    /// Every `*.json` under `operation`'s directory, sorted.
    fn vectors(&self, operation: &str) -> Vec<(String, Value)> {
        let dir = self.root.join(VECTORS).join(operation);
        let mut names: Vec<String> = std::fs::read_dir(&dir)
            .unwrap_or_else(|e| panic!("read {}: {e}", dir.display()))
            .map(|entry| entry.expect("dir entry").file_name().to_string_lossy().into_owned())
            .filter(|name| name.ends_with(".json"))
            .collect();
        names.sort();
        assert!(!names.is_empty(), "no vectors in {}", dir.display());
        names
            .into_iter()
            .map(|name| {
                let file = self.vector(&format!("{operation}/{name}"));
                (format!("{operation}/{name}"), file)
            })
            .collect()
    }

    fn fixture(&self, relative: &str) -> Vec<u8> {
        let path = self.root.join(relative);
        std::fs::read(&path).unwrap_or_else(|e| panic!("read {}: {e}", path.display()))
    }

    fn open(&self, fixture: &str, password: Option<&str>) -> Session {
        Session::open(self.fixture(fixture), password, fixture)
    }

    /// Every file the check reads, repository-relative, for copying the
    /// corpus to a machine without a checkout.
    pub fn files(&self) -> Vec<String> {
        let mut files = vec![
            format!("{VECTORS}/metadata/pages.json"),
            format!("{VECTORS}/render/geometry.json"),
            format!("{VECTORS}/pages.text/report.json"),
            format!("{VECTORS}/annotation/read.json"),
            format!("{VECTORS}/forms/write.json"),
        ];
        let mut fixtures = Vec::new();
        for (name, file) in self.vectors("pages.geometry") {
            files.push(format!("{VECTORS}/{name}"));
            fixtures.push(file["fixture"].as_str().expect("fixture").to_owned());
        }
        for case in self.vector("metadata/pages.json")["cases"].as_array().expect("cases") {
            fixtures.push(case["fixture"].as_str().expect("fixture").to_owned());
        }
        for case in self.vector("annotation/read.json")["cases"].as_array().expect("cases") {
            fixtures.push(case["fixture"].as_str().expect("fixture").to_owned());
        }
        fixtures.push(self.vector("forms/write.json")["fixture"].as_str().expect("f").to_owned());
        fixtures
            .push(self.vector("pages.text/report.json")["fixture"].as_str().expect("f").to_owned());
        fixtures.sort();
        fixtures.dedup();
        files.extend(fixtures);
        files
    }
}

/// One operation's check: adds its failures and answers how many answers it
/// compared.
type Check = fn(&Corpus, &mut Vec<String>) -> usize;

/// Runs every check, printing one line per operation. Answers the failures.
pub fn run(corpus: &Corpus) -> Vec<String> {
    let mut failures = Vec::new();
    let checks: [(&str, Check); 6] = [
        ("open + pages.list", pages_list),
        ("pages.geometry", pages_geometry),
        ("pages.render", pages_render),
        ("search.query", search_query),
        ("annotations.list", annotations_list),
        ("forms.setValue", forms_set_value),
    ];
    for (name, check) in checks {
        let before = failures.len();
        let compared = check(corpus, &mut failures);
        let failed = failures.len() - before;
        if compared == 0 {
            failures.push(format!("{name}: compared nothing"));
        }
        println!("check {name}: {compared} compared, {failed} failed");
    }
    failures
}

/// `metadata/pages.json`: the listing, quantized, as `pages_corpus.rs`
/// compares it.
fn pages_list(corpus: &Corpus, failures: &mut Vec<String>) -> usize {
    let file = corpus.vector("metadata/pages.json");
    let mut compared = 0;
    for case in file["cases"].as_array().expect("cases") {
        if case.get("password").is_some_and(|p| !p.is_null()) {
            continue;
        }
        let name = case["name"].as_str().unwrap_or_default();
        let mut session = corpus.open(case["fixture"].as_str().expect("fixture"), None);
        match session.send(json!({ "kind": "pages.list" }), Vec::new()).outcome {
            Ok(result) => {
                if let Some(d) = first_difference(&quantized(&case["pages"]), &quantized(&result)) {
                    failures.push(format!("pages.list {name}: {d}"));
                }
            }
            Err(error) => failures.push(format!("pages.list {name}: {error}")),
        }
        compared += 1;
    }
    compared
}

/// `pages.geometry/*.json`: every corpus page, quantized, as
/// `geometry_corpus.rs` compares it.
fn pages_geometry(corpus: &Corpus, failures: &mut Vec<String>) -> usize {
    let mut compared = 0;
    for (name, file) in corpus.vectors("pages.geometry") {
        let password = file["password"].as_str();
        let mut session = corpus.open(file["fixture"].as_str().expect("fixture"), password);
        for page in file["pages"].as_array().expect("pages") {
            let request = json!({ "kind": "pages.geometry",
                "page": { "kind": "objectNumber", "objectNumber": page["pageObjectNumber"] } });
            match session.send(request, Vec::new()).outcome {
                Ok(result) => {
                    let expected = quantized(&page["snapshot"]);
                    if let Some(d) = first_difference(&expected, &quantized(&result)) {
                        failures.push(format!("pages.geometry {name} page {}: {d}", page["index"]));
                    }
                }
                Err(error) => failures.push(format!("pages.geometry {name}: {error}")),
            }
            compared += 1;
        }
    }
    compared
}

/// `render/geometry.json`: the raster a wire render answers is the size the
/// TypeScript engine computes for the same region, viewport and rotation.
///
/// Rendered on the first unrotated US Letter page of `metadata/pages.json`'s
/// corpus whose crop box sits at the origin, which is the page the vectors
/// assume. Cases on another page height are left out.
fn pages_render(corpus: &Corpus, failures: &mut Vec<String>) -> usize {
    let listing = corpus.vector("metadata/pages.json");
    let letter = listing["cases"]
        .as_array()
        .expect("cases")
        .iter()
        .filter(|case| case.get("password").is_none_or(Value::is_null))
        .find_map(|case| {
            let page = &case["pages"]["pages"][0];
            let crop = &page["pdfCropBox"];
            let fits = page["rotation"] == 0
                && crop == &json!({ "left": 0, "right": 612, "bottom": 0, "top": 792 });
            fits.then(|| {
                (case["fixture"].as_str().expect("fixture").to_owned(), page["ref"].clone())
            })
        })
        .expect("a US Letter page in metadata/pages.json");
    let (fixture, page) = letter;
    let mut session = corpus.open(&fixture, None);

    let file = corpus.vector("render/geometry.json");
    let mut compared = 0;
    for case in file["cases"].as_array().expect("cases") {
        if case["pageHeight"] != 792 {
            continue;
        }
        let name = case["name"].as_str().unwrap_or_default();
        let rect = &case["rect"];
        let whole = rect == &json!({ "left": 0, "bottom": 0, "right": 612, "top": 792 });
        let mut options = json!({ "viewport": case["viewport"], "rotation": case["rotation"] });
        if !whole {
            // A thumbnail is always the whole page on the wire.
            if case["viewport"]["kind"] == "thumbnail" {
                continue;
            }
            // The vectors give the region in the file's coordinates; the
            // wire takes it in page space, from the crop box's top-left.
            let n = |v: &Value| v.as_f64().expect("a number");
            options["target"] = json!({ "kind": "rect", "rect": {
                "x": n(&rect["left"]),
                "y": 792.0 - n(&rect["top"]),
                "width": n(&rect["right"]) - n(&rect["left"]),
                "height": n(&rect["top"]) - n(&rect["bottom"]),
            } });
        }
        let request = json!({ "kind": "pages.render", "page": page, "options": options });
        match session.send(request, Vec::new()).outcome {
            Ok(result) => {
                let raster = &result["raster"];
                let actual = json!({ "width": raster["width"], "height": raster["height"] });
                if actual != case["size"] {
                    failures.push(format!(
                        "pages.render {name}: {actual} where TypeScript computes {}",
                        case["size"]
                    ));
                }
            }
            Err(error) => failures.push(format!("pages.render {name}: {error}")),
        }
        compared += 1;
    }
    compared
}

/// Words in `report.pdf` whose case folding is plain ASCII and which no
/// ligature or hyphenated line break splits, so the recorded text counts
/// their matches directly.
const SEARCH_WORDS: [&str; 4] = ["trace", "type", "compiler", "javascript"];

/// `pages.text/report.json`: a search for each of [`SEARCH_WORDS`] matches
/// once per case-insensitive occurrence in the recorded text, on each page.
fn search_query(corpus: &Corpus, failures: &mut Vec<String>) -> usize {
    let file = corpus.vector("pages.text/report.json");
    let mut session = corpus.open(file["fixture"].as_str().expect("fixture"), None);
    let pages = file["pages"].as_array().expect("pages");
    let mut compared = 0;
    for word in SEARCH_WORDS {
        let expected: Vec<(Value, usize)> = pages
            .iter()
            .map(|page| {
                let text = page["snapshot"]["text"].as_str().expect("text").to_ascii_lowercase();
                (page["pageObjectNumber"].clone(), text.matches(word).count())
            })
            .collect();
        let request = json!({ "kind": "search.query", "request": { "text": word } });
        match session.send(request, Vec::new()).outcome {
            Ok(result) => {
                let matches = result["matches"].as_array().cloned().unwrap_or_default();
                for (page, count) in &expected {
                    let found = matches.iter().filter(|m| match_page(m) == Some(page)).count();
                    if found != *count {
                        failures.push(format!(
                            "search.query {word:?}: {found} matches on page {page}, the recorded text has {count}"
                        ));
                    }
                }
                if expected.iter().all(|(_, count)| *count == 0) {
                    failures.push(format!("search.query {word:?}: the recorded text never has it"));
                }
            }
            Err(error) => failures.push(format!("search.query {word:?}: {error}")),
        }
        compared += 1;
    }
    compared
}

/// The object number of the page a search match is on.
fn match_page(m: &Value) -> Option<&Value> {
    m.get("page").and_then(|page| page.get("objectNumber"))
}

/// `annotation/read.json`: each page's annotations, then its widgets, as
/// `annotation_reader.rs` compares them.
fn annotations_list(corpus: &Corpus, failures: &mut Vec<String>) -> usize {
    let file = corpus.vector("annotation/read.json");
    let mut compared = 0;
    for case in file["cases"].as_array().expect("cases") {
        let name = case["name"].as_str().unwrap_or_default();
        let mut session = corpus.open(case["fixture"].as_str().expect("fixture"), None);
        let list = session.expect(json!({ "kind": "annotations.list" }), name);
        let form = session.expect(json!({ "kind": "forms.list" }), name);
        let widgets = form["widgets"].as_array().cloned().unwrap_or_default();
        let annotations = list["annotations"].as_array().cloned().unwrap_or_default();
        let listed = list["pages"].as_array().cloned().unwrap_or_default();
        let recorded = case["pages"].as_array().expect("pages");
        if listed.len() != recorded.len() {
            failures.push(format!(
                "annotations.list {name}: {} pages, recorded {}",
                listed.len(),
                recorded.len()
            ));
            continue;
        }
        for (page, theirs) in listed.iter().zip(recorded) {
            if page != &theirs["page"] {
                failures.push(format!(
                    "annotations.list {name}: page {page} where {} was recorded",
                    theirs["page"]
                ));
                continue;
            }
            let mine: Vec<&Value> = annotations
                .iter()
                .chain(&widgets)
                .filter(|a| a["page"]["objectNumber"] == page["objectNumber"])
                .collect();
            let theirs = theirs["annotations"].as_array().expect("annotations");
            if mine.len() != theirs.len() {
                failures.push(format!(
                    "annotations.list {name} page {page}: {} annotations, recorded {}",
                    mine.len(),
                    theirs.len()
                ));
                continue;
            }
            for (index, (mine, theirs)) in mine.iter().zip(theirs).enumerate() {
                if test_support::canonical_numbers(mine) != test_support::canonical_numbers(theirs)
                {
                    failures.push(format!(
                        "annotations.list {name} page {page} annotation {index} differs"
                    ));
                }
                compared += 1;
            }
        }
    }
    compared
}

/// `forms/write.json`: each recorded request on a fresh copy of the fixture,
/// compared as `form_write.rs` compares it. Only the `forms.setValue` cases.
fn forms_set_value(corpus: &Corpus, failures: &mut Vec<String>) -> usize {
    let file = corpus.vector("forms/write.json");
    let fixture = file["fixture"].as_str().expect("fixture");
    let mut compared = 0;
    for case in file["cases"].as_array().expect("cases") {
        if case["request"]["kind"] != "forms.setValue" {
            continue;
        }
        let name = case["name"].as_str().unwrap_or_default();
        let mut session = corpus.open(fixture, None);
        let mut request = case["request"].clone();
        if let Value::Object(fields) = &mut request {
            // The session's own ids replace the recorded placeholders.
            for key in ["docId", "jobId", "opId"] {
                fields.remove(key);
            }
        }
        let outcome = session.send(request, Vec::new()).outcome;
        if case["refused"] == true {
            match outcome {
                Err(error) if error["code"] == case["refusedWith"] => {}
                other => failures.push(format!(
                    "forms.setValue {name}: {} where {} was recorded",
                    describe(&other),
                    case["refusedWith"],
                )),
            }
        } else {
            match outcome {
                Ok(answer) => {
                    // A write answers `{result, mutationSequence, event}`.
                    let result = &answer["result"];
                    let field = test_support::canonical_numbers(&result["field"]);
                    if field != test_support::canonical_numbers(&case["field"]) {
                        failures.push(format!("forms.setValue {name}: the field differs"));
                    }
                    let widgets = case.get("changedWidgets").cloned().unwrap_or(json!([]));
                    if result["meta"]["changedWidgets"] != widgets {
                        failures.push(format!("forms.setValue {name}: the changed widgets differ"));
                    }
                }
                Err(error) => failures.push(format!("forms.setValue {name}: {error}")),
            }
        }
        compared += 1;
    }
    compared
}

fn describe(outcome: &Result<Value, Value>) -> String {
    match outcome {
        Ok(_) => "accepted".to_owned(),
        Err(error) => format!("refused with {}", error["code"]),
    }
}
