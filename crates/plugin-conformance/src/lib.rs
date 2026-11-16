//! Replays `tooling/plugin-conformance/vectors/` against the Rust kernel and
//! plugins.
//!
//! The TypeScript generator is the oracle. This crate is the other half of
//! the same harness: the scenario runner ([`scenario`]), the fake engine
//! ([`fake_engine`]), the probe plugin ([`probe`]) and canonical comparison
//! ([`canonical`]), each a port of the file of the same name under
//! `tooling/plugin-conformance/src/`. A plugin crate adds a
//! [`harness::HarnessPlugin`] and replays its vectors with [`assert_suite`].

#![allow(clippy::type_complexity)]

pub mod canonical;
pub mod exceptions;
pub mod fake_engine;
pub mod harness;
/// Moved to the kernel, which prints actions and state with it.
pub use epdf_kernel::js;
pub mod probe;
pub mod receipt;
pub mod scenario;
pub mod test_kernel;

use std::path::{Path, PathBuf};

use serde_json::Value;

pub use harness::{Answer, Callback, Harness, HarnessPlugin, LiveFn, Outcome, ReleaseFn};
pub use scenario::Registry;

/// The envelope version the generator writes (`vectors.ts`).
pub const SCHEMA_VERSION: u64 = 2;

/// Drop object members that are JavaScript `undefined`, as JSON would. The
/// TypeScript's canonical form never prints them, so a Rust value carrying
/// one is the same value.
pub fn scrub(value: &Value) -> Value {
    match value {
        Value::Object(map) => Value::Object(
            map.iter()
                .filter(|(_, v)| !epdf_kernel::is_undefined(v))
                .map(|(k, v)| (k.clone(), scrub(v)))
                .collect(),
        ),
        Value::Array(items) => Value::Array(items.iter().map(scrub).collect()),
        other => other.clone(),
    }
}

pub fn vectors_root() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../../tooling/plugin-conformance/vectors")
}

/// The text of the vector suite `file`, named by its `.json` name relative
/// to [`vectors_root`] (an absolute path stands as it is): the
/// zstd-compressed `<file>.zst` decompressed when it exists, else `<file>`.
/// Panics naming the path when neither reads.
pub fn read_vector_text(file: &str) -> String {
    read_vector(file).1
}

/// [`read_vector_text`], with the path it read.
fn read_vector(file: &str) -> (PathBuf, String) {
    let path = vectors_root().join(file);
    #[cfg(not(target_arch = "wasm32"))]
    {
        let mut compressed = path.clone().into_os_string();
        compressed.push(".zst");
        let compressed = PathBuf::from(compressed);
        if compressed.exists() {
            let text = decompress(&compressed);
            return (compressed, text);
        }
    }
    let text =
        std::fs::read_to_string(&path).unwrap_or_else(|e| panic!("read {}: {e}", path.display()));
    (path, text)
}

#[cfg(not(target_arch = "wasm32"))]
fn decompress(path: &Path) -> String {
    use std::io::Read;
    let file = std::fs::File::open(path).unwrap_or_else(|e| panic!("read {}: {e}", path.display()));
    let mut decoder = ruzstd::decoding::StreamingDecoder::new(std::io::BufReader::new(file))
        .unwrap_or_else(|e| panic!("decompress {}: {e}", path.display()));
    let mut text = String::new();
    decoder
        .read_to_string(&mut text)
        .unwrap_or_else(|e| panic!("decompress {}: {e}", path.display()));
    text
}

pub fn load(file: &str) -> Value {
    let (path, text) = read_vector(file);
    let suite: Value =
        serde_json::from_str(&text).unwrap_or_else(|e| panic!("parse {}: {e}", path.display()));
    assert_eq!(suite["schemaVersion"].as_u64(), Some(SCHEMA_VERSION), "{file}: schema version");
    suite
}

/// Compare one case's trace, returning the first difference.
pub fn compare_trace(expected: &[Value], actual: &[Value]) -> Option<String> {
    for (index, (want, got)) in expected.iter().zip(actual.iter()).enumerate() {
        if let Some(diff) = canonical::diff_entry(want, got) {
            return Some(format!("step {index}: {diff}"));
        }
    }
    if expected.len() != actual.len() {
        return Some(format!("expected {} trace entries, got {}", expected.len(), actual.len()));
    }
    None
}

/// A pure function a function suite may call by name. Arguments and result
/// are canonical JSON.
pub type TableFn = fn(&[Value]) -> Result<Value, epdf_kernel::Error>;
pub type FunctionTable = Vec<(&'static str, TableFn)>;

fn panic_message(panic: Box<dyn std::any::Any + Send>) -> String {
    panic
        .downcast_ref::<String>()
        .cloned()
        .or_else(|| panic.downcast_ref::<&str>().map(|s| s.to_string()))
        .unwrap_or_default()
}

/// Replay every case of a vector file; panic listing every case that
/// differs. `PLUGIN_CONFORMANCE_ONLY=<substring>` restricts the run to the
/// cases whose name contains it. `PLUGIN_CONFORMANCE_RECEIPTS=<dir>` writes
/// a [`receipt::Receipt`] of the run into `<dir>`.
pub fn assert_suite(file: &str, registry: &Registry, functions: &FunctionTable) {
    let only = std::env::var("PLUGIN_CONFORMANCE_ONLY").ok();
    let receipts = std::env::var_os("PLUGIN_CONFORMANCE_RECEIPTS").map(PathBuf::from);
    assert_loaded_suite(
        file,
        &load(file),
        registry,
        functions,
        only.as_deref(),
        receipts.as_deref(),
    );
}

/// [`assert_suite`] on an already loaded `suite`, with the case filter and
/// the receipt directory passed in rather than read from the environment.
pub fn assert_loaded_suite(
    file: &str,
    suite: &Value,
    registry: &Registry,
    functions: &FunctionTable,
    only: Option<&str>,
    receipts: Option<&Path>,
) {
    assert_suite_with_ledger(
        file,
        suite,
        registry,
        functions,
        only,
        receipts,
        exceptions::ledger(),
    );
}

/// [`assert_loaded_suite`] against `ledger` rather than `divergences.json`.
fn assert_suite_with_ledger(
    file: &str,
    suite: &Value,
    registry: &Registry,
    functions: &FunctionTable,
    only: Option<&str>,
    receipts: Option<&Path>,
    ledger: &[exceptions::Divergence],
) {
    let verbose = std::env::var("PLUGIN_CONFORMANCE_VERBOSE").is_ok();
    let mut failures = Vec::new();
    let mut ran = Vec::new();
    let mut exceptions_seen = Vec::new();
    let mut exceptions_applied = Vec::new();
    let mut member_order_allowed = Vec::new();
    for case in suite["cases"].as_array().expect("cases") {
        let name = case["name"].as_str().unwrap_or("?");
        if only.is_some_and(|o| !name.contains(o)) {
            continue;
        }
        ran.push(name.to_string());
        let allowed_paths = canonical::allowed_member_order(file, name);
        if !allowed_paths.is_empty() {
            member_order_allowed.push(name.to_string());
        }
        let _allowed = canonical::allow_member_order(allowed_paths);
        match suite["kind"].as_str() {
            Some("scenario") => {
                let run = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
                    scenario::run_case(case, registry)
                }));
                match run {
                    Err(panic) => {
                        failures.push(format!("{name}: panicked: {}", panic_message(panic)));
                        epdf_kernel::rt::reset();
                    }
                    Ok(mut run) => {
                        let expected = case["trace"].as_array().cloned().unwrap_or_default();
                        if verbose {
                            eprintln!(
                                "== {name}\n{}",
                                serde_json::to_string_pretty(&run.trace).unwrap()
                            );
                        }
                        let mut stale = None;
                        for (divergence, observation) in
                            exceptions::observations(ledger, file, name)
                        {
                            exceptions_seen.push(name.to_string());
                            match exceptions::apply(observation, &expected, &mut run.trace) {
                                Ok(()) => {
                                    if !exceptions_applied.contains(&divergence.id) {
                                        exceptions_applied.push(divergence.id.clone());
                                    }
                                }
                                Err(why) => stale = Some(format!("{}: {why}", divergence.id)),
                            }
                        }
                        if let Some(why) = stale {
                            let diff = compare_trace(&expected, &run.trace)
                                .unwrap_or_else(|| "no other difference".into());
                            failures.push(format!("{name}: {why}; trace: {diff}"));
                        } else if let Some(diff) = compare_trace(&expected, &run.trace) {
                            failures.push(format!("{name}: {diff}"));
                        } else if !run.unmatched.is_empty()
                            && case["allowUnmatched"].as_bool() != Some(true)
                        {
                            // The generator refuses the same case (`vectors.ts`).
                            failures.push(format!(
                                "{name}: unanswered engine calls:\n    {}",
                                run.unmatched.join("\n    ")
                            ));
                        }
                    }
                }
            }
            Some("function") => {
                let fn_name = case["fn"].as_str().unwrap_or_default();
                let Some((_, f)) = functions.iter().find(|(n, _)| *n == fn_name) else {
                    failures.push(format!("{name}: no Rust function \"{fn_name}\""));
                    continue;
                };
                let args = case["args"].as_array().cloned().unwrap_or_default();
                let run = std::panic::catch_unwind(|| f(&args));
                let outcome = match run {
                    Ok(Ok(value)) => serde_json::json!({ "returned": scrub(&value) }),
                    Ok(Err(error)) => serde_json::json!({ "threw": error.to_canonical() }),
                    Err(panic) => {
                        failures.push(format!("{name}: panicked: {}", panic_message(panic)));
                        continue;
                    }
                };
                if let Some(diff) = canonical::diff(&case["outcome"], &outcome) {
                    failures.push(format!("{name}: {diff}"));
                }
            }
            other => panic!("{file}: unknown suite kind {other:?}"),
        }
    }
    if let Some(dir) = receipts {
        let receipt = receipt::Receipt {
            file: file.to_string(),
            only: only.map(str::to_string),
            ran: ran.clone(),
            exceptions: exceptions_applied,
            member_order_allowed,
        };
        receipt::write_receipt(dir, &receipt)
            .unwrap_or_else(|e| panic!("{file}: write receipt into {}: {e}", dir.display()));
    }
    let ran = ran.len();
    assert!(ran > 0, "{file}: no case ran");
    if only.is_none() {
        for divergence in ledger {
            for observation in divergence.observed_by.iter().filter(|o| o.file == file) {
                if !exceptions_seen.contains(&observation.case) {
                    failures.push(format!(
                        "{}: {}: observation is stale: no scenario case of that name ran",
                        observation.case, divergence.id
                    ));
                }
            }
        }
    }
    if !failures.is_empty() {
        panic!(
            "{file}: {} of {ran} cases differ from the TypeScript:\n  {}",
            failures.len(),
            failures.join("\n  ")
        );
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    /// A fresh directory under the system temp dir, removed on drop.
    struct TempDir(PathBuf);

    impl TempDir {
        fn new(tag: &str) -> Self {
            let dir = std::env::temp_dir()
                .join(format!("plugin-conformance-{tag}-{}", std::process::id()));
            let _ = std::fs::remove_dir_all(&dir);
            std::fs::create_dir_all(&dir).unwrap();
            TempDir(dir)
        }
    }

    impl Drop for TempDir {
        fn drop(&mut self) {
            let _ = std::fs::remove_dir_all(&self.0);
        }
    }

    /// `tests/data/x.json.zst` is the generator's `writeVector` output
    /// (level 3) for a 40-case function suite that `describes` "from zst".
    #[test]
    fn reads_zst_before_json() {
        let dir = TempDir::new("reads-zst");
        let fixture = Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/data/x.json.zst");
        std::fs::copy(&fixture, dir.0.join("x.json.zst")).unwrap();
        std::fs::write(dir.0.join("x.json"), "{\"describes\": \"from json\"}\n").unwrap();
        let text = read_vector_text(dir.0.join("x.json").to_str().unwrap());
        let suite: Value = serde_json::from_str(&text).unwrap();
        assert_eq!(suite["describes"], "from zst");
        assert_eq!(suite["cases"].as_array().map(Vec::len), Some(40));
        assert_eq!(suite["cases"][39]["name"], "case-39");
    }

    /// A `.json.zst` that decompresses to text that is not JSON: the parse
    /// failure names the compressed file, the one actually read.
    #[cfg(not(target_arch = "wasm32"))]
    #[test]
    #[should_panic(expected = "x.json.zst: ")]
    fn load_names_the_zst_it_failed_to_parse() {
        let dir = TempDir::new("load-zst-parse");
        let bytes = ruzstd::encoding::compress_to_vec(
            &b"not json"[..],
            ruzstd::encoding::CompressionLevel::Fastest,
        );
        std::fs::write(dir.0.join("x.json.zst"), bytes).unwrap();
        load(dir.0.join("x.json").to_str().unwrap());
    }

    /// A ledger observation of a case the suite does not have fails the
    /// unfiltered replay, and only the observation of this file counts.
    #[test]
    fn an_observation_whose_case_never_ran_fails() {
        let suite = serde_json::json!({
            "schemaVersion": SCHEMA_VERSION,
            "kind": "function",
            "cases": [{ "name": "one", "fn": "id", "args": [1], "outcome": { "returned": 1 } }],
        });
        let functions: FunctionTable = vec![("id", |args: &[Value]| Ok(args[0].clone()))];
        let ledger = exceptions::parse(
            r#"[{ "id": "X1", "summary": "s", "reason": "r", "decided": "d",
                  "observedBy": [
                    { "file": "a/b.json", "case": "gone",
                      "valueAt": { "path": "$[0].x", "expected": 1, "actual": 2 } },
                    { "file": "other.json", "case": "elsewhere",
                      "valueAt": { "path": "$[0].x", "expected": 1, "actual": 2 } } ] }]"#,
        )
        .unwrap();
        let run = |only: Option<&str>| {
            std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
                assert_suite_with_ledger(
                    "a/b.json",
                    &suite,
                    &Registry::new(),
                    &functions,
                    only,
                    None,
                    &ledger,
                )
            }))
        };
        let message = panic_message(run(None).unwrap_err());
        assert!(
            message.contains("gone: X1: observation is stale: no scenario case of that name ran"),
            "{message}"
        );
        assert!(!message.contains("elsewhere"), "{message}");
        assert!(run(Some("one")).is_ok(), "a filtered run does not check observations");
    }

    #[test]
    fn reads_json_when_no_zst_exists() {
        let dir = TempDir::new("reads-json");
        std::fs::write(dir.0.join("x.json"), "{\"describes\": \"from json\"}\n").unwrap();
        let text = read_vector_text(dir.0.join("x.json").to_str().unwrap());
        assert_eq!(text, "{\"describes\": \"from json\"}\n");
    }
}
