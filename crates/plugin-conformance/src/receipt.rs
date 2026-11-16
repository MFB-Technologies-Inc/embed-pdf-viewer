//! Replay receipts: proof of which cases of a vector file actually ran.
//!
//! With `PLUGIN_CONFORMANCE_RECEIPTS=<dir>` set, every [`crate::assert_suite`]
//! call writes one receipt into `<dir>`, named
//! `<file with "/" replaced by "__">.<pid>.<n>.json`, where `n` counts the
//! receipts this process wrote. Two tests that replay the same file therefore
//! leave two receipts, and a reader merges them. A receipt is written even
//! when the suite fails, and records `only` when `PLUGIN_CONFORMANCE_ONLY`
//! filtered the run, so a checker can refuse a partial replay.

use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicUsize, Ordering};

use serde::{Deserialize, Serialize};

use crate::exceptions::MovedCallback;

/// What one [`crate::assert_suite`] call replayed.
#[derive(Clone, Debug, Default, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Receipt {
    /// The vector file, relative to the vectors root.
    pub file: String,
    /// The `PLUGIN_CONFORMANCE_ONLY` filter, when one was set.
    pub only: Option<String>,
    /// The names of the cases that ran, in file order.
    pub ran: Vec<String>,
    /// The ids of the outcome exceptions that were applied.
    pub exceptions: Vec<String>,
    /// The cases that ran with a member-order allowance.
    pub member_order_allowed: Vec<String>,
}

/// Write `receipt` into `dir` under a name no other receipt of this process
/// uses, and return its path. The text goes to a temporary name first and is
/// renamed into place, so a reader never sees half a receipt.
pub fn write_receipt(dir: &Path, receipt: &Receipt) -> std::io::Result<PathBuf> {
    static NEXT: AtomicUsize = AtomicUsize::new(0);
    let n = NEXT.fetch_add(1, Ordering::Relaxed);
    let name = format!("{}.{}.{n}.json", receipt.file.replace('/', "__"), std::process::id());
    std::fs::create_dir_all(dir)?;
    let path = dir.join(&name);
    let partial = dir.join(format!("{name}.partial"));
    std::fs::write(&partial, serde_json::to_vec_pretty(receipt)?)?;
    std::fs::rename(&partial, &path)?;
    Ok(path)
}

/// The id a receipt records for an applied outcome exception: the divergence
/// id its `reason` names after "Kept divergence ", else `<file>/<case>`.
pub fn exception_id(entry: &MovedCallback) -> String {
    entry
        .reason
        .strip_prefix("Kept divergence ")
        .and_then(|rest| rest.split_whitespace().next())
        .map(|token| token.trim_end_matches(|c: char| c.is_ascii_punctuation()))
        .filter(|id| !id.is_empty())
        .map(str::to_string)
        .unwrap_or_else(|| format!("{}/{}", entry.file, entry.case))
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::{Value, json};

    /// A fresh directory under the system temp dir, removed on drop.
    struct TempDir(PathBuf);

    impl TempDir {
        fn new(tag: &str) -> Self {
            static NEXT: AtomicUsize = AtomicUsize::new(0);
            let n = NEXT.fetch_add(1, Ordering::Relaxed);
            let dir = std::env::temp_dir()
                .join(format!("plugin-conformance-{tag}-{}-{n}", std::process::id()));
            let _ = std::fs::remove_dir_all(&dir);
            std::fs::create_dir_all(&dir).unwrap();
            TempDir(dir)
        }

        fn entries(&self) -> Vec<PathBuf> {
            let mut list: Vec<PathBuf> =
                std::fs::read_dir(&self.0).unwrap().map(|e| e.unwrap().path()).collect();
            list.sort();
            list
        }
    }

    impl Drop for TempDir {
        fn drop(&mut self) {
            let _ = std::fs::remove_dir_all(&self.0);
        }
    }

    fn receipt(ran: &[&str]) -> Receipt {
        Receipt {
            file: "a/b.json".into(),
            only: None,
            ran: ran.iter().map(|s| s.to_string()).collect(),
            exceptions: vec![],
            member_order_allowed: vec![],
        }
    }

    fn read(path: &Path) -> Receipt {
        serde_json::from_str(&std::fs::read_to_string(path).unwrap()).unwrap()
    }

    /// A one-case function suite that passes.
    fn one_case_suite() -> Value {
        json!({
            "schemaVersion": crate::SCHEMA_VERSION,
            "kind": "function",
            "cases": [{ "name": "one", "fn": "id", "args": [1], "outcome": { "returned": 1 } }],
        })
    }

    fn functions() -> crate::FunctionTable {
        vec![("id", |args: &[Value]| Ok(args[0].clone()))]
    }

    #[test]
    fn two_writes_for_one_file_keep_both() {
        let dir = TempDir::new("two-writes");
        let first = write_receipt(&dir.0, &receipt(&["case-a"])).unwrap();
        let second = write_receipt(&dir.0, &receipt(&["case-b"])).unwrap();
        assert_ne!(first, second);
        assert_eq!(dir.entries().len(), 2);
        assert_eq!(read(&first).ran, ["case-a"]);
        assert_eq!(read(&second).ran, ["case-b"]);
        let name = first.file_name().unwrap().to_str().unwrap();
        assert!(name.starts_with(&format!("a__b.json.{}.", std::process::id())), "{name}");
        assert!(name.ends_with(".json"), "{name}");
    }

    #[test]
    fn a_receipt_uses_the_wire_names() {
        let dir = TempDir::new("wire");
        let mut r = receipt(&["case-a"]);
        r.only = Some("case".into());
        r.exceptions = vec!["M13".into()];
        r.member_order_allowed = vec!["case-a".into()];
        let path = write_receipt(&dir.0, &r).unwrap();
        let value: Value = serde_json::from_str(&std::fs::read_to_string(path).unwrap()).unwrap();
        assert_eq!(
            value,
            json!({
                "file": "a/b.json",
                "only": "case",
                "ran": ["case-a"],
                "exceptions": ["M13"],
                "memberOrderAllowed": ["case-a"],
            })
        );
    }

    #[test]
    fn an_unfiltered_receipt_writes_only_as_null() {
        let dir = TempDir::new("null-only");
        let path = write_receipt(&dir.0, &receipt(&["case-a"])).unwrap();
        let value: Value = serde_json::from_str(&std::fs::read_to_string(path).unwrap()).unwrap();
        assert_eq!(value["only"], Value::Null);
    }

    #[test]
    fn no_env_writes_nothing() {
        let dir = TempDir::new("no-env");
        crate::assert_loaded_suite(
            "a/b.json",
            &one_case_suite(),
            &crate::Registry::new(),
            &functions(),
            None,
            None,
        );
        assert!(dir.entries().is_empty());
    }

    #[test]
    fn a_suite_writes_what_it_ran() {
        let dir = TempDir::new("ran");
        crate::assert_loaded_suite(
            "a/b.json",
            &one_case_suite(),
            &crate::Registry::new(),
            &functions(),
            None,
            Some(&dir.0),
        );
        let [path] = &dir.entries()[..] else { panic!("one receipt: {:?}", dir.entries()) };
        let r = read(path);
        assert_eq!((r.file.as_str(), r.only, r.ran), ("a/b.json", None, vec!["one".to_string()]));
    }

    #[test]
    fn a_failing_suite_still_writes_its_receipt() {
        let dir = TempDir::new("failing");
        let mut suite = one_case_suite();
        suite["cases"][0]["outcome"] = json!({ "returned": 2 });
        let run = std::panic::catch_unwind(|| {
            crate::assert_loaded_suite(
                "a/b.json",
                &suite,
                &crate::Registry::new(),
                &functions(),
                None,
                Some(&dir.0),
            )
        });
        assert!(run.is_err(), "the suite differs, so it fails");
        let [path] = &dir.entries()[..] else { panic!("one receipt: {:?}", dir.entries()) };
        assert_eq!(read(path).ran, ["one"]);
    }

    #[test]
    fn a_filtered_run_records_its_filter() {
        let dir = TempDir::new("only");
        let mut suite = one_case_suite();
        suite["cases"]
            .as_array_mut()
            .unwrap()
            .push(json!({ "name": "two", "fn": "id", "args": [2], "outcome": { "returned": 2 } }));
        crate::assert_loaded_suite(
            "a/b.json",
            &suite,
            &crate::Registry::new(),
            &functions(),
            Some("two"),
            Some(&dir.0),
        );
        let [path] = &dir.entries()[..] else { panic!("one receipt: {:?}", dir.entries()) };
        let r = read(path);
        assert_eq!((r.only.as_deref(), r.ran), (Some("two"), vec!["two".to_string()]));
    }

    #[test]
    fn a_kept_divergence_is_named_by_its_id() {
        let entry = crate::exceptions::all()
            .iter()
            .find(|e| e.case == "dispose-pending-factory")
            .expect("the checked-in M13 entry");
        assert_eq!(exception_id(entry), "M13");
        let other =
            crate::exceptions::MovedCallback { reason: "Something else".into(), ..entry.clone() };
        assert_eq!(exception_id(&other), "core/acrojs-host.json/dispose-pending-factory");
    }
}
